# Zyon — Arte: Design

- **Data:** 2026-10-24
- **Status:** aprovado em brainstorming, aguardando revisão do spec escrito
- **Escopo:** subprojeto de arte: caminho dos sprites (gerador → PNGs → jogo), arte do conteúdo existente, jogador animado em camadas

## 1. Ambientação (contexto)

Mistura em camadas: superfície pós-apocalíptica com biomas (floresta, neve…), subsolo profundo com ruínas de uma civilização high-tech. O jogador começa numa base subterrânea simples (estilo bunker), que melhora com o tempo. Radiação e resistência por armadura virão depois.

Ordem dos próximos subprojetos: **arte (este)** → base inicial (bunker) → biomas → crafting e estações → inimigos/armaduras/radiação. Cada subprojeto traz a arte do próprio conteúdo usando o caminho criado aqui.

## 2. Decisões

| Tema | Decisão |
|---|---|
| Origem da arte | Blocos, paredes, jogador e ícones **gerados por código** agora; qualquer PNG pode ser trocado depois por arte de pacote, de IA ou feita à mão |
| Estilo dos blocos | **Contornado** (estilo Terraria): manchas em 3 tons, contorno escuro de 1 px e cantos arredondados contra o ar, brilho no topo |
| Fronteira entre materiais | **Mesclada**: o material mais mole avança 2–4 px irregulares sobre o vizinho mais duro |
| Jogador | **Molde em camadas** (pronto para armaduras), animação **básica + item na mão** com golpe em arco |
| Caminho | **Gerador como ferramenta**: PNGs e metadados commitados; o jogo só carrega PNGs |

## 3. O gerador — `src/Zyon.ArtGen`

- Programa .NET 10 de linha de comando, sem Godot; testes em `tests/Zyon.ArtGen.Tests`; roda com `tools/gen-art.sh`.
- Codificador PNG próprio (RGBA 8 bits, compressão `ZLibStream` do .NET, CRC32 próprio); sem bibliotecas de imagem externas.
- **Determinístico**: mesma entrada → mesmos bytes (ruído por hash inteiro, sem `System.Random`, sem ponto flutuante dependente de plataforma nos pixels).
- **Entrada — `art/art.json`:**
  - por material de bloco e parede: paleta de 3 tons (escuro, base, claro) e receita de textura (`clumps` — manchas; `ore` — base de outro material + pintas na cor do minério; `panels` — manchas + linhas de painel discretas; `sprite` — desenho próprio, ex.: tocha);
  - `mergeOrder`: lista de blocos do mais mole ao mais duro (terra → rocha → rocha profunda → minério A → minério B); blocos fora da lista (não sólidos) não mesclam;
  - cores do jogador: pele, cabelo, macacão (cor principal e detalhe), contorno;
  - itens: os de bloco usam a miniatura do bloco; os demais têm receita de desenho (`pickaxe`, `torch`).
- **Saída — `assets/art/`:**
  - `tiles.png` — por material: 16 máscaras de borda × 3 variações (16×16 px cada);
  - `walls.png` — o mesmo para paredes;
  - `merge.png` — por material da `mergeOrder`: 4 lados × 3 variações de borda irregular (16×16, transparente fora da borda);
  - `player/body.png`, `player/outfit.png`, `player/hair.png`, `player/arm.png` — folhas com os mesmos quadros (32×48 px);
  - `items.png` — ícones 16×16;
  - `art.json` — metadados: posição de cada sprite por nome, lista de quadros por animação, ponto da mão por quadro do braço, ponto de giro de cada item.
- Erros de entrada (paleta inválida, material desconhecido na `mergeOrder`, repetido, receita desconhecida) param o gerador com arquivo e campo.

## 4. A arte

### Blocos e paredes

- Textura em manchas de 2×2 px com os 3 tons da paleta, escolhidas por ruído de hash da posição no atlas (variação estável).
- Máscara de borda de 4 vizinhos (como o `AutoTile` atual), considerando só **ar** como borda: contorno escuro de 1 px no lado exposto, linha de brilho logo abaixo do topo exposto, cantos arredondados (pixel do canto transparente) onde dois lados expostos se encontram.
- Terra com topo exposto: camada de "cinza" de 2–3 px (equivalente pós-guerra da grama).
- Minérios: base de rocha + pintas na cor do minério (A ferrugem/cobre, B ciano).
- Rocha profunda: manchas + linhas de painel discretas (pista das ruínas).
- Paredes: mesma receita, escurecidas, sem mescla; borda contra parede vazia.
- Tocha: sprite único (cabo e chama), sem bordas.

### Mescla

