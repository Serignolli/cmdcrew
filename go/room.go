package main

import (
	"fmt"
	"image/color"
	"math"
	"math/rand"
	"strings"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/text/v2"
	"github.com/hajimehoshi/ebiten/v2/vector"
)

// Uma sala na tela = uma sessão de um agente. Port de src/Rooms.cs (classe Room).

type particle struct {
	X, Y, VX, VY, Life, Gravity, W, H float32
	C                                 color.RGBA
}

type Room struct {
	SessionID   string
	Theme       *RoomTheme
	ProjectName string
	Terminal    string
	State       RoomState
	Started     time.Time
	Changed     time.Time
	Dismissed   bool
	SlotX       float32 // posição na faixa (NaN = ainda sem lugar)
	Enter       float32
	Leave       float32
	Leaving     bool
	Gone        bool
	Ask         *PermissionAsk
	AskHover    int
	AskButtons  [][4]float32 // x, y, w, h em pixels, relativos ao canto da sala

	ch                *Character
	skin              *Skin
	rnd               *rand.Rand
	speech, endSpeech []string
	parts             []*particle
	frames            map[string][]*ebiten.Image
	u, charScale      float32
	charTop, charBot  int
	background        *ebiten.Image
	canvas            *ebiten.Image
	t, stateT         float64
	ax, jumpY, jumpV  float32
	faceRight         bool
	spot, frame       int
	mode, kind        string
	modeT, frameT     float32
	bubbleT, nextBub  float32
	bubble, doneLine  string
	lastState         RoomState
	font, small       text.Face
}

func newRoom(theme *RoomTheme, ch *Character, variant int, seed int64, preferSkin string) *Room {
	r := &Room{Theme: theme, ch: ch, rnd: rand.New(rand.NewSource(seed)), frames: map[string][]*ebiten.Image{},
		Started: time.Now(), Changed: time.Now(), SlotX: float32(math.NaN()), AskHover: -1, mode: "work", faceRight: true}
	var ids []string
	for _, id := range theme.Skins[ch.ID] {
		for _, s := range ch.Skins {
			if s.ID == id {
				ids = append(ids, id)
			}
		}
	}
	if len(ids) == 0 {
		for _, s := range ch.Skins {
			ids = append(ids, s.ID)
		}
	}
	if preferSkin != "" && preferSkin != "*" {
		for _, s := range ch.Skins {
			if s.ID == preferSkin {
				kept := []string{preferSkin}
				for _, id := range ids {
					if id != preferSkin {
						kept = append(kept, id)
					}
				}
				ids = kept
				break
			}
		}
	}
	r.skin = ch.GetSkin(ids[variant%len(ids)])
	r.speech = ch.Phrases["falas"]
	r.endSpeech = ch.Phrases["falasFim"]
	s0 := theme.Spots[0]
	r.ax, r.faceRight, r.kind = s0.X, s0.FaceRight, s0.Kind
	r.modeT = r.randT(s0)
	r.nextBub = 2 + r.rnd.Float32()*4
	return r
}

func (r *Room) randT(s Spot) float32 { return s.MinT + r.rnd.Float32()*(s.MaxT-s.MinT) }

func (r *Room) PixelWidth() int  { return int(roomW * r.u) }
func (r *Room) PixelHeight() int { return int(roomH * r.u) }

func (r *Room) SetScale(unit float32) {
	if math.Abs(float64(unit-r.u)) < 0.01 && r.background != nil {
		return
	}
	r.u = unit
	for _, arr := range r.frames {
		for _, b := range arr {
			b.Deallocate()
		}
	}
	r.frames = map[string][]*ebiten.Image{}
	if r.background != nil {
		r.background.Deallocate()
		r.background = nil
	}
	if r.canvas != nil {
		r.canvas.Deallocate()
		r.canvas = nil
	}
	r.charTop, r.charBot = r.skin.TopRow, r.ch.FeetRow
	vis := max(1, r.charBot-r.charTop+1)
	s := r.Theme.CharHeight * unit / float32(vis)
	if s >= 2 {
		r.charScale = rnd32(s)
	} else {
		r.charScale = float32(math.Max(0.5, float64(s)))
	}
	r.font = pixelFace(float64(6.2 * unit))
	r.small = pixelFace(float64(5.4 * unit))
}

