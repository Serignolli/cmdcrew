package main

import (
	"archive/zip"
	"bufio"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"runtime"
	"sort"
	"strconv"
	"strings"
)

// Assistente de instalação e configuração no terminal (no Windows são janelas: SetupForm e ConfigForm).

var stdin = bufio.NewReader(os.Stdin)

func ask(prompt string) string {
	fmt.Print(prompt)
	line, _ := stdin.ReadString('\n')
	return strings.TrimSpace(line)
}

func askYesNo(prompt string, def bool) bool {
	hint := " [S/n] "
	if !def {
		hint = " [s/N] "
	}
	for {
		a := strings.ToLower(ask(prompt + hint))
		switch a {
		case "":
			return def
		case "s", "sim", "y", "yes":
			return true
		case "n", "nao", "não", "no":
			return false
		}
	}
}

// Mostra a lista numerada e devolve o índice escolhido (ou def se só apertar Enter).
func choose(title string, options []string, def int) int {
	fmt.Println()
	fmt.Println(title)
	for i, o := range options {
		mark := "  "
		if i == def {
			mark = "▸ "
		}
		fmt.Printf("  %s%2d. %s\n", mark, i+1, o)
	}
	for {
		a := ask(fmt.Sprintf("Número [%d]: ", def+1))
		if a == "" {
			return def
		}
		if n, err := strconv.Atoi(a); err == nil && n >= 1 && n <= len(options) {
			return n - 1
		}
	}
}

func bold(s string) string {
	if runtime.GOOS == "windows" {
		return s
	}
	return "\033[1m" + s + "\033[0m"
}

func sortedCharacters() []*Character {
	all := loadAllCharacters(nil)
	order := []string{"anoes", "gnomos-jardim", "elfos-hd", "chocolateiros", "programadores", "clawd", "claude", "dragao", "elfos", "gatos", "pinguins", "formigas", "robos", "robos-classicos", "cachorros", "gnomos"}
	idx := func(id string) int {
		for i, o := range order {
			if o == id {
				return i
			}
		}
		return 99
	}
	sort.SliceStable(all, func(i, j int) bool { return idx(all[i].ID) < idx(all[j].ID) })
	return all
}

func charLabel(c *Character) string {
	l := c.Name + " · sala: " + themeForCharacter(c.ID).Name
	if c.Premium {
		l += "  ★"
	}
	return l
}

func chooseCharacter(title, current string) *Character {
	all := sortedCharacters()
	var labels []string
	def := 0
	for i, c := range all {
		labels = append(labels, charLabel(c))
		if c.ID == current {
			def = i
		}
	}
	return all[choose(title, labels, def)]
}

func chooseSkin(c *Character, current string) string {
	labels := []string{"Misturar todas as cores"}
	def := 0
	for i, s := range c.Skins {
		labels = append(labels, s.Name)
		if s.ID == current {
			def = i + 1
		}
	}
	i := choose("Cores", labels, def)
	if i == 0 {
		return "*"
	}
	return c.Skins[i-1].ID
}

var modeIDs = []string{"ambos", "salas", "equipe"}
var modeNames = []string{"No terminal e na sala (os dois)", "Só as salas no canto da tela", "Só a equipe andando na tela"}

func indexOf(list []string, v string) int {
	for i, x := range list {
		if x == v {
			return i
		}
	}
	return 0
}

func chooseAgents() string {
	fmt.Println()
	fmt.Println(bold("Com quais agentes?") + " A equipe aparece quando qualquer um deles estiver trabalhando.")
	var ids []string
	for _, a := range allAgents {
		found := a.Detected()
		label := a.Name
		if !found {
			label += " (não encontrado)"
		}
		if askYesNo("  "+label+"?", found || a.ID == "claude") {
			ids = append(ids, a.ID)
		}
	}
	return strings.Join(ids, ",")
}

func runSetup() {
	fmt.Println()
	fmt.Println(bold("CmdCrew "+version) + ": contrate a equipe!")
	fmt.Println("Enquanto o seu agente trabalha, uma equipe de bichinhos em pixel art trabalha junto:")
	fmt.Println("andando na tela e numa salinha no canto. Quando ele termina, eles comemoram e avisam.")
	var found []string
	for _, a := range allAgents {
		if a.Detected() {
			found = append(found, a.Name)
		}
	}
	if len(found) > 0 {
		fmt.Println("\nEncontrei neste computador: " + strings.Join(found, ", ") + ".")
	} else {
		fmt.Println("\nNão encontrei nenhum agente. Dá para instalar mesmo assim.")
	}
	cfg := loadConfig()
	ch := chooseCharacter(bold("Quem vai trabalhar pra você?")+" (ele ganha a sala dele)", cfg.Character)
	cfg.Character = ch.ID
	cfg.Skin = chooseSkin(ch, cfg.Skin)
	cfg.RoomCharacter = "" // o escolhido trabalha na tela e na sala dele
	cfg.Mode = modeIDs[choose("Onde eles aparecem", modeNames, indexOf(modeIDs, "ambos"))]
	cfg.Enabled = true
	agents := chooseAgents()
	if err := cfg.Save(); err != nil {
		fmt.Println("Não deu para salvar a configuração: " + err.Error())
		return
	}
	installHooks(agents)
	fmt.Println()
	fmt.Println(bold("Equipe contratada!") + " No próximo prompt eles aparecem.")
	fmt.Println("Se o agente já estiver aberto, feche e abra de novo para ele perceber a equipe.")
	fmt.Println("Para mudar depois: " + bold("cmdcrew config"))
	if askYesNo("\nVer as salas funcionando agora?", true) {
		launchDetached("demo-salas", "3", "10")
	}
}

