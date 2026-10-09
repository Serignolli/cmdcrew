package main

import (
	"image/color"
	"math"
	"math/rand"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/text/v2"
	"github.com/hajimehoshi/ebiten/v2/vector"
)

// A equipe andando em cima da tela (port de src/Overlay.cs). No macOS e no Linux ela fica no rodapé
// (ou no topo) da tela: os sistemas não deixam acompanhar a janela do terminal sem permissões especiais.

const (
	cWalk = iota
	cWork
	cIdle
	cJump
)

type worker struct {
	X, Target, Timer, FrameTimer, Delay, SpeedMul, BubbleTimer float32
	Y, VY, Squash, MeetCooldown, CheerTimer, IdleHold, Phase   float32
	Dir, State, Frame, TopRow                                  int
	Carrying, Gone, Looked                                     bool
	WalkR, WalkL, IdleR, IdleL, WorkR, WorkL                   []*ebiten.Image
	WorksR, WorksL                                             [][]*ebiten.Image
	WorkFx                                                     []string
	CarryR, CarryL                                             *ebiten.Image
	Bubble, Effect                                             string
}

type cparticle struct {
	X, Y, VX, VY, Life, MaxLife, Gravity, Grow float32
	C                                          color.RGBA
	Text                                       string
	Size                                       int
	Bug                                        bool
}

type Crew struct {
	cfg                *Config
	ch                 *Character
	workers            []*worker
	parts              []*cparticle
	rnd                *rand.Rand
	scale, px, spriteW int
	formH              int
	gravity            float32
	speech, endSpeech  []string
	sessionMode        bool
	demoSeconds        float64
	elapsed            float64
	nextCheck, nextBub float64
	ending             float64
	last               time.Time
	w, h               int // tamanho da janela em pixels
	dev                float64
	bubbleFace         text.Face
	symbolFace         text.Face
	ready              bool
}

func runCrew(sessionMode bool, demoSeconds float64, cfg *Config) {
	var lock *instanceLock
	if sessionMode {
		l, ok := acquireLock("equipe")
		if !ok {
			return
		}
		lock = l
	}
	c := &Crew{cfg: cfg, sessionMode: sessionMode, demoSeconds: demoSeconds, ending: -1, nextBub: 2.5,
		rnd: rand.New(rand.NewSource(time.Now().UnixNano()))}
	var errs []string
	c.ch = findCharacter(loadAllCharacters(&errs), cfg.Character)
	for _, e := range errs {
		logf("personagem com erro: %s", e)
	}
	if c.ch == nil {
		logf("nenhum personagem em %s", charactersDir())
		return
	}
	ebiten.SetWindowDecorated(false)
	ebiten.SetWindowFloating(true)
	ebiten.SetWindowMousePassthrough(true)
	ebiten.SetWindowResizingMode(ebiten.WindowResizingModeDisabled)
	ebiten.SetWindowTitle("CmdCrew")
	ebiten.SetRunnableOnUnfocused(true)
	ebiten.SetTPS(33)
	ebiten.SetWindowSize(1, 1)
	ebiten.SetWindowPosition(-10000, -10000)
	err := ebiten.RunGameWithOptions(c, &ebiten.RunGameOptions{ScreenTransparent: transparent(), InitUnfocused: true, SkipTaskbar: true,
		X11ClassName: "CmdCrew", X11InstanceName: "cmdcrew-equipe"})
	if err != nil && err != ebiten.Termination {
		logf("equipe: %v", err)
		if lock != nil {
			lock.Release()
		}
		return
	}
	if lock != nil {
		lock.Release()
		cfg := loadConfig()
		if cfg.Enabled && cfg.CrewOn() && anyActive(cfg.IdleMinutes) {
			launchDetached("run")
		}
	}
}

