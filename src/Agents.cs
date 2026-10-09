using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CmdCrew
{
    // Um agente de terminal que a equipe acompanha. Cada um avisa o CmdCrew do seu jeito:
    // ganchos no settings.json (Claude Code, Qwen Code, Gemini CLI), hooks.json (Codex),
    // um arquivo de ganchos próprio (Copilot CLI) ou um plugin (OpenCode).
    public class Agent
    {
        public string Id, Name, Executable;
        public string[] ConfigDirs;                 // onde ele guarda a configuração (se existir, ele está instalado)
        public Func<bool> Installed;
        public Action Install, Remove;

        // Está neste computador? (pasta de configuração ou o executável no PATH)
        public bool Detected
        {
            get
            {
                if (ConfigDirs.Any(Directory.Exists)) return true;
                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                return path.Split(';').Any(p =>
                {
                    try
                    {
                        string d = p.Trim();
                        return d.Length > 0 && new[] { ".exe", ".cmd", ".ps1", "" }.Any(ext => File.Exists(Path.Combine(d, Executable + ext)));
                    }
                    catch { return false; }
                });
            }
        }

        public override string ToString() { return Name; }
    }

    public static class Agents
    {
        static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }

        static string EnvOr(string variable, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        static string ClaudeDir { get { return Path.GetDirectoryName(Paths.ClaudeSettings); } }
        static string QwenSettings { get { return Path.Combine(Home, ".qwen", "settings.json"); } }
        static string GeminiSettings { get { return Path.Combine(EnvOr("GEMINI_CLI_HOME", Path.Combine(Home, ".gemini")), "settings.json"); } }
        static string CodexHooks { get { return Path.Combine(EnvOr("CODEX_HOME", Path.Combine(Home, ".codex")), "hooks.json"); } }
        static string CopilotHooks { get { return Path.Combine(EnvOr("COPILOT_HOME", Path.Combine(Home, ".copilot")), "hooks", "cmdcrew.json"); } }
        static string OpenCodeDir { get { return Path.Combine(EnvOr("XDG_CONFIG_HOME", Path.Combine(Home, ".config")), "opencode"); } }
        static string OpenCodePlugin { get { return Path.Combine(OpenCodeDir, "plugins", "cmdcrew.js"); } }

        // Linha de comando para quem roda o gancho num shell (o Windows usa o cmd.exe).
        static string Shell(string command, string agent) { return "\"" + Paths.Exe + "\" " + command + " " + agent; }

        // evento do agente -> comando do CmdCrew
        static readonly string[][] QwenEvents = {
            new[] { "UserPromptSubmit", "start" }, new[] { "Notification", "wait" }, new[] { "PermissionRequest", "permission" },
            new[] { "PostToolUse", "tool" }, new[] { "Stop", "stop" }, new[] { "StopFailure", "fail" }, new[] { "SessionEnd", "end" } };
        static readonly string[][] GeminiEvents = {
            new[] { "BeforeAgent", "start" }, new[] { "Notification", "wait" }, new[] { "AfterTool", "tool" },
            new[] { "AfterAgent", "stop" }, new[] { "SessionEnd", "end" } };
        static readonly string[][] CodexEvents = {
            new[] { "UserPromptSubmit", "start" }, new[] { "PermissionRequest", "permission" }, new[] { "PostToolUse", "tool" },
            new[] { "Stop", "stop" }, new[] { "SessionEnd", "end" } };
        static readonly string[][] CopilotEvents = {
            new[] { "userPromptSubmitted", "start" }, new[] { "permissionRequest", "permission" }, new[] { "postToolUse", "tool" },
            new[] { "agentStop", "stop" }, new[] { "errorOccurred", "fail" }, new[] { "sessionEnd", "end" } };

        static List<Agent> all;
        public static List<Agent> All
        {
            get
            {
                if (all != null) return all;
                return all = new List<Agent>
                {
                    new Agent { Id = "claude", Name = "Claude Code", Executable = "claude", ConfigDirs = new[] { ClaudeDir },
                        Installed = ClaudeSettings.HooksInstalled, Install = ClaudeSettings.InstallHooks,
                        Remove = () => { if (File.Exists(Paths.ClaudeSettings)) ClaudeSettings.RemoveAll(); } },
                    JsonHooks("qwen", "Qwen Code", "qwen", () => QwenSettings, QwenEvents, (cmd, blocking) =>
                    {
                        var h = Hook(cmd, "qwen");
                        h["name"] = "cmdcrew";
                        h["timeout"] = blocking ? 600 : 15;     // segundos
                        if (!blocking) h["async"] = true;
                        return h;
                    }),
                    JsonHooks("gemini", "Gemini CLI", "gemini", () => GeminiSettings, GeminiEvents, (cmd, blocking) =>
                    {
                        var h = Hook(cmd, "gemini");
                        h["name"] = "cmdcrew";
                        h["timeout"] = 15000;                   // milissegundos
                        return h;
                    }),
                    JsonHooks("codex", "Codex CLI", "codex", () => CodexHooks, CodexEvents, (cmd, blocking) =>
                    {
                        var h = Hook(cmd, "codex");
                        h["timeout"] = blocking ? 600 : cmd == "end" ? 2 : 15;
                        if (!blocking) h["async"] = true;
                        return h;
                    }),
                    new Agent { Id = "opencode", Name = "OpenCode", Executable = "opencode", ConfigDirs = new[] { OpenCodeDir },
                        Installed = () => File.Exists(OpenCodePlugin), Install = InstallOpenCode,
                        Remove = () => { if (File.Exists(OpenCodePlugin)) File.Delete(OpenCodePlugin); } },
                    new Agent { Id = "copilot", Name = "Copilot CLI", Executable = "copilot", ConfigDirs = new[] { Path.GetDirectoryName(Path.GetDirectoryName(CopilotHooks)) },
                        Installed = () => File.Exists(CopilotHooks), Install = InstallCopilot,
                        Remove = () => { if (File.Exists(CopilotHooks)) File.Delete(CopilotHooks); } },
                };
            }
        }

        public static Agent Find(string id) { return All.FirstOrDefault(a => a.Id == id); }

        static Dictionary<string, object> Hook(string cmd, string agent)
        {
            var h = new Dictionary<string, object>();
            h["type"] = "command";
            h["command"] = Shell(cmd, agent);
            return h;
        }

        // Agentes que usam o formato do Claude Code: { "hooks": { "Evento": [ { "hooks": [ {...} ] } ] } }.
        static Agent JsonHooks(string id, string name, string exe, Func<string> path, string[][] events, Func<string, bool, Dictionary<string, object>> make)
        {
            return new Agent
            {
                Id = id, Name = name, Executable = exe, ConfigDirs = new[] { Path.GetDirectoryName(path()) },
                Installed = () => ClaudeSettings.HasOurHooks(path()),
                Install = () =>
                {
                    var s = ClaudeSettings.LoadWithBackup(path());
                    ClaudeSettings.StripOurHooks(s);
                    var hooks = Json.Obj(s, "hooks");
                    if (hooks == null) { hooks = new Dictionary<string, object>(); s["hooks"] = hooks; }
                    foreach (var ev in events)
                    {
                        var list = Json.Arr(hooks, ev[0]) ?? new List<object>();
                        var group = new Dictionary<string, object>();
                        group["hooks"] = new object[] { make(ev[1], ev[1] == "permission") };
                        list.Add(group);
                        hooks[ev[0]] = list;
                    }
                    Json.Save(path(), s);
                },
                Remove = () =>
                {
                    if (!File.Exists(path())) return;
                    var s = ClaudeSettings.LoadWithBackup(path());
                    ClaudeSettings.StripOurHooks(s);
                    Json.Save(path(), s);
                }
            };
        }

        // Copilot CLI: um arquivo só nosso em ~/.copilot/hooks/ (ele junta todos os .json dessa pasta).
        static void InstallCopilot()
        {
            var hooks = new Dictionary<string, object>();
            foreach (var ev in CopilotEvents)
            {
                var h = new Dictionary<string, object>();
                h["type"] = "command";
                h["powershell"] = "& '" + Paths.Exe.Replace("'", "''") + "' " + ev[1] + " copilot";
                h["bash"] = "'" + Paths.Exe.Replace('\\', '/') + "' " + ev[1] + " copilot";
                h["timeoutSec"] = ev[1] == "permission" ? 600 : 15;
                hooks[ev[0]] = new object[] { h };
            }
            var d = new Dictionary<string, object>();
            d["version"] = 1;
            d["hooks"] = hooks;
            Json.Save(CopilotHooks, d);
        }

        // OpenCode: o plugin (agentes/opencode.js, embutido no .exe) com o caminho deste executável.
        static void InstallOpenCode()
        {
            string text;
            using (var st = typeof(Agents).Assembly.GetManifestResourceStream("agentes/opencode.js"))
            using (var r = new StreamReader(st, Encoding.UTF8)) text = r.ReadToEnd();
            text = text.Replace("__CMDCREW_EXE__", Json.Stringify(Paths.Exe));
            Directory.CreateDirectory(Path.GetDirectoryName(OpenCodePlugin));
            File.WriteAllText(OpenCodePlugin, text, new UTF8Encoding(false));
        }

        // Liga os agentes da lista (ids separados por vírgula; "detectados" = o Claude Code e todos os encontrados).
        public static List<Agent> Parse(string ids)
        {
            if (string.IsNullOrEmpty(ids) || ids == "detectados")
                return All.Where(a => a.Id == "claude" || a.Detected).ToList();
            var want = ids.Split(',').Select(x => x.Trim()).ToList();
            return All.Where(a => want.Contains(a.Id)).ToList();
        }

        public static void RemoveAll()
        {
            foreach (var a in All)
                try { a.Remove(); } catch (Exception ex) { Paths.Log("remover " + a.Id + ": " + ex.Message); }
        }
    }
}
