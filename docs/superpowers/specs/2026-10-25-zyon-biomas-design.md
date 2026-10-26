# Zyon — Biomas da superfície: Design

- **Data:** 2026-10-25
- **Status:** aprovado em brainstorming, aguardando revisão do spec escrito
- **Escopo:** três biomas de superfície (floresta morta, neve, deserto) com blocos, paredes, árvores derrubáveis, decoração, cor do céu e fundo em parallax por bioma, e detecção do bioma em volta do jogador

## 1. Contexto

A superfície é pós-apocalíptica com biomas no estilo Terraria (ver spec da arte, seção 1). Hoje ela é igual no mundo todo: terra com "cinza" no topo, rocha embaixo, sem árvores. Ordem dos subprojetos: arte → bunker → **biomas (este)** → crafting e estações → inimigos/armaduras/radiação.

## 2. Decisões

| Tema | Decisão |
|---|---|
| Biomas | Floresta morta (padrão, centro), neve, deserto |
| Disposição | Faixas fixas: neve de um lado do centro, deserto do outro (lado sorteado pela seed), borda irregular por ruído |
| Profundidade | O bioma desce até o fim da rocha rasa, afinando para baixo; rocha profunda, ruínas, minérios e cavernas iguais em todo lugar |
| Vegetação | Árvores simples feitas de tiles (árvore morta, pinheiro, cacto), derrubadas com a picareta, + decoração de 1 tile |
| Atmosfera | Cor do céu por bioma com transição + fundo distante em parallax (2 camadas por bioma) |
| Bioma atual | Contagem de blocos típicos em volta do jogador (como o Terraria); nada no save |

## 3. Geração do mundo

Ordem dos passes: `terrain` → `mountain` → **`biomes`** → `layers` → `caves` → `ores` → `walls` → `bunker` → **`trees`**.

### Passe `biomes`

- Produz um mapa de bioma por coluna (`ctx.Biomes`), que só existe durante a geração.
- Padrão: floresta. Duas faixas: neve e deserto; um sorteio (`ctx.Random` do passe) decide qual fica à esquerda do centro.
- Cada faixa tem largura `widthMin`–`widthMax` (fração da largura do mundo, padrão 0,15–0,20) e começa a `gapMin`–`gapMax` (fração, padrão 0,10–0,15) do lado de fora da montanha (do pé mais distante do centro, `MountainInfo`) — a montanha e o bunker ficam sempre na floresta. Entre a faixa e a borda do mundo sobra floresta.
- Mundo pequeno: a faixa é encolhida para caber entre a montanha + 10 colunas e a borda − 10 colunas; nunca lança erro; se sobrarem menos de 20 colunas, a faixa tem largura 20 e encosta na borda.
- Borda irregular: cada divisa é deslocada por ruído 1D (`biomes.edge`, amplitude `edgeNoise`, padrão 6 colunas).
- Afinamento: a `d` linhas abaixo da superfície, a divisa anda `d / taper` colunas para dentro da faixa (padrão `taper` = 4). O mapa guarda a divisa na superfície; `layers` e `walls` aplicam o afinamento por tile.

### Passe `layers` (alterado)

Por coluna, usa os blocos do bioma do tile (com o afinamento): camada de cima (hoje terra) = `surfaceBlock`, até o início da rocha profunda = `shallowBlock`. Floresta: `dirt`/`stone` (como hoje). Neve: `snow`/`ice`. Deserto: `sand`/`sandstone`. A rocha profunda não muda.

### Passes `ores` e `walls` (alterados)

- `ores`: minérios também substituem `ice` e `sandstone` (o `shallowBlock` de qualquer bioma), não só `stone`.
- `walls`: parede atrás da camada de cima = `surfaceWall`; atrás da rocha rasa = `shallowWall` (floresta `dirt_wall`/`stone_wall`; neve `snow_wall`/`ice_wall`; deserto `sand_wall`/`sandstone_wall`).

### Passe `trees`

- Roda depois do `bunker`; não planta dentro do retângulo do bunker (com o teto) nem no patamar diante do portão (± 2 colunas de folga).
- Percorre as colunas da superfície; em cada uma, com probabilidade `treeChance` do bioma e respeitando `treeSpacing` colunas desde a última árvore, planta a árvore do bioma se: o tile da superfície é o `surfaceBlock` do bioma, há ar para a árvore inteira (tronco + copa/galhos) e nenhum outro bloco no caminho.
- Formatos (código, parâmetros nos dados):
  - **dead** (árvore morta): tronco de `heightMin`–`heightMax` (6–12) com 2–4 galhos de 1–2 tiles saindo para os lados, alternados, na metade de cima.
  - **pine** (pinheiro): tronco de 8–14; copa triangular de `pine_leaves` nas últimas 2/3 do tronco, largura diminuindo de 5 para 1 tile até a ponta acima do tronco.
  - **cactus**: tronco de 3–6; até 2 braços (1 tile para o lado e 1–2 para cima), em lados diferentes.