// Monta os quadros e a equipe (precisa da janela aberta para saber a escala da tela).
func (c *Crew) setup() {
	c.dev = ebiten.Monitor().DeviceScaleFactor()
	if c.dev <= 0 {
		c.dev = 1
	}
	ch := c.ch
	c.scale = max(1, int(math.Round(float64(max(1, c.cfg.Size))*c.dev)))
	c.px = ch.PixelScale(c.scale)
	c.spriteW = ch.Width * c.px
	c.gravity = 900 * float32(c.scale) / 3
	c.formH = (ch.FeetRow-ch.TopRow+1)*c.px + 7*c.scale + 6*c.scale + int(40*c.dev)
	if ch.FlyHeight > 0 {
		c.formH += ch.FlyHeight*c.px + 2*c.scale
	}
	c.speech = c.cfg.Phrases(ch, "falas")
	c.endSpeech = c.cfg.Phrases(ch, "falasFim")
	c.bubbleFace = pixelFace(11 * c.dev)
	c.symbolFace = pixelFace((8 + float64(max(1, c.cfg.Size))*2) * c.dev)

	toImgs := func(skin *Skin, kind string, flip bool) []*ebiten.Image {
		var out []*ebiten.Image
		for _, im := range ch.Render(skin, kind, c.px, flip) {
			out = append(out, ebiten.NewImageFromImage(im))
		}
		return out
	}
	n := max(1, min(20, c.cfg.Count))
	order := append([]*Skin(nil), ch.Skins...)
	c.rnd.Shuffle(len(order), func(i, j int) { order[i], order[j] = order[j], order[i] })
	cache := map[string]*worker{}
	for i := 0; i < n; i++ {
		skin := ch.GetSkin(c.cfg.Skin)
		if c.cfg.Skin == "*" {
			skin = order[i%len(order)]
		}
		tpl, ok := cache[skin.ID]
		if !ok {
			tpl = &worker{WalkR: toImgs(skin, "andar", false), WalkL: toImgs(skin, "andar", true),
				IdleR: toImgs(skin, "parado", false), IdleL: toImgs(skin, "parado", true)}
			for _, k := range ch.WorkKinds(skin) {
				tpl.WorksR = append(tpl.WorksR, toImgs(skin, k, false))
				tpl.WorksL = append(tpl.WorksL, toImgs(skin, k, true))
				tpl.WorkFx = append(tpl.WorkFx, ch.EffectOf(skin, k))
			}
			if p := getProp(ch.CarryOf(skin)); p != nil {
				tpl.CarryR = ebiten.NewImageFromImage(renderPixels(p.Rows, p.Colors, c.scale, false))
				tpl.CarryL = ebiten.NewImageFromImage(renderPixels(p.Rows, p.Colors, c.scale, true))
			}
			cache[skin.ID] = tpl
		}
		w := &worker{WalkR: tpl.WalkR, WalkL: tpl.WalkL, IdleR: tpl.IdleR, IdleL: tpl.IdleL, WorksR: tpl.WorksR, WorksL: tpl.WorksL,
			WorkFx: tpl.WorkFx, CarryR: tpl.CarryR, CarryL: tpl.CarryL, TopRow: skin.TopRow, IdleHold: 0.8,
			SpeedMul: 0.8 + c.rnd.Float32()*0.4, Delay: float32(i)*0.6 + c.rnd.Float32()*0.4, MeetCooldown: 2,
			Phase: c.rnd.Float32() * math.Pi * 2, X: float32(math.NaN()), State: cWalk, Dir: 1}
		if i%2 == 1 {
			w.Dir = -1
		}
		c.pickWork(w)
		c.workers = append(c.workers, w)
	}
	// rodapé ou topo da tela, com a largura toda
	mw, mh := ebiten.Monitor().Size()
	lh := int(math.Ceil(float64(c.formH) / c.dev))
	y := mh - bottomMargin() - lh + 8
	if c.cfg.Position == "topo" {
		y = 28
	}
	c.w, c.h = int(float64(mw)*c.dev), c.formH
	ebiten.SetWindowSize(mw, lh)
	ebiten.SetWindowPosition(0, y)
	c.ready = true
}

func (c *Crew) Layout(ow, oh int) (int, int) {
	if !c.ready {
		return max(1, ow), max(1, oh)
	}
	return c.w, c.h
}

