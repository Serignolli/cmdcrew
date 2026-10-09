// CmdCrew para macOS e Linux: o mesmo CmdCrew do Windows (src/, em C#), escrito em Go com o Ebitengine.
// Lê os mesmos personagens/, salas/ e config.json, e conversa com os mesmos agentes.
//
//	cmdcrew setup                 assistente de instalação (no terminal)
//	cmdcrew config                configuração (no terminal)
//	cmdcrew demo-salas [n] [s]    salas de demonstração
//	cmdcrew demo [s]              equipe de demonstração
//	cmdcrew install [agentes]     liga os ganchos (sem lista: o Claude Code e os agentes encontrados)
//	cmdcrew uninstall             tira os ganchos de todos os agentes
//	cmdcrew desinstalar           tira os ganchos e apaga o programa
//	cmdcrew shot <sala|personagem> <estado> <arquivo.png> [segundos]
//
// Os comandos start, wait, stop, fail, end, permission e tool são os ganchos chamados pelos agentes.
package main

import (
	"fmt"
	"os"
	"runtime/debug"
	"strconv"
	"strings"
	"time"
)

const version = "1.1.0"

func main() {
	cmd := ""
	if len(os.Args) > 1 {
		cmd = strings.ToLower(os.Args[1])
	}
	if len(os.Args) > 2 {
		hookAgent = strings.ToLower(os.Args[2])
	}
	defer func() {
		if r := recover(); r != nil {
			// um gancho nunca deve atrapalhar o agente: registra e sai
			logf("%s: %v\n%s", cmd, r, debug.Stack())
			_ = os.MkdirAll(stateDir(), 0o755)
			_ = os.WriteFile(errorFile(), []byte(fmt.Sprint(r)), 0o644)
			os.Exit(1)
		}
	}()
	arg := func(i int, def string) string {
		if len(os.Args) > i {
			return os.Args[i]
		}
		return def
	}
	num := func(i int, def float64) float64 {
		if v, err := strconv.ParseFloat(arg(i, ""), 64); err == nil {
			return v
		}
		return def
	}
	switch cmd {
	case "start":
		hook("trabalhando")
	case "wait":
		hook("aguardando")
	case "stop":
		hook("pronto")
	case "fail":
		hook("erro")
	case "end":
		hookEnd()
	case "permission":
		hookPermission()
	case "tool":
		hookTool()
	case "salas":
		runRooms()
	case "run":
		runCrew(true, 0, loadConfig())
	case "demo":
		runCrew(false, max(2, num(2, 12)), loadConfig())
	case "demo-salas":
		runRoomsDemo(int(num(2, 3)), num(3, 10))
	case "install":
		installHooks(arg(2, ""))
	case "agentes-ligados":
		// usado pelo install.sh na atualização: religa só os agentes que já estavam ligados
		var on []string
		for _, a := range allAgents {
			if a.Installed() {
				on = append(on, a.ID)
			}
		}
		if len(on) == 0 {
			on = []string{"nenhum"}
		}
		fmt.Println(strings.Join(on, ","))
	case "uninstall":
		killOthers()
		removeAllAgents()
	case "desinstalar":
		uninstallEverything()
	case "quit":
		killOthers()
	case "config":
		runConfig()
	case "setup":
		runSetup()
	case "shot":
		runShot(arg(2, "escritorio"), arg(3, "trabalhando"), arg(4, "sala.png"), num(5, 2.5))
	case "version", "--version", "-v":
		fmt.Println("CmdCrew " + version)
	case "", "help", "--help", "-h", "ajuda":
		fmt.Println("CmdCrew " + version + ": uma equipe de bichinhos em pixel art que trabalha junto com o seu agente de IA.")
		fmt.Println()
		fmt.Println("  cmdcrew config             configurar (personagem, salas, agentes, sons)")
		fmt.Println("  cmdcrew demo-salas         ver as salas funcionando")
		fmt.Println("  cmdcrew demo               ver a equipe funcionando")
		fmt.Println("  cmdcrew setup              assistente de instalação")
		fmt.Println("  cmdcrew uninstall          tirar dos agentes (os arquivos ficam)")
		fmt.Println("  cmdcrew desinstalar        tirar dos agentes e apagar o programa")
	default:
		fmt.Fprintln(os.Stderr, "comando desconhecido: "+cmd+" (veja: cmdcrew ajuda)")
		os.Exit(2)
	}
}

func installHooks(ids string) {
	cfg := loadConfig()
	if !fileExists(configFile()) {
		_ = cfg.Save()
	}
	for _, a := range parseAgents(ids) {
		if err := a.Install(); err != nil {
			logf("instalar %s: %v", a.ID, err)
			fmt.Fprintln(os.Stderr, "Não deu para ligar o "+a.Name+": "+err.Error())
		}
	}
	if err := applyPhrases(cfg); err != nil {
		logf("frases: %v", err)
	}
}

// Salas de mentira para ver funcionando sem nenhum agente (a segunda pede uma permissão).
func runRoomsDemo(n int, seconds float64) {
	n = max(1, min(6, n))
	names := []string{"meu-site", "api-pagamentos", "jogo-da-cobrinha", "scripts", "app-mobile", "relatorios"}
	start := time.Now()
	list := make([]*SessionInfo, n)
	for i := 0; i < n; i++ {
		s := &SessionInfo{ID: "demo-" + strconv.Itoa(i), Cwd: "/demo/" + names[i%len(names)], State: "trabalhando",
			Started: start.Add(-time.Duration(37*i+5) * time.Second), Changed: start, Demo: true, Agent: "demo"}
		writeSession(s)
		list[i] = s
		if i == 0 && !isRunning("salas") {
			launchDetached("salas")
		}
		time.Sleep(900 * time.Millisecond)
	}
	for i := 0; i < n; i++ {
		at := seconds + float64(i*4)
		for time.Since(start).Seconds() < at {
			if n > 1 && i == 0 && list[1].State == "trabalhando" && time.Since(start).Seconds() > seconds*0.5 {
				list[1].State, list[1].Changed = "aguardando", time.Now()
				list[1].Ask = &PermissionAsk{ID: "demo-pedido", Tool: "Bash", Summary: "npm test -- --coverage"}
				writeSession(list[1])
			}
			if n > 1 && list[1].Ask != nil && fileExists(answerFile("demo-pedido")) {
				_ = os.Remove(answerFile("demo-pedido"))
				list[1].Ask, list[1].State, list[1].Changed = nil, "trabalhando", time.Now()
				writeSession(list[1])
			}
			time.Sleep(200 * time.Millisecond)
		}
		list[i].Ask = nil
		list[i].State = "pronto"
		if n > 2 && i == n-1 {
			list[i].State = "erro"
		}
		list[i].Changed = time.Now()
		writeSession(list[i])
	}
	time.Sleep(time.Duration(loadConfig().DoneSeconds*1000+2500) * time.Millisecond)
	for _, s := range list {
		deleteSession(s.ID)
	}
}

func splitTrim(s, sep string) []string {
	var out []string
	for _, p := range strings.Split(s, sep) {
		if p = strings.TrimSpace(p); p != "" {
			out = append(out, p)
		}
	}
	return out
}