func (r *Room) px() int {
	if r.charScale >= 1 && math.Abs(float64(r.charScale)-math.Round(float64(r.charScale))) < 0.01 {
		return int(math.Round(float64(r.charScale)))
	}
	return 1
}

func (r *Room) Frames(k string, right bool) []*ebiten.Image {
	key := k + map[bool]string{true: ">", false: "<"}[right]
	if b, ok := r.frames[key]; ok {
		return b
	}
	kind := k
	if !r.ch.HasKind(r.skin, k) && !strings.HasPrefix(k, "trabalhar") && k != "parado" {
		kind = "andar"
	}
	var out []*ebiten.Image
	for _, img := range r.ch.Render(r.skin, kind, r.px(), !right) {
		out = append(out, ebiten.NewImageFromImage(img))
	}
	r.frames[key] = out
	return out
}

func (r *Room) SetState(st RoomState, changed time.Time) {
	r.Changed = changed
	if st == r.State {
		return
	}
	r.State = st
	r.stateT = 0
	if st == Working {
		r.Dismissed = false
		if r.Leaving && !r.Gone {
			r.Leaving = false
		}
	}
}

func (r *Room) ShouldLeave(doneSeconds float64) bool {
	if r.Dismissed {
		return true
	}
	since := time.Since(r.Changed).Seconds()
	switch r.State {
	case Done, Failed:
		return since > doneSeconds && r.stateT > 1.5
	case Stopped:
		return r.stateT > 3
	}
	return false
}

func (r *Room) Update(dt float32, bubbles bool) {
	r.t += float64(dt)
	r.stateT += float64(dt)
	if r.Leaving {
		r.Leave = min(1, r.Leave+dt/0.4)
		if r.Leave >= 1 {
			r.Gone = true
		}
	} else {
		r.Leave = max(0, r.Leave-dt/0.3)
		r.Enter = min(1, r.Enter+dt/0.45)
	}
	if r.State != r.lastState {
		switch {
		case r.State == Done:
			r.spawnConfetti()
			r.bubble = ""
			r.doneLine = ""
			if bubbles && len(r.endSpeech) > 0 {
				r.doneLine = r.endSpeech[r.rnd.Intn(len(r.endSpeech))]
			}
		case r.State == Waiting:
			r.say("Preciso de você!", 9999)
		case r.State == Failed:
			r.say("Ops, deu erro...", 5)
		case r.lastState == Waiting:
			r.bubble = ""
		}
		if r.State != Working {
			r.jumpY, r.jumpV = 0, 0
		}
		r.lastState = r.State
	}
	r.updateAgent(dt, bubbles)
	if r.bubble != "" {
		r.bubbleT -= dt
		if r.bubbleT <= 0 {
			r.bubble = ""
		}
	}
	kept := r.parts[:0]
	for _, c := range r.parts {
		c.Life -= dt
		c.VY += c.Gravity * dt
		c.X += c.VX * dt
		c.Y += c.VY * dt
		if c.Life > 0 && c.Y <= sceneH && c.X >= -4 && c.X <= sceneW+4 {
			kept = append(kept, c)
		}
	}
	r.parts = kept
}

func (r *Room) say(text string, seconds float32) { r.bubble, r.bubbleT = text, seconds }

func (r *Room) spawnConfetti() {
	cols := []string{"#ffca3a", "#3ecf7a", "#8ab4f8", "#f28b82", "#c58af9", "#ffffff"}
	for i := 0; i < 36; i++ {
		r.parts = append(r.parts, &particle{X: sceneW/2 + (r.rnd.Float32()-0.5)*20, Y: 14,
			VX: (r.rnd.Float32() - 0.5) * 70, VY: -30 - r.rnd.Float32()*35, Gravity: 60, W: 1, H: 1,
			Life: 1.6 + r.rnd.Float32(), C: col(cols[r.rnd.Intn(len(cols))])})
	}
}

