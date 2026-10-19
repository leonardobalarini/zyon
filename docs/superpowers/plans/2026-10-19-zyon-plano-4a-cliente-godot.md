# Zyon — Plano 4a: Cliente Godot (mundo jogável na tela) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Abrir o jogo no Godot e jogar num mundo gerado: tiles renderizados por chunk com autotiling e arte provisória, jogador e itens interpolados, câmera pixel-perfect, controles (andar, pular, minerar, colocar, hotbar, soltar item), destaque do tile mirado, rachaduras e uma hotbar simples — com smoke test headless.

**Architecture:** `Zyon.Client` (.NET puro, testado) contém toda a lógica de apresentação: máscara de autotiling, layout do atlas, montagem dos vértices de cada chunk, visibilidade de chunks, rastreio de chunks sujos, conversões tile↔pixel, cores provisórias e geração do atlas provisório em RGBA. `Zyon.Game` (projeto Godot 4.7 .NET) é uma camada fina que transforma esses dados em `ArrayMesh`, `MultiMesh` e nós, lê input e conversa só com `IGameSession`.

**Tech Stack:** Godot 4.7.2 .NET (portátil em `D:\Projetos\Ferramentas\Godot`), .NET 10, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`, seção 7. O **Plano 4b** cobre o restante da seção 7 e o fluxo de saves: inventário completo com arrastar/soltar, menus (novo/carregar mundo e personagem), tela de carregamento com progresso, autosave e salvar ao sair.

**Validado antes do plano:** um projeto `Godot.NET.Sdk/4.7.2` com `net10.0` referenciando `Zyon.Core` compila com `dotnet build` e roda headless (`--headless --path`), gerando um mundo (runtime 10.0.7).

---

## Convenções (valem para todas as tarefas)

- Tudo do `CLAUDE.md` continua valendo. `Zyon.Client` **nunca** referencia Godot; usa `System.Numerics.Vector2`.
- **Pixels por tile:** 16 (`RenderMath.PixelsPerTile`). Mundo Godot em pixels, Y para baixo (igual ao Core).
- **Nós Godot criados por código** têm construtor sem parâmetros e um método `Init(...)` (o Godot exige construtor vazio para recarregar assemblies no editor).
- **Godot:** `GODOT="/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64_console.exe"`. Sempre a versão `_console` para ver a saída no terminal.
- **Verificação do Godot** = `dotnet build` do projeto do jogo + `tools/smoke-test.sh` (a partir da Task 5). Não existe teste unitário de nó Godot.
- **Shell:** Bash (Git Bash) a partir de `D:\Projetos\Zyon`.
- **Commits:** mensagem + segundo `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. Nunca `--amend`.
- **Branch:** `feat/plano-4a-cliente-godot` (criada pelo controlador).

## Mapa de arquivos

```
src/Zyon.Client/
  Zyon.Client.csproj
  RenderMath.cs            # tile↔pixel, interpolação
  ChunkVisibility.cs       # faixa de chunks visíveis
  DirtyChunkTracker.cs     # chunks a reconstruir a partir de TileChanged
  AutoTile.cs              # máscara de 4 vizinhos + variação estável
  TileAtlasLayout.cs       # células e UVs do atlas
  ChunkMeshBuilder.cs      # vértices/UVs/índices de um chunk (ChunkMeshData)
  ClientVisuals.cs         # cores provisórias (data/client/visuals.json)
  PlaceholderAtlas.cs      # atlas provisório em RGBA
  LaunchOptions.cs         # argumentos de linha de comando do jogo
tests/Zyon.Client.Tests/   # um arquivo de teste por unidade acima
data/client/visuals.json
src/Zyon.Game/             # projeto Godot
  project.godot  Zyon.Game.csproj  icon.svg
  scenes/Game.tscn
  GameRoot.cs  DataPaths.cs  GodotLog.cs  GodotConvert.cs
  Rendering/ChunkNode.cs  Rendering/WorldRenderer.cs  Rendering/EntityRenderer.cs  Rendering/WorldOverlay.cs
  GameCamera.cs  PlayerController.cs  Hud/Hotbar.cs  SmokeTest.cs
tools/smoke-test.sh
```

---

### Task 0: Projetos `Zyon.Client` e `Zyon.Client.Tests`

**Files:**
- Create: `src/Zyon.Client/Zyon.Client.csproj`, `tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj`
- Modify: `Zyon.slnx`

- [ ] **Step 1: Criar `src/Zyon.Client/Zyon.Client.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Zyon.Core\Zyon.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Zyon.Client.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Criar `tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Zyon.Client\Zyon.Client.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="..\..\data\**\*.json" LinkBase="data" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Adicionar à solução e compilar**

```bash
dotnet sln Zyon.slnx add src/Zyon.Client/Zyon.Client.csproj tests/Zyon.Client.Tests/Zyon.Client.Tests.csproj
dotnet build
```

Expected: 0 avisos, 0 erros.

- [ ] **Step 4: Commit**

```bash
git add Zyon.slnx src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "chore(client): projetos Zyon.Client e testes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 1: Conversões, visibilidade e chunks sujos

**Files:**
- Create: `src/Zyon.Client/RenderMath.cs`, `src/Zyon.Client/ChunkVisibility.cs`, `src/Zyon.Client/DirtyChunkTracker.cs`
- Test: `tests/Zyon.Client.Tests/RenderMathTests.cs`, `tests/Zyon.Client.Tests/ChunkVisibilityTests.cs`, `tests/Zyon.Client.Tests/DirtyChunkTrackerTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Client.Tests/RenderMathTests.cs`:

```csharp
using System.Numerics;

namespace Zyon.Client.Tests;

public class RenderMathTests
{
    [Fact]
    public void ToPixels_ScalesBySixteen()
    {
        Assert.Equal(new Vector2(160f, 40f), RenderMath.ToPixels(new Vector2(10f, 2.5f)));
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(15.9f, 31.9f, 0, 1)]
    [InlineData(16f, 32f, 1, 2)]
    [InlineData(-0.1f, -16f, -1, -1)]
    public void TileAtPixel_FloorsToTiles(float px, float py, int tx, int ty)
    {
        Assert.Equal((tx, ty), RenderMath.TileAtPixel(new Vector2(px, py)));
    }

