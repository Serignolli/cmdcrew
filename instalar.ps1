# Compila o executável, instala os ganchos no Claude Code e abre a configuração.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin\CmdCrew.exe'

Write-Host ''
Write-Host '  CmdCrew para o Claude Code' -ForegroundColor Yellow
Write-Host '  -----------------------'
Write-Host '  Compilando...'
& (Join-Path $PSScriptRoot 'src\build.ps1')

Write-Host '  Instalando os ganchos em ~/.claude/settings.json (um backup e feito antes)...'
$p = Start-Process $exe -ArgumentList 'install' -Wait -PassThru
if ($p.ExitCode -ne 0) {
    $err = Join-Path $env:LOCALAPPDATA 'CmdCrew\ultimo-erro.txt'
    $msg = if (Test-Path $err) { [IO.File]::ReadAllText($err) } else { 'erro desconhecido' }
    throw "Falha ao instalar: $msg"
}

Write-Host ''
Write-Host '  Pronto! Abrindo a tela de configuracao...' -ForegroundColor Green
Write-Host '  Reinicie as sessoes abertas do Claude Code para os ganchos valerem.'
Write-Host '  Para mudar depois: clique duas vezes em configurar.cmd'
Write-Host ''
Start-Process $exe -ArgumentList 'config'