func (c *Crew) Update() error {
	now := time.Now()
	if !c.ready {
		c.setup()
		macAccessory()
		c.last = now
		return nil
	}
	dt := float32(math.Min(0.1, now.Sub(c.last).Seconds()))
	c.last = now
	c.elapsed += float64(dt)
	if c.elapsed >= c.nextCheck {
		c.nextCheck = c.elapsed + 0.4
		active := c.elapsed < c.demoSeconds
		if c.sessionMode {
			active = anyActive(c.cfg.IdleMinutes)
		}
		if !active && c.ending < 0 {
			c.beginEnding()
		} else if active && c.ending >= 0 {
			c.cancelEnding()
		}
	}
	if c.ending >= 0 {
		c.ending += float64(dt)
		if c.ending > 2.4+0.9 {
			return ebiten.Termination
		}
	}
	for _, w := range c.workers {
		c.updateWorker(w, dt)
	}
	if c.ending < 0 {
		c.checkMeetings()
	}
	c.updateParticles(dt)
	c.updateBubbles(dt)
	return nil
}

func (c *Crew) beginEnding() {
	c.ending = 0
	var visible []*worker
	for _, w := range c.workers {
		if w.Delay <= 0 && !math.IsNaN(float64(w.X)) {
			visible = append(visible, w)
		} else {
			w.Gone = true
			continue
		}
		w.State, w.Timer, w.Carrying, w.Frame = cIdle, 999, false, 0
		w.CheerTimer = c.rnd.Float32() * 0.5
		w.Bubble = ""
	}
	if c.cfg.Bubbles && len(c.endSpeech) > 0 {
		c.rnd.Shuffle(len(visible), func(i, j int) { visible[i], visible[j] = visible[j], visible[i] })
		for _, w := range visible[:min(3, (len(visible)+1)/2)] {
			w.Bubble = c.endSpeech[c.rnd.Intn(len(c.endSpeech))]
			w.BubbleTimer = 3
		}
	}
}

func (c *Crew) cancelEnding() {
	c.ending = -1
	for _, w := range c.workers {
		w.Bubble = ""
		if w.Gone {
			w.Gone, w.X, w.Delay, w.State = false, float32(math.NaN()), c.rnd.Float32()*1.5, cWalk
		} else {
			c.decide(w)
		}
	}
}

func (c *Crew) randomTarget() float32 {
	margin := float32(c.spriteW) * 0.75
	span := max(1, float32(c.w)-2*margin)
	return margin + c.rnd.Float32()*span
}

func (c *Crew) decide(w *worker) {
	r := c.rnd.Float64()
	w.Frame, w.FrameTimer, w.Looked = 0, 0, false
	switch {
	case r < 0.46:
		w.State = cWork
		c.pickWork(w)
		w.Timer = 2.5 + c.rnd.Float32()*4
		if w.Carrying {
			w.Carrying = false
			c.spawn("poeira", w, 3)
		}
	case r < 0.8:
		w.State = cWalk
		w.Target = c.randomTarget()
		w.Carrying = w.CarryR != nil && c.rnd.Float64() < 0.45
	case r < 0.88 && w.Y <= 0 && c.ch.FlyHeight == 0:
		w.State = cJump
		c.launch(w, 5+c.rnd.Float32()*3)
	default:
		w.State = cIdle
		w.Timer = 0.9 + c.rnd.Float32()*1.6
		if c.rnd.Float64() < 0.5 {
			w.Dir = -w.Dir
		}
	}
}

func (c *Crew) pickWork(w *worker) {
	if len(w.WorksR) == 0 {
		w.WorkR, w.WorkL = w.IdleR, w.IdleL
		return
	}
	k := c.rnd.Intn(len(w.WorksR))
	w.WorkR, w.WorkL, w.Effect = w.WorksR[k], w.WorksL[k], w.WorkFx[k]
}

func (c *Crew) launch(w *worker, height float32) {
	if w.Y > 0 {
		return
	}
	w.VY = float32(math.Sqrt(float64(2 * c.gravity * height * float32(c.scale))))
	w.Y = 0.01
}

