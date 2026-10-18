# Zyon — Fatia Vertical: Design

- **Data:** 2026-10-15
- **Status:** aprovado em brainstorming, aguardando revisão do spec escrito
- **Escopo:** primeiro subprojeto do Zyon — a base jogável sobre a qual os demais sistemas serão construídos

## 1. Contexto

Zyon é um jogo 2D de mineração, construção e sobrevivência inspirado em Terraria. A ambientação pretendida é pós-guerra nuclear (estilo Fallout) com elementos cyberpunk, ainda não fechada — esta fatia não depende dela.

### Decisões de base

| Tema | Decisão |
|---|---|
| Objetivo | Projeto pessoal sério: jogo completo e jogável, possivelmente publicado gratuitamente (itch.io) |
| Desenvolvimento | Majoritariamente feito por IA, com pouco trabalho manual |
| Motor | Godot 4 (.NET) com C# |
| Plataformas | Windows, Linux e macOS |
| Mundo | Finito, tamanho fixo escolhido na criação, gerado inteiro de uma vez |
| Multiplayer | Não na fatia, mas a arquitetura já é preparada para cooperativo com servidor autoritativo |
| Escala de entidades | Requisito: suportar milhares de entidades no futuro |
| Arte | Pixel art, tiles de 16×16 px, arte provisória de pacotes gratuitos |

### Decomposição do jogo completo

Cada item é um subprojeto com seu próprio ciclo spec → plano → implementação:

1. **Núcleo + geração simples + inventário + save — esta fatia**
2. Iluminação
3. Crafting
4. Combate e inimigos
5. Progressão (chefes, tiers, eventos)
6. NPCs e base
7. Multiplayer cooperativo
8. Arte definitiva e ambientação

### Escopo da fatia

**Dentro:** mundo em tiles com chunks, movimento do jogador, minerar e colocar blocos, geração procedural com terreno, camadas, cavernas e veios de material, itens coletáveis, inventário com hotbar, save/load de mundo e personagem.

**Fora:** iluminação, crafting, combate, inimigos, NPCs, multiplayer em rede, áudio, partículas, parallax, colocação de paredes pelo jogador, divisão de pilhas, arte definitiva.

## 2. Arquitetura

Núcleo em C# puro com o Godot como camada de apresentação. Entidades em ECS desde o início e tiles em arrays densos (modelo híbrido).

### Projetos

| Projeto | Papel | Depende de |
|---|---|---|
| `Zyon.Core` | Lógica do jogo: mundo, entidades, simulação, física, geração, inventário, save. É o que um servidor dedicado rodará. | .NET, Arch (ECS) |
| `Zyon.Client` | Lógica de apresentação independente de motor: máscara de autotiling, montagem de vértices de malha, interpolação | `Zyon.Core` |
| `Zyon.Game` | Projeto Godot 4 (.NET). Camada fina: nós, input, HUD | `Zyon.Core`, `Zyon.Client` |
| `Zyon.Core.Tests` | Testes xUnit do Core | `Zyon.Core` |
| `Zyon.Client.Tests` | Testes xUnit do Client | `Zyon.Client` |
| `data/` | Definições JSON: blocos, itens, física | — |

**Regra inviolável:** `Zyon.Core` e `Zyon.Client` nunca referenciam Godot.

## 3. Modelo de dados

### Tiles

- O mundo é dividido em **chunks de 32×32 tiles**.
- Cada chunk guarda os dados em arrays paralelos (structure of arrays):
  - `ushort[] Block`: bloco de frente (0 = ar)
  - `ushort[] Wall`: parede de fundo (0 = nenhuma)
- Cada chunk tem uma flag `Dirty`, usada para redesenho, persistência e, no futuro, sincronização em rede.
- As paredes existem no modelo desde já para estabilizar o formato de save. Na fatia, só a geração cria paredes.

### Tamanhos de mundo

| Tamanho | Tiles | Chunks (aprox.) |
|---|---|---|
| Pequeno | 4200 × 1200 | 132 × 38 |
| Médio | 6400 × 1800 | 200 × 57 |
| Grande | 8400 × 2400 | 263 × 75 |

