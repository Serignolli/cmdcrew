package main

import (
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
)

// Os agentes de terminal que a equipe acompanha. Mesmos arquivos e formatos do programa do Windows (src/Agents.cs).

type Agent struct {
	ID, Name, Executable string
	ConfigDirs           func() []string
	Installed            func() bool
	Install              func() error
	Remove               func() error
}

// Está neste computador? (pasta de configuração ou o executável no PATH)
func (a *Agent) Detected() bool {
	for _, d := range a.ConfigDirs() {
		if dirExists(d) {
			return true
		}
	}
	_, err := exec.LookPath(a.Executable)
	return err == nil
}

func envOr(name, fallback string) string {
	if v := os.Getenv(name); v != "" {
		return v
	}
	return fallback
}

func qwenSettings() string { return filepath.Join(home(), ".qwen", "settings.json") }
func geminiSettings() string {
	return filepath.Join(envOr("GEMINI_CLI_HOME", filepath.Join(home(), ".gemini")), "settings.json")
}
func codexHooks() string {
	return filepath.Join(envOr("CODEX_HOME", filepath.Join(home(), ".codex")), "hooks.json")
}
func copilotHooks() string {
	return filepath.Join(envOr("COPILOT_HOME", filepath.Join(home(), ".copilot")), "hooks", "cmdcrew.json")
}
func openCodeDir() string {
	return filepath.Join(envOr("XDG_CONFIG_HOME", filepath.Join(home(), ".config")), "opencode")
}
func openCodePlugin() string { return filepath.Join(openCodeDir(), "plugins", "cmdcrew.js") }

// Linha de comando para quem roda o gancho num shell.
func shellCmd(command, agent string) string {
	if runtime.GOOS == "windows" {
		return `"` + paths.Exe + `" ` + command + " " + agent
	}
	return "'" + strings.ReplaceAll(paths.Exe, "'", `'\''`) + "' " + command + " " + agent
}

type hookEvent struct{ event, command string }

var (
	claudeEvents = []hookEvent{{"UserPromptSubmit", "start"}, {"Notification", "wait"}, {"PermissionRequest", "permission"},
		{"PostToolUse", "tool"}, {"Stop", "stop"}, {"StopFailure", "fail"}, {"SessionEnd", "end"}}
	qwenEvents    = claudeEvents
	geminiEvents  = []hookEvent{{"BeforeAgent", "start"}, {"Notification", "wait"}, {"AfterTool", "tool"}, {"AfterAgent", "stop"}, {"SessionEnd", "end"}}
	codexEvents   = []hookEvent{{"UserPromptSubmit", "start"}, {"PermissionRequest", "permission"}, {"PostToolUse", "tool"}, {"Stop", "stop"}, {"SessionEnd", "end"}}
	copilotEvents = []hookEvent{{"userPromptSubmitted", "start"}, {"permissionRequest", "permission"}, {"postToolUse", "tool"},
		{"agentStop", "stop"}, {"errorOccurred", "fail"}, {"sessionEnd", "end"}}
)

var hookMarkers = []string{"cmdcrew", "oompaloompas.exe", "salinhas.exe"}

func isOurHook(h any) bool {
	o, ok := h.(*Obj)
	if !ok {
		return false
	}
	cmd := strings.ToLower(jStr(o, "command", ""))
	for _, m := range hookMarkers {
		if strings.Contains(cmd, m) {
			return true
		}
	}
	return false
}

// Tira os nossos ganchos de um arquivo no formato do Claude Code.
func stripOurHooks(s *Obj) {
	hooks := jObj(s, "hooks")
	if hooks == nil {
		return
	}
	for _, ev := range hooks.Keys() {
		groups := jArr(hooks, ev)
		if groups == nil {
			continue
		}
		var kept []any
		for _, g := range groups {
			gd, ok := g.(*Obj)
			hs := jArr(gd, "hooks")
			if !ok || hs == nil {
				kept = append(kept, g)
				continue
			}
			var rest []any
			mine := false
			for _, h := range hs {
				if isOurHook(h) {
					mine = true
				} else {
					rest = append(rest, h)
				}
			}
			if !mine {
				kept = append(kept, g)
				continue
			}
			if len(rest) == 0 {
				continue
			}
			gd.Set("hooks", rest)
			kept = append(kept, gd)
		}
		if len(kept) == 0 {
			hooks.Delete(ev)
		} else {
			hooks.Set(ev, kept)
		}
	}
	if hooks.Len() == 0 {
		s.Delete("hooks")
	}
}