func (c *Crew) updateWorker(w *worker, dt float32) {
	if w.Gone {
		return
	}
	if w.Delay > 0 {
		w.Delay -= dt
		return
	}
	if math.IsNaN(float64(w.X)) {
		if w.Dir > 0 {
			w.X = float32(-c.spriteW)
		} else {
			w.X = float32(c.w + c.spriteW)
		}
		w.Target = c.randomTarget()
		w.State = cWalk
	}
	if w.BubbleTimer > 0 {
		w.BubbleTimer -= dt
		if w.BubbleTimer <= 0 {
			w.Bubble = ""
		}
	}
	if w.Squash > 0 {
		w.Squash -= dt
	}
	if w.MeetCooldown > 0 {
		w.MeetCooldown -= dt
	}
	w.FrameTimer += dt
	if w.Y > 0 {
		w.VY -= c.gravity * dt
		w.Y += w.VY * dt
		if w.Y <= 0 {
			w.Y, w.VY, w.Squash = 0, 0, 0.12
			c.spawn("poeira", w, 2)
			if w.State == cJump {
				c.decide(w)
			}
		}
	}
	speed := float32(34.0*float64(c.scale)/3.0*c.cfg.Speed) * w.SpeedMul
	switch w.State {
	case cWalk:
		dx := w.Target - w.X
		if float32(math.Abs(float64(dx))) <= speed*dt {
			w.X = w.Target
			c.decide(w)
			break
		}
		w.Dir = 1
		if dx < 0 {
			w.Dir = -1
		}
		w.X += float32(w.Dir) * speed * dt
		step := float32(0.18)
		if len(w.WalkR) >= 8 {
			step = 0.095
		} else if len(w.WalkR) >= 4 {
			step = 0.13
		}
		if w.FrameTimer > step/float32(c.cfg.Speed) {
			w.FrameTimer = 0
			w.Frame = (w.Frame + 1) % len(w.WalkR)
		}
	case cWork:
		w.Timer -= dt
		step := float32(0.3)
		if len(w.WorkR) >= 4 {
			step = 0.11
		}
		if w.FrameTimer > step/float32(math.Max(0.5, c.cfg.Speed)) {
			w.FrameTimer = 0
			w.Frame = (w.Frame + 1) % len(w.WorkR)
			if w.Frame == len(w.WorkR)-1 {
				c.spawn(w.Effect, w, 0)
			}
		}
		if w.Timer <= 0 {
			c.decide(w)
		}
	case cJump:
	default:
		if c.ending >= 0 {
			w.CheerTimer -= dt
			if w.CheerTimer <= 0 && w.Y <= 0 && c.ending < 2 {
				c.launch(w, 4+c.rnd.Float32()*3)
				w.CheerTimer = 0.15 + c.rnd.Float32()*0.35
				if c.rnd.Float64() < 0.3 {
					w.Dir = -w.Dir
				}
			}
			break
		}
		w.Timer -= dt
		c.animateIdle(w)
		if !w.Looked && w.Timer < 0.6 && c.rnd.Float64() < 0.5 {
			w.Dir = -w.Dir
			w.Looked = true
		}
		if w.Timer <= 0 {
			c.decide(w)
		}
	}
}

func (c *Crew) animateIdle(w *worker) {
	if len(w.IdleR) < 2 {
		w.Frame = 0
		return
	}
	hold := float32(0.09)
	if w.Frame == 0 {
		hold = w.IdleHold
	}
	if w.FrameTimer < hold {
		return
	}
	w.FrameTimer = 0
	w.Frame = (w.Frame + 1) % len(w.IdleR)
	if w.Frame == 0 {
		w.IdleHold = 1.2 + c.rnd.Float32()*2.2
	}
}

