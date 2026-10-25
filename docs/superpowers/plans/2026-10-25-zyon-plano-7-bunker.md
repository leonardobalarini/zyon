# Plano 7 — Base inicial (bunker) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O jogo começa dentro de um bunker de metal iluminado, escavado na face íngreme de uma montanha perto do centro do mundo, de onde o jogador sai andando pelo portão.

**Architecture:** Dois passes novos na geração (`mountain` depois de `terrain`; `bunker` no fim, no lugar de `spawn`). O bunker é um molde de texto em `data/structures/bunker.json` (formato genérico de estrutura, `Zyon.Core.WorldGen.Structures`). Blocos, parede e itens novos entram nos dados; a arte vem de receitas novas no `Zyon.ArtGen` (`plates`, `stripes`, `prop`).

**Tech Stack:** .NET 10, C#, xUnit 2.9.2, Arch ECS (inalterado), Godot 4.7.2 .NET (só o smoke test muda).

**Spec:** `docs/superpowers/specs/2026-10-25-zyon-bunker-design.md`

---

## Convenções (valem para todas as tarefas)

- Branch: `feat/plano-7-bunker` (crie a partir do `main` antes da Tarefa 1).
- Todo commit termina com estas duas linhas (como segundo `-m`):
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2
  ```
- Nunca `--amend`, nunca `push`.
- `TreatWarningsAsErrors` está ligado: nada de warnings.
- Toda tarefa termina com `dotnet test` (solução inteira) passando.
- Leia `CLAUDE.md` antes de começar (regras invioláveis: ids posicionais, blocos novos no **fim** do `blocks.json`, geração determinística, `DeterministicMath`, etc.).
- Comentários e mensagens em português, como no resto do código.

## Mapa de arquivos

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `data/blocks.json`, `data/walls.json`, `data/items.json` | Modificar | 7 blocos, 1 parede, 7 itens novos (no fim) |
| `art/art.json` | Modificar | Paletas/receitas dos novos |
| `src/Zyon.ArtGen/ArtSource.cs` | Modificar | Campos `shape`/`accent`, validação das receitas novas |
| `src/Zyon.ArtGen/TileGenerator.cs` | Modificar | Receitas `plates` e `stripes` (blocos e paredes), despacho de `prop` |
| `src/Zyon.ArtGen/PropGenerator.cs` | Criar | Desenho dos decorativos de 1 tile |
| `data/client/art/*` | Regenerar | `tools/gen-art.sh` |
| `src/Zyon.Core/WorldGen/Structures/StructureTemplate.cs` | Criar | Molde com nomes + `Resolve` |
| `src/Zyon.Core/WorldGen/Structures/Structure.cs` | Criar | Molde resolvido (ids), espelhamento |
| `src/Zyon.Core/WorldGen/Structures/StructureLoader.cs` | Criar | Leitura/validação do JSON do molde |
| `data/structures/bunker.json` | Criar | Planta do bunker |
| `src/Zyon.Core/WorldGen/WorldGenConfig.cs` | Modificar | Parâmetros novos; `Bunker` (molde carregado) |
| `src/Zyon.Core/WorldGen/WorldGenConfigLoader.cs` | Modificar | Valida parâmetros; carrega o molde |
| `src/Zyon.Core/WorldGen/WorldGenConfigFingerprint.cs` | Modificar | Inclui o texto do molde |
| `src/Zyon.Core/WorldGen/WorldGenMaterials.cs` | Modificar | `Bunker` resolvido |
| `src/Zyon.Core/WorldGen/WorldGenContext.cs` | Modificar | `MountainInfo`, `BunkerPlacement` |
| `src/Zyon.Core/WorldGen/Passes/MountainPass.cs` | Criar | Montanha |
| `src/Zyon.Core/WorldGen/Passes/BunkerPass.cs` | Criar | Bunker, teto, patamar, spawn |
| `src/Zyon.Core/WorldGen/Passes/SpawnPass.cs` | Remover | Substituído pelo `BunkerPass` |
| `src/Zyon.Core/WorldGen/WorldGenerator.cs` | Modificar | Ordem dos passes, `Version = 2` |
| `data/worldgen.json` | Modificar | Parâmetros novos; saem os de spawn |
| `src/Zyon.Game/SmokeTest.cs` | Modificar | Confere que começa no bunker |
| `CLAUDE.md`, `tools/smoke-test.sh` (se citar frames) | Modificar | Documentação |
| Testes | Criar/Modificar | Ver cada tarefa |

---

### Task 1: Conteúdo novo e arte (blocos, parede, itens, receitas)

**Files:**
- Modify: `data/blocks.json`, `data/walls.json`, `data/items.json`, `art/art.json`
- Modify: `src/Zyon.ArtGen/ArtSource.cs`, `src/Zyon.ArtGen/TileGenerator.cs`
- Create: `src/Zyon.ArtGen/PropGenerator.cs`
- Modify: `tests/Zyon.ArtGen.Tests/TileGeneratorTests.cs`
- Regenerate: `data/client/art/*`

- [ ] **Step 1: Dados — adicionar no FIM de cada arquivo**

`data/blocks.json` (depois de `torch`):
```json
  { "id": "metal_plate",  "hardness": 5.0, "solid": true,  "drops": "metal_plate" },
  { "id": "hazard_plate", "hardness": 5.0, "solid": true,  "drops": "hazard_plate" },
  { "id": "bunker_lamp",  "hardness": 0.0, "solid": false, "drops": "bunker_lamp", "light": 0.9 },
  { "id": "crate",        "hardness": 0.5, "solid": false, "drops": "crate" },
  { "id": "pipe",         "hardness": 0.5, "solid": false, "drops": "pipe" },
  { "id": "vent",         "hardness": 0.5, "solid": false, "drops": "vent" },
  { "id": "panel",        "hardness": 0.5, "solid": false, "drops": "panel", "light": 0.2 }
```

`data/walls.json` (depois de `stone_wall`):
```json
  { "id": "metal_wall" }
```

`data/items.json` (depois de `torch`):
```json
  { "id": "metal_plate",  "name": "Chapa de metal",  "maxStack": 999, "kind": "Block", "placesBlock": "metal_plate" },
  { "id": "hazard_plate", "name": "Chapa listrada",  "maxStack": 999, "kind": "Block", "placesBlock": "hazard_plate" },
  { "id": "bunker_lamp",  "name": "Luminária",       "maxStack": 999, "kind": "Block", "placesBlock": "bunker_lamp" },
  { "id": "crate",        "name": "Caixote",         "maxStack": 999, "kind": "Block", "placesBlock": "crate" },
  { "id": "pipe",         "name": "Cano",            "maxStack": 999, "kind": "Block", "placesBlock": "pipe" },
  { "id": "vent",         "name": "Grade",           "maxStack": 999, "kind": "Block", "placesBlock": "vent" },
  { "id": "panel",        "name": "Painel",          "maxStack": 999, "kind": "Block", "placesBlock": "panel" }
```
(Lembre das vírgulas: o JSON precisa continuar válido.)

`art/art.json` — em `blocks`, depois de `torch`:
```json
    "metal_plate":  { "recipe": "plates",  "palette": ["#3b434b", "#56606a", "#76818c"] },
    "hazard_plate": { "recipe": "stripes", "palette": ["#9a7a1e", "#d1a72c", "#ecc95a"], "accent": ["#1c1c1e", "#2a2a2d", "#3a3a3e"] },
    "bunker_lamp":  { "recipe": "prop", "shape": "lamp",  "palette": ["#2a2f35", "#4a525c", "#6e7884"], "accent": ["#c9a24a", "#ffe08a", "#fff6d2"] },
    "crate":        { "recipe": "prop", "shape": "crate", "palette": ["#4a3a26", "#6e5636", "#8e744c"] },
    "pipe":         { "recipe": "prop", "shape": "pipe",  "palette": ["#3d4a44", "#5a6e64", "#7f958a"] },
    "vent":         { "recipe": "prop", "shape": "vent",  "palette": ["#22272c", "#3e464f", "#5c6672"] },
    "panel":        { "recipe": "prop", "shape": "panel", "palette": ["#2a3038", "#434c58", "#626d7c"], "accent": ["#1e5a3a", "#3fd07a", "#b8ffd0"] }
```
e em `walls`, depois de `stone_wall`:
```json
    "metal_wall": { "recipe": "plates", "palette": ["#22282e", "#2e353c", "#3a424a"] }
```
`mergeOrder` não muda (metal não mescla).

- [ ] **Step 2: Escrever os testes que falham**

Em `tests/Zyon.ArtGen.Tests/TileGeneratorTests.cs`:

1. Corrija o teste existente `Blocks_HaveOneRowPerBlock_AndAllCells`: a linha `Assert.Equal(R.Blocks.Count - 2, rows["torch"]);` passa a ser
```csharp
        Assert.Equal(R.BlockId("torch") - 1, rows["torch"]);
```
(a tocha deixou de ser o último bloco).

2. Acrescente (use os helpers já existentes no arquivo — `Source`, `R`, `T`; se o arquivo usar outro nome para montar o atlas, siga o padrão de `Torch_IsTheSameInEveryCell_AndTransparentAround`):
```csharp
    [Theory]
    [InlineData("bunker_lamp")]
    [InlineData("crate")]
    [InlineData("pipe")]
    [InlineData("vent")]
    [InlineData("panel")]
    public void Prop_IsTheSameInEveryCell_AndHasTransparentPixels(string name)
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        int y = rows[name] * T;
        int cells = img.Width / T;
        bool anyTransparent = false, anyOpaque = false;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            var first = img.Get(px, y + py);
            if (first.A == 0) anyTransparent = true; else anyOpaque = true;
            for (int cell = 1; cell < cells; cell++)
                Assert.Equal(first, img.Get(cell * T + px, y + py));
        }
        Assert.True(anyTransparent, $"{name}: sem fundo transparente");
        Assert.True(anyOpaque, $"{name}: vazio");
    }

    [Fact]
    public void MetalPlate_HasJointsAndRivets()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var pal = ArtSource.Colors(Source.Blocks["metal_plate"].Palette!);
        // Máscara 15 (todos os vizinhos conectados): sem contorno; variação 0.
        int x = 15 * Source.Variations * T, y = rows["metal_plate"] * T;
        Assert.Equal(pal[0], img.Get(x + 7, y + 3));    // junta vertical (fiada de cima, sem desencontro)
        Assert.Equal(pal[0], img.Get(x + 3, y + 7));    // junta horizontal
        Assert.Equal(pal[2], img.Get(x + 1, y + 1));    // rebite
    }

    [Fact]
    public void HazardPlate_HasBothStripeColors()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var m = Source.Blocks["hazard_plate"];
        var yellow = ArtSource.Colors(m.Palette!);
        var black = ArtSource.Colors(m.Accent!);
        int x = 15 * Source.Variations * T, y = rows["hazard_plate"] * T;
        var pixels = Enumerable.Range(0, T * T).Select(i => img.Get(x + i % T, y + i / T)).ToList();
        Assert.Contains(pixels, p => yellow.Contains(p));
        Assert.Contains(pixels, p => black.Contains(p));
    }

    [Fact]
    public void MetalWall_UsesPlatesJoints()
    {
        var img = TileGenerator.BuildWalls(Source, R, out var rows);
        var pal = ArtSource.Colors(Source.Walls["metal_wall"].Palette!);
        int x = 15 * Source.Variations * T, y = rows["metal_wall"] * T;
        Assert.Equal(pal[0], img.Get(x + 7, y + 3));
    }
```
3. Acrescente casos ao `InvalidSource_Throws` não dá (o JSON mínimo falha antes). Crie um teste próprio que parte do `art/art.json` real e o edita:
```csharp
    [Theory]
    [InlineData("crate", """{"recipe":"clumps","palette":["#000000","#000000","#000000"]}""", "'torch' ou 'prop'")]
    [InlineData("crate", """{"recipe":"prop","shape":"sofa","palette":["#000000","#000000","#000000"]}""", "forma 'sofa'")]
    [InlineData("bunker_lamp", """{"recipe":"prop","shape":"lamp","palette":["#000000","#000000","#000000"]}""", "accent")]
    [InlineData("hazard_plate", """{"recipe":"stripes","palette":["#000000","#000000","#000000"]}""", "accent")]
    public void InvalidNewRecipes_Throw(string block, string entry, string expected)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(RepoPaths.ArtSource))!;
        root["blocks"]![block] = System.Text.Json.Nodes.JsonNode.Parse(entry);
        var ex = Assert.Throws<DataException>(() => ArtSource.Load(root.ToJsonString(), R));
        Assert.Contains(expected, ex.Message);
    }
```
Confira em `tests/Zyon.ArtGen.Tests/RepoPaths.cs` o nome real da propriedade que aponta para `art/art.json` e use-o (se não existir, acrescente `public static string ArtSource => Path.Combine(Root, "art", "art.json");` seguindo o padrão do arquivo).

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test tests/Zyon.ArtGen.Tests`
Expected: FAIL (`ArtSource` rejeita `plates`/`prop`, `Accent` não existe — erro de compilação).

- [ ] **Step 4: `ArtSource` — campos e validação**

Em `MaterialSource` acrescente:
```csharp
    public string? Shape { get; set; }
    public string[]? Accent { get; set; }
```
Troque `SolidRecipes` por:
```csharp
    private static readonly string[] SolidRecipes = ["clumps", "panels", "ore", "plates", "stripes"];
    private static readonly string[] WallRecipes = ["clumps", "plates"];
```
No laço de blocos de `Validate`, substitua as duas linhas de receita sólida/não sólida por:
```csharp
            if (def.Solid && !SolidRecipes.Contains(m.Recipe)) throw Fail($"{ctx}: receita '{m.Recipe}' inválida para bloco sólido (use {string.Join(", ", SolidRecipes)})");
            if (!def.Solid && m.Recipe is not ("torch" or "prop")) throw Fail($"{ctx}: bloco não sólido precisa da receita 'torch' ou 'prop'");
            if (m.Recipe == "prop")
            {
                if (m.Shape is null || !PropGenerator.Shapes.Contains(m.Shape))
                    throw Fail($"{ctx}: forma '{m.Shape}' desconhecida (use {string.Join(", ", PropGenerator.Shapes)})");
                if (PropGenerator.ShapesWithAccent.Contains(m.Shape)) CheckColors(m.Accent, 3, $"{ctx}.accent");
            }
            if (m.Recipe == "stripes") CheckColors(m.Accent, 3, $"{ctx}.accent");
```
(mantenha o restante: `ore`, `palette`, `cap`, `flame`). No laço de paredes, troque a checagem de receita por:
```csharp
            if (!WallRecipes.Contains(m.Recipe)) throw Fail($"walls.{def.Name}: paredes usam a receita 'clumps' ou 'plates'");
```

- [ ] **Step 5: `PropGenerator`**

Create `src/Zyon.ArtGen/PropGenerator.cs`:
```csharp
namespace Zyon.ArtGen;

/// <summary>Decorativos de 1 tile (blocos não sólidos): o mesmo desenho em todas as células, fundo transparente.</summary>
public static class PropGenerator
{
    public static readonly string[] Shapes = ["lamp", "crate", "pipe", "vent", "panel"];

    /// <summary>Formas que usam a cor de destaque (luz da luminária, tela e luzinha do painel).</summary>
    public static readonly string[] ShapesWithAccent = ["lamp", "panel"];

    public static void Draw(Canvas img, int ox, int oy, string shape, Rgba[] pal, Rgba[]? accent)
    {
        void Rect(int x0, int y0, int x1, int y1, Rgba c)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                img.Set(ox + x, oy + y, c);
        }

        switch (shape)
        {
            case "lamp":   // presa ao teto: suporte, corpo e vidro aceso embaixo
                Rect(4, 0, 11, 1, pal[0]);
                Rect(5, 2, 10, 3, pal[1]);
                Rect(5, 2, 10, 2, pal[2]);
                Rect(4, 4, 11, 4, pal[0]);
                Rect(5, 4, 10, 4, accent![1]);
                Rect(6, 5, 9, 5, accent[2]);
                break;
            case "crate":  // apoiado no chão: moldura, ripa do meio e travessa diagonal
                Rect(2, 4, 13, 15, pal[0]);
                Rect(3, 5, 12, 14, pal[1]);
                Rect(3, 9, 12, 10, pal[0]);
                for (int i = 0; i < 10; i++) img.Set(ox + 3 + i, oy + 14 - i, pal[2]);
                break;
            case "pipe":   // atravessa o tile na horizontal, com uma flange
                Rect(0, 6, 15, 6, pal[2]);
                Rect(0, 7, 15, 8, pal[1]);
                Rect(0, 9, 15, 9, pal[0]);
                Rect(6, 5, 7, 10, pal[0]);
                Rect(6, 5, 6, 10, pal[2]);
                break;
            case "vent":   // grade com aletas
                Rect(2, 3, 13, 12, pal[0]);
                for (int y = 4; y <= 11; y++) Rect(3, y, 12, y, y % 2 == 0 ? pal[2] : pal[0]);
                break;
            case "panel":  // caixa com tela, botões e luzinha
                Rect(3, 2, 12, 13, pal[0]);
                Rect(4, 3, 11, 12, pal[1]);
                Rect(5, 4, 10, 7, accent![0]);
                Rect(6, 5, 7, 5, accent[1]);
                Rect(5, 10, 6, 10, pal[2]);
                img.Set(ox + 9, oy + 10, accent[1]);
                img.Set(ox + 10, oy + 10, accent[2]);
                break;
            default:
                throw new ArgumentException($"forma '{shape}' desconhecida", nameof(shape));
        }
    }
}
```

- [ ] **Step 6: `TileGenerator` — `plates`, `stripes`, `prop`, parede `plates`**

Em `DrawBlock`, logo depois do `if (m.Recipe == "torch") {...}`:
```csharp
        if (m.Recipe == "prop")
        {
            PropGenerator.Draw(img, ox, oy, m.Shape!, ArtSource.Colors(m.Palette!), m.Accent is null ? null : ArtSource.Colors(m.Accent));
            return;
        }
```
Depois de `var fleck = ...;` acrescente:
```csharp
        var accent = m.Accent is null ? null : ArtSource.Colors(m.Accent);
```
e troque a linha `var c = pal[Tone(px, py, v, src.Seed, salt)];` por:
```csharp
            var c = m.Recipe switch
            {
                "plates" => Plate(px, py, v, src.Seed, salt, pal),
                "stripes" => Stripe(px, py, v, src.Seed, salt, pal, accent!),
                _ => pal[Tone(px, py, v, src.Seed, salt)],
            };
```
Acrescente os helpers (perto de `Panel`):
```csharp
    /// <summary>Chapas 8×8 em fiada desencontrada: junta escura, bisel claro, rebites nos cantos e manchas raras.</summary>
    private static Rgba Plate(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int row = py / 8, ly = py % 8;
        int shift = (row + v) % 2 * 4;   // a variação troca o desencontro das fiadas
        int lx = (px + shift) % 8;
        if (ly == 7 || lx == 7) return pal[0];
        if ((lx == 1 || lx == 5) && (ly == 1 || ly == 5)) return pal[2];
        if (ly == 0 || lx == 0) return pal[2];
        return ArtHash.Chance(v * T + px, py, seed + salt + 30, 6) ? pal[0] : pal[1];
    }

    /// <summary>Listras diagonais de 4 px (paleta e destaque) com lascas no tom escuro.</summary>
    private static Rgba Stripe(int px, int py, int v, int seed, int salt, Rgba[] pal, Rgba[] accent)
    {
        var tones = (px + py + v * 2) / 4 % 2 == 0 ? pal : accent;
        return ArtHash.Chance(v * T + px, py, seed + salt + 31, 7) ? tones[0] : tones[1];
    }
```
Confira: na máscara 15, variação 0, `(7,3)`: `row 0`, `shift 0`, `lx 7` → `pal[0]` ✓; `(3,7)`: `ly 7` → `pal[0]` ✓; `(1,1)`: rebite → `pal[2]` ✓ (o teste do Step 2 depende disto — a checagem de rebite vem antes do bisel).

Paredes: em `BuildWalls`, passe a receita para `DrawWall`:
```csharp
                DrawWall(img, (mask * src.Variations + v) * T, row * T, pal, src.Walls[name].Recipe == "plates", mask, v, src.Seed, salt: 5000 + id * 101);
```
e em `DrawWall` (nova assinatura `DrawWall(Canvas img, int ox, int oy, Rgba[] pal, bool plates, int mask, int v, int seed, int salt)`) troque `var c = pal[Tone(px, py, v, seed, salt)];` por:
```csharp
            var c = plates ? Plate(px, py, v, seed, salt, pal) : pal[Tone(px, py, v, seed, salt)];
```

- [ ] **Step 7: Rodar os testes do ArtGen**

Run: `dotnet test tests/Zyon.ArtGen.Tests`
Expected: os testes novos passam; `ArtUpToDateTests` FALHA (PNGs desatualizados) — esperado até o Step 8.

- [ ] **Step 8: Regenerar a arte e rodar tudo**

Run: `tools/gen-art.sh` e depois `dotnet test`
Expected: tudo PASS (Core, Client, ArtGen). Se algum teste do Client/Core assumir a tocha como último bloco ou contar blocos/paredes fixos, corrija usando `Registry.BlockId(...)`/`Blocks.Count` em vez de constantes.

Abra `data/client/art/tiles.png`, `walls.png` e `items.png` (Read da imagem) e confira visualmente: metal com juntas/rebites, listras amarelo/preto, os 5 decorativos legíveis, ícones novos no fim de `items.png`.

- [ ] **Step 9: Commit**

```bash
git add data/blocks.json data/walls.json data/items.json art/art.json src/Zyon.ArtGen tests/Zyon.ArtGen.Tests data/client/art
git commit -m "feat(art): blocos, parede e decorativos do bunker" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```
(Inclua também testes do Client/Core que precisou corrigir.)

---

### Task 2: Formato de estrutura (molde) — `Zyon.Core.WorldGen.Structures`

**Files:**
- Create: `src/Zyon.Core/WorldGen/Structures/StructureTemplate.cs`
- Create: `src/Zyon.Core/WorldGen/Structures/Structure.cs`
- Create: `src/Zyon.Core/WorldGen/Structures/StructureLoader.cs`
- Test: `tests/Zyon.Core.Tests/StructureLoaderTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Create `tests/Zyon.Core.Tests/StructureLoaderTests.cs`:
```csharp
using Zyon.Core.Data;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.Tests;

public class StructureLoaderTests
{
    // 6×6: casco de metal, interior com parede, portão (D) à esquerda, spawn (S) no meio, "keep" no canto.
    private const string Valid = """
    {
      "legend": {
        "#": { "block": "metal_plate", "wall": "metal_wall" },
        ".": { "wall": "metal_wall" },
        "S": { "wall": "metal_wall", "spawn": true },
        "D": { "wall": "metal_wall", "anchor": true },
        " ": "keep"
      },
      "rows": [
        "##### ",
        "#....#",
        ".....#",
        ".....#",
        "D.S..#",
        "######"
      ]
    }
    """;

    private static string With(string rowsJson) =>
        Valid[..Valid.IndexOf("\"rows\"", StringComparison.Ordinal)] + "\"rows\": " + rowsJson + "\n}";

    [Fact]
    public void Load_ReadsSizeMarkersAndCells()
    {
        var t = StructureLoader.Load("teste", Valid);

        Assert.Equal(6, t.Width);
        Assert.Equal(6, t.Height);
        Assert.Equal((0, 4), t.Anchor);
        Assert.Equal((2, 4), t.Spawn);
        Assert.True(t[5, 0].Keep);
        Assert.Equal("metal_plate", t[0, 0].Block);
        Assert.Null(t[1, 1].Block);
        Assert.Equal("metal_wall", t[1, 1].Wall);
        Assert.Equal(Valid, t.SourceText);
    }

    [Fact]
    public void Resolve_MapsNamesToIds_AndKeep()
    {
        var r = TestData.Registry();
        var s = StructureLoader.Load("teste", Valid).Resolve(r);

        Assert.Equal(r.BlockId("metal_plate"), s[0, 0].Block);
        Assert.Equal(r.WallId("metal_wall"), s[0, 0].Wall);
        Assert.Equal(0, s[1, 1].Block);
        Assert.True(s[5, 0].Keep);
        Assert.Equal(0, s.TopRow(0));
        Assert.Equal(1, s.TopRow(5));
    }

    [Fact]
    public void Mirrored_FlipsColumnsAndMarkers()
    {
        var s = StructureLoader.Load("teste", Valid).Resolve(TestData.Registry());
        var m = s.Mirrored();

        Assert.Equal((5, 4), m.Anchor);
        Assert.Equal((3, 4), m.Spawn);
        Assert.True(m[0, 0].Keep);
        Assert.Equal(s[0, 0], m[5, 0]);
    }

    [Theory]
    [InlineData("""["#####","####"]""", "linha 2: largura 4 diferente da primeira (5)")]
    [InlineData("""["##x##","D.S.#","#####"]""", "linha 1, coluna 3: caractere 'x' fora da legenda")]
    [InlineData("""["#####","D...#","#####"]""", "nenhum tile marcado com 'spawn'")]
    [InlineData("""["#####","DSS.#","#####"]""", "linha 2, coluna 3: segundo tile marcado com 'spawn'")]
    [InlineData("""["#####","..S.#","#####"]""", "nenhum tile marcado com 'anchor'")]
    [InlineData("""[]""", "'rows' ausente ou vazio")]
    public void Load_InvalidRows_ThrowsWithPosition(string rows, string expected)
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", With(rows)));

        Assert.Contains("structures/teste.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("""{"legend":{"ab":"keep"},"rows":["a"]}""", "a chave 'ab' deve ter um caractere")]
    [InlineData("""{"legend":{"#":"nada"},"rows":["#"]}""", "use \"keep\" ou um objeto")]
    [InlineData("""{"legend":{"#":{"blok":"x"}},"rows":["#"]}""", "campo 'blok' desconhecido")]
    [InlineData("""{"rows":["#"]}""", "campo 'legend' ausente")]
    [InlineData("""não é json""", "JSON inválido")]
    public void Load_InvalidLegend_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Resolve_UnknownBlock_ThrowsWithLegendChar()
    {
        var json = Valid.Replace("\"block\": \"metal_plate\"", "\"block\": \"granito\"");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("legenda '#': bloco inexistente 'granito'", ex.Message);
    }

    [Fact]
    public void Resolve_SpawnWithoutFloor_Throws()
    {
        var json = With("""["#####","#...#","#...#","D.S.#","##.##"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("linha 4, coluna 3: o spawn precisa de chão sólido logo abaixo", ex.Message);
    }

    [Fact]
    public void Resolve_SpawnWithoutHeadroom_Throws()
    {
        var json = With("""["#####","#####","D.S.#","#####"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("o spawn precisa de 3 colunas × 3 linhas livres", ex.Message);
    }

    [Fact]
    public void Resolve_AnchorWithoutFloor_Throws()
    {
        var json = With("""["#####","#...#","#...#","D.S.#",".####"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("linha 4, coluna 1: a âncora precisa de chão sólido logo abaixo", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.LoadFromDirectory(TestData.DataDir, "nao_existe"));

        Assert.Contains("structures/nao_existe.json", ex.Message);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter StructureLoaderTests`
Expected: FAIL (tipos não existem).

- [ ] **Step 3: Implementar**

Create `src/Zyon.Core/WorldGen/Structures/StructureTemplate.cs`:
```csharp
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen.Structures;

/// <summary>Entrada da legenda: bloco e parede por nome (null = ar / sem parede). <c>Keep</c> não altera o tile.</summary>
public sealed record StructureCell(char Symbol, string? Block, string? Wall, bool Keep);

/// <summary>Molde lido de <c>data/structures</c>, com nomes ainda não resolvidos. Imutável.</summary>
public sealed class StructureTemplate
{
    private readonly StructureCell[] cells;

    internal StructureTemplate(string name, string sourceText, int width, int height, StructureCell[] cells,
        (int X, int Y) anchor, (int X, int Y) spawn)
    {
        Name = name;
        SourceText = sourceText;
        Width = width;
        Height = height;
        this.cells = cells;
        Anchor = anchor;
        Spawn = spawn;
    }

    public string Name { get; }

    /// <summary>Texto do arquivo (entra na impressão digital da configuração da geração).</summary>
    public string SourceText { get; }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Tile de ar no pé do vão do portão; o tile abaixo dele é chão.</summary>
    public (int X, int Y) Anchor { get; }

    /// <summary>Tile onde ficam os pés do jogador ao nascer.</summary>
    public (int X, int Y) Spawn { get; }

    public StructureCell this[int x, int y] => cells[y * Width + x];

    /// <summary>Resolve os nomes no Registry e confere o chão e o espaço do spawn e o chão da âncora.</summary>
    public Structure Resolve(Registry registry)
    {
        DataException Fail(string message) => new($"{StructureLoader.FilePath(Name)}: {message}");

        var tiles = new StructureTile[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c.Keep)
            {
                tiles[i] = new StructureTile(0, 0, true);
                continue;
            }
            ushort block = 0, wall = 0;
            if (c.Block is not null && !registry.TryGetBlockId(c.Block, out block))
                throw Fail($"legenda '{c.Symbol}': bloco inexistente '{c.Block}'");
            if (c.Wall is not null && !registry.TryGetWallId(c.Wall, out wall))
                throw Fail($"legenda '{c.Symbol}': parede inexistente '{c.Wall}'");
            tiles[i] = new StructureTile(block, wall, false);
        }

        var s = new Structure(Name, Width, Height, tiles, Anchor, Spawn);
        bool Solid(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && !s[x, y].Keep && s[x, y].Block != 0 && registry.Block(s[x, y].Block).Solid;
        bool Free(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && !s[x, y].Keep && (s[x, y].Block == 0 || !registry.Block(s[x, y].Block).Solid);

        var (ax, ay) = Anchor;
        if (!Free(ax, ay) || !Solid(ax, ay + 1))
            throw Fail($"linha {ay + 1}, coluna {ax + 1}: a âncora precisa de chão sólido logo abaixo");

        var (sx, sy) = Spawn;
        if (!Solid(sx, sy + 1))
            throw Fail($"linha {sy + 1}, coluna {sx + 1}: o spawn precisa de chão sólido logo abaixo");
        for (int y = sy - 2; y <= sy; y++)
        for (int x = sx - 1; x <= sx + 1; x++)
            if (!Free(x, y))
                throw Fail($"linha {sy + 1}, coluna {sx + 1}: o spawn precisa de 3 colunas × 3 linhas livres (bloqueado em linha {y + 1}, coluna {x + 1})");
        return s;
    }
}
```
Atenção ao teste `Resolve_SpawnWithoutFloor_Throws`: a ordem das checagens é âncora → chão do spawn → espaço do spawn. No molde desse teste a âncora tem chão (`#` na linha 5, coluna 1) e o spawn (linha 4, coluna 3) tem `.` abaixo → falha no chão do spawn ✓. No `Resolve_AnchorWithoutFloor_Throws` a âncora (linha 4, coluna 1) tem `.` abaixo ✓.

Create `src/Zyon.Core/WorldGen/Structures/Structure.cs`:
```csharp
namespace Zyon.Core.WorldGen.Structures;

/// <summary>Tile do molde resolvido: ids de bloco e parede (0 = ar / sem parede). <c>Keep</c> não altera o mundo.</summary>
public readonly record struct StructureTile(ushort Block, ushort Wall, bool Keep);

/// <summary>Molde com ids do Registry, pronto para ser carimbado no mundo. Imutável.</summary>
public sealed class Structure
{
    private readonly StructureTile[] tiles;

    public Structure(string name, int width, int height, StructureTile[] tiles, (int X, int Y) anchor, (int X, int Y) spawn)
    {
        if (tiles.Length != width * height) throw new ArgumentException("tamanho dos tiles diferente de largura × altura", nameof(tiles));
        Name = name;
        Width = width;
        Height = height;
        this.tiles = tiles;
        Anchor = anchor;
        Spawn = spawn;
    }

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public (int X, int Y) Anchor { get; }
    public (int X, int Y) Spawn { get; }

    public StructureTile this[int x, int y] => tiles[y * Width + x];

    /// <summary>Cópia espelhada na horizontal (portão do outro lado).</summary>
    public Structure Mirrored()
    {
        var flipped = new StructureTile[tiles.Length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            flipped[y * Width + (Width - 1 - x)] = tiles[y * Width + x];
        return new Structure(Name, Width, Height, flipped, (Width - 1 - Anchor.X, Anchor.Y), (Width - 1 - Spawn.X, Spawn.Y));
    }

    /// <summary>Primeira linha da coluna que não é <c>Keep</c>, ou −1 se a coluna inteira é <c>Keep</c>.</summary>
    public int TopRow(int x)
    {
        for (int y = 0; y < Height; y++)
            if (!this[x, y].Keep) return y;
        return -1;
    }
}
```

Create `src/Zyon.Core/WorldGen/Structures/StructureLoader.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen.Structures;

/// <summary>
/// Lê moldes de <c>data/structures/NOME.json</c>: <c>legend</c> (caractere → "keep" ou {block, wall, spawn, anchor})
/// e <c>rows</c> (linhas de mesma largura). Exatamente um tile com <c>spawn</c> e um com <c>anchor</c>.
/// </summary>
public static class StructureLoader
{
    public const string DirectoryName = "structures";

    public static string FilePath(string name) => $"{DirectoryName}/{name}.json";

    public static StructureTemplate LoadFromDirectory(string dataDir, string name)
    {
        var path = Path.Combine(dataDir, DirectoryName, name + ".json");
        if (!File.Exists(path))
            throw new DataException($"{FilePath(name)}: arquivo não encontrado em '{dataDir}'");
        return Load(name, File.ReadAllText(path));
    }

    public static StructureTemplate Load(string name, string json)
    {
        DataException Fail(string message) => new($"{FilePath(name)}: {message}");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        if (root is not JsonObject obj) throw Fail("o conteúdo deve ser um objeto");
        if (obj["legend"] is not JsonObject legendNode) throw Fail("campo 'legend' ausente");
        if (obj["rows"] is not JsonArray rowsNode || rowsNode.Count == 0) throw Fail("campo 'rows' ausente ou vazio");

        var legend = new Dictionary<char, (StructureCell Cell, bool Spawn, bool Anchor)>();
        foreach (var (key, value) in legendNode)
        {
            if (key.Length != 1) throw Fail($"legenda: a chave '{key}' deve ter um caractere");
            char ch = key[0];
            if (value is JsonValue text && text.TryGetValue<string>(out var s))
            {
                if (s != "keep") throw Fail($"legenda '{key}': use \"keep\" ou um objeto");
                legend[ch] = (new StructureCell(ch, null, null, true), false, false);
                continue;
            }
            if (value is not JsonObject entry) throw Fail($"legenda '{key}': use \"keep\" ou um objeto");
            foreach (var (field, _) in entry)
                if (field is not ("block" or "wall" or "spawn" or "anchor"))
                    throw Fail($"legenda '{key}': campo '{field}' desconhecido");
            legend[ch] = (new StructureCell(ch, Name(entry, "block", key, Fail), Name(entry, "wall", key, Fail), false),
                Flag(entry, "spawn", key, Fail), Flag(entry, "anchor", key, Fail));
        }

        var rows = new List<string>();
        for (int r = 0; r < rowsNode.Count; r++)
        {
            if (rowsNode[r] is not JsonValue v || !v.TryGetValue<string>(out var row))
                throw Fail($"linha {r + 1}: deve ser texto");
            rows.Add(row);
        }
        int width = rows[0].Length;
        if (width == 0) throw Fail("linha 1: vazia");

        var cells = new StructureCell[width * rows.Count];
        (int X, int Y)? spawn = null, anchor = null;
        for (int y = 0; y < rows.Count; y++)
        {
            if (rows[y].Length != width) throw Fail($"linha {y + 1}: largura {rows[y].Length} diferente da primeira ({width})");
            for (int x = 0; x < width; x++)
            {
                char ch = rows[y][x];
                if (!legend.TryGetValue(ch, out var e)) throw Fail($"linha {y + 1}, coluna {x + 1}: caractere '{ch}' fora da legenda");
                cells[y * width + x] = e.Cell;
                if (e.Spawn)
                {
                    if (spawn is not null) throw Fail($"linha {y + 1}, coluna {x + 1}: segundo tile marcado com 'spawn'");
                    spawn = (x, y);
                }
                if (e.Anchor)
                {
                    if (anchor is not null) throw Fail($"linha {y + 1}, coluna {x + 1}: segundo tile marcado com 'anchor'");
                    anchor = (x, y);
                }
            }
        }
        if (spawn is null) throw Fail("nenhum tile marcado com 'spawn'");
        if (anchor is null) throw Fail("nenhum tile marcado com 'anchor'");
        return new StructureTemplate(name, json, width, rows.Count, cells, anchor.Value, spawn.Value);
    }

    private static string? Name(JsonObject entry, string field, string key, Func<string, DataException> fail)
    {
        if (!entry.TryGetPropertyValue(field, out var node) || node is null) return null;
        if (node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) return s;
        throw fail($"legenda '{key}': '{field}' deve ser um nome");
    }

    private static bool Flag(JsonObject entry, string field, string key, Func<string, DataException> fail)
    {
        if (!entry.TryGetPropertyValue(field, out var node) || node is null) return false;
        if (node is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        throw fail($"legenda '{key}': '{field}' deve ser true ou false");
    }
}
```
Nota: no teste `["#####","DSS.#","#####"]` o primeiro `S` está na coluna 2 e o segundo na coluna 3 → mensagem "linha 2, coluna 3: segundo tile marcado com 'spawn'" ✓. No `["#####","####"]` a mensagem é "linha 2: largura 4 diferente da primeira (5)" ✓.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Zyon.Core.Tests --filter StructureLoaderTests`
Expected: PASS. Depois `dotnet test` (tudo).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/WorldGen/Structures tests/Zyon.Core.Tests/StructureLoaderTests.cs
git commit -m "feat(core): moldes de estrutura (data/structures)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 3: Configuração da montanha e do bunker, molde do bunker

**Files:**
- Create: `data/structures/bunker.json`
- Modify: `src/Zyon.Core/WorldGen/WorldGenConfig.cs`, `WorldGenConfigLoader.cs`, `WorldGenConfigFingerprint.cs`, `WorldGenMaterials.cs`, `WorldGenContext.cs`
- Modify: `data/worldgen.json`
- Test: `tests/Zyon.Core.Tests/WorldGenConfigTests.cs`, `tests/Zyon.Core.Tests/FingerprintTests.cs`, `tests/Zyon.Core.Tests/BunkerTemplateTests.cs` (novo)

Os parâmetros de spawn (`spawnSearchRadius`, `spawnFlatWidth`) só saem na Tarefa 5, junto com o `SpawnPass`.

- [ ] **Step 1: Criar o molde do bunker**

Create `data/structures/bunker.json` (40 × 10; portão à esquerda — colunas 0–1, vão nas linhas 5–8 —; divisórias nas colunas 10, 21 e 31 com vão de 4 linhas; salas: hall, dormitório (spawn), depósito, máquinas), exatamente assim:
```json
{
  "legend": {
    "#": { "block": "metal_plate", "wall": "metal_wall" },
    "H": { "block": "hazard_plate", "wall": "metal_wall" },
    ".": { "wall": "metal_wall" },
    "L": { "block": "bunker_lamp", "wall": "metal_wall" },
    "P": { "block": "pipe", "wall": "metal_wall" },
    "V": { "block": "vent", "wall": "metal_wall" },
    "N": { "block": "panel", "wall": "metal_wall" },
    "C": { "block": "crate", "wall": "metal_wall" },
    "S": { "wall": "metal_wall", "spawn": true },
    "D": { "wall": "metal_wall", "anchor": true },
    " ": "keep"
  },
  "rows": [
    "HH######################################",
    "HH...L....#....L.....#....L....#...L...#",
    "HH........#..........#.........#PPPPPPP#",
    "HH....V...#..........#.........#.......#",
    "................................N..N...#",
    ".......................................#",
    "......................C......C.........#",
    "D..............S......CC.....CC.......V#",
    "HH######################################",
    "HH######################################"
  ]
}
```
Confira com um script que as 10 linhas têm 40 caracteres. Âncora `D` = (0, 7); spawn `S` = (15, 7).

- [ ] **Step 2: Escrever os testes que falham**

Create `tests/Zyon.Core.Tests/BunkerTemplateTests.cs`:
```csharp
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.Tests;

public class BunkerTemplateTests
{
    [Fact]
    public void BunkerTemplate_LoadsAndResolves_WithGateOnTheLeft()
    {
        var r = TestData.Registry();
        var t = StructureLoader.LoadFromDirectory(TestData.DataDir, "bunker");
        var s = t.Resolve(r);

        Assert.Equal(40, s.Width);
        Assert.Equal(10, s.Height);
        Assert.Equal((0, 7), s.Anchor);
        Assert.Equal((15, 7), s.Spawn);
        Assert.Equal(r.WallId("metal_wall"), s[s.Spawn.X, s.Spawn.Y].Wall);
    }

    [Fact]
    public void BunkerTemplate_UsesEveryBunkerBlock()
    {
        var r = TestData.Registry();
        var s = StructureLoader.LoadFromDirectory(TestData.DataDir, "bunker").Resolve(r);
        var used = new HashSet<ushort>();
        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
            used.Add(s[x, y].Block);

        foreach (var name in new[] { "metal_plate", "hazard_plate", "bunker_lamp", "crate", "pipe", "vent", "panel" })
            Assert.Contains(r.BlockId(name), used);
    }
}
```

Em `tests/Zyon.Core.Tests/WorldGenConfigTests.cs`, acrescente ao `[Theory]` de valores inválidos (o que já tem `[InlineData("""{"spawnFlatWidth": 1}""", "spawnFlatWidth")]` — siga o mesmo formato de `InlineData(json, campo)`):
```csharp
    [InlineData("""{"mountainSteepWidth": 3}""", "mountainSteepWidth")]
    [InlineData("""{"mountainGentleWidth": 3}""", "mountainGentleWidth")]
    [InlineData("""{"mountainHeight": 0}""", "mountainHeight")]
    [InlineData("""{"mountainNoiseFrequency": 0}""", "mountainNoiseFrequency")]
    [InlineData("""{"mountainNoiseAmplitude": -1}""", "mountainNoiseAmplitude")]
    [InlineData("""{"bunkerRoofRows": 0}""", "bunkerRoofRows")]
    [InlineData("""{"bunkerStructure": "../x"}""", "bunkerStructure")]
```
e os testes:
```csharp
    [Fact]
    public void LoadFromDirectory_LoadsTheBunkerTemplate()
    {
        var cfg = TestData.WorldGenConfig();

        Assert.NotNull(cfg.Bunker);
        Assert.Equal("bunker", cfg.Bunker!.Name);
    }

    [Fact]
    public void Load_WithoutDirectory_HasNoBunker()
    {
        Assert.Null(WorldGenConfigLoader.Load("{}").Bunker);
    }

    [Fact]
    public void Materials_ResolveTheBunker()
    {
        var m = WorldGenMaterials.Resolve(TestData.WorldGenConfig(), TestData.Registry());

        Assert.NotNull(m.Bunker);
        Assert.Equal(40, m.Bunker!.Width);
    }
```
Em `tests/Zyon.Core.Tests/FingerprintTests.cs`:
```csharp
    [Fact]
    public void BunkerTemplate_IsPartOfTheFingerprint()
    {
        var withBunker = TestData.WorldGenConfig();
        var withoutBunker = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")));

        Assert.NotEqual(WorldGenConfigFingerprint.Compute(withoutBunker), WorldGenConfigFingerprint.Compute(withBunker));
    }
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test tests/Zyon.Core.Tests`
Expected: FAIL de compilação (`Bunker`, `MountainSteepWidth` etc. não existem).

- [ ] **Step 4: Implementar**

`WorldGenConfig.cs` — acrescente `using System.Text.Json.Serialization;` e `using Zyon.Core.WorldGen.Structures;`, e as propriedades (antes de `SpawnSearchRadius`):
```csharp
    /// <summary>Distância do pico ao centro do mundo; positivo = à direita (portão voltado para o centro, à esquerda).</summary>
    public int MountainOffset { get; init; } = 40;
    /// <summary>Colunas do pé ao pico do lado do portão (paredão).</summary>
    public int MountainSteepWidth { get; init; } = 30;
    /// <summary>Colunas do pico ao pé do lado suave.</summary>
    public int MountainGentleWidth { get; init; } = 80;
    public float MountainHeight { get; init; } = 45f;
    public float MountainNoiseFrequency { get; init; } = 0.05f;
    public float MountainNoiseAmplitude { get; init; } = 4f;
    /// <summary>Nome do molde em data/structures (sem extensão).</summary>
    public string BunkerStructure { get; init; } = "bunker";
    /// <summary>Linhas sólidas garantidas acima do teto do bunker.</summary>
    public int BunkerRoofRows { get; init; } = 3;

    /// <summary>Molde do bunker, carregado por <see cref="WorldGenConfigLoader.LoadFromDirectory"/> (null com <c>Load(json)</c>).</summary>
    [JsonIgnore]
    public StructureTemplate? Bunker { get; internal set; }
```

`WorldGenConfigLoader.cs` — `LoadFromDirectory`:
```csharp
    public static WorldGenConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path))
            throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        var config = Load(File.ReadAllText(path));
        config.Bunker = StructureLoader.LoadFromDirectory(dataDir, config.BunkerStructure);
        return config;
    }
```
(com `using Zyon.Core.WorldGen.Structures;`). Em `Validate`, antes do laço de `Ores`:
```csharp
        Require(c.MountainSteepWidth >= 4, "'mountainSteepWidth' deve ser >= 4");
        Require(c.MountainGentleWidth >= 4, "'mountainGentleWidth' deve ser >= 4");
        Require(c.MountainHeight > 0, "'mountainHeight' deve ser > 0");
        Require(c.MountainNoiseFrequency > 0, "'mountainNoiseFrequency' deve ser > 0");
        Require(c.MountainNoiseAmplitude >= 0, "'mountainNoiseAmplitude' deve ser >= 0");
        Require(c.BunkerRoofRows >= 1, "'bunkerRoofRows' deve ser >= 1");
        Require(!string.IsNullOrEmpty(c.BunkerStructure) && c.BunkerStructure.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'),
            "'bunkerStructure' deve ter só letras minúsculas, dígitos e '_'");
```

`WorldGenConfigFingerprint.cs`:
```csharp
    public static ulong Compute(WorldGenConfig config)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(config, RegistryLoader.Options);
        ulong h = 14695981039346656037UL;
        h = Mix(h, json);
        if (config.Bunker is { } bunker)
            h = Mix(h, System.Text.Encoding.UTF8.GetBytes(bunker.SourceText));   // o molde muda o mundo gerado
        return h;
    }

    private static ulong Mix(ulong h, byte[] bytes)
    {
        foreach (byte b in bytes)
            h = (h ^ b) * 1099511628211UL;
        return h;
    }
```
Atualize o `<summary>` da classe: "FNV-1a 64 da configuração normalizada (serializada em JSON) e do texto do molde do bunker...".

`WorldGenMaterials.cs` — acrescente ao record (depois do parâmetro `Ores`, como propriedade de init) e preencha em `Resolve`:
```csharp
    /// <summary>Molde do bunker resolvido no Registry; null quando a configuração veio de <c>Load(json)</c>.</summary>
    public Structure? Bunker { get; init; }
```
e no fim de `Resolve`:
```csharp
        return new WorldGenMaterials(
            Block(m.Dirt, "materials.dirt"),
            Block(m.Stone, "materials.stone"),
            Block(m.DeepStone, "materials.deepStone"),
            Wall(m.DirtWall, "materials.dirtWall"),
            Wall(m.StoneWall, "materials.stoneWall"),
            ores)
        {
            Bunker = config.Bunker?.Resolve(registry),
        };
```
(com `using Zyon.Core.WorldGen.Structures;`).

`WorldGenContext.cs` — acrescente (no arquivo, fora da classe):
```csharp
/// <summary>Montanha do passe "mountain": pico, pé da face íngreme e se o portão fica à esquerda do pico.</summary>
public sealed record MountainInfo(int PeakX, int FootX, bool FacesLeft);

/// <summary>Onde o passe "bunker" carimbou o molde (canto superior esquerdo) e qual molde (já espelhado se preciso).</summary>
public sealed record BunkerPlacement(int X, int Y, Structure Structure);
```
e na classe:
```csharp
    /// <summary>Definido pelo passe "mountain".</summary>
    public MountainInfo? Mountain { get; internal set; }

    /// <summary>Definido pelo passe "bunker".</summary>
    public BunkerPlacement? Bunker { get; internal set; }
```
(com `using Zyon.Core.WorldGen.Structures;`).

`data/worldgen.json` — acrescente (antes de `"spawnSearchRadius"`):
```json
  "mountainOffset": 40,
  "mountainSteepWidth": 30,
  "mountainGentleWidth": 80,
  "mountainHeight": 45,
  "mountainNoiseFrequency": 0.05,
  "mountainNoiseAmplitude": 4,
  "bunkerStructure": "bunker",
  "bunkerRoofRows": 3,
```

- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test`
Expected: PASS (a geração ainda não usa nada disso; o golden hash não muda).

- [ ] **Step 6: Commit**

```bash
git add data/structures data/worldgen.json src/Zyon.Core/WorldGen tests/Zyon.Core.Tests
git commit -m "feat(core): parâmetros da montanha e do bunker e molde do bunker" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 4: Passe `mountain`

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/MountainPass.cs`
- Test: `tests/Zyon.Core.Tests/MountainPassTests.cs`

O passe ainda **não** entra em `DefaultPasses` (Tarefa 5).

- [ ] **Step 1: Escrever os testes que falham**

Create `tests/Zyon.Core.Tests/MountainPassTests.cs`:
```csharp
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class MountainPassTests
{
    private static WorldGenContext RunMountain(int width = Width, int height = Height, WorldGenConfig? config = null)
    {
        var ctx = Context(width, height, Seed, config);
        WorldGenerator.RunPass(ctx, new TerrainPass());
        WorldGenerator.RunPass(ctx, new MountainPass());
        return ctx;
    }

    [Fact]
    public void Mountain_PeakRightOfCenter_FacingLeft_TallerThanTheFoot()
    {
        var ctx = RunMountain();
        var m = ctx.Mountain!;

        Assert.True(m.FacesLeft);
        Assert.Equal(Width / 2 + 40, m.PeakX);
        Assert.Equal(m.PeakX - 30, m.FootX);
        Assert.True(ctx.Surface[m.FootX] - ctx.Surface[m.PeakX] >= 20,
            $"pico {ctx.Surface[m.PeakX]} pouco acima do pé {ctx.Surface[m.FootX]}");
    }

    [Fact]
    public void Mountain_KeepsTerrainOutsideItsRange_AndFillsBelowTheSurface()
    {
        var terrainOnly = Context();
        WorldGenerator.RunPass(terrainOnly, new TerrainPass());
        var ctx = RunMountain();
        var m = ctx.Mountain!;

        for (int x = 0; x < Width; x++)
        {
            if (x < m.FootX || x > m.PeakX + 80)
                Assert.Equal(terrainOnly.Surface[x], ctx.Surface[x]);
            Assert.Equal(0, ctx.World.GetBlock(x, ctx.Surface[x] - 1));
            for (int y = ctx.Surface[x]; y < Height; y++)
                Assert.NotEqual(0, ctx.World.GetBlock(x, y));
        }
        Assert.Equal(terrainOnly.Surface[m.FootX], ctx.Surface[m.FootX]);   // o pé fica no nível do terreno
    }

    [Fact]
    public void Mountain_SteepSideRisesFasterThanTheGentleSide()
    {
        var ctx = RunMountain();
        var m = ctx.Mountain!;
        int steepRise = ctx.Surface[m.FootX] - ctx.Surface[m.FootX + 10];
        int gentleRise = ctx.Surface[m.PeakX + 80] - ctx.Surface[m.PeakX + 70];

        Assert.True(steepRise > gentleRise, $"paredão {steepRise} vs suave {gentleRise}");
    }

    [Fact]
    public void Mountain_NegativeOffset_FacesRight()
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json"))
            .Replace("\"mountainOffset\": 40", "\"mountainOffset\": -40"));
        var ctx = RunMountain(config: cfg);
        var m = ctx.Mountain!;

        Assert.False(m.FacesLeft);
        Assert.Equal(Width / 2 - 40, m.PeakX);
        Assert.Equal(m.PeakX + 30, m.FootX);
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 128)]
    public void Mountain_SmallWorld_ScalesDownAndStaysInBounds(int width, int height)
    {
        var ctx = RunMountain(width, height);
        var m = ctx.Mountain!;
        int minRow = (int)(ctx.Config.SurfaceMin * height / 2);

        Assert.InRange(m.PeakX, 0, width - 1);
        Assert.InRange(m.FootX, 0, width - 1);
        for (int x = 0; x < width; x++)
            Assert.True(ctx.Surface[x] >= minRow, $"coluna {x}: superfície {ctx.Surface[x]} acima de {minRow}");
    }

    [Fact]
    public void Mountain_IsDeterministic()
    {
        Assert.Equal(RunMountain().Surface, RunMountain().Surface);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter MountainPassTests`
Expected: FAIL (`MountainPass` não existe).

- [ ] **Step 3: Implementar**

Create `src/Zyon.Core/WorldGen/Passes/MountainPass.cs`:
```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Ergue uma montanha perto do centro sobre o terreno: paredão (1 − u⁴) do lado do centro, onde fica o portão do
/// bunker, e descida suave (1 − smoothstep) do outro. Preenche com rocha (as camadas refinam depois). Em mundos
/// pequenos, offset, larguras e altura encolhem; o pico nunca sobe acima de surfaceMin × altura / 2.
/// </summary>
internal sealed class MountainPass : IWorldGenPass
{
    public string Name => "mountain";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int width = world.Width;
        bool facesLeft = cfg.MountainOffset >= 0;
        int dir = facesLeft ? 1 : -1;   // do pico para o lado suave

        float available = width / 2f - 2f;
        float k = Math.Min(1f, available / (Math.Abs(cfg.MountainOffset) + cfg.MountainGentleWidth));
        int offset = (int)MathF.Round(Math.Abs(cfg.MountainOffset) * k);
        int steep = Math.Max(4, (int)MathF.Round(cfg.MountainSteepWidth * k));
        int gentle = Math.Max(4, (int)MathF.Round(cfg.MountainGentleWidth * k));

        int peakX = Math.Clamp(width / 2 + dir * offset, 0, width - 1);
        int footX = Math.Clamp(peakX - dir * steep, 0, width - 1);
        int minRow = (int)(cfg.SurfaceMin * world.Height / 2);
        float height = Math.Min(cfg.MountainHeight * k, Math.Max(0, ctx.Surface[footX] - minRow));
        var noise = ctx.CreateNoise("mountain", cfg.MountainNoiseFrequency, 2);
        ushort fill = ctx.Materials.Stone;

        for (int i = -steep; i <= gentle; i++)
        {
            int x = peakX + dir * i;
            if (x < 0 || x >= width) continue;
            float u = i < 0 ? -i / (float)steep : i / (float)gentle;
            float shape = i < 0 ? 1f - u * u * u * u : 1f - u * u * (3f - 2f * u);
            int rise = (int)MathF.Round((height + noise.GetNoise(x, 0f) * cfg.MountainNoiseAmplitude) * shape);
            if (rise <= 0) continue;
            int top = Math.Max(minRow, ctx.Surface[x] - rise);
            for (int y = top; y < ctx.Surface[x]; y++)
                world.SetBlock(x, y, fill);
            ctx.Surface[x] = top;
        }

        ctx.Mountain = new MountainInfo(peakX, footX, facesLeft);
    }
}
```
Nota: no pé (`i = -steep`, `u = 1`) `shape = 0` → `rise = 0`: o pé fica no nível do terreno, como o teste exige. `MathF.Round`, `Math.Min/Max/Clamp` e aritmética simples são seguros para determinismo (ver CLAUDE.md); nada de `Sin/Cos/Pow`.

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test tests/Zyon.Core.Tests --filter MountainPassTests`
Expected: PASS. Se `Mountain_PeakRightOfCenter_..._TallerThanTheFoot` falhar por pouco no mundo 512×256 (o teto `minRow` = 32 limita a altura), mostre os valores e ajuste o limite do teste (≥ 20 deve sobrar folga: superfície típica ~70–90).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/WorldGen/Passes/MountainPass.cs tests/Zyon.Core.Tests/MountainPassTests.cs
git commit -m "feat(worldgen): passe da montanha" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 5: Passe `bunker`, nova ordem dos passes e fim do `SpawnPass`

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/BunkerPass.cs`
- Delete: `src/Zyon.Core/WorldGen/Passes/SpawnPass.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenerator.cs`, `WorldGenConfig.cs`, `WorldGenConfigLoader.cs`, `data/worldgen.json`
- Test: `tests/Zyon.Core.Tests/BunkerPassTests.cs` (novo); modificar `WorldGenPassTests.cs`, `WorldGeneratorTests.cs`, `WorldGenConfigTests.cs`, `WorldGenDeterminismTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Create `tests/Zyon.Core.Tests/BunkerPassTests.cs`:
```csharp
using Zyon.Core.Data;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class BunkerPassTests
{
    private static WorldGenContext RunAll(int width = Width, int height = Height, int seed = Seed, WorldGenConfig? config = null)
    {
        var ctx = Context(width, height, seed, config);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    public static TheoryData<int, int, int> Worlds => new()
    {
        { 512, 256, 1 }, { 512, 256, 2 }, { 512, 256, 3 },
        { 1050, 300, 1 }, { 1050, 300, 42 },
        { 128, 128, 5 }, { 64, 64, 9 },
    };

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Bunker_IsStampedTileByTile(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;

        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
        {
            var t = s[x, y];
            if (t.Keep) continue;
            Assert.Equal(t.Block, ctx.World.GetBlock(x0 + x, y0 + y));
            Assert.Equal(t.Wall, ctx.World.GetWall(x0 + x, y0 + y));
        }
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Spawn_IsOnTheMarkedTile_InsideTheBunker(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;

        Assert.Equal(new System.Numerics.Vector2(x0 + s.Spawn.X + 0.5f, y0 + s.Spawn.Y + 1), ctx.SpawnFeet);
        Assert.Equal(ctx.Registry.WallId("metal_wall"), ctx.World.GetWall(x0 + s.Spawn.X, y0 + s.Spawn.Y));
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Roof_HasSolidRowsAboveEveryColumn(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;
        int roof = ctx.Config.BunkerRoofRows;

        for (int x = 0; x < s.Width; x++)
        {
            int top = s.TopRow(x);
            for (int y = Math.Max(0, y0 + top - roof); y < y0 + top; y++)
                Assert.True(ctx.World.GetBlock(x0 + x, y) != 0, $"buraco no teto em ({x0 + x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Player_CanWalkFromSpawnToOpenSky(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
        var (x0, _, s) = ctx.Bunker!;
        int sx = (int)ctx.SpawnFeet.X, feet = (int)ctx.SpawnFeet.Y - 1;

        Assert.True(Walkable.ReachesOpenSky(ctx.World, ctx.Registry, sx, feet, x => x < x0 || x >= x0 + s.Width),
            $"sem caminho do spawn ({sx}, {feet}) até o céu aberto");
    }

    [Fact]
    public void Surface_MatchesFirstBlockOverTheBunkerAndPlateau()
    {
        var ctx = RunAll();
        var (x0, _, s) = ctx.Bunker!;

        for (int x = x0 - 3; x < x0 + s.Width; x++)
        {
            int first = 0;
            while (ctx.World.GetBlock(x, first) == 0) first++;
            Assert.Equal(first, ctx.Surface[x]);
        }
    }

    [Fact]
    public void MirroredMountain_PutsTheGateOnTheRight_AndStillWalksOut()
    {
        var cfg = WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir);
        var json = File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")).Replace("\"mountainOffset\": 40", "\"mountainOffset\": -40");
        var mirrored = WorldGenConfigLoader.Load(json);
        mirrored.Bunker = cfg.Bunker;   // Load(json) não carrega o molde
        var ctx = RunAll(config: mirrored);
        var (x0, _, s) = ctx.Bunker!;

        Assert.Equal(s.Width - 1, s.Anchor.X);
        int sx = (int)ctx.SpawnFeet.X, feet = (int)ctx.SpawnFeet.Y - 1;
        Assert.True(Walkable.ReachesOpenSky(ctx.World, ctx.Registry, sx, feet, x => x < x0 || x >= x0 + s.Width));
    }

    [Fact]
    public void WithoutTemplate_Throws()
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")));

        var ex = Assert.Throws<InvalidOperationException>(() => RunAll(config: cfg));
        Assert.Contains("molde do bunker", ex.Message);
    }
}

/// <summary>
/// Caminho andável aproximado: o jogador como coluna de 1 tile × 3 de altura; anda para o lado no mesmo nível,
/// sobe 1 tile (pulo) ou cai qualquer altura.
/// </summary>
internal static class Walkable
{
    public static bool ReachesOpenSky(TileWorld w, Registry r, int startX, int startFeet, Func<int, bool> outside)
    {
        bool Solid(int x, int y) => !w.InBounds(x, y) || r.Block(w.GetBlock(x, y)).Solid;
        bool Free(int x, int y) => !Solid(x, y) && !Solid(x, y - 1) && !Solid(x, y - 2);
        int Land(int x, int y)
        {
            while (y + 1 < w.Height && !Solid(x, y + 1)) y++;
            return y;
        }
        bool OpenSky(int x, int y)
        {
            if (w.GetWall(x, y) != 0) return false;
            for (int yy = 0; yy < y; yy++)
                if (w.GetBlock(x, yy) != 0) return false;
            return true;
        }

        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, Land(startX, startFeet)));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (!seen.Add((x, y))) continue;
            if (outside(x) && OpenSky(x, y)) return true;
            foreach (int dx in new[] { -1, 1 })
            {
                int nx = x + dx;
                if (Free(nx, y)) queue.Enqueue((nx, Land(nx, y)));
                else if (Free(x, y - 1) && Free(nx, y - 1)) queue.Enqueue((nx, Land(nx, y - 1)));   // sobe 1 tile
            }
        }
        return false;
    }
}
```
Nota: `mirrored.Bunker = cfg.Bunker;` exige que o setter `internal` de `Bunker` seja visível aos testes — o projeto de testes do Core já enxerga membros internos (ele usa `WorldGenContext` e passes internos). Se não enxergar, use `LoadFromDirectory` numa pasta temporária com o `worldgen.json` editado e uma cópia de `structures/bunker.json`.

Em `WorldGenDeterminismTests.cs`, troque a lista de `DefaultPasses_AreInSpecOrder` por:
```csharp
            new[] { "terrain", "mountain", "layers", "caves", "ores", "walls", "bunker" },
```

Em `WorldGenPassTests.cs`, **apague** `AssertValidSpawn`, `Spawn_OnNaturalTerrain_IsFlatSolidAndClear`, `Spawn_WithoutFlatGround_FlattensAtCenter` e `Spawn_FallbackWithTallZigzag_LeavesNoOverhang` (o `BunkerPassTests` cobre o spawn). Em `WorldGeneratorTests.cs`, apague `Generate_SpawnFlatWidthTooLargeForWorld_Throws`. Em `WorldGenConfigTests.cs`, apague o `InlineData` de `spawnFlatWidth` (e qualquer outro que cite `spawnSearchRadius`/`spawnFlatWidth`).

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/Zyon.Core.Tests`
Expected: FAIL (`BunkerPass` não existe; ordem dos passes).

- [ ] **Step 3: Implementar o `BunkerPass`**

Create `src/Zyon.Core/WorldGen/Passes/BunkerPass.cs`:
```csharp
using System.Numerics;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Carimba o molde do bunker com o portão no pé da face íngreme da montanha (espelhado se a face olha para a direita),
/// completa com rocha as linhas de teto que faltarem, abre um patamar diante do portão e põe o spawn no tile marcado.
/// </summary>
internal sealed class BunkerPass : IWorldGenPass
{
    /// <summary>Colunas livres diante do portão.</summary>
    public const int PlateauWidth = 3;

    /// <summary>Linhas livres acima do chão do patamar (o vão do portão tem 4).</summary>
    public const int PlateauClearRows = 5;

    public string Name => "bunker";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var mountain = ctx.Mountain ?? throw new InvalidOperationException("o passe 'bunker' precisa do passe 'mountain' antes");
        var template = ctx.Materials.Bunker
            ?? throw new InvalidOperationException("molde do bunker não carregado: use WorldGenConfigLoader.LoadFromDirectory");
        var s = mountain.FacesLeft ? template : template.Mirrored();
        int outward = mountain.FacesLeft ? -1 : 1;   // lado de fora do portão

        int x0 = Math.Clamp(mountain.FootX - s.Anchor.X, PlateauWidth + 1, world.Width - s.Width - PlateauWidth - 1);
        int gateX = x0 + s.Anchor.X;
        int y0 = Math.Clamp(ctx.Surface[gateX] - 1 - s.Anchor.Y, ctx.Config.BunkerRoofRows,
            world.Height - s.Height - ctx.Config.BottomSolidRows);
        int anchorY = y0 + s.Anchor.Y;

        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
        {
            var t = s[x, y];
            if (t.Keep) continue;
            world.SetBlock(x0 + x, y0 + y, t.Block);
            world.SetWall(x0 + x, y0 + y, t.Wall);
        }

        for (int x = 0; x < s.Width; x++)
            CoverRoof(ctx, x0 + x, y0 + s.TopRow(x), s.TopRow(x) >= 0);

        for (int i = 1; i <= PlateauWidth; i++)
            OpenPlateau(ctx, gateX + outward * i, anchorY);

        ctx.Bunker = new BunkerPlacement(x0, y0, s);
        ctx.SetSpawn(new Vector2(x0 + s.Spawn.X + 0.5f, y0 + s.Spawn.Y + 1));
    }

    /// <summary>As BunkerRoofRows linhas acima do topo do molde ficam sólidas (ar vira rocha, com parede abaixo da nova superfície).</summary>
    private static void CoverRoof(WorldGenContext ctx, int x, int top, bool hasTop)
    {
        if (!hasTop) return;
        var world = ctx.World;
        for (int y = Math.Max(0, top - ctx.Config.BunkerRoofRows); y < top; y++)
            if (world.GetBlock(x, y) == 0)
                world.SetBlock(x, y, ctx.Materials.Stone);

        int surface = FirstBlockRow(world, x);
        for (int y = surface + 1; y < top; y++)
            if (world.GetWall(x, y) == 0)
                world.SetWall(x, y, ctx.Materials.StoneWall);
        ctx.Surface[x] = surface;
    }

    /// <summary>Coluna diante do portão: livre (sem bloco nem parede) acima do chão do portão e com chão sólido.</summary>
    private static void OpenPlateau(WorldGenContext ctx, int x, int anchorY)
    {
        var world = ctx.World;
        for (int y = Math.Max(0, anchorY - PlateauClearRows + 1); y <= anchorY; y++)
        {
            world.SetBlock(x, y, 0);
            world.SetWall(x, y, 0);
        }
        if (world.GetBlock(x, anchorY + 1) == 0)
            world.SetBlock(x, anchorY + 1, ctx.Materials.Dirt);
        ctx.Surface[x] = FirstBlockRow(world, x);
    }

    private static int FirstBlockRow(TileWorld world, int x)
    {
        int y = 0;
        while (y < world.Height - 1 && world.GetBlock(x, y) == 0) y++;
        return y;
    }
}
```

- [ ] **Step 4: Ligar o passe, remover o spawn antigo, Version 2**

- Delete `src/Zyon.Core/WorldGen/Passes/SpawnPass.cs`.
- `WorldGenerator.cs`: `public const int Version = 2;`; `DefaultPasses()`:
  ```csharp
      public static IReadOnlyList<IWorldGenPass> DefaultPasses() =>
      [
          new TerrainPass(),
          new MountainPass(),
          new LayersPass(),
          new CavesPass(),
          new OrePass(),
          new WallsPass(),
          new BunkerPass(),
      ];
  ```
  e apague o bloco `if (config.SpawnFlatWidth > width / 4) ...` de `Generate`. Atualize o comentário "Gerador com os passes padrão do spec (seção 6)" para "Gerador com os passes padrão (terreno, montanha, camadas, cavernas, minérios, paredes, bunker)".
- `WorldGenConfig.cs`: apague `SpawnSearchRadius` e `SpawnFlatWidth`.
- `WorldGenConfigLoader.cs`: apague as duas validações de spawn.
- `data/worldgen.json`: apague `"spawnSearchRadius"` e `"spawnFlatWidth"` (cuide da vírgula final do objeto).

- [ ] **Step 5: Rodar; atualizar o golden hash**

Run: `dotnet test tests/Zyon.Core.Tests`
Expected: tudo passa exceto `GoldenHash_512x256_Seed1234` (mundo mudou). Leia os valores reais da mensagem de falha (hash e `SpawnFeet`) e atualize `GoldenHash` e `GoldenSpawnFeet` em `WorldGenDeterminismTests.cs`. Rode de novo: PASS.

Se `WorldGenScenarioTests.PlayerSpawnedOnGeneratedWorld_StandsStillAndCanWalk` falhar porque o jogador anda para a direita e bate numa parede interna do bunker em 120 ticks, isso NÃO é falha: o teste exige `walked.Position.X > spawn.X`, e o dormitório tem 5 tiles livres à direita do spawn — deve passar. Se não passar, mostre a posição e investigue antes de mudar o teste.

- [ ] **Step 6: Rodar tudo**

Run: `dotnet test`
Expected: PASS (Core, Client, ArtGen). Testes do Client que geram mundo (`GameLoaderTests`, `GameSaverTests`) devem continuar passando; se algum assumir o spawn na superfície, ajuste para o comportamento novo e explique no commit.

- [ ] **Step 7: Commit**

```bash
git add -A src/Zyon.Core data/worldgen.json tests/Zyon.Core.Tests tests/Zyon.Client.Tests
git commit -m "feat(worldgen): bunker na encosta da montanha, spawn dentro dele" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 6: Smoke test, documentação e verificação no jogo

**Files:**
- Modify: `src/Zyon.Game/SmokeTest.cs`
- Modify: `CLAUDE.md`
- Modify: `tools/smoke-test.sh` (só se o padrão de frames precisar subir)

- [ ] **Step 1: Smoke test confere o começo no bunker**

Em `SmokeTest.Step()`, logo depois da checagem de posição finita, acrescente:
```csharp
        if (frame == 0)
        {
            int tx = (int)System.MathF.Floor(me.Position.X);
            int ty = (int)System.MathF.Floor(me.Position.Y + me.Size.Y / 2 - 0.5f);   // tile dos pés
            if (session.World.GetWall(tx, ty) != session.World.Registry.WallId("metal_wall"))
            {
                Fail($"o jogador não começou no bunker (parede {session.World.GetWall(tx, ty)} em ({tx}, {ty}))");
                return;
            }
        }
```
Atualize o `<summary>` da classe: "Roteiro do smoke test: confere que o jogador nasce no bunker, fica parado, anda para a direita, ...".

- [ ] **Step 2: Rodar o smoke test**

Run: `tools/smoke-test.sh`
Expected: `SMOKE OK: ... save ok, luz ok, arte ok`. O chão do bunker é chapa de metal (dureza 5 → ~2,5 s com a picareta), então o roteiro leva mais tempo que antes: se falhar com "o roteiro não chegou à tocha", rode `tools/smoke-test.sh 900`; se 900 passar, mude o padrão em `tools/smoke-test.sh` para 900 e atualize o texto do CLAUDE.md ("padrão 600; o roteiro ... precisa de uns 300") com os números novos. Se falhar por outro motivo, investigue (superpowers:systematic-debugging) antes de mexer.

- [ ] **Step 3: Documentação — `CLAUDE.md`**

- Em "Estrutura", no item `src/Zyon.Core/WorldGen`: passes passam a ser `terrain`, `mountain`, `layers`, `caves`, `ores`, `walls`, `bunker`; acrescente: "estruturas em `data/structures/` (molde de texto: `legend` + `rows`, um `spawn` e um `anchor`; `Structures/StructureLoader`); o `bunker` carimba `bunker.json` no pé da face íngreme da montanha e define o spawn".
- Em "Regras invioláveis", acrescente: "Mudou um molde em `data/structures/`? A saída da geração muda: incremente `WorldGenerator.Version` e atualize o golden hash (o molde entra na impressão digital da configuração)." e "Blocos usados em moldes precisam existir no `blocks.json` e ter arte."
- Em "Comandos", se o padrão do smoke test mudou, atualize-o.

- [ ] **Step 4: Verificação visual no jogo**

Run: `"$GODOT" --path src/Zyon.Game -- --dev --size dev --seed 1` (Godot portátil: ver CLAUDE.md). Se não houver como abrir janela no ambiente, use o smoke test headless e descreva o que foi conferido. Confira (ou registre para o teste manual do usuário):
1. Começa dentro do bunker, iluminado pelas luminárias; paredes de metal ao fundo.
2. Anda para a esquerda, passa pelos vãos das divisórias, sai pelo portão listrado até o patamar e a superfície.
3. A montanha aparece com o paredão do lado do portão.
4. Minerar chapa de metal dá o item "Chapa de metal"; recolocá-la funciona; decorativos também.
5. F3: sem queda de FPS.

- [ ] **Step 5: Rodar tudo e commit**

Run: `dotnet test` e `tools/smoke-test.sh` (com os frames padrão).
Expected: PASS e `SMOKE OK`.

```bash
git add src/Zyon.Game CLAUDE.md tools/smoke-test.sh
git commit -m "feat(game): smoke test confere o começo no bunker; docs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

## Critérios do teste manual (para o usuário)

1. O jogo começa dentro de um bunker de metal iluminado, escavado numa montanha.
2. O jogador sai andando pelo portão até a superfície.
3. Minerar o bunker dá os itens, que podem ser recolocados.
4. Mundos continuam determinísticos; saves antigos carregam (abra um mundo salvo antes deste plano).