- Na malha do chunk, para cada tile sólido de material M e cada vizinho ortogonal de material S **sólido e mais mole** que M na `mergeOrder`, desenha-se por cima do tile M a borda de S voltada para aquele lado (quad extra na mesma malha, variação estável pela posição).
- Nunca há mescla com ar, com blocos não sólidos nem entre paredes.

### Jogador

- Quadro de 32×48 px; pés no centro da borda de baixo (caixa de colisão 1,25 × 2,75 tiles = 20×44 px).
- Camadas, de trás para frente: corpo (pele e cabeça) → roupa (macacão do bunker) → cabelo → braço da frente → item na mão. Armaduras futuras = camadas de cabeça, tronco e pernas no mesmo molde.
- Animações: parado (1 quadro), andando (8 quadros, cadência proporcional à velocidade horizontal), no ar (1 quadro), golpe (braço da frente em 5 ângulos ao longo do arco).
- Golpe: enquanto o botão de uso está pressionado com item usável, braço e item giram de cima-atrás para frente-baixo; uma volta dura `0,35 s / velocidade da ferramenta` (itens sem velocidade usam 1) e se repete.
- Lado: ao usar, olha para a mira; senão, para o último sentido de movimento.
- Item na mão: ferramentas só durante o uso; itens de bloco (inclusive tocha) enquanto selecionados. A tocha na mão não ilumina (fora do escopo).

### Ícones

- 16×16; itens de bloco = miniatura do bloco com contorno; picareta e tocha desenhadas.
- Os mesmos ícones na hotbar, no inventário, no chão (desenhados com `itemSize` = 0,75 tile) e na mão.

## 5. Integração

### Core

`EntityView` ganha `Velocity`, `OnGround`, `UsingItem` (botão de uso pressionado), `AimX`/`AimY` (tile mirado) e `HeldItemId` (item selecionado na hotbar; 0 se nenhum). Os valores já existem na simulação; só passam a ser expostos. O save não muda.

### Client (testável)

- `ArtManifest`: lê `assets/art/art.json` e valida contra o `Registry` — todo bloco, parede e item precisa de arte, senão erro claro ("falta a arte do bloco 'torch'").
- `ChunkMeshBuilder`: usa as UVs do atlas de arte e emite os quads de mescla.
- `PlayerAnimator`: função pura de (`EntityView`, tempo, estado anterior) → quadro de cada camada, lado, ângulo do golpe e posição/rotação do item.
- Saem o `PlaceholderAtlas` e as cores de blocos/itens de `visuals.json` (fica a cor do céu e as do HUD).

### Game (Godot)

- `GameFlow` carrega os PNGs e o `ArtManifest` junto com os dados.
- `WorldRenderer` usa as texturas de arte; `EntityRenderer` desenha o jogador com as camadas (Sprite2D por camada ou um nó de desenho próprio) e os itens no chão com os ícones; hotbar e inventário usam os ícones.

## 6. Erros

- Arte faltando para conteúdo dos dados → falha na carga com mensagem clara (como as cores hoje).
- PNG ausente ou ilegível → falha na carga com o caminho do arquivo.
- Erros de entrada do gerador → o gerador para com arquivo e campo.

## 7. Testes

- **ArtGen:** PNG gravado e relido devolve os mesmos pixels; determinismo; contorno só contra ar; cantos transparentes; bordas de mescla com 2–4 px; quadros do jogador nas posições do `art.json`; **os PNGs commitados são iguais à saída atual do gerador**.
- **Client:** `PlayerAnimator` (parado, andando com cadência, no ar, golpe e fase do arco, lado pela mira e pelo movimento); quads de mescla (só vizinho sólido mais mole; nunca ar, não sólidos ou paredes); `ArtManifest` (leitura e erros de arte faltando).
- **Core:** os campos novos de `EntityView` refletem a simulação.
- **Smoke test:** a arte carregou; o quadro do jogador mudou ao andar e ao minerar.
- **Revisão visual:** os PNGs gerados e uma captura do jogo são revisados antes do teste manual.

## 8. Fora do escopo

Blocos do bunker e dos biomas; tocha na mão iluminando; partículas e poeira; céu desenhado; armaduras (só o molde); arte de pacotes.

## 9. Critérios de sucesso

1. Blocos texturizados no estilo contornado, com fronteiras mescladas entre terra, rocha e rocha profunda.
2. Jogador de macacão que anda, pula e golpeia com a picareta na mão, virando para a mira.
3. Ícones nos slots, no chão e na mão.
4. Trocar um PNG por outro do mesmo tamanho muda a arte no jogo sem mudar código.
5. Sem queda de FPS.
