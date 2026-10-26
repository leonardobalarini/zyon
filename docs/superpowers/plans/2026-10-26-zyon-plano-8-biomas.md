# Plano 8 — Biomas da superfície Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A superfície ganha três biomas — floresta morta (centro, padrão), neve e deserto (faixas em lados opostos) — com blocos, paredes, árvores derrubáveis, decoração, cor do céu e fundo em parallax próprios, e o jogo detecta o bioma em volta do jogador.

**Architecture:** Parâmetros em `data/biomes.json` (Core, `Zyon.Core.Biomes`: loader, conjunto resolvido, detector). Na geração, um passe `biomes` (depois de `mountain`) monta um `BiomeMap` (faixas com borda por ruído e afinamento com a profundidade) que `layers`, `ores` e `walls` consultam por tile; um passe `trees` (depois de `bunker`) planta árvores e decoração. Quebrar um bloco com `"tree": true` derruba a árvore (`TreeFelling`). No cliente, `data/client/biomes.json` (céu + camadas de fundo) substitui o `visuals.json`; `BiomeAmbience` faz a transição de 2 s; o Godot desenha o céu e dois `Parallax2D` por bioma. A arte nova (receitas `strata`, `planks`, brilho no `clumps`, formas de `prop` e fundos `backdrop`) vem do `Zyon.ArtGen`.

**Tech Stack:** .NET 10, C#, xUnit, Arch ECS (inalterado), Godot 4.7.2 .NET (`Parallax2D`).

**Spec:** `docs/superpowers/specs/2026-10-25-zyon-biomas-design.md`

---

## Convenções (valem para todas as tarefas)