    [Fact]
    public void Interpolate_BlendsPreviousAndCurrent()
    {
        var prev = new Vector2(0f, 10f);
        var cur = new Vector2(4f, 6f);

        Assert.Equal(prev, RenderMath.Interpolate(prev, cur, 0f));
        Assert.Equal(new Vector2(1f, 9f), RenderMath.Interpolate(prev, cur, 0.25f));
        Assert.Equal(cur, RenderMath.Interpolate(prev, cur, 1f));
    }
}
```

`tests/Zyon.Client.Tests/ChunkVisibilityTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class ChunkVisibilityTests
{
    // Um chunk tem 32 tiles × 16 px = 512 px.
    [Fact]
    public void Range_CoversRectPlusMargin()
    {
        var r = ChunkVisibility.Range(600f, 100f, 640f, 360f, margin: 1, chunksX: 100, chunksY: 100);

        Assert.Equal(new ChunkRange(0, 0, 3, 1), r);
    }

    [Fact]
    public void Range_IsClampedToWorld()
    {
        var r = ChunkVisibility.Range(-1000f, -1000f, 640f, 360f, margin: 2, chunksX: 4, chunksY: 3);

        Assert.Equal(new ChunkRange(0, 0, 1, 0), r);
    }

    [Fact]
    public void Contains_IsInclusive()
    {
        var r = new ChunkRange(1, 2, 3, 4);

        Assert.True(r.Contains(1, 2));
        Assert.True(r.Contains(3, 4));
        Assert.False(r.Contains(4, 4));
        Assert.False(r.Contains(1, 1));
    }
}
```

`tests/Zyon.Client.Tests/DirtyChunkTrackerTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class DirtyChunkTrackerTests
{
    private static List<(int, int)> Drain(DirtyChunkTracker t)
    {
        var all = new List<(int, int)>();
        while (t.TryTake(out var c)) all.Add(c);
        return all;
    }

    [Fact]
    public void InteriorTile_MarksOnlyItsChunk()
    {
        var t = new DirtyChunkTracker(chunksX: 4, chunksY: 4);

        t.MarkTile(40, 40);

        Assert.Equal(new[] { (1, 1) }, Drain(t));
    }

    [Fact]
    public void EdgeAndCornerTiles_MarkNeighbours()
    {
        var t = new DirtyChunkTracker(4, 4);

        t.MarkTile(32, 50);   // borda esquerda do chunk (1,1)
        Assert.Equal(new[] { (1, 1), (0, 1) }, Drain(t));

        t.MarkTile(63, 63);   // canto inferior direito do chunk (1,1)
        Assert.Equal(new[] { (1, 1), (2, 1), (1, 2) }, Drain(t));
    }

    [Fact]
    public void WorldBorder_SkipsMissingNeighbours()
    {
        var t = new DirtyChunkTracker(2, 2);

        t.MarkTile(0, 0);

        Assert.Equal(new[] { (0, 0) }, Drain(t));
    }

    [Fact]
    public void RepeatedMarks_AreDeduplicatedUntilTaken()
    {
        var t = new DirtyChunkTracker(4, 4);

        t.MarkTile(40, 40);
        t.MarkTile(41, 41);
        Assert.Equal(1, t.Count);
        Drain(t);
        t.MarkTile(40, 40);
        Assert.Equal(1, t.Count);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test tests/Zyon.Client.Tests`
Expected: FAIL na compilação — tipos inexistentes.

- [ ] **Step 3: Implementar**

`src/Zyon.Client/RenderMath.cs`:

```csharp
using System.Numerics;

namespace Zyon.Client;

/// <summary>Conversões entre unidades de tile (Core) e pixels (apresentação).</summary>
public static class RenderMath
{
    public const int PixelsPerTile = 16;

    public static Vector2 ToPixels(Vector2 tiles) => tiles * PixelsPerTile;

    public static (int X, int Y) TileAtPixel(Vector2 pixels) =>
        ((int)MathF.Floor(pixels.X / PixelsPerTile), (int)MathF.Floor(pixels.Y / PixelsPerTile));

    /// <summary>Posição para desenhar entre o tick anterior e o atual (alpha em [0, 1]).</summary>
    public static Vector2 Interpolate(Vector2 previous, Vector2 current, float alpha) =>
        Vector2.Lerp(previous, current, alpha);
}
```

`src/Zyon.Client/ChunkVisibility.cs`:

```csharp
using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>Faixa inclusiva de chunks.</summary>
public readonly record struct ChunkRange(int MinX, int MinY, int MaxX, int MaxY)
{
    public bool Contains(int cx, int cy) => cx >= MinX && cx <= MaxX && cy >= MinY && cy <= MaxY;
}

public static class ChunkVisibility
{
    public const int ChunkPixels = Chunk.Size * RenderMath.PixelsPerTile;

    /// <summary>Chunks que intersectam o retângulo (em pixels), mais uma margem, limitados ao mundo.</summary>
    public static ChunkRange Range(float left, float top, float width, float height, int margin, int chunksX, int chunksY)
    {
        int minX = (int)MathF.Floor(left / ChunkPixels) - margin;
        int minY = (int)MathF.Floor(top / ChunkPixels) - margin;
        int maxX = (int)MathF.Floor((left + width) / ChunkPixels) + margin;
        int maxY = (int)MathF.Floor((top + height) / ChunkPixels) + margin;
        return new ChunkRange(
            Math.Clamp(minX, 0, chunksX - 1),
            Math.Clamp(minY, 0, chunksY - 1),
            Math.Clamp(maxX, 0, chunksX - 1),
            Math.Clamp(maxY, 0, chunksY - 1));
    }
}
```

`src/Zyon.Client/DirtyChunkTracker.cs`:

```csharp
using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>
/// Chunks cuja malha precisa ser refeita. Um tile na borda de um chunk também suja o vizinho,
/// porque o autotiling do vizinho depende dele.
/// </summary>
public sealed class DirtyChunkTracker(int chunksX, int chunksY)
{
    private readonly HashSet<(int, int)> pending = new();
    private readonly Queue<(int, int)> order = new();

    public int Count => pending.Count;

    public void MarkTile(int x, int y)
    {
        int cx = x >> Chunk.Shift, cy = y >> Chunk.Shift;
        int lx = x & Chunk.Mask, ly = y & Chunk.Mask;
        Mark(cx, cy);
        if (lx == 0) Mark(cx - 1, cy);
        if (lx == Chunk.Mask) Mark(cx + 1, cy);
        if (ly == 0) Mark(cx, cy - 1);
        if (ly == Chunk.Mask) Mark(cx, cy + 1);
    }

    public bool TryTake(out (int Cx, int Cy) chunk)
    {
        if (order.TryDequeue(out chunk))
        {
            pending.Remove(chunk);
            return true;
        }
        return false;
    }

    private void Mark(int cx, int cy)
    {
        if ((uint)cx >= (uint)chunksX || (uint)cy >= (uint)chunksY) return;
        if (pending.Add((cx, cy)))
            order.Enqueue((cx, cy));
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test tests/Zyon.Client.Tests` → PASS, 13 testes. Depois `dotnet test` → tudo verde.

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): conversões tile/pixel, visibilidade e chunks sujos" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Autotiling e layout do atlas

**Files:**
- Create: `src/Zyon.Client/AutoTile.cs`, `src/Zyon.Client/TileAtlasLayout.cs`
- Create: `tests/Zyon.Client.Tests/TestData.cs`
- Test: `tests/Zyon.Client.Tests/AutoTileTests.cs`, `tests/Zyon.Client.Tests/TileAtlasLayoutTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Client.Tests/TestData.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

internal static class TestData
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Registry Registry() => RegistryLoader.LoadFromDirectory(DataDir);

    /// <summary>Simulação sobre um mundo vazio do tamanho dado (IWorldView para os testes de apresentação).</summary>
    public static Simulation View(TileWorld world) => new(Registry(), world);
}
```

`tests/Zyon.Client.Tests/AutoTileTests.cs`:

```csharp
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

public class AutoTileTests
{
    [Fact]
    public void IsolatedTile_HasEmptyMask()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(10, 10, 1);
        using var view = TestData.View(w);

        Assert.Equal(0, AutoTile.Mask(view, 10, 10, TileLayer.Blocks));
    }

    [Fact]
    public void Neighbours_SetTheirBits()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(10, 10, 1);
        w.SetBlock(10, 9, 2);    // norte (material diferente também conecta)
        w.SetBlock(11, 10, 1);   // leste
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.North | AutoTile.East, AutoTile.Mask(view, 10, 10, TileLayer.Blocks));
    }

    [Fact]
    public void WorldEdge_CountsAsConnected()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(0, 63, 1);
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.West | AutoTile.South, AutoTile.Mask(view, 0, 63, TileLayer.Blocks));
    }

    [Fact]
    public void Layers_AreIndependent()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(10, 10, 1);
        w.SetBlock(10, 9, 1);    // bloco não conecta parede
        w.SetWall(9, 10, 1);
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.West, AutoTile.Mask(view, 10, 10, TileLayer.Walls));
    }

    [Fact]
    public void Variation_IsStableAndInRange()
    {
        var seen = new HashSet<int>();
        for (int x = 0; x < 200; x++)
        {
            int v = AutoTile.Variation(x, 7, 3);
            Assert.InRange(v, 0, 2);
            Assert.Equal(v, AutoTile.Variation(x, 7, 3));
            seen.Add(v);
        }
        Assert.Equal(3, seen.Count);
    }
}
```

`tests/Zyon.Client.Tests/TileAtlasLayoutTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class TileAtlasLayoutTests
{
    [Fact]
    public void Dimensions_HoldEveryMaskAndVariationPerMaterial()
    {
        var layout = new TileAtlasLayout(materialCount: 6, variations: 3);

        Assert.Equal(16 * 3 * 16, layout.WidthPx);
        Assert.Equal(6 * 16, layout.HeightPx);
    }

    [Fact]
    public void CellOrigin_RowIsMaterial_ColumnIsMaskThenVariation()
    {
        var layout = new TileAtlasLayout(6, 3);

        Assert.Equal((0, 0), layout.CellOrigin(0, 0, 0));
        Assert.Equal(((5 * 3 + 2) * 16, 4 * 16), layout.CellOrigin(4, 5, 2));
    }

    [Fact]
    public void Uv_IsInsideTheCell_WithASmallInset()
    {
        var layout = new TileAtlasLayout(6, 3);
        var (x, y) = layout.CellOrigin(2, 9, 1);

        var uv = layout.Uv(2, 9, 1);

        Assert.True(uv.U0 > x / (float)layout.WidthPx);
        Assert.True(uv.U1 < (x + 16) / (float)layout.WidthPx);
        Assert.True(uv.V0 > y / (float)layout.HeightPx);
        Assert.True(uv.V1 < (y + 16) / (float)layout.HeightPx);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 3: Implementar**

`src/Zyon.Client/AutoTile.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Client;

public enum TileLayer { Walls, Blocks }

/// <summary>Máscara de 4 vizinhos (N, L, S, O) e variação estável por posição.</summary>
public static class AutoTile
{
    public const int North = 1;
    public const int East = 2;
    public const int South = 4;
    public const int West = 8;
    public const int MaskCount = 16;

    public static int Mask(IWorldView view, int x, int y, TileLayer layer)
    {
        int mask = 0;
        if (Connects(view, x, y - 1, layer)) mask |= North;
        if (Connects(view, x + 1, y, layer)) mask |= East;
        if (Connects(view, x, y + 1, layer)) mask |= South;
        if (Connects(view, x - 1, y, layer)) mask |= West;
        return mask;
    }

    /// <summary>Índice em [0, count) que só depende da posição — quebra a repetição sem piscar.</summary>
    public static int Variation(int x, int y, int count)
    {
        uint h = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u);
        h ^= h >> 13;
        h = unchecked(h * 0x5bd1e995u);
        h ^= h >> 15;
        return (int)(h % (uint)count);
    }

    /// <summary>Fora do mundo conta como conectado; blocos conectam com qualquer bloco sólido.</summary>
    private static bool Connects(IWorldView view, int x, int y, TileLayer layer)
    {
        if ((uint)x >= (uint)view.Width || (uint)y >= (uint)view.Height) return true;
        if (layer == TileLayer.Walls) return view.GetWall(x, y) != 0;
        ushort block = view.GetBlock(x, y);
        return block != 0 && view.Registry.Block(block).Solid;
    }
}
```

`src/Zyon.Client/TileAtlasLayout.cs`:

```csharp
namespace Zyon.Client;

/// <summary>
/// Atlas de 16×16 px: uma linha por material (id), e em cada linha uma célula por (máscara, variação),
/// na ordem máscara × Variations + variação.
/// </summary>
public sealed class TileAtlasLayout(int materialCount, int variations)
{
    public const int TileSize = RenderMath.PixelsPerTile;
    private const float InsetPx = 0.01f;   // evita costuras entre células com filtro nearest

    public int MaterialCount { get; } = materialCount;
    public int Variations { get; } = variations;
    public int WidthPx => AutoTile.MaskCount * Variations * TileSize;
    public int HeightPx => MaterialCount * TileSize;

    public (int X, int Y) CellOrigin(int material, int mask, int variation) =>
        ((mask * Variations + variation) * TileSize, material * TileSize);

    public (float U0, float V0, float U1, float V1) Uv(int material, int mask, int variation)
    {
        var (x, y) = CellOrigin(material, mask, variation);
        float w = WidthPx, h = HeightPx;
        return ((x + InsetPx) / w, (y + InsetPx) / h, (x + TileSize - InsetPx) / w, (y + TileSize - InsetPx) / h);
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test tests/Zyon.Client.Tests` → PASS, 21 testes. Depois `dotnet test` → tudo verde.

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): máscara de autotiling e layout do atlas" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Montagem da malha de um chunk

**Files:**
- Create: `src/Zyon.Client/ChunkMeshBuilder.cs`
- Test: `tests/Zyon.Client.Tests/ChunkMeshBuilderTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Client.Tests/ChunkMeshBuilderTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

public class ChunkMeshBuilderTests
{
    private static readonly TileAtlasLayout Atlas = new(6, 3);

    [Fact]
    public void EmptyChunk_HasNoQuads()
    {
        using var view = TestData.View(new TileWorld(64, 64));
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);

        Assert.Equal(0, data.QuadCount);
        Assert.Empty(data.Indices);
    }

    [Fact]
    public void FullChunk_HasOneQuadPerTile()
    {
        var w = new TileWorld(64, 64);
        for (int y = 32; y < 64; y++)
            for (int x = 32; x < 64; x++)
                w.SetBlock(x, y, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, data);

        Assert.Equal(Chunk.Area, data.QuadCount);
        Assert.Equal(Chunk.Area * 4, data.Vertices.Count);
        Assert.Equal(Chunk.Area * 6, data.Indices.Count);
    }

    [Fact]
    public void Quad_IsPlacedInChunkLocalPixels_WithAtlasUvs()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(35, 33, 2);   // local (3, 1) do chunk (1, 1), isolado
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, data);

        Assert.Equal(1, data.QuadCount);
        Assert.Equal(new Vector2(48f, 16f), data.Vertices[0]);
        Assert.Equal(new Vector2(64f, 32f), data.Vertices[2]);
        var uv = Atlas.Uv(2, 0, AutoTile.Variation(35, 33, Atlas.Variations));
        Assert.Equal(new Vector2(uv.U0, uv.V0), data.Uvs[0]);
        Assert.Equal(new Vector2(uv.U1, uv.V1), data.Uvs[2]);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, data.Indices);
    }

    [Fact]
    public void WallBehindBlock_IsSkipped()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, 1);
        w.SetWall(6, 5, 1);
        w.SetBlock(6, 5, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void Build_ReusesTheOutputBuffers()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(1, 1, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);
        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 3: Implementar**

`src/Zyon.Client/ChunkMeshBuilder.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>Buffers reutilizáveis de uma malha de chunk (posições em pixels relativas à origem do chunk).</summary>
public sealed class ChunkMeshData
{
    public List<Vector2> Vertices { get; } = new();
    public List<Vector2> Uvs { get; } = new();
    public List<int> Indices { get; } = new();
    public int QuadCount => Vertices.Count / 4;

    public void Clear()
    {
        Vertices.Clear();
        Uvs.Clear();
        Indices.Clear();
    }
}

public static class ChunkMeshBuilder
{
    /// <summary>Um quad por tile não vazio da camada. Paredes escondidas atrás de blocos são puladas.</summary>
    public static void Build(IWorldView view, int chunkX, int chunkY, TileLayer layer, TileAtlasLayout atlas, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        const float s = RenderMath.PixelsPerTile;

        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            ushort id = layer == TileLayer.Blocks ? view.GetBlock(x, y) : view.GetWall(x, y);
            if (id == 0) continue;
            if (layer == TileLayer.Walls && view.GetBlock(x, y) != 0) continue;

            int mask = AutoTile.Mask(view, x, y, layer);
            var uv = atlas.Uv(id, mask, AutoTile.Variation(x, y, atlas.Variations));
            float px = lx * s, py = ly * s;
            int b = output.Vertices.Count;

            output.Vertices.Add(new Vector2(px, py));
            output.Vertices.Add(new Vector2(px + s, py));
            output.Vertices.Add(new Vector2(px + s, py + s));
            output.Vertices.Add(new Vector2(px, py + s));
            output.Uvs.Add(new Vector2(uv.U0, uv.V0));
            output.Uvs.Add(new Vector2(uv.U1, uv.V0));
            output.Uvs.Add(new Vector2(uv.U1, uv.V1));
            output.Uvs.Add(new Vector2(uv.U0, uv.V1));
            output.Indices.Add(b);
            output.Indices.Add(b + 1);
            output.Indices.Add(b + 2);
            output.Indices.Add(b);
            output.Indices.Add(b + 2);
            output.Indices.Add(b + 3);
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test tests/Zyon.Client.Tests` → PASS, 26 testes. Depois `dotnet test` → tudo verde.

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): montagem da malha de chunk com autotiling" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Cores provisórias e atlas provisório

**Files:**
- Create: `data/client/visuals.json`, `src/Zyon.Client/ClientVisuals.cs`, `src/Zyon.Client/PlaceholderAtlas.cs`
- Test: `tests/Zyon.Client.Tests/ClientVisualsTests.cs`, `tests/Zyon.Client.Tests/PlaceholderAtlasTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Client.Tests/ClientVisualsTests.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ClientVisualsTests
{
    [Fact]
    public void RealFile_LoadsColorsByIdForEveryBlockWallAndItem()
    {
        var r = TestData.Registry();

        var v = ClientVisuals.LoadFromDirectory(TestData.DataDir, r);

        Assert.Equal(r.Blocks.Count, v.BlockColors.Count);
        Assert.Null(v.BlockColors[0]);
        Assert.NotNull(v.BlockColors[r.BlockId("dirt")]);
        Assert.NotNull(v.WallColors[r.WallId("stone_wall")]);
        Assert.NotEqual(ClientVisuals.FallbackColor, v.ItemColor(r.ItemId("basic_pickaxe")));
    }

    [Fact]
    public void ItemWithoutOwnColor_UsesThePlacedBlockColor()
    {
        var r = TestData.Registry();

        var v = ClientVisuals.Load("""{"blocks": {"dirt":"#102030","stone":"#000000","deep_stone":"#000000","ore_a":"#000000","ore_b":"#000000"}, "walls": {"dirt_wall":"#000000","stone_wall":"#000000"}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", r);

        Assert.Equal(new Rgb(0x10, 0x20, 0x30), v.ItemColor(r.ItemId("dirt")));
        Assert.Equal(ClientVisuals.FallbackColor, v.ItemColor(r.ItemId("basic_pickaxe")));
    }

    [Theory]
    [InlineData("""{"blocks": {}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "dirt")]
    [InlineData("""{"blocks": {"dirt":"#zz0000"}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "#zz0000")]
    [InlineData("""{"blocks": {"mithril":"#000000"}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "mithril")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json, TestData.Registry()));

        Assert.Contains("visuals.json", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }
}
```

`tests/Zyon.Client.Tests/PlaceholderAtlasTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class PlaceholderAtlasTests
{
    private static readonly Rgb Brown = new(100, 80, 60);

    private static Rgb Pixel(byte[] rgba, TileAtlasLayout layout, int x, int y)
    {
        int i = (y * layout.WidthPx + x) * 4;
        Assert.Equal(255, rgba[i + 3]);
        return new Rgb(rgba[i], rgba[i + 1], rgba[i + 2]);
    }

    [Fact]
    public void Buffer_HasRgbaForTheWholeAtlas()
    {
        var layout = new TileAtlasLayout(2, 3);

        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);

        Assert.Equal(layout.WidthPx * layout.HeightPx * 4, rgba.Length);
    }

    [Fact]
    public void CellCenter_IsCloseToTheBaseColor()
    {
        var layout = new TileAtlasLayout(2, 3);
        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);
        var (x, y) = layout.CellOrigin(1, AutoTile.MaskCount - 1, 0);

        var p = Pixel(rgba, layout, x + 8, y + 8);

        Assert.InRange(p.R, Brown.R - 12, Brown.R + 12);
        Assert.InRange(p.G, Brown.G - 12, Brown.G + 12);
    }

    [Fact]
    public void ExposedSide_IsDarker_ConnectedSideIsNot()
    {
        var layout = new TileAtlasLayout(2, 3);
        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);
        var (x, y) = layout.CellOrigin(1, AutoTile.South, 0);   // só o sul conectado

        var top = Pixel(rgba, layout, x + 8, y);         // norte exposto
        var bottom = Pixel(rgba, layout, x + 8, y + 15); // sul conectado

        Assert.True(top.R < Brown.R - 20, $"topo {top}");
        Assert.True(bottom.R > Brown.R - 15, $"base {bottom}");
    }

    [Fact]
    public void MaterialWithoutColor_IsTransparent()
    {
        var layout = new TileAtlasLayout(2, 3);

        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);

        Assert.Equal(0, rgba[3]);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 3: Criar `data/client/visuals.json`**

```json
{
  "blocks": {
    "dirt": "#6b4a2b",
    "stone": "#7a7a7a",
    "deep_stone": "#4b4b58",
    "ore_a": "#b0652e",
    "ore_b": "#3aa39b"
  },
  "walls": {
    "dirt_wall": "#3a2818",
    "stone_wall": "#353538"
  },
  "items": {
    "basic_pickaxe": "#c8c8c8"
  },
  "player": "#e0b080",
  "sky": "#7f9fbf"
}
```

- [ ] **Step 4: Implementar**

`src/Zyon.Client/ClientVisuals.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Client;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static bool TryParse(string? hex, out Rgb color)
    {
        color = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        if (!uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
        color = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}

/// <summary>Cores provisórias por id (até existir arte). Lidas de data/client/visuals.json.</summary>
public sealed class ClientVisuals
{
    public const string FileName = "visuals.json";
    public static readonly Rgb FallbackColor = new(200, 0, 200);

    private readonly Rgb?[] itemColors;

    private ClientVisuals(Rgb?[] blocks, Rgb?[] walls, Rgb?[] items, Rgb player, Rgb sky)
    {
        BlockColors = blocks;
        WallColors = walls;
        itemColors = items;
        Player = player;
        Sky = sky;
    }

    public IReadOnlyList<Rgb?> BlockColors { get; }
    public IReadOnlyList<Rgb?> WallColors { get; }
    public Rgb Player { get; }
    public Rgb Sky { get; }

    public Rgb ItemColor(ushort itemId) => itemColors[itemId] ?? FallbackColor;

    public static ClientVisuals LoadFromDirectory(string dataDir, Registry registry)
    {
        var path = Path.Combine(dataDir, "client", FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), registry);
    }

    public static ClientVisuals Load(string json, Registry registry)
    {
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, RegistryOptions) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        var blocks = new Rgb?[registry.Blocks.Count];
        foreach (var (name, hex) in dto.Blocks ?? new Dictionary<string, string>())
            blocks[registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' não existe")] = Parse(hex, $"blocks.{name}");
        for (int id = 1; id < blocks.Length; id++)
            if (blocks[id] is null) throw Fail($"falta a cor do bloco '{registry.Block((ushort)id).Name}'");

        var walls = new Rgb?[registry.Walls.Count];
        foreach (var (name, hex) in dto.Walls ?? new Dictionary<string, string>())
            walls[registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' não existe")] = Parse(hex, $"walls.{name}");
        for (int id = 1; id < walls.Length; id++)
            if (walls[id] is null) throw Fail($"falta a cor da parede '{registry.Wall((ushort)id).Name}'");

        var items = new Rgb?[registry.Items.Count];
        foreach (var (name, hex) in dto.Items ?? new Dictionary<string, string>())
            items[registry.TryGetItemId(name, out var id) ? id : throw Fail($"item '{name}' não existe")] = Parse(hex, $"items.{name}");
        for (int id = 1; id < items.Length; id++)
        {
            var def = registry.Item((ushort)id);
            if (items[id] is null && def.PlacesBlockId != 0)
                items[id] = blocks[def.PlacesBlockId];
        }

        return new ClientVisuals(blocks, walls, items, Parse(dto.Player, "player"), Parse(dto.Sky, "sky"));
    }

    private static Rgb Parse(string? hex, string field) =>
        Rgb.TryParse(hex, out var c) ? c : throw Fail($"'{field}': cor inválida '{hex}' (use #rrggbb)");

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private static readonly JsonSerializerOptions RegistryOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private sealed class Dto
    {
        public Dictionary<string, string>? Blocks { get; set; }
        public Dictionary<string, string>? Walls { get; set; }
        public Dictionary<string, string>? Items { get; set; }
        public string? Player { get; set; }
        public string? Sky { get; set; }
    }
}
```

`src/Zyon.Client/PlaceholderAtlas.cs`:

```csharp
namespace Zyon.Client;

/// <summary>
/// Gera um atlas provisório em RGBA8: cor base com ruído estável por pixel e borda escura de 2 px
/// nos lados sem vizinho (segundo a máscara). Materiais sem cor ficam transparentes.
/// </summary>
public static class PlaceholderAtlas
{
    private const int Border = 2;
    private const float BorderShade = 0.6f;
    private const int NoiseAmplitude = 8;

    public static byte[] BuildRgba(TileAtlasLayout layout, IReadOnlyList<Rgb?> colorsById)
    {
        var rgba = new byte[layout.WidthPx * layout.HeightPx * 4];
        int size = TileAtlasLayout.TileSize;

        for (int material = 0; material < layout.MaterialCount; material++)
        {
            if (material >= colorsById.Count || colorsById[material] is not { } baseColor) continue;
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int variation = 0; variation < layout.Variations; variation++)
            {
                var (ox, oy) = layout.CellOrigin(material, mask, variation);
                for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    int noise = Noise(material, variation, px, py);
                    bool edge = (py < Border && (mask & AutoTile.North) == 0)
                                || (py >= size - Border && (mask & AutoTile.South) == 0)
                                || (px < Border && (mask & AutoTile.West) == 0)
                                || (px >= size - Border && (mask & AutoTile.East) == 0);
                    float shade = edge ? BorderShade : 1f;
                    int i = ((oy + py) * layout.WidthPx + ox + px) * 4;
                    rgba[i] = Channel(baseColor.R, noise, shade);
                    rgba[i + 1] = Channel(baseColor.G, noise, shade);
                    rgba[i + 2] = Channel(baseColor.B, noise, shade);
                    rgba[i + 3] = 255;
                }
            }
        }
        return rgba;
    }

    private static byte Channel(byte value, int noise, float shade) =>
        (byte)Math.Clamp((int)((value + noise) * shade), 0, 255);

    private static int Noise(int material, int variation, int px, int py)
    {
        uint h = unchecked((uint)(material * 7919 + variation * 104729 + px * 31 + py * 1009));
        h ^= h >> 16;
        h = unchecked(h * 0x7feb352du);
        h ^= h >> 15;
        return (int)(h % (2 * NoiseAmplitude + 1)) - NoiseAmplitude;
    }
}
```

- [ ] **Step 5: Rodar e confirmar que passa**

Run: `dotnet test tests/Zyon.Client.Tests` → PASS, 35 testes. Depois `dotnet test` → tudo verde.

- [ ] **Step 6: Commit**

```bash
git add data/client src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): cores provisórias e atlas provisório gerado em código" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Opções de lançamento e esqueleto do projeto Godot

**Files:**
- Create: `src/Zyon.Client/LaunchOptions.cs`, `tests/Zyon.Client.Tests/LaunchOptionsTests.cs`
- Create: `src/Zyon.Game/project.godot`, `src/Zyon.Game/Zyon.Game.csproj`, `src/Zyon.Game/icon.svg`, `src/Zyon.Game/scenes/Game.tscn`, `src/Zyon.Game/GameRoot.cs`, `src/Zyon.Game/DataPaths.cs`, `src/Zyon.Game/GodotLog.cs`, `src/Zyon.Game/GodotConvert.cs`
- Create: `tools/smoke-test.sh`
- Modify: `Zyon.slnx`

- [ ] **Step 1: Testes das opções de lançamento (falham)**

`tests/Zyon.Client.Tests/LaunchOptionsTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void Defaults_AreDevWorldNoSmokeTest()
    {
        var o = LaunchOptions.Parse([]);

        Assert.Equal((1050, 300), (o.Width, o.Height));
        Assert.Null(o.Seed);
        Assert.False(o.SmokeTest);
    }

    [Fact]
    public void Parses_SizeSeedAndSmokeTest()
    {
        var o = LaunchOptions.Parse(["--size", "small", "--seed", "42", "--smoke-test", "--frames", "120"]);

        Assert.Equal((4200, 1200), (o.Width, o.Height));
        Assert.Equal(42, o.Seed);
        Assert.True(o.SmokeTest);
        Assert.Equal(120, o.SmokeFrames);
    }

    [Theory]
    [InlineData("--size", "huge")]
    [InlineData("--seed", "abc")]
    [InlineData("--frames", "0")]
    [InlineData("--bogus", "1")]
    public void InvalidArguments_Throw(string flag, string value)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse([flag, value]));
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar `src/Zyon.Client/LaunchOptions.cs`**

```csharp
using System.Globalization;
using Zyon.Core.WorldGen;

namespace Zyon.Client;

/// <summary>Argumentos do jogo (depois de "--" na linha de comando do Godot).</summary>
public sealed record LaunchOptions(int Width, int Height, int? Seed, bool SmokeTest, int SmokeFrames)
{
    public static readonly (int Width, int Height) DevSize = (1050, 300);

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var (width, height) = DevSize;
        int? seed = null;
        bool smoke = false;
        int frames = 600;

        for (int i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"'{args[i]}' precisa de um valor");
            switch (args[i])
            {
                case "--size":
                    (width, height) = Next() switch
                    {
                        "dev" => DevSize,
                        "small" => WorldSizes.Dimensions(WorldSize.Small),
                        "medium" => WorldSizes.Dimensions(WorldSize.Medium),
                        "large" => WorldSizes.Dimensions(WorldSize.Large),
                        var other => throw new ArgumentException($"tamanho desconhecido '{other}' (dev, small, medium, large)"),
                    };
                    break;
                case "--seed":
                    seed = int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s)
                        ? s : throw new ArgumentException("--seed precisa de um inteiro");
                    break;
                case "--smoke-test":
                    smoke = true;
                    break;
                case "--frames":
                    frames = int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f > 0
                        ? f : throw new ArgumentException("--frames precisa de um inteiro > 0");
                    break;
                default:
                    throw new ArgumentException($"argumento desconhecido '{args[i]}'");
            }
        }

        return new LaunchOptions(width, height, seed, smoke, frames);
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → PASS, 41 testes.

- [ ] **Step 3: Projeto Godot**

`src/Zyon.Game/project.godot`:

```ini
config_version=5

[application]

config/name="Zyon"
run/main_scene="res://scenes/Game.tscn"
config/features=PackedStringArray("4.7", "C#")
config/icon="res://icon.svg"

[display]

window/size/viewport_width=640
window/size/viewport_height=360
window/size/window_width_override=1280
window/size/window_height_override=720
window/stretch/mode="viewport"
window/stretch/scale_mode="integer"

[dotnet]

project/assembly_name="Zyon.Game"

[rendering]

textures/canvas_textures/default_texture_filter=0
2d/snap/snap_2d_transforms_to_pixel=true
```

`src/Zyon.Game/Zyon.Game.csproj`:

```xml
<Project Sdk="Godot.NET.Sdk/4.7.2">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Zyon.Game</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Zyon.Core\Zyon.Core.csproj" />
    <ProjectReference Include="..\Zyon.Client\Zyon.Client.csproj" />
  </ItemGroup>
</Project>
```

(O `Directory.Build.props` da raiz também vale aqui — `Nullable` e `TreatWarningsAsErrors`. Se os geradores de código do Godot emitirem avisos que não dependem do nosso código, adicione `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` **só** neste csproj e reporte quais avisos foram.)

`src/Zyon.Game/icon.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect width="128" height="128" fill="#6b4a2b"/><rect y="0" width="128" height="40" fill="#7f9fbf"/></svg>
```

`src/Zyon.Game/scenes/Game.tscn`:

```ini
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://GameRoot.cs" id="1"]

[node name="Game" type="Node2D"]
script = ExtResource("1")
```

`src/Zyon.Game/DataPaths.cs`:

```csharp
using Godot;

namespace Zyon.Game;

/// <summary>Pasta data/: "res://data" num jogo exportado, ou a pasta data/ do repositório em desenvolvimento.</summary>
public static class DataPaths
{
    public static string Resolve()
    {
        string exported = ProjectSettings.GlobalizePath("res://data");
        if (System.IO.Directory.Exists(exported)) return exported;
        string dev = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "data"));
        if (System.IO.Directory.Exists(dev)) return dev;
        throw new System.IO.DirectoryNotFoundException($"Pasta data/ não encontrada (procurei '{exported}' e '{dev}')");
    }
}
```

`src/Zyon.Game/GodotLog.cs`:

```csharp
using Godot;
using Zyon.Core.Logging;

