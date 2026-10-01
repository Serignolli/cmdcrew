# Compila src\*.cs em bin\CmdCrew.exe usando o compilador C# que já vem no Windows.
# Os personagens gratuitos (personagens\*.json), a fonte, os sons e o ícone vão embutidos no .exe, para ele também
# funcionar sozinho como instalador. Uma cópia com o nome de distribuição fica em dist\CmdCrew-Setup.exe.
# Os personagens do Supporters Pack (supporters-pack\) nunca entram no .exe: são um pacote à parte.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'Compilador C# do .NET Framework 4 nao encontrado.' }

$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
Get-Process CmdCrew -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

$sources = (Get-ChildItem (Join-Path $PSScriptRoot '*.cs')).FullName
$res = Join-Path $PSScriptRoot 'recursos'
$resources = @("/resource:$(Join-Path $res 'PixelifySans.ttf'),recursos/PixelifySans.ttf")
foreach ($f in Get-ChildItem (Join-Path $res 'sons\*.wav')) {
    $resources += "/resource:$($f.FullName),sons/$($f.Name)"
}
foreach ($f in Get-ChildItem (Join-Path $root 'personagens\*.json')) {
    $resources += "/resource:$($f.FullName),personagens/$($f.Name)"
}
$out = Join-Path $bin 'CmdCrew.exe'
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$out" "/win32icon:$(Join-Path $res 'icone.ico')" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $resources $sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar.' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item $out (Join-Path $dist 'CmdCrew-Setup.exe') -Force
Write-Host 'Compilado: bin\CmdCrew.exe (e dist\CmdCrew-Setup.exe para distribuir)'