func (r *Room) updateAgent(dt float32, bubbles bool) {
	step := float32(0.13)
	spots := r.Theme.Spots
	if r.State == Working {
		r.modeT -= dt
		if r.mode == "walk" {
			target := spots[r.spot]
			dir := float32(-1)
			if target.X > r.ax {
				dir = 1
			}
			r.faceRight = dir > 0
			r.ax += dir * 11 * dt
			if (dir > 0 && r.ax >= target.X) || (dir < 0 && r.ax <= target.X) {
				r.ax, r.mode, r.kind, r.faceRight = target.X, "work", target.Kind, target.FaceRight
				r.modeT = r.randT(target)
				r.frame = 0
			}
		} else if r.modeT <= 0 {
			next := 0
			if r.spot == 0 && len(spots) > 1 && r.rnd.Float64() < 0.55 {
				next = 1 + r.rnd.Intn(len(spots)-1)
			}
			if next == r.spot {
				r.modeT = r.randT(spots[r.spot])
			} else {
				r.mode, r.spot, r.frame = "walk", next, 0
				r.kind = "andar"
				if next != 0 {
					r.kind = spots[next].WalkKind
				}
			}
		}
		if bubbles && len(r.speech) > 0 && r.bubble == "" {
			r.nextBub -= dt
			if r.nextBub <= 0 {
				r.say(r.speech[r.rnd.Intn(len(r.speech))], 3.2)
				r.nextBub = 8 + r.rnd.Float32()*10
			}
		}
		if r.ch.Detail == 1 {
			step = 0.22
		}
	} else {
		if r.mode == "walk" {
			r.mode = "work"
		}
		r.kind = "parado"
		if r.State == Done && r.stateT < 3.2 && r.jumpY <= 0 && r.jumpV <= 0 && int(r.stateT*10)%8 == 0 {
			r.jumpV = 26
		}
		step = 0.3
	}
	if r.jumpY > 0 || r.jumpV > 0 {
		r.jumpY += r.jumpV * dt
		r.jumpV -= 120 * dt
		if r.jumpY <= 0 {
			r.jumpY, r.jumpV = 0, 0
		}
	}
	r.frameT += dt
	if r.frameT >= step {
		r.frameT = 0
		r.frame++
		if r.State == Working && r.mode == "work" && strings.HasPrefix(r.kind, "trabalhar") && r.u > 0 {
			if n := len(r.Frames(r.kind, r.faceRight)); n > 0 && r.frame%n == n-1 {
				r.spawnEffect(r.ch.EffectOf(r.skin, r.kind))
			}
		}
	}
}

// Quem voa (o dragão) fica no ar, subindo e descendo devagar.
func (r *Room) fly() float32 {
	if r.ch.FlyHeight <= 0 || r.u <= 0 {
		return 0
	}
	return float32(math.Min(12, float64(float32(r.ch.FlyHeight)*r.charScale/r.u))) + float32(math.Sin(r.t*3.2))*1.2
}

func (r *Room) rf(a, b float32) float32 { return a + r.rnd.Float32()*(b-a) }

func (r *Room) addPart(x, y, vx, vy, grav, life float32, hex string, w, h float32) {
	if len(r.parts) > 160 {
		return
	}
	r.parts = append(r.parts, &particle{X: x, Y: y, VX: vx, VY: vy, Gravity: grav, Life: life, C: col(hex), W: w, H: h})
}

