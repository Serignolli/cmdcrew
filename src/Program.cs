using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CmdCrew
{
    static class Program
    {
        const string OverlayMutex = "Local\\CmdCrewOverlay";
        const string StripMutex = "Local\\CmdCrewRooms";

        [STAThread]
        static int Main(string[] args)
        {
            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            try
            {
                switch (cmd)
                {
                    // ganchos do Claude Code
                    case "start": return Hook("trabalhando");
                    case "wait": return Hook("aguardando");
                    case "stop": return Hook("pronto");
                    case "fail": return Hook("erro");
                    case "end": return HookEnd();
                    case "permission": return HookPermission();
                    case "tool": return HookTool();
                    // janelas
                    case "run": return RunOverlay();
                    case "salas": return RunRooms();
                    case "demo": return RunDemo(args);
                    case "demo-salas": return RunRoomsDemo(args);
                    case "install":
                        ClaudeSettings.InstallHooks();
                        if (!File.Exists(Paths.ConfigFile)) new Config().Save();
                        ClaudeSettings.ApplyPhrases(Config.Load());
                        return 0;
                    case "uninstall":
                        KillOthers();
                        ClaudeSettings.RemoveAll();
                        return 0;
                    case "quit":
                        KillOthers();
                        return 0;
                    case "sheet":
                        return SpriteSheet(args.Length > 1 ? args[1] : Path.Combine(Paths.StateDir, "sprites.png"));
                    case "shot":
                        return Shot(args);
                    case "uninstall-app":
                        return UninstallApp();
                    case "setup":
                        return RunForm(() => new SetupForm(args.Length > 1 ? args[1] : null));
                    default:
                        // Baixado sozinho (sem a pasta personagens/ ao lado): é o instalador.
                        if (cmd == "config" || Installer.RunningInstalled) return RunForm(() => new ConfigForm());
                        return RunForm(() => new SetupForm());
                }
            }
            catch (Exception ex)
            {
                // Um gancho nunca deve atrapalhar o Claude Code: registra e sai em silêncio.
                Paths.Log(cmd + ": " + ex);
                try { Directory.CreateDirectory(Paths.StateDir); File.WriteAllText(Paths.ErrorFile, ex.Message, Encoding.UTF8); } catch { }
                return 1;
            }
        }

        // ---------- ganchos ----------

        static Dictionary<string, object> ReadHookInput()
        {
            string text = "";
            var t = new Thread(() =>
            {
                try
                {
                    using (var r = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8)) text = r.ReadToEnd();
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
            t.Join(2000);
            try { return Json.Parse(text) as Dictionary<string, object> ?? new Dictionary<string, object>(); }
            catch { return new Dictionary<string, object>(); }
        }

        static int Hook(string state)
        {
            var input = ReadHookInput();
            var cfg = Config.Load();
            if (!cfg.Enabled) return 0;
            string id = Json.Str(input, "session_id", "");
            var s = Sessions.Read(Sessions.FileFor(id));

            if (state != "trabalhando")
            {
                // só muda quem está trabalhando: o aviso de "parado há um tempo" (idle_prompt) chega depois do Stop
                if (s == null || s.State != "trabalhando" && s.State != "aguardando") return 0;
                if (state == "aguardando" && Json.Str(input, "notification_type", "") == "idle_prompt") return 0;
            }
            if (s == null) s = new SessionInfo();
            s.Id = id;
            var now = DateTime.UtcNow;
            if (state == "trabalhando")
            {
                // quem está em primeiro plano quando você aperta Enter é o próprio terminal
                IntPtr fg = Native.GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    s.Hwnd = fg.ToInt64();
                    try { Directory.CreateDirectory(Paths.StateDir); File.WriteAllText(Paths.TargetFile, s.Hwnd.ToString(CultureInfo.InvariantCulture)); } catch { }
                }
                if (s.State != "trabalhando" && s.State != "aguardando" || s.Started == default(DateTime)) s.Started = now;
            }
            if (state != "aguardando") s.Ask = null;
            s.State = state;
            s.Changed = now;
            s.Transcript = Json.Str(input, "transcript_path", s.Transcript);
            s.Cwd = Json.Str(input, "cwd", s.Cwd);
            Sessions.Write(s);
            if (state == "trabalhando") EnsureWindows(cfg);
            return 0;
        }

        static int HookEnd()
        {
            var input = ReadHookInput();
            Sessions.Delete(Json.Str(input, "session_id", ""));
            return 0;
        }

        // PostToolUse: uma ferramenta rodou. Se ainda havia um pedido aberto na sala, ele foi respondido
        // no próprio terminal: tira o painel da sala (o gancho de permissão que esperava percebe e sai).
        static int HookTool()
        {
            var input = ReadHookInput();
            string id = Json.Str(input, "session_id", "");
            var s = Sessions.Read(Sessions.FileFor(id));
            if (s == null || s.Ask == null) return 0;
            s.Ask = null;
            s.State = "trabalhando";
            s.Changed = DateTime.UtcNow;
            Sessions.Write(s);
            return 0;
        }

        // PermissionRequest: mostra o pedido na sala e espera você clicar em Permitir, Negar ou Terminal.
        // Sem resposta (ou com "Terminal"), não decide nada e o Claude Code segue com a pergunta normal no terminal.
        static int HookPermission()
        {
            var input = ReadHookInput();
            var cfg = Config.Load();
            if (!cfg.Enabled || !cfg.RoomsOn || !cfg.RoomPermissions) return 0;
            string id = Json.Str(input, "session_id", "");
            string askId = Json.Str(input, "tool_use_id", "");
            if (askId.Length == 0) askId = Guid.NewGuid().ToString("N");
            string tool = Json.Str(input, "tool_name", "");
            var toolInput = Json.Obj(input, "tool_input");

            var s = Sessions.Read(Sessions.FileFor(id)) ?? new SessionInfo { Id = id, Started = DateTime.UtcNow };
            s.Id = id;
            s.Transcript = Json.Str(input, "transcript_path", s.Transcript);
            s.Cwd = Json.Str(input, "cwd", s.Cwd);
            s.State = "aguardando";
            s.Changed = DateTime.UtcNow;
            s.Ask = new PermissionAsk { Id = askId, Tool = tool, Summary = Summarize(tool, toolInput) };
            string answerFile = Sessions.AnswerFile(askId);
            try { File.Delete(answerFile); } catch { }
            Sessions.Write(s);
            EnsureWindows(cfg);

            string answer = null;
            var deadline = DateTime.UtcNow.AddSeconds(585);   // o gancho tem 600 s antes do Claude Code desistir dele
            int round = 0;
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150);
                if (File.Exists(answerFile))
                {
                    try { answer = File.ReadAllText(answerFile).Trim(); File.Delete(answerFile); } catch { }
                    break;
                }
                var cur = Sessions.Read(Sessions.FileFor(id));
                // a sessão acabou, mudou de estado ou fez outro pedido: não há mais o que decidir aqui
                if (cur == null || cur.Ask == null || cur.Ask.Id != askId) break;
                // respondido direto no terminal: o resultado desta ferramenta já está no transcript
                if (++round % 6 == 0 && AnsweredInTranscript(s.Transcript, askId)) break;
            }

            // tira o pedido da sala e volta a "trabalhando"
            var after = Sessions.Read(Sessions.FileFor(id));
            if (after != null && after.Ask != null && after.Ask.Id == askId)
            {
                after.Ask = null;
                after.State = "trabalhando";
                after.Changed = DateTime.UtcNow;
                Sessions.Write(after);
            }

            if (answer == "allow" || answer == "deny")
            {
                var decision = new Dictionary<string, object>();
                decision["behavior"] = answer;
                if (answer == "deny") decision["message"] = "Negado pelo CmdCrew.";
                var hso = new Dictionary<string, object>();
                hso["hookEventName"] = "PermissionRequest";
                hso["decision"] = decision;
                var output = new Dictionary<string, object>();
                output["hookSpecificOutput"] = hso;
                var bytes = new UTF8Encoding(false).GetBytes(Json.Stringify(output));
                using (var o = Console.OpenStandardOutput()) { o.Write(bytes, 0, bytes.Length); o.Flush(); }
            }
            return 0;
        }

        // O transcript já tem o resultado (tool_result) desta chamada? Lê só o final do arquivo.
        static bool AnsweredInTranscript(string transcript, string toolUseId)
        {
            try
            {
                if (string.IsNullOrEmpty(transcript) || !File.Exists(transcript)) return false;
                using (var f = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, f.Length - 256 * 1024);
                    f.Seek(start, SeekOrigin.Begin);
                    var buf = new byte[f.Length - start];
                    int read = 0;
                    while (read < buf.Length) { int n = f.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
                    string tail = Encoding.UTF8.GetString(buf, 0, read);
                    return tail.Contains("\"tool_use_id\":\"" + toolUseId + "\"");
                }
            }
            catch { return false; }
        }

        // Uma linha que diz o que a ferramenta quer fazer (o comando, o arquivo, o endereço...).
        static string Summarize(string tool, Dictionary<string, object> ti)
        {
            if (ti == null) return tool;
            foreach (var key in new[] { "command", "file_path", "url", "query", "pattern", "path", "description" })
            {
                string v = Json.Str(ti, key, null);
                if (!string.IsNullOrEmpty(v)) return key == "file_path" || key == "path" ? Path.GetFileName(v.TrimEnd('\\', '/')) : v;
            }
            return tool;
        }

        // ---------- janelas ----------

        static void EnsureWindows(Config cfg)
        {
            if (cfg.CrewOn && !Running(OverlayMutex)) LaunchDetached("run");
            if (cfg.RoomsOn && !Running(StripMutex)) LaunchDetached("salas");
        }

        static bool Running(string mutex)
        {
            Mutex m;
            if (!Mutex.TryOpenExisting(mutex, out m)) return false;
            m.Dispose();
            return true;
        }

        // As janelas saem do "job" do Claude Code (CREATE_BREAKAWAY_FROM_JOB): sem isso elas são encerradas junto
        // com a sessão (no "claude -p", por exemplo) antes de se despedir. Se não der, abre do jeito normal.
        static void LaunchDetached(string arguments)
        {
            try
            {
                var si = new Native.STARTUPINFO();
                si.cb = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.STARTUPINFO));
                Native.PROCESS_INFORMATION pi;
                const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000, CREATE_NEW_PROCESS_GROUP = 0x200;
                var cmdLine = new StringBuilder("\"" + Paths.Exe + "\" " + arguments);
                if (Native.CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, CREATE_BREAKAWAY_FROM_JOB | CREATE_NEW_PROCESS_GROUP,
                        IntPtr.Zero, Path.GetDirectoryName(Paths.Exe), ref si, out pi))
                {
                    Native.CloseHandle(pi.hProcess); Native.CloseHandle(pi.hThread);
                    return;
                }
            }
            catch { }
            Launch(arguments);
        }

        public static void Launch(string arguments)
        {
            // UseShellExecute = true: o processo filho não herda os pipes do gancho.
            var psi = new ProcessStartInfo(Paths.Exe, arguments);
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        static int RunOverlay()
        {
            bool created;
            using (var m = new Mutex(true, OverlayMutex, out created))
            {
                if (!created) return 0;
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.Run(new OverlayForm(Config.Load(), true, 0));
                m.ReleaseMutex();
            }
            // Se um novo prompt chegou enquanto eles iam embora, volta ao trabalho.
            var cfg = Config.Load();
            if (cfg.Enabled && cfg.CrewOn && Sessions.AnyActive(cfg.IdleMinutes)) LaunchDetached("run");
            return 0;
        }

        static int RunRooms()
        {
            bool created;
            using (var m = new Mutex(true, StripMutex, out created))
            {
                if (!created) return 0;
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.Run(new StripForm());
                m.ReleaseMutex();
            }
            // Se um novo prompt chegou enquanto a última sala saía, abre de novo.
            var cfg = Config.Load();
            if (cfg.Enabled && cfg.RoomsOn && Sessions.AnyRoom(cfg)) LaunchDetached("salas");
            return 0;
        }

        // Equipe de demonstração em cima da janela ativa: "demo [segundos] [config.json]".
        static int RunDemo(string[] args)
        {
            double seconds = 12;
            if (args.Length > 1) double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            var cfg = args.Length > 2 ? Config.Load(args[2]) : Config.Load();
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            var form = new OverlayForm(cfg, false, Math.Max(2, seconds));
            // Na demonstração, eles aparecem sobre a janela que estava ativa (a própria tela de configuração).
            form.FixedTarget = Native.GetForegroundWindow();
            Application.Run(form);
            return 0;
        }

        // Salas de mentira para ver funcionando sem o Claude Code: "demo-salas [n] [segundos]".
        // A segunda sala pede uma permissão no meio do caminho.
        static int RunRoomsDemo(string[] args)
        {
            int n = 2;
            double seconds = 12;
            if (args.Length > 1) int.TryParse(args[1], out n);
            if (args.Length > 2) double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            n = Math.Max(1, Math.Min(6, n));
            string[] names = { "meu-site", "api-pagamentos", "jogo-da-cobrinha", "scripts", "app-mobile", "relatorios" };
            var list = new List<SessionInfo>();
            var start = DateTime.UtcNow;
            for (int i = 0; i < n; i++)
            {
                var s = new SessionInfo { Id = "demo-" + i, Cwd = "C:\\demo\\" + names[i % names.Length], State = "trabalhando",
                    Started = start.AddSeconds(-37 * i - 5), Changed = start, Demo = true };
                Sessions.Write(s);
                list.Add(s);
                if (i == 0 && !Running(StripMutex)) Launch("salas");
                Thread.Sleep(900);
            }
            for (int i = 0; i < n; i++)
            {
                double at = seconds + i * 4;
                while ((DateTime.UtcNow - start).TotalSeconds < at)
                {
                    if (n > 1 && i == 0 && list[1].State == "trabalhando" && (DateTime.UtcNow - start).TotalSeconds > seconds * 0.5)
                    {
                        list[1].State = "aguardando"; list[1].Changed = DateTime.UtcNow;
                        list[1].Ask = new PermissionAsk { Id = "demo-pedido", Tool = "Bash", Summary = "npm test -- --coverage" };
                        Sessions.Write(list[1]);
                    }
                    // a resposta da demonstração só tira o pedido da sala
                    if (list.Count > 1 && list[1].Ask != null && File.Exists(Sessions.AnswerFile("demo-pedido")))
                    {
                        try { File.Delete(Sessions.AnswerFile("demo-pedido")); } catch { }
                        list[1].Ask = null; list[1].State = "trabalhando"; list[1].Changed = DateTime.UtcNow; Sessions.Write(list[1]);
                    }
                    Thread.Sleep(200);
                }
                list[i].Ask = null;
                list[i].State = n > 2 && i == n - 1 ? "erro" : "pronto";
                list[i].Changed = DateTime.UtcNow;
                Sessions.Write(list[i]);
            }
            Thread.Sleep((int)(Config.Load().DoneSeconds * 1000) + 2500);
            foreach (var s in list) Sessions.Delete(s.Id);
            return 0;
        }

        // Captura uma sala num PNG (para conferir desenhos): "shot <tema> <estado> <arquivo> [segundos] [quadros]".
        // Com quadros > 1, salva <arquivo>-00.png, -01.png... (para montar GIFs do site).
        static int Shot(string[] args)
        {
            var theme = RoomTheme.Find(args.Length > 1 ? args[1] : "escritorio");
            string stName = args.Length > 2 ? args[2] : "trabalhando";
            var st = stName == "pronto" ? RoomState.Done : stName == "aguardando" || stName == "permissao" ? RoomState.Waiting : RoomState.Working;
            string outPath = args.Length > 3 ? args[3] : Path.Combine(Paths.StateDir, "sala.png");
            double secs = 2.5;
            int frames = 1;
            if (args.Length > 4) double.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out secs);
            if (args.Length > 5) int.TryParse(args[5], out frames);
            var all = Character.LoadAll(null);
            var ch = all.FirstOrDefault(c => c.Id == theme.CharacterId) ?? all.FirstOrDefault(c => c.Id == theme.FallbackId) ?? all[0];
            var room = new Room(theme, ch, 0, 1) { ProjectName = "meu-projeto", Started = DateTime.UtcNow.AddSeconds(-83) };
            if (stName == "permissao") room.Ask = new PermissionAsk { Id = "x", Tool = "Bash", Summary = "npm test -- --coverage" };
            const int unit = 3;
            room.SetScale(unit);
            room.Enter = 1;
            double t = 0;
            for (; t < secs; t += 0.033) { if (t > secs - 1.2 && st != RoomState.Working) room.SetState(st, DateTime.UtcNow); room.Update(0.033f, true); }
            using (var f = new System.Drawing.Font(Theme.PixelFamily, 6.2f * unit, System.Drawing.GraphicsUnit.Pixel))
            using (var sm = new System.Drawing.Font(Theme.PixelFamily, 5.4f * unit, System.Drawing.GraphicsUnit.Pixel))
                for (int i = 0; i < Math.Max(1, frames); i++)
                {
                    if (i > 0) for (int k = 0; k < 3; k++) room.Update(0.033f, true);   // ~10 quadros por segundo
                    using (var bmp = new System.Drawing.Bitmap(room.PixelWidth, room.PixelHeight))
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        room.Draw(g, 0, 0, f, sm);
                        string p = frames > 1 ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)),
                            Path.GetFileNameWithoutExtension(outPath) + "-" + i.ToString("00") + ".png") : outPath;
                        bmp.Save(p, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            return 0;
        }

        // Folha com todos os personagens/skins/quadros, útil para conferir desenhos novos.
        static int SpriteSheet(string outPath)
        {
            var errors = new List<string>();
            var all = Character.LoadAll(errors);
            foreach (var e in errors) Paths.Log("sheet: " + e);
            const int scale = 4, pad = 8;
            // Cada personagem pode ter um tamanho de desenho diferente; os detalhados ficam com pontos menores.
            Func<Character, int> sc = c => Math.Max(2, c.PixelScale(scale));
            var kinds = all.SelectMany(c => c.Skins).SelectMany(s => s.Frames.Keys).Distinct()
                .OrderBy(k => k == "andar" ? 0 : k == "parado" ? 2 : 1).ThenBy(k => k).ToArray();
            Func<Skin, string, int> count = (s, k) => s.Frames.ContainsKey(k) ? s.Frames[k].Count : 0;
            int cellW = all.Select(c => c.Width * sc(c) + pad).DefaultIfEmpty(1).Max();
            int cols = all.SelectMany(c => c.Skins).Select(s => kinds.Sum(k => count(s, k))).DefaultIfEmpty(1).Max();
            int totalH = all.Sum(c => c.Skins.Count * (Math.Max(c.Height * sc(c), 30) + pad));
            using (var bmp = new System.Drawing.Bitmap(cellW * cols + 160, Math.Max(1, totalH) + pad))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            using (var font = new System.Drawing.Font("Segoe UI", 8f))
            {
                g.Clear(System.Drawing.Color.FromArgb(235, 240, 245));
                int y = pad;
                foreach (var c in all)
                    foreach (var s in c.Skins)
                    {
                        g.DrawString(c.Name + "\n" + s.Name, font, System.Drawing.Brushes.Black, 4, y);
                        int x = 160;
                        foreach (var kind in kinds)
                        {
                            if (count(s, kind) == 0) continue;
                            foreach (var b in c.Render(s, kind, sc(c), false)) { g.DrawImage(b, x, y); x += cellW; b.Dispose(); }
                        }
                        y += Math.Max(c.Height * sc(c), 30) + pad;
                    }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            return errors.Count == 0 ? 0 : 1;
        }

        static int RunForm(Func<Form> create)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(create());
            return 0;
        }

        // Chamado por "Aplicativos instalados" do Windows.
        static int UninstallApp()
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            ConfirmUninstall(null);
            return 0;
        }

        // Usado por "Aplicativos instalados" e pelo link da tela de configuração.
        public static bool ConfirmUninstall(IWin32Window owner)
        {
            if (MessageBox.Show(owner, "Desinstalar o CmdCrew?\n\nA equipe sai do Claude Code e as frases do spinner voltam ao normal.",
                    "CmdCrew", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            Installer.Uninstall();
            MessageBox.Show(owner, "Pronto, a equipe foi dispensada. Até a próxima!", "CmdCrew", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        public static void KillOthers()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("CmdCrew"))
            {
                try { if (p.Id != me) p.Kill(); } catch { }
            }
        }
    }
}