- Decoração: nas colunas sem árvore, com probabilidade `decorationChance`, põe o decorativo do bioma sobre o chão (`dry_grass`, `frozen_shrub`, `dry_bush`).

### Parâmetros — `data/biomes.json` (novo)

```json
{
  "default": "forest",
  "edgeNoise": 6,
  "taper": 4,
  "detectWidth": 60,
  "detectHeight": 40,
  "detectThreshold": 200,
  "biomes": [
    { "id": "forest", "surfaceBlock": "dirt", "shallowBlock": "stone", "surfaceWall": "dirt_wall", "shallowWall": "stone_wall",
      "tree": { "shape": "dead", "trunk": "dead_trunk", "crown": "dead_branch", "heightMin": 6, "heightMax": 12 },
      "treeChance": 0.12, "treeSpacing": 5, "decoration": "dry_grass", "decorationChance": 0.25, "markers": [] },
    { "id": "snow", "band": { "widthMin": 0.15, "widthMax": 0.2, "gapMin": 0.1, "gapMax": 0.15 },
      "surfaceBlock": "snow", "shallowBlock": "ice", "surfaceWall": "snow_wall", "shallowWall": "ice_wall",
      "tree": { "shape": "pine", "trunk": "pine_trunk", "crown": "pine_leaves", "heightMin": 8, "heightMax": 14 },
      "treeChance": 0.15, "treeSpacing": 4, "decoration": "frozen_shrub", "decorationChance": 0.15, "markers": ["snow", "ice"] },
    { "id": "desert", "band": { "widthMin": 0.15, "widthMax": 0.2, "gapMin": 0.1, "gapMax": 0.15 },
      "surfaceBlock": "sand", "shallowBlock": "sandstone", "surfaceWall": "sand_wall", "shallowWall": "sandstone_wall",
      "tree": { "shape": "cactus", "trunk": "cactus", "crown": "cactus", "heightMin": 3, "heightMax": 6 },
      "treeChance": 0.06, "treeSpacing": 6, "decoration": "dry_bush", "decorationChance": 0.1, "markers": ["sand", "sandstone", "cactus"] }
  ]
}
```

- `band` ausente = bioma padrão (exatamente um, igual a `default`); exatamente duas faixas.
- `markers`: blocos que contam para a detecção (neve: `snow`, `ice`; deserto: `sand`, `sandstone`, `cactus`).
- Erros (bloco/parede inexistente, `shape` desconhecida, faixas ≠ 2, padrão ausente) param a carga com arquivo e campo, como os outros loaders.
- O arquivo entra na impressão digital da configuração; `WorldGenerator.Version` incrementada e `GoldenHash_*` atualizados.

## 4. Conteúdo

### Blocos novos (no **fim** do `blocks.json`)

| id | sólido | dureza | drop | árvore |
|---|---|---|---|---|
| `snow` | sim | 0,8 | `snow` | |
| `ice` | sim | 2,0 | `ice` | |
| `sand` | sim | 1,0 | `sand` | |
| `sandstone` | sim | 2,0 | `sandstone` | |
| `wood` | sim | 1,5 | `wood` | |
| `dead_trunk` | não | 1,5 | `wood` | sim |
| `dead_branch` | não | 0,5 | `wood` | sim |
| `pine_trunk` | não | 1,5 | `wood` | sim |
| `pine_leaves` | não | 0,2 | — | sim |
| `cactus` | não | 1,0 | `cactus` | sim |
| `dry_grass` | não | 0 | — | |
| `frozen_shrub` | não | 0 | — | |
| `dry_bush` | não | 0 | — | |

- Campo novo `"tree": true` no `blocks.json` (padrão falso). `"drops"` ausente = não dá item (já suportado pelo `RegistryLoader`).
- A areia não cai (fora do escopo).

### Paredes novas

`snow_wall`, `ice_wall`, `sand_wall`, `sandstone_wall`.

### Itens novos

Neve, Gelo, Areia, Arenito, Madeira (`wood`, coloca o bloco de tábuas), Cacto (`cactus`, coloca o bloco de cacto).

### Derrubar árvores (Core)

Quando um bloco com `tree` é quebrado, todos os blocos com `tree` ligados a ele (4 vizinhos, só por blocos com `tree`) que estejam **na mesma linha ou acima** também quebram, cada um soltando o próprio drop no seu tile e emitindo `TileChanged`. O que está abaixo (toco) fica. Não há regra de sustentação: cavar o chão sob a árvore não a derruba. Limite de segurança: no máximo 256 blocos por derrubada.

### Arte (ArtGen, `art/art.json`)

