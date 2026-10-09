package main

import (
	"image/color"
	"image/png"
	"math"
	"os"
	"path/filepath"
	"sort"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/vector"
)

// As salas (salas/*.json, geradas por ferramentas/gerar_salas.py). Este arquivo interpreta o JSON exatamente
// como o programa do Windows (src/Rooms.cs, classe RoomTheme), para as salas ficarem iguais nos dois.

const (
	roomW, roomH, roomFrame, roomHeader = 100, 64, 2, 10
	sceneX, sceneY                      = roomFrame, roomFrame + roomHeader
	sceneW, sceneH                      = roomW - 2*roomFrame, roomH - sceneY - roomFrame
	genericRoom                         = "generica"
)

// Desenha em "pontos" da sala: cada ponto vira U×U pixels.
type Painter struct {
	Img       *ebiten.Image
	U, OX, OY float32
}

func rnd32(v float32) float32 { return float32(math.Round(float64(v))) }

func (p Painter) R(c color.Color, x, y, w, h float32) {
	vector.FillRect(p.Img, p.OX+rnd32(x*p.U), p.OY+rnd32(y*p.U), rnd32(w*p.U), rnd32(h*p.U), c, false)
}

func (p Painter) RH(hex string, x, y, w, h float32) { p.R(col(hex), x, y, w, h) }

// Retângulo com os cantos "comidos" (cara de pixel art).
func (p Painter) Box(c color.Color, x, y, w, h float32) {
	p.R(c, x+1, y, w-2, h)
	p.R(c, x, y+1, w, h-2)
}

func (p Painter) Glow(c color.RGBA, alpha int, cx, cy, radius float32) {
	a := uint8(max(0, min(255, alpha)))
	vector.FillCircle(p.Img, p.OX+cx*p.U, p.OY+cy*p.U, radius*p.U, color.NRGBA{c.R, c.G, c.B, a}, true)
}

type Spot struct {
	X              float32
	Kind, WalkKind string
	FaceRight      bool
	MinT, MaxT     float32
}

type RoomRect struct {
	C          color.RGBA
	X, Y, W, H float32
}

type RoomAnim struct {
	Type                                         string
	When                                         map[string]bool
	Points                                       [][]float32
	Colors                                       []color.RGBA
	Speed, Threshold, X, Y, W, H, Radius, Height float32
	VX, VY, SizeW, SizeH                         float32
	N                                            int
	Color, Ready, Fail, Background               color.RGBA
	StateColors                                  map[string]color.RGBA
	Blink                                        bool
	Frames                                       [][]RoomRect
}

type RoomTheme struct {
	ID, Name, Description, Accent string
	Characters                    []string
	Skins                         map[string][]string
	FloorY, CharHeight            float32
	Spots                         []Spot
	back, front                   []RoomRect
	image                         string // arte de fundo opcional ("imagem": "mina.png", em salas/)
	bg                            *ebiten.Image
	anims                         []*RoomAnim
}

var allThemes []*RoomTheme

func themes() []*RoomTheme {
	if allThemes != nil {
		return allThemes
	}
	files, _ := filepath.Glob(filepath.Join(roomsDir(), "*.json"))
	sort.Strings(files)
	for _, f := range files {
		o, err := LoadObj(f)
		if err != nil {
			logf("sala %s: %v", filepath.Base(f), err)
			continue
		}
		allThemes = append(allThemes, loadTheme(o))
	}
	if len(allThemes) == 0 {
		allThemes = []*RoomTheme{{ID: genericRoom, Name: "Salinha", Accent: "#8a7cf0", FloorY: 42, CharHeight: 22,
			Skins: map[string][]string{}, Spots: []Spot{{X: 40, Kind: "trabalhar", WalkKind: "andar", FaceRight: true, MinT: 4, MaxT: 8}}}}
	}
	return allThemes
}

func findTheme(id string) *RoomTheme {
	for _, t := range themes() {
		if t.ID == id {
			return t
		}
	}
	for _, t := range themes() {
		if t.ID == genericRoom {
			return t
		}
	}
	return themes()[0]
}

// A sala do personagem; quem não tem sala própria trabalha na genérica.
func themeForCharacter(id string) *RoomTheme {
	if id == "oompa-loompas" {
		id = "chocolateiros"
	}
	for _, t := range themes() {
		for _, c := range t.Characters {
			if c == id {
				return t
			}
		}
	}
	return findTheme(genericRoom)
}

