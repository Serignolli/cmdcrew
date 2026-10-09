package main

import (
	"bytes"
	"image/color"
	"os"
	"path/filepath"
	"strings"

	"github.com/hajimehoshi/ebiten/v2"
	"github.com/hajimehoshi/ebiten/v2/text/v2"
)

// A fonte Pixelify Sans (recursos/PixelifySans.ttf), a mesma das salas no Windows.
var pixelSource *text.GoTextFaceSource

func pixelFace(size float64) text.Face {
	if pixelSource == nil {
		data, err := os.ReadFile(filepath.Join(resourcesDir(), "PixelifySans.ttf"))
		if err == nil {
			pixelSource, err = text.NewGoTextFaceSource(bytes.NewReader(data))
		}
		if err != nil {
			logf("fonte: %v", err)
			return nil
		}
	}
	return &text.GoTextFace{Source: pixelSource, Size: size}
}

func measure(s string, face text.Face) (float64, float64) {
	if face == nil {
		return float64(len(s)) * 6, 10
	}
	return text.Measure(s, face, face.Metrics().HAscent+face.Metrics().HDescent)
}

func drawText(dst *ebiten.Image, s string, face text.Face, x, y float64, c color.Color) {
	if face == nil {
		return
	}
	op := &text.DrawOptions{}
	op.GeoM.Translate(x, y)
	op.ColorScale.ScaleWithColor(c)
	text.Draw(dst, s, face, op)
}

// Corta o texto com "…" para caber em maxW pixels.
func ellipsize(s string, face text.Face, maxW float64) string {
	if w, _ := measure(s, face); w <= maxW {
		return s
	}
	r := []rune(s)
	for len(r) > 1 {
		r = r[:len(r)-1]
		if w, _ := measure(string(r)+"…", face); w <= maxW {
			break
		}
	}
	return string(r) + "…"
}

// Quebra o texto em linhas de até maxW pixels.
func wrapText(s string, face text.Face, maxW float64) []string {
	var lines []string
	cur := ""
	for _, word := range strings.Fields(s) {
		try := word
		if cur != "" {
			try = cur + " " + word
		}
		if w, _ := measure(try, face); w > maxW && cur != "" {
			lines = append(lines, cur)
			cur = word
		} else {
			cur = try
		}
	}
	if cur != "" {
		lines = append(lines, cur)
	}
	return lines
}