func (c *Crew) checkMeetings() {
	for i, a := range c.workers {
		if a.State != cWalk || a.Gone || a.Delay > 0 || math.IsNaN(float64(a.X)) || a.MeetCooldown > 0 {
			continue
		}
		for _, b := range c.workers[i+1:] {
			if b.State != cWalk || b.Gone || b.Delay > 0 || math.IsNaN(float64(b.X)) || b.MeetCooldown > 0 || a.Dir == b.Dir {
				continue
			}
			gap := b.X - a.X
			ag := float32(math.Abs(float64(gap)))
			if ag > float32(c.spriteW)*0.9 || ag < float32(c.spriteW)*0.5 || (gap > 0) != (a.Dir > 0) {
				continue
			}
			a.MeetCooldown, b.MeetCooldown = 10, 10
			if c.rnd.Float64() > 0.45 {
				continue
			}
			for _, w := range []*worker{a, b} {
				w.State, w.Timer, w.Frame, w.Looked = cIdle, 1.3+c.rnd.Float32()*0.5, 0, true
			}
			if gap > 0 {
				a.Dir = 1
			} else {
				a.Dir = -1
			}
			b.Dir = -a.Dir
			hopper := a
			if c.rnd.Intn(2) == 0 {
				hopper = b
			}
			c.launch(hopper, 3)
			sym := "♥"
			if c.rnd.Intn(3) == 0 {
				sym = "!"
			}
			c.add((a.X+b.X)/2-float32(c.scale)*2, float32(c.headTop(a)-c.scale*2), c.rf(-6, 6), -22, 0, 1.3,
				[]string{"#ec407a", "#ffca28"}[c.rnd.Intn(2)], sym, 0)
			break
		}
	}
}

func (c *Crew) updateBubbles(dt float32) {
	if !c.cfg.Bubbles || c.ending >= 0 || len(c.speech) == 0 {
		return
	}
	c.nextBub -= float64(dt)
	if c.nextBub > 0 {
		return
	}
	var cand []*worker
	for _, w := range c.workers {
		if !w.Gone && w.Delay <= 0 && !math.IsNaN(float64(w.X)) && w.Bubble == "" && w.X > 0 && w.X < float32(c.w) {
			cand = append(cand, w)
		}
	}
	if len(cand) > 0 {
		w := cand[c.rnd.Intn(len(cand))]
		w.Bubble = c.speech[c.rnd.Intn(len(c.speech))]
		w.BubbleTimer = 3.2
	}
	c.nextBub = 5 + c.rnd.Float64()*8
}

// ---------- partículas ----------

func (c *Crew) ground() int    { return c.h }
func (c *Crew) spriteTop() int { return c.ground() - (c.ch.FeetRow+1)*c.px }
func (c *Crew) headTop(w *worker) int {
	return c.spriteTop() + w.TopRow*c.px - int(w.Y) - c.fly(w)
}

func (c *Crew) fly(w *worker) int {
	if c.ch.FlyHeight == 0 {
		return 0
	}
	return c.ch.FlyHeight*c.px + int(math.Round(math.Sin(c.elapsed*3.2+float64(w.Phase))*float64(c.scale)*1.3))
}

func (c *Crew) rf(a, b float32) float32 { return a + c.rnd.Float32()*(b-a) }

func (c *Crew) pick(s ...string) string { return s[c.rnd.Intn(len(s))] }