func onOff(b bool) string {
	if b {
		return "sim"
	}
	return "não"
}

func runConfig() {
	for {
		cfg := loadConfig()
		all := loadAllCharacters(nil)
		crew := findCharacter(all, cfg.Character)
		room := "o mesmo do terminal"
		if cfg.RoomCharacter == "*" {
			room = "alternar entre todos"
		} else if cfg.RoomCharacter != "" {
			if c := findCharacterExact(all, cfg.RoomCharacter); c != nil {
				room = c.Name
			}
		}
		var on []string
		for _, a := range allAgents {
			if a.Installed() {
				on = append(on, a.Name)
			}
		}
		agents := strings.Join(on, ", ")
		if agents == "" {
			agents = "nenhum"
		}
		skin := "todas misturadas"
		if cfg.Skin != "*" && crew != nil {
			skin = crew.GetSkin(cfg.Skin).Name
		}
		pos := map[string]string{"topo": "no topo da tela"}[cfg.Position]
		if pos == "" {
			pos = "no rodapé da tela"
		}
		corner := "direito"
		if cfg.Corner == "esquerda" {
			corner = "esquerdo"
		}
		opts := []string{
			"Onde eles aparecem: " + modeNames[indexOf(modeIDs, cfg.Mode)],
			"Personagem da equipe: " + crew.Name + " (" + skin + ")",
			"Quem trabalha na sala: " + room,
			fmt.Sprintf("Salas: tamanho %d, canto %s, ficam %.0f s depois de terminar", cfg.RoomSize, corner, cfg.DoneSeconds),
			fmt.Sprintf("Equipe: %d, tamanho %d, %s", cfg.Count, cfg.Size, pos),
			"Agentes: " + agents,
			fmt.Sprintf("Sons: pronto %s, atenção %s, falha %s", onOff(cfg.SoundDone), onOff(cfg.SoundAttention), onOff(cfg.SoundFailure)),
			fmt.Sprintf("Balões de fala: %s · frases no spinner do Claude: %s · permissões na sala: %s", onOff(cfg.Bubbles), onOff(cfg.TerminalPhrases), onOff(cfg.RoomPermissions)),
			"Adicionar pacote (Supporters Pack)…",
			"Testar as salas",
			"Testar a equipe",
			fmt.Sprintf("Ativo: %s", onOff(cfg.Enabled)),
			"Sair",
		}
		i := choose(bold("CmdCrew "+version)+": configuração (tudo é salvo na hora)", opts, len(opts)-1)
		switch i {
		case 0:
			cfg.Mode = modeIDs[choose("Onde eles aparecem", modeNames, indexOf(modeIDs, cfg.Mode))]
		case 1:
			c := chooseCharacter("Personagem da equipe", cfg.Character)
			if c.ID != cfg.Character {
				cfg.Skin = "*"
			}
			cfg.Character = c.ID
			cfg.Skin = chooseSkin(c, cfg.Skin)
		case 2:
			list := sortedCharacters()
			labels := []string{"O mesmo do terminal"}
			def := 0
			for k, c := range list {
				labels = append(labels, charLabel(c))
				if c.ID == cfg.RoomCharacter {
					def = k + 1
				}
			}
			labels = append(labels, "Alternar entre todos (um por terminal)")
			if cfg.RoomCharacter == "*" {
				def = len(labels) - 1
			}
			k := choose("Quem trabalha na sala (cada personagem tem a sua)", labels, def)
			switch {
			case k == 0:
				cfg.RoomCharacter = ""
			case k == len(labels)-1:
				cfg.RoomCharacter = "*"
			default:
				cfg.RoomCharacter = list[k-1].ID
			}
		case 3:
			cfg.RoomSize = int(askNumber("Tamanho das salas (1 a 4)", float64(cfg.RoomSize), 1, 4))
			if choose("Canto", []string{"Direito, embaixo", "Esquerdo, embaixo"}, map[bool]int{true: 1, false: 0}[cfg.Corner == "esquerda"]) == 1 {
				cfg.Corner = "esquerda"
			} else {
				cfg.Corner = "direita"
			}
			cfg.DoneSeconds = askNumber("Segundos que a sala fica depois de terminar", cfg.DoneSeconds, 2, 120)
			cfg.ShowName = askYesNo("Mostrar o nome da pasta do projeto na sala?", cfg.ShowName)
		case 4:
			cfg.Count = int(askNumber("Quantos na equipe (1 a 15)", float64(cfg.Count), 1, 15))
			cfg.Size = int(askNumber("Tamanho (1 a 8)", float64(cfg.Size), 1, 8))
			cfg.Speed = askNumber("Velocidade (0.3 a 3)", cfg.Speed, 0.3, 3)
			if choose("Onde a equipe anda", []string{"No rodapé da tela", "No topo da tela"}, map[bool]int{true: 1, false: 0}[cfg.Position == "topo"]) == 1 {
				cfg.Position = "topo"
			} else {
				cfg.Position = "rodape"
			}
		case 5:
			for _, a := range allAgents {
				was := a.Installed()
				label := a.Name
				if !a.Detected() {
					label += " (não encontrado)"
				}
				want := askYesNo("  "+label+"?", was)
				if want == was {
					continue
				}
				var err error
				if want {
					err = a.Install()
				} else {
					err = a.Remove()
				}
				if err != nil {
					fmt.Println("  Não deu: " + err.Error())
				}
			}
			fmt.Println("Sessões abertas antes disso precisam ser reiniciadas.")
		case 6:
			cfg.SoundDone = askYesNo("Som quando termina?", cfg.SoundDone)
			cfg.SoundAttention = askYesNo("Som quando precisa de você?", cfg.SoundAttention)
			cfg.SoundFailure = askYesNo("Som quando falha?", cfg.SoundFailure)
		case 7:
			cfg.Bubbles = askYesNo("Balões de fala?", cfg.Bubbles)
			cfg.TerminalPhrases = askYesNo("Frases da equipe no spinner do Claude Code?", cfg.TerminalPhrases)
			cfg.RoomPermissions = askYesNo("Permitir ou negar permissões direto na sala?", cfg.RoomPermissions)
		case 8:
			addPack(ask("Caminho do .zip (ou .json) do pacote: "))
		case 9:
			launchDetached("demo-salas", "3", "10")
			fmt.Println("Olha o canto da tela!")
		case 10:
			launchDetached("demo", "12")
			fmt.Println("Olha a equipe na tela! (12 segundos)")
		case 11:
			cfg.Enabled = !cfg.Enabled
		default:
			return
		}
		if err := cfg.Save(); err != nil {
			fmt.Println("Não deu para salvar: " + err.Error())
		}
		if err := applyPhrases(cfg); err != nil {
			logf("frases: %v", err)
		}
	}
}

