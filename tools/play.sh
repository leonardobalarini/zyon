#!/usr/bin/env bash
# Compila e abre o jogo. Argumentos extras vão para o jogo (ex.: tools/play.sh --size small --seed 42).
set -euo pipefail
GODOT="${GODOT:-/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/src/Zyon.Game"
dotnet build "$GAME/Zyon.Game.csproj" -nologo -v q
if [ ! -d "$GAME/.godot/imported" ]; then
  "${GODOT%.exe}_console.exe" --headless --path "$GAME" --import > /dev/null 2>&1 || true
fi
if [ "$#" -eq 0 ]; then set -- --size dev; fi
"$GODOT" --path "$GAME" -- "$@"