namespace Zyon.Game;

public sealed class GodotLog : ILog
{
    public void Debug(string message)
    {
        if (OS.IsDebugBuild()) GD.Print($"[debug] {message}");
    }

    public void Info(string message) => GD.Print(message);
    public void Warn(string message) => GD.PushWarning(message);
    public void Error(string message) => GD.PushError(message);
}
```

`src/Zyon.Game/GodotConvert.cs`:

```csharp
using Zyon.Client;

namespace Zyon.Game;

/// <summary>Conversões entre tipos do Client/.NET e do Godot.</summary>
public static class GodotConvert
{
    public static Godot.Vector2 ToGodot(this System.Numerics.Vector2 v) => new(v.X, v.Y);

    public static System.Numerics.Vector2 ToNumerics(this Godot.Vector2 v) => new(v.X, v.Y);

    public static Godot.Color ToGodot(this Rgb c, float alpha = 1f) => new(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
}
```

`src/Zyon.Game/GameRoot.cs` (esqueleto — as próximas tarefas acrescentam renderização, câmera e controles):

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Game;

/// <summary>Raiz da cena de jogo: cria a sessão e conecta renderização, câmera, controles e HUD.</summary>
public partial class GameRoot : Node2D
{
    private IGameSession? session;
    private LaunchOptions options = null!;
    private int frame;

    public override void _Ready()
    {
        options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        string dataDir = DataPaths.Resolve();
        var registry = RegistryLoader.LoadFromDirectory(dataDir);
        var visuals = ClientVisuals.LoadFromDirectory(dataDir, registry);
        int seed = options.Seed ?? System.Environment.TickCount;
        var generated = new WorldGenerator(registry, WorldGenConfigLoader.LoadFromDirectory(dataDir))
            .Generate(options.Width, options.Height, seed);
        var world = WorldSave.FromGenerated(generated);
        var sim = world.CreateSimulation(registry, new GodotLog());
        int player = PlayerSave.New("Dev", registry).SpawnInto(sim, world.PlayerPosition("Dev", registry.Physics));
        session = new LocalSession(sim, player);
        RenderingServer.SetDefaultClearColor(visuals.Sky.ToGodot());
        GD.Print($"Zyon: mundo {options.Width}×{options.Height}, seed {seed}");
    }

    public override void _Process(double delta)
    {
        if (session is null) return;
        try
        {
            session.Update(delta);
            session.DrainEvents();
            if (options.SmokeTest && ++frame >= options.SmokeFrames)
            {
                GD.Print($"SMOKE OK: {frame} frames, tick {session.World.Tick}");
                GetTree().Quit(0);
            }
        }
        catch (System.Exception ex) when (options.SmokeTest)
        {
            GD.PrintErr($"SMOKE FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    public override void _ExitTree() => session?.Dispose();
}
```

Adicionar o projeto à solução: `dotnet sln Zyon.slnx add src/Zyon.Game/Zyon.Game.csproj`.

- [ ] **Step 4: Script do smoke test**

`tools/smoke-test.sh`:

```bash
#!/usr/bin/env bash
# Compila o jogo e roda N frames em modo headless. Sai com 0 só se o jogo imprimir "SMOKE OK".
set -uo pipefail
GODOT="${GODOT:-/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64_console.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/src/Zyon.Game"
FRAMES="${1:-600}"

dotnet build "$GAME/Zyon.Game.csproj" -nologo -v q || exit 1
if [ ! -d "$GAME/.godot" ]; then
  "$GODOT" --headless --path "$GAME" --import > /dev/null 2>&1
fi
out="$("$GODOT" --headless --path "$GAME" -- --smoke-test --frames "$FRAMES" --size dev --seed 1 2>&1)"
code=$?
echo "$out" | tail -n 25
if [ "$code" -ne 0 ] || ! echo "$out" | grep -q "SMOKE OK"; then
  echo "smoke test FALHOU (código $code)"
  exit 1
fi
echo "smoke test OK"
```

Run: `chmod +x tools/smoke-test.sh && tools/smoke-test.sh 120`
Expected: termina com `SMOKE OK: 120 frames, tick ...` e `smoke test OK`.

- [ ] **Step 5: Suíte e commit**

Run: `dotnet test` → tudo verde (o projeto Godot compila junto com a solução).

```bash
git add Zyon.slnx src/Zyon.Client src/Zyon.Game tests/Zyon.Client.Tests tools
git commit -m "feat(game): projeto Godot, opções de lançamento e smoke test headless" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Renderização dos tiles e câmera

**Files:**
- Create: `src/Zyon.Game/Rendering/ChunkNode.cs`, `src/Zyon.Game/Rendering/WorldRenderer.cs`, `src/Zyon.Game/GameCamera.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`

- [ ] **Step 1: Implementar**

`src/Zyon.Game/Rendering/ChunkNode.cs`:

```csharp
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>Um chunk na tela: malha de paredes (mais escura) e malha de blocos.</summary>
public partial class ChunkNode : Node2D
{
    private static readonly Color WallTint = new(0.55f, 0.55f, 0.55f);

