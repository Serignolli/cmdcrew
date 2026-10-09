//go:build windows

package main

// No Windows quem roda é o programa em C# (src/). Este arquivo só existe para dar para testar
// o port de macOS/Linux numa máquina Windows.

import (
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"syscall"

	"golang.org/x/sys/windows"
)

func launchDetached(args ...string) {
	cmd := exec.Command(paths.Exe, args...)
	cmd.Dir = filepath.Dir(paths.Exe)
	cmd.SysProcAttr = &syscall.SysProcAttr{CreationFlags: windows.CREATE_NEW_PROCESS_GROUP | windows.DETACHED_PROCESS}
	if err := cmd.Start(); err != nil {
		logf("abrir %v: %v", args, err)
		return
	}
	_ = cmd.Process.Release()
}

type instanceLock struct{ h windows.Handle }

func acquireLock(name string) (*instanceLock, bool) {
	n, _ := windows.UTF16PtrFromString(`Local\CmdCrewGo-` + name)
	h, err := windows.CreateMutex(nil, true, n)
	if err != nil || windows.GetLastError() == windows.ERROR_ALREADY_EXISTS {
		if h != 0 {
			windows.CloseHandle(h)
		}
		return nil, false
	}
	_ = os.MkdirAll(stateDir(), 0o755)
	_ = os.WriteFile(filepath.Join(stateDir(), name+".pid"), []byte(strconv.Itoa(os.Getpid())), 0o644)
	return &instanceLock{h}, true
}

func (l *instanceLock) Release() {
	if l != nil {
		windows.ReleaseMutex(l.h)
		windows.CloseHandle(l.h)
	}
}

func isRunning(name string) bool {
	n, _ := windows.UTF16PtrFromString(`Local\CmdCrewGo-` + name)
	h, err := windows.OpenMutex(windows.SYNCHRONIZE, false, n)
	if err != nil {
		return false
	}
	windows.CloseHandle(h)
	return true
}

func killOthers() {
	for _, name := range []string{"salas", "equipe"} {
		data, err := os.ReadFile(filepath.Join(stateDir(), name+".pid"))
		if err != nil {
			continue
		}
		if pid, err := strconv.Atoi(strings.TrimSpace(string(data))); err == nil && pid != os.Getpid() {
			if p, err := os.FindProcess(pid); err == nil {
				_ = p.Kill()
			}
		}
	}
}

var (
	user32              = windows.NewLazySystemDLL("user32.dll")
	getForegroundWindow = user32.NewProc("GetForegroundWindow")
	setForegroundWindow = user32.NewProc("SetForegroundWindow")
)

func captureTerminal() string {
	h, _, _ := getForegroundWindow.Call()
	if h == 0 {
		return ""
	}
	return "hwnd:" + strconv.FormatUint(uint64(h), 10)
}

func focusTerminal(t string) {
	if v, err := strconv.ParseUint(strings.TrimPrefix(t, "hwnd:"), 10, 64); err == nil && strings.HasPrefix(t, "hwnd:") {
		setForegroundWindow.Call(uintptr(v))
	}
}

func openFolder(p string) { _ = exec.Command("explorer", p).Start() }
