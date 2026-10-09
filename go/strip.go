package main

import (
	"image"
	"math"
	"math/rand"
	"os"
	"runtime"
	"sort"
	"strconv"
	"strings"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
)

// A faixa de salas no canto de baixo da tela: uma sala por terminal trabalhando (port de src/Strip.cs).

func imageRect(x, y, w, h int) image.Rectangle { return image.Rect(x, y, x+w, y+h) }

type Strip struct {
	cfg        *Config
	cfgStamp   time.Time
	rooms      []*Room
	finished   map[string]time.Time
	lastStates map[string]RoomState
	all        []*Character
	created    int
	rnd        *rand.Rand
	unit       float32
	gap        int
	scale      float64 // pixels por ponto lógico da tela (HiDPI)
	last       time.Time
	nextPoll   time.Time
	emptyFor   float64

	anchor         *[2]int // canto de referência arrastado (lógico); nil = canto padrão
	downAt         [2]int
	down, dragging bool
	lastClick      time.Time
	winX, winY     int
	winW, winH     int
	demoSeconds    float64
}

func runRooms() {
	lock, ok := acquireLock("salas")
	if !ok {
		return
	}
	s := &Strip{finished: map[string]time.Time{}, lastStates: map[string]RoomState{}, rnd: rand.New(rand.NewSource(time.Now().UnixNano()))}
	s.cfg = loadConfig()
	s.cfgStamp = stamp(configFile())
	ebiten.SetWindowDecorated(false)
	ebiten.SetWindowFloating(true)
	ebiten.SetWindowResizingMode(ebiten.WindowResizingModeDisabled)
	ebiten.SetWindowTitle("CmdCrew")
	ebiten.SetRunnableOnUnfocused(true)
	ebiten.SetTPS(30)
	ebiten.SetWindowSize(1, 1)
	ebiten.SetWindowPosition(-10000, -10000)
	err := ebiten.RunGameWithOptions(s, &ebiten.RunGameOptions{ScreenTransparent: transparent(), InitUnfocused: true, SkipTaskbar: true,
		X11ClassName: "CmdCrew", X11InstanceName: "cmdcrew-salas"})
	lock.Release()
	if err != nil && err != ebiten.Termination {
		// sem janela (sem vídeo, sem OpenGL...): não adianta tentar de novo
		logf("salas: %v", err)
		return
	}
	// se um novo prompt chegou enquanto a última sala saía, abre de novo
	cfg := loadConfig()
	if cfg.Enabled && cfg.RoomsOn() && anyRoom(cfg) {
		launchDetached("salas")
	}
}

// CMDCREW_OPACO=1 abre as janelas sem transparência (para testar onde não há OpenGL, como no Windows).
func transparent() bool { return os.Getenv("CMDCREW_OPACO") == "" }

func stamp(f string) time.Time {
	if fi, err := os.Stat(f); err == nil {
		return fi.ModTime()
	}
	return time.Time{}
}

func (s *Strip) applyScale() {
	s.scale = ebiten.Monitor().DeviceScaleFactor()
	if s.scale <= 0 {
		s.scale = 1
	}
	s.unit = float32(math.Max(1, math.Round(float64(s.cfg.RoomSize)*s.scale)))
	s.gap = int(4 * s.scale)
	for _, r := range s.rooms {
		r.SetScale(s.unit)
	}
}

func (s *Strip) nextCharacter() *Character {
	if s.all == nil {
		s.all = loadAllCharacters(nil)
	}
	return s.cfg.RoomCharacterFor(s.all, s.created)
}

func (s *Strip) poll() {
	if st := stamp(configFile()); !st.Equal(s.cfgStamp) {
		s.cfgStamp = st
		old := s.cfg.RoomSize
		s.cfg = loadConfig()
		if s.cfg.RoomSize != old {
			s.applyScale()
		}
	}
	sessions := loadAllSessions()
	ids := map[string]bool{}
	for _, ses := range sessions {
		ids[ses.ID] = true
		st := ses.Resolve(s.cfg.IdleMinutes)
		var room *Room
		for _, r := range s.rooms {
			if r.SessionID == ses.ID && !r.Leaving {
				room = r
			}
		}
		if room == nil {
			if seen, ok := s.finished[ses.ID]; ok && seen.Equal(ses.Changed) {
				continue
			}
			recent := time.Since(ses.Changed).Seconds() < s.cfg.DoneSeconds
			if !(st == Working || st == Waiting || (st == Done || st == Failed) && recent) {
				continue
			}
			ch := s.nextCharacter()
			if ch == nil {
				logf("nenhum personagem em %s", charactersDir())
				continue
			}
			theme := themeForCharacter(ch.ID)
			variant := 0
			for _, r := range s.rooms {
				if r.Theme == theme && !r.Leaving {
					variant++
				}
			}
			room = newRoom(theme, ch, variant, s.rnd.Int63(), s.cfg.RoomSkinFor(ch))
			room.SessionID = ses.ID
			room.SetScale(s.unit)
			s.rooms = append(s.rooms, room)
			s.created++
		}
		room.ProjectName = ""
		if s.cfg.ShowName {
			room.ProjectName = ses.ProjectName()
		}
		room.Terminal = ses.Terminal
		room.Started = ses.Started
		room.Ask = ses.Ask
		room.SetState(st, ses.Changed)
		s.playFor(ses.ID, st)
	}
	for _, r := range s.rooms {
		if !r.Leaving && (!ids[r.SessionID] || r.ShouldLeave(s.cfg.DoneSeconds)) {
			r.Leaving = true
			s.finished[r.SessionID] = r.Changed
		}
	}
}

