# Zyon

Jogo 2D de mineração/construção/sobrevivência inspirado em Terraria, ambientação pós-guerra (em definição).
Motor: Godot 4 (.NET) com C#. A lógica do jogo vive em bibliotecas .NET puras.

- Spec da fatia vertical: `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`
- Planos: `docs/superpowers/plans/`

## Estrutura

- `src/Zyon.Core` — simulação (mundo, entidades ECS/Arch, física, inventário, sessão). O que um servidor dedicado rodaria.
- `tests/Zyon.Core.Tests` — xUnit.
- `data/` — definições JSON (blocos, paredes, itens, física, jogador). Conteúdo novo entra aqui, nunca hardcoded.
- `src/Zyon.Core/WorldGen` — geração de mundo: `WorldGenerator` roda passes (`terrain`, `layers`, `caves`, `ores`, `walls`, `spawn`); parâmetros em `data/worldgen.json`; FastNoiseLite vendorizada em `WorldGen/ThirdParty` (ver `THIRD_PARTY_NOTICES.md`, não editar).
- `src/Zyon.Core/Persistence` — saves: `WorldSave`/`WorldFile` (`.zworld`, binário: magic + versão sem compressão, resto em Brotli com paleta de nomes e RLE) e `PlayerSave`/`PlayerFile` (`.zplayer`, JSON). `SaveFiles` grava atomicamente (`.tmp` → troca, anterior vira `.bak`) e lê com fallback para o `.bak`.

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
- Geração determinística: dentro de `Parallel.For` use só ruído e escreva só na própria coluna; nunca `ctx.Random` nem `ctx.Report`. `ctx.Random` é um `GenRandom` (SplitMix64 próprio): nunca use `System.Random` na geração (a sequência com seed não é garantida entre versões do .NET). Mudou a saída da geração para a mesma seed? Incremente `WorldGenerator.Version`.
- Nomes de passes semeiam Random e ruído: renomear um passe muda o mundo gerado.
- Na geração, nunca use `MathF`/`Math` transcendentais (Sin, Cos, Exp, Log, Pow, Atan…): divergem entre plataformas. Use `DeterministicMath` (Floor/Ceiling/Round/Abs/Sqrt/Min/Max/Clamp são seguros).
- `WorldGenDeterminismTests.GoldenHash_*` fixa o hash de um mundo de referência: se falhar, a saída da geração mudou — se foi intencional, incremente `WorldGenerator.Version` e atualize as constantes.
- Mudou o que vai num save? Incremente `WorldFile.FormatVersion`/`PlayerFile.FormatVersion` e escreva a migração da versão anterior; saves antigos são migrados, nunca descartados.
- Saves referenciam blocos/paredes/itens **pelo nome**; um nome que sumiu dos dados faz a carga falhar com mensagem clara (nunca carregar com ids errados).
- Carga com erro nunca sobrescreve arquivos: use `SaveFiles.ReadWithBackup` e mostre a `SaveLoadException` ao jogador.
- Salvar o mundo: `WorldFile.Write` roda na thread da simulação entre Steps (os tiles não são copiados); só os bytes resultantes podem ir para o disco em segundo plano.
- Ao salvar, passe as posições do save anterior para `WorldSave.Capture(..., previousPositions)` para não perder personagens ausentes.
- Nomes de personagem passam por `PlayerSave.ValidateName`; o Godot deve montar o caminho do arquivo a partir do nome validado.

## Comandos

- Build: `dotnet build`
- Testes: `dotnet test`
- Desempenho (só vale em Release): `dotnet test -c Release --filter Category=Performance` (já compila em Release; não usar `--no-build` sem um `dotnet build -c Release` prévio)

Toda mudança termina com `dotnet test` passando.