func readRect(o any) RoomRect {
	a, _ := o.([]any)
	if len(a) < 5 {
		return RoomRect{}
	}
	s, _ := a[0].(string)
	return RoomRect{C: col(s), X: toF32(a[1]), Y: toF32(a[2]), W: toF32(a[3]), H: toF32(a[4])}
}

func readRects(list []any) []RoomRect {
	out := make([]RoomRect, 0, len(list))
	for _, o := range list {
		out = append(out, readRect(o))
	}
	return out
}

func loadTheme(d *Obj) *RoomTheme {
	t := &RoomTheme{Skins: map[string][]string{}}
	t.ID = jStr(d, "id", "sala")
	t.Name = jStr(d, "nome", t.ID)
	t.Description = jStr(d, "descricao", "")
	t.image = jStr(d, "imagem", "")
	t.Characters, _ = jStrs(d, "personagens")
	t.Accent = jStr(d, "cor", "#7c6cf0")
	t.FloorY = float32(jNum(d, "chao", 40))
	t.CharHeight = float32(jNum(d, "altura", 24))
	if sk := jObj(d, "skins"); sk != nil {
		for _, k := range sk.Keys() {
			t.Skins[k], _ = jStrs(sk, k)
		}
	}
	for _, o := range jArr(d, "lugares") {
		l, ok := o.(*Obj)
		if !ok {
			continue
		}
		t.Spots = append(t.Spots, Spot{X: float32(jNum(l, "x", 40)), Kind: jStr(l, "tipo", "trabalhar"), WalkKind: jStr(l, "andar", "andar"),
			FaceRight: jBool(l, "direita", true), MinT: float32(jNum(l, "min", 4)), MaxT: float32(jNum(l, "max", 8))})
	}
	if len(t.Spots) == 0 {
		t.Spots = []Spot{{X: 40, Kind: "trabalhar", WalkKind: "andar", FaceRight: true, MinT: 4, MaxT: 8}}
	}
	t.back = readRects(jArr(d, "fundo"))
	t.front = readRects(jArr(d, "frente"))
	for _, o := range jArr(d, "animacoes") {
		if a, ok := o.(*Obj); ok {
			t.anims = append(t.anims, readAnim(a))
		}
	}
	return t
}

func readAnim(a *Obj) *RoomAnim {
	r := &RoomAnim{StateColors: map[string]color.RGBA{}, SizeW: 1, SizeH: 1}
	r.Type = jStr(a, "tipo", "")
	if when, ok := jStrs(a, "quando"); ok {
		r.When = map[string]bool{}
		for _, w := range when {
			r.When[w] = true
		}
	}
	for _, p := range jArr(a, "pontos") {
		list, _ := p.([]any)
		pt := make([]float32, len(list))
		for i, v := range list {
			pt[i] = toF32(v)
		}
		r.Points = append(r.Points, pt)
	}
	if cores, ok := jStrs(a, "cores"); ok {
		for _, c := range cores {
			r.Colors = append(r.Colors, col(c))
		}
	}
	r.Speed = float32(jNum(a, "vel", 1))
	r.Threshold = float32(jNum(a, "limiar", 0))
	r.X, r.Y = float32(jNum(a, "x", 0)), float32(jNum(a, "y", 0))
	r.W, r.H = float32(jNum(a, "w", 1)), float32(jNum(a, "h", 1))
	r.Radius = float32(jNum(a, "raio", 10))
	r.Height = float32(jNum(a, "altura", 5))
	r.N = int(jNum(a, "n", 2))
	r.VX, r.VY = float32(jNum(a, "vx", 0)), float32(jNum(a, "vy", 0))
	if size := jArr(a, "tam"); len(size) >= 2 {
		r.SizeW, r.SizeH = toF32(size[0]), toF32(size[1])
	}
	r.Color = col(jStr(a, "cor", "#ffffff"))
	r.Ready = col(jStr(a, "pronto", jStr(a, "cor", "#ffffff")))
	r.Fail = col(jStr(a, "erro", jStr(a, "cor", "#ffffff")))
	r.Background = col(jStr(a, "fundo", "#0f1a24"))
	r.Blink = jBool(a, "piscar", false)
	if sc := jObj(a, "cores"); sc != nil {
		for _, k := range sc.Keys() {
			r.StateColors[k] = col(jStr(sc, k, "#ffffff"))
		}
	}
	if area := jArr(a, "area"); len(area) >= 4 {
		r.X, r.Y, r.W, r.H = toF32(area[0]), toF32(area[1]), toF32(area[2]), toF32(area[3])
	}
	for _, q := range jArr(a, "quadros") {
		list, _ := q.([]any)
		r.Frames = append(r.Frames, readRects(list))
	}
	return r
}