func (s *Strip) playFor(id string, st RoomState) {
	old, had := s.lastStates[id]
	s.lastStates[id] = st
	if had && old == st {
		return
	}
	switch {
	case st == Done && had && s.cfg.SoundDone:
		go playSound("pronto")
	case st == Waiting && s.cfg.SoundAttention:
		go playSound("atencao")
	case st == Failed && s.cfg.SoundFailure:
		go playSound("falha")
	}
}

func (s *Strip) roomW() int { return int(roomW * s.unit) }
func (s *Strip) roomH() int { return int(roomH * s.unit) }

func (s *Strip) growsLeft() bool { return s.cfg.Corner != "esquerda" }

func (s *Strip) Update() error {
	now := time.Now()
	if s.last.IsZero() {
		// o monitor só é conhecido com a janela aberta
		s.last = now
		s.applyScale()
		s.loadAnchor()
		macAccessory()
	}
	dt := float32(math.Min(0.1, now.Sub(s.last).Seconds()))
	s.last = now
	if now.After(s.nextPoll) {
		s.nextPoll = now.Add(400 * time.Millisecond)
		s.poll()
	}
	for _, r := range s.rooms {
		r.Update(dt, s.cfg.Bubbles)
	}
	kept := s.rooms[:0]
	for _, r := range s.rooms {
		if !r.Gone {
			kept = append(kept, r)
		}
	}
	s.rooms = kept
	if len(s.rooms) == 0 {
		s.emptyFor += float64(dt)
		if s.emptyFor > 1.5 {
			return ebiten.Termination
		}
	} else {
		s.emptyFor = 0
	}
	s.layout(dt)
	s.mouse()
	return nil
}

// Margem de baixo: a barra de tarefas do Windows, o Dock do macOS e o painel do Linux ficam ali.
func bottomMargin() int {
	switch runtime.GOOS {
	case "darwin":
		return 84
	case "windows":
		return 52
	}
	return 48
}

func (s *Strip) layout(dt float32) {
	order := append([]*Room(nil), s.rooms...)
	sort.SliceStable(order, func(i, j int) bool { return order[i].Started.Before(order[j].Started) })
	n := max(1, len(order))
	width := n*s.roomW() + (n-1)*s.gap
	for i, r := range order {
		var target float32
		if s.growsLeft() {
			target = float32(width - (i+1)*s.roomW() - i*s.gap)
		} else {
			target = float32(i * (s.roomW() + s.gap))
		}
		if math.IsNaN(float64(r.SlotX)) {
			r.SlotX = target
		} else {
			r.SlotX += (target - r.SlotX) * min(1, dt*10)
		}
	}
	if s.dragging {
		return
	}
	mw, mh := ebiten.Monitor().Size()
	lw := int(math.Ceil(float64(width) / s.scale))
	lh := int(math.Ceil(float64(s.roomH()) / s.scale))
	ax, ay := mw-12, mh-bottomMargin()
	if !s.growsLeft() {
		ax = 12
	}
	if s.anchor != nil {
		ax, ay = s.anchor[0], s.anchor[1]
	}
	x := ax
	if s.growsLeft() {
		x = ax - lw
	}
	y := ay - lh
	if lw != s.winW || lh != s.winH {
		// a faixa cresce para a esquerda: as salas acompanham o deslocamento
		if s.growsLeft() && s.winW > 0 {
			dx := float32((lw - s.winW)) * float32(s.scale)
			for _, r := range s.rooms {
				r.SlotX += dx
			}
		}
		s.winW, s.winH = lw, lh
		ebiten.SetWindowSize(lw, lh)
	}
	if x != s.winX || y != s.winY {
		s.winX, s.winY = x, y
		ebiten.SetWindowPosition(x, y)
	}
}

