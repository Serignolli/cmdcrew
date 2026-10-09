using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace CmdCrew
{
    // Volta ao terminal de uma sessão. O HWND gravado no gancho não basta: várias sessões podem ser abas da mesma
    // janela do Windows Terminal, e a aba pode mudar de janela. Por isso o gancho também guarda o pid de um processo
    // ligado ao console da aba; na hora de ir até ela, lemos o título atual desse console e selecionamos a aba com esse nome.
    static class TerminalTab
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] static extern bool FreeConsole();
        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetConsoleTitle(StringBuilder title, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder name, int size);
        [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32First(IntPtr snap, ref PROCESSENTRY32 e);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32Next(IntPtr snap, ref PROCESSENTRY32 e);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PROCESSENTRY32
        {
            public int dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public int th32ModuleID, cntThreads, th32ParentProcessID, pcPriClassBase, dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }

        static int ParentOf(int pid)
        {
            IntPtr snap = CreateToolhelp32Snapshot(0x2 /* TH32CS_SNAPPROCESS */, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return 0;
            try
            {
                var e = new PROCESSENTRY32 { dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                for (bool ok = Process32First(snap, ref e); ok; ok = Process32Next(snap, ref e))
                    if (e.th32ProcessID == pid) return e.th32ParentProcessID;
            }
            finally { Native.CloseHandle(snap); }
            return 0;
        }

        // Liga-se por um instante ao console de "pid" (sem tratar Ctrl+C dele) e faz "read".
        static T WithConsole<T>(int pid, Func<T> read, T fallback)
        {
            if (pid <= 0) return fallback;
            FreeConsole();
            if (!AttachConsole(pid)) return fallback;
            try { SetConsoleCtrlHandler(IntPtr.Zero, true); return read(); }
            catch { return fallback; }
            finally { FreeConsole(); }
        }

        // No gancho (que não tem console próprio): o primeiro ancestral ligado a um console, ou 0.
        public static int CapturePid()
        {
            try
            {
                int pid = ParentOf(System.Diagnostics.Process.GetCurrentProcess().Id);
                for (int i = 0; i < 6 && pid > 0; i++, pid = ParentOf(pid))
                    if (WithConsole(pid, () => GetConsoleWindow() != IntPtr.Zero, false)) return pid;
            }
            catch { }
            return 0;
        }

        // O Claude Code põe um símbolo animado (✳, ⠂, ⠐...) antes do título; compara sem ele.
        static string Bare(string t)
        {
            t = (t ?? "").Trim();
            int i = 0;
            while (i < t.Length && !char.IsLetterOrDigit(t[i])) i++;
            return t.Substring(i).Trim();
        }

        // Acha a janela da sessão de "pid" e já deixa a aba dela selecionada. O HWND gravado no gancho ("hint") pode
        // estar errado (aba arrastada para outra janela, outra janela na frente na hora do Enter), por isso procura a
        // aba pelo título em todas as janelas do Windows Terminal, começando pela gravada. Sem achar, devolve "hint".
        public static IntPtr Locate(IntPtr hint, int pid)
        {
            try
            {
                string title = "";
                IntPtr console = WithConsole(pid, () =>
                {
                    var sb = new StringBuilder(1024);
                    if (GetConsoleTitle(sb, sb.Capacity) > 0) title = sb.ToString();
                    return GetConsoleWindow();
                }, IntPtr.Zero);
                // console clássico (conhost): a janela do console é a própria janela do terminal. No Windows Terminal
                // ela é uma janela escondida de pseudoconsole ("PseudoConsoleWindow"), que não serve.
                var cls = new StringBuilder(64);
                if (console != IntPtr.Zero && GetClassName(console, cls, cls.Capacity) > 0
                    && cls.ToString() == "ConsoleWindowClass" && Native.IsWindowVisible(console)) return console;
                if (title.Length == 0) return hint;

                var windows = new System.Collections.Generic.List<AutomationElement>();
                if (hint != IntPtr.Zero && Native.IsWindow(hint)) { try { windows.Add(AutomationElement.FromHandle(hint)); } catch { } }
                foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children,
                        new PropertyCondition(AutomationElement.ClassNameProperty, "CASCADIA_HOSTING_WINDOW_CLASS")))
                    if (new IntPtr(w.Current.NativeWindowHandle) != hint) windows.Add(w);

                var tabCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                AutomationElement loose = null, looseWin = null;
                foreach (var w in windows)
                    foreach (AutomationElement t in w.FindAll(TreeScope.Descendants, tabCond))
                    {
                        string name = t.Current.Name ?? "";
                        if (name == title) return Pick(w, t);
                        if (loose == null && Bare(name).Length > 0 && Bare(name) == Bare(title)) { loose = t; looseWin = w; }
                    }
                if (loose != null) return Pick(looseWin, loose);
            }
            catch (Exception ex) { Paths.Log("aba do terminal: " + ex.Message); }
            return hint;
        }

        static IntPtr Pick(AutomationElement window, AutomationElement tab)
        {
            object pattern;
            if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern)) ((SelectionItemPattern)pattern).Select();
            return new IntPtr(window.Current.NativeWindowHandle);
        }
    }
}
