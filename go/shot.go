package main

import (
	"image"
	"image/png"
	"os"
	"time"

	"github.com/hajimehoshi/ebiten/v2"
)

// "shot": desenha uma sala num PNG, para conferir os desenhos (e comparar com o programa do Windows).
type shotGame struct {
	what, state, out string
	secs             float64
	err              error
}

func runShot(what, state, out string, secs float64) {
	g := &shotGame{what: what, state: state, out: out, secs: secs}
	ebiten.SetWindowDecorated(false)
	ebiten.SetWindowSize(1, 1)
	ebiten.SetWindowPosition(-10000, -10000)
	if err := ebiten.RunGameWithOptions(g, &ebiten.RunGameOptions{InitUnfocused: true, SkipTaskbar: true}); err != nil && err != ebiten.Termination {
		logf("shot: %v", err)
	}
	if g.err != nil {
		logf("shot: %v", g.err)
		os.Exit(1)
	}
}

func (g *shotGame) Update() error {
	all := loadAllCharacters(nil)
	theme := themeForCharacter(g.what)
	for _, t := range themes() {
		if t.ID == g.what {
			theme = t
		}
	}
	ch := findCharacterExact(all, g.what)
	if ch == nil {
		for _, id := range theme.Characters {
			if ch = findCharacterExact(all, id); ch != nil {
				break
			}
		}
	}
	if ch == nil && len(all) > 0 {
		ch = all[0]
	}
	if ch == nil {
		g.err = os.ErrNotExist
		return ebiten.Termination
	}
	st := Working
	switch g.state {
	case "pronto":
		st = Done
	case "aguardando", "permissao":
		st = Waiting
	case "erro":
		st = Failed
	}
	r := newRoom(theme, ch, 0, 1, "")
	r.ProjectName = "meu-projeto"
	r.Started = time.Now().Add(-83 * time.Second)
	if g.state == "permissao" {
		r.Ask = &PermissionAsk{ID: "x", Tool: "Bash", Summary: "npm test -- --coverage"}
	}
	r.SetScale(3)
	r.Enter = 1
	for t := 0.0; t < g.secs; t += 0.033 {
		if t > g.secs-1.2 && st != Working {
			r.SetState(st, time.Now())
		}
		r.Update(0.033, true)
	}
	img := ebiten.NewImage(r.PixelWidth(), r.PixelHeight())
	r.Draw(img, 0, 0)
	rgba := image.NewRGBA(img.Bounds())
	img.ReadPixels(rgba.Pix)
	f, err := os.Create(g.out)
	if err != nil {
		g.err = err
		return ebiten.Termination
	}
	defer f.Close()
	g.err = png.Encode(f, rgba)
	return ebiten.Termination
}

func (g *shotGame) Draw(*ebiten.Image)         {}
func (g *shotGame) Layout(w, h int) (int, int) { return max(1, w), max(1, h) }
