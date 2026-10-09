// CmdCrew para o OpenCode: avisa a equipe quando o OpenCode trabalha, termina, falha ou pede permissão.
// Instalado pelo CmdCrew. Para tirar, desligue o OpenCode na configuração do CmdCrew (ou apague este arquivo).
import { spawn } from "node:child_process"

const EXE = __CMDCREW_EXE__

// Roda "cmdcrew <comando> opencode" com o mesmo JSON que os outros agentes mandam para os ganchos.
function run(command, payload, wantOutput) {
  return new Promise((resolve) => {
    try {
      const p = spawn(EXE, [command, "opencode"], {
        stdio: ["pipe", wantOutput ? "pipe" : "ignore", "ignore"],
        windowsHide: true,
      })
      let out = ""
      if (wantOutput) p.stdout.on("data", (d) => (out += d))
      p.on("error", () => resolve(""))
      p.on("close", () => resolve(out))
      p.stdin.on("error", () => {})
      p.stdin.end(JSON.stringify(payload))
    } catch {
      resolve("")
    }
  })
}

export const CmdCrew = async ({ client, directory }) => {
  const busy = new Set()
  const asked = new Set()   // versões diferentes do OpenCode avisam o mesmo pedido por eventos diferentes
  const base = (sessionID, extra) => ({ session_id: sessionID || "", cwd: directory, ...(extra || {}) })

  const start = (sid) => {
    if (busy.has(sid)) return
    busy.add(sid)
    run("start", base(sid))
  }
  const stop = (sid, command) => {
    if (!busy.delete(sid) && command === "stop") return
    run(command, base(sid))
  }

  // Pedido de permissão: aparece na sala com Permitir / Negar / Terminal. Se clicarem num botão, responde ao OpenCode.
  async function ask(p) {
    const id = p.id || p.requestID || ""
    if (!id || asked.has(id)) return
    asked.add(id)
    const what = (p.patterns && p.patterns.join(" ")) || p.title || (p.metadata && p.metadata.command) || ""
    const out = await run("permission", base(p.sessionID, {
      tool_use_id: id,
      tool_name: p.permission || p.type || "",
      tool_input: { description: what },
    }), true)
    let behavior = ""
    try { behavior = JSON.parse(out).hookSpecificOutput.decision.behavior } catch {}
    if (behavior !== "allow" && behavior !== "deny") return
    const reply = behavior === "allow" ? "once" : "reject"
    try {
      if (client.permission && client.permission.reply) return await client.permission.reply({ requestID: id, reply })
    } catch {}
    try {
      await client.postSessionIdPermissionsPermissionId({ path: { id: p.sessionID, permissionID: id }, body: { response: reply } })
    } catch {}
  }

  return {
    event: async ({ event }) => {
      const p = event.properties || {}
      const sid = p.sessionID || (p.info && p.info.id) || ""
      switch (event.type) {
        case "session.status":
          if (p.status && p.status.type === "busy") start(sid)
          else if (p.status && p.status.type === "idle") stop(sid, "stop")
          break
        case "session.idle":
          stop(sid, "stop")
          break
        case "session.error":
          busy.delete(sid)
          run("fail", base(sid))
          break
        case "session.deleted":
          busy.delete(sid)
          run("end", base(sid))
          break
        case "permission.asked":
        case "permission.updated":
          ask(p)
          break
        case "permission.replied":
          run("tool", base(sid))
          break
      }
    },
    "tool.execute.after": async (input) => {
      run("tool", base(input && input.sessionID))
    },
  }
}