func (s *Strip) Draw(screen *ebiten.Image) {
	order := append([]*Room(nil), s.rooms...)
	sort.SliceStable(order, func(i, j int) bool { return order[i].Started.Before(order[j].Started) })
	for _, r := range order {
		if math.IsNaN(float64(r.SlotX)) {
			continue
		}
		r.Draw(screen, math.Round(float64(r.SlotX)), 0)
	}
}

func (s *Strip) Layout(ow, oh int) (int, int) {
	if s.scale <= 0 {
		return max(1, ow), max(1, oh)
	}
	return int(math.Ceil(float64(ow) * s.scale)), int(math.Ceil(float64(oh) * s.scale))
}

// ---------- mouse: botões de permissão, duplo clique leva ao terminal, arrastar move a faixa ----------

func (s *Strip) roomAt(x, y int) *Room {
	for _, r := range s.rooms {
		if float32(x) >= r.SlotX && float32(x) < r.SlotX+float32(s.roomW()) && y >= 0 && y < s.roomH() {
			return r
		}
	}
	return nil
}

func (s *Strip) askButtonAt(x, y int) (*Room, int) {
	r := s.roomAt(x, y)
	if r == nil || r.Ask == nil {
		return r, -1
	}
	lx, ly := float32(x)-r.SlotX, float32(y)
	for i, b := range r.AskButtons {
		if lx >= b[0] && lx < b[0]+b[2] && ly >= b[1] && ly < b[1]+b[3] {
			return r, i
		}
	}
	return r, -1
}

func (s *Strip) mouse() {
	x, y := ebiten.CursorPosition()
	over, btn := s.askButtonAt(x, y)
	for _, r := range s.rooms {
		r.AskHover = -1
		if r == over {
			r.AskHover = btn
		}
	}
	if btn >= 0 {
		ebiten.SetCursorShape(ebiten.CursorShapePointer)
	} else {
		ebiten.SetCursorShape(ebiten.CursorShapeDefault)
	}
	pressed := ebiten.IsMouseButtonPressed(ebiten.MouseButtonLeft)
	if pressed && !s.down {
		s.down, s.dragging = true, false
		s.downAt = [2]int{x, y}
		return
	}
	if pressed && s.down {
		dx, dy := x-s.downAt[0], y-s.downAt[1]
		if !s.dragging && (abs(dx) > 4 || abs(dy) > 4) {
			s.dragging = true
		}
		if s.dragging && (dx != 0 || dy != 0) {
			// o cursor fica no mesmo ponto da janela: mover a janela pela diferença
			s.winX += int(float64(dx) / s.scale)
			s.winY += int(float64(dy) / s.scale)
			ebiten.SetWindowPosition(s.winX, s.winY)
		}
		return
	}
	if !pressed && s.down {
		s.down = false
		if s.dragging {
			s.dragging = false
			ax := s.winX
			if s.growsLeft() {
				ax = s.winX + s.winW
			}
			s.anchor = &[2]int{ax, s.winY + s.winH}
			s.saveAnchor()
			return
		}
		if over != nil && btn >= 0 {
			answers := []string{"allow", "deny", "terminal"}
			writeAnswer(over.Ask.ID, answers[btn])
			over.Ask = nil
			if btn == 2 {
				focusTerminal(over.Terminal)
			}
			return
		}
		// duplo clique: vai ao terminal da sala
		if time.Since(s.lastClick) < 450*time.Millisecond {
			s.lastClick = time.Time{}
			if r := s.roomAt(x, y); r != nil {
				if r.Ask != nil {
					writeAnswer(r.Ask.ID, "terminal")
					r.Ask = nil
				}
				focusTerminal(r.Terminal)
				if r.State != Working && r.State != Waiting {
					r.Dismissed = true
				}
			}
			return
		}
		s.lastClick = time.Now()
	}
}

func abs(v int) int {
	if v < 0 {
		return -v
	}
	return v
}

func (s *Strip) loadAnchor() {
	data, err := os.ReadFile(positionFile())
	if err != nil {
		return
	}
	parts := strings.Split(strings.TrimSpace(string(data)), ",")
	if len(parts) != 2 {
		return
	}
	x, e1 := strconv.Atoi(parts[0])
	y, e2 := strconv.Atoi(parts[1])
	mw, mh := ebiten.Monitor().Size()
	if e1 == nil && e2 == nil && x > 0 && y > 0 && x <= mw && y <= mh {
		s.anchor = &[2]int{x, y}
	}
}

func (s *Strip) saveAnchor() {
	_ = os.MkdirAll(stateDir(), 0o755)
	if s.anchor == nil {
		_ = os.Remove(positionFile())
		return
	}
	_ = os.WriteFile(positionFile(), []byte(strconv.Itoa(s.anchor[0])+","+strconv.Itoa(s.anchor[1])), 0o644)
}