// Os efeitos de trabalho em versão de pontinhos (mesma lógica de Room.SpawnEffect no C#).
func (r *Room) spawnEffect(effect string) {
	if effect == "" {
		return
	}
	if strings.Contains(effect, "+") {
		for _, e := range strings.Split(effect, "+") {
			r.spawnEffect(strings.TrimSpace(e))
		}
		return
	}
	dir := float32(-1)
	if r.faceRight {
		dir = 1
	}
	halfW := float32(r.ch.Width) * r.charScale / r.u / 2
	front := r.ax + dir*halfW*0.9
	floor := r.Theme.FloorY - r.fly()
	head := r.HeadY()
	mid := head + (r.Theme.FloorY-head)*0.4
	pick := func(c ...string) string { return c[r.rnd.Intn(len(c))] }
	switch strings.ToLower(effect) {
	case "faiscas":
		for i := 0; i < 6; i++ {
			r.addPart(front, floor-1, r.rf(-30, 30), r.rf(-55, -20), 140, r.rf(0.3, 0.6), pick("#ffd54f", "#ff9800", "#ffffff", "#ff5722"), 1, 1)
		}
	case "terra":
		for i := 0; i < 5; i++ {
			r.addPart(front, floor-1, -dir*r.rf(-10, 25), r.rf(-50, -25), 140, r.rf(0.4, 0.7), pick("#79553a", "#5d402d", "#966e46"), 1, 1)
		}
	case "poeira":
		for i := 0; i < 3; i++ {
			r.addPart(r.ax+r.rf(-halfW*0.6, halfW*0.6), floor-0.5, r.rf(-8, 8), r.rf(-10, -3), 0, r.rf(0.4, 0.8), pick("#beaa8c", "#96826e"), 1, 1)
		}
	case "neve":
		for i := 0; i < 4; i++ {
			r.addPart(front, mid, r.rf(-10, 10), r.rf(-20, -7), 20, r.rf(0.6, 1.0), pick("#ffffff", "#b4dcff"), 1, 1)
		}
	case "fogo":
		for i := 0; i < 10; i++ {
			r.addPart(r.ax+dir*halfW, head+(r.Theme.FloorY-head)*0.25+r.rf(-1, 1), dir*r.rf(35, 65), r.rf(-6, 3), -10, r.rf(0.25, 0.5),
				pick("#ffeb3b", "#ff9800", "#f44336", "#fff3b0"), r.rf(1, 2), 1)
		}
	case "flechas":
		r.addPart(front, mid, dir*80, -1, 0, 0.8, "#8d6e63", 4, 1)
	case "moedas":
		for i := 0; i < 2; i++ {
			r.addPart(front, mid, r.rf(-15, 15), r.rf(-55, -35), 140, r.rf(0.6, 0.9), pick("#ffd700", "#ffd54f"), 2, 2)
		}
	case "flores", "estrelas", "claude", "magia", "coracoes", "notas", "codigo", "dados":
		cols := map[string][]string{
			"flores": {"#ec407a", "#ffeb3b", "#ab47bc", "#66bb6a"}, "estrelas": {"#ffd700", "#fff096", "#ff69b4"},
			"claude": {"#d97757", "#f5a37f", "#b85f42"}, "magia": {"#b388ff", "#64ffda", "#ffd700"},
			"coracoes": {"#ec407a", "#f48fb1", "#e53935"}, "notas": {"#7a3ca0", "#287a3c", "#c85028"},
			"dados": {"#42a5f5", "#26c6da", "#ffca28"}, "codigo": {"#00e676", "#69f0ae", "#2ea043"},
		}[effect]
		for i := 0; i < 3; i++ {
			r.addPart(r.ax+dir*halfW*0.4+r.rf(-2, 2), head+r.rf(0, 3), dir*r.rf(2, 7), r.rf(-12, -6), 0, r.rf(0.9, 1.4), pick(cols...), 1, 1)
		}
	}
}

func (r *Room) HeadY() float32 {
	return r.Theme.FloorY - float32(r.charBot-r.charTop+1)*r.charScale/r.u - r.jumpY - r.fly()
}

// ---------------- desenho ----------------

func (r *Room) frameColor() color.RGBA {
	switch r.State {
	case Waiting:
		if int(r.t*2.5)%2 == 0 {
			return col("#ffb020")
		}
		return col("#8a5a00")
	case Done:
		k := (0.5 + 0.5*math.Sin(r.t*5)) * 0.6
		return blend(col("#3ecf7a"), col("#b9f5cf"), k)
	case Failed:
		return col("#e05050")
	case Stopped:
		return col("#6b6b78")
	}
	return col(r.Theme.Accent)
}

