# Zyon — Base inicial (bunker): Design

- **Data:** 2026-10-25
- **Status:** implementado
- **Escopo:** subprojeto da base inicial: montanha perto do centro, bunker de metal escavado na encosta (molde em arquivo), blocos e arte novos, spawn dentro do bunker

## 1. Contexto

O jogador começa num bunker simples estilo vault (ver spec da arte, seção 1), que melhora com o tempo. Este subprojeto entrega só a **estrutura**: melhorias, portas funcionais, baús e móveis grandes vêm com crafting e estações.

## 2. Decisões

| Tema | Decisão |
|---|---|
| Escopo | Só a estrutura (A); melhorias depois |
| Localização | Bunker escavado **na horizontal numa montanha**: o portão fica no pé da encosta, o jogador sai andando, sem escada |
| Entrada | **Portão decorativo aberto**: vão no casco emoldurado por chapas listradas; porta funcional fica para o crafting |
| Interior | **Planta fixa em arquivo** (molde de texto em `data/structures/`), reutilizável para outras estruturas |
| Mobília | Estrutura + **decoração de 1 tile** só visual (caixote, cano, grade, painel); nada de objetos de vários tiles |
| Mineração | O bunker é minerável como qualquer bloco; cada bloco dá o próprio item |

## 3. Geração do mundo

Ordem dos passes: `terrain` → **`mountain`** → `layers` → `caves` → `ores` → `walls` → **`bunker`**. O `spawn` atual sai.

### Passe `mountain`

- Ergue a superfície (`ctx.Surface`) a partir do terreno local, com pico em `center + mountainOffset` (padrão 40; negativo = montanha à esquerda do centro).
- Perfil assimétrico: do lado do centro (face do portão) cai em `mountainSteepWidth` colunas (padrão 30) num paredão (`1 − u⁴`); do outro, desce suave em `mountainGentleWidth` colunas (padrão 80, `1 − smoothstep(u)`). Altura de pico `mountainHeight` (padrão 45) mais ruído (`mountainNoiseFrequency`, `mountainNoiseAmplitude`), multiplicado pelo perfil (zero no pé).
- Preenche com rocha entre a nova superfície e a antiga; roda antes de `layers`, então a montanha recebe terra no topo, rocha por dentro, cavernas e minérios normalmente.
- Mundo pequeno: offset, larguras e altura reduzem pelo fator `min(1, (largura/2 − 2) / (|offset| + gentle))`; o pico nunca sobe acima da linha `surfaceMin × altura / 2`. Nunca lança erro.
- Registra no contexto o pico, o pé da face íngreme e o lado do portão (`MountainInfo`).

### Passe `bunker`

- Põe o portão (âncora do molde) no **pé da face íngreme**, com o chão do portão no nível do terreno ali; o molde entra na montanha. Se não couber no mundo, a posição é empurrada para dentro dos limites.
- Carimba o molde: cada caractere define bloco e parede; `keep` não mexe. Isso substitui cavernas, minérios e paredes naturais dentro da área.
- **Teto:** em cada coluna do molde, as `bunkerRoofRows` (3) linhas acima do topo do molde ficam sólidas — o que for ar vira rocha (com parede de rocha abaixo da nova superfície). Por isso não há busca de encosta: perto do pé, a rocha completa a montanha por cima do bunker.
- **Patamar:** diante do portão, 3 colunas × 5 linhas acima do chão do portão ficam livres (sem bloco nem parede) e com chão sólido.
- Define o spawn no caractere marcado (centro horizontal do tile, pés no topo do chão) e registra a posição do bunker no contexto (`BunkerPlacement`).

### Parâmetros (`data/worldgen.json`)

`mountainOffset`, `mountainSteepWidth`, `mountainGentleWidth`, `mountainHeight`, `mountainNoiseFrequency`, `mountainNoiseAmplitude`, `bunkerStructure` (nome do arquivo em `data/structures/`), `bunkerRoofRows`. Saem `spawnSearchRadius` e `spawnFlatWidth`.

A saída da geração muda: incrementar `WorldGenerator.Version` e atualizar as constantes do `WorldGenDeterminismTests.GoldenHash_*`.

## 4. Conteúdo

### Blocos novos (no **fim** do `blocks.json`; cada um dropa o item de mesmo nome)

| id | sólido | dureza | luz | uso |
|---|---|---|---|---|
| `metal_plate` | sim | 5,0 | — | casco: chapas com rebites |
| `hazard_plate` | sim | 5,0 | — | listras amarelo/preto em volta do portão |
| `bunker_lamp` | não | 0 | 0,9 | luminária de teto |
| `crate` | não | 0,5 | — | caixote |
| `pipe` | não | 0,5 | — | trecho de cano |
| `vent` | não | 0,5 | — | grade de ventilação |
| `panel` | não | 0,5 | 0,2 | painel com luzinha |

Decorativos comportam-se como a tocha (atravessáveis, recolocáveis). Itens de bloco novos no `items.json` (nomes: Chapa de metal, Chapa listrada, Luminária, Caixote, Cano, Grade, Painel).

