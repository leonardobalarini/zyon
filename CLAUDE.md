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
- `src/Zyon.Core/Persistence` — saves: `WorldSave`/`WorldFile` (`.zworld`, binário: magic + versão + tamanho e hash FNV-1a (de versão + tamanho + payload) sem compressão, payload em Brotli com paleta de nomes e RLE; leitura só lança `DataException` (corrompido) ou `SaveIncompatibleException` (íntegro mas inutilizável por esta versão)) e `PlayerSave`/`PlayerFile` (`.zplayer`, JSON). `SaveFiles` grava atomicamente (`.tmp` → troca, anterior vira `.bak`) e lê com fallback para o `.bak` só em corrupção (o principal ruim vai para `.corrupt`; com `FromBackup` avise o jogador). Save incompatível ou arquivo em uso nunca caem para o `.bak`. Principal ausente com `.tmp` legível (crash no meio da troca): o `.tmp` é promovido a principal (`RecoveredFromTemp`); ao gravar sem principal, um `.tmp` existente vira `.tmp.orphan` em vez de ser apagado.
- `src/Zyon.Client` — lógica de apresentação sem Godot (autotiling, malhas de chunk, visibilidade, chunks sujos, cores e atlas provisórios, opções de lançamento, fluxo de jogo (`SaveLibrary`, `GameLoader`, `GameSaver`, `AutosaveTimer`, `DevLaunch`)). Testes em `tests/Zyon.Client.Tests`.
- `src/Zyon.Game` — projeto Godot 4.7 (.NET) fino: converte dados do Client em `ArrayMesh`/`MultiMesh`/nós, lê input e só conversa com `IGameSession`. Cenas `MainMenu` → `Loading` → `Game`, ligadas por `GameFlow` (estático: dados carregados uma vez, pedido do menu e jogo carregado). Cores provisórias em `data/client/visuals.json`. O csproj usa `CopyLocalLockFileAssemblies` (o Godot carrega as DLLs da pasta de saída, inclusive pacotes NuGet como o Arch).

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
- Carga com erro nunca sobrescreve arquivos: use `SaveFiles.ReadWithBackup` e capture `SaveException`. `SaveIncompatibleException` → mostre "save de versão mais nova ou conteúdo removido"; `SaveLoadException.Kind` diz o motivo (`Corrupt`, `Missing`, `Inaccessible` — este último vale tentar de novo). Com `FromBackup` avise o jogador que voltou ao backup (o principal ruim ficou em `QuarantinedPath`). Outras exceções são bugs: não as engula.
- Salvar o mundo: `WorldFile.Write` roda na thread da simulação entre Steps (os tiles não são copiados); só os bytes resultantes podem ir para o disco em segundo plano (gravações no mesmo arquivo em fila única). `Write` valida antes de gravar e não limpa `Dirty`. Dirty não tem consumidor hoje; `GameSaver` não chama `MarkSaved` (seria corrida entre threads). Se um dia `Dirty` for usado, faça snapshot das versões na captura e limpe na thread do jogo após a confirmação.
- Mundo e personagem são gravados em sequência, não como par atômico: mostre `GameSaver.LastError` e tente de novo ao sair.
- Modos descartáveis (`--dev`/`--smoke-test`/`--profile-walk`) usam a subpasta `dev/` dos saves; nunca apagam saves reais.
- Ao salvar, passe as posições do save anterior para `WorldSave.Capture(..., previousPositions)` para não perder personagens ausentes.
- Nomes de personagem passam por `PlayerSave.ValidateName` (também chamado por `Capture` e `PlayerFile.Write`); ele devolve o nome normalizado (NFC) — o Godot deve montar o caminho do arquivo a partir desse valor.
- Nós Godot criados por código: construtor sem parâmetros + `Init(...)`. Lógica testável vai para `Zyon.Client`, nunca para `Zyon.Game`.

## Comandos