func (c *Crew) spawn(effect string, w *worker, count int) {
	if effect == "" {
		return
	}
	for i := 0; i < len(effect); i++ {
		if effect[i] == '+' {
			for _, part := range splitTrim(effect, "+") {
				c.spawn(part, w, count)
			}
			return
		}
	}
	sw := float32(c.spriteW)
	sc := float32(c.scale)
	dir := float32(w.Dir)
	frontX := w.X + dir*sw*0.45
	headY := float32(c.headTop(w))
	midY := float32(c.ground()) - float32(c.ch.FeetRow-w.TopRow)*float32(c.px)*0.3 - float32(c.fly(w))
	headX := w.X + dir*sw*0.2
	g := float32(c.ground())
	n := func(def int) int {
		if count > 0 {
			return count
		}
		return def
	}
	switch effect {
	case "faiscas":
		for i := 0; i < n(7); i++ {
			c.add(frontX, g-sc*2, c.rf(-110, 110), c.rf(-190, -60), 420, c.rf(0.35, 0.7), c.pick("#ffd700", "#ffa500", "#ffffff", "#ff4500"), "", c.scale)
		}
	case "poeira":
		for i := 0; i < n(4); i++ {
			c.add(w.X+c.rf(-sw*0.3, sw*0.3), g-sc, c.rf(-25, 25), c.rf(-35, -10), 0, c.rf(0.4, 0.8), c.pick("#beaa8c", "#96826e"), "", c.scale*(1+c.rnd.Intn(2)))
		}
	case "terra":
		for i := 0; i < n(6); i++ {
			c.add(frontX, g-sc*2, c.rf(-40, 90)*-dir, c.rf(-170, -80), 420, c.rf(0.4, 0.8), c.pick("#79553a", "#5d402d", "#966e46"), "", c.scale*(1+c.rnd.Intn(2)))
		}
	case "neve":
		for i := 0; i < n(5); i++ {
			c.add(frontX+c.rf(-10, 10), midY, c.rf(-30, 30), c.rf(-60, -20), 60, c.rf(0.6, 1.1), c.pick("#ffffff", "#b4dcff"), "", c.scale)
		}
	case "estrelas":
		for i := 0; i < n(3); i++ {
			c.add(frontX+c.rf(-6, 6), midY, c.rf(-40, 40), c.rf(-80, -30), 60, c.rf(0.5, 0.9), c.pick("#ffd700", "#fff096", "#ff69b4"), "", c.scale)
		}
	case "magia":
		for i := 0; i < n(2); i++ {
			c.add(headX+c.rf(-10, 10), headY+c.rf(0, 12), c.rf(-24, 24), c.rf(-40, -20), 0, c.rf(1.0, 1.5), c.pick("#b388ff", "#64ffda", "#ffd700"), "★", 0)
		}
	case "zzz":
		t := "z"
		if c.rnd.Intn(3) == 0 {
			t = "Z"
		}
		c.add(w.X+dir*sw*0.3, headY+sc*4, dir*c.rf(8, 16), c.rf(-26, -18), 0, 2.2, "#5a6ec8", t, 0)
	case "notas":
		c.add(headX, headY, dir*c.rf(10, 22), c.rf(-30, -20), 0, 1.8, c.pick("#783ca0", "#28783c", "#c85028"), "♪", 0)
	case "claude":
		c.add(headX, headY, dir*c.rf(6, 20), c.rf(-34, -20), 0, 1.6, c.pick("#d97757", "#f5a37f", "#b85f42"), "✻", 0)
	case "codigo":
		for i := 0; i < n(2); i++ {
			c.add(headX+c.rf(-8, 8), headY+c.rf(0, 10), dir*c.rf(4, 16), c.rf(-34, -18), 0, c.rf(1.2, 1.7),
				c.pick("#00e676", "#69f0ae", "#2ea043"), c.pick("0", "1", "{ }", "</>", ";", "=>", "#", "()"), 0)
		}
	case "dados":
		for i := 0; i < n(2); i++ {
			c.add(headX+c.rf(-8, 8), headY+c.rf(0, 10), dir*c.rf(4, 16), c.rf(-34, -18), 0, c.rf(1.2, 1.7),
				c.pick("#42a5f5", "#26c6da", "#ffca28"), c.pick("%", "+", "=", "#", "42"), 0)
		}
	case "bugs":
		for i := 0; i < n(1+c.rnd.Intn(2)); i++ {
			if p := c.add(frontX, midY, c.rf(-60, 60), c.rf(-150, -90), 420, c.rf(1.6, 2.4), "#e53935", "", c.scale*2); p != nil {
				p.Bug = true
			}
		}
	case "fumaca":
		for i := 0; i < n(3); i++ {
			if p := c.add(headX+c.rf(-6, 6), headY+c.rf(0, 8), c.rf(-10, 10), c.rf(-30, -16), 0, c.rf(1.0, 1.6), c.pick("#787878", "#969696", "#5f5f5f"), "", c.scale); p != nil {
				p.Grow = sc * 1.8
			}
		}
	case "moedas":
		for i := 0; i < n(2); i++ {
			c.add(frontX, midY, c.rf(-50, 50), c.rf(-190, -120), 420, c.rf(0.6, 0.9), c.pick("#ffd700", "#ffd54f"), "", c.scale*2)
		}
	case "latidos":
		c.add(headX+dir*sw*0.25, headY+sc*2, dir*c.rf(10, 20), c.rf(-26, -16), 0, 1.3, c.pick("#ffb74d", "#fff176"), c.pick("au!", "au au!", "auu!", "woof!"), 0)
	case "coracoes":
		c.add(headX, headY, dir*c.rf(6, 16), c.rf(-30, -18), 0, 1.6, c.pick("#ec407a", "#f48fb1", "#e53935"), "♥", 0)
	case "fogo":
		for i := 0; i < n(12); i++ {
			c.add(w.X+dir*sw*0.5, headY+c.rf(2, 4)*sc, dir*c.rf(140, 240), c.rf(-25, 10), -40, c.rf(0.25, 0.55),
				c.pick("#ffeb3b", "#ff9800", "#f44336", "#fff3b0"), "", c.scale*(1+c.rnd.Intn(2)))
		}
	case "flechas":
		c.add(w.X+dir*sw*0.5, midY-sc*3, dir*c.rf(240, 300), c.rf(-8, -2), 0, 0.8, "#8d6e63", map[bool]string{true: "→", false: "←"}[dir > 0], 0)
	case "flores":
		for i := 0; i < n(2); i++ {
			c.add(frontX+c.rf(-6, 6), midY, c.rf(-30, 30), c.rf(-60, -30), 20, c.rf(1.0, 1.5), c.pick("#ec407a", "#ffeb3b", "#ab47bc", "#66bb6a"), "✿", 0)
		}
	}
}

