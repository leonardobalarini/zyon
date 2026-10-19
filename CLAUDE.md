# Zyon

Jogo 2D de mineração/construção/sobrevivência inspirado em Terraria, ambientação pós-guerra (em definição).
Motor: Godot 4 (.NET) com C#. A lógica do jogo vive em bibliotecas .NET puras.

- Spec da fatia vertical: `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`
- Planos: `docs/superpowers/plans/`

## Estrutura

- `src/Zyon.Core` — simulação (mundo, entidades ECS/Arch, física, inventário, sessão). O que um servidor dedicado rodaria.
- `tests/Zyon.Core.Tests` — xUnit.
- `data/` — definições JSON (blocos, paredes, itens, física, jogador). Conteúdo novo entra aqui, nunca hardcoded.

## Regras invioláveis

- `Zyon.Core` (e o futuro `Zyon.Client`) **nunca** referenciam Godot.
- Comandos (`ICommand`) e eventos (`IGameEvent`) são `record` imutáveis e serializáveis.
- Toda validação de regra de jogo acontece no Core; comando inválido é descartado em silêncio (contador + log debug).
- Coordenadas em unidades de tile, Y cresce para baixo, `Position` é o centro do AABB.
- Namespaces: `Zyon.Core.Tiles` (não `World` — colide com `Arch.Core.World`), `Zyon.Core.Sim` (não `Simulation`).
- Nunca criar/destruir entidades dentro de um `Query` do Arch: colete e aplique depois.
- Godot: usar somente API do Godot 4 (nunca Godot 3).
- Requer .NET SDK 10 (os projetos têm alvo net10.0).
- Ids numéricos de blocos/paredes/itens são posicionais (ordem do JSON); nunca persistir ids crus sem a paleta de nomes.
- Ordem dos sistemas: comandos → Movement → Physics → ToolAction → ItemPickup → ItemMerge → ItemDespawn. A física dos itens vive no `ItemPickupSystem`.
- Escritas diretas no `TileWorld` não emitem `TileChanged` (só use antes da sessão começar).
- Interfaces somente leitura (`IWorldView`, `IReadOnlyInventory`) são read-only por convenção — não fazer downcast no código de apresentação.

## Comandos

- Build: `dotnet build`
- Testes: `dotnet test`
- Desempenho (só vale em Release): `dotnet test -c Release --filter Category=Performance` (já compila em Release; não usar `--no-build` sem um `dotnet build -c Release` prévio)

Toda mudança termina com `dotnet test` passando.
