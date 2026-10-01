using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace CmdCrew
{
    // Instalação para usuário final: o CmdCrew-Setup.exe traz os personagens embutidos,
    // se copia para %LOCALAPPDATA%\Programs\CmdCrew e aparece em "Aplicativos instalados".
    public static class Installer
    {
        public const string Version = "1.0.0";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CmdCrew";

        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CmdCrew"); }
        }
        public static string InstalledExe { get { return Path.Combine(InstallDir, "bin", "CmdCrew.exe"); } }
        static string Shortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "CmdCrew.lnk"); }
        }

        public static bool IsInstalled { get { return File.Exists(InstalledExe); } }

        // Rodando de dentro da instalação (ou da pasta do projeto, com personagens/ ao lado)?
        public static bool RunningInstalled { get { return Directory.Exists(Paths.CharactersDir); } }

        public static bool ClaudeCodeFound
        {
            get
            {
                string dir = Path.GetDirectoryName(Paths.ClaudeSettings);
                if (Directory.Exists(dir)) return true;
                // o executável do claude no PATH também vale
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                return path.Split(';').Any(p =>
                {
                    try { return File.Exists(Path.Combine(p.Trim(), "claude.exe")) || File.Exists(Path.Combine(p.Trim(), "claude.cmd")); }
                    catch { return false; }
                });
            }
        }

        // Copia os arquivos, grava a configuração escolhida e instala os ganchos pelo executável instalado
        // (o caminho dele é o que fica gravado no settings.json do Claude Code).
        public static void Install(Config chosen, Action<string> progress)
        {
            progress("Arrumando a oficina...");
            foreach (var p in Process.GetProcessesByName("CmdCrew"))
                try { if (p.Id != Process.GetCurrentProcess().Id) p.Kill(); } catch { }
            Directory.CreateDirectory(Path.Combine(InstallDir, "bin"));
            string chars = Path.Combine(InstallDir, "personagens");
            Directory.CreateDirectory(chars);

            progress("Copiando o programa...");
            string self = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, InstalledExe, true);

            progress("Chamando a equipe...");
            foreach (var f in Character.EmbeddedFiles())
                File.WriteAllText(Path.Combine(chars, f.Key), f.Value, new UTF8Encoding(false));

            // Atualização: mantém a configuração antiga (frases editadas etc.) e só troca a escolha da equipe.
            string cfgPath = Path.Combine(InstallDir, "config.json");
            var cfg = File.Exists(cfgPath) ? Config.Load(cfgPath) : new Config();
            cfg.Character = chosen.Character; cfg.Skin = chosen.Skin; cfg.Count = chosen.Count; cfg.Mode = chosen.Mode; cfg.Enabled = true;
            cfg.Save(cfgPath);

            progress("Avisando o Claude Code...");
            var psi = new ProcessStartInfo(InstalledExe, "install") { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit(30000);
                if (p.ExitCode != 0)
                {
                    string msg = File.Exists(Paths.ErrorFile) ? File.ReadAllText(Paths.ErrorFile) : "erro desconhecido";
                    throw new InvalidOperationException("Não consegui configurar o Claude Code: " + msg);
                }
            }

            progress("Criando o atalho...");
            try { CreateShortcut(Shortcut, InstalledExe, "config", "CmdCrew: configurar a equipe do Claude Code"); } catch (Exception ex) { Paths.Log("atalho: " + ex.Message); }
            try { RegisterUninstall(); } catch (Exception ex) { Paths.Log("registro: " + ex.Message); }
            progress("Pronto!");
        }

        static void RegisterUninstall()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "CmdCrew para o Claude Code");
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", "Serignolli");
                k.SetValue("DisplayIcon", InstalledExe);
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" uninstall-app");
                k.SetValue("URLInfoAbout", "https://serignolli.com");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long size = new DirectoryInfo(InstallDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;
                k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
            }
        }

        // Atalho via WScript.Shell (por reflexão, para não depender de referências COM na compilação).
        static void CreateShortcut(string lnk, string target, string args, string description)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }

        // Tira tudo do Claude Code, apaga atalho e registro e, depois que este processo sair, a pasta.
        // O .exe em uso trava a pasta, então o cmd tenta de novo a cada segundo por até 30 segundos.
        public static void Uninstall()
        {
            foreach (var p in Process.GetProcessesByName("CmdCrew"))
                try { if (p.Id != Process.GetCurrentProcess().Id) p.Kill(); } catch { }
            ClaudeSettings.RemoveAll();
            try { if (File.Exists(Shortcut)) File.Delete(Shortcut); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            if (Directory.Exists(InstallDir))
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c for /l %i in (1,1,30) do (ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"" + InstallDir + "\" 2> nul & if not exist \"" + InstallDir + "\" exit)")
                { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(psi);
            }
        }
    }
}
