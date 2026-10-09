package main

import (
	"bytes"
	"io"
	"math"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"time"
)

// O que os ganchos sabem de cada sessão de um agente. Fica em <estado>/sessoes/<id>.json.
// Mesmo formato do programa do Windows (src/Sessions.cs).

type RoomState int

const (
	Working RoomState = iota
	Waiting
	Done
	Failed
	Stopped
)

type PermissionAsk struct{ ID, Tool, Summary string }

type SessionInfo struct {
	ID, Transcript, Cwd, State string
	Terminal                   string // como voltar ao terminal (app do macOS ou janela do X11)
	Agent                      string
	Started, Changed           time.Time
	Demo                       bool
	Ask                        *PermissionAsk
}

func (s *SessionInfo) ProjectName() string {
	if s.Cwd == "" {
		return "terminal"
	}
	n := filepath.Base(strings.TrimRight(s.Cwd, `\/`))
	if i := strings.LastIndexAny(n, `\/`); i >= 0 {
		n = n[i+1:]
	}
	if n == "" || n == "." {
		return s.Cwd
	}
	return n
}

// Última vez que algo aconteceu: o arquivo da sessão, o transcript ou o de um subagente
// (o Claude Code grava os subagentes em <sessão>/subagents/).
func (s *SessionInfo) LastActivity() time.Time {
	last := s.Changed
	if s.Transcript == "" {
		return last
	}
	if fi, err := os.Stat(s.Transcript); err == nil {
		if fi.ModTime().After(last) {
			last = fi.ModTime()
		}
		sub := filepath.Join(filepath.Dir(s.Transcript), strings.TrimSuffix(filepath.Base(s.Transcript), filepath.Ext(s.Transcript)), "subagents")
		if entries, err := os.ReadDir(sub); err == nil {
			for _, e := range entries {
				if info, err := e.Info(); err == nil && info.ModTime().After(last) {
					last = info.ModTime()
				}
			}
		}
	}
	return last
}

type cachedTail struct {
	stamp time.Time
	size  int64
	at    time.Time
}

var (
	tailsMu sync.Mutex
	tails   = map[string]cachedTail{}
)

// O Esc não dispara o Stop: a interrupção só aparece no transcript ("[Request interrupted by user...]").
func (s *SessionInfo) Interrupted() bool {
	if s.Transcript == "" {
		return false
	}
	fi, err := os.Stat(s.Transcript)
	if err != nil {
		return false
	}
	tailsMu.Lock()
	c, ok := tails[s.Transcript]
	tailsMu.Unlock()
	if !ok || !c.stamp.Equal(fi.ModTime()) || c.size != fi.Size() {
		c = cachedTail{stamp: fi.ModTime(), size: fi.Size(), at: lastInterrupt(s.Transcript)}
		tailsMu.Lock()
		tails[s.Transcript] = c
		tailsMu.Unlock()
	}
	return c.at.After(s.Started)
}

func lastInterrupt(transcript string) time.Time {
	f, err := os.Open(transcript)
	if err != nil {
		return time.Time{}
	}
	defer f.Close()
	fi, _ := f.Stat()
	start := max(0, fi.Size()-64*1024)
	_, _ = f.Seek(start, io.SeekStart)
	buf, _ := io.ReadAll(f)
	lines := bytes.Split(buf, []byte("\n"))
	for i := len(lines) - 1; i >= 0; i-- {
		l := string(lines[i])
		if !strings.Contains(l, `"type":"user"`) && !strings.Contains(l, `"type":"assistant"`) {
			continue
		}
		if !strings.Contains(l, "[Request interrupted by user") {
			return time.Time{}
		}
		if k := strings.Index(l, `"timestamp":"`); k >= 0 {
			rest := l[k+13:]
			if j := strings.IndexByte(rest, '"'); j > 0 {
				if t, err := time.Parse(time.RFC3339Nano, rest[:j]); err == nil {
					return t
				}
			}
		}
		return time.Now()
	}
	return time.Time{}
}

// Estado para desenhar (ver o comentário em src/Sessions.cs).
func (s *SessionInfo) Resolve(idleMinutes float64) RoomState {
	switch s.State {
	case "pronto":
		return Done
	case "erro":
		return Failed
	case "aguardando":
		if s.Ask != nil {
			return Waiting
		}
		if !s.Demo && s.LastActivity().After(s.Changed.Add(2*time.Second)) {
			return Working
		}
		return Waiting
	default:
		if s.Demo {
			return Working
		}
		if s.Interrupted() {
			return Stopped
		}
		if time.Since(s.LastActivity()).Minutes() > math.Max(minIdleMinutes, idleMinutes) {
			return Stopped
		}
		return Working
	}
}