func askNumber(prompt string, cur, lo, hi float64) float64 {
	for {
		a := ask(fmt.Sprintf("%s [%g]: ", prompt, cur))
		if a == "" {
			return cur
		}
		if v, err := strconv.ParseFloat(strings.ReplaceAll(a, ",", "."), 64); err == nil && v >= lo && v <= hi {
			return v
		}
	}
}

// Um pacote é um .zip (ou .json soltos) com personagens; eles vão para a pasta pacotes/.
func addPack(path string) {
	path = strings.Trim(strings.TrimSpace(path), `"'`)
	if strings.HasPrefix(path, "~/") {
		path = filepath.Join(home(), path[2:])
	}
	if path == "" {
		return
	}
	if err := os.MkdirAll(packsDir(), 0o755); err != nil {
		fmt.Println("Não deu: " + err.Error())
		return
	}
	n := 0
	if strings.HasSuffix(strings.ToLower(path), ".zip") {
		z, err := zip.OpenReader(path)
		if err != nil {
			fmt.Println("Não deu para abrir o pacote: " + err.Error())
			return
		}
		defer z.Close()
		for _, f := range z.File {
			name := filepath.Base(f.Name)
			if !strings.HasSuffix(strings.ToLower(name), ".json") {
				continue
			}
			rc, err := f.Open()
			if err != nil {
				continue
			}
			data, _ := io.ReadAll(rc)
			rc.Close()
			if os.WriteFile(filepath.Join(packsDir(), name), data, 0o644) == nil {
				n++
			}
		}
	} else if data, err := os.ReadFile(path); err == nil {
		if os.WriteFile(filepath.Join(packsDir(), filepath.Base(path)), data, 0o644) == nil {
			n++
		}
	} else {
		fmt.Println("Não deu: " + err.Error())
		return
	}
	fmt.Printf("%d personagem(ns) adicionado(s). Obrigado pelo apoio!\n", n)
}

// Tira dos agentes e apaga a instalação (o atalho em ~/.local/bin, a pasta do programa e o estado).
func uninstallEverything() {
	if !askYesNo("Desinstalar o CmdCrew? A equipe sai de todos os agentes e o programa é apagado.", false) {
		return
	}
	killOthers()
	removeAllAgents()
	link := filepath.Join(home(), ".local", "bin", "cmdcrew")
	if target, err := filepath.EvalSymlinks(link); err == nil && target == paths.Exe {
		_ = os.Remove(link)
	}
	_ = os.RemoveAll(stateDir())
	if fileExists(filepath.Join(paths.Root, "bin", filepath.Base(paths.Exe))) && dirExists(filepath.Join(paths.Root, "personagens")) &&
		!dirExists(filepath.Join(paths.Root, ".git")) {
		_ = os.RemoveAll(paths.Root)
	}
	fmt.Println("Pronto, a equipe foi dispensada. Até a próxima!")
}