    private readonly MeshInstance2D walls = new();
    private readonly MeshInstance2D blocks = new();

    public void Init(Texture2D wallAtlas, Texture2D blockAtlas)
    {
        walls.Texture = wallAtlas;
        walls.Modulate = WallTint;
        blocks.Texture = blockAtlas;
        AddChild(walls);
        AddChild(blocks);
    }

    public void SetWalls(ChunkMeshData data) => walls.Mesh = ToMesh(data);

    public void SetBlocks(ChunkMeshData data) => blocks.Mesh = ToMesh(data);

    private static ArrayMesh? ToMesh(ChunkMeshData data)
    {
        if (data.Indices.Count == 0) return null;
        var vertices = new Vector2[data.Vertices.Count];
        var uvs = new Vector2[data.Uvs.Count];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = data.Vertices[i].ToGodot();
            uvs[i] = data.Uvs[i].ToGodot();
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
```

`src/Zyon.Game/Rendering/WorldRenderer.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>
/// Mantém nós só para os chunks visíveis (+ margem), reaproveitando-os. Chunks novos na tela são
/// montados na hora; chunks alterados (TileChanged) são reconstruídos com orçamento por frame.
/// </summary>
public partial class WorldRenderer : Node2D
{
    private const int MarginChunks = 1;
    private const int RebuildBudgetPerFrame = 6;
    private const int Variations = 3;

    private readonly Dictionary<(int, int), ChunkNode> active = new();
    private readonly Stack<ChunkNode> pool = new();
    private readonly ChunkMeshData scratch = new();
    private IWorldView view = null!;
    private TileAtlasLayout blockLayout = null!;
    private TileAtlasLayout wallLayout = null!;
    private Texture2D blockAtlas = null!;
    private Texture2D wallAtlas = null!;
    private DirtyChunkTracker dirty = null!;
    private int chunksX;
    private int chunksY;

    public int ActiveChunkCount => active.Count;

    public void Init(IWorldView worldView, ClientVisuals visuals)
    {
        view = worldView;
        chunksX = (view.Width + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        chunksY = (view.Height + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        dirty = new DirtyChunkTracker(chunksX, chunksY);
        blockLayout = new TileAtlasLayout(view.Registry.Blocks.Count, Variations);
        wallLayout = new TileAtlasLayout(view.Registry.Walls.Count, Variations);
        blockAtlas = BuildAtlas(blockLayout, visuals.BlockColors);
        wallAtlas = BuildAtlas(wallLayout, visuals.WallColors);
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged t)
            dirty.MarkTile(t.X, t.Y);
    }

    public void UpdateVisible(Rect2 visiblePixels)
    {
        var range = ChunkVisibility.Range(visiblePixels.Position.X, visiblePixels.Position.Y,
            visiblePixels.Size.X, visiblePixels.Size.Y, MarginChunks, chunksX, chunksY);

        foreach (var key in active.Keys.Where(k => !range.Contains(k.Item1, k.Item2)).ToList())
        {
            var node = active[key];
            node.Visible = false;
            pool.Push(node);
            active.Remove(key);
        }

        for (int cy = range.MinY; cy <= range.MaxY; cy++)
        for (int cx = range.MinX; cx <= range.MaxX; cx++)
        {
            if (active.ContainsKey((cx, cy))) continue;
            var node = pool.Count > 0 ? pool.Pop() : CreateNode();
            node.Position = new Vector2(cx * ChunkVisibility.ChunkPixels, cy * ChunkVisibility.ChunkPixels);
            node.Visible = true;
            Rebuild(node, cx, cy);
            active[(cx, cy)] = node;
        }

        for (int built = 0; built < RebuildBudgetPerFrame && dirty.TryTake(out var c);)
        {
            if (!active.TryGetValue(c, out var node)) continue;   // fora da tela: será montado ao aparecer
            Rebuild(node, c.Cx, c.Cy);
            built++;
        }
    }

    private ChunkNode CreateNode()
    {
        var node = new ChunkNode();
        node.Init(wallAtlas, blockAtlas);
        AddChild(node);
        return node;
    }

    private void Rebuild(ChunkNode node, int cx, int cy)
    {
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Walls, wallLayout, scratch);
        node.SetWalls(scratch);
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Blocks, blockLayout, scratch);
        node.SetBlocks(scratch);
    }

    private static Texture2D BuildAtlas(TileAtlasLayout layout, IReadOnlyList<Rgb?> colors)
    {
        byte[] rgba = PlaceholderAtlas.BuildRgba(layout, colors);
        var image = Image.CreateFromData(layout.WidthPx, layout.HeightPx, false, Image.Format.Rgba8, rgba);
        return ImageTexture.CreateFromImage(image);
    }
}
```

`src/Zyon.Game/GameCamera.cs`:

```csharp
using Godot;

namespace Zyon.Game;

/// <summary>Segue o jogador com suavização e fica presa às bordas do mundo.</summary>
public partial class GameCamera : Camera2D
{
    public void Init(int worldWidthTiles, int worldHeightTiles)
    {
        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = worldWidthTiles * Zyon.Client.RenderMath.PixelsPerTile;
        LimitBottom = worldHeightTiles * Zyon.Client.RenderMath.PixelsPerTile;
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 10f;
    }

