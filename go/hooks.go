package main

import (
	"crypto/rand"
	"encoding/hex"
	"io"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// Os ganchos chamados pelos agentes (port de src/Program.cs). Um gancho nunca pode atrapalhar o agente:
// erros vão para o log e o processo sai em silêncio.

var hookAgent = "claude"

// O Copilot CLI manda os campos em camelCase; aqui tudo vira o formato do Claude Code.
var inputAliases = [][2]string{{"sessionId", "session_id"}, {"transcriptPath", "transcript_path"}, {"toolName", "tool_name"},
	{"toolArgs", "tool_input"}, {"toolUseId", "tool_use_id"}, {"notificationType", "notification_type"}}

func readHookInput() *Obj {
	ch := make(chan []byte, 1)
	go func() { b, _ := io.ReadAll(os.Stdin); ch <- b }()
	var data []byte
	select {
	case data = <-ch:
	case <-time.After(2 * time.Second):
	}
	d := NewObj()
	if v, err := ParseJSON(data); err == nil {
		if o, ok := v.(*Obj); ok {
			d = o
		}
	}
	for _, a := range inputAliases {
		if _, has := d.Get(a[1]); has {
			continue
		}
		v, ok := d.Get(a[0])
		if !ok {
			continue
		}
		if s, isStr := v.(string); isStr && a[1] == "tool_input" {
			if parsed, err := ParseJSON([]byte(s)); err == nil {
				v = parsed
			}
		}
		d.Set(a[1], v)
	}
	return d
}

func hook(state string) {
	input := readHookInput()
	cfg := loadConfig()
	if !cfg.Enabled {
		return
	}
	id := jStr(input, "session_id", "")
	if id == "" {
		logf("%s: gancho sem session_id (entrada vazia ou cortada)", state)
		return
	}
	s := readSession(sessionFile(id))
	if state != "trabalhando" {
		// só muda quem está trabalhando (o aviso de "parado há um tempo" chega depois do Stop)
		if s == nil {
			if state != "aguardando" {
				logf("%s: sessão %s não encontrada", state, id)
			}
			return
		}
		if s.State != "trabalhando" && s.State != "aguardando" {
			return
		}
		if state == "aguardando" && jStr(input, "notification_type", "") == "idle_prompt" {
			return
		}
	}
	if s == nil {
		s = &SessionInfo{}
	}
	s.ID = id
	s.Agent = hookAgent
	now := time.Now()
	if state == "trabalhando" {
		if t := captureTerminal(); t != "" {
			s.Terminal = t
		}
		// novo trabalho: depois de pronto, de erro, de uma interrupção (Esc) ou de uma resposta terminada sem o Stop
		before := s.Resolve(cfg.IdleMinutes)
		if s.State != "trabalhando" && s.State != "aguardando" || s.Started.IsZero() || before == Stopped || before == Done {
			s.Started = now
		}
	}
	if state != "aguardando" {
		s.Ask = nil
	}
	s.State = state
	s.Changed = now
	s.Transcript = jStr(input, "transcript_path", s.Transcript)
	s.Cwd = jStr(input, "cwd", s.Cwd)
	writeSession(s)
	if state == "trabalhando" {
		ensureWindows(cfg)
	}
}

func hookEnd() {
	input := readHookInput()
	deleteSession(jStr(input, "session_id", ""))
}

func hookTool() {
	input := readHookInput()
	s := readSession(sessionFile(jStr(input, "session_id", "")))
	if s == nil {
		return
	}
	if s.Ask == nil {
		// sinal de vida para agentes sem transcript (OpenCode)
		if s.State == "trabalhando" && s.Transcript == "" {
			s.Changed = time.Now()
			writeSession(s)
		}
		return
	}
	s.Ask = nil
	s.State = "trabalhando"
	s.Changed = time.Now()
	writeSession(s)
}

// PermissionRequest: mostra o pedido na sala e espera Permitir, Negar ou Terminal.
func hookPermission() {
	input := readHookInput()
	cfg := loadConfig()
	if !cfg.Enabled || !cfg.RoomsOn() || !cfg.RoomPermissions {
		return
	}
	id := jStr(input, "session_id", "")
	askID := jStr(input, "tool_use_id", "")
	if askID == "" {
		b := make([]byte, 16)
		_, _ = rand.Read(b)
		askID = hex.EncodeToString(b)
	}
	tool := jStr(input, "tool_name", "")
	s := readSession(sessionFile(id))
	if s == nil {
		s = &SessionInfo{ID: id, Started: time.Now()}
	}
	s.ID = id
	s.Agent = hookAgent
	s.Transcript = jStr(input, "transcript_path", s.Transcript)
	s.Cwd = jStr(input, "cwd", s.Cwd)
	s.State = "aguardando"
	s.Changed = time.Now()
	s.Ask = &PermissionAsk{ID: askID, Tool: tool, Summary: summarize(tool, jObj(input, "tool_input"))}
	af := answerFile(askID)
	_ = os.Remove(af)
	writeSession(s)
	ensureWindows(cfg)

	answer := ""
	deadline := time.Now().Add(585 * time.Second)
	for round := 1; time.Now().Before(deadline); round++ {
		time.Sleep(150 * time.Millisecond)
		if data, err := os.ReadFile(af); err == nil {
			answer = strings.TrimSpace(string(data))
			_ = os.Remove(af)
			break
		}
		cur := readSession(sessionFile(id))
		if cur == nil || cur.Ask == nil || cur.Ask.ID != askID {
			break
		}
		if round%6 == 0 && answeredInTranscript(s.Transcript, askID) {
			break
		}
	}
	if after := readSession(sessionFile(id)); after != nil && after.Ask != nil && after.Ask.ID == askID {
		after.Ask = nil
		after.State = "trabalhando"
		after.Changed = time.Now()
		writeSession(after)
	}
	if answer != "allow" && answer != "deny" {
		return
	}
	out := NewObj()
	if hookAgent == "copilot" {
		out.Set("behavior", answer)
		if answer == "deny" {
			out.Set("message", "Negado pelo CmdCrew.")
		}
	} else {
		dec := NewObj()
		dec.Set("behavior", answer)
		if answer == "deny" {
			dec.Set("message", "Negado pelo CmdCrew.")
		}
		hso := NewObj()
		hso.Set("hookEventName", "PermissionRequest")
		hso.Set("decision", dec)
		out.Set("hookSpecificOutput", hso)
	}
	os.Stdout.WriteString(Stringify(out))
}

func answeredInTranscript(transcript, toolUseID string) bool {
	if transcript == "" {
		return false
	}
	f, err := os.Open(transcript)
	if err != nil {
		return false
	}
	defer f.Close()
	fi, _ := f.Stat()
	_, _ = f.Seek(max(0, fi.Size()-256*1024), io.SeekStart)
	data, _ := io.ReadAll(f)
	return strings.Contains(string(data), `"tool_use_id":"`+toolUseID+`"`)
}

// Uma linha que diz o que a ferramenta quer fazer.
func summarize(tool string, ti *Obj) string {
	if ti == nil {
		return tool
	}
	for _, key := range []string{"command", "file_path", "url", "query", "pattern", "path", "description"} {
		if v := jStr(ti, key, ""); v != "" {
			if key == "file_path" || key == "path" {
				return filepath.Base(strings.TrimRight(v, `\/`))
			}
			return v
		}
	}
	return tool
}

func ensureWindows(cfg *Config) {
	if cfg.CrewOn() && !isRunning("equipe") {
		launchDetached("run")
	}
	if cfg.RoomsOn() && !isRunning("salas") {
		launchDetached("salas")
	}
}