Quando a dimensão do mundo não é múltipla de 32, o último chunk de cada eixo é parcial. Tiles fora do mundo são tratados como sólidos.

### Definições data-driven

- `data/blocks.json`: `id` (nome textual), `hardness`, `solid`, `drops` (id do item)
- `data/walls.json`: `id`
- `data/items.json`: `id`, nome, `maxStack`, `kind` (`Block` com `placesBlock`, ou `Tool` com `power` e `speed`)
- `data/physics.json`: constantes de movimento e física
- Um `Registry` carrega os JSONs na inicialização e mapeia nomes textuais para IDs numéricos `ushort`. O ID 0 é reservado para ar ou "nenhum".

### Entidades (ECS)

- Biblioteca: **Arch**.
- Entidades na fatia: o **jogador** e os **itens caídos**.
- Componentes: `Position`, `Velocity`, `Collider` (AABB em unidades de tile), `PlayerControlled`, `PlayerInputState`, `MiningState`, `InventoryRef`, `DroppedItem`, `Lifetime`, `NetworkId`.
- O `NetworkId` é um identificador estável, separado do ID interno do Arch. É o que trafega por eventos, rede e save.

### Tempo e unidades

- A simulação roda num **tick fixo de 60 Hz**, desacoplado do framerate.
- O Core trabalha em **unidades de tile**: a posição 10.5 é o meio do tile 10. Só a apresentação converte para pixels (×16).
- O multiplayer futuro será com servidor autoritativo, então `float` é suficiente e não há exigência de determinismo bit a bit na simulação. A geração de mundo, porém, é determinística (ver seção 6).

## 4. Simulação, comandos e eventos

### Ordem do tick

1. Aplica o `PlayerInput` mais recente de cada jogador.
2. Valida e aplica os comandos discretos enfileirados, em ordem de chegada.
3. Roda os sistemas em ordem fixa: **Movimento → Física → Mineração/Colocação → Coleta de itens → Fusão de itens → Despawn**.
4. Emite os eventos do tick.

### Entradas

- **`PlayerInput`** (contínuo, um por tick): eixo horizontal (-1..1), pulo pressionado, ação primária pressionada, tile mirado (x, y).
- **Comandos discretos:** `SelectHotbarSlot(slot)`, `MoveInventoryItem(from, to)`, `DropItem(slot)`.

Comandos e eventos são `record` imutáveis e serializáveis.

### Ação primária contextual

Enquanto a ação primária estiver pressionada, o efeito depende do item no slot selecionado da hotbar:

- **`Tool`:** minera o tile mirado. O progresso acumula a cada tick em função da `hardness` do bloco e de `power`/`speed` da ferramenta. Ao completar, o bloco vira ar e nasce um `DroppedItem` com o item de `drops`. Trocar de tile mirado ou soltar o botão zera o progresso.
- **`Block`:** coloca o bloco no tile mirado e consome 1 unidade. Existe um intervalo mínimo entre colocações (configurável em `physics.json`).
- **Slot vazio ou outro tipo:** nada acontece.

O jogador começa com uma picareta básica no slot 1 da hotbar.

### Validação (sempre no Core)

- **Alcance:** o centro do tile mirado está a no máximo 5 tiles (configurável) do centro do jogador.
- **Minerar:** o tile está dentro do mundo e não é ar.
- **Colocar:** o tile é ar, tem ao menos um vizinho ortogonal com bloco, ou uma parede no próprio tile, e não intersecta o AABB de nenhum jogador. Itens caídos não bloqueiam a colocação; inimigos futuros poderão bloquear.
- **Comando inválido:** é descartado em silêncio (sem exceção), com contador e log em modo debug.

### Eventos

`TileChanged(x, y, block, wall)`, `EntitySpawned(networkId, kind, position)`, `EntityDespawned(networkId)`, `InventoryChanged(playerNetworkId, slots alterados)`, `HotbarSelectionChanged`, `MiningProgressChanged(x, y, progresso)`, `WorldGenProgress(passe, percentual)`.

