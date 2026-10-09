//go:build darwin

package main

import (
	"github.com/ebitengine/purego"
	"github.com/ebitengine/purego/objc"
)

// No macOS, as janelas do CmdCrew não ganham ícone no Dock nem entram no Cmd+Tab
// (NSApplicationActivationPolicyAccessory). O AppKit só aceita isso na thread principal.
func macAccessory() {
	defer func() { recover() }()
	lib, err := purego.Dlopen("/usr/lib/libSystem.B.dylib", purego.RTLD_GLOBAL)
	if err != nil {
		return
	}
	mainQueue, err := purego.Dlsym(lib, "_dispatch_main_q")
	if err != nil {
		return
	}
	var dispatchAsyncF func(queue, ctx, fn uintptr)
	purego.RegisterLibFunc(&dispatchAsyncF, lib, "dispatch_async_f")
	cb := purego.NewCallback(func(ctx uintptr) uintptr {
		app := objc.ID(objc.GetClass("NSApplication")).Send(objc.RegisterName("sharedApplication"))
		app.Send(objc.RegisterName("setActivationPolicy:"), 1)
		return 0
	})
	dispatchAsyncF(mainQueue, 0, cb)
}
