package main

import "math"

// A configuração (config.json), com as mesmas chaves do programa do Windows (src/Data.cs, classe Config).
type Config struct {
	Mode        string // "salas", "equipe" ou "ambos"
	Enabled     bool
	IdleMinutes float64
	Bubbles     bool

	// equipe
	Character, Skin, Position, VerbMode string
	Count, Size                         int
	Speed                               float64
	TerminalPhrases, TerminalTips       bool
	Custom                              *Obj

	// salas: "" = o mesmo personagem da equipe, "*" = alternar entre todos, ou o id de um personagem
	RoomCharacter   string
	RoomSkin        string // cor de quem trabalha na sala quando ele foi escolhido à parte ("*" = misturar)
	Corner          string
	RoomSize        int
	DoneSeconds     float64
	ShowName        bool
	RoomPermissions bool

	SoundDone, SoundAttention, SoundFailure bool
}

// Rede de segurança: sem Stop nem interrupção, a sala sai depois desse tempo sem nada mexer.
const minIdleMinutes = 30

func defaultConfig() *Config {
	return &Config{
		Mode: "ambos", Enabled: true, IdleMinutes: 45, Bubbles: true,
		Character: "chocolateiros", Skin: "classico", Position: "rodape", VerbMode: "replace",
		Count: 4, Size: 3, Speed: 1, TerminalPhrases: true, TerminalTips: true, Custom: NewObj(),
		RoomCharacter: "", RoomSkin: "*", Corner: "direita", RoomSize: 2, DoneSeconds: 10, ShowName: true, RoomPermissions: true,
		SoundDone: true, SoundAttention: true, SoundFailure: true,
	}
}

func (c *Config) CrewOn() bool  { return c.Mode == "equipe" || c.Mode == "ambos" }
func (c *Config) RoomsOn() bool { return c.Mode == "salas" || c.Mode == "ambos" }

func loadConfig() *Config { return loadConfigFrom(configFile()) }

func loadConfigFrom(path string) *Config {
	c := defaultConfig()
	d, err := LoadObj(path)
	if err != nil {
		logf("config inválida: %v", err)
		return c
	}
	clamp := func(v, lo, hi float64) float64 { return math.Max(lo, math.Min(hi, v)) }
	c.Mode = jStr(d, "modo", c.Mode)
	if v, ok := d.Get("sala"); ok {
		c.RoomCharacter, _ = v.(string)
	} else {
		c.RoomCharacter = fromOldTheme(jStr(d, "tema", ""))
	}
	c.RoomSkin = jStr(d, "skinSala", c.RoomSkin)
	c.Corner = jStr(d, "canto", c.Corner)
	c.RoomSize = int(clamp(jNum(d, "tamanhoSala", float64(c.RoomSize)), 1, 6))
	c.DoneSeconds = clamp(jNum(d, "segundosFim", c.DoneSeconds), 2, 120)
	c.ShowName = jBool(d, "mostrarNome", c.ShowName)
	c.RoomPermissions = jBool(d, "permissoesNaSala", c.RoomPermissions)
	c.SoundDone = jBool(d, "somPronto", c.SoundDone)
	c.SoundAttention = jBool(d, "somAtencao", c.SoundAttention)
	c.SoundFailure = jBool(d, "somFalha", c.SoundFailure)
	c.Character = jStr(d, "personagem", c.Character)
	c.Skin = jStr(d, "skin", c.Skin)
	c.Position = jStr(d, "posicao", c.Position)
	c.VerbMode = jStr(d, "modoVerbos", c.VerbMode)
	c.Count = int(jNum(d, "quantidade", float64(c.Count)))
	c.Size = int(jNum(d, "tamanho", float64(c.Size)))
	c.Speed = jNum(d, "velocidade", c.Speed)
	c.IdleMinutes = jNum(d, "inatividadeMin", c.IdleMinutes)
	c.Enabled = jBool(d, "ativo", c.Enabled)
	c.Bubbles = jBool(d, "baloes", c.Bubbles)
	c.TerminalPhrases = jBool(d, "frasesTerminal", c.TerminalPhrases)
	c.TerminalTips = jBool(d, "dicasTerminal", c.TerminalTips)
	if p := jObj(d, "personalizacoes"); p != nil {
		c.Custom = p
	}
	return c
}

// Configurações antigas escolhiam o tema da sala; hoje cada personagem tem a sua.
func fromOldTheme(theme string) string {
	switch theme {
	case "*":
		return "*"
	case "escritorio":
		return "devs-hd"
	case "mina":
		return "anoes"
	}
	return ""
}

func (c *Config) Save() error { return c.SaveTo(configFile()) }

func (c *Config) SaveTo(path string) error {
	d := NewObj()
	d.Set("ativo", c.Enabled)
	d.Set("modo", c.Mode)
	d.Set("personagem", c.Character)
	d.Set("skin", c.Skin)
	d.Set("quantidade", c.Count)
	d.Set("velocidade", c.Speed)
	d.Set("tamanho", c.Size)
	d.Set("posicao", c.Position)
	d.Set("baloes", c.Bubbles)
	d.Set("inatividadeMin", c.IdleMinutes)
	d.Set("frasesTerminal", c.TerminalPhrases)
	d.Set("modoVerbos", c.VerbMode)
	d.Set("dicasTerminal", c.TerminalTips)
	d.Set("sala", c.RoomCharacter)
	d.Set("skinSala", c.RoomSkin)
	d.Set("canto", c.Corner)
	d.Set("tamanhoSala", c.RoomSize)
	d.Set("segundosFim", c.DoneSeconds)
	d.Set("mostrarNome", c.ShowName)
	d.Set("permissoesNaSala", c.RoomPermissions)
	d.Set("somPronto", c.SoundDone)
	d.Set("somAtencao", c.SoundAttention)
	d.Set("somFalha", c.SoundFailure)
	d.Set("personalizacoes", c.Custom)
	return SaveJSON(path, d)
}

// Frases do personagem: as editadas pela pessoa ou as padrão.
func (c *Config) Phrases(ch *Character, kind string) []string {
	if per := jObj(c.Custom, ch.ID); per != nil {
		if l, ok := jStrs(per, kind); ok {
			return l
		}
	}
	return ch.Phrases[kind]
}

// O personagem da próxima sala; n = quantas salas já abriram (para alternar entre todos).
func (c *Config) RoomCharacterFor(all []*Character, n int) *Character {
	if len(all) == 0 {
		return nil
	}
	id := c.RoomCharacter
	if id == "*" {
		return all[n%len(all)]
	}
	if id == "" {
		id = c.Character
	}
	if id == "oompa-loompas" {
		id = "chocolateiros"
	}
	if ch := findCharacterExact(all, id); ch != nil {
		return ch
	}
	// personagem de um pacote que não está instalado: quem mais trabalha naquela sala
	for _, other := range themeForCharacter(id).Characters {
		if ch := findCharacterExact(all, other); ch != nil {
			return ch
		}
	}
	return findCharacter(all, c.Character)
}

// A cor da sala: a escolhida para ela, quando o personagem da sala foi escolhido à parte;
// senão a da equipe, quando é o mesmo personagem.
func (c *Config) RoomSkinFor(ch *Character) string {
	if ch == nil {
		return ""
	}
	if c.RoomCharacter != "" && ch.ID == c.RoomCharacter {
		return c.RoomSkin
	}
	if ch.ID == c.Character {
		return c.Skin
	}
	return ""
}
