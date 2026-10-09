//go:build !windows

package main

import (
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"syscall"
)

// Abre "cmdcrew <args>" fora do grupo de processos do agente (senão a janela morre junto com o gancho).
func launchDetached(args ...string) {
	cmd := exec.Command(paths.Exe, args...)
	cmd.Dir = filepath.Dir(paths.Exe)
	cmd.SysProcAttr = &syscall.SysProcAttr{Setsid: true}
	if err := cmd.Start(); err != nil {
		logf("abrir %v: %v", args, err)
		return
	}
	_ = cmd.Process.Release()
}

type instanceLock struct{ f *os.File }

// Uma janela de cada tipo por vez (salas, equipe): trava um arquivo em <estado>/<nome>.lock.
func acquireLock(name string) (*instanceLock, bool) {
	_ = os.MkdirAll(stateDir(), 0o755)
	f, err := os.OpenFile(filepath.Join(stateDir(), name+".lock"), os.O_CREATE|os.O_RDWR, 0o644)
	if err != nil {
		return nil, false
	}
	if err := syscall.Flock(int(f.Fd()), syscall.LOCK_EX|syscall.LOCK_NB); err != nil {
		f.Close()
		return nil, false
	}
	_ = f.Truncate(0)
	_, _ = f.WriteString(strconv.Itoa(os.Getpid()))
	return &instanceLock{f}, true
}

func (l *instanceLock) Release() {
	if l == nil {
		return
	}
	_ = syscall.Flock(int(l.f.Fd()), syscall.LOCK_UN)
	l.f.Close()
}

func isRunning(name string) bool {
	l, ok := acquireLock(name)
	if ok {
		l.Release()
		return false
	}
	return true
}

// Fecha as janelas abertas (salas e equipe), pelo pid gravado no arquivo de trava.
func killOthers() {
	for _, name := range []string{"salas", "equipe"} {
		if !isRunning(name) {
			continue
		}
		data, err := os.ReadFile(filepath.Join(stateDir(), name+".lock"))
		if err != nil {
			continue
		}
		if pid, err := strconv.Atoi(strings.TrimSpace(string(data))); err == nil && pid != os.Getpid() {
			_ = syscall.Kill(pid, syscall.SIGTERM)
		}
	}
}

// Quem é o terminal desta sessão, para o duplo clique na sala voltar até ele.
// No macOS: o app que abriu o agente (o próprio sistema põe o id dele no ambiente).
// No Linux (X11): a janela ativa quando o prompt foi enviado.
func captureTerminal() string {
	if runtime.GOOS == "darwin" {
		if id := os.Getenv("__CFBundleIdentifier"); id != "" {
			return "app:" + id
		}
		return ""
	}
	if os.Getenv("DISPLAY") == "" {
		return ""
	}
	if w := os.Getenv("WINDOWID"); w != "" {
		return "x11:" + w
	}
	if out, err := exec.Command("xdotool", "getactivewindow").Output(); err == nil {
		return "x11:" + strings.TrimSpace(string(out))
	}
	return ""
}

func focusTerminal(t string) {
	switch {
	case strings.HasPrefix(t, "app:"):
		_ = exec.Command("open", "-b", strings.TrimPrefix(t, "app:")).Start()
	case strings.HasPrefix(t, "x11:"):
		id := strings.TrimPrefix(t, "x11:")
		if err := exec.Command("xdotool", "windowactivate", id).Start(); err != nil {
			_ = exec.Command("wmctrl", "-i", "-a", id).Start()
		}
	}
}

func openFolder(p string) {
	if runtime.GOOS == "darwin" {
		_ = exec.Command("open", p).Start()
	} else {
		_ = exec.Command("xdg-open", p).Start()
	}
}
