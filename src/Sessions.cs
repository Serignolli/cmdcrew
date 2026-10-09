using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CmdCrew
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int idx, IntPtr v);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr h, int idx, int v);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr procAttr, IntPtr threadAttr, bool inherit,
            uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

        // Torna "owner" o dono da janela: ela passa a ficar sempre logo acima dele no empilhamento.
        public static void SetOwner(IntPtr h, IntPtr owner)
        {
            const int GWLP_HWNDPARENT = -8;
            if (IntPtr.Size == 8) SetWindowLongPtr64(h, GWLP_HWNDPARENT, owner);
            else SetWindowLong32(h, GWLP_HWNDPARENT, owner.ToInt32());
        }

        // Limites visíveis da janela (sem a sombra invisível do Windows 10/11).
        public static Rectangle WindowBounds(IntPtr h)
        {
            RECT r;
            if (DwmGetWindowAttribute(h, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                GetWindowRect(h, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        // Traz o terminal da sala para a frente (restaurando se estiver minimizado), já na aba da sessão.
        public static void Focus(IntPtr h, int pid)
        {
            h = TerminalTab.Locate(h, pid);
            if (h == IntPtr.Zero || !IsWindow(h)) return;
            if (IsIconic(h)) ShowWindow(h, 9 /* SW_RESTORE */);
            SetForegroundWindow(h);
        }
    }

    public enum RoomState { Working, Waiting, Done, Failed, Stopped }

    // Um pedido de permissão esperando resposta (gancho PermissionRequest).
    public class PermissionAsk
    {
        public string Id, Tool, Summary;
    }

    // O que um gancho sabe de uma sessão do Claude Code. Fica em %LOCALAPPDATA%\CmdCrew\sessoes\<id>.json.
    public class SessionInfo
    {
        public string Id, Transcript, Cwd, State;
        public long Hwnd;
        public int Pid;     // processo ligado ao console da aba (para achar a aba certa no Windows Terminal)
        public DateTime Started, Changed;   // UTC
        public bool Demo;
        public PermissionAsk Ask;

        public string ProjectName
        {
            get
            {
                if (string.IsNullOrEmpty(Cwd)) return "terminal";
                string n = Path.GetFileName(Cwd.TrimEnd('\\', '/'));
                return string.IsNullOrEmpty(n) ? Cwd : n;
            }
        }

        // Última vez que algo aconteceu: o próprio arquivo, o transcript da conversa ou o de um subagente
        // (o Claude Code grava os subagentes em <sessão>\subagents\, e o transcript principal fica parado enquanto eles trabalham).
        public DateTime LastActivity
        {
            get
            {
                DateTime last = Changed;
                try
                {
                    if (!string.IsNullOrEmpty(Transcript) && File.Exists(Transcript))
                    {
                        var t = File.GetLastWriteTimeUtc(Transcript);
                        if (t > last) last = t;
                        string sub = Path.Combine(Path.GetDirectoryName(Transcript), Path.GetFileNameWithoutExtension(Transcript), "subagents");
                        if (Directory.Exists(sub))
                            foreach (var f in Directory.GetFiles(sub))
                            {
                                var ft = File.GetLastWriteTimeUtc(f);
                                if (ft > last) last = ft;
                            }
                    }
                }
                catch { }
                return last;
            }
        }

        // O Esc não dispara o Stop: a interrupção só aparece no transcript, como uma mensagem
        // "[Request interrupted by user...]" depois do início do trabalho.
        public bool Interrupted
        {
            get
            {
                if (string.IsNullOrEmpty(Transcript)) return false;
                try
                {
                    var fi = new FileInfo(Transcript);
                    if (!fi.Exists) return false;
                    CachedTail c;
                    lock (tails)
                        if (tails.TryGetValue(Transcript, out c) && c.Stamp == fi.LastWriteTimeUtc && c.Length == fi.Length)
                            return c.InterruptedAt > Started;
                    DateTime at = LastInterrupt(Transcript);
                    lock (tails) tails[Transcript] = new CachedTail { Stamp = fi.LastWriteTimeUtc, Length = fi.Length, InterruptedAt = at };
                    return at > Started;
                }
                catch { return false; }
            }
        }

        class CachedTail { public DateTime Stamp, InterruptedAt; public long Length; }
        static readonly Dictionary<string, CachedTail> tails = new Dictionary<string, CachedTail>();

        // Horário (UTC) da última interrupção registrada no final do transcript, se a última mensagem for ela.
        static DateTime LastInterrupt(string transcript)
        {
            using (var f = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long start = Math.Max(0, f.Length - 64 * 1024);
                f.Seek(start, SeekOrigin.Begin);
                var buf = new byte[f.Length - start];
                int read = 0;
                while (read < buf.Length) { int n = f.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
                var lines = Encoding.UTF8.GetString(buf, 0, read).Split('\n');
                // a última mensagem de verdade (usuário ou assistente) decide
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string l = lines[i];
                    if (l.IndexOf("\"type\":\"user\"", StringComparison.Ordinal) < 0 && l.IndexOf("\"type\":\"assistant\"", StringComparison.Ordinal) < 0) continue;
                    if (l.IndexOf("[Request interrupted by user", StringComparison.Ordinal) < 0) return DateTime.MinValue;
                    int k = l.IndexOf("\"timestamp\":\"", StringComparison.Ordinal);
                    DateTime ts;
                    if (k >= 0 && DateTime.TryParse(l.Substring(k + 13, Math.Min(30, l.Length - k - 13)).Split('"')[0],
                            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out ts))
                        return ts;
                    return DateTime.UtcNow;
                }
            }
            return DateTime.MinValue;
        }

        // Estado para desenhar. "trabalhando" só vira "parado" se a conversa foi interrompida (Esc) ou, como rede de
        // segurança, se nada mexe há muito tempo (comandos e raciocínios longos ficam vários minutos sem gravar nada).
        // "aguardando" volta a "trabalhando" assim que o transcript anda (a permissão foi respondida no terminal).
        public RoomState Resolve(double idleMinutes)
        {
            switch (State)
            {
                case "pronto": return RoomState.Done;
                case "erro": return RoomState.Failed;
                case "aguardando":
                    if (Ask != null) return RoomState.Waiting;
                    if (!Demo && LastActivity > Changed.AddSeconds(2)) return RoomState.Working;
                    return RoomState.Waiting;
                default:
                    if (Demo) return RoomState.Working;
                    if (Interrupted) return RoomState.Stopped;
                    if ((DateTime.UtcNow - LastActivity).TotalMinutes > Math.Max(Config.MinIdleMinutes, idleMinutes)) return RoomState.Stopped;
                    return RoomState.Working;
            }
        }

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object>();
            d["id"] = Id ?? "";
            d["estado"] = State ?? "trabalhando";
            d["cwd"] = Cwd ?? "";
            d["transcript"] = Transcript ?? "";
            d["hwnd"] = Hwnd.ToString(CultureInfo.InvariantCulture);
            d["pid"] = Pid.ToString(CultureInfo.InvariantCulture);
            d["inicio"] = Started.Ticks.ToString(CultureInfo.InvariantCulture);
            d["mudou"] = Changed.Ticks.ToString(CultureInfo.InvariantCulture);
            if (Demo) d["demo"] = true;
            if (Ask != null)
            {
                var a = new Dictionary<string, object>();
                a["id"] = Ask.Id ?? ""; a["ferramenta"] = Ask.Tool ?? ""; a["resumo"] = Ask.Summary ?? "";
                d["pedido"] = a;
            }
            return d;
        }

        static DateTime ReadTicks(Dictionary<string, object> d, string key)
        {
            long t;
            if (long.TryParse(Json.Str(d, key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out t) && t > 0)
                return new DateTime(t, DateTimeKind.Utc);
            return DateTime.UtcNow;
        }

        public static SessionInfo FromJson(Dictionary<string, object> d)
        {
            var s = new SessionInfo();
            s.Id = Json.Str(d, "id", "");
            s.State = Json.Str(d, "estado", "trabalhando");
            s.Cwd = Json.Str(d, "cwd", "");
            s.Transcript = Json.Str(d, "transcript", "");
            long h;
            long.TryParse(Json.Str(d, "hwnd", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out h);
            s.Hwnd = h;
            int pid;
            int.TryParse(Json.Str(d, "pid", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid);
            s.Pid = pid;
            s.Started = ReadTicks(d, "inicio");
            s.Changed = ReadTicks(d, "mudou");
            s.Demo = Json.Bool(d, "demo", false);
            var a = Json.Obj(d, "pedido");
            if (a != null) s.Ask = new PermissionAsk { Id = Json.Str(a, "id", ""), Tool = Json.Str(a, "ferramenta", ""), Summary = Json.Str(a, "resumo", "") };
            return s;
        }
    }

    public static class Sessions
    {
        static string Safe(string id)
        {
            var safe = new string((id ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            return safe.Length == 0 ? "sessao" : safe;
        }

        public static string FileFor(string sessionId) { return Path.Combine(Paths.SessionsDir, Safe(sessionId) + ".json"); }

        // Resposta da sala para um pedido de permissão: "allow", "deny" ou "terminal".
        public static string AnswerFile(string askId) { return Path.Combine(Paths.AnswersDir, Safe(askId) + ".txt"); }

        public static void Answer(string askId, string answer)
        {
            try
            {
                Directory.CreateDirectory(Paths.AnswersDir);
                File.WriteAllText(AnswerFile(askId), answer, new UTF8Encoding(false));
            }
            catch (Exception ex) { Paths.Log("resposta: " + ex.Message); }
        }

        public static SessionInfo Read(string file)
        {
            if (!File.Exists(file)) return null;
            for (int i = 0; i < 4; i++)
            {
                try { return SessionInfo.FromJson(Json.LoadObject(file)); }
                catch (IOException) { Thread.Sleep(40); }
                catch { return null; }
            }
            return null;
        }

        public static void Write(SessionInfo s)
        {
            Directory.CreateDirectory(Paths.SessionsDir);
            string f = FileFor(s.Id);
            string tmp = f + ".tmp";
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    // grava ao lado e troca, para quem lê nunca pegar o arquivo pela metade
                    File.WriteAllText(tmp, Json.Stringify(s.ToJson()), new UTF8Encoding(false));
                    if (File.Exists(f)) File.Replace(tmp, f, null); else File.Move(tmp, f);
                    return;
                }
                catch (IOException) { Thread.Sleep(40); }
                catch (UnauthorizedAccessException) { Thread.Sleep(40); }
            }
        }

        public static void Delete(string sessionId)
        {
            try { File.Delete(FileFor(sessionId)); } catch { }
        }

        // Todas as sessões conhecidas. Apaga as esquecidas há mais de 12 horas (terminal fechado sem SessionEnd).
        public static List<SessionInfo> LoadAll()
        {
            var list = new List<SessionInfo>();
            if (!Directory.Exists(Paths.SessionsDir)) return list;
            foreach (var f in Directory.GetFiles(Paths.SessionsDir, "*.json"))
            {
                var s = Read(f);
                if (s == null) continue;
                if ((DateTime.UtcNow - s.LastActivity).TotalHours > 12) { try { File.Delete(f); } catch { } continue; }
                list.Add(s);
            }
            return list;
        }

        // Alguma sessão trabalhando (para a equipe em cima do terminal)?
        public static bool AnyActive(double idleMinutes)
        {
            foreach (var s in LoadAll())
            {
                var st = s.Resolve(idleMinutes);
                if (st == RoomState.Working || st == RoomState.Waiting) return true;
            }
            return false;
        }

        // Alguma sessão ainda precisa de sala na tela?
        public static bool AnyRoom(Config cfg)
        {
            foreach (var s in LoadAll())
            {
                var st = s.Resolve(cfg.IdleMinutes);
                if (st == RoomState.Working || st == RoomState.Waiting) return true;
                if ((DateTime.UtcNow - s.Changed).TotalSeconds < cfg.DoneSeconds) return true;
            }
            return false;
        }
    }
}