func blend(a, b color.RGBA, k float64) color.RGBA {
	m := func(x, y uint8) uint8 { return uint8(float64(x) + (float64(y)-float64(x))*k) }
	return color.RGBA{m(a.R, b.R), m(a.G, b.G), m(a.B, b.B), 255}
}

func clock(d time.Duration) string {
	if d < 0 {
		d = 0
	}
	s := int(d.Seconds())
	if s >= 3600 {
		return fmt.Sprintf("%d:%02d:%02d", s/3600, s/60%60, s%60)
	}
	return fmt.Sprintf("%d:%02d", s/60, s%60)
}

func ease(k float32) float32 {
	k = max(0, min(1, k))
	return 1 - (1-k)*(1-k)*(1-k)
}

// Draw desenha a sala na tela com o canto em (x, y) (com a animação de subir e descer).
func (r *Room) Draw(screen *ebiten.Image, x, y float64) {
	w, h := r.PixelWidth(), r.PixelHeight()
	if w <= 0 || h <= 0 {
		return
	}
	if r.background == nil {
		r.background = ebiten.NewImage(max(1, int(sceneW*r.u)), max(1, int(sceneH*r.u)))
		r.Theme.Background(Painter{Img: r.background, U: r.u})
	}
	if r.canvas == nil {
		r.canvas = ebiten.NewImage(w, h)
	}
	c := r.canvas
	c.Clear()
	if r.Ask == nil {
		r.AskButtons = nil
	}
	u := r.u
	p := Painter{Img: c, U: u}
	p.RH("#0d0a12", 0, 0, roomW, roomH)
	p.R(r.frameColor(), 0.5, 0.5, roomW-1, roomH-1)
	p.RH("#16121f", roomFrame, roomFrame, sceneW, roomHeader)
	r.drawHeader(c, p)

	sx, sy := float32(sceneX)*u, float32(sceneY)*u
	op := &ebiten.DrawImageOptions{}
	op.GeoM.Translate(float64(int(sx)), float64(int(sy)))
	c.DrawImage(r.background, op)
	scene := c.SubImage(imageRect(int(sx), int(sy), int(sceneW*u), int(sceneH*u))).(*ebiten.Image)
	sp := Painter{Img: scene, U: u, OX: sx, OY: sy}
	r.Theme.Animate(sp, r.t, r.State)
	r.drawAgent(scene, sx, sy)
	r.Theme.Front(sp)
	for _, pt := range r.parts {
		sp.R(pt.C, pt.X, pt.Y, pt.W, pt.H)
	}
	if r.State == Done {
		r.drawBanner(scene, sp)
	}
	if r.Ask != nil {
		r.drawAsk(scene, sp)
	} else if r.bubble != "" {
		r.drawBubble(scene, sp)
	}

	yoff := (1-ease(r.Enter))*float32(h) + ease(r.Leave)*float32(h)
	dop := &ebiten.DrawImageOptions{}
	dop.GeoM.Translate(x, y+float64(yoff))
	screen.DrawImage(c, dop)
}