    public override void _Ready() => MakeCurrent();

    public void Follow(Vector2 targetPixels) => Position = targetPixels;

    /// <summary>Retângulo do mundo (em pixels) que está na tela.</summary>
    public Rect2 VisiblePixels()
    {
        var size = GetViewportRect().Size / Zoom;
        return new Rect2(GetScreenCenterPosition() - size / 2, size);
    }
}
```

- [ ] **Step 2: Ligar no `GameRoot`**

Em `GameRoot.cs`: adicionar `using Zyon.Game.Rendering;`, os campos `private WorldRenderer worldRenderer = null!; private GameCamera camera = null!; private int playerId;`, guardar `playerId = player;` em `_Ready` e, ao final de `_Ready`:

```csharp
        worldRenderer = new WorldRenderer();
        AddChild(worldRenderer);
        worldRenderer.Init(session.World, visuals);
        camera = new GameCamera();
        AddChild(camera);
        camera.Init(options.Width, options.Height);
        if (session.World.TryGetEntityView(playerId, out var start))
        {
            camera.Follow(RenderMath.ToPixels(start.Position).ToGodot());
            camera.ResetSmoothing();
        }
```

Em `_Process`, trocar `session.DrainEvents();` por:

```csharp
            foreach (var gameEvent in session.DrainEvents())
                worldRenderer.OnEvent(gameEvent);
            if (session.World.TryGetEntityView(playerId, out var me))
                camera.Follow(RenderMath.ToPixels(RenderMath.Interpolate(me.PreviousPosition, me.Position, session.InterpolationAlpha)).ToGodot());
            worldRenderer.UpdateVisible(camera.VisiblePixels());
```

e, no bloco do smoke test, imprimir também os chunks ativos: `GD.Print($"SMOKE OK: {frame} frames, tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}");`.

- [ ] **Step 3: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0 erros. Run: `tools/smoke-test.sh 120` → `SMOKE OK: ... chunks N` com N > 0.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): renderização dos chunks com atlas provisório e câmera" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Jogador, itens e controles

**Files:**
- Create: `src/Zyon.Game/Rendering/EntityRenderer.cs`, `src/Zyon.Game/PlayerController.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`

- [ ] **Step 1: Implementar**

`src/Zyon.Game/Rendering/EntityRenderer.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using Zyon.Client;
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>Jogadores como retângulos provisórios; itens caídos em lote com MultiMesh (um draw call).</summary>
public partial class EntityRenderer : Node2D
{
    private const float ItemPixels = 12f;

