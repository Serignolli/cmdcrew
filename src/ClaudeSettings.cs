using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CmdCrew
{
    // Mexe só nas partes do ~/.claude/settings.json que são nossas:
    // os ganchos que apontam para CmdCrew.exe, spinnerVerbs e spinnerTipsOverride.
    public static class ClaudeSettings
    {
        // Os nomes antigos (Oompa-Loompas e Salinhas, antes do CmdCrew) também são reconhecidos,
        // para a atualização não deixar ganchos duplicados para trás.
        static readonly string[] Markers = { "CmdCrew.exe", "OompaLoompas.exe", "Salinhas.exe" };

        static readonly string[][] HookEvents = {
            new[] { "UserPromptSubmit", "start" },
            new[] { "Notification", "wait" },
            new[] { "PermissionRequest", "permission" },
            new[] { "PostToolUse", "tool" },
            new[] { "Stop", "stop" },
            new[] { "StopFailure", "fail" },
            new[] { "SessionEnd", "end" },
        };

        static Dictionary<string, object> Load()
        {
            string path = Paths.ClaudeSettings;
            string backup = path + ".antes-cmdcrew.bak";
            if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
            return Json.LoadObject(path);
        }

        static void Save(Dictionary<string, object> s) { Json.Save(Paths.ClaudeSettings, s); }

        static bool IsOurs(object hook)
        {
            var h = hook as Dictionary<string, object>;
            string cmd = h == null ? "" : Json.Str(h, "command", "");
            return Markers.Any(m => cmd.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void StripOurHooks(Dictionary<string, object> s)
        {
            var hooks = Json.Obj(s, "hooks");
            if (hooks == null) return;
            foreach (var ev in hooks.Keys.ToList())
            {
                var groups = Json.Arr(hooks, ev);
                if (groups == null) continue;
                var kept = new List<object>();
                foreach (var g in groups)
                {
                    var gd = g as Dictionary<string, object>;
                    var hs = gd == null ? null : Json.Arr(gd, "hooks");
                    if (hs == null) { kept.Add(g); continue; }
                    var mine = hs.Where(IsOurs).ToList();
                    if (mine.Count == 0) { kept.Add(g); continue; }
                    var rest = hs.Where(h => !IsOurs(h)).ToList();
                    if (rest.Count == 0) continue;
                    gd["hooks"] = rest;
                    kept.Add(gd);
                }
                if (kept.Count == 0) hooks.Remove(ev);
                else hooks[ev] = kept;
            }
            if (hooks.Count == 0) s.Remove("hooks");
        }

        public static bool HooksInstalled()
        {
            try
            {
                var hooks = Json.Obj(Json.LoadObject(Paths.ClaudeSettings), "hooks");
                if (hooks == null) return false;
                foreach (var ev in hooks.Keys)
                    foreach (var g in Json.Arr(hooks, ev) ?? new List<object>())
                    {
                        var hs = Json.Arr(g as Dictionary<string, object>, "hooks");
                        if (hs != null && hs.Any(IsOurs)) return true;
                    }
            }
            catch { }
            return false;
        }

        public static void InstallHooks()
        {
            var s = Load();
            StripOurHooks(s);
            var hooks = Json.Obj(s, "hooks");
            if (hooks == null) { hooks = new Dictionary<string, object>(); s["hooks"] = hooks; }
            foreach (var pair in HookEvents)
            {
                var list = Json.Arr(hooks, pair[0]) ?? new List<object>();
                var h = new Dictionary<string, object>();
                h["type"] = "command";
                h["command"] = Paths.Exe;
                h["args"] = new object[] { pair[1] };
                if (pair[1] == "permission")
                {
                    // este espera a resposta na sala, então precisa bloquear (e ter tempo para você decidir)
                    h["timeout"] = 600;
                }
                else
                {
                    h["async"] = true;
                    h["timeout"] = 15;
                }
                var group = new Dictionary<string, object>();
                group["hooks"] = new object[] { h };
                list.Add(group);
                hooks[pair[0]] = list;
            }
            Save(s);
        }

        public static void RemoveAll()
        {
            var s = Load();
            StripOurHooks(s);
            s.Remove("spinnerVerbs");
            s.Remove("spinnerTipsOverride");
            Save(s);
        }

        public static void ApplyPhrases(Config cfg)
        {
            var ch = Character.Find(Character.LoadAll(null), cfg.Character);
            var s = Load();
            s.Remove("spinnerVerbs");
            s.Remove("spinnerTipsOverride");
            if (cfg.Enabled && cfg.TerminalPhrases && ch != null)
            {
                var verbs = cfg.Phrases(ch, "verbos");
                if (verbs.Count > 0)
                {
                    var v = new Dictionary<string, object>();
                    v["mode"] = cfg.VerbMode == "append" ? "append" : "replace";
                    v["verbs"] = verbs.ToArray();
                    s["spinnerVerbs"] = v;
                }
                var tips = cfg.Phrases(ch, "dicas");
                if (cfg.TerminalTips && tips.Count > 0)
                {
                    var t = new Dictionary<string, object>();
                    t["tips"] = tips.ToArray();
                    t["excludeDefault"] = false;
                    s["spinnerTipsOverride"] = t;
                }
            }
            Save(s);
        }
    }
}
