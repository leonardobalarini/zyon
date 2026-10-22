# Zyon — Iluminação: Design

- **Data:** 2026-10-21
- **Status:** aprovado em brainstorming, aguardando revisão do spec escrito
- **Escopo:** subprojeto 2 da decomposição (depois da fatia vertical): luz por tile, fontes de luz, ciclo de dia e noite

## 1. Decisões

| Tema | Decisão |
|---|---|
| Estilo | Luz **suave** (degradê contínuo entre tiles), **um canal**, sem cor |
| Ciclo de dia e noite | Sim, **simples**: o céu escurece e clareia; sem gradiente de céu, sol, lua ou estrelas |
| Fontes de luz | **Tochas** (bloco não sólido colocável), **blocos que brilham** (minério B), **halo** fraco do jogador |
| Abordagem | Mapa de luz por tile calculado **no cliente** para a região visível + margem, com motor em C# puro no Core; desenhado como textura 1 px/tile esticada com filtro bilinear e blend multiply |

Fora do escopo: luz colorida, luz de itens segurados ou caídos, luz que afeta regras de jogo (ex.: spawn de inimigos), céu decorado, crafting de tochas.

## 2. Regras da luz

### Nível de luz

Cada tile tem um nível em [0, 1] (0 = escuro total, 1 = luz plena). O nível multiplica a cor de tudo que é desenhado no tile: blocos, paredes, jogador e itens. HUD, menus e sobreposição de depuração não são afetados.

### Fontes

- **Céu:** um tile recebe a luz do céu (= fator do céu) se ele e todos os tiles acima dele na coluna forem **ar** (bloco 0) **sem parede de fundo**. Como a geração põe paredes em todo o subsolo, o céu só entra no subsolo por buracos abertos pelo jogador.
- **Blocos emissores:** campo opcional `"light"` em `blocks.json`, valor em [0, 1]. A **tocha** emite 1,0; o **minério B** emite 0,35.
- **Halo do jogador:** emissor de 0,3 no tile do centro do jogador.

### Propagação

A luz se espalha para os 4 vizinhos ortogonais perdendo, a cada passo, a atenuação do tile de **destino**:

- tile de ar ou bloco não sólido: −0,06 (uma tocha alcança ~16 tiles);
- bloco sólido: −0,25 (a luz entra 3–4 tiles na parede).

O nível final de cada tile é o máximo entre sua própria emissão/céu e `vizinho − atenuação` de cada vizinho.

### Ciclo de dia e noite

- **Hora do dia** em [0, 1) derivada do `Tick` da simulação: `hora = (Tick / ticksPorDia + horaInicial) mod 1`. Como o `Tick` já vai no save do mundo, o formato de save **não muda**.
- Duração do dia: 24 minutos reais (86 400 ticks a 60 Hz). Um mundo novo começa de manhã, já com o dia claro (`horaInicial` = 0,30).
- **Fator do céu:** 1,0 durante o dia, 0,15 durante a noite (luar). Amanhecer (por volta de 0,25) e anoitecer (por volta de 0,75) fazem transição suave (smoothstep) com duração de 1/24 do dia (~1 minuto real).

## 3. Dados

- **`data/lighting.json`** (novo, validado na carga; erros com arquivo e campo):
  - `airAttenuation` 0,06
  - `solidAttenuation` 0,25
  - `dayLengthSeconds` 1440
  - `startTimeOfDay` 0,30
  - `nightSkyLevel` 0,15
  - `twilightFraction` 1/24
  - `playerHaloLevel` 0,3
  - `regionMargin` 18 (maior que o alcance de uma tocha, 1/0,06 ≈ 16,7 tiles)
  - Todos os níveis em [0, 1], atenuações em (0, 1], duração > 0.
- **`data/blocks.json`:**
  - novo bloco `torch`: `solid: false`, `hardness: 0`, `light: 1.0`, solta o item `torch`;
  - `ore_b` ganha `light: 0.35`.
- **`data/items.json`:** item `torch` (`Block`, `placesBlock: torch`, `maxStack: 999`).
- **`data/player.json`:** itens iniciais ganham 30 tochas.
- **`data/client/visuals.json`:** cor da tocha.
- **Atlas provisório:** materiais não sólidos são desenhados como um "palito" pequeno centralizado no tile (não um quadrado cheio).