    private readonly List<EntityView> buffer = new();
    private readonly Dictionary<int, ColorRect> players = new();
    private readonly HashSet<int> seen = new();
    private IWorldView view = null!;
    private ClientVisuals visuals = null!;
    private MultiMesh items = null!;

    public void Init(IWorldView worldView, ClientVisuals clientVisuals)
    {
        view = worldView;
        visuals = clientVisuals;
        items = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = new QuadMesh { Size = new Vector2(ItemPixels, ItemPixels) },
        };
        AddChild(new MultiMeshInstance2D { Multimesh = items });
    }

    public void Refresh(float alpha)
    {
        view.CopyEntities(buffer);
        seen.Clear();
        int itemCount = 0;
        foreach (var e in buffer)
            if (e.Kind == EntityKind.DroppedItem) itemCount++;
        if (items.InstanceCount < itemCount)
            items.InstanceCount = Mathf.Max(itemCount, items.InstanceCount * 2);
        items.VisibleInstanceCount = itemCount;

        int i = 0;
        foreach (var e in buffer)
        {
            var center = RenderMath.ToPixels(RenderMath.Interpolate(e.PreviousPosition, e.Position, alpha)).ToGodot();
            if (e.Kind == EntityKind.DroppedItem)
            {
                items.SetInstanceTransform2D(i, new Transform2D(0f, center));
                items.SetInstanceColor(i, visuals.ItemColor(e.Item.ItemId).ToGodot());
                i++;
                continue;
            }

            seen.Add(e.NetworkId);
            if (!players.TryGetValue(e.NetworkId, out var rect))
            {
                rect = new ColorRect { Color = visuals.Player.ToGodot() };
                AddChild(rect);
                players[e.NetworkId] = rect;
            }
            var size = RenderMath.ToPixels(e.Size).ToGodot();
            rect.Size = size;
            rect.Position = center - size / 2;
        }

        foreach (var id in new List<int>(players.Keys))
        {
            if (seen.Contains(id)) continue;
            players[id].QueueFree();
            players.Remove(id);
        }
    }
}
```

`src/Zyon.Game/PlayerController.cs`:

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Lê teclado e mouse e fala com a sessão: A/D andam, Espaço pula, botão esquerdo usa o item,
/// 1–0 e a roda escolhem o slot, Q solta o item selecionado. ScriptedInput substitui o teclado (smoke test).
/// </summary>
public partial class PlayerController : Node
{
    private IGameSession session = null!;

    public PlayerInput? ScriptedInput { get; set; }

    public void Init(IGameSession gameSession) => session = gameSession;

    public void Poll(Vector2 mouseWorldPixels)
    {
        if (ScriptedInput is { } scripted)
        {
            session.SetInput(scripted);
            return;
        }
        float move = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);
        bool jump = Input.IsPhysicalKeyPressed(Key.Space);
        bool primary = Input.IsMouseButtonPressed(MouseButton.Left);
        var (tx, ty) = RenderMath.TileAtPixel(mouseWorldPixels.ToNumerics());
        session.SetInput(new PlayerInput(move, jump, primary, tx, ty));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        int id = session.LocalPlayerId;
        int selected = session.World.GetSelectedSlot(id);
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                int slot = key.PhysicalKeycode switch
                {
                    Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4,
                    Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7, Key.Key9 => 8, Key.Key0 => 9,
                    _ => -1,
                };
                if (slot >= 0) session.Send(new SelectHotbarSlot(id, slot));
                if (key.PhysicalKeycode == Key.Q) session.Send(new DropItem(id, selected));
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                session.Send(new SelectHotbarSlot(id, (selected + 9) % 10));
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                session.Send(new SelectHotbarSlot(id, (selected + 1) % 10));
                break;
        }
    }
}
```

- [ ] **Step 2: Ligar no `GameRoot`**

Adicionar campos `private EntityRenderer entityRenderer = null!; private PlayerController controller = null!;`. Em `_Ready`, depois do `worldRenderer`:

```csharp
        entityRenderer = new EntityRenderer();
        AddChild(entityRenderer);
        entityRenderer.Init(session.World, visuals);
        controller = new PlayerController();
        AddChild(controller);
        controller.Init(session);
```

Em `_Process`, **antes** de `session.Update(delta);`: `controller.Poll(GetGlobalMousePosition());`. Depois de `camera.Follow(...)`: `entityRenderer.Refresh(session.InterpolationAlpha);`.

- [ ] **Step 3: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0 erros. Run: `tools/smoke-test.sh 120` → `SMOKE OK`.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): jogador, itens caídos e controles" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Tile mirado, rachaduras e hotbar