func hasOurHooks(path string) bool {
	s, err := LoadObj(path)
	if err != nil {
		return false
	}
	hooks := jObj(s, "hooks")
	for _, ev := range hooks.Keys() {
		for _, g := range jArr(hooks, ev) {
			gd, _ := g.(*Obj)
			for _, h := range jArr(gd, "hooks") {
				if isOurHook(h) {
					return true
				}
			}
		}
	}
	return false
}

// Antes da primeira alteração, guarda uma cópia do arquivo como estava.
func loadWithBackup(path string) (*Obj, error) {
	backup := path + ".antes-cmdcrew.bak"
	if fileExists(path) && !fileExists(backup) {
		if data, err := os.ReadFile(path); err == nil {
			_ = os.WriteFile(backup, data, 0o644)
		}
	}
	return LoadObj(path)
}

func installJSONHooks(path string, events []hookEvent, make func(command string, blocking bool) *Obj) error {
	s, err := loadWithBackup(path)
	if err != nil {
		return err
	}
	stripOurHooks(s)
	hooks := jObj(s, "hooks")
	if hooks == nil {
		hooks = NewObj()
		s.Set("hooks", hooks)
	}
	for _, ev := range events {
		list := jArr(hooks, ev.event)
		group := NewObj()
		group.Set("hooks", []any{make(ev.command, ev.command == "permission")})
		hooks.Set(ev.event, append(list, group))
	}
	return SaveJSON(path, s)
}

func removeJSONHooks(path string) error {
	if !fileExists(path) {
		return nil
	}
	s, err := loadWithBackup(path)
	if err != nil {
		return err
	}
	stripOurHooks(s)
	return SaveJSON(path, s)
}

func shellHook(command, agent string) *Obj {
	h := NewObj()
	h.Set("type", "command")
	h.Set("command", shellCmd(command, agent))
	return h
}

func jsonAgent(id, name, exe string, path func() string, events []hookEvent, make func(string, bool) *Obj) *Agent {
	return &Agent{
		ID: id, Name: name, Executable: exe,
		ConfigDirs: func() []string { return []string{filepath.Dir(path())} },
		Installed:  func() bool { return hasOurHooks(path()) },
		Install:    func() error { return installJSONHooks(path(), events, make) },
		Remove:     func() error { return removeJSONHooks(path()) },
	}
}

func removeFile(p string) error {
	if err := os.Remove(p); err != nil && !os.IsNotExist(err) {
		return err
	}
	return nil
}

