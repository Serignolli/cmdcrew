package main

import (
	"bytes"
	"os"
	"path/filepath"

	"github.com/hajimehoshi/ebiten/v2/audio"
	"github.com/hajimehoshi/ebiten/v2/audio/wav"
)

// Sons das salas (recursos/sons/*.wav, feitos no SFX Forge).
var (
	audioCtx *audio.Context
	sounds   = map[string][]byte{}
)

func playSound(name string) {
	defer func() {
		if r := recover(); r != nil {
			logf("som %s: %v", name, r)
		}
	}()
	if audioCtx == nil {
		audioCtx = audio.NewContext(44100)
	}
	pcm, ok := sounds[name]
	if !ok {
		data, err := os.ReadFile(filepath.Join(resourcesDir(), "sons", name+".wav"))
		if err != nil {
			logf("som %s: %v", name, err)
			sounds[name] = nil
			return
		}
		st, err := wav.DecodeF32(bytes.NewReader(data))
		if err != nil {
			logf("som %s: %v", name, err)
			sounds[name] = nil
			return
		}
		var buf bytes.Buffer
		if _, err := buf.ReadFrom(st); err != nil {
			logf("som %s: %v", name, err)
		}
		pcm = buf.Bytes()
		sounds[name] = pcm
	}
	if len(pcm) == 0 {
		return
	}
	audioCtx.NewPlayerF32FromBytes(pcm).Play()
}