**Files:**
- Create: `src/Zyon.Game/Rendering/WorldOverlay.cs`, `src/Zyon.Game/Hud/Hotbar.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`

- [ ] **Step 1: Implementar**

`src/Zyon.Game/Rendering/WorldOverlay.cs`:

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>Desenha no mundo o contorno do tile mirado (se alcançável) e as rachaduras da mineração.</summary>
public partial class WorldOverlay : Node2D
{
    private static readonly Color Highlight = new(1f, 1f, 1f, 0.8f);
    private static readonly Color Crack = new(0f, 0f, 0f, 1f);
    private const float Tile = RenderMath.PixelsPerTile;

    private IWorldView view = null!;
    private int playerId;
    private (int X, int Y)? aimed;
    private (int X, int Y, float Progress)? mining;

    public void Init(IWorldView worldView, int localPlayerId)
    {
        view = worldView;
        playerId = localPlayerId;
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is MiningProgressChanged m && m.PlayerId == playerId)
            mining = m.Progress > 0f ? (m.X, m.Y, m.Progress) : null;
    }

    public void Refresh(Vector2 mouseWorldPixels)
    {
        var tile = RenderMath.TileAtPixel(mouseWorldPixels.ToNumerics());
        aimed = InReach(tile.X, tile.Y) ? tile : null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (aimed is { } a)
            DrawRect(new Rect2(a.X * Tile, a.Y * Tile, Tile, Tile), Highlight, filled: false, width: 1f);
        if (mining is { } m)
            DrawRect(new Rect2(m.X * Tile, m.Y * Tile, Tile, Tile), new Color(Crack, 0.6f * m.Progress));
    }

    /// <summary>Mesma regra do Core (distância do centro do jogador ao centro do tile), só para o visual.</summary>
    private bool InReach(int x, int y)
    {
        if (!view.TryGetEntityView(playerId, out var me)) return false;
        var tileCenter = new System.Numerics.Vector2(x + 0.5f, y + 0.5f);
        return System.Numerics.Vector2.Distance(me.Position, tileCenter) <= view.Registry.Physics.ReachTiles;
    }
}
```

`src/Zyon.Game/Hud/Hotbar.cs`:

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Game.Hud;

/// <summary>Hotbar provisória: 10 slots com a cor do item, quantidade e o slot selecionado destacado.</summary>
public partial class Hotbar : CanvasLayer
{
    private const int SlotSize = 20;
    private const int Gap = 2;
    private static readonly Color SlotBack = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Selected = new(1f, 0.85f, 0.3f);

    private readonly ColorRect[] backs = new ColorRect[Inventory.HotbarSize];
    private readonly ColorRect[] icons = new ColorRect[Inventory.HotbarSize];
    private readonly Label[] counts = new Label[Inventory.HotbarSize];
    private IWorldView view = null!;
    private ClientVisuals visuals = null!;
    private int playerId;

    public void Init(IWorldView worldView, ClientVisuals clientVisuals, int localPlayerId)
    {
        view = worldView;
        visuals = clientVisuals;
        playerId = localPlayerId;
        const int total = Inventory.HotbarSize * (SlotSize + Gap) - Gap;
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var x = (640 - total) / 2 + i * (SlotSize + Gap);
            backs[i] = new ColorRect { Position = new Vector2(x, 360 - SlotSize - 4), Size = new Vector2(SlotSize, SlotSize), Color = SlotBack };
            icons[i] = new ColorRect { Position = new Vector2(5, 5), Size = new Vector2(SlotSize - 10, SlotSize - 10) };
            counts[i] = new Label { Position = new Vector2(1, SlotSize - 12) };
            counts[i].AddThemeFontSizeOverride("font_size", 8);
            backs[i].AddChild(icons[i]);
            backs[i].AddChild(counts[i]);
            AddChild(backs[i]);
        }
    }

    public void Refresh()
    {
        var inventory = view.GetInventory(playerId);
        if (inventory is null) return;
        int selected = view.GetSelectedSlot(playerId);
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var stack = inventory[i];
            icons[i].Visible = !stack.IsEmpty;
            if (!stack.IsEmpty) icons[i].Color = visuals.ItemColor(stack.ItemId).ToGodot();
            counts[i].Text = stack.Count > 1 ? stack.Count.ToString() : "";
            backs[i].Color = i == selected ? Selected : SlotBack;
        }
    }
}
```