### Parede nova

`metal_wall` — fundo das salas e do vão do portão.

### Arte (ArtGen, `art/art.json`)

- Receita `plates`: placas de metal com juntas a cada 8 px e rebites nos cantos, 3 tons de cinza-azulado; contorno contra ar como os outros blocos.
- Receita `stripes`: listras diagonais amarelo/preto gastas, com contorno.
- Parede `metal_wall`: receita `plates` escurecida.
- Sprites (receita `sprite`, desenho próprio por nome): `bunker_lamp` (luminária presa ao topo do tile, luz amarelada), `crate`, `pipe` (horizontal, atravessa o tile), `vent` (grade), `panel` (caixa com luzinha verde).
- Fora da `mergeOrder` (metal não mescla).
- Regerar com `tools/gen-art.sh`; `ArtUpToDateTests` garante PNGs atualizados.

## 5. O molde — `data/structures/bunker.json`

```json
{
  "legend": {
    "#": { "block": "metal_plate", "wall": "metal_wall" },
    "H": { "block": "hazard_plate", "wall": "metal_wall" },
    ".": { "wall": "metal_wall" },
    "L": { "block": "bunker_lamp", "wall": "metal_wall" },
    "S": { "wall": "metal_wall", "spawn": true },
    "D": { "wall": "metal_wall", "anchor": true },
    " ": "keep"
  },
  "rows": [ "..." ]
}
```

- Cada entrada da legenda: `block` (opcional; ausente = ar), `wall` (opcional; ausente = sem parede), `spawn` e `anchor` (marcadores; exatamente um de cada no molde). `"keep"` não altera o tile.
- `anchor` marca o tile de ar no pé do vão do portão (o tile abaixo dele é chão); o passe alinha esse tile com a coluna da encosta. O portão fica na borda do molde voltada para o centro do mundo; o molde é desenhado com o portão à **esquerda** e espelhado horizontalmente quando a montanha está à esquerda do centro.
- Planta (40 × 10 tiles): hall de entrada com o portão emoldurado em `H`, corredor, dormitório (spawn), depósito (caixotes), sala de máquinas (canos, painéis, grades); luminárias no teto de cada sala; chão contínuo, degraus de no máximo 1 tile.
- Carga em `StructureLoader` (Core): erros param com arquivo, linha e coluna — caractere fora da legenda, bloco/parede desconhecido, linhas de larguras diferentes, zero ou mais de um `spawn`/`anchor`, `anchor` sem chão sólido logo abaixo.

## 6. Integração

- **Core:** `WorldGen/Structures/StructureTemplate` (grade imutável + marcadores) e `StructureLoader`; `MountainPass`, `BunkerPass`; `WorldGenConfig` com os parâmetros novos; `SpawnPass` removido.
- **Client/Game:** nenhum sistema novo; decorativos desenhados como a tocha (sprite único), metal com autotile normal.
- **Saves:** formato inalterado (blocos novos no fim; saves gravam por nome); mundos existentes carregam como estão.
- **Luz:** o interior não vê o céu (tem parede); só luminárias e painéis iluminam.

## 7. Erros

- Molde inválido → falha na carga com arquivo, linha e coluna.
- Montanha que não cabe → reduz a escala, sem erro.
- Bunker: não há busca de encosta. A posição vem do pé da face íngreme, empurrada para dentro do mundo se não couber; o teto é completado com rocha e o patamar diante do portão é aberto. Sempre há bunker.
- Bloco/parede novo sem arte → o jogo recusa carregar (regra já existente).

## 8. Testes

- **StructureLoader:** molde válido lido corretamente; cada erro com mensagem, linha e coluna; espelhamento.
- **MountainPass:** pico na posição e altura esperadas; face íngreme do lado do centro; mundo `dev` sem estouro.
- **BunkerPass** (várias seeds e todos os tamanhos):
  - molde carimbado tile a tile (respeitando `keep`);
  - spawn dentro do dormitório, sobre chão sólido, com espaço livre para o jogador;
  - existe caminho andável (subida de no máximo 1 tile por passo, altura livre do jogador) do spawn até um tile de superfície fora da montanha a céu aberto;
  - pelo menos `bunkerRoofRows` linhas de bloco acima do teto do molde.
- **Determinismo:** `WorldGenerator.Version` incrementada e hash de referência atualizado.
- **ArtGen:** receitas `plates` e `stripes` (contorno, determinismo), sprites presentes; PNGs commitados atualizados.
- **Smoke test:** confere que o jogador começa dentro do bunker (parede de metal no tile do spawn) e ajusta o roteiro de mineração e tocha ao spawn novo.

## 9. Fora do escopo

Porta funcional, baú, móveis de vários tiles, melhorias/módulos do bunker, outras estruturas (ruínas), biomas.

## 10. Critérios de sucesso

1. O jogo começa dentro de um bunker de metal iluminado, escavado numa montanha.
2. O jogador sai andando pelo portão até a superfície.
3. O bunker é minerável e seus blocos podem ser recolocados.
4. Mundos continuam determinísticos; saves antigos carregam.