As posições das entidades não geram eventos por tick. A apresentação as lê do estado a cada frame via `IWorldView`. Na futura camada de rede, a sincronização de posições será feita por snapshots, definidos no subprojeto de multiplayer.

### Sessão

```csharp
interface IGameSession {
    void Send(ICommand command);
    void SetInput(PlayerInput input);
    IWorldView World { get; }
    IReadOnlyList<IGameEvent> DrainEvents();
}
```

- `LocalSession`: roda a `Simulation` no mesmo processo. É a única implementação na fatia.
- `NetworkSession` (futuro): serializa comandos e inputs para um servidor que roda o mesmo `Zyon.Core`.

## 5. Física e movimento

- Física própria em grade, implementada como sistema do ECS no Core. Não usa a física do Godot.
- **AABB:** jogador com ~1,25 × 2,75 tiles; itens caídos com ~0,75 × 0,75 tiles.
- **Resolução por eixo:** primeiro X, depois Y. Em cada eixo, verifica os tiles sólidos que o AABB vai ocupar e trava na borda.
- **Anti-tunneling:** deslocamento acima de 0,5 tile num tick é dividido em subpassos.
- **Bordas do mundo:** sólidas.
- **Movimento do jogador:** aceleração e desaceleração horizontais, velocidade máxima, gravidade com velocidade terminal, pulo de altura variável (segurar sobe mais), coyote time de poucos ticks.
- **Auto-step:** andando contra um degrau de exatamente 1 tile com espaço livre acima, o jogador sobe automaticamente. Degraus de 2 ou mais tiles bloqueiam.
- **Itens caídos:**
  - Mesma gravidade e colisão do jogador.
  - **Ímã:** dentro de ~3 tiles, o item acelera em direção ao jogador e, ao tocar o AABB dele, é adicionado ao inventário. Se não couber tudo, o restante continua no chão.
  - **Fusão:** itens iguais a menos de ~1 tile se fundem numa pilha, respeitando `maxStack`.
  - **Despawn:** depois de 5 minutos (configurável).
- Todas as constantes ficam em `data/physics.json`.

## 6. Geração do mundo

### Pipeline

```csharp
interface IWorldGenPass {
    string Name { get; }
    void Apply(WorldGenContext ctx);
}
```