func stateName(st RoomState) string {
	switch st {
	case Waiting:
		return "esperando"
	case Done:
		return "pronto"
	case Failed:
		return "erro"
	case Stopped:
		return "parado"
	}
	return "trabalhando"
}

// Número pseudoaleatório em [0, 1), igual ao RoomTheme.Hash do C#.
func hashN(n int) float64 {
	x := uint32(n) * 2654435761
	x ^= x >> 15
	x *= 2246822519
	x ^= x >> 13
	x *= 3266489917
	x ^= x >> 16
	return float64(x) / 4294967296.0
}

func fmod(a, m float32) float32 {
	if m <= 0 {
		return 0
	}
	r := float32(math.Mod(float64(a), float64(m)))
	if r < 0 {
		r += m
	}
	return r
}

func (t *RoomTheme) Background(p Painter) {
	if t.image != "" && t.bg == nil {
		if f, err := os.Open(filepath.Join(roomsDir(), t.image)); err == nil {
			if img, err := png.Decode(f); err == nil {
				t.bg = ebiten.NewImageFromImage(img)
			} else {
				logf("imagem da sala %s: %v", t.image, err)
			}
			f.Close()
		}
	}
	if t.bg != nil {
		b := t.bg.Bounds()
		op := &ebiten.DrawImageOptions{}
		op.GeoM.Scale(float64(sceneW*p.U)/float64(b.Dx()), float64(sceneH*p.U)/float64(b.Dy()))
		op.GeoM.Translate(float64(p.OX), float64(p.OY))
		p.Img.DrawImage(t.bg, op)
	}
	for _, r := range t.back {
		p.R(r.C, r.X, r.Y, r.W, r.H)
	}
}

func (t *RoomTheme) Front(p Painter) {
	for _, r := range t.front {
		p.R(r.C, r.X, r.Y, r.W, r.H)
	}
}

var codeColors = []string{"#7fd1ae", "#ffca3a", "#8ab4f8", "#f28b82", "#c58af9", "#e8eaed"}