- `snow`, `ice`, `sand`, `sandstone`: receita `clumps` (gelo com brilhos claros; arenito com faixas horizontais — receita nova `strata`); entram na `mergeOrder` (neve < areia < terra < gelo < arenito < rocha …).
- `wood`: receita nova `planks` (tábuas horizontais com juntas).
- Troncos, galhos, folhas, cacto e decorativos: desenhos próprios (receita `prop`, formas novas: `trunk`, `branch`, `leaves`, `cactus`, `grass`, `shrub`, `bush`); troncos e cacto conectam verticalmente (o desenho atravessa o tile).
- Paredes: as receitas dos blocos, escurecidas.
- **Fundos:** receita nova `backdrop`: por bioma, 2 camadas PNG de 480×180 px, repetíveis na horizontal, silhuetas em 2 tons transparentes acima (floresta: árvores mortas e prédios em ruínas; neve: montanhas com neve; deserto: dunas e carcaças de veículos). Saída em `data/client/art/backgrounds/<bioma>_far.png` e `_near.png`, listadas no `art.json`.

## 5. Cliente

### Detecção (Core)

`BiomeDetector` (puro, testável): para um ponto, conta os `markers` de cada bioma num retângulo `detectWidth` × `detectHeight` centrado nele; o bioma com mais marcadores vence se tiver ≥ `detectThreshold`; senão, o padrão. O jogo consulta a cada 30 ticks na posição do jogador local.

### Ambiente (Client)

- `data/client/biomes.json`: por bioma, `sky` (cor) e os nomes das camadas de fundo. O `visuals.json` (que só tinha o céu) sai: `ClientVisuals` passa a ler `data/client/biomes.json` e exige uma entrada para cada bioma do `data/biomes.json`.
- `BiomeAmbience` (Client, testável): recebe o bioma detectado e o tempo; dá a cor do céu atual e o peso de cada fundo, mudando linearmente em 2 s quando o bioma muda.
- Godot: `RenderingServer.SetDefaultClearColor` com a cor atual; um `Parallax2D` por camada e bioma (fator de rolagem 0,1 e 0,3, repetição horizontal), com `Modulate.A` = peso; a base da camada fica na altura média da superfície do mundo (linha `surfaceMax × altura`), e não se desenha nada abaixo dela. Ficam atrás dos chunks e embaixo da camada de luz (a noite os escurece).
- F3: linha com o bioma atual.

## 6. Saves

Formato inalterado. Mundos antigos carregam como estão (sem blocos de bioma → floresta). Blocos novos no fim do `blocks.json`.

## 7. Erros

- `biomes.json` inválido → falha na carga com arquivo e campo.
- Faixa que não cabe → encolhe (nunca erro).
- Árvore que não cabe → não é plantada.
- Bloco/parede/fundo sem arte → o jogo recusa carregar (regra existente; o manifesto passa a validar os fundos dos biomas do `data/client/biomes.json`).

## 8. Testes

- **Loader de biomas:** arquivo real; cada erro com mensagem.
- **Passe `biomes`** (todos os tamanhos, várias seeds): três biomas presentes; neve e deserto em lados opostos; lado varia com a seed; montanha e bunker na floresta; larguras dentro dos limites (ou encolhidas no mundo pequeno).
- **Camadas:** blocos e paredes certos por bioma; abaixo da rocha rasa só rocha profunda; afinamento (a divisa anda para dentro com a profundidade).
- **Árvores:** inteiras, sobre o `surfaceBlock` do bioma, fora do bunker e do patamar; espaçamento respeitado.
- **Derrubada:** quebrar o tronco quebra o que está acima (galhos, folhas, braços) e solta madeira; o toco fica; blocos de árvore vizinhos abaixo não quebram.
- **Detector:** cada bioma reconhecido; abaixo do limiar = padrão.
- **Determinismo:** versão incrementada e hash atualizado.
- **ArtGen:** receitas novas (determinismo, contorno), fundos repetíveis (coluna 0 casa com a última), PNGs atualizados.
- **Client:** `BiomeAmbience` (transição de cor e pesos em 2 s, troca no meio da transição).
- **Smoke test:** o bioma no spawn é floresta.

## 9. Fora do escopo

Areia que cai, água/líquidos, clima (neve caindo, tempestade de areia), gelo escorregadio, biomas subterrâneos, zona radioativa, sustentação de árvores, mudas/replantio, inimigos por bioma.

## 10. Critérios de sucesso

1. Andando para um lado do bunker chega-se à neve; para o outro, ao deserto; cada um com blocos, árvores e decoração próprios.
2. Cavando na neve encontra-se gelo, no deserto arenito; mais fundo, a rocha profunda de sempre.
3. Árvores caem inteiras ao quebrar o tronco e dão madeira, que pode ser colocada.
4. O céu e o fundo mudam suavemente ao trocar de bioma.
5. Mundos determinísticos; saves antigos carregam; sem queda de FPS.