func (c *Crew) add(x, y, vx, vy, grav, life float32, hex, text string, size int) *cparticle {
	if len(c.parts) > 300 {
		return nil
	}
	p := &cparticle{X: x, Y: y, VX: vx, VY: vy, Gravity: grav, Life: life, MaxLife: life, C: col(hex), Text: text, Size: size}
	c.parts = append(c.parts, p)
	return p
}

func (c *Crew) updateParticles(dt float32) {
	kept := c.parts[:0]
	g := float32(c.ground())
	for _, p := range c.parts {
		p.Life -= dt
		if p.Life <= 0 {
			continue
		}
		p.VY += p.Gravity * dt
		p.X += p.VX * dt
		p.Y += p.VY * dt
		if p.Gravity > 0 && p.Y > g-float32(p.Size) {
			p.Y = g - float32(p.Size)
			if p.Bug {
				p.VY, p.Gravity = 0, 0
				d := float32(1)
				if p.VX < 0 {
					d = -1
				}
				p.VX = d * 38 * float32(c.scale) / 3
			} else {
				p.VY *= -0.3
				p.VX *= 0.6
			}
		}
		kept = append(kept, p)
	}
	c.parts = kept
}

// ---------- desenho ----------

// Símbolos que a fonte em pixel não tem viram desenhinhos (5×5).
var symbolSprites = map[string][]string{
	"♥": {".X.X.", "XXXXX", "XXXXX", ".XXX.", "..X.."},
	"♪": {"..XX.", "..X.X", "..X..", "XXX..", "XX..."},
	"★": {"..X..", ".XXX.", "XXXXX", ".XXX.", ".X.X."},
	"✻": {"X.X.X", ".XXX.", "XXXXX", ".XXX.", "X.X.X"},
	"✿": {".X.X.", "XXXXX", ".XOX.", "XXXXX", ".X.X."},
	"→": {".....", "...X.", "XXXXX", "...X.", "....."},
	"←": {".....", ".X...", "XXXXX", ".X...", "....."},
}

