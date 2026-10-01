# Remove os ganchos e as frases do ~/.claude/settings.json (os arquivos desta pasta ficam).
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin\CmdCrew.exe'
if (-not (Test-Path $exe)) { throw 'bin\CmdCrew.exe nao encontrado. Nada a desinstalar.' }

$p = Start-Process $exe -ArgumentList 'uninstall' -Wait -PassThru
if ($p.ExitCode -ne 0) {
    $err = Join-Path $env:LOCALAPPDATA 'CmdCrew\ultimo-erro.txt'
    $msg = if (Test-Path $err) { [IO.File]::ReadAllText($err) } else { 'erro desconhecido' }
    throw "Falha ao desinstalar: $msg"
}
Write-Host 'Removido do Claude Code. Pode apagar esta pasta se quiser.' -ForegroundColor Green