// Datas como "ticks" do .NET, para o arquivo ser igual ao do programa do Windows.
const ticksAtUnixEpoch = 621355968000000000

func toTicks(t time.Time) string {
	return strconv.FormatInt(t.UTC().UnixNano()/100+ticksAtUnixEpoch, 10)
}

func fromTicks(s string) time.Time {
	v, err := strconv.ParseInt(s, 10, 64)
	if err != nil || v <= 0 {
		return time.Now()
	}
	return time.Unix(0, (v-ticksAtUnixEpoch)*100)
}

func (s *SessionInfo) toJSON() *Obj {
	d := NewObj()
	d.Set("id", s.ID)
	st := s.State
	if st == "" {
		st = "trabalhando"
	}
	d.Set("estado", st)
	d.Set("cwd", s.Cwd)
	d.Set("transcript", s.Transcript)
	d.Set("terminal", s.Terminal)
	d.Set("agente", s.Agent)
	d.Set("inicio", toTicks(s.Started))
	d.Set("mudou", toTicks(s.Changed))
	if s.Demo {
		d.Set("demo", true)
	}
	if s.Ask != nil {
		a := NewObj()
		a.Set("id", s.Ask.ID)
		a.Set("ferramenta", s.Ask.Tool)
		a.Set("resumo", s.Ask.Summary)
		d.Set("pedido", a)
	}
	return d
}

func sessionFromJSON(d *Obj) *SessionInfo {
	s := &SessionInfo{
		ID: jStr(d, "id", ""), State: jStr(d, "estado", "trabalhando"), Cwd: jStr(d, "cwd", ""),
		Transcript: jStr(d, "transcript", ""), Terminal: jStr(d, "terminal", ""), Agent: jStr(d, "agente", ""),
		Started: fromTicks(jStr(d, "inicio", "")), Changed: fromTicks(jStr(d, "mudou", "")), Demo: jBool(d, "demo", false),
	}
	if a := jObj(d, "pedido"); a != nil {
		s.Ask = &PermissionAsk{ID: jStr(a, "id", ""), Tool: jStr(a, "ferramenta", ""), Summary: jStr(a, "resumo", "")}
	}
	return s
}

func safeID(id string) string {
	var b strings.Builder
	for _, c := range id {
		if c == '-' || c == '_' || (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') {
			b.WriteRune(c)
		}
	}
	if b.Len() == 0 {
		return "sessao"
	}
	return b.String()
}

func sessionFile(id string) string   { return filepath.Join(sessionsDir(), safeID(id)+".json") }
func answerFile(askID string) string { return filepath.Join(answersDir(), safeID(askID)+".txt") }

func writeAnswer(askID, answer string) {
	_ = os.MkdirAll(answersDir(), 0o755)
	if err := os.WriteFile(answerFile(askID), []byte(answer), 0o644); err != nil {
		logf("resposta: %v", err)
	}
}

func readSession(file string) *SessionInfo {
	for i := 0; i < 4; i++ {
		data, err := os.ReadFile(file)
		if err != nil {
			if os.IsNotExist(err) {
				return nil
			}
			time.Sleep(40 * time.Millisecond)
			continue
		}
		v, err := ParseJSON(data)
		if err != nil {
			time.Sleep(40 * time.Millisecond)
			continue
		}
		if o, ok := v.(*Obj); ok {
			return sessionFromJSON(o)
		}
		return nil
	}
	return nil
}

func writeSession(s *SessionInfo) {
	_ = os.MkdirAll(sessionsDir(), 0o755)
	if err := SaveJSON(sessionFile(s.ID), s.toJSON()); err != nil {
		logf("sessão: %v", err)
	}
}

func deleteSession(id string) { _ = os.Remove(sessionFile(id)) }

// Todas as sessões conhecidas; apaga as esquecidas há mais de 12 horas.
func loadAllSessions() []*SessionInfo {
	var list []*SessionInfo
	files, _ := filepath.Glob(filepath.Join(sessionsDir(), "*.json"))
	for _, f := range files {
		s := readSession(f)
		if s == nil {
			continue
		}
		if time.Since(s.LastActivity()).Hours() > 12 {
			_ = os.Remove(f)
			continue
		}
		list = append(list, s)
	}
	return list
}

func anyActive(idle float64) bool {
	for _, s := range loadAllSessions() {
		if st := s.Resolve(idle); st == Working || st == Waiting {
			return true
		}
	}
	return false
}

func anyRoom(c *Config) bool {
	for _, s := range loadAllSessions() {
		st := s.Resolve(c.IdleMinutes)
		if st == Working || st == Waiting || time.Since(s.Changed).Seconds() < c.DoneSeconds {
			return true
		}
	}
	return false
}