var allAgents = []*Agent{
	{
		ID: "claude", Name: "Claude Code", Executable: "claude",
		ConfigDirs: func() []string { return []string{filepath.Dir(claudeSettings())} },
		Installed:  func() bool { return hasOurHooks(claudeSettings()) },
		Install: func() error {
			// o Claude Code aceita o executável e os argumentos separados (sem passar por um shell)
			return installJSONHooks(claudeSettings(), claudeEvents, func(command string, blocking bool) *Obj {
				h := NewObj()
				h.Set("type", "command")
				h.Set("command", paths.Exe)
				h.Set("args", []any{command})
				if blocking {
					h.Set("timeout", 600)
				} else {
					h.Set("async", true)
					h.Set("timeout", 15)
				}
				return h
			})
		},
		Remove: removeClaude,
	},
	jsonAgent("qwen", "Qwen Code", "qwen", qwenSettings, qwenEvents, func(command string, blocking bool) *Obj {
		h := shellHook(command, "qwen")
		h.Set("name", "cmdcrew")
		if blocking {
			h.Set("timeout", 600)
		} else {
			h.Set("timeout", 15)
			h.Set("async", true)
		}
		return h
	}),
	jsonAgent("gemini", "Gemini CLI", "gemini", geminiSettings, geminiEvents, func(command string, blocking bool) *Obj {
		h := shellHook(command, "gemini")
		h.Set("name", "cmdcrew")
		h.Set("timeout", 15000) // milissegundos
		return h
	}),
	jsonAgent("codex", "Codex CLI", "codex", codexHooks, codexEvents, func(command string, blocking bool) *Obj {
		h := shellHook(command, "codex")
		switch {
		case blocking:
			h.Set("timeout", 600)
		case command == "end":
			h.Set("timeout", 2)
			h.Set("async", true)
		default:
			h.Set("timeout", 15)
			h.Set("async", true)
		}
		return h
	}),
	{
		ID: "opencode", Name: "OpenCode", Executable: "opencode",
		ConfigDirs: func() []string { return []string{openCodeDir()} },
		Installed:  func() bool { return fileExists(openCodePlugin()) },
		Install: func() error {
			data, err := os.ReadFile(rootPath("agentes", "opencode.js"))
			if err != nil {
				return err
			}
			text := strings.ReplaceAll(string(data), "__CMDCREW_EXE__", Stringify(paths.Exe))
			if err := os.MkdirAll(filepath.Dir(openCodePlugin()), 0o755); err != nil {
				return err
			}
			return os.WriteFile(openCodePlugin(), []byte(text), 0o644)
		},
		Remove: func() error { return removeFile(openCodePlugin()) },
	},
	{
		ID: "copilot", Name: "Copilot CLI", Executable: "copilot",
		ConfigDirs: func() []string { return []string{filepath.Dir(filepath.Dir(copilotHooks()))} },
		Installed:  func() bool { return fileExists(copilotHooks()) },
		Install: func() error {
			hooks := NewObj()
			for _, ev := range copilotEvents {
				h := NewObj()
				h.Set("type", "command")
				h.Set("bash", "'"+strings.ReplaceAll(filepath.ToSlash(paths.Exe), "'", `'\''`)+"' "+ev.command+" copilot")
				h.Set("powershell", "& '"+strings.ReplaceAll(paths.Exe, "'", "''")+"' "+ev.command+" copilot")
				if ev.command == "permission" {
					h.Set("timeoutSec", 600)
				} else {
					h.Set("timeoutSec", 15)
				}
				hooks.Set(ev.event, []any{h})
			}
			d := NewObj()
			d.Set("version", 1)
			d.Set("hooks", hooks)
			return SaveJSON(copilotHooks(), d)
		},
		Remove: func() error { return removeFile(copilotHooks()) },
	},
}

func findAgent(id string) *Agent {
	for _, a := range allAgents {
		if a.ID == id {
			return a
		}
	}
	return nil
}

// "claude,qwen" ou vazio/"detectados" (o Claude Code e todos os agentes encontrados).
func parseAgents(ids string) []*Agent {
	var out []*Agent
	if ids == "" || ids == "detectados" {
		for _, a := range allAgents {
			if a.ID == "claude" || a.Detected() {
				out = append(out, a)
			}
		}
		return out
	}
	want := map[string]bool{}
	for _, id := range strings.Split(ids, ",") {
		want[strings.TrimSpace(id)] = true
	}
	for _, a := range allAgents {
		if want[a.ID] {
			out = append(out, a)
		}
	}
	return out
}

func removeAllAgents() {
	for _, a := range allAgents {
		if err := a.Remove(); err != nil {
			logf("remover %s: %v", a.ID, err)
		}
	}
}

// ---------- Claude Code: frases do spinner ----------

func removeClaude() error {
	if !fileExists(claudeSettings()) {
		return nil
	}
	s, err := loadWithBackup(claudeSettings())
	if err != nil {
		return err
	}
	stripOurHooks(s)
	s.Delete("spinnerVerbs")
	s.Delete("spinnerTipsOverride")
	return SaveJSON(claudeSettings(), s)
}

// As frases da equipe no spinner do Claude Code ("Thinking..." vira "Picando pedra...").
func applyPhrases(c *Config) error {
	if !hasOurHooks(claudeSettings()) {
		return nil
	}
	ch := findCharacter(loadAllCharacters(nil), c.Character)
	s, err := loadWithBackup(claudeSettings())
	if err != nil {
		return err
	}
	s.Delete("spinnerVerbs")
	s.Delete("spinnerTipsOverride")
	if c.Enabled && c.TerminalPhrases && ch != nil {
		if verbs := c.Phrases(ch, "verbos"); len(verbs) > 0 {
			v := NewObj()
			mode := "replace"
			if c.VerbMode == "append" {
				mode = "append"
			}
			v.Set("mode", mode)
			v.Set("verbs", strsToAny(verbs))
			s.Set("spinnerVerbs", v)
		}
		if tips := c.Phrases(ch, "dicas"); c.TerminalTips && len(tips) > 0 {
			t := NewObj()
			t.Set("tips", strsToAny(tips))
			t.Set("excludeDefault", false)
			s.Set("spinnerTipsOverride", t)
		}
	}
	return SaveJSON(claudeSettings(), s)
}