- [ ] **Step 2: Ligar no `GameRoot`**

Adicionar `using Zyon.Game.Hud;` e os campos `private WorldOverlay overlay = null!; private Hotbar hotbar = null!;`. Em `_Ready`, depois do `entityRenderer`:

```csharp
        overlay = new WorldOverlay();
        AddChild(overlay);
        overlay.Init(session.World, playerId);
        hotbar = new Hotbar();
        AddChild(hotbar);
        hotbar.Init(session.World, visuals, playerId);
```

No laço de eventos de `_Process`, também chamar `overlay.OnEvent(gameEvent);`. Depois de `entityRenderer.Refresh(...)`:

```csharp
            overlay.Refresh(GetGlobalMousePosition());
            hotbar.Refresh();
```

- [ ] **Step 3: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0 erros. Run: `tools/smoke-test.sh 120` → `SMOKE OK`.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): destaque do tile mirado, rachaduras e hotbar" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Smoke test com jogabilidade roteirizada, docs e verificação

**Files:**
- Create: `src/Zyon.Game/SmokeTest.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`, `CLAUDE.md`

- [ ] **Step 1: Implementar `src/Zyon.Game/SmokeTest.cs`**

```csharp
using Godot;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Roteiro do smoke test: parado, anda para a direita, pula, minera o chão à frente e confere que o
/// jogador continua no mundo, que algum tile mudou e que a renderização tem chunks ativos.
/// </summary>
public sealed class SmokeTest(IGameSession session, PlayerController controller, int totalFrames)
{
    private int frame;
    private int tilesChanged;
    private (int X, int Y) target;

    public bool Finished { get; private set; }
    public string? Failure { get; private set; }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged) tilesChanged++;
    }

    public void Step()
    {
        int id = session.LocalPlayerId;
        if (!session.World.TryGetEntityView(id, out var me))
        {
            Fail("o jogador sumiu");
            return;
        }
        if (!float.IsFinite(me.Position.X) || !float.IsFinite(me.Position.Y))
        {
            Fail($"posição inválida {me.Position}");
            return;
        }

        int third = totalFrames / 3;
        if (frame < third / 2)
            controller.ScriptedInput = default;
        else if (frame < third)
            controller.ScriptedInput = new PlayerInput(1f, frame % 40 < 10, false, 0, 0);
        else if (frame < third + 30)
            controller.ScriptedInput = default;   // para e pousa antes de mirar
        else
        {
            if (frame == third + 30)
                target = ((int)System.MathF.Floor(me.Position.X) + 1, (int)System.MathF.Floor(me.Position.Y + me.Size.Y / 2));
            controller.ScriptedInput = new PlayerInput(0f, false, true, target.X, target.Y);
        }

        if (++frame < totalFrames) return;
        if (tilesChanged == 0)
        {
            Fail($"nenhum tile mudou (alvo {target})");
            return;
        }
        Finished = true;
    }

    private void Fail(string reason)
    {
        Failure = reason;
        Finished = true;
    }
}
```

- [ ] **Step 2: Usar o roteiro no `GameRoot`**

Adicionar o campo `private SmokeTest? smoke;`. Ao final de `_Ready`: `if (options.SmokeTest) smoke = new SmokeTest(session, controller, options.SmokeFrames);`. No laço de eventos: `smoke?.OnEvent(gameEvent);`. Substituir o bloco do smoke test em `_Process` (o que usa `++frame >= options.SmokeFrames`) por:

```csharp
            if (smoke is not null)
            {
                smoke.Step();
                if (smoke.Finished)
                {
                    if (smoke.Failure is null)
                    {
                        GD.Print($"SMOKE OK: tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}");
                        GetTree().Quit(0);
                    }
                    else
                    {
                        GD.PrintErr($"SMOKE FAIL: {smoke.Failure}");
                        GetTree().Quit(1);
                    }
                }
            }
```

(Remova o campo `frame`, que deixa de ser usado. `smoke.Step()` precisa rodar **antes** de `controller.Poll(...)` no próximo frame — basta que fique no fim de `_Process`.)

- [ ] **Step 3: Rodar o smoke test completo**

Run: `tools/smoke-test.sh 600`
Expected: `SMOKE OK: tick ..., chunks N` e `smoke test OK`. Se falhar, **não** afrouxe o roteiro: imprima a posição do jogador e o alvo e use superpowers:systematic-debugging.

- [ ] **Step 4: Atualizar o `CLAUDE.md`**

Na seção "Estrutura", adicionar:

```markdown
- `src/Zyon.Client` — lógica de apresentação sem Godot (autotiling, malhas de chunk, visibilidade, chunks sujos, cores e atlas provisórios, opções de lançamento). Testes em `tests/Zyon.Client.Tests`.
- `src/Zyon.Game` — projeto Godot 4.7 (.NET) fino: converte dados do Client em `ArrayMesh`/`MultiMesh`/nós, lê input e só conversa com `IGameSession`. Cores provisórias em `data/client/visuals.json`.
```

Na seção "Comandos", adicionar:

```markdown
- Godot (portátil): `/d/Projetos/Ferramentas/Godot/Godot_v4.7.2-stable_mono_win64_console.exe` (variável `GODOT` sobrescreve).
- Jogar: `"$GODOT" --path src/Zyon.Game -- --size dev` (opções: `--size dev|small|medium|large`, `--seed N`).
- Smoke test do jogo (headless): `tools/smoke-test.sh [frames]` — rode sempre que mexer em `src/Zyon.Game`.
```

Na seção "Regras invioláveis", adicionar:

```markdown
- Nós Godot criados por código: construtor sem parâmetros + `Init(...)`. Lógica testável vai para `Zyon.Client`, nunca para `Zyon.Game`.
```

- [ ] **Step 5: Verificação final e commit**

Run: `dotnet build -c Release` → 0 avisos/erros. `dotnet test` → tudo verde. `tools/smoke-test.sh 600` → OK. `grep -ri "godot" src/Zyon.Core src/Zyon.Client --include=*.cs || echo ok` → `ok`. `git status --short` → só os arquivos desta tarefa.

```bash
git add src/Zyon.Game CLAUDE.md
git commit -m "test(game): smoke test roteirizado; docs do cliente" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