A tocha, por ser não sólida, pode ser atravessada (a física já ignora blocos não sólidos), quebra na hora e segue a regra de colocação dos blocos (vizinho ortogonal com bloco ou parede no tile).

## 4. Arquitetura

### Core — `Zyon.Core.Lighting` (C# puro)

- `LightingConfig` + loader do `lighting.json`.
- `DayCycle`: `TimeOfDay(long tick)` e `SkyFactor(long tick)`.
- `LightMap`: retângulo de níveis (`OriginX`, `OriginY`, `Width`, `Height`, `float[]`), reutilizável.
- `LightEngine.Compute(IWorldView view, região, skyFactor, emissores extras, LightMap destino)`:
  1. semeia a região com céu e blocos emissores (+ emissores extras, ex.: o halo);
  2. propaga com varreduras em grade (esquerda→direita, direita→esquerda, cima→baixo, baixo→cima), repetidas até nenhum valor mudar (tipicamente 2–3 rodadas).
- A região calculada = área visível + `regionMargin` em cada lado, para que emissores fora da tela iluminem a borda corretamente.

### Client — `Zyon.Client`

- Conversão de `LightMap` em bytes L8 (0–255).
- `LightRecomputePolicy`: decide se recalcula no frame (ver §5).

### Game — `Zyon.Game` (camada fina)

- `LightOverlay`: `Sprite2D` com `ImageTexture` L8 de 1 pixel por tile, posicionado e escalado ×16 sobre a região, filtro bilinear (produz o degradê suave) e `CanvasItemMaterial` com blend **multiply**. Fica acima do mundo e das entidades e abaixo de HUD, menus e F3.
- F3 ganha a linha "luz: recálculos/s, tempo (ms)".

## 5. Quando recalcular

No máximo uma vez por frame, e só se:

- houve `TileChanged` dentro da região;
- o jogador mudou de tile (o halo se move);
- a área visível saiu da região já calculada;
- o fator do céu variou mais de 0,01 desde o último cálculo.

## 6. Desempenho

- Região típica: ~72×55 tiles (~4 mil) × 2–3 rodadas de 4 varreduras.
- Meta: **< 1 ms** por cálculo em Release (teste de desempenho no Core).
- Atualizar a textura (4 KB) é desprezível.

## 7. Testes

**Core:**
- ar sob o céu aberto = luz plena; um poço até a caverna ilumina o fundo;
- caverna fechada = escuro total, exceto pelo halo; parede de fundo bloqueia o céu;
- tocha: queda de exatamente 0,06 por tile no ar, alcance ~16; bloco sólido no caminho corta quase tudo;
- minério B brilha fraco;
- equivalência: a margem dá o mesmo resultado que calcular o mundo inteiro (mundo pequeno);
- `DayCycle`: tick → hora; começa de manhã; fator vai de 1,0 a 0,15 e volta, transição contínua e monotônica;
- dados: `lighting.json` validado; tocha não sólida, dureza 0, emissão 1,0; personagem novo com 30 tochas;
- desempenho: região 72×55 com tochas < 1 ms em Release.

**Client:** conversão para bytes; política de recálculo para cada gatilho.

**Smoke test:** coloca uma tocha no subsolo e confere que o tile ficou iluminado e que houve recálculo.

## 8. Riscos e limites

- Personagens criados antes deste subprojeto **não** ganham tochas (itens iniciais só valem para personagens novos). Aceito até existir crafting.
- Tocha provisória é um palito colorido; a arte definitiva vem no subprojeto de arte.
- A luz só existe perto da tela; nenhuma regra de jogo depende dela. Se um dia depender (ex.: spawn no escuro), o motor no Core permite evoluir para um mapa autoritativo.
- Canal único; luz colorida pode virar 3 canais sem mudar a estrutura do motor.

## 9. Critérios de sucesso

1. De dia, a superfície fica clara; cavernas fechadas ficam escuras, só com o halo do jogador.
2. Uma tocha ilumina a área com degradê suave; ao removê-la, a sombra volta.
3. O dia escurece e clareia ao longo de 24 minutos, sem saltos.
4. O minério B é visível no escuro como um brilho fraco.
5. O FPS não cai; o F3 mostra o custo da luz abaixo de 1 ms.