func (r *Room) drawHeader(c *ebiten.Image, p Painter) {
	var dot string
	switch r.State {
	case Done:
		dot = "#3ecf7a"
	case Failed:
		dot = "#e05050"
	case Waiting:
		dot = map[bool]string{true: "#ffb020", false: "#5a4010"}[int(r.t*2.5)%2 == 0]
	case Stopped:
		dot = "#6b6b78"
	default:
		dot = map[bool]string{true: "#7fd1ae", false: "#3a7a62"}[int(r.t*1.5)%2 == 0]
	}
	p.Box(col(dot), 4, 5, 4, 4)
	var status string
	switch r.State {
	case Done:
		status = "pronto " + clock(r.Changed.Sub(r.Started))
	case Failed:
		status = "erro"
	case Waiting:
		status = "espera você"
	case Stopped:
		status = "parado"
	default:
		status = clock(time.Since(r.Started))
	}
	u := float64(r.u)
	right := (roomW - roomFrame - 2) * u
	sw, sh := measure(status, r.font)
	ty := roomFrame*u + (roomHeader*u-sh)/2 + u*0.3
	stc := col("#b8b2cc")
	if r.State == Done {
		stc = col("#7ff0a8")
	} else if r.State == Waiting {
		stc = col("#ffcf66")
	}
	drawText(c, status, r.font, right-sw, ty, stc)
	maxW := right - sw - 11*u - 3*u
	if r.ProjectName != "" && maxW > 4*u {
		drawText(c, ellipsize(r.ProjectName, r.font, maxW), r.font, 10.5*u, ty, col("#f3eefe"))
	}
}

func (r *Room) drawAgent(scene *ebiten.Image, sx, sy float32) {
	set := r.Frames(r.kind, r.faceRight)
	if len(set) == 0 {
		return
	}
	bmp := set[r.frame%len(set)]
	px := float32(r.px())
	k := r.charScale / px
	bw, bh := bmp.Bounds().Dx(), bmp.Bounds().Dy()
	w := float32(bw) * k
	feet := float32(r.charBot+1) * px * k
	fl := r.fly()
	cx := sx + r.ax*r.u
	fy := sy + r.Theme.FloorY*r.u - (r.jumpY+fl)*r.u
	shw := float32(math.Min(float64(w*0.45), float64(14*r.u))) * (1 - min(0.5, (r.jumpY+fl)/20))
	vector.FillRect(scene, cx-shw/2, sy+r.Theme.FloorY*r.u-r.u*0.5, shw, r.u, color.NRGBA{0, 0, 0, 70}, false)
	_ = bh
	op := &ebiten.DrawImageOptions{}
	op.GeoM.Scale(float64(k), float64(k))
	op.GeoM.Translate(math.Round(float64(cx-w/2)), math.Round(float64(fy-feet)))
	scene.DrawImage(bmp, op)
}

// Letreiro "PRONTO!" com a fala de fim do personagem.
func (r *Room) drawBanner(scene *ebiten.Image, sp Painter) {
	u := float64(r.u)
	k := float32(math.Min(1, r.stateT/0.35))
	title := "PRONTO!"
	tw, _ := measure(title, r.font)
	subW := 0.0
	if r.doneLine != "" {
		subW, _ = measure(r.doneLine, r.small)
	}
	wU := float32(math.Ceil(math.Max(tw, subW)/u)) + 8
	hU := float32(9)
	if r.doneLine != "" {
		hU = 15
	}
	wU = min(wU, sceneW-4)
	by := -hU - 2 + (hU+5)*ease(k)
	bx := rnd32((sceneW - wU) / 2)
	sp.Box(col("#0d0a12"), bx-1, by-1, wU+2, hU+2)
	sp.Box(col("#3ecf7a"), bx, by, wU, hU)
	sp.RH("#2aa860", bx+1, by+hU-2, wU-2, 1)
	cx := float64(sp.OX) + float64(bx+wU/2)*u
	drawText(scene, title, r.font, cx-tw/2, float64(sp.OY)+float64(by+1.5)*u, col("#0d2a18"))
	if r.doneLine != "" {
		drawText(scene, r.doneLine, r.small, cx-math.Min(subW, float64(wU-4)*u)/2, float64(sp.OY)+float64(by+8.5)*u, col("#14502e"))
	}
}

var askLabels = []string{"Permitir", "Negar", "Terminal"}
var askColors = []string{"#3ecf7a", "#f05a5a", "#c9c3d9"}
var askDark = []string{"#2aa860", "#c03a3a", "#9d97ae"}

