#!/usr/bin/env bash
# Compila e abre o jogo no menu inicial. Argumentos extras vão para o jogo: tools/play.sh --dev --size small --seed 42
# entra direto num mundo de teste descartável (--size/--seed só valem para mundos criados assim); --world NOME --player NOME entra
# direto num mundo salvo.
set -euo pipefail
GODOT="${GODOT:-/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/src/Zyon.Game"
dotnet build "$GAME/Zyon.Game.csproj" -nologo -v q
if [ ! -d "$GAME/.godot/imported" ]; then
  "${GODOT%.exe}_console.exe" --headless --path "$GAME" --import > /dev/null 2>&1 || true
fi
if [ "$#" -eq 0 ]; then
  "$GODOT" --path "$GAME"
else
  "$GODOT" --path "$GAME" -- "$@"
fi