- O mundo é gerado por uma lista ordenada de passes.
- `WorldGenContext` expõe o mundo, a seed, um `Random` próprio do passe (derivado de `seed + Name` por hash estável, sem usar `string.GetHashCode`) e o reporte de progresso.
- Ruído: **FastNoiseLite** (arquivo único em C#, licença MIT), incluído no Core.
- Passes que trabalham por coluna ou região rodam em paralelo (`Parallel.For`), desde que o resultado continue determinístico.

### Passes da fatia

| # | Passe | Responsabilidade |
|---|---|---|
| 1 | Terreno | Ruído 1D define a altura da superfície, entre ~25% e 35% da altura do mundo |
| 2 | Camadas | Solo perto da superfície e rocha abaixo, com fronteira irregular por ruído, mais uma camada profunda de maior `hardness` |
| 3 | Cavernas | Bolsões por limiar de ruído 2D ("queijo") e túneis longos por caminhada aleatória ("minhocas") |
| 4 | Bolsões de material | Veios de 2–3 materiais distribuídos por faixa de profundidade |
| 5 | Paredes | Preenche paredes de fundo abaixo da superfície, incluindo dentro das cavernas |
| 6 | Ponto de spawn | Encontra a superfície perto do centro horizontal, num trecho plano com espaço livre para o jogador |

Nomes e visuais dos materiais são provisórios (terra, rocha, rocha profunda, minério A, minério B). A identidade da ambientação será aplicada depois, trocando dados em JSON.

### Requisitos

- Mesma seed + mesma versão do gerador produzem um mundo idêntico, byte a byte.
- O mundo pequeno é gerado em menos de 10 s numa máquina de desenvolvimento típica.
- O progresso é reportado por eventos `WorldGenProgress` para a tela de carregamento.
- A versão do gerador é gravada no save.

## 7. Renderização e apresentação

### Princípio

O Godot lê o mundo por `IWorldView` (somente leitura) e reage a eventos. Ele nunca altera o estado diretamente: toda ação passa pela sessão.

### Tiles

- Cada chunk visível é um `Node2D` com duas `MeshInstance2D`: paredes, desenhadas mais escuras, e blocos. Cada uma usa um `ArrayMesh` de quads sobre um atlas de texturas.
- Só existem os nós dos chunks dentro da janela da câmera mais uma margem de 1 chunk. Os nós são reaproveitados por pool.
- Um chunk afetado por `TileChanged` é marcado para reconstrução. Se o tile estiver na borda, o chunk vizinho também é marcado. Há um orçamento máximo de reconstruções por frame, e o restante fica para os frames seguintes.
- A montagem dos vértices (posições e UVs a partir dos dados do chunk) fica em `Zyon.Client`. O `Zyon.Game` apenas transfere os arrays para o `ArrayMesh`.

### Autotiling

- A variante do sprite é escolhida pela máscara dos 4 vizinhos ortogonais (16 combinações), mais uma variação estável por posição (hash de x, y) entre N variações.
- Vizinhos de materiais diferentes contam como "conectados" se ambos forem sólidos. Uma mistura mais refinada entre materiais fica para o subprojeto de arte.
- **Layout do atlas:** para cada material, uma grade de 16 variantes × N variações de 16×16 px.
- Um script gera atlas provisórios nesse layout a partir de texturas simples, para que a falta de arte compatível não bloqueie a fatia.

### Entidades

- Cada entidade visível tem uma representação ligada ao seu `NetworkId`.
- O jogador é um nó com sprite.
- Os itens caídos são desenhados em lote com `MultiMeshInstance2D`, numa só chamada de desenho. Esse é o padrão para qualquer entidade numerosa no futuro (projéteis, partículas).
- A posição renderizada é interpolada entre o tick anterior e o atual.

### Câmera

- Resolução base de 640×360, escalada por fator inteiro (pixel-perfect).
- A câmera segue o jogador com suavização e fica presa aos limites do mundo.

### Interface

- **Menu inicial:** novo mundo (nome, tamanho, seed opcional), carregar mundo, novo ou carregar personagem.
- **Tela de carregamento** com o progresso da geração.
- **Hotbar** com 10 slots: teclas 1–0 e roda do mouse.
- **Inventário completo**, aberto e fechado por tecla, com arrastar e soltar. Soltar fora do painel gera `DropItem`.
- **Destaque do tile mirado**, visível só dentro do alcance.
- **Overlay de rachaduras** proporcional ao progresso de mineração.

## 8. Inventário e save

### Inventário

- 50 slots: índices 0–9 são a hotbar e 10–49 a mochila. Cada slot é um `ItemStack { ItemId, Count }` ou vazio.
- **Adicionar:** completa as pilhas existentes do mesmo item (hotbar primeiro, depois mochila), depois ocupa slots vazios (hotbar primeiro). Retorna a quantidade que não coube.
- **`MoveInventoryItem(from, to)`:** se o destino tem o mesmo item, funde até `maxStack` e o excedente fica na origem. Caso contrário, troca os conteúdos.
- **`DropItem(slot)`:** remove a pilha inteira e cria um `DroppedItem` à frente do jogador, com um pequeno impulso.
- A lógica fica na classe `Inventory`, testável isoladamente. No ECS, o jogador tem uma referência a ela via `InventoryRef`.

### Arquivos

O mundo e o personagem são salvos em arquivos independentes, como no Terraria. O Core lê e escreve `Stream`s, e o caminho é decidido pelo Godot (`user://saves/worlds/` e `user://saves/players/`).

#### Mundo (`.zworld`, binário)

1. **Cabeçalho:** magic `ZYONWLD`, versão do formato, versão do gerador, seed, largura, altura, posição de spawn.
2. **Paleta:** lista de pares nome textual → ID numérico de cada bloco e parede presentes.
3. **Chunks:** para cada chunk, `Block` e `Wall` codificados em RLE.
4. **Entidades:** itens caídos (item por nome, quantidade, posição, tempo restante).

O arquivo inteiro após o cabeçalho é comprimido com Brotli (`System.IO.Compression`). Na carga, a paleta traduz os IDs gravados para os IDs do `Registry` atual. Se um nome não existir mais, a carga falha com uma mensagem clara.

#### Personagem (`.zplayer`, JSON)

Versão do formato, nome, slot selecionado e inventário com os itens referenciados por nome textual.

#### Versionamento

Cada formato tem uma versão. A carga aplica migrações sequenciais (v1→v2→…) até a versão atual. Saves antigos são atualizados, nunca descartados.

#### Segurança

- **Gravação atômica:** escreve em `arquivo.tmp`, move o arquivo atual para `arquivo.bak` e renomeia o `.tmp` para o nome final.
- **Autosave** a cada 5 minutos (configurável) e ao sair do mundo. A posição do jogador no mundo é salva junto com o mundo; o personagem (inventário) é salvo no `.zplayer`.

## 9. Tratamento de erros

| Situação | Comportamento |
|---|---|
| Comando inválido | Descartado em silêncio, com contador e log em debug |
| JSON de dados inválido (ID duplicado, referência inexistente, campo faltando) | Falha imediata na inicialização com mensagem indicando arquivo e campo |
| Save corrompido ou ilegível | Tenta o `.bak`. Se também falhar, mostra o erro ao jogador e nunca sobrescreve o original |
| Exceção dentro do tick | Em desenvolvimento, o jogo quebra de forma explícita, sem engolir a exceção |
| Logs | O Core e o Client usam uma interface própria de log. No Godot, um adaptador escreve no console |

## 10. Testes

Todos com xUnit, executados por `dotnet test`, sem precisar do Godot (exceto o smoke test).

| Tipo | Cobertura mínima |
|---|---|
| Unitários (Core) | Inventário (fusão, pilha cheia, sobra, troca), todas as regras de validação, `Registry` (duplicatas, referências inválidas) |
| Unitários (Client) | Máscara de autotiling → variante; montagem de vértices de um chunk; interpolação |
| Física por cenário | Queda e pouso; auto-step de 1 tile; bloqueio em 2 tiles; sem atravessar paredes na velocidade terminal; colisão com as bordas do mundo |
| Simulação roteirizada | Input scriptado por N ticks → asserção do estado. Ex.: minerar 3 blocos resulta em 3 itens no inventário |
| Geração | Mesma seed → mesmo hash; seeds diferentes → mundos diferentes; spawn válido; bordas íntegras |
| Save | Ida e volta mundo/personagem; paleta com IDs remapeados; fallback para `.bak`; migração com fixtures de versões antigas (a partir da v2) |
| Desempenho | Mundo pequeno em menos de 10 s; tick com 5.000 itens caídos abaixo de 2 ms em média |
| Smoke test (Godot) | `godot --headless` carrega um mundo, roda alguns segundos de simulação e encerra sem erros |

## 11. Fluxo de desenvolvimento com IA

O projeto terá um `CLAUDE.md` com:

- Versão do Godot fixada (4.x .NET; a versão exata é definida no início da implementação) e uso exclusivo da API do Godot 4.
- `Zyon.Core` e `Zyon.Client` nunca referenciam Godot.
- Comandos e eventos são `record` imutáveis e serializáveis.
- Toda mudança termina com `dotnet test` passando, e com o smoke test quando tocar no `Zyon.Game`.
- Conteúdo novo entra por JSON em `data/`, nunca hardcoded.

## 12. Critérios de sucesso

1. Criar um mundo novo (pequeno, médio ou grande) a partir de uma seed, com tela de progresso.
2. Andar, pular e subir degraus com movimento fluido a 60 FPS.
3. Minerar com a picareta (rachaduras visíveis), coletar os itens pelo ímã e vê-los no inventário.
4. Colocar blocos respeitando alcance e adjacência.
5. Organizar o inventário com arrastar e soltar, e jogar itens fora.
6. Sair, voltar e encontrar o mundo e o personagem exatamente como estavam.
7. Rodar no Windows e no Linux; no macOS se houver máquina disponível para teste.
8. Todos os testes passando, incluindo os de desempenho.