func toolLabel(tool string) string {
	switch tool {
	case "Bash", "PowerShell", "run_shell_command", "shell", "bash":
		return "rodar um comando"
	case "Edit", "MultiEdit", "edit", "replace":
		return "editar um arquivo"
	case "Write", "write_file", "write":
		return "criar um arquivo"
	case "WebFetch", "web_fetch", "webfetch":
		return "abrir um site"
	case "WebSearch", "google_web_search", "websearch":
		return "pesquisar na web"
	}
	return "usar " + tool
}

func (r *Room) drawAsk(scene *ebiten.Image, sp Painter) {
	u := float64(r.u)
	var px, py, pw, ph float32 = 3, 2, sceneW - 6, 27
	blink := int(r.t*2.5)%2 == 0
	sp.Box(col("#0d0a12"), px-1, py-1, pw+2, ph+2)
	sp.Box(col(map[bool]string{true: "#ffe3a3", false: "#ffd98a"}[blink]), px, py, pw, ph)
	tx := float64(sp.OX) + float64(px+2.5)*u
	maxW := float64(pw-5) * u
	drawText(scene, "Quer "+toolLabel(r.Ask.Tool)+":", r.font, tx, float64(sp.OY)+float64(py+1.5)*u, col("#231a2e"))
	sum := strings.TrimSpace(strings.NewReplacer("\r", " ", "\n", " ").Replace(r.Ask.Summary))
	if sum == "" {
		sum = r.Ask.Tool
	}
	drawText(scene, ellipsize(sum, r.small, maxW), r.small, tx, float64(sp.OY)+float64(py+8.5)*u, col("#5a4630"))
	bw := (pw - 4 - 2*2) / 3
	var bh float32 = 8
	by := py + ph - bh - 2
	r.AskButtons = make([][4]float32, 3)
	for i := 0; i < 3; i++ {
		bx := px + 2 + float32(i)*(bw+2)
		c := askColors[i]
		if r.AskHover == i {
			c = askDark[i]
		}
		sp.Box(col("#0d0a12"), bx, by, bw, bh)
		sp.Box(col(c), bx+0.5, by+0.5, bw-1, bh-1.5)
		lw, _ := measure(askLabels[i], r.small)
		drawText(scene, askLabels[i], r.small, float64(sp.OX)+float64(bx+bw/2)*u-lw/2, float64(sp.OY)+float64(by+1.6)*u, col("#14101c"))
		r.AskButtons[i] = [4]float32{sp.OX + bx*r.u, sp.OY + by*r.u, bw * r.u, bh * r.u}
	}
}

func (r *Room) drawBubble(scene *ebiten.Image, sp Painter) {
	u := float64(r.u)
	maxW := float64(sceneW-10) * u
	lines := wrapText(r.bubble, r.small, maxW)
	lw, lh := 0.0, 0.0
	for _, l := range lines {
		w, h := measure(l, r.small)
		lw = math.Max(lw, w)
		lh = h
	}
	textH := lh * float64(len(lines))
	wU := float32(math.Ceil(lw/u)) + 5
	hU := float32(math.Ceil(textH/u)) + 3
	head := max(hU+3, r.HeadY())
	off := float32(-4)
	if r.faceRight {
		off = 4
	}
	bx := max(1, min(sceneW-wU-1, r.ax-wU/2+off))
	by := max(1, head-hU-3)
	fill := "#fbf7ee"
	if r.State == Waiting {
		fill = "#ffe3a3"
	}
	sp.Box(col("#0d0a12"), bx-1, by-1, wU+2, hU+2)
	sp.Box(col(fill), bx, by, wU, hU)
	tx := max(bx+2, min(bx+wU-4, r.ax-1))
	sp.RH("#0d0a12", tx-1, by+hU, 4, 2)
	sp.RH(fill, tx, by+hU, 2, 1)
	sp.RH("#0d0a12", tx+0.5, by+hU+2, 1, 1)
	for i, l := range lines {
		drawText(scene, l, r.small, float64(sp.OX)+float64(bx+2.5)*u, float64(sp.OY)+float64(by+1.5)*u+float64(i)*lh, col("#231a2e"))
	}
}