func (c *Crew) Draw(screen *ebiten.Image) {
	if !c.ready {
		return
	}
	alpha := float32(1)
	if c.ending > 2.4 {
		alpha = float32(max(0.01, 1-(c.ending-2.4)/0.9))
	}
	top := c.spriteTop()
	for _, w := range c.workers {
		if w.Gone || w.Delay > 0 || math.IsNaN(float64(w.X)) {
			continue
		}
		r := w.Dir > 0
		var set []*ebiten.Image
		switch w.State {
		case cWork:
			set = map[bool][]*ebiten.Image{true: w.WorkR, false: w.WorkL}[r]
		case cWalk:
			set = map[bool][]*ebiten.Image{true: w.WalkR, false: w.WalkL}[r]
		default:
			set = map[bool][]*ebiten.Image{true: w.IdleR, false: w.IdleL}[r]
		}
		if len(set) == 0 {
			continue
		}
		frame := w.Frame % len(set)
		if w.State == cJump || c.ending >= 0 {
			frame = 0
		}
		bmp := set[frame]
		bw, bh := bmp.Bounds().Dx(), bmp.Bounds().Dy()
		x := int(w.X) - bw/2
		y := top - int(w.Y) - c.fly(w)
		op := &ebiten.DrawImageOptions{}
		if w.Squash > 0 {
			dh := max(c.scale, bh/10)
			op.GeoM.Scale(float64(bw+dh)/float64(bw), float64(bh-dh)/float64(bh))
			op.GeoM.Translate(float64(x-dh/2), float64(y+dh))
		} else {
			op.GeoM.Translate(float64(x), float64(y))
		}
		op.ColorScale.ScaleAlpha(alpha)
		screen.DrawImage(bmp, op)
		if w.Carrying && w.State == cWalk && w.CarryR != nil {
			cimg := w.CarryL
			if r {
				cimg = w.CarryR
			}
			bob := (frame % 2) * c.scale
			cop := &ebiten.DrawImageOptions{}
			cop.GeoM.Translate(float64(int(w.X)-cimg.Bounds().Dx()/2), float64(c.headTop(w)-cimg.Bounds().Dy()-c.scale+bob))
			cop.ColorScale.ScaleAlpha(alpha)
			screen.DrawImage(cimg, cop)
		}
	}
	for _, p := range c.parts {
		cl := p.C
		if p.Text != "" {
			if spr, ok := symbolSprites[p.Text]; ok {
				u := float32(max(1, c.scale*2/3+1))
				for yy, row := range spr {
					for xx, ch := range row {
						if ch == '.' {
							continue
						}
						pc := cl
						if ch == 'O' {
							pc = col("#ffeb3b")
						}
						vector.FillRect(screen, p.X+float32(xx)*u, p.Y+float32(yy)*u, u, u, pc, false)
					}
				}
			} else {
				drawText(screen, p.Text, c.symbolFace, float64(p.X), float64(p.Y), cl)
			}
			continue
		}
		s := float32(p.Size)
		if p.Grow > 0 {
			s += p.Grow * (1 - p.Life/p.MaxLife)
		}
		vector.FillRect(screen, float32(int(p.X)), float32(int(p.Y)), s, s, cl, false)
		if p.Bug {
			hx := p.X + s
			if p.VX < 0 {
				hx = p.X - float32(c.scale)
			}
			vector.FillRect(screen, hx, p.Y, float32(c.scale), float32(c.scale), col("#212121"), false)
		}
	}
	for _, w := range c.workers {
		if w.Bubble != "" && !w.Gone && !math.IsNaN(float64(w.X)) {
			c.drawBubble(screen, w)
		}
	}
}

func (c *Crew) drawBubble(screen *ebiten.Image, w *worker) {
	tw, th := measure(w.Bubble, c.bubbleFace)
	pw, ph := 7*c.dev, 3*c.dev
	bw, bh := float32(tw+pw*2), float32(th+ph*2)
	headY := float32(c.headTop(w))
	if w.Carrying && w.CarryR != nil {
		headY -= float32(w.CarryR.Bounds().Dy() + c.scale)
	}
	by := max(0, headY-bh-7)
	bx := max(1, min(float32(c.w)-bw-1, w.X-bw/2))
	border := col("#282830")
	vector.FillRect(screen, bx, by, bw, bh, border, false)
	vector.FillRect(screen, bx+1, by+1, bw-2, bh-2, color.White, false)
	tx := max(bx+4, min(bx+bw-10, w.X-3))
	for i := 0; i < 5; i++ {
		ww := float32(7 - i*2 + 2)
		if ww <= 0 {
			break
		}
		vector.FillRect(screen, tx+float32(i), by+bh-1+float32(i), max(1, ww), 1, border, false)
		if ww > 2 {
			vector.FillRect(screen, tx+float32(i)+1, by+bh-1+float32(i), ww-2, 1, color.White, false)
		}
	}
	drawText(screen, w.Bubble, c.bubbleFace, float64(bx)+pw, float64(by)+ph, col("#222228"))
}
