#!/usr/bin/env bash
# Gera data/client/art/ a partir de art/art.json e data/. Rode depois de mudar a arte ou adicionar conteúdo.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project src/Zyon.ArtGen -c Release -- "$(pwd)"