func (t *RoomTheme) Animate(p Painter, tm float64, st RoomState) {
	stn := stateName(st)
	for _, a := range t.anims {
		if a.When != nil && !a.When[stn] {
			continue
		}
		switch a.Type {
		case "piscar":
			for i, pt := range a.Points {
				if math.Sin(tm*float64(a.Speed)+float64(i)*1.7) <= float64(a.Threshold) || len(a.Colors) == 0 || len(pt) < 2 {
					continue
				}
				w, h := float32(1), float32(1)
				if len(pt) > 3 {
					w, h = pt[2], pt[3]
				}
				p.R(a.Colors[i%len(a.Colors)], pt[0], pt[1], w, h)
			}
		case "brilho":
			for i, pt := range a.Points {
				if math.Sin(tm*1.7+float64(i)*2.1) > 0.85 {
					p.RH("#ffffff", pt[0], pt[1], 1, 1)
					p.RH("#ffffff", pt[0]-1, pt[1]+1, 1, 1)
				}
			}
		case "fumaca":
			for i := 0; i < a.N; i++ {
				ph := math.Mod(tm*0.8+float64(i)*0.5, 1.0)
				p.R(a.Color, a.X+float32(math.Round(math.Sin(ph*6+float64(i))*0.8)), a.Y-float32(ph*float64(a.Height)), 1, 1)
			}
		case "luz":
			flick := 0.5 + 0.25*math.Sin(tm*13) + 0.25*math.Sin(tm*7.3+1)
			c := a.Color
			if st == Done {
				c = a.Ready
			} else if st == Failed {
				c = a.Fail
			}
			p.Glow(c, int(9+5*flick), a.X, a.Y, a.Radius)
			p.Glow(c, int(12+7*flick), a.X, a.Y, a.Radius*0.65)
			p.Glow(c, int(18+10*flick), a.X, a.Y, a.Radius*0.35)
		case "estado":
			c, ok := a.StateColors[stn]
			if !ok {
				if c, ok = a.StateColors["trabalhando"]; !ok {
					c = col(t.Accent)
				}
			}
			speed := 1.2
			if st == Waiting {
				speed = 2.5
			}
			if a.Blink && (st == Waiting || st == Working) && int(tm*speed)%2 == 1 {
				break
			}
			p.R(c, a.X, a.Y, a.W, a.H)
		case "quadros":
			if len(a.Frames) == 0 {
				break
			}
			i := 0
			if a.Speed > 0 {
				i = int(tm*float64(a.Speed)) % len(a.Frames)
			}
			for _, r := range a.Frames[i] {
				p.R(r.C, r.X, r.Y, r.W, r.H)
			}
		case "particulas":
			if len(a.Colors) == 0 {
				break
			}
			for i := 0; i < a.N; i++ {
				k := 0.7 + 0.6*float32(hashN(i*7+13))
				x := a.X + fmod(float32(hashN(i*7+11))*a.W+float32(tm)*a.VX*k, a.W)
				y := a.Y + fmod(float32(hashN(i*7+12))*a.H+float32(tm)*a.VY*k, a.H)
				p.R(a.Colors[i%len(a.Colors)], float32(math.Floor(float64(x))), float32(math.Floor(float64(y))), a.SizeW, a.SizeH)
			}
		case "tela":
			t.screen(p, a, tm, st)
		}
	}
}

// Monitor: código subindo enquanto trabalha, visto verde no fim, X vermelho no erro e "!" esperando.
func (t *RoomTheme) screen(p Painter, a *RoomAnim, tm float64, st RoomState) {
	cx := float32(math.Floor(float64(a.X + a.W/2)))
	cy := float32(math.Floor(float64(a.Y + a.H/2)))
	switch st {
	case Done:
		p.RH("#123826", a.X, a.Y, a.W, a.H)
		for _, d := range [][2]float32{{-5, 0}, {-3, 2}, {-1, 0}, {1, -2}, {3, -4}} {
			p.RH("#3ecf7a", cx+d[0], cy+d[1], 2, 2)
		}
	case Failed:
		p.RH("#3a1418", a.X, a.Y, a.W, a.H)
		for i := float32(0); i < 7; i++ {
			p.RH("#f05a5a", cx-3+i, cy-3+i, 1, 1)
			p.RH("#f05a5a", cx+3-i, cy-3+i, 1, 1)
		}
	case Waiting:
		p.RH("#3a2e10", a.X, a.Y, a.W, a.H)
		if int(tm*2)%2 == 0 {
			p.RH("#ffb020", cx, cy-3, 2, 5)
			p.RH("#ffb020", cx, cy+3, 2, 2)
		}
	default:
		p.R(a.Background, a.X, a.Y, a.W, a.H)
		colors := a.Colors
		if len(colors) == 0 {
			for _, h := range codeColors {
				colors = append(colors, col(h))
			}
		}
		speed := 0.0
		if st == Working {
			speed = 2.2
		}
		scroll := int(tm * speed)
		rows := max(1, int((a.H-1)/2))
		maxLen := max(1, int(a.W)-2)
		for row := 0; row < rows; row++ {
			n := scroll + row
			indent := int(hashN(n*3+1)*3) * 2
			ln := 3 + int(hashN(n*3+2)*10)
			if ln+indent > maxLen {
				ln = max(1, maxLen-indent)
			}
			y := a.Y + 1 + float32(row*2)
			if row == rows-1 {
				frac := tm*speed - math.Floor(tm*speed)
				ln = max(1, int(float64(ln)*frac))
				if int(tm*4)%2 == 0 {
					p.RH("#e8eaed", a.X+1+float32(indent+ln), y, 1, 1)
				}
			}
			p.R(colors[int(hashN(n*3+3)*float64(len(colors)))%len(colors)], a.X+1+float32(indent), y, float32(ln), 1)
		}
	}
}