- Branch: `feat/plano-8-biomas` (crie a partir do `main` antes da Tarefa 1).
- Todo commit termina com estas duas linhas (como segundo `-m`):
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2
  ```
- Nunca `--amend`, nunca `push`.
- `TreatWarningsAsErrors` está ligado: nada de warnings.
- Toda tarefa termina com `dotnet test` (solução inteira — inclui compilar o `Zyon.Game`) passando.
- Leia `CLAUDE.md` antes de começar (ids posicionais, blocos novos no **fim** do `blocks.json`, geração determinística, `DeterministicMath`, nada de `System.Random`, nomes de passes semeiam o Random, etc.).
- Comentários e mensagens em português, como no resto do código; siga a densidade de comentários dos arquivos vizinhos.
- Testes que montam a configuração com `WorldGenConfigLoader.Load(json)` não têm o molde **nem os biomas**. Se rodarem `biomes`, `layers`, `ores`, `walls`, `trees` ou o gerador inteiro, copie os dois do carregado do disco, como já fazem com o molde:
  ```csharp
  var loaded = TestData.WorldGenConfig();
  cfg.Bunker = loaded.Bunker;
  cfg.Biomes = loaded.Biomes;
  ```

## Mapa de arquivos

| Arquivo | Ação | Responsabilidade |
|---|---|---|
| `src/Zyon.Core/Data/Defs.cs`, `RegistryLoader.cs` | Modificar | Campo `tree` dos blocos |
| `src/Zyon.ArtGen/ArtSource.cs` | Modificar | Receitas `strata`/`planks`, brilho do `clumps`, fundos |
| `src/Zyon.ArtGen/TileGenerator.cs` | Modificar | Desenho de `strata`, `planks`, brilhos; paredes `strata` |
| `src/Zyon.ArtGen/PropGenerator.cs` | Modificar | Formas `trunk`, `branch`, `leaves`, `cactus`, `grass`, `shrub`, `bush` |
| `src/Zyon.ArtGen/BackdropGenerator.cs` | Criar | Fundos 480×180 repetíveis |
| `src/Zyon.ArtGen/ArtBuilder.cs` | Modificar | Gera e lista os fundos |
| `src/Zyon.Client/ArtManifest.cs`, `ArtCatalog.cs` | Modificar | `backgrounds` no manifesto, tamanho e existência |
| `data/blocks.json`, `walls.json`, `items.json`, `art/art.json` | Modificar | 13 blocos, 4 paredes, 6 itens, arte e fundos |
| `data/client/art/*` | Regenerar | `tools/gen-art.sh` |
| `data/biomes.json` | Criar | Parâmetros dos biomas |
| `src/Zyon.Core/Biomes/BiomeConfig.cs` | Criar | Classes do JSON |
| `src/Zyon.Core/Biomes/BiomeConfigLoader.cs` | Criar | Leitura e validação estrutural |
| `src/Zyon.Core/Biomes/BiomeSet.cs` | Criar | Biomas resolvidos (ids) |
| `src/Zyon.Core/Biomes/BiomeDetector.cs` | Criar | Bioma em volta de um ponto |
| `src/Zyon.Core/WorldGen/WorldGenConfig.cs`, `WorldGenConfigLoader.cs`, `WorldGenConfigFingerprint.cs`, `WorldGenMaterials.cs`, `WorldGenContext.cs` | Modificar | Biomas na configuração, materiais e contexto |
| `src/Zyon.Core/WorldGen/BiomeMap.cs` | Criar | Bioma de cada tile durante a geração |
| `src/Zyon.Core/WorldGen/Passes/BiomesPass.cs` | Criar | Faixas de neve e deserto |
| `src/Zyon.Core/WorldGen/Passes/TreesPass.cs`, `TreeShapes.cs` | Criar | Árvores e decoração |
| `src/Zyon.Core/WorldGen/Passes/MountainPass.cs`, `LayersPass.cs`, `OrePass.cs`, `WallsPass.cs` | Modificar | Pé suave da montanha; blocos/paredes por bioma |
| `src/Zyon.Core/WorldGen/WorldGenerator.cs` | Modificar | Ordem dos passes, `Version` 3 |
| `src/Zyon.Core/Sim/Systems/TreeFelling.cs` | Criar | Blocos de árvore a derrubar |
| `src/Zyon.Core/Sim/Systems/ToolActionSystem.cs` | Modificar | Derruba a árvore ao quebrar um bloco de árvore |
| `data/client/biomes.json` | Criar | Céu e camadas de fundo por bioma |
| `data/client/visuals.json` | Apagar | Substituído pelo anterior |
| `src/Zyon.Client/ClientVisuals.cs` | Reescrever | Lê `data/client/biomes.json` |
| `src/Zyon.Client/BiomeAmbience.cs` | Criar | Transição de céu e fundos |
| `src/Zyon.Game/GameFlow.cs`, `GameRoot.cs`, `Rendering/ArtTextures.cs`, `Rendering/BackgroundLayers.cs` (novo), `Hud/DebugOverlay.cs` | Modificar | Céu, fundos, detecção, F3, smoke |
| `CLAUDE.md` | Modificar | Documentação |

---

### Task 1: Campo `tree` dos blocos

**Files:**
- Modify: `src/Zyon.Core/Data/Defs.cs`
- Modify: `src/Zyon.Core/Data/RegistryLoader.cs`
- Test: `tests/Zyon.Core.Tests/RegistryLoaderTests.cs`

- [ ] **Step 1: Escreva os testes que falham**

No fim da classe `RegistryLoaderTests` (adicione `using System.Text.Json.Nodes;` se faltar):

```csharp
    [Fact]
    public void TreeFlag_DefaultsToFalse_AndIsRead()
    {
        var r = TestData.RegistryWithEdits(editBlocks: blocks =>
            blocks.Add(JsonNode.Parse("""{"id": "test_trunk", "hardness": 1.0, "solid": false, "tree": true}""")));

        Assert.True(r.Block(r.BlockId("test_trunk")).Tree);
        Assert.False(r.Block(r.BlockId("dirt")).Tree);
        Assert.False(r.Block(0).Tree);
    }

    [Fact]
    public void TreeFlag_WithWrongType_Throws()
    {
        var ex = Assert.Throws<DataException>(() => TestData.RegistryWithEdits(editBlocks: blocks =>
            blocks.Add(JsonNode.Parse("""{"id": "test_trunk", "hardness": 1.0, "solid": false, "tree": "sim"}"""))));

        Assert.Contains("blocks.json", ex.Message);
    }
```

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~RegistryLoaderTests.TreeFlag"`
Expected: FAIL — não compila (`BlockDef` não tem `Tree`).

- [ ] **Step 3: Implemente**

`src/Zyon.Core/Data/Defs.cs` — troque a declaração do `BlockDef`:

```csharp
/// <summary>Light: luz emitida pelo bloco, em [0, 1] (0 = não emite). Tree: parte de árvore (quebrar derruba o que está ligado acima).</summary>
public sealed record BlockDef(ushort Id, string Name, float Hardness, bool Solid, ushort DropItemId, float Light = 0f, bool Tree = false);
```

`src/Zyon.Core/Data/RegistryLoader.cs`:
- na criação do bloco: `blocks[i + 1] = new BlockDef((ushort)(i + 1), d.Id!, hardness, solid, drop, light, d.Tree ?? false);`
- no `BlockDto`, depois de `Light`: `public bool? Tree { get; set; }`

- [ ] **Step 4: Rode e veja passar**

Run: `dotnet test`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/Data tests/Zyon.Core.Tests/RegistryLoaderTests.cs
git commit -m "feat(core): campo tree nos blocos" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 2: Receitas novas da arte (`strata`, `planks`, brilhos, formas de árvore e decoração)

Ainda sem conteúdo novo: os testes trocam a receita de um bloco existente numa cópia do `art.json` carregado, ou desenham a forma direto num `Canvas`.

**Files:**
- Modify: `src/Zyon.ArtGen/ArtSource.cs`
- Modify: `src/Zyon.ArtGen/TileGenerator.cs`
- Modify: `src/Zyon.ArtGen/PropGenerator.cs`
- Test: `tests/Zyon.ArtGen.Tests/TileGeneratorTests.cs`

- [ ] **Step 1: Escreva os testes que falham**

No fim de `TileGeneratorTests`:

```csharp
    private static ArtSource FreshSource() => ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);

    private static readonly string[] Gray = ["#101010", "#808080", "#c0c0c0"];

    private static (int X, int Y) CellOf(ArtSource src, int row, int mask, int variation) =>
        ((mask * src.Variations + variation) * T, row * T);

    /// <summary>Em cada coluna da célula, as linhas escuras ficam a 4 px uma da outra (faixas horizontais).</summary>
    private static void AssertStrata(Canvas img, int x, int y)
    {
        var dark = Rgba.Parse(Gray[0]);
        for (int px = 0; px < T; px++)
        {
            var rows = Enumerable.Range(0, T).Where(py => img.Get(x + px, y + py) == dark).ToList();
            Assert.Equal(4, rows.Count);
            for (int i = 1; i < rows.Count; i++) Assert.Equal(4, rows[i] - rows[i - 1]);
        }
    }

    [Fact]
    public void Strata_HasHorizontalDarkLinesEvery4Pixels()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "strata", Palette = Gray };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 1);

        AssertStrata(img, x, y);
    }

    [Fact]
    public void StrataWall_HasTheSameLines()
    {
        var src = FreshSource();
        src.Walls["stone_wall"] = new MaterialSource { Recipe = "strata", Palette = Gray };
        var img = TileGenerator.BuildWalls(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone_wall"], mask: 15, variation: 0);

        AssertStrata(img, x, y);
    }

    [Fact]
    public void Planks_HaveDarkJointEvery4Rows_AndASeamInEachPlank()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "planks", Palette = Gray };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 2);
        var dark = Rgba.Parse(Gray[0]);

        foreach (int py in new[] { 3, 7, 11, 15 })
            for (int px = 0; px < T; px++)
                Assert.Equal(dark, img.Get(x + px, y + py));
        foreach (int py in new[] { 1, 5, 9, 13 })
            Assert.Contains(Enumerable.Range(0, T), px => img.Get(x + px, y + py) == dark);
    }

    [Fact]
    public void ClumpsWithAccent_HaveGlints()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "clumps", Palette = Gray, Accent = ["#0000ff", "#00ff00", "#ff0000"] };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 0);
        var brightest = Rgba.Parse("#ff0000");

        Assert.Contains(Enumerable.Range(0, T * T), i => img.Get(x + i % T, y + i / T) == brightest);
    }

    [Fact]
    public void PlanksWall_IsRejected()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "art", "art.json")))!;
        root["walls"]!["stone_wall"]!["recipe"] = "planks";

        var ex = Assert.Throws<DataException>(() => ArtSource.Load(root.ToJsonString(), R));

        Assert.Contains("planks", ex.Message);
    }

    [Theory]
    [InlineData("trunk", true)]
    [InlineData("cactus", true)]
    [InlineData("branch", false)]
    [InlineData("leaves", false)]
    [InlineData("grass", false)]
    [InlineData("shrub", false)]
    [InlineData("bush", false)]
    public void TreeAndDecorationShapes_HaveOpaqueAndTransparentPixels(string shape, bool crossesVertically)
    {
        var img = new Canvas(T, T);
        var pal = Gray.Select(Rgba.Parse).ToArray();

        PropGenerator.Draw(img, 0, 0, shape, pal, pal);

        int opaque = Enumerable.Range(0, T * T).Count(i => img.Get(i % T, i / T).A != 0);
        Assert.InRange(opaque, 8, T * T - 1);
        if (crossesVertically)   // tronco e cacto atravessam o tile: o de cima continua o de baixo
            for (int px = 0; px < T; px++)
                Assert.Equal(img.Get(px, 0).A != 0, img.Get(px, T - 1).A != 0);
    }
```

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.ArtGen.Tests --filter "FullyQualifiedName~TileGeneratorTests"`
Expected: FAIL (receitas e formas desconhecidas).

- [ ] **Step 3: Implemente o `ArtSource`**

Em `src/Zyon.ArtGen/ArtSource.cs`:

```csharp
    private static readonly string[] SolidRecipes = ["clumps", "panels", "ore", "plates", "stripes", "strata", "planks"];
    private static readonly string[] WallRecipes = ["clumps", "plates", "strata"];
```

No laço dos blocos, depois de `if (m.Recipe == "stripes") CheckColors(m.Accent, 3, $"{ctx}.accent");`:

```csharp
            if (m.Recipe == "clumps" && m.Accent is not null) CheckColors(m.Accent, 3, $"{ctx}.accent");   // brilhos (gelo)
```

- [ ] **Step 4: Implemente o `TileGenerator`**

Em `DrawBlock`, troque o `switch` da cor e acrescente o brilho:

```csharp
            var c = m.Recipe switch
            {
                "plates" => Plate(px, py, v, src.Seed, salt, pal),
                "stripes" => Stripe(px, py, v, src.Seed, salt, pal, accent!),
                "strata" => Strata(px, py, v, src.Seed, salt, pal),
                "planks" => Plank(px, py, v, src.Seed, salt, pal),
                _ => pal[Tone(px, py, v, src.Seed, salt)],
            };
            if (panels) c = Panel(px, py, v, src.Seed, salt, pal) ?? c;
            if (fleck is not null) c = Fleck(px, py, v, src.Seed, salt, fleck) ?? c;
            if (m.Recipe == "clumps" && accent is not null) c = Glint(px, py, v, src.Seed, salt, accent) ?? c;
```

Novos métodos (perto de `Plate`/`Stripe`):

```csharp
    /// <summary>Faixas horizontais de 4 px (dois tons alternados) com a linha escura no fim de cada uma; a borda sobe 1 px em trechos de 4 colunas.</summary>
    private static Rgba Strata(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int wave = ArtHash.Range(v * T + px / 4, 0, seed + salt + 50, 2);
        int k = py + wave + v;
        if (k % 4 == 3) return pal[0];
        return k / 4 % 2 == 0 ? pal[1] : pal[2];
    }

    /// <summary>Tábuas horizontais de 4 px: junta escura embaixo, topo claro, uma emenda vertical por tábua (desencontradas) e veios.</summary>
    private static Rgba Plank(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int row = py / 4, ly = py % 4;
        int seam = 2 + ArtHash.Range(v, row, seed + salt + 60, 12);
        if (ly == 3 || px == seam) return pal[0];
        if (ly == 0) return pal[2];
        return ArtHash.Chance(v * T + px, py, seed + salt + 61, 8) ? pal[0] : pal[1];
    }

    /// <summary>Dois brilhos em cruz pequena na cor de destaque (gelo).</summary>
    private static Rgba? Glint(int px, int py, int v, int seed, int salt, Rgba[] accent)
    {
        for (int k = 0; k < 2; k++)
        {
            int cx = 3 + ArtHash.Range(v, k, seed + salt + 70, 10);
            int cy = 3 + ArtHash.Range(v, k, seed + salt + 71, 10);
            int d = Math.Abs(px - cx) + Math.Abs(py - cy);
            if (d == 0) return accent[2];
            if (d == 1) return accent[1];
        }
        return null;
    }
```

Paredes: em `BuildWalls` troque a chamada para passar a receita:

```csharp
                DrawWall(img, (mask * src.Variations + v) * T, row * T, pal, src.Walls[name].Recipe, mask, v, src.Seed, salt: 5000 + id * 101);
```

e em `DrawWall`:

```csharp
    private static void DrawWall(Canvas img, int ox, int oy, Rgba[] pal, string recipe, int mask, int v, int seed, int salt)
    {
        bool n = (mask & AutoTile.North) == 0, e = (mask & AutoTile.East) == 0;
        bool s = (mask & AutoTile.South) == 0, w = (mask & AutoTile.West) == 0;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            var c = recipe switch
            {
                "plates" => Plate(px, py, v, seed, salt, pal),
                "strata" => Strata(px, py, v, seed, salt, pal),
                _ => pal[Tone(px, py, v, seed, salt)],
            };
            if ((n && py == 0) || (s && py == T - 1) || (w && px == 0) || (e && px == T - 1)) c = pal[0].Scale(0.8f);
            img.Set(ox + px, oy + py, c);
        }
    }
```

- [ ] **Step 5: Implemente as formas no `PropGenerator`**

```csharp
    public static readonly string[] Shapes = ["lamp", "crate", "pipe", "vent", "panel", "trunk", "branch", "leaves", "cactus", "grass", "shrub", "bush"];

    /// <summary>Formas que usam a cor de destaque (luz da luminária, tela e luzinha do painel, neve no arbusto).</summary>
    public static readonly string[] ShapesWithAccent = ["lamp", "panel", "shrub"];
```

Atualize o resumo da classe: `/// <summary>Decorativos de 1 tile (blocos não sólidos, inclusive partes de árvore): o mesmo desenho em todas as células, fundo transparente.</summary>`

Novos `case` antes do `default` (tudo dentro da célula 16×16):

```csharp
            case "trunk":   // atravessa o tile na vertical (o tronco de cima continua o de baixo), com dois nós
                Rect(5, 0, 10, 15, pal[1]);
                Rect(5, 0, 5, 15, pal[0]);
                Rect(10, 0, 10, 15, pal[0]);
                Rect(7, 0, 7, 15, pal[2]);
                Rect(8, 5, 9, 6, pal[0]);
                Rect(6, 11, 7, 12, pal[0]);
                break;
            case "branch":  // atravessa o tile na horizontal, com um graveto para cima
                Rect(0, 8, 15, 10, pal[1]);
                Rect(0, 8, 15, 8, pal[2]);
                Rect(0, 10, 15, 10, pal[0]);
                for (int i = 0; i < 5; i++) img.Set(ox + 9 + i, oy + 7 - i, pal[1]);
                break;
            case "leaves":  // agulhas de pinheiro em fiadas de 4 px com recortes: folhas vizinhas formam a copa
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int band = y % 4;
                    if (band == 0 && (x + y / 4) % 4 == 0) continue;
                    img.Set(ox + x, oy + y, band == 3 ? pal[0] : (x + y) % 5 == 0 ? pal[2] : pal[1]);
                }
                break;
            case "cactus":  // atravessa o tile na vertical: gomos com sulco escuro e espinhos claros nas bordas
                Rect(4, 0, 11, 15, pal[1]);
                Rect(4, 0, 4, 15, pal[0]);
                Rect(11, 0, 11, 15, pal[0]);
                Rect(7, 0, 7, 15, pal[0]);
                Rect(5, 0, 5, 15, pal[2]);
                Rect(9, 0, 9, 15, pal[2]);
                for (int y = 2; y < 13; y += 4)
                {
                    img.Set(ox + 3, oy + y, pal[2]);
                    img.Set(ox + 12, oy + y + 2, pal[2]);
                }
                break;
            case "grass":   // capim seco: seis lâminas finas saindo do chão, as mais altas tortas
                for (int i = 0; i < 6; i++)
                {
                    int x = 1 + i * 2 + i / 3, h = 4 + i * 5 % 7;
                    for (int y = 0; y < h; y++)
                    {
                        int bend = y > h / 2 ? (i % 2 == 0 ? 1 : -1) : 0;
                        img.Set(ox + x + bend, oy + 15 - y, y == h - 1 ? pal[2] : i % 2 == 0 ? pal[1] : pal[0]);
                    }
                }
                break;
            case "shrub":   // arbusto congelado: copa baixa e oval sobre um tronquinho, com neve em cima
                Rect(7, 12, 8, 15, pal[0]);
                for (int y = 6; y <= 12; y++)
                for (int x = 3; x <= 12; x++)
                {
                    int dx = 2 * x - 15, dy = 2 * y - 18;
                    if (dx * dx + dy * dy * 2 > 100) continue;
                    img.Set(ox + x, oy + y, (x + y) % 3 == 0 ? pal[0] : pal[1]);
                }
                Rect(5, 6, 10, 6, accent![2]);
                Rect(4, 7, 11, 7, accent[1]);
                break;
            case "bush":    // arbusto seco: cinco galhos finos em leque, sem folhas
                for (int i = 0; i < 5; i++)
                {
                    int dir = i - 2;
                    for (int s = 0; s < 8; s++)
                        img.Set(ox + 7 + dir * s / 3, oy + 15 - s, s >= 6 ? pal[2] : s % 2 == 0 ? pal[1] : pal[0]);
                }
                break;
```

- [ ] **Step 6: Rode e veja passar**

Run: `dotnet test`
Expected: PASS. (A arte commitada não muda: nenhum bloco usa as receitas novas ainda, e `DrawWall` dá os mesmos pixels para `clumps`/`plates`.)

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.ArtGen tests/Zyon.ArtGen.Tests
git commit -m "feat(artgen): receitas strata e planks, brilhos e formas de árvore e decoração" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 3: Conteúdo novo (blocos, paredes, itens e arte)

**Files:**
- Modify: `data/blocks.json`, `data/walls.json`, `data/items.json`, `art/art.json`
- Regenerate: `data/client/art/*`
- Test: `tests/Zyon.Core.Tests/DataFilesTests.cs`

- [ ] **Step 1: Ajuste o teste de dados (falha com os dados atuais)**

Em `DataFilesTests`, troque `EveryBlockDropsAnItemThatPlacesIt` por:

```csharp
    /// <summary>Folhas e decorativos não dão item; tronco e galho dão madeira; o resto dá o item que o coloca de volta.</summary>
    [Fact]
    public void EveryBlockDropsAnItemThatPlacesIt_ExceptTreesAndDecorations()
    {
        var r = TestData.Registry();
        string[] noDrop = ["pine_leaves", "dry_grass", "frozen_shrub", "dry_bush"];

        foreach (var block in r.Blocks.Skip(1))
        {
            if (noDrop.Contains(block.Name))
            {
                Assert.Equal(0, block.DropItemId);
                continue;
            }
            Assert.NotEqual(0, block.DropItemId);
            if (!block.Tree) Assert.Equal(block.Id, r.Item(block.DropItemId).PlacesBlockId);
        }
    }

    [Fact]
    public void BiomeContent_Exists()
    {
        var r = TestData.Registry();
        string[] trees = ["dead_trunk", "dead_branch", "pine_trunk", "pine_leaves", "cactus"];

        foreach (var name in trees) Assert.True(r.Block(r.BlockId(name)).Tree, name);
        foreach (var name in new[] { "snow", "ice", "sand", "sandstone", "wood" })
        {
            Assert.True(r.Block(r.BlockId(name)).Solid, name);
            Assert.False(r.Block(r.BlockId(name)).Tree, name);
        }
        Assert.Equal(r.ItemId("wood"), r.Block(r.BlockId("dead_trunk")).DropItemId);
        foreach (var wall in new[] { "snow_wall", "ice_wall", "sand_wall", "sandstone_wall" })
            Assert.True(r.TryGetWallId(wall, out _), wall);
    }
```

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~DataFilesTests"`
Expected: FAIL (`Bloco desconhecido: 'dead_trunk'`).

- [ ] **Step 3: Dados**

`data/blocks.json` — acrescente **no fim** (depois de `panel`; vírgula no `panel`):

```json
  { "id": "snow",         "hardness": 0.8, "solid": true,  "drops": "snow" },
  { "id": "ice",          "hardness": 2.0, "solid": true,  "drops": "ice" },
  { "id": "sand",         "hardness": 1.0, "solid": true,  "drops": "sand" },
  { "id": "sandstone",    "hardness": 2.0, "solid": true,  "drops": "sandstone" },
  { "id": "wood",         "hardness": 1.5, "solid": true,  "drops": "wood" },
  { "id": "dead_trunk",   "hardness": 1.5, "solid": false, "drops": "wood", "tree": true },
  { "id": "dead_branch",  "hardness": 0.5, "solid": false, "drops": "wood", "tree": true },
  { "id": "pine_trunk",   "hardness": 1.5, "solid": false, "drops": "wood", "tree": true },
  { "id": "pine_leaves",  "hardness": 0.2, "solid": false, "tree": true },
  { "id": "cactus",       "hardness": 1.0, "solid": false, "drops": "cactus", "tree": true },
  { "id": "dry_grass",    "hardness": 0.0, "solid": false },
  { "id": "frozen_shrub", "hardness": 0.0, "solid": false },
  { "id": "dry_bush",     "hardness": 0.0, "solid": false }
```

`data/walls.json` — no fim:

```json
  { "id": "snow_wall" },
  { "id": "ice_wall" },
  { "id": "sand_wall" },
  { "id": "sandstone_wall" }
```

`data/items.json` — no fim:

```json
  { "id": "snow",         "name": "Neve",            "maxStack": 999, "kind": "Block", "placesBlock": "snow" },
  { "id": "ice",          "name": "Gelo",            "maxStack": 999, "kind": "Block", "placesBlock": "ice" },
  { "id": "sand",         "name": "Areia",           "maxStack": 999, "kind": "Block", "placesBlock": "sand" },
  { "id": "sandstone",    "name": "Arenito",         "maxStack": 999, "kind": "Block", "placesBlock": "sandstone" },
  { "id": "wood",         "name": "Madeira",         "maxStack": 999, "kind": "Block", "placesBlock": "wood" },
  { "id": "cactus",       "name": "Cacto",           "maxStack": 999, "kind": "Block", "placesBlock": "cactus" }
```

- [ ] **Step 4: Arte**

`art/art.json` — em `blocks`, depois de `panel`:

```json
    "snow":         { "recipe": "clumps", "palette": ["#9aa6b4", "#c4cdd8", "#e6ecf2"] },
    "ice":          { "recipe": "clumps", "palette": ["#4f7f9e", "#76a6c4", "#a3cde3"], "accent": ["#bfe3f5", "#e2f4fc", "#ffffff"] },
    "sand":         { "recipe": "clumps", "palette": ["#a08455", "#c2a46c", "#dcc38c"] },
    "sandstone":    { "recipe": "strata", "palette": ["#7d5f3a", "#9a7a4e", "#b59664"] },
    "wood":         { "recipe": "planks", "palette": ["#4a3220", "#6e4c30", "#8e6a44"] },
    "dead_trunk":   { "recipe": "prop", "shape": "trunk",  "palette": ["#2e2620", "#4a3e34", "#62564a"] },
    "dead_branch":  { "recipe": "prop", "shape": "branch", "palette": ["#2e2620", "#4a3e34", "#62564a"] },
    "pine_trunk":   { "recipe": "prop", "shape": "trunk",  "palette": ["#2e2016", "#4a3424", "#664a34"] },
    "pine_leaves":  { "recipe": "prop", "shape": "leaves", "palette": ["#1c3326", "#2a4a36", "#3e6650"] },
    "cactus":       { "recipe": "prop", "shape": "cactus", "palette": ["#2c4a2a", "#46703e", "#6e9a5a"] },
    "dry_grass":    { "recipe": "prop", "shape": "grass",  "palette": ["#5e5034", "#857244", "#a8925c"] },
    "frozen_shrub": { "recipe": "prop", "shape": "shrub",  "palette": ["#26343a", "#3a4e56", "#56707a"], "accent": ["#c4cdd8", "#e6ecf2", "#ffffff"] },
    "dry_bush":     { "recipe": "prop", "shape": "bush",   "palette": ["#3e3022", "#5a4632", "#786048"] }
```

Em `walls`, depois de `metal_wall`:

```json
    "snow_wall":      { "recipe": "clumps", "palette": ["#4a5260", "#5c6676", "#6c7686"] },
    "ice_wall":       { "recipe": "clumps", "palette": ["#22394a", "#2c4a5e", "#365a70"] },
    "sand_wall":      { "recipe": "clumps", "palette": ["#4c3e28", "#5e4c32", "#6c583c"] },
    "sandstone_wall": { "recipe": "strata", "palette": ["#3c2e1e", "#4a3a26", "#584630"] }
```

`mergeOrder` (do mais mole ao mais duro; a madeira não mescla):

```json
  "mergeOrder": ["snow", "sand", "dirt", "ice", "sandstone", "stone", "deep_stone", "ore_a", "ore_b"],
```

Rode: `tools/gen-art.sh`
Expected: termina sem erro; `git status` mostra `data/client/art/tiles.png`, `walls.png`, `merge.png`, `items.png` e `art.json` modificados.

- [ ] **Step 5: Rode tudo**

Run: `dotnet test`
Expected: PASS. Se algum teste fixar a ordem da `mergeOrder` (ex.: linha do `dirt` no `merge.png` ser 0), atualize-o para a ordem nova — não mude a ordem.

- [ ] **Step 6: Commit**

```bash
git add data art tests/Zyon.Core.Tests/DataFilesTests.cs
git commit -m "feat(dados): blocos, paredes e itens dos biomas, com arte" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 4: Fundos em parallax na arte (`backdrop`)

**Files:**
- Create: `src/Zyon.ArtGen/BackdropGenerator.cs`
- Modify: `src/Zyon.ArtGen/ArtSource.cs`, `src/Zyon.ArtGen/ArtBuilder.cs`
- Modify: `src/Zyon.Client/ArtManifest.cs`, `src/Zyon.Client/ArtCatalog.cs`
- Modify: `art/art.json`; regenerate `data/client/art/*`
- Test: `tests/Zyon.ArtGen.Tests/BackdropGeneratorTests.cs` (novo), `tests/Zyon.Client.Tests/ArtCatalogTests.cs`

- [ ] **Step 1: Testes do gerador (falham)**

`tests/Zyon.ArtGen.Tests/BackdropGeneratorTests.cs`:

```csharp
namespace Zyon.ArtGen.Tests;

public class BackdropGeneratorTests
{
    private static readonly Rgba[] Colors = [Rgba.Parse("#203040"), Rgba.Parse("#c0d0e0")];

    public static TheoryData<string, bool> Layers => new()
    {
        { "forest", false }, { "forest", true }, { "snow", false }, { "snow", true }, { "desert", false }, { "desert", true },
    };

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_IsTransparentOnTop_OpaqueAtTheBottom_AndUsesOnlyItsTwoColors(string style, bool near)
    {
        var img = BackdropGenerator.Draw(style, near, Colors, seed: 7);

        Assert.Equal((BackdropGenerator.Width, BackdropGenerator.Height), (img.Width, img.Height));
        for (int x = 0; x < img.Width; x++)
        {
            Assert.Equal(0, img.Get(x, 0).A);
            Assert.NotEqual(0, img.Get(x, img.Height - 1).A);
        }
        for (int y = 0; y < img.Height; y++)
        for (int x = 0; x < img.Width; x++)
        {
            var c = img.Get(x, y);
            Assert.True(c.A == 0 || c == Colors[0] || c == Colors[1], $"cor inesperada em ({x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void SameInput_SamePixels(string style, bool near)
    {
        var a = BackdropGenerator.Draw(style, near, Colors, seed: 3);
        var b = BackdropGenerator.Draw(style, near, Colors, seed: 3);

        Assert.True(a.Pixels.AsSpan().SequenceEqual(b.Pixels));
    }

    [Theory]
    [InlineData(48, 110)]
    [InlineData(120, 50)]
    public void Ridge_IsPeriodic_AndContinuousAcrossTheSeam(int step, int amplitude)
    {
        for (int x = 0; x < BackdropGenerator.Width; x++)
            Assert.Equal(BackdropGenerator.RidgeHeight(x, step, amplitude, 9), BackdropGenerator.RidgeHeight(x + BackdropGenerator.Width, step, amplitude, 9));
        int last = BackdropGenerator.RidgeHeight(BackdropGenerator.Width - 1, step, amplitude, 9);
        int first = BackdropGenerator.RidgeHeight(0, step, amplitude, 9);
        Assert.InRange(Math.Abs(last - first), 0, 3 * amplitude / (2 * step) + 1);
    }

    [Theory]
    [InlineData("snow")]
    [InlineData("desert")]
    public void FarLayer_FirstAndLastColumnsMatch(string style)
    {
        var img = BackdropGenerator.Draw(style, near: false, Colors, seed: 7);
        static int Top(Canvas c, int x) { int y = 0; while (c.Get(x, y).A == 0) y++; return y; }

        Assert.InRange(Math.Abs(Top(img, 0) - Top(img, img.Width - 1)), 0, 4);
    }

    [Fact]
    public void PutWrapped_WrapsAroundTheWidth()
    {
        var img = new Canvas(BackdropGenerator.Width, BackdropGenerator.Height);

        BackdropGenerator.PutWrapped(img, -1, 5, Colors[0]);
        BackdropGenerator.PutWrapped(img, BackdropGenerator.Width, 6, Colors[1]);

        Assert.Equal(Colors[0], img.Get(BackdropGenerator.Width - 1, 5));
        Assert.Equal(Colors[1], img.Get(0, 6));
    }

    [Fact]
    public void UnknownStyle_Throws()
    {
        Assert.Throws<ArgumentException>(() => BackdropGenerator.Draw("jungle", false, Colors, 1));
    }
}
```

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.ArtGen.Tests --filter "FullyQualifiedName~BackdropGeneratorTests"`
Expected: FAIL (não compila).

- [ ] **Step 3: Manifesto (Client)**

`src/Zyon.Client/ArtManifest.cs` — depois de `public const string ArmLayer = "arm";`:

```csharp
    public const int BackgroundWidth = 480;
    public const int BackgroundHeight = 180;
```

depois de `public PlayerArt Player ...`:

```csharp
    /// <summary>Camadas de fundo (nome sem extensão), em backgrounds/&lt;nome&gt;.png, 480×180, repetíveis na horizontal.</summary>
    public List<string> Backgrounds { get; set; } = [];

    public static string BackgroundFile(string name) => $"backgrounds/{name}.png";
```

`src/Zyon.Client/ArtCatalog.cs`:
- em `ExpectedSize`, antes do `throw`:
  ```csharp
        if (m.Backgrounds.Any(b => ArtManifest.BackgroundFile(b) == file))
            return ArtImageSize.Exact(ArtManifest.BackgroundWidth, ArtManifest.BackgroundHeight);
  ```
- em `LoadFromDirectory`, depois de `files.Add(ArtManifest.PlayerFile(ArtManifest.ArmLayer));`:
  ```csharp
        files.AddRange(catalog.Manifest.Backgrounds.Select(ArtManifest.BackgroundFile));
  ```
- em `Resolve`, depois de `if (m.Player is null) throw Fail("player ausente");`:
  ```csharp
        if (m.Backgrounds is null) throw Fail("backgrounds ausente");
        var backgroundNames = new HashSet<string>();
        for (int i = 0; i < m.Backgrounds.Count; i++)
        {
            string name = m.Backgrounds[i];
            if (string.IsNullOrEmpty(name)) throw Fail($"backgrounds[{i}] ausente");
            if (!backgroundNames.Add(name)) throw Fail($"backgrounds: '{name}' repetido");
        }
  ```

- [ ] **Step 4: Testes do catálogo**

Em `ArtCatalogTests`:
- em `RealArt_PngSizesMatchTheManifest`, depois de `files.AddRange(c.Manifest.Player.Layers...)`: `files.AddRange(c.Manifest.Backgrounds.Select(ArtManifest.BackgroundFile));` e acrescente `Assert.Equal(6, c.Manifest.Backgrounds.Count);` (vale depois do Step 7).
- novo teste (use o mesmo jeito de montar um manifesto válido que os testes vizinhos usam — ex.: `ArtManifest.FromJson` do `art.json` real; leia `ArtCatalogTests` para o auxiliar existente):

```csharp
    [Fact]
    public void Backgrounds_ExpectExactSize_AndRejectDuplicates()
    {
        var m = ArtManifest.FromJson(File.ReadAllText(Path.Combine(TestData.DataDir, "client", "art", ArtManifest.FileName)));
        m.Backgrounds = ["forest_far"];
        var c = ArtCatalog.Resolve(m, TestData.Registry());

        Assert.True(c.ExpectedSize(ArtManifest.BackgroundFile("forest_far")).Matches(480, 180));
        Assert.False(c.ExpectedSize(ArtManifest.BackgroundFile("forest_far")).Matches(480, 192));

        m.Backgrounds = ["forest_far", "forest_far"];
        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry()));
        Assert.Contains("repetido", ex.Message);
    }
```

Se `NullCollections_ThrowDataException` lista as coleções do manifesto, acrescente o caso `backgrounds` com a mensagem `"backgrounds ausente"`.

- [ ] **Step 5: Gerador**

`src/Zyon.ArtGen/BackdropGenerator.cs`:

```csharp
using Zyon.Client;

namespace Zyon.ArtGen;

/// <summary>
/// Fundos em parallax (480×180, repetíveis na horizontal): silhuetas em 2 tons (forma e realce) sobre transparente.
/// Tudo é função de x módulo a largura — relevo por pontos de controle periódicos e objetos desenhados com volta —,
/// então a coluna 0 continua a última. A camada distante (near = false) e a próxima de cada estilo:
/// floresta: prédios em ruínas atrás de morros / árvores mortas; neve: montanhas com neve / morros com pinheiros;
/// deserto: dunas altas / dunas baixas com carcaças de veículos.
/// </summary>
public static class BackdropGenerator
{
    public const int Width = ArtManifest.BackgroundWidth;
    public const int Height = ArtManifest.BackgroundHeight;

    public static readonly string[] Styles = ["forest", "snow", "desert"];

    public static Canvas Draw(string style, bool near, Rgba[] colors, int seed)
    {
        int s = Array.IndexOf(Styles, style);
        if (s < 0) throw new ArgumentException($"estilo '{style}' desconhecido", nameof(style));
        var img = new Canvas(Width, Height);
        int salt = seed * 1000 + s * 10 + (near ? 1 : 0);
        Rgba shape = colors[0], light = colors[1];
        switch (style, near)
        {
            case ("forest", false):
                Buildings(img, shape, light, salt, count: 7, minHeight: 40, maxHeight: 90);
                Ridge(img, shape, light, salt, step: 60, baseY: 150, amplitude: 25, cap: 1);
                break;
            case ("forest", true):
                DeadTrees(img, shape, light, Ridge(img, shape, light, salt, step: 80, baseY: 165, amplitude: 12, cap: 1), salt, count: 8);
                break;
            case ("snow", false):
                Ridge(img, shape, light, salt, step: 48, baseY: 150, amplitude: 110, cap: 8);
                break;
            case ("snow", true):
                Pines(img, shape, light, Ridge(img, shape, light, salt, step: 80, baseY: 160, amplitude: 30, cap: 2), salt, count: 10);
                break;
            case ("desert", false):
                Ridge(img, shape, light, salt, step: 120, baseY: 150, amplitude: 50, cap: 2);
                break;
            default:
                Wrecks(img, shape, light, Ridge(img, shape, light, salt, step: 96, baseY: 165, amplitude: 20, cap: 1), salt, count: 3);
                break;
        }
        return img;
    }

    /// <summary>Altura do relevo em x (0..amplitude): pontos de controle a cada step px, periódicos na largura, ligados por smoothstep inteiro.</summary>
    public static int RidgeHeight(int x, int step, int amplitude, int salt)
    {
        int n = Width / step;
        x = Mod(x, Width);
        int i = x / step, t = x % step;
        int a = ArtHash.Range(i % n, 0, salt, amplitude + 1);
        int b = ArtHash.Range((i + 1) % n, 0, salt, amplitude + 1);
        long smooth = (long)t * t * (3 * step - 2 * t);
        return a + (int)((b - a) * smooth / ((long)step * step * step));
    }

    /// <summary>Escreve o pixel com x dado a volta na largura (y fora da imagem é ignorado).</summary>
    public static void PutWrapped(Canvas img, int x, int y, Rgba c) => img.Set(Mod(x, Width), y, c);

    private static int Mod(int x, int m) => ((x % m) + m) % m;

    /// <summary>Relevo cheio até a base da imagem; as cap primeiras linhas dos trechos altos levam o realce. Devolve o topo de cada coluna.</summary>
    private static int[] Ridge(Canvas img, Rgba shape, Rgba light, int salt, int step, int baseY, int amplitude, int cap)
    {
        var top = new int[Width];
        for (int x = 0; x < Width; x++)
        {
            int h = RidgeHeight(x, step, amplitude, salt);
            top[x] = baseY - h;
            bool capped = h * 3 >= amplitude;
            for (int y = top[x]; y < Height; y++)
                img.Set(x, y, capped && y < top[x] + cap ? light : shape);
        }
        return top;
    }

    /// <summary>Prédios em ruínas até a base: topo quebrado em trechos de 3 colunas, aresta esquerda clara e janelas vazadas.</summary>
    private static void Buildings(Canvas img, Rgba shape, Rgba light, int salt, int count, int minHeight, int maxHeight)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int w = 14 + ArtHash.Range(k, 2, salt, 20);
            int top = Height - (minHeight + ArtHash.Range(k, 3, salt, maxHeight - minHeight + 1));
            for (int dx = 0; dx < w; dx++)
            {
                int chunk = k * 64 + dx / 3;
                int columnTop = top + (ArtHash.Range(chunk, 4, salt, 100) < 35 ? ArtHash.Range(chunk, 5, salt, 8) : 0);
                for (int y = columnTop; y < Height; y++)
                {
                    int lx = dx % 6, ly = (y - top) % 8;
                    bool window = dx > 1 && dx < w - 2 && lx is 2 or 3 && ly is >= 3 and <= 5
                                  && ArtHash.Chance(k * 64 + dx / 6, (y - top) / 8, salt + 6, 40);
                    if (window) continue;
                    PutWrapped(img, x0 + dx, y, dx == 0 ? light : shape);
                }
            }
        }
    }

    /// <summary>Árvores mortas sobre o relevo: tronco de 2 px com realce e três galhos inclinados, alternando o lado.</summary>
    private static void DeadTrees(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int h = 22 + ArtHash.Range(k, 2, salt, 24);
            int ground = top[x0] + 2;
            for (int y = ground - h; y <= ground; y++)
            {
                PutWrapped(img, x0, y, y % 3 == 0 ? light : shape);
                PutWrapped(img, x0 + 1, y, shape);
            }
            for (int b = 0; b < 3; b++)
            {
                int by = ground - h + 4 + b * h / 4;
                int dir = (k + b) % 2 == 0 ? 1 : -1;
                int length = 6 + ArtHash.Range(k, 10 + b, salt, 7);
                for (int i = 0; i < length; i++)
                    PutWrapped(img, x0 + (dir > 0 ? 2 : -1) + dir * i, by - i / 2, shape);
            }
        }
    }

    /// <summary>Pinheiros sobre o relevo: triângulo serrilhado com faixas de neve do lado esquerdo e um toco embaixo.</summary>
    private static void Pines(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int h = 18 + ArtHash.Range(k, 2, salt, 18);
            int ground = top[x0] + 2;
            for (int r = 0; r < h; r++)
            {
                int y = ground - h + r;
                int half = r * 6 / h - (r % 4 == 3 ? 1 : 0);
                for (int dx = -half; dx <= half; dx++)
                    PutWrapped(img, x0 + dx, y, r % 5 == 1 && dx < 0 ? light : shape);
            }
            for (int y = ground; y <= ground + 2; y++) PutWrapped(img, x0, y, shape);
        }
    }

    /// <summary>Carcaças de veículos meio enterradas: carroceria, cabine com janela vazada, rodas e realce no teto.</summary>
    private static void Wrecks(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int ground = top[x0] + 1;
            for (int dx = 0; dx < 34; dx++)
            for (int y = ground - 9; y < ground; y++)
                PutWrapped(img, x0 + dx, y, y == ground - 9 && (dx < 8 || dx > 21) ? light : shape);
            for (int dx = 8; dx <= 21; dx++)
            for (int y = ground - 15; y <= ground - 10; y++)
            {
                bool window = dx >= 11 && dx <= 18 && y >= ground - 14 && y <= ground - 12;
                if (!window) PutWrapped(img, x0 + dx, y, y == ground - 15 ? light : shape);
            }
            foreach (int wx in new[] { 6, 27 })
                for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy <= 9) PutWrapped(img, x0 + wx + dx, ground + dy, shape);
        }
    }
}
```

- [ ] **Step 6: Fonte e construtor**

`src/Zyon.ArtGen/ArtSource.cs` — nova classe (perto de `ItemSource`):

```csharp
public sealed class BackdropSource
{
    public string Style { get; set; } = "";
    public string[]? Far { get; set; }
    public string[]? Near { get; set; }
}
```

propriedade em `ArtSource`: `public Dictionary<string, BackdropSource?> Backgrounds { get; set; } = [];`

validação (no fim de `Validate`, antes do `CheckColors(Player.Skin...)`):

```csharp
        foreach (var (name, b) in Backgrounds)
        {
            string ctx = $"backgrounds.{name}";
            if (name.Length == 0 || !name.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                throw Fail($"{ctx}: o nome deve ter só letras minúsculas, dígitos e '_'");
            if (b is null) throw Fail($"{ctx}: entrada nula");
            if (!BackdropGenerator.Styles.Contains(b.Style))
                throw Fail($"{ctx}: estilo '{b.Style}' desconhecido (use {string.Join(", ", BackdropGenerator.Styles)})");
            CheckColors(b.Far, 2, $"{ctx}.far");
            CheckColors(b.Near, 2, $"{ctx}.near");
        }
```

`src/Zyon.ArtGen/ArtBuilder.cs` — em `Build`, antes de montar o `manifest`:

```csharp
        var backgrounds = new Dictionary<string, Canvas>();   // nome da camada → imagem, na ordem do art.json
        foreach (var (name, b) in src.Backgrounds)
        {
            backgrounds[$"{name}_far"] = BackdropGenerator.Draw(b!.Style, near: false, ArtSource.Colors(b.Far!), src.Seed);
            backgrounds[$"{name}_near"] = BackdropGenerator.Draw(b.Style, near: true, ArtSource.Colors(b.Near!), src.Seed);
        }
```

no inicializador do manifesto: `Backgrounds = [.. backgrounds.Keys],`; depois do `foreach (var (layer, canvas) in layers) ...`:

```csharp
        foreach (var (name, canvas) in backgrounds) images[ArtManifest.BackgroundFile(name)] = canvas;
```

- [ ] **Step 7: Fundos no `art/art.json` e geração**

Acrescente no `art/art.json` (depois de `items`):

```json
  "backgrounds": {
    "forest": { "style": "forest", "far": ["#5d6b78", "#6c7a86"], "near": ["#3a434c", "#4a545e"] },
    "snow":   { "style": "snow",   "far": ["#7c8ea4", "#dfe8f2"], "near": ["#4c5c6e", "#c8d4e0"] },
    "desert": { "style": "desert", "far": ["#b08a64", "#c49e76"], "near": ["#8a6644", "#a07a54"] }
  }
```

Rode `tools/gen-art.sh`.
Expected: `data/client/art/backgrounds/` com `forest_far.png`, `forest_near.png`, `snow_far.png`, `snow_near.png`, `desert_far.png`, `desert_near.png`; `art.json` com a lista `backgrounds`. Abra um dos PNGs (ferramenta Read) e confira que é uma silhueta reconhecível.

- [ ] **Step 8: Rode tudo**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/Zyon.ArtGen src/Zyon.Client tests art data/client/art
git commit -m "feat(artgen): fundos em parallax por bioma" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 5: `data/biomes.json`: loader, conjunto resolvido e configuração da geração

**Files:**
- Create: `data/biomes.json`
- Create: `src/Zyon.Core/Biomes/BiomeConfig.cs`, `BiomeConfigLoader.cs`, `BiomeSet.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenConfig.cs`, `WorldGenConfigLoader.cs`, `WorldGenConfigFingerprint.cs`, `WorldGenMaterials.cs`
- Test: `tests/Zyon.Core.Tests/BiomeConfigTests.cs` (novo), `tests/Zyon.Core.Tests/FingerprintTests.cs`

- [ ] **Step 1: Dados**

`data/biomes.json`:

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

- [ ] **Step 2: Testes (falham)**

`tests/Zyon.Core.Tests/BiomeConfigTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Zyon.Core.Biomes;
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class BiomeConfigTests
{
    private static string RealJson => File.ReadAllText(Path.Combine(TestData.DataDir, BiomeConfigLoader.FileName));

    private static string Edit(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(RealJson)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject Biome(JsonObject root, string id) =>
        root["biomes"]!.AsArray().Select(n => n!.AsObject()).First(b => b["id"]!.GetValue<string>() == id);

    [Fact]
    public void RealFile_LoadsAndResolves()
    {
        var r = TestData.Registry();
        var set = BiomeSet.Resolve(BiomeConfigLoader.LoadFromDirectory(TestData.DataDir), r);

        Assert.Equal(new[] { "forest", "snow", "desert" }, set.All.Select(b => b.Id));
        Assert.Equal(new[] { 0, 1, 2 }, set.All.Select(b => b.Index));
        Assert.Equal("forest", set.Default.Id);
        Assert.Null(set.Default.Band);
        Assert.Equal(new[] { "snow", "desert" }, set.Bands.Select(b => b.Id));
        var snow = set.Get("snow");
        Assert.Equal(r.BlockId("snow"), snow.SurfaceBlock);
        Assert.Equal(r.BlockId("ice"), snow.ShallowBlock);
        Assert.Equal(r.WallId("snow_wall"), snow.SurfaceWall);
        Assert.Equal(r.WallId("ice_wall"), snow.ShallowWall);
        Assert.Equal(TreeShape.Pine, snow.Tree.Shape);
        Assert.Equal(r.BlockId("pine_leaves"), snow.Tree.Crown);
        Assert.Equal(r.BlockId("frozen_shrub"), snow.Decoration);
        Assert.Equal(new[] { r.BlockId("snow"), r.BlockId("ice") }, snow.Markers);
        Assert.Equal(0.15f, snow.Band!.WidthMin);
        Assert.Equal(6, set.EdgeNoise);
        Assert.Equal(4, set.Taper);
        Assert.Equal((60, 40, 200), (set.DetectWidth, set.DetectHeight, set.DetectThreshold));
    }

    public static TheoryData<string, string> StructuralErrors => new()
    {
        { "default-unknown", "default" },
        { "default-with-band", "band" },
        { "three-bands", "band" },
        { "one-band", "band" },
        { "unknown-shape", "shape" },
        { "height-inverted", "heightMin" },
        { "chance-out-of-range", "treeChance" },
        { "taper-zero", "taper" },
        { "band-too-wide", "widthMax" },
        { "unknown-field", "JSON" },
        { "duplicate-id", "repetido" },
        { "tree-missing", "tree" },
        { "spacing-zero", "treeSpacing" },
    };

    private static string Broken(string name) => name switch
    {
        "default-unknown" => Edit(r => r["default"] = "jungle"),
        "default-with-band" => Edit(r => Biome(r, "forest")["band"] = Biome(r, "snow")["band"]!.DeepClone()),
        "three-bands" => Edit(r =>
        {
            r["default"] = "snow";
            Biome(r, "snow").Remove("band");
            Biome(r, "forest")["band"] = Biome(r, "desert")["band"]!.DeepClone();
            var extra = Biome(r, "desert").DeepClone().AsObject();
            extra["id"] = "dunes";
            r["biomes"]!.AsArray().Add(extra);
        }),
        "one-band" => Edit(r => Biome(r, "desert").Remove("band")),
        "unknown-shape" => Edit(r => Biome(r, "snow")["tree"]!["shape"] = "palm"),
        "height-inverted" => Edit(r => Biome(r, "desert")["tree"]!["heightMin"] = 7),
        "chance-out-of-range" => Edit(r => Biome(r, "forest")["treeChance"] = 1.5),
        "taper-zero" => Edit(r => r["taper"] = 0),
        "band-too-wide" => Edit(r => Biome(r, "snow")["band"]!["widthMax"] = 0.6),
        "unknown-field" => Edit(r => r["foo"] = 1),
        "duplicate-id" => Edit(r => Biome(r, "desert")["id"] = "snow"),
        "tree-missing" => Edit(r => Biome(r, "forest").Remove("tree")),
        "spacing-zero" => Edit(r => Biome(r, "forest")["treeSpacing"] = 0),
        _ => throw new ArgumentException(name),
    };

    [Theory]
    [MemberData(nameof(StructuralErrors))]
    public void InvalidFile_ThrowsNamingFileAndField(string name, string expected)
    {
        var ex = Assert.Throws<DataException>(() => BiomeConfigLoader.Load(Broken(name)));

        Assert.Contains(BiomeConfigLoader.FileName, ex.Message);
        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("surfaceBlock", "granito")]
    [InlineData("shallowWall", "granito_wall")]
    [InlineData("decoration", "flor")]
    public void UnknownContent_ThrowsOnResolve(string field, string value)
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "snow")[field] = value));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains(field, ex.Message);
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void TrunkThatIsNotATreeBlock_ThrowsOnResolve()
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "forest")["tree"]!["trunk"] = "dirt"));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains("tree.trunk", ex.Message);
    }

    [Fact]
    public void MarkerInTwoBiomes_ThrowsOnResolve()
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "desert")["markers"]!.AsArray().Add("snow")));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains("marcador", ex.Message);
    }

    [Fact]
    public void WorldGenConfig_FromDirectory_HasBiomes_AndMaterialsResolveThem()
    {
        var config = TestData.WorldGenConfig();

        Assert.NotNull(config.Biomes);
        Assert.Equal("forest", Zyon.Core.WorldGen.WorldGenMaterials.Resolve(config, TestData.Registry()).Biomes!.Default.Id);
        Assert.Null(Zyon.Core.WorldGen.WorldGenConfigLoader.Load("{}").Biomes);
    }
}
```

Em `FingerprintTests`, novo teste:

```csharp
    [Fact]
    public void Biomes_ChangeTheFingerprint()
    {
        var a = TestData.WorldGenConfig();
        var b = TestData.WorldGenConfig();
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, Zyon.Core.Biomes.BiomeConfigLoader.FileName)))!;
        json["edgeNoise"] = 7;
        b.Biomes = Zyon.Core.Biomes.BiomeConfigLoader.Load(json.ToJsonString());

        Assert.NotEqual(WorldGenConfigFingerprint.Compute(a), WorldGenConfigFingerprint.Compute(b));
    }
```

- [ ] **Step 3: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~BiomeConfigTests|FullyQualifiedName~FingerprintTests"`
Expected: FAIL (não compila).

- [ ] **Step 4: Classes do JSON**

`src/Zyon.Core/Biomes/BiomeConfig.cs`:

```csharp
namespace Zyon.Core.Biomes;

/// <summary>data/biomes.json como escrito (nomes do Registry). Validado por <see cref="BiomeConfigLoader"/> e resolvido por <see cref="BiomeSet.Resolve"/>.</summary>
public sealed class BiomeConfig
{
    /// <summary>Bioma de tudo fora das faixas (o único sem <c>band</c>).</summary>
    public string Default { get; init; } = "";
    /// <summary>Deslocamento máximo, em colunas, das divisas das faixas (ruído).</summary>
    public int EdgeNoise { get; init; } = 6;
    /// <summary>A d linhas abaixo da superfície a divisa anda d / taper colunas para dentro da faixa.</summary>
    public int Taper { get; init; } = 4;
    public int DetectWidth { get; init; } = 60;
    public int DetectHeight { get; init; } = 40;
    /// <summary>Marcadores necessários na área de detecção para o bioma valer.</summary>
    public int DetectThreshold { get; init; } = 200;
    public List<BiomeEntry?> Biomes { get; init; } = [];
}

public sealed class BiomeEntry
{
    public string Id { get; init; } = "";
    /// <summary>Faixa de largura e distância (frações da largura do mundo); ausente = bioma padrão.</summary>
    public BandConfig? Band { get; init; }
    public string SurfaceBlock { get; init; } = "";
    public string ShallowBlock { get; init; } = "";
    public string SurfaceWall { get; init; } = "";
    public string ShallowWall { get; init; } = "";
    public TreeConfig? Tree { get; init; }
    public float TreeChance { get; init; }
    public int TreeSpacing { get; init; } = 1;
    public string Decoration { get; init; } = "";
    public float DecorationChance { get; init; }
    public List<string?> Markers { get; init; } = [];
}

public sealed class BandConfig
{
    public float WidthMin { get; init; }
    public float WidthMax { get; init; }
    public float GapMin { get; init; }
    public float GapMax { get; init; }
}

public sealed class TreeConfig
{
    /// <summary>dead, pine ou cactus (formato desenhado pelo passe trees).</summary>
    public string Shape { get; init; } = "";
    public string Trunk { get; init; } = "";
    public string Crown { get; init; } = "";
    public int HeightMin { get; init; }
    public int HeightMax { get; init; }
}
```

- [ ] **Step 5: Loader**

`src/Zyon.Core/Biomes/BiomeConfigLoader.cs`:

```csharp
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.Biomes;

public static class BiomeConfigLoader
{
    public const string FileName = "biomes.json";

    public static readonly string[] Shapes = ["dead", "pine", "cactus"];

    public static BiomeConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        return Load(File.ReadAllText(path));
    }

    public static BiomeConfig Load(string json)
    {
        BiomeConfig config;
        try
        {
            config = JsonSerializer.Deserialize<BiomeConfig>(json, RegistryLoader.Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        Validate(config);
        return config;
    }

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw Fail(message);
    }

    private static void Validate(BiomeConfig c)
    {
        Require(c.Biomes is not null, "campo 'biomes' não pode ser nulo");
        Require(c.EdgeNoise >= 0, "'edgeNoise' deve ser >= 0");
        Require(c.Taper >= 1, "'taper' deve ser >= 1");
        Require(c.DetectWidth >= 1 && c.DetectHeight >= 1, "'detectWidth' e 'detectHeight' devem ser >= 1");
        Require(c.DetectThreshold >= 1, "'detectThreshold' deve ser >= 1");

        var ids = new HashSet<string>();
        int bands = 0, defaults = 0;
        for (int i = 0; i < c.Biomes!.Count; i++)
        {
            var b = c.Biomes[i];
            Require(b is not null, $"biomes[{i}]: entrada nula");
            string ctx = $"biomes[{i}] ('{b!.Id}')";
            Require(!string.IsNullOrEmpty(b.Id) && b.Id.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'),
                $"biomes[{i}]: 'id' deve ter só letras minúsculas, dígitos e '_'");
            Require(ids.Add(b.Id), $"{ctx}: id repetido");
            if (b.Band is { } band)
            {
                bands++;
                Require(b.Id != c.Default, $"{ctx}: o bioma padrão não pode ter 'band'");
                Require(band.WidthMin > 0 && band.WidthMin <= band.WidthMax && band.WidthMax < 0.5f,
                    $"{ctx}: 'band.widthMin' e 'band.widthMax' devem satisfazer 0 < min <= max < 0,5");
                Require(band.GapMin >= 0 && band.GapMin <= band.GapMax && band.GapMax < 0.5f,
                    $"{ctx}: 'band.gapMin' e 'band.gapMax' devem satisfazer 0 <= min <= max < 0,5");
            }
            else
            {
                defaults++;
                Require(b.Id == c.Default, $"{ctx}: só o bioma padrão ('default') fica sem 'band'");
            }
            foreach (var (field, value) in new[] { ("surfaceBlock", b.SurfaceBlock), ("shallowBlock", b.ShallowBlock),
                         ("surfaceWall", b.SurfaceWall), ("shallowWall", b.ShallowWall), ("decoration", b.Decoration) })
                Require(!string.IsNullOrWhiteSpace(value), $"{ctx}: campo '{field}' ausente");
            Require(b.Tree is not null, $"{ctx}: campo 'tree' ausente");
            var t = b.Tree!;
            Require(Shapes.Contains(t.Shape), $"{ctx}: 'tree.shape' '{t.Shape}' desconhecida (use {string.Join(", ", Shapes)})");
            Require(!string.IsNullOrWhiteSpace(t.Trunk) && !string.IsNullOrWhiteSpace(t.Crown), $"{ctx}: 'tree.trunk' e 'tree.crown' são obrigatórios");
            Require(t.HeightMin >= 3 && t.HeightMin <= t.HeightMax && t.HeightMax <= 32,
                $"{ctx}: 'tree.heightMin' e 'tree.heightMax' devem satisfazer 3 <= min <= max <= 32");
            Require(b.TreeChance is >= 0 and <= 1, $"{ctx}: 'treeChance' deve estar entre 0 e 1");
            Require(b.DecorationChance is >= 0 and <= 1, $"{ctx}: 'decorationChance' deve estar entre 0 e 1");
            Require(b.TreeSpacing >= 1, $"{ctx}: 'treeSpacing' deve ser >= 1");
            Require(b.Markers is not null && b.Markers.All(m => !string.IsNullOrWhiteSpace(m)), $"{ctx}: 'markers' com entrada vazia");
        }
        Require(ids.Contains(c.Default), $"'default' referencia bioma inexistente '{c.Default}'");
        Require(defaults == 1 && bands == 2, $"precisa de exatamente um bioma sem 'band' (o padrão) e dois com 'band' (há {defaults} e {bands})");
    }
}
```

Atenção: o caso de teste `three-bands` termina com 3 faixas e o padrão sem faixa — a última verificação dá a mensagem com `band`. O caso `one-band` dá 2 biomas sem faixa: a mensagem do `else` (`só o bioma padrão ... sem 'band'`) também contém `band`.

- [ ] **Step 6: Conjunto resolvido**

`src/Zyon.Core/Biomes/BiomeSet.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Biomes;

public enum TreeShape { Dead, Pine, Cactus }

public sealed record BiomeTree(TreeShape Shape, ushort Trunk, ushort Crown, int HeightMin, int HeightMax);

public sealed record BiomeBand(float WidthMin, float WidthMax, float GapMin, float GapMax);

/// <summary>Um bioma com ids do Registry. Index = posição no biomes.json.</summary>
public sealed record Biome(
    int Index, string Id, BiomeBand? Band,
    ushort SurfaceBlock, ushort ShallowBlock, ushort SurfaceWall, ushort ShallowWall,
    BiomeTree Tree, float TreeChance, int TreeSpacing,
    ushort Decoration, float DecorationChance, IReadOnlyList<ushort> Markers);

/// <summary>Biomas de data/biomes.json resolvidos no Registry.</summary>
public sealed class BiomeSet
{
    private BiomeSet(IReadOnlyList<Biome> all, Biome @default, BiomeConfig c)
    {
        All = all;
        Default = @default;
        Bands = all.Where(b => b.Band is not null).ToList();
        EdgeNoise = c.EdgeNoise;
        Taper = c.Taper;
        DetectWidth = c.DetectWidth;
        DetectHeight = c.DetectHeight;
        DetectThreshold = c.DetectThreshold;
    }

    public IReadOnlyList<Biome> All { get; }
    public Biome Default { get; }

    /// <summary>Os dois biomas com faixa, na ordem do arquivo.</summary>
    public IReadOnlyList<Biome> Bands { get; }

    public int EdgeNoise { get; }
    public int Taper { get; }
    public int DetectWidth { get; }
    public int DetectHeight { get; }
    public int DetectThreshold { get; }

    public Biome Get(string id) => All.FirstOrDefault(b => b.Id == id) ?? throw new KeyNotFoundException($"Bioma desconhecido: '{id}'");

    public static BiomeSet Resolve(BiomeConfig config, Registry registry)
    {
        string file = BiomeConfigLoader.FileName;
        var markerOwner = new Dictionary<ushort, string>();
        var all = new List<Biome>();
        for (int i = 0; i < config.Biomes.Count; i++)
        {
            var e = config.Biomes[i]!;
            string ctx = $"{file}: biomes[{i}] ('{e.Id}')";

            ushort Block(string name, string field) =>
                registry.TryGetBlockId(name, out var id) ? id : throw new DataException($"{ctx}: '{field}' referencia bloco inexistente '{name}'");
            ushort Wall(string name, string field) =>
                registry.TryGetWallId(name, out var id) ? id : throw new DataException($"{ctx}: '{field}' referencia parede inexistente '{name}'");
            ushort TreeBlock(string name, string field)
            {
                ushort id = Block(name, field);
                return registry.Block(id).Tree ? id : throw new DataException($"{ctx}: '{field}' precisa ser um bloco de árvore (\"tree\": true), '{name}' não é");
            }

            var t = e.Tree!;
            var tree = new BiomeTree(t.Shape switch { "dead" => TreeShape.Dead, "pine" => TreeShape.Pine, _ => TreeShape.Cactus },
                TreeBlock(t.Trunk, "tree.trunk"), TreeBlock(t.Crown, "tree.crown"), t.HeightMin, t.HeightMax);

            var markers = new List<ushort>();
            foreach (var name in e.Markers)
            {
                ushort id = Block(name!, "markers");
                if (markerOwner.TryGetValue(id, out var other))
                    throw new DataException($"{ctx}: marcador '{name}' já é do bioma '{other}' (cada bloco marca um bioma só)");
                markerOwner[id] = e.Id;
                markers.Add(id);
            }

            var band = e.Band is { } b ? new BiomeBand(b.WidthMin, b.WidthMax, b.GapMin, b.GapMax) : null;
            all.Add(new Biome(i, e.Id, band,
                Block(e.SurfaceBlock, "surfaceBlock"), Block(e.ShallowBlock, "shallowBlock"),
                Wall(e.SurfaceWall, "surfaceWall"), Wall(e.ShallowWall, "shallowWall"),
                tree, e.TreeChance, e.TreeSpacing, Block(e.Decoration, "decoration"), e.DecorationChance, markers));
        }
        return new BiomeSet(all, all.First(b => b.Id == config.Default), config);
    }
}
```

- [ ] **Step 7: Configuração da geração**

`WorldGenConfig.cs` — depois da propriedade `Bunker` (precisa de `using Zyon.Core.Biomes;`):

```csharp
    /// <summary>Biomas (data/biomes.json), carregados por <see cref="WorldGenConfigLoader.LoadFromDirectory"/> (null com <c>Load(json)</c>).</summary>
    [JsonIgnore]
    public BiomeConfig? Biomes { get; internal set; }
```

`WorldGenConfigLoader.LoadFromDirectory` — depois de carregar o molde: `config.Biomes = BiomeConfigLoader.LoadFromDirectory(dataDir);` (`using Zyon.Core.Biomes;`).

`WorldGenConfigFingerprint.Compute` — depois do bloco do molde:

```csharp
        if (config.Biomes is { } biomes)
            h = Mix(h, JsonSerializer.SerializeToUtf8Bytes(biomes, RegistryLoader.Options));   // os biomas mudam o mundo gerado
```

Atualize o resumo da classe: `... do texto do molde do bunker e dos biomas.`

`WorldGenMaterials` — nova propriedade e resolução:

```csharp
    /// <summary>Biomas resolvidos no Registry; null quando a configuração veio de <c>Load(json)</c>.</summary>
    public BiomeSet? Biomes { get; init; }
```

e no `return new WorldGenMaterials(...) { Bunker = ..., Biomes = config.Biomes is null ? null : BiomeSet.Resolve(config.Biomes, registry), };`

- [ ] **Step 8: Rode e veja passar**

Run: `dotnet test`
Expected: PASS. A saída da geração não muda (nenhum passe usa os biomas ainda); só a impressão digital muda — se algum teste fixar o valor dela, atualize a constante.

- [ ] **Step 9: Commit**

```bash
git add data/biomes.json src/Zyon.Core tests/Zyon.Core.Tests
git commit -m "feat(core): biomes.json com loader, validação e biomas resolvidos" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 6: `BiomeMap` e passe `biomes`

**Files:**
- Create: `src/Zyon.Core/WorldGen/BiomeMap.cs`, `src/Zyon.Core/WorldGen/Passes/BiomesPass.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenContext.cs`, `Passes/MountainPass.cs`, `WorldGenerator.cs`
- Test: `tests/Zyon.Core.Tests/BiomesPassTests.cs` (novo), `WorldGenDeterminismTests.cs`

- [ ] **Step 1: Testes (falham)**

`tests/Zyon.Core.Tests/BiomesPassTests.cs`:

```csharp
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class BiomesPassTests
{
    private static WorldGenContext RunBiomes(int width, int height, int seed)
    {
        var ctx = Context(width, height, seed);
        foreach (var pass in new IWorldGenPass[] { new TerrainPass(), new MountainPass(), new BiomesPass() })
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    private static (int Left, int Right) Inner(WorldGenContext ctx)
    {
        var m = ctx.Mountain!;
        int center = ctx.World.Width / 2;
        return (Math.Min(center, Math.Min(m.FootX, m.GentleFootX)), Math.Max(center, Math.Max(m.FootX, m.GentleFootX)));
    }

    [Theory]
    [InlineData(4200, 1200, 1)]
    [InlineData(4200, 1200, 2)]
    [InlineData(6400, 1800, 3)]
    [InlineData(8400, 2400, 4)]
    [InlineData(1050, 300, 1)]
    [InlineData(512, 256, 7)]
    public void Bands_AreOnOppositeSides_AndTheMountainIsForest(int width, int height, int seed)
    {
        var ctx = RunBiomes(width, height, seed);
        var map = ctx.Biomes!;
        var (innerLeft, innerRight) = Inner(ctx);

        Assert.Equal(2, map.Bands.Count);
        Assert.NotEqual(map.Bands[0].Biome.Id, map.Bands[1].Biome.Id);
        Assert.True(map.Bands[0].Right <= innerLeft - BiomesPass.EdgeMargin, $"faixa esquerda termina em {map.Bands[0].Right}");
        Assert.True(map.Bands[1].Left >= innerRight + BiomesPass.EdgeMargin, $"faixa direita começa em {map.Bands[1].Left}");
        Assert.True(map.Set.EdgeNoise < BiomesPass.EdgeMargin);   // o ruído não leva a faixa até a montanha
        for (int x = innerLeft; x <= innerRight; x++)
            Assert.Same(map.Set.Default, map.At(x, ctx.Surface[x], ctx.Surface[x]));
        Assert.Contains(Enumerable.Range(0, width), x => map.At(x, ctx.Surface[x], ctx.Surface[x]) == map.Bands[0].Biome);
        Assert.Contains(Enumerable.Range(0, width), x => map.At(x, ctx.Surface[x], ctx.Surface[x]) == map.Bands[1].Biome);
    }

    [Theory]
    [InlineData(4200, 1200, 1)]
    [InlineData(6400, 1800, 2)]
    [InlineData(8400, 2400, 3)]
    [InlineData(1050, 300, 4)]
    public void BandWidths_FollowTheData(int width, int height, int seed)
    {
        var ctx = RunBiomes(width, height, seed);

        foreach (var (biome, left, right) in ctx.Biomes!.Bands)
            Assert.InRange(right - left + 1, (int)(biome.Band!.WidthMin * width) - 1, (int)MathF.Ceiling(biome.Band.WidthMax * width) + 1);
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(100, 64)]
    [InlineData(200, 100)]
    public void TinyWorlds_NeverThrow_AndKeepBandsInside(int width, int height)
    {
        var ctx = RunBiomes(width, height, 1);

        foreach (var (_, left, right) in ctx.Biomes!.Bands)
        {
            Assert.InRange(left, 0, width - 1);
            Assert.InRange(right, left + BiomesPass.MinBandWidth - 1, width - 1);
        }
    }

    [Fact]
    public void Side_VariesWithTheSeed()
    {
        var leftIds = Enumerable.Range(1, 20).Select(seed => RunBiomes(1050, 300, seed).Biomes!.Bands[0].Biome.Id).ToHashSet();

        Assert.Equal(2, leftIds.Count);
    }

    [Fact]
    public void SameSeed_SameBands()
    {
        var a = RunBiomes(1050, 300, 5).Biomes!.Bands.Select(b => (b.Biome.Id, b.Left, b.Right));
        var b = RunBiomes(1050, 300, 5).Biomes!.Bands.Select(b => (b.Biome.Id, b.Left, b.Right));

        Assert.Equal(a, b);   // por id: os registros de bioma de execuções diferentes têm listas (Markers) diferentes
    }

    [Theory]
    [InlineData(true, 2000, 420, 700, 4200, 881, 1580)]     // cabe
    [InlineData(true, 300, 50, 400, 600, 10, 250)]          // encolhe até a borda − 10
    [InlineData(true, 25, 5, 30, 64, 0, 19)]                // sobra pouco: 20 colunas encostadas na borda
    [InlineData(false, 2200, 420, 700, 4200, 2620, 3319)]
    [InlineData(false, 3000, 420, 1000, 4200, 3420, 4189)]
    [InlineData(false, 62, 6, 10, 64, 44, 63)]
    public void PlaceBand_FitsShrinksOrHugsTheBorder(bool leftSide, int inner, int gap, int bandWidth, int worldWidth, int left, int right)
    {
        Assert.Equal((left, right), BiomesPass.PlaceBand(leftSide, inner, gap, bandWidth, worldWidth));
    }

    [Fact]
    public void Taper_MovesTheEdgeIntoTheBandWithDepth()
    {
        var ctx = RunBiomes(1050, 300, 1);
        var map = ctx.Biomes!;
        var (biome, left, _) = map.Bands[0];
        int x = left + BiomesPass.EdgeMargin;           // dentro da faixa mesmo com o ruído (±EdgeNoise)
        int surface = ctx.Surface[x];
        int depth = map.Set.Taper * (BiomesPass.EdgeMargin + map.Set.EdgeNoise + 1);

        Assert.Same(biome, map.At(x, surface, surface));
        Assert.Same(map.Set.Default, map.At(x, surface + depth, surface));
    }
}
```

Em `WorldGenDeterminismTests.DefaultPasses_AreInSpecOrder`, a lista esperada passa a ser `{ "terrain", "mountain", "biomes", "layers", "caves", "ores", "walls", "bunker" }`.

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~BiomesPassTests"`
Expected: FAIL (não compila).

- [ ] **Step 3: Montanha com o pé suave**

`WorldGenContext.cs`:

```csharp
/// <summary>Montanha do passe "mountain": pico, pé da face íngreme (portão), se o portão fica à esquerda do pico e o pé do lado suave.</summary>
public sealed record MountainInfo(int PeakX, int FootX, bool FacesLeft, int GentleFootX);
```

`MountainPass.Apply` — no fim: 

```csharp
        int gentleFootX = Math.Clamp(peakX + dir * gentle, 0, width - 1);
        ctx.Mountain = new MountainInfo(peakX, footX, facesLeft, gentleFootX);
```

Corrija outros `new MountainInfo(` nos testes, se houver (`grep -rn "new MountainInfo" tests`).

- [ ] **Step 4: `BiomeMap`**

`src/Zyon.Core/WorldGen/BiomeMap.cs`:

```csharp
using Zyon.Core.Biomes;

namespace Zyon.Core.WorldGen;

/// <summary>Faixa posicionada: colunas [Left, Right] da divisa na superfície, antes do ruído.</summary>
public sealed record PlacedBand(Biome Biome, int Left, int Right);

/// <summary>
/// Bioma de cada tile durante a geração (não vai para o save). Fora das faixas vale o padrão. Cada divisa de faixa
/// é deslocada por ruído (um valor por linha, ±EdgeNoise colunas) e, a d linhas abaixo da superfície da coluna,
/// anda d / Taper colunas para dentro da faixa — o bioma afina com a profundidade. Somente leitura depois do passe
/// "biomes": <see cref="At"/> pode ser chamado de dentro de Parallel.For.
/// </summary>
public sealed class BiomeMap
{
    private readonly int height;
    private readonly List<PlacedBand> bands = [];
    private readonly List<(int[] Left, int[] Right)> offsets = [];

    public BiomeMap(BiomeSet set, int worldHeight)
    {
        Set = set;
        height = worldHeight;
    }

    public BiomeSet Set { get; }

    /// <summary>A da esquerda primeiro.</summary>
    public IReadOnlyList<PlacedBand> Bands => bands;

    /// <summary>Acrescenta uma faixa; o ruído das duas divisas é lido uma vez por linha (amostras em x = edge × 1000).</summary>
    internal void AddBand(Biome biome, int left, int right, FastNoiseLite edgeNoise)
    {
        int edge = 2 * bands.Count;
        var l = new int[height];
        var r = new int[height];
        for (int y = 0; y < height; y++)
        {
            l[y] = (int)MathF.Round(edgeNoise.GetNoise(edge * 1000f, y) * Set.EdgeNoise);
            r[y] = (int)MathF.Round(edgeNoise.GetNoise((edge + 1) * 1000f, y) * Set.EdgeNoise);
        }
        bands.Add(new PlacedBand(biome, left, right));
        offsets.Add((l, r));
    }

    /// <summary>Bioma do tile (x, y) numa coluna cuja superfície está em surfaceY.</summary>
    public Biome At(int x, int y, int surfaceY)
    {
        int row = Math.Clamp(y, 0, height - 1);
        int inward = Math.Max(0, y - surfaceY) / Set.Taper;
        for (int i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            var (l, r) = offsets[i];
            if (x >= band.Left + l[row] + inward && x <= band.Right + r[row] - inward)
                return band.Biome;
        }
        return Set.Default;
    }
}
```

(`FastNoiseLite` está no namespace usado por `WorldGenContext.CreateNoise` — confira o `namespace` em `ThirdParty/FastNoiseLite.cs` e ajuste o `using` se precisar.)

`WorldGenContext`:
- novas propriedades (depois de `DirtBottom`):

```csharp
    /// <summary>Primeira linha da rocha profunda de cada coluna (definida pelo passe de camadas).</summary>
    public int[] DeepStart { get; }

    /// <summary>Bioma de cada tile; só o padrão até o passe "biomes" rodar. Null se a configuração não tem biomas (<c>Load(json)</c>).</summary>
    public BiomeMap? Biomes { get; }
```

- no construtor: `DeepStart = new int[world.Width];` e `Biomes = materials.Biomes is { } set ? new BiomeMap(set, world.Height) : null;`

- [ ] **Step 5: Passe `biomes`**

`src/Zyon.Core/WorldGen/Passes/BiomesPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Põe as duas faixas de bioma (neve e deserto, nos dados) em lados opostos do centro — o Random do passe sorteia o
/// lado —, cada uma a uma distância da montanha (ou do centro) e com largura sorteadas entre os limites dos dados,
/// em frações da largura do mundo. A montanha e o bunker ficam sempre no bioma padrão.
/// </summary>
internal sealed class BiomesPass : IWorldGenPass
{
    /// <summary>Colunas livres entre a faixa e a montanha e entre a faixa e a borda do mundo.</summary>
    public const int EdgeMargin = 10;

    /// <summary>Largura mínima de uma faixa; com menos espaço ela encosta na borda do mundo.</summary>
    public const int MinBandWidth = 20;

    public const float EdgeNoiseFrequency = 0.04f;

    public string Name => "biomes";

    public void Apply(WorldGenContext ctx)
    {
        var map = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        int width = ctx.World.Width, center = width / 2;
        int innerLeft = center, innerRight = center;
        if (ctx.Mountain is { } m)
        {
            innerLeft = Math.Min(center, Math.Min(m.FootX, m.GentleFootX));
            innerRight = Math.Max(center, Math.Max(m.FootX, m.GentleFootX));
        }

        var rng = ctx.Random;
        var bands = map.Set.Bands;
        int leftIndex = rng.Next(2);
        var noise = ctx.CreateNoise("biomes.edge", EdgeNoiseFrequency, 1);
        for (int side = 0; side < 2; side++)
        {
            var biome = bands[side == 0 ? leftIndex : 1 - leftIndex];
            var band = biome.Band!;
            int gap = (int)MathF.Round(width * Lerp(band.GapMin, band.GapMax, (float)rng.NextDouble()));
            int bandWidth = (int)MathF.Round(width * Lerp(band.WidthMin, band.WidthMax, (float)rng.NextDouble()));
            var (left, right) = side == 0
                ? PlaceBand(leftSide: true, innerLeft, gap, bandWidth, width)
                : PlaceBand(leftSide: false, innerRight, gap, bandWidth, width);
            map.AddBand(biome, left, right, noise);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Colunas [left, right] de uma faixa: começa a gap colunas da borda interna (montanha ou centro) e tem bandWidth
    /// colunas; encolhe para ficar a EdgeMargin da montanha e da borda; se sobrar menos de MinBandWidth, fica com
    /// MinBandWidth colunas encostada na borda do mundo.
    /// </summary>
    internal static (int Left, int Right) PlaceBand(bool leftSide, int inner, int gap, int bandWidth, int worldWidth)
    {
        if (leftSide)
        {
            int right = Math.Min(inner - gap, inner - EdgeMargin);
            int left = Math.Max(right - bandWidth + 1, EdgeMargin);
            return right - left + 1 >= MinBandWidth ? (left, right) : (0, MinBandWidth - 1);
        }
        else
        {
            int left = Math.Max(inner + gap, inner + EdgeMargin);
            int right = Math.Min(left + bandWidth - 1, worldWidth - 1 - EdgeMargin);
            return right - left + 1 >= MinBandWidth ? (left, right) : (worldWidth - MinBandWidth, worldWidth - 1);
        }
    }
}
```

Verificação rápida dos exemplos do teste: `(true, 2000, 420, 700, 4200)` → right = min(1580, 1990) = 1580, left = max(881, 10) = 881 ✓. `(false, 3000, 420, 1000, 4200)` → left = 3420, right = min(4419, 4189) = 4189 ✓.

- [ ] **Step 6: Ordem dos passes**

`WorldGenerator.DefaultPasses()`: insira `new BiomesPass(),` depois de `new MountainPass(),`. Atualize o comentário do construtor padrão (`terreno, montanha, biomas, camadas, ...`).

- [ ] **Step 7: Rode tudo**

Run: `dotnet test`
Expected: PASS. Os `GoldenHash_*` **não** mudam (o passe não escreve tiles; o Random e o ruído dos outros passes são semeados pelos próprios nomes). Se algum teste com `WorldGenConfigLoader.Load(json)` passar a falhar com "biomas não carregados", aplique a convenção do topo do plano.

- [ ] **Step 8: Commit**

```bash
git add src/Zyon.Core tests/Zyon.Core.Tests
git commit -m "feat(worldgen): passe biomes com faixas de neve e deserto" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 7: Camadas, minérios e paredes por bioma (`Version` 3)

**Files:**
- Modify: `src/Zyon.Core/WorldGen/Passes/LayersPass.cs`, `OrePass.cs`, `WallsPass.cs`, `WorldGenerator.cs`
- Test: `tests/Zyon.Core.Tests/WorldGenPassTests.cs`, `WorldGenDeterminismTests.cs`

- [ ] **Step 1: Atualize os testes de camadas, paredes e minérios (falham)**

Em `WorldGenPassTests`, troque `Layers_DirtOnTop_StoneBelow_DeepStoneAtBottom` por:

```csharp
    [Fact]
    public void Layers_FollowTheBiomeOfEachTile_AndDeepStoneBelow()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass());
        var m = ctx.Materials;
        var map = ctx.Biomes!;

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x], d = ctx.DirtBottom[x], deep = ctx.DeepStart[x];
            Assert.True(d > s, $"coluna {x}: DirtBottom {d} <= superfície {s}");
            Assert.True(deep > d, $"coluna {x}: DeepStart {deep} <= DirtBottom {d}");
            for (int y = s; y < Height; y++)
            {
                var b = map.At(x, y, s);
                ushort expected = y >= deep ? m.DeepStone : y < d ? b.SurfaceBlock : b.ShallowBlock;
                Assert.Equal(expected, ctx.World.GetBlock(x, y));
            }
        }
    }

    [Fact]
    public void Layers_HaveTheBlocksOfAllThreeBiomes()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass());
        var r = ctx.Registry;
        var present = new HashSet<ushort>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            present.Add(ctx.World.GetBlock(x, y));

        foreach (var name in new[] { "dirt", "stone", "snow", "ice", "sand", "sandstone", "deep_stone" })
            Assert.Contains(r.BlockId(name), present);
    }
```

Troque `Ores_NeverReplaceDirtOrAir` por:

```csharp
    [Fact]
    public void Ores_NeverReplaceTheSurfaceLayerOrAir()
    {
        var before = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass());
        var after = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass(), new OrePass());
        var surfaceBlocks = after.Biomes!.Set.All.Select(b => b.SurfaceBlock).ToHashSet();

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            ushort old = before.World.GetBlock(x, y);
            if (old == 0 || surfaceBlocks.Contains(old))
                Assert.Equal(old, after.World.GetBlock(x, y));
        }
    }

    [Fact]
    public void Ores_AlsoReplaceIceAndSandstone()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")))!;
        root["ores"] = System.Text.Json.Nodes.JsonNode.Parse(
            """[{"block":"ore_a","minDepth":0.3,"maxDepth":0.65,"veinsPerMillionTiles":3000,"veinSizeMin":10,"veinSizeMax":14}]""");
        var config = WorldGenConfigLoader.Load(root.ToJsonString());
        var loaded = TestData.WorldGenConfig();
        config.Biomes = loaded.Biomes;
        IWorldGenPass[] layers = [new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass()];

        var before = RunWith(config, layers);
        var after = RunWith(config, [.. layers, new OrePass()]);
        var r = after.Registry;
        ushort ore = r.BlockId("ore_a");
        var replaced = new HashSet<ushort>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (after.World.GetBlock(x, y) == ore && before.World.GetBlock(x, y) != ore)
                replaced.Add(before.World.GetBlock(x, y));

        Assert.Contains(r.BlockId("ice"), replaced);
        Assert.Contains(r.BlockId("sandstone"), replaced);
    }
```

Troque `Walls_FillBelowSurface_IncludingCaves_AndNothingAbove` por:

```csharp
    [Fact]
    public void Walls_FollowTheBiome_BelowTheSurface_AndNothingAbove()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass(), new WallsPass());
        var m = ctx.Materials;
        var map = ctx.Biomes!;

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            ushort wall = ctx.World.GetWall(x, y);
            int s = ctx.Surface[x];
            if (y <= s)
            {
                Assert.Equal(0, wall);
                continue;
            }
            var b = map.At(x, y, s);
            ushort expected = y >= ctx.DeepStart[x] ? m.StoneWall : y < ctx.DirtBottom[x] ? b.SurfaceWall : b.ShallowWall;
            Assert.Equal(expected, wall);
        }
    }
```

No `Ores_StayInsideANarrowBand` (usa `Load(json)` e roda `LayersPass`), acrescente `config.Biomes = TestData.WorldGenConfig().Biomes;` depois do `Load`. Revise os outros testes de `WorldGenPassTests` que rodam `LayersPass` sem `BiomesPass`: continuam válidos (sem o passe `biomes`, o mapa só tem o padrão, a floresta: terra/rocha como antes).

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL (`DeepStart` vazio, blocos ainda de terra/rocha).

- [ ] **Step 3: `LayersPass`**

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Camada de cima e rocha rasa com os blocos do bioma de cada tile (BiomeMap: afina com a profundidade), rocha profunda
/// embaixo (igual em todo lugar), com fronteiras irregulares por ruído.
/// </summary>
internal sealed class LayersPass : IWorldGenPass
{
    public string Name => "layers";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        var m = ctx.Materials;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        int height = world.Height;
        var dirtNoise = ctx.CreateNoise("layers.dirt", cfg.LayerNoiseFrequency, 2);
        var deepNoise = ctx.CreateNoise("layers.deep", cfg.LayerNoiseFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = surface + cfg.DirtDepth + (int)MathF.Round(dirtNoise.GetNoise(x, 0f) * cfg.DirtDepthVariation);
            dirtBottom = Math.Clamp(dirtBottom, surface + 1, height);
            int deepStart = (int)MathF.Round(cfg.DeepLayerStart * height + deepNoise.GetNoise(x, 0f) * cfg.DeepLayerVariation);
            deepStart = Math.Max(deepStart, dirtBottom + 1);
            ctx.DirtBottom[x] = dirtBottom;
            ctx.DeepStart[x] = deepStart;

            for (int y = surface; y < height; y++)
            {
                ushort block;
                if (y >= deepStart)
                    block = m.DeepStone;
                else
                {
                    var biome = biomes.At(x, y, surface);
                    block = y < dirtBottom ? biome.SurfaceBlock : biome.ShallowBlock;
                }
                world.SetBlock(x, y, block);
            }
        });
    }
}
```

- [ ] **Step 4: `OrePass`**

Atualize o resumo (`... só substituindo rocha rasa (de qualquer bioma) e rocha profunda ...`) e, no começo de `Apply`:

```csharp
        var replaceable = new bool[ctx.Registry.Blocks.Count];
        replaceable[m.Stone] = replaceable[m.DeepStone] = true;
        if (ctx.Biomes is { } biomes)
            foreach (var biome in biomes.Set.All)
                replaceable[biome.ShallowBlock] = true;
```

e troque a condição `if (current == m.Stone || current == m.DeepStone)` por `if (replaceable[current])`.

- [ ] **Step 5: `WallsPass`**

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Paredes de fundo em todo tile abaixo da superfície (inclusive dentro das cavernas): as do bioma do tile na camada
/// de cima e na rocha rasa, rocha na rocha profunda.
/// </summary>
internal sealed class WallsPass : IWorldGenPass
{
    public string Name => "walls";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = ctx.DirtBottom[x];
            int deepStart = ctx.DeepStart[x];
            for (int y = surface + 1; y < world.Height; y++)
            {
                ushort wall;
                if (y >= deepStart)
                    wall = m.StoneWall;
                else
                {
                    var biome = biomes.At(x, y, surface);
                    wall = y < dirtBottom ? biome.SurfaceWall : biome.ShallowWall;
                }
                world.SetWall(x, y, wall);
            }
        });
    }
}
```

Nota: os testes de paredes que rodam `WallsPass` sem `LayersPass` (se houver) teriam `DeepStart` = 0 e só pedra; rode a suíte e ajuste o teste para rodar `LayersPass` antes, se algum depender disso.

- [ ] **Step 6: Versão e hash de referência**

`WorldGenerator.Version = 3;` Rode `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~GoldenHash"`; ele falha mostrando o hash novo (e talvez o spawn). Atualize `GoldenHash` (e `GoldenSpawnFeet`, se mudou) em `WorldGenDeterminismTests` com os valores mostrados. Atenção: confira antes que **só** essa falha mudou — `SameSeed_ProducesIdenticalWorld` tem de passar (determinismo).

- [ ] **Step 7: Rode tudo, inclusive o desempenho da geração**

Run: `dotnet test`
Expected: PASS.

Run: `dotnet test -c Release --filter Category=Performance`
Expected: PASS (a consulta ao `BiomeMap` por tile é barata; se o teste da geração estourar o limite, reporte o tempo medido em vez de mexer no limite).

- [ ] **Step 8: Commit**

```bash
git add src/Zyon.Core tests/Zyon.Core.Tests
git commit -m "feat(worldgen): camadas, paredes e minérios por bioma (versão 3)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 8: Passe `trees` (árvores e decoração)

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/TreeShapes.cs`, `src/Zyon.Core/WorldGen/Passes/TreesPass.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenerator.cs`
- Test: `tests/Zyon.Core.Tests/TreesPassTests.cs` (novo), `WorldGenDeterminismTests.cs`

- [ ] **Step 1: Testes (falham)**

`tests/Zyon.Core.Tests/TreesPassTests.cs`:

```csharp
using Zyon.Core.Biomes;
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class TreesPassTests
{
    private static WorldGenContext Generate(int seed)
    {
        var ctx = Context(1050, 300, seed);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    private static BiomeTree TreeOf(TreeShape shape) =>
        BiomeSet.Resolve(TestData.WorldGenConfig().Biomes!, TestData.Registry()).All.First(b => b.Tree.Shape == shape).Tree;

    /// <summary>Raízes: tronco do bioma da coluna logo acima da superfície.</summary>
    private static List<int> Roots(WorldGenContext ctx)
    {
        var roots = new List<int>();
        for (int x = 0; x < ctx.World.Width; x++)
        {
            int s = ctx.Surface[x];
            if (s > 0 && ctx.World.GetBlock(x, s - 1) == ctx.Biomes!.At(x, s, s).Tree.Trunk) roots.Add(x);
        }
        return roots;
    }

    private static (int From, int To) Excluded(WorldGenContext ctx)
    {
        var b = ctx.Bunker!;
        return (b.X - BunkerPass.PlateauWidth - TreesPass.BunkerClearance,
                b.X + b.Structure.Width - 1 + BunkerPass.PlateauWidth + TreesPass.BunkerClearance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryBiomeGetsTrees_OnItsSurfaceBlock_WithTheDataHeights(int seed)
    {
        var ctx = Generate(seed);
        var roots = Roots(ctx);

        foreach (var biome in ctx.Biomes!.Set.All)
            Assert.Contains(roots, x => ctx.Biomes.At(x, ctx.Surface[x], ctx.Surface[x]) == biome);
        foreach (int x in roots)
        {
            int s = ctx.Surface[x];
            var biome = ctx.Biomes.At(x, s, s);
            Assert.Equal(biome.SurfaceBlock, ctx.World.GetBlock(x, s));
            int trunk = 0;
            while (ctx.World.GetBlock(x, s - 1 - trunk) == biome.Tree.Trunk) trunk++;
            if (biome.Tree.Shape != TreeShape.Cactus)   // no cacto o braço usa o mesmo bloco e pode emendar no topo
                Assert.InRange(trunk, biome.Tree.HeightMin, biome.Tree.HeightMax);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Trees_RespectSpacing_AndStayAwayFromTheBunker(int seed)
    {
        var ctx = Generate(seed);
        var roots = Roots(ctx);
        var (from, to) = Excluded(ctx);

        for (int i = 1; i < roots.Count; i++)
        {
            int x = roots[i];
            var biome = ctx.Biomes!.At(x, ctx.Surface[x], ctx.Surface[x]);
            Assert.True(x - roots[i - 1] >= biome.TreeSpacing, $"árvores em {roots[i - 1]} e {x}");
        }
        for (int x = Math.Max(0, from); x <= Math.Min(ctx.World.Width - 1, to); x++)
        for (int y = 0; y < ctx.World.Height; y++)
        {
            var def = ctx.Registry.Block(ctx.World.GetBlock(x, y));
            Assert.False(def.Tree, $"árvore em ({x}, {y}) perto do bunker");
            Assert.DoesNotContain(def.Id, ctx.Biomes!.Set.All.Select(b => b.Decoration));
        }
    }

    [Fact]
    public void EveryTreeBlock_IsConnectedToARoot()
    {
        var ctx = Generate(1);
        var world = ctx.World;
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();
        foreach (int x in Roots(ctx))
        {
            queue.Enqueue((x, ctx.Surface[x] - 1));
            seen.Add((x, ctx.Surface[x] - 1));
        }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (ctx.Registry.Block(world.GetBlock(nx, ny)).Tree && seen.Add((nx, ny)))
                    queue.Enqueue((nx, ny));
        }

        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
            if (ctx.Registry.Block(world.GetBlock(x, y)).Tree)
                Assert.Contains((x, y), seen);
    }

    [Fact]
    public void Decorations_SitOnTheSurfaceBlockOfTheirBiome()
    {
        var ctx = Generate(2);
        int found = 0;
        for (int x = 0; x < ctx.World.Width; x++)
        {
            int s = ctx.Surface[x];
            var biome = ctx.Biomes!.At(x, s, s);
            if (s == 0 || ctx.World.GetBlock(x, s - 1) != biome.Decoration) continue;
            found++;
            Assert.Equal(biome.SurfaceBlock, ctx.World.GetBlock(x, s));
        }
        Assert.True(found > 10, $"só {found} decorativos");
    }

    [Fact]
    public void Pine_CrownNarrowsFrom5To1_AboveTheTrunk()
    {
        var tree = TreeOf(TreeShape.Pine);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();

        TreeShapes.Build(tree, new GenRandom(4), tiles);

        int h = tiles.Count(t => t.Block == tree.Trunk);
        var crownRows = tiles.Where(t => t.Block == tree.Crown).GroupBy(t => t.Dy).OrderByDescending(g => g.Key).ToList();
        int Width(IGrouping<int, (int Dx, int Dy, ushort Block)> row) => row.Max(t => t.Dx) - row.Min(t => t.Dx) + 1;
        Assert.Equal(5, Width(crownRows.First()));
        Assert.Equal(1, Width(crownRows.Last()));
        Assert.Equal(-h, crownRows.Last().Key);   // ponta um tile acima do tronco
        for (int i = 1; i < crownRows.Count; i++) Assert.True(Width(crownRows[i]) <= Width(crownRows[i - 1]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Dead_Has2To4BranchesInTheUpperHalf_AndCactusUpTo2Arms(int seed)
    {
        var dead = TreeOf(TreeShape.Dead);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();
        TreeShapes.Build(dead, new GenRandom(seed), tiles);
        int h = tiles.Count(t => t.Block == dead.Trunk);
        var branchRoots = tiles.Where(t => t.Block == dead.Crown && Math.Abs(t.Dx) == 1).ToList();
        Assert.InRange(branchRoots.Count, 1, 4);   // dois galhos no mesmo lado e linha se sobrepõem
        Assert.All(branchRoots, t => Assert.True(-t.Dy >= h / 2 && -t.Dy < h));
        Assert.Contains(branchRoots, t => t.Dx < 0);
        Assert.Contains(branchRoots, t => t.Dx > 0);

        var cactus = TreeOf(TreeShape.Cactus);
        TreeShapes.Build(cactus, new GenRandom(seed), tiles);
        var sides = tiles.Where(t => t.Dx != 0).Select(t => Math.Sign(t.Dx)).Distinct().ToList();
        Assert.True(sides.Count <= 2);
        Assert.All(tiles, t => Assert.InRange(Math.Abs(t.Dx), 0, 1));
    }
}
```

Em `WorldGenDeterminismTests.DefaultPasses_AreInSpecOrder`: `{ "terrain", "mountain", "biomes", "layers", "caves", "ores", "walls", "bunker", "trees" }`.

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~TreesPassTests"`
Expected: FAIL (não compila).

- [ ] **Step 3: Formatos**

`src/Zyon.Core/WorldGen/Passes/TreeShapes.cs`:

```csharp
using Zyon.Core.Biomes;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Tiles de uma árvore a partir da base: (dx, dy, bloco), dy negativo para cima, (0, 0) = tile logo acima do chão.
/// Pode repetir um tile (dois galhos iguais); quem planta confere o espaço livre antes.
/// </summary>
internal static class TreeShapes
{
    public static void Build(BiomeTree tree, GenRandom rng, List<(int Dx, int Dy, ushort Block)> tiles)
    {
        tiles.Clear();
        int h = rng.Next(tree.HeightMin, tree.HeightMax + 1);
        for (int i = 0; i < h; i++) tiles.Add((0, -i, tree.Trunk));
        switch (tree.Shape)
        {
            case TreeShape.Dead: DeadBranches(tree, h, rng, tiles); break;
            case TreeShape.Pine: PineCrown(tree, h, tiles); break;
            default: CactusArms(tree, h, rng, tiles); break;
        }
    }

    /// <summary>2 a 4 galhos de 1–2 tiles, alternando o lado, na metade de cima do tronco.</summary>
    private static void DeadBranches(BiomeTree tree, int h, GenRandom rng, List<(int, int, ushort)> tiles)
    {
        int count = rng.Next(2, 5);
        int side = rng.Next(2) == 0 ? -1 : 1;
        int lowest = h / 2;
        for (int i = 0; i < count; i++, side = -side)
        {
            int row = lowest + rng.Next(h - lowest);
            int length = rng.Next(1, 3);
            for (int k = 1; k <= length; k++) tiles.Add((side * k, -row, tree.Crown));
        }
    }

    /// <summary>Copa triangular nos 2/3 de cima do tronco: 5 tiles de largura embaixo até 1 na ponta, um tile acima do tronco.</summary>
    private static void PineCrown(BiomeTree tree, int h, List<(int, int, ushort)> tiles)
    {
        int start = h / 3, top = h, span = top - start;
        for (int row = start; row <= top; row++)
        {
            int half = (2 * (top - row) + span / 2) / span;   // 2 embaixo → 0 na ponta
            for (int dx = -half; dx <= half; dx++)
                if (dx != 0 || row == top) tiles.Add((dx, -row, tree.Crown));
        }
    }

    /// <summary>Até 2 braços em lados diferentes: 1 tile para o lado e 1–2 para cima, acima do chão e abaixo do topo.</summary>
    private static void CactusArms(BiomeTree tree, int h, GenRandom rng, List<(int, int, ushort)> tiles)
    {
        int arms = rng.Next(3);
        int side = rng.Next(2) == 0 ? -1 : 1;
        for (int i = 0; i < arms; i++, side = -side)
        {
            int row = 1 + rng.Next(h - 2);
            int up = rng.Next(1, 3);
            for (int k = 0; k <= up; k++) tiles.Add((side, -row - k, tree.Crown));
        }
    }
}
```

(Os testes do pinheiro: na linha `start`, `half` = (2·span + span/2) / span = 2 → largura 5; na ponta, `half` = (span/2)/span = 0 → largura 1. `heightMin` do pinheiro é 8, então `span` ≥ 6.)

- [ ] **Step 4: Passe**

`src/Zyon.Core/WorldGen/Passes/TreesPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Árvores e decoração sobre a superfície, depois do bunker. Da esquerda para a direita, em cada coluna cuja
/// superfície é o bloco de cima do bioma: com a chance do bioma e a treeSpacing colunas da última árvore, planta a
/// árvore do bioma se todos os tiles dela estiverem livres (ar, dentro do mundo, fora da área do bunker); senão,
/// com a chance de decoração, põe o decorativo sobre o chão. A área do bunker (com o teto e o patamar, mais
/// BunkerClearance colunas de folga) fica sem nada.
/// </summary>
internal sealed class TreesPass : IWorldGenPass
{
    public const int BunkerClearance = 2;

    private readonly List<(int Dx, int Dy, ushort Block)> shape = [];

    public string Name => "trees";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        var rng = ctx.Random;
        int skipFrom = int.MaxValue, skipTo = int.MinValue;
        if (ctx.Bunker is { } bunker)
        {
            skipFrom = bunker.X - BunkerPass.PlateauWidth - BunkerClearance;
            skipTo = bunker.X + bunker.Structure.Width - 1 + BunkerPass.PlateauWidth + BunkerClearance;
        }

        int lastTree = int.MinValue / 2;
        for (int x = 0; x < world.Width; x++)
        {
            if (x >= skipFrom && x <= skipTo) continue;
            int y = ctx.Surface[x];
            if (y == 0) continue;
            var biome = biomes.At(x, y, y);
            if (world.GetBlock(x, y) != biome.SurfaceBlock) continue;

            if (x - lastTree >= biome.TreeSpacing && rng.NextDouble() < biome.TreeChance)
            {
                TreeShapes.Build(biome.Tree, rng, shape);
                if (Fits(world, x, y - 1, skipFrom, skipTo))
                {
                    foreach (var (dx, dy, block) in shape) world.SetBlock(x + dx, y - 1 + dy, block);
                    lastTree = x;
                    continue;
                }
            }
            if (rng.NextDouble() < biome.DecorationChance && world.GetBlock(x, y - 1) == 0)
                world.SetBlock(x, y - 1, biome.Decoration);
        }
    }

    private bool Fits(Tiles.TileWorld world, int baseX, int baseY, int skipFrom, int skipTo)
    {
        foreach (var (dx, dy, _) in shape)
        {
            int tx = baseX + dx, ty = baseY + dy;
            if (!world.InBounds(tx, ty) || world.GetBlock(tx, ty) != 0 || (tx >= skipFrom && tx <= skipTo)) return false;
        }
        return true;
    }
}
```

(Ajuste o `using`/qualificação de `TileWorld` conforme o arquivo — `Zyon.Core.Tiles.TileWorld`.)

`WorldGenerator.DefaultPasses()`: `new TreesPass(),` depois de `new BunkerPass(),`.

- [ ] **Step 5: Hash de referência**

Rode os `GoldenHash_*`, confira que `SameSeed_ProducesIdenticalWorld` passa e atualize as constantes. `Version` continua 3 (a versão 3 cobre todo o plano, ainda não publicado).

- [ ] **Step 6: Rode tudo**

Run: `dotnet test`
Expected: PASS. Se `BunkerPassTests`/`WorldGenScenarioTests` reclamarem de algo acima da superfície (árvore ou decorativo), confira se o teste assume "só ar acima da superfície"; troque a verificação para "nenhum bloco **sólido**" — não remova árvores.

Run: `dotnet test -c Release --filter Category=Performance`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.Core tests/Zyon.Core.Tests
git commit -m "feat(worldgen): passe trees com árvores e decoração por bioma" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 9: Derrubar árvores

**Files:**
- Create: `src/Zyon.Core/Sim/Systems/TreeFelling.cs`
- Modify: `src/Zyon.Core/Sim/Systems/ToolActionSystem.cs`
- Test: `tests/Zyon.Core.Tests/TreeFellingTests.cs` (novo)

- [ ] **Step 1: Testes (falham)**

`tests/Zyon.Core.Tests/TreeFellingTests.cs`:

```csharp
using Zyon.Core.Entities;
using Zyon.Core.Sim;
using Zyon.Core.Sim.Systems;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class TreeFellingTests
{
    private static PlayerInput Use(int x, int y) => new(0f, false, true, x, y);

    private static int TicksToMine(SimHarness h, string block)
    {
        var r = h.Registry;
        var pick = r.Item(r.ItemId("basic_pickaxe"));
        float perSecond = pick.ToolPower * pick.ToolSpeed * r.Physics.MiningRate / r.Block(r.BlockId(block)).Hardness;
        return (int)MathF.Ceiling(Simulation.TicksPerSecond / perSecond) + 2;
    }

    /// <summary>Árvore morta em x=22 sobre o chão (y=40): tronco y=39..34, galhos (23,35) e (21,36), (20,36).</summary>
    private static void PlantDeadTree(SimHarness h)
    {
        ushort trunk = h.Registry.BlockId("dead_trunk"), branch = h.Registry.BlockId("dead_branch");
        for (int y = 34; y <= 39; y++) h.Tiles.SetBlock(22, y, trunk);
        h.Tiles.SetBlock(23, 35, branch);
        h.Tiles.SetBlock(21, 36, branch);
        h.Tiles.SetBlock(20, 36, branch);
    }

    [Fact]
    public void BreakingTheTrunk_FellsEverythingAbove_LeavesTheStump_AndDropsWood()
    {
        using var h = new SimHarness();
        PlantDeadTree(h);
        h.Idle(5);
        h.Sim.DrainEvents();

        h.Run(TicksToMine(h, "dead_trunk"), Use(22, 38));

        Assert.Equal(0, h.Tiles.GetBlock(22, 38));
        foreach (var (x, y) in new[] { (22, 37), (22, 36), (22, 35), (22, 34), (23, 35), (21, 36), (20, 36) })
            Assert.Equal(0, h.Tiles.GetBlock(x, y));
        Assert.Equal(h.Registry.BlockId("dead_trunk"), h.Tiles.GetBlock(22, 39));   // toco
        var events = h.Sim.DrainEvents();
        Assert.Equal(8, events.OfType<TileChanged>().Count(e => e.Block == 0));
        Assert.Equal(8, events.OfType<EntitySpawned>().Count(e => e.Kind == EntityKind.DroppedItem));
    }

    [Fact]
    public void BreakingTheGroundUnderATree_DoesNotFellIt()
    {
        using var h = new SimHarness();
        PlantDeadTree(h);
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dead_trunk"));   // raiz vizinha na linha de baixo

        h.Run(TicksToMine(h, "dirt") + 5, Use(22, 40));

        Assert.Equal(0, h.Tiles.GetBlock(22, 40));
        Assert.Equal(h.Registry.BlockId("dead_trunk"), h.Tiles.GetBlock(22, 39));
        Assert.Equal(h.Registry.BlockId("dead_trunk"), h.Tiles.GetBlock(23, 39));
    }

    [Fact]
    public void Collect_TakesTheSameRowAndAbove_NotBelow()
    {
        var r = TestData.Registry();
        var world = new TileWorld(32, 32);
        ushort trunk = r.BlockId("pine_trunk");
        for (int y = 10; y <= 20; y++) world.SetBlock(10, y, trunk);
        world.SetBlock(11, 15, trunk);    // mesma linha do corte: vai junto
        world.SetBlock(12, 16, trunk);    // ligado só por baixo: fica
        world.SetBlock(12, 15, r.BlockId("dirt"));
        world.SetBlock(10, 15, 0);        // o tile cortado já foi quebrado
        var result = new List<(int X, int Y)>();

        TreeFelling.Collect(world, r, 10, 15, result);

        Assert.Contains((11, 15), result);
        Assert.Contains((10, 10), result);
        Assert.DoesNotContain((10, 16), result);
        Assert.DoesNotContain((12, 16), result);
        Assert.Equal(6, result.Count);   // (10,14..10) + (11,15)
    }

    [Fact]
    public void Collect_StopsAtTheLimit()
    {
        var r = TestData.Registry();
        var world = new TileWorld(64, 64);
        ushort trunk = r.BlockId("dead_trunk");
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 20; x++)
            world.SetBlock(x, y, trunk);
        world.SetBlock(10, 39, 0);
        var result = new List<(int X, int Y)>();

        TreeFelling.Collect(world, r, 10, 39, result);

        Assert.Equal(TreeFelling.MaxBlocks, result.Count);
    }
}
```

Observação do teste de queda: o jogador está em x=20, y≈38,5; o tile (22, 38) fica a ~2 tiles, dentro do alcance. Os galhos em x=20,21 (y=36) não colidem com o jogador porque não são sólidos. O item de cada tile quebrado é um `EntitySpawned` próprio (a fusão de itens acontece depois, sem desfazer os eventos de criação).

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~TreeFellingTests"`
Expected: FAIL (não compila).

- [ ] **Step 3: `TreeFelling`**

`src/Zyon.Core/Sim/Systems/TreeFelling.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Sim.Systems;

/// <summary>
/// Derrubada de árvore: os blocos com "tree" ligados (4 vizinhos, só passando por blocos com "tree") ao tile quebrado
/// que estejam na linha dele ou acima. O que está abaixo (o toco) fica; não há regra de sustentação.
/// </summary>
internal static class TreeFelling
{
    /// <summary>Limite de segurança por derrubada.</summary>
    public const int MaxBlocks = 256;

    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, -1), (0, 1)];

    /// <summary>Preenche result com os tiles a derrubar a partir de (x, y), que já foi quebrado (não entra no resultado).</summary>
    public static void Collect(TileWorld tiles, Registry registry, int x, int y, List<(int X, int Y)> result)
    {
        result.Clear();
        var seen = new HashSet<(int, int)> { (x, y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (dx, dy) in Neighbors)
            {
                int nx = cx + dx, ny = cy + dy;
                if (ny > y || !seen.Add((nx, ny)) || !registry.Block(tiles.GetBlock(nx, ny)).Tree) continue;
                result.Add((nx, ny));
                if (result.Count == MaxBlocks) return;
                queue.Enqueue((nx, ny));
            }
        }
    }
}
```

- [ ] **Step 4: `ToolActionSystem`**

Campo novo: `private readonly List<(int X, int Y)> felled = new();`

Em `Mine`, troque o fim (depois de `if (st.MineProgress < 1f) {...}`) por:

```csharp
        Break(sim, tx, ty, block);
        if (block.Tree)
        {
            TreeFelling.Collect(sim.Tiles, sim.Registry, tx, ty, felled);
            foreach (var (x, y) in felled)
                Break(sim, x, y, sim.Registry.Block(sim.Tiles.GetBlock(x, y)));
        }
        StopMining(sim, playerId, ref st);
    }

    /// <summary>Quebra o tile: some, emite TileChanged e agenda o drop no centro dele.</summary>
    private void Break(Simulation sim, int x, int y, BlockDef block)
    {
        sim.Tiles.SetBlock(x, y, 0);
        sim.Emit(new TileChanged(x, y, 0, sim.Tiles.GetWall(x, y)));
        if (block.DropItemId != 0)
            pendingDrops.Add((new ItemStack(block.DropItemId, 1), new Vector2(x + 0.5f, y + 0.5f)));
    }
```

Atualize o resumo da classe: `/// <summary>Ação primária contextual: ferramenta minera (bloco de árvore derruba a árvore, ver TreeFelling), bloco é colocado.</summary>`

- [ ] **Step 5: Rode tudo**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Zyon.Core tests/Zyon.Core.Tests
git commit -m "feat(core): quebrar o tronco derruba a árvore" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 10: `BiomeDetector`

**Files:**
- Create: `src/Zyon.Core/Biomes/BiomeDetector.cs`
- Test: `tests/Zyon.Core.Tests/BiomeDetectorTests.cs` (novo)

- [ ] **Step 1: Testes (falham)**

```csharp
using Zyon.Core.Biomes;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class BiomeDetectorTests
{
    private static readonly Data.Registry R = TestData.Registry();
    private static readonly BiomeSet Set = BiomeSet.Resolve(TestData.WorldGenConfig().Biomes!, R);

    /// <summary>Preenche count tiles com o bloco, linha a linha a partir de (x0, y0), numa largura de 20.</summary>
    private static void Fill(TileWorld world, string block, int count, int x0 = 40, int y0 = 40)
    {
        ushort id = R.BlockId(block);
        for (int i = 0; i < count; i++) world.SetBlock(x0 + i % 20, y0 + i / 20, id);
    }

    private static string Detect(TileWorld world, int x = 50, int y = 50)
    {
        using var sim = new Simulation(R, world);
        return new BiomeDetector(Set, R).Detect(sim, x, y).Id;
    }

    [Theory]
    [InlineData("snow", "snow")]
    [InlineData("ice", "snow")]
    [InlineData("sand", "desert")]
    [InlineData("sandstone", "desert")]
    [InlineData("cactus", "desert")]
    public void EnoughMarkers_DetectTheirBiome(string block, string biome)
    {
        var world = new TileWorld(128, 128);
        Fill(world, block, Set.DetectThreshold);

        Assert.Equal(biome, Detect(world));
    }

    [Fact]
    public void BelowTheThreshold_IsTheDefault()
    {
        var world = new TileWorld(128, 128);
        Fill(world, "snow", Set.DetectThreshold - 1);
        Fill(world, "dirt", 400, y0: 60);

        Assert.Equal("forest", Detect(world));
    }

    [Fact]
    public void MostMarkersWin()
    {
        var world = new TileWorld(128, 128);
        Fill(world, "sand", 250);
        Fill(world, "snow", 300, y0: 55);

        Assert.Equal("snow", Detect(world));
    }

    [Fact]
    public void MarkersOutsideTheArea_DoNotCount()
    {
        var world = new TileWorld(256, 128);
        Fill(world, "snow", 400, x0: 150);

        Assert.Equal("forest", Detect(world, 50, 50));
    }

    [Fact]
    public void NearTheWorldEdge_DoesNotThrow()
    {
        var world = new TileWorld(64, 64);
        Fill(world, "sand", 300, x0: 0, y0: 0);

        Assert.Equal("desert", Detect(world, 0, 0));
    }
}
```

- [ ] **Step 2: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Core.Tests --filter "FullyQualifiedName~BiomeDetectorTests"`
Expected: FAIL (não compila).

- [ ] **Step 3: Implemente**

`src/Zyon.Core/Biomes/BiomeDetector.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Sim;

namespace Zyon.Core.Biomes;

/// <summary>
/// Bioma em volta de um ponto, como no Terraria: conta os marcadores de cada bioma num retângulo DetectWidth ×
/// DetectHeight centrado nele; o que tiver mais vence se chegar a DetectThreshold, senão vale o padrão (empate: o
/// primeiro do arquivo). Nada vai para o save. O jogo consulta a cada <see cref="IntervalTicks"/>.
/// </summary>
public sealed class BiomeDetector
{
    public const int IntervalTicks = 30;

    private readonly BiomeSet set;
    private readonly int[] biomeOfBlock;
    private readonly int[] counts;

    public BiomeDetector(BiomeSet set, Registry registry)
    {
        this.set = set;
        biomeOfBlock = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        foreach (var biome in set.All)
            foreach (var marker in biome.Markers)
                biomeOfBlock[marker] = biome.Index;
        counts = new int[set.All.Count];
    }

    public Biome Detect(IWorldView view, int centerX, int centerY)
    {
        Array.Clear(counts);
        int x0 = Math.Max(0, centerX - set.DetectWidth / 2), x1 = Math.Min(view.Width, centerX - set.DetectWidth / 2 + set.DetectWidth);
        int y0 = Math.Max(0, centerY - set.DetectHeight / 2), y1 = Math.Min(view.Height, centerY - set.DetectHeight / 2 + set.DetectHeight);
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            int b = biomeOfBlock[view.GetBlock(x, y)];
            if (b >= 0) counts[b]++;
        }

        int best = -1;
        for (int i = 0; i < counts.Length; i++)
            if (counts[i] >= set.DetectThreshold && (best < 0 || counts[i] > counts[best]))
                best = i;
        return best < 0 ? set.Default : set.All[best];
    }
}
```

- [ ] **Step 4: Rode tudo**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/Biomes tests/Zyon.Core.Tests/BiomeDetectorTests.cs
git commit -m "feat(core): BiomeDetector conta marcadores em volta do jogador" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 11: Céu e fundos por bioma no cliente (`data/client/biomes.json`, `BiomeAmbience`)

**Files:**
- Create: `data/client/biomes.json`, `src/Zyon.Client/BiomeAmbience.cs`
- Delete: `data/client/visuals.json`
- Rewrite: `src/Zyon.Client/ClientVisuals.cs`
- Modify: `src/Zyon.Game/GameFlow.cs`, `src/Zyon.Game/GameRoot.cs` (só para compilar)
- Test: `tests/Zyon.Client.Tests/ClientVisualsTests.cs` (reescrever), `tests/Zyon.Client.Tests/BiomeAmbienceTests.cs` (novo)

- [ ] **Step 1: Dados**

`data/client/biomes.json`:

```json
{
  "forest": { "sky": "#7f9fbf", "layers": ["forest_far", "forest_near"] },
  "snow":   { "sky": "#a9bfd6", "layers": ["snow_far", "snow_near"] },
  "desert": { "sky": "#c9ad86", "layers": ["desert_far", "desert_near"] }
}
```

Apague `data/client/visuals.json` (`git rm`).

- [ ] **Step 2: Testes (falham)**

`tests/Zyon.Client.Tests/ClientVisualsTests.cs` (substitui o arquivo):

```csharp
using Zyon.Core.Biomes;
using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ClientVisualsTests
{
    private static readonly string[] Ids = ["forest", "snow", "desert"];

    private const string Valid = """
        {
          "forest": { "sky": "#010203", "layers": ["forest_far", "forest_near"] },
          "snow":   { "sky": "#040506", "layers": ["snow_far", "snow_near"] },
          "desert": { "sky": "#070809", "layers": ["desert_far", "desert_near"] }
        }
        """;

    [Fact]
    public void RealFile_HasEveryBiomeOfTheData_AndItsArtExists()
    {
        var ids = BiomeConfigLoader.LoadFromDirectory(TestData.DataDir).Biomes.Select(b => b!.Id);
        var v = ClientVisuals.LoadFromDirectory(TestData.DataDir, ids);

        Assert.Equal(new Rgb(0x7f, 0x9f, 0xbf), v.For("forest").Sky);
        Assert.Equal(new[] { "snow_far", "snow_near" }, v.For("snow").Layers);
        v.RequireArt(ArtCatalog.LoadFromDirectory(TestData.DataDir, TestData.Registry()).Manifest);
    }

    [Fact]
    public void Valid_Loads_WithCommentsAndTrailingCommas()
    {
        var v = ClientVisuals.Load("// céus\n" + Valid.TrimEnd().TrimEnd('}') + ",}", Ids);

        Assert.Equal(new Rgb(7, 8, 9), v.For("desert").Sky);
    }

    [Theory]
    [InlineData("{ not json", "JSON")]
    [InlineData("""{"forest": {"sky": "#zz0000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "#zz0000")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "layers")]
    [InlineData("""{"forest": {"layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "sky")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}}""", "desert")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}, "jungle": {"sky": "#000000", "layers": ["a", "b"]}}""", "jungle")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"], "fog": 1}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "JSON")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json, Ids));

        Assert.Contains(ClientVisuals.DisplayName, ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void LayerWithoutArt_Throws()
    {
        var v = ClientVisuals.Load(Valid, Ids);
        var manifest = new ArtManifest { Backgrounds = ["forest_far", "forest_near", "snow_far", "snow_near", "desert_far"] };

        var ex = Assert.Throws<DataException>(() => v.RequireArt(manifest));

        Assert.Contains("desert_near", ex.Message);
    }
}
```

`tests/Zyon.Client.Tests/BiomeAmbienceTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class BiomeAmbienceTests
{
    private static readonly ClientVisuals Visuals = ClientVisuals.Load("""
        {
          "forest": { "sky": "#000000", "layers": ["f1", "f2"] },
          "snow":   { "sky": "#c8c8c8", "layers": ["s1", "s2"] },
          "desert": { "sky": "#640000", "layers": ["d1", "d2"] }
        }
        """, ["forest", "snow", "desert"]);

    [Fact]
    public void Start_IsTheInitialBiome()
    {
        var a = new BiomeAmbience(Visuals, "forest");

        Assert.Equal(new Rgb(0, 0, 0), a.Sky);
        Assert.Equal(1f, a.Weight("forest"));
        Assert.Equal(0f, a.Weight("snow"));
    }

    [Fact]
    public void Change_IsLinearOverTwoSeconds()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");

        a.Advance(1f);
        Assert.Equal(new Rgb(100, 100, 100), a.Sky);
        Assert.Equal(0.5f, a.Weight("forest"), 3);
        Assert.Equal(0.5f, a.Weight("snow"), 3);

        a.Advance(1.5f);
        Assert.Equal(new Rgb(200, 200, 200), a.Sky);
        Assert.Equal(0f, a.Weight("forest"));
        Assert.Equal(1f, a.Weight("snow"));
    }

    [Fact]
    public void ChangeMidway_StartsFromTheCurrentColorAndWeights()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");
        a.Advance(1f);                      // céu (100, 100, 100), floresta 0,5, neve 0,5

        a.SetBiome("desert");
        a.Advance(1f);

        Assert.Equal(new Rgb(100, 50, 50), a.Sky);   // metade do caminho de (100,100,100) a (100,0,0)
        Assert.Equal(0f, a.Weight("forest"), 3);
        Assert.Equal(0f, a.Weight("snow"), 3);
        Assert.Equal(0.5f, a.Weight("desert"), 3);
    }

    [Fact]
    public void SameBiome_DoesNotRestartTheTransition()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");
        a.Advance(1f);

        a.SetBiome("snow");
        a.Advance(1f);

        Assert.Equal(new Rgb(200, 200, 200), a.Sky);
    }

    [Fact]
    public void UnknownBiome_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BiomeAmbience(Visuals, "forest").SetBiome("jungle"));
    }
}
```

- [ ] **Step 3: Rode e veja falhar**

Run: `dotnet test tests/Zyon.Client.Tests --filter "FullyQualifiedName~ClientVisualsTests|FullyQualifiedName~BiomeAmbienceTests"`
Expected: FAIL (não compila).

- [ ] **Step 4: `ClientVisuals`**

Substitua `src/Zyon.Client/ClientVisuals.cs` (mantenha o `Rgb` como está, no topo do arquivo):

```csharp
/// <summary>Céu e camadas de fundo (da mais distante à mais próxima) de um bioma.</summary>
public sealed record BiomeVisual(Rgb Sky, IReadOnlyList<string> Layers);

/// <summary>
/// Cores e fundos de cada bioma, lidos de data/client/biomes.json (não confundir com data/biomes.json, da
/// simulação). Precisa de exatamente uma entrada para cada bioma dos dados; as camadas precisam existir na arte.
/// </summary>
public sealed class ClientVisuals
{
    public const string FileName = "biomes.json";
    public const string DisplayName = "client/biomes.json";

    /// <summary>Camadas por bioma: distante e próxima.</summary>
    public const int LayerCount = 2;

    private readonly Dictionary<string, BiomeVisual> biomes;

    private ClientVisuals(Dictionary<string, BiomeVisual> biomes) => this.biomes = biomes;

    public IReadOnlyDictionary<string, BiomeVisual> Biomes => biomes;

    public BiomeVisual For(string biomeId) => biomes[biomeId];

    public static ClientVisuals LoadFromDirectory(string dataDir, IEnumerable<string> biomeIds)
    {
        var path = Path.Combine(dataDir, "client", FileName);
        if (!File.Exists(path)) throw new DataException($"{DisplayName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), biomeIds);
    }

    public static ClientVisuals Load(string json, IEnumerable<string> biomeIds)
    {
        Dictionary<string, Dto?> dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<Dictionary<string, Dto?>>(json, JsonOptions) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        var ids = biomeIds.ToList();
        foreach (var id in ids)
            if (!dtos.ContainsKey(id)) throw Fail($"falta o bioma '{id}' (de data/biomes.json)");
        var result = new Dictionary<string, BiomeVisual>();
        foreach (var (id, dto) in dtos)
        {
            if (!ids.Contains(id)) throw Fail($"bioma '{id}' não existe em data/biomes.json");
            if (dto is null) throw Fail($"{id}: entrada nula");
            var layers = dto.Layers;
            if (layers is null || layers.Count != LayerCount || layers.Any(string.IsNullOrEmpty))
                throw Fail($"{id}: 'layers' precisa de {LayerCount} nomes (distante, próxima)");
            result[id] = new BiomeVisual(Parse(dto.Sky, $"{id}.sky"), layers!);
        }
        return new ClientVisuals(result);
    }

    /// <summary>Lança DataException se alguma camada de fundo não estiver no manifesto da arte.</summary>
    public void RequireArt(ArtManifest manifest)
    {
        foreach (var (id, visual) in biomes)
            foreach (var layer in visual.Layers)
                if (!manifest.Backgrounds.Contains(layer))
                    throw Fail($"{id}: camada '{layer}' sem arte (falta em art/art.json → backgrounds; rode tools/gen-art.sh)");
    }

    private static Rgb Parse(string? hex, string field) =>
        hex is null ? throw Fail($"campo '{field}' ausente") :
        Rgb.TryParse(hex, out var c) ? c : throw Fail($"'{field}': cor inválida '{hex}' (use #rrggbb)");

    private static DataException Fail(string message) => new($"{DisplayName}: {message}");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class Dto
    {
        public string? Sky { get; set; }
        public List<string>? Layers { get; set; }
    }
}
```

- [ ] **Step 5: `BiomeAmbience`**

`src/Zyon.Client/BiomeAmbience.cs`:

```csharp
namespace Zyon.Client;

/// <summary>
/// Céu e peso (opacidade) dos fundos de cada bioma conforme o bioma atual. Ao trocar, o céu vai linearmente da cor
/// daquele instante até a do bioma novo em <see cref="TransitionSeconds"/>, o peso do novo sobe e o dos outros desce
/// no mesmo ritmo — trocar no meio de uma transição parte do que está na tela.
/// </summary>
public sealed class BiomeAmbience
{
    public const float TransitionSeconds = 2f;

    private readonly ClientVisuals visuals;
    private readonly Dictionary<string, float> weights = [];
    private Rgb from;
    private float progress = 1f;

    public BiomeAmbience(ClientVisuals visuals, string initialBiome)
    {
        this.visuals = visuals;
        foreach (var id in visuals.Biomes.Keys) weights[id] = 0f;
        Require(initialBiome);
        Current = initialBiome;
        weights[initialBiome] = 1f;
        Sky = from = visuals.For(initialBiome).Sky;
    }

    public string Current { get; private set; }
    public Rgb Sky { get; private set; }

    public float Weight(string biomeId) => weights[biomeId];

    public void SetBiome(string biomeId)
    {
        Require(biomeId);
        if (biomeId == Current) return;
        Current = biomeId;
        from = Sky;
        progress = 0f;
    }

    public void Advance(float seconds)
    {
        float step = seconds / TransitionSeconds;
        progress = Math.Min(1f, progress + step);
        foreach (var id in visuals.Biomes.Keys)
            weights[id] = id == Current ? Math.Min(1f, weights[id] + step) : Math.Max(0f, weights[id] - step);
        var to = visuals.For(Current).Sky;
        Sky = new Rgb(Lerp(from.R, to.R, progress), Lerp(from.G, to.G, progress), Lerp(from.B, to.B, progress));
    }

    private void Require(string biomeId)
    {
        if (!weights.ContainsKey(biomeId)) throw new ArgumentException($"bioma '{biomeId}' sem visual", nameof(biomeId));
    }

    private static byte Lerp(byte a, byte b, float t) => (byte)Math.Clamp((int)MathF.Round(a + (b - a) * t), 0, 255);
}
```

- [ ] **Step 6: Mantenha o jogo compilando**

`src/Zyon.Game/GameFlow.cs`:
- `using Zyon.Core.Biomes;`
- propriedade: `public static BiomeSet Biomes { get; private set; } = null!;`
- em `EnsureInitialized`, troque a linha do `Visuals` (que fica depois do `Art`; mova-a para depois do `ArtTextures`):

```csharp
        Biomes = BiomeSet.Resolve(WorldGenConfig.Biomes!, Registry);
        ...
        Art = ArtCatalog.LoadFromDirectory(DataDir, Registry);
        ArtTextures = ArtTextures.Load(Art);
        Visuals = ClientVisuals.LoadFromDirectory(DataDir, Biomes.All.Select(b => b.Id));
        Visuals.RequireArt(Art.Manifest);
```

(`System.Linq` — confira se o arquivo já tem os usings implícitos; o projeto Godot pode não ter `ImplicitUsings`. Acrescente `using System.Linq;` se precisar.)

`src/Zyon.Game/GameRoot.cs` — em `Start`, a linha do céu passa a usar o bioma padrão (a Tarefa 12 troca por `BiomeAmbience`):

```csharp
            RenderingServer.SetDefaultClearColor(visuals.For(GameFlow.Biomes.Default.Id).Sky.ToGodot());
```

- [ ] **Step 7: Rode tudo**

Run: `dotnet test`
Expected: PASS (inclui compilar o `Zyon.Game`). `grep -rn "visuals.json" src tests tools` não acha nada.

- [ ] **Step 8: Commit**

```bash
git add -A data/client src/Zyon.Client src/Zyon.Game tests/Zyon.Client.Tests
git commit -m "feat(client): céu e fundos por bioma em data/client/biomes.json, com transição" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 12: Jogo (Godot): céu e fundo em parallax por bioma, F3 e smoke test

**Files:**
- Create: `src/Zyon.Game/Rendering/BackgroundLayers.cs`
- Modify: `src/Zyon.Game/Rendering/ArtTextures.cs`, `src/Zyon.Game/GameRoot.cs`, `src/Zyon.Game/Hud/DebugOverlay.cs`

Não há testes unitários no `Zyon.Game`: a verificação é o build, o smoke test e uma olhada com janela.

- [ ] **Step 1: Texturas dos fundos**

`ArtTextures`:

```csharp
    /// <summary>Camadas de fundo pelo nome (manifesto: backgrounds).</summary>
    public required IReadOnlyDictionary<string, Texture2D> Backgrounds { get; init; }
```

e em `Load`:

```csharp
        var backgrounds = new Dictionary<string, Texture2D>();
        foreach (var name in art.Manifest.Backgrounds) backgrounds[name] = Png(art, ArtManifest.BackgroundFile(name));
```

com `Backgrounds = backgrounds,` no inicializador.

- [ ] **Step 2: Camadas de fundo**

`src/Zyon.Game/Rendering/BackgroundLayers.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Fundo distante em parallax: as camadas de todos os biomas (distantes primeiro, depois as próximas), cada uma um
/// Parallax2D que rola na horizontal a uma fração da câmera e se repete; na vertical acompanha o mundo, com a base
/// na linha surfaceMax × altura (abaixo dela só o céu). A opacidade de cada bioma vem do BiomeAmbience. Fica antes
/// dos chunks na árvore (atrás deles) e antes da LightOverlay (a noite escurece o fundo).
/// </summary>
public partial class BackgroundLayers : Node2D
{
    /// <summary>Fração da rolagem da câmera de cada camada (distante, próxima).</summary>
    private static readonly float[] ScrollFactors = [0.1f, 0.3f];

    /// <summary>Pixels do mundo por pixel do fundo.</summary>
    private const int PixelScale = 2;

    /// <summary>Repetições de cada lado: cobre a tela mais larga com o zoom mais afastado.</summary>
    private const int RepeatTimes = 8;

    private readonly List<(string Biome, Parallax2D Node)> layers = new();

    public void Init(ClientVisuals visuals, IReadOnlyDictionary<string, Texture2D> textures, float baseY)
    {
        for (int i = 0; i < ClientVisuals.LayerCount; i++)
            foreach (var (biome, visual) in visuals.Biomes)
            {
                var texture = textures[visual.Layers[i]];
                var parallax = new Parallax2D
                {
                    ScrollScale = new Vector2(ScrollFactors[i], 1f),
                    RepeatSize = new Vector2(texture.GetWidth() * PixelScale, 0f),
                    RepeatTimes = RepeatTimes,
                    Visible = false,
                };
                parallax.AddChild(new Sprite2D
                {
                    Texture = texture,
                    Centered = false,
                    Scale = new Vector2(PixelScale, PixelScale),
                    Position = new Vector2(0f, baseY - texture.GetHeight() * PixelScale),
                    TextureFilter = TextureFilterEnum.Nearest,
                });
                AddChild(parallax);
                layers.Add((biome, parallax));
            }
    }

    public void Refresh(BiomeAmbience ambience)
    {
        foreach (var (biome, node) in layers)
        {
            float weight = ambience.Weight(biome);
            node.Visible = weight > 0f;
            node.Modulate = new Color(1f, 1f, 1f, weight);
        }
    }
}
```

- [ ] **Step 3: `GameRoot`**

Usings: `using Zyon.Core.Biomes;`. Campos novos:

```csharp
    private BackgroundLayers backgrounds = null!;
    private BiomeDetector biomeDetector = null!;
    private BiomeAmbience ambience = null!;
    private long lastBiomeTick;
    private Rgb lastSky;
```

Em `Start`, troque a linha `RenderingServer.SetDefaultClearColor(...)` e crie os fundos **antes** do `worldRenderer` (ordem na árvore = ordem de desenho):

```csharp
            biomeDetector = new BiomeDetector(GameFlow.Biomes, GameFlow.Registry);
            ambience = new BiomeAmbience(visuals, DetectBiome(newSession.World).Id);   // começa já no bioma do jogador, sem transição
            lastSky = ambience.Sky;
            RenderingServer.SetDefaultClearColor(lastSky.ToGodot());
            GD.Print(...);   // a linha "Zyon: mundo ..." existente fica aqui

            backgrounds = new BackgroundLayers();
            AddChild(backgrounds);
            backgrounds.Init(visuals, GameFlow.ArtTextures.Backgrounds,
                GameFlow.WorldGenConfig.SurfaceMax * sim.Height * RenderMath.PixelsPerTile);
            backgrounds.Refresh(ambience);
            worldRenderer = new WorldRenderer();
            ...
```

Método novo:

```csharp
    /// <summary>Bioma em volta do jogador local (o padrão se ele não existir).</summary>
    private Biome DetectBiome(IWorldView world) =>
        world.TryGetEntityView(playerId, out var me)
            ? biomeDetector.Detect(world, (int)System.MathF.Floor(me.Position.X), (int)System.MathF.Floor(me.Position.Y))
            : GameFlow.Biomes.Default;
```

(`playerId` já está definido antes, no começo de `Start`.)

Em `_Process`, logo depois de `session.Update(delta);` e do `long updateEnd = ...` — mantenha o `updateEnd` onde está, e acrescente depois do bloco que drena os eventos e antes de `entityRenderer.Refresh(...)`:

```csharp
            if (session.World.Tick - lastBiomeTick >= BiomeDetector.IntervalTicks)
            {
                lastBiomeTick = session.World.Tick;
                ambience.SetBiome(DetectBiome(session.World).Id);
            }
            ambience.Advance((float)delta);
            if (ambience.Sky != lastSky)
            {
                lastSky = ambience.Sky;
                RenderingServer.SetDefaultClearColor(lastSky.ToGodot());
            }
            backgrounds.Refresh(ambience);
```

`ToGodot()` de `Rgb` já existe (usado antes com `visuals.Sky`); confira em `GodotConvert.cs`.

Smoke test — novo método e chamada antes de `PrintSmokeStats()`:

```csharp
    /// <summary>Smoke test: o spawn fica no bunker, na floresta (bioma padrão).</summary>
    private void VerifySmokeBiome()
    {
        if (ambience.Current != GameFlow.Biomes.Default.Id)
            throw new InvalidOperationException($"bioma no spawn é '{ambience.Current}', esperado '{GameFlow.Biomes.Default.Id}'");
    }
```

```csharp
                        VerifySmokeArt();
                        VerifySmokeBiome();
                        PrintSmokeStats();
                        GD.Print($"SMOKE OK: tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}, " +
                            $"minerado {smoke.MinedTile}, tocha em {smoke.TorchTile}, save ok, luz ok, arte ok, bioma {ambience.Current}");
```

F3 — passe o bioma no `DebugInfo`: `new DebugInfo(..., lightOverlay.SkyFactor, ambience.Current)`.

- [ ] **Step 4: `DebugOverlay`**

`DebugInfo` ganha `string Biome` no fim. No `Refresh`, a última linha do formato passa a:

```csharp
            "janela {25}×{26} | tela interna {27:0}×{28:0} (F11 tela cheia)\n" +
            "bioma: {29}",
```

e o argumento `info.Biome` depois de `screen.X, screen.Y`. Atualize o resumo da classe (`... o custo da luz e o bioma atual.`).

- [ ] **Step 5: Build, testes e smoke**

Run: `dotnet test`
Expected: PASS.

Run: `tools/smoke-test.sh`
Expected: `SMOKE OK: ... arte ok, bioma forest` e `smoke test OK`.

- [ ] **Step 6: Olhada com janela (manual)**

Rode `tools/play.sh --dev --size dev --seed 1`; ande para os dois lados por ~300 tiles (o mundo dev tem 1050 colunas) e confira com F3: o bioma muda para `snow` de um lado e `desert` do outro; o céu muda em ~2 s; os fundos aparecem atrás do terreno, rolam mais devagar que o mundo e se repetem sem emenda visível; árvores e decorativos estão sobre o chão. Se o fundo aparecer deslocado (ex.: preso à tela na vertical ou fora da altura da superfície), ajuste `Position`/`ScrollScale` no `BackgroundLayers` — anote o que mudou no relatório. Se não houver como olhar (sem janela), diga isso no relatório em vez de afirmar que está certo.

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): céu e fundo em parallax por bioma, bioma no F3 e no smoke test" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

### Task 13: Documentação e verificação final

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: `CLAUDE.md`**

- Item `src/Zyon.Core/WorldGen`: passes `terrain`, `mountain`, `biomes`, `layers`, `caves`, `ores`, `walls`, `bunker`, `trees`; acrescente: "biomas em `data/biomes.json` (`Zyon.Core.Biomes`: `BiomeConfigLoader`, `BiomeSet`; o passe `biomes` monta o `BiomeMap` — faixas de neve e deserto em lados opostos da montanha, divisa por ruído que afina com a profundidade — e `layers`/`ores`/`walls` usam os blocos e paredes do bioma de cada tile; o passe `trees` planta árvores e decoração fora da área do bunker). O `biomes.json` entra na impressão digital da configuração."
- Novo item para `src/Zyon.Core/Biomes`: "`BiomeDetector` (bioma em volta de um ponto pela contagem de marcadores; o jogo consulta a cada 30 ticks)".
- Regras invioláveis — acrescente: "Blocos com `"tree": true` são partes de árvore: quebrar um derruba os ligados na mesma linha ou acima (`TreeFelling`, máx. 256). Um bloco só pode ser marcador de um bioma."
- Item `src/Zyon.Game`: troque "`data/client/visuals.json` só tem a cor do céu" por "`data/client/biomes.json` tem o céu e as camadas de fundo de cada bioma (`ClientVisuals`, exige todos os biomas de `data/biomes.json`); `BiomeAmbience` faz a transição de 2 s e `BackgroundLayers` desenha os `Parallax2D` atrás dos chunks".
- Item `src/Zyon.ArtGen`: acrescente os fundos (`backdrop`, `data/client/art/backgrounds/`).
- Smoke test: a linha `SMOKE OK` termina com `bioma forest`.
- F3: acrescente "bioma atual".

- [ ] **Step 2: Verificação completa**

Run: `dotnet test`
Expected: PASS (anote as contagens de Core, Client e ArtGen).

Run: `dotnet test -c Release --filter Category=Performance`
Expected: PASS.

Run: `tools/smoke-test.sh`
Expected: `smoke test OK`.

Run: `git status`
Expected: limpo depois do commit; nenhum PNG desatualizado (`ArtUpToDateTests` passou).

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: biomas no CLAUDE.md" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01W24Cc2XiDdunZLMumFEGj2"
```

---

## Notas e riscos conhecidos

- **Sombra das árvores:** a luz do céu desce pela coluna até o primeiro bloco (inclusive não sólido), então a coluna sob uma árvore fica um pouco mais escura (a luz entra pelos lados com a atenuação do ar). Aceito neste plano.
- **`Parallax2D` com zoom:** o fundo usa o zoom da câmera; a verificação visual da Tarefa 12 é o único teste disso.
- **Mundos minúsculos** (< ~500 colunas): as faixas podem encostar na montanha (regra de 20 colunas na borda do spec); só os testes de tamanhos reais exigem a montanha na floresta.
