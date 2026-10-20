# Compila e abre o jogo. Argumentos extras vão para o jogo (ex.: .\tools\play.ps1 --size small --seed 42).
$ErrorActionPreference = 'Stop'
$godot = if ($env:GODOT) { $env:GODOT } else { 'D:\Projetos\Ferramentas\Godot\Godot_v4.7.2-stable_mono_win64.exe' }
$console = $godot -replace '\.exe$', '_console.exe'
$game = Join-Path $PSScriptRoot '..\src\Zyon.Game' | Resolve-Path
dotnet build (Join-Path $game 'Zyon.Game.csproj') -nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path (Join-Path $game '.godot\imported'))) {
    & $console --headless --path $game --import *> $null
}
$gameArgs = if ($args.Count -gt 0) { $args } else { @('--size', 'dev') }
& $godot --path $game -- @gameArgs