- Build: `dotnet build`
- Testes: `dotnet test`
- Desempenho (só vale em Release): `dotnet test -c Release --filter Category=Performance` (já compila em Release; não usar `--no-build` sem um `dotnet build -c Release` prévio)
- Godot (portátil): `/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64_console.exe` (variável `GODOT` sobrescreve).
- Jogar: `tools/play.sh` ou `.\tools\play.ps1` (abre o menu). `--dev` entra direto num mundo de teste **descartável** (ex.: `--dev --size small --seed 42`; `--size dev|small|medium|large` e `--seed N` só valem para mundos criados pelo atalho: `--dev`, `--smoke-test`, `--profile-walk` ou `--world` de um mundo que ainda não existe); `--world NOME --player NOME` entra direto num mundo salvo (cria se não existir); `--saves-dir PASTA` muda onde ficam os saves (padrão `user://saves`); `--autosave-seconds N` (padrão 300); `--camera-smoothing V` — 0 = câmera direta, o padrão; 10 era o antigo.
- Controles: A/D andar, Espaço pular, botão esquerdo usar item, 1–0/roda hotbar, Q soltar item, E inventário (arrastar entre slots; soltar fora do painel joga o item no chão), Esc pausa (fecha o inventário primeiro). Sair pelo menu de pausa ou fechar a janela salva antes; se a gravação falhar, um aviso oferece tentar de novo ou sair sem salvar.
- No jogo: F3 mostra/esconde a sobreposição de depuração (FPS, tempos de quadro, custo da sessão/render, chunks, GC, alpha; no último segundo, quadros com 0 e com 2+ ticks, delta máximo do motor e intervalo máximo medido pelo relógio; e o último "soluço" — quadro com 2+ ticks — com delta, intervalo medido, `_Process`, remontagens/chunks novos e GC, também impresso no console enquanto F3 estiver ligado). Leitura: soluço com intervalo medido ~33 ms e `_Process` baixo = VSync perdido fora do nosso código; `_Process` alto com chunks novos ou GC = custo nosso; delta ≠ medido = o motor mexeu no delta. F4 liga/desliga a suavização da câmera; F5 liga/desliga o VSync. O projeto usa `swapchain_image_count=2` (buffer duplo, menos latência com VSync).
- Relógio da simulação: `LocalSession` conta ticks com `FrameClock` (Core), que arredonda deltas a até 0,5 ms de um múltiplo do tick e mantém a fase no meio do tick — num monitor de ~60 Hz roda sempre 1 tick por quadro. Por isso o `project.godot` desliga a suavização de delta do Godot (`application/run/delta_smoothing=false`) e o `physics_jitter_fix` (0): os dois redistribuem o tempo em "lotes" (deltas de 25 ms ou 18,06 ms em quadros de 16,7 ms) que viravam quadros de 2 ticks e saltos na imagem. Não religue.
- Diagnóstico de soluços (headless): `"$GODOT" --headless --max-fps 60 --path src/Zyon.Game -- --profile-walk --frames 7200 --size dev --seed 1` anda pelo mundo cruzando chunks e imprime `PROFILE` (ticks por quadro, delta vs intervalo medido, `_Process` com/sem chunk novo, picos por causa) e grava um CSV por quadro em `user://profile-walk.csv`. Em headless o `--max-fps` dorme pelo SO: atrasos de 25–100 ms com `_Process` baixo são do SO, não do jogo.
- Smoke test do jogo (headless): `tools/smoke-test.sh [frames]` (padrão 600; o roteiro de mineração precisa de uns 300) — rode sempre que mexer em `src/Zyon.Game`. Passa pelo menu pulado → carregamento → jogo com saves numa pasta temporária, salva no fim e confere o mundo relido (`SMOKE OK ... save ok`). Imprime `SMOKE STATS` (tempos de `_Process`, atraso da câmera) para comparar desempenho em headless.

Toda mudança termina com `dotnet test` passando.
