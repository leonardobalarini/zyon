# Compila e abre o jogo no menu inicial. Argumentos extras vão para o jogo: .\tools\play.ps1 --dev --size small --seed 42
# entra direto num mundo de teste descartável (--size/--seed só valem para mundos criados assim); --world NOME --player NOME entra
# direto num mundo salvo.
$ErrorActionPreference = 'Stop'
$godot = if ($env:GODOT) { $env:GODOT } else { 'D:\Projetos\Ferramentas\Godot\Godot_v4.7.2-stable_mono_win64.exe' }
$console = $godot -replace '\.exe$', '_console.exe'
$game = Join-Path $PSScriptRoot '..\src\Zyon.Game' | Resolve-Path
dotnet build (Join-Path $game 'Zyon.Game.csproj') -nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path (Join-Path $game '.godot\imported'))) {
    & $console --headless --path $game --import *> $null
}
if ($args.Count -gt 0) { & $godot --path $game -- @args } else { & $godot --path $game }
