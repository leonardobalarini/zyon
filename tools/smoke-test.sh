#!/usr/bin/env bash
# Compila o jogo e roda N frames em modo headless. Sai com 0 só se o jogo imprimir "SMOKE OK".
set -uo pipefail
GODOT="${GODOT:-/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64_console.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/src/Zyon.Game"
FRAMES="${1:-600}"

dotnet build "$GAME/Zyon.Game.csproj" -nologo -v q || exit 1
# dotnet build já cria .godot/mono: a importação é detectada por .godot/imported.
if [ ! -d "$GAME/.godot/imported" ]; then
  "$GODOT" --headless --path "$GAME" --import > /dev/null 2>&1
fi
# timeout: uma exceção que impeça o jogo de chamar Quit não pode travar o script.
out="$(timeout "${SMOKE_TIMEOUT:-300}" "$GODOT" --headless --path "$GAME" -- --smoke-test --frames "$FRAMES" --size dev --seed 1 2>&1)"
code=$?
echo "$out" | tail -n 25
if [ "$code" -ne 0 ] || ! echo "$out" | grep -q "SMOKE OK"; then
  echo "smoke test FALHOU (código $code)"
  exit 1
fi
echo "smoke test OK"
