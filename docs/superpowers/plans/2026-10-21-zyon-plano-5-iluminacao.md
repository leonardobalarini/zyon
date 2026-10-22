# Zyon — Plano 5: Iluminação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Luz suave por tile (céu com ciclo de dia e noite, tochas, minério que brilha, halo do jogador) calculada para a região visível e desenhada como uma textura multiplicada sobre o mundo.

**Architecture:** O motor (`Zyon.Core.Lighting`) é C# puro: `LightingConfig` (de `data/lighting.json`), `DayCycle` (tick → hora e fator do céu), `LightMap` e `LightEngine` (semeia céu e emissores e propaga por varreduras em grade). `Zyon.Client` converte o mapa em bytes L8 e decide quando recalcular. `Zyon.Game` tem um `LightOverlay` (Sprite2D com textura de 1 px por tile, filtro bilinear e blend multiply).

**Tech Stack:** .NET 10, Godot 4.7.2 .NET, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-21-zyon-iluminacao-design.md`.

---

## Convenções

- Tudo do `CLAUDE.md` continua valendo. Core e Client nunca referenciam Godot.
- **Blocos novos entram no fim do `blocks.json`** para não mudar ids existentes (o hash de referência da geração continua valendo).
- **Shell:** Bash (Git Bash) a partir de `D:\Projetos\Zyon`. **Commits:** mensagem + segundo `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`; nunca `--amend`; nunca `push`.
- **Branch:** `feat/plano-5-iluminacao` (criada pelo controlador).
- **Verificação:** `dotnet test`; nas tarefas do Godot também `dotnet build src/Zyon.Game/Zyon.Game.csproj` e `tools/smoke-test.sh 600`.

## Mapa de arquivos

```
data/blocks.json  data/items.json  data/player.json  data/client/visuals.json   # (mod) tocha, brilho do minério B
data/lighting.json                                                             # novo
src/Zyon.Core/Data/Defs.cs  src/Zyon.Core/Data/RegistryLoader.cs               # (mod) BlockDef.Light
src/Zyon.Core/Lighting/
  LightingConfig.cs  LightingConfigLoader.cs  DayCycle.cs
  TileRect.cs  LightMap.cs  LightEmitter.cs  LightEngine.cs
src/Zyon.Client/
  LightTexture.cs  LightRecomputePolicy.cs                                     # novos
  PlaceholderAtlas.cs  ChunkMeshBuilder.cs                                     # (mod) blocos não sólidos
src/Zyon.Game/Rendering/LightOverlay.cs                                        # novo
src/Zyon.Game/Rendering/WorldRenderer.cs  src/Zyon.Game/GameRoot.cs  src/Zyon.Game/Hud/DebugOverlay.cs  src/Zyon.Game/SmokeTest.cs   # (mod)
tests/Zyon.Core.Tests/  LightingDataTests.cs  DayCycleTests.cs  LightEngineTests.cs  LightingPerformanceTests.cs
tests/Zyon.Client.Tests/ LightTextureTests.cs  LightRecomputePolicyTests.cs  (+ casos novos em PlaceholderAtlasTests, ChunkMeshBuilderTests)
```

---

### Task 1: Tocha, brilho de blocos e dados

**Files:**
- Modify: `src/Zyon.Core/Data/Defs.cs`, `src/Zyon.Core/Data/RegistryLoader.cs`
- Modify: `data/blocks.json`, `data/items.json`, `data/player.json`, `data/client/visuals.json`
- Test: `tests/Zyon.Core.Tests/LightingDataTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Core.Tests/LightingDataTests.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class LightingDataTests
{
    private const string Walls = """[{"id":"dirt_wall"}]""";
    private const string Items = """[{"id":"dirt","name":"Terra","maxStack":999,"kind":"Block","placesBlock":"dirt"}]""";
    private const string Player = """{"starterItems":[]}""";

    [Fact]
    public void Light_IsOptional_AndDefaultsToZero()
    {
        var r = RegistryLoader.Load("""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt"}]""", Walls, Items, "{}", Player);

        Assert.Equal(0f, r.Block(r.BlockId("dirt")).Light);
        Assert.Equal(0f, r.Block(0).Light);
    }

    [Fact]
    public void Light_IsLoaded()
    {
        var r = RegistryLoader.Load("""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt","light":0.4}]""", Walls, Items, "{}", Player);

        Assert.Equal(0.4f, r.Block(r.BlockId("dirt")).Light);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Light_OutsideZeroToOne_Throws(double light)
    {
        string blocks = $$"""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt","light":{{light.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]""";

        var ex = Assert.Throws<DataException>(() => RegistryLoader.Load(blocks, Walls, Items, "{}", Player));

        Assert.Contains("blocks.json", ex.Message);
        Assert.Contains("light", ex.Message);
    }

    [Fact]
    public void RealData_HasTorch_GlowingOre_AndStarterTorches()
    {
        var r = TestData.Registry();

        var torch = r.Block(r.BlockId("torch"));
        Assert.False(torch.Solid);
        Assert.Equal(0f, torch.Hardness);
        Assert.Equal(1f, torch.Light);
        Assert.Equal(r.ItemId("torch"), torch.DropItemId);
        Assert.Equal(0.35f, r.Block(r.BlockId("ore_b")).Light);
        Assert.Contains(r.StarterItems, s => s.ItemId == r.ItemId("torch") && s.Count == 30);
    }

    [Fact]
    public void Torch_IsTheLastBlock_SoExistingIdsDoNotChange()
    {
        var r = TestData.Registry();

        Assert.Equal(r.Blocks.Count - 1, r.BlockId("torch"));
    }
}
```

Run: `dotnet test --filter "FullyQualifiedName~LightingDataTests"` → FAIL na compilação (`Light` não existe).

- [ ] **Step 2: Implementar**

`src/Zyon.Core/Data/Defs.cs` — o record de bloco ganha `Light` com padrão (as chamadas existentes continuam compilando):

```csharp
/// <summary>Light: luz emitida pelo bloco, em [0, 1] (0 = não emite).</summary>
public sealed record BlockDef(ushort Id, string Name, float Hardness, bool Solid, ushort DropItemId, float Light = 0f);
```

`src/Zyon.Core/Data/RegistryLoader.cs`:
- em `BlockDto`, adicionar `public float? Light { get; set; }`;
- no laço dos blocos, depois de ler `solid` e `drop`:

```csharp
            float light = d.Light ?? 0f;
            if (light < 0f || light > 1f || !float.IsFinite(light))
                throw new DataException($"{ctx}: campo 'light' deve estar entre 0 e 1");
            blocks[i + 1] = new BlockDef((ushort)(i + 1), d.Id!, hardness, solid, drop, light);
```

(substituindo a linha `blocks[i + 1] = new BlockDef(...)` atual).

- [ ] **Step 3: Dados**

`data/blocks.json`: adicionar `"light": 0.35` à entrada `ore_b` e, **como última entrada**:

```json
  { "id": "torch",      "hardness": 0.0, "solid": false, "drops": "torch", "light": 1.0 }
```

`data/items.json`, como última entrada:

```json
  { "id": "torch",         "name": "Tocha",           "maxStack": 999, "kind": "Block", "placesBlock": "torch" }
```

`data/player.json`:

```json
{
  "starterItems": [
    { "item": "basic_pickaxe", "count": 1 },
    { "item": "torch", "count": 30 }
  ]
}
```

`data/client/visuals.json`: em `"blocks"`, adicionar `"torch": "#ffb347"`.

- [ ] **Step 4: Rodar e confirmar**

Run: `dotnet test` → tudo verde (o hash de referência da geração não muda: a tocha é o último bloco e a geração não a usa).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/Data data tests/Zyon.Core.Tests/LightingDataTests.cs
git commit -m "feat(data): tocha, brilho de blocos e 30 tochas iniciais" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Configuração da luz e ciclo de dia e noite

**Files:**
- Create: `data/lighting.json`, `src/Zyon.Core/Lighting/LightingConfig.cs`, `src/Zyon.Core/Lighting/LightingConfigLoader.cs`, `src/Zyon.Core/Lighting/DayCycle.cs`
- Modify: `tests/Zyon.Core.Tests/TestData.cs`
- Test: `tests/Zyon.Core.Tests/DayCycleTests.cs`, `tests/Zyon.Core.Tests/LightingConfigTests.cs`

- [ ] **Step 1: Testes que falham**

Em `tests/Zyon.Core.Tests/TestData.cs`, adicionar `using Zyon.Core.Lighting;` e:

```csharp
    public static LightingConfig LightingConfig() => LightingConfigLoader.LoadFromDirectory(DataDir);
```

`tests/Zyon.Core.Tests/LightingConfigTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Lighting;

namespace Zyon.Core.Tests;

public class LightingConfigTests
{
    [Fact]
    public void RealFile_Loads()
    {
        var c = TestData.LightingConfig();

        Assert.Equal(0.06f, c.AirAttenuation);
        Assert.Equal(0.25f, c.SolidAttenuation);
        Assert.Equal(1440, c.DayLengthSeconds);
        Assert.Equal(18, c.RegionMargin);
    }

    [Theory]
    [InlineData("""{"airAttenuation": 0}""", "airAttenuation")]
    [InlineData("""{"solidAttenuation": 1.5}""", "solidAttenuation")]
    [InlineData("""{"dayLengthSeconds": 0}""", "dayLengthSeconds")]
    [InlineData("""{"startTimeOfDay": 1}""", "startTimeOfDay")]
    [InlineData("""{"nightSkyLevel": 2}""", "nightSkyLevel")]
    [InlineData("""{"twilightFraction": 0.5}""", "twilightFraction")]
    [InlineData("""{"playerHaloLevel": -1}""", "playerHaloLevel")]
    [InlineData("""{"regionMargin": -1}""", "regionMargin")]
    [InlineData("""{"airAtenuation": 0.1}""", "lighting.json")]
    [InlineData("""{""", "lighting.json")]
    public void Invalid_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => LightingConfigLoader.Load(json));

        Assert.Contains("lighting.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }
}
```

`tests/Zyon.Core.Tests/DayCycleTests.cs`:

```csharp
using Zyon.Core.Lighting;

namespace Zyon.Core.Tests;

public class DayCycleTests
{
    private static readonly DayCycle Cycle = new(TestData.LightingConfig());

    [Fact]
    public void TicksPerDay_FollowsDayLength()
    {
        Assert.Equal(1440L * 60, Cycle.TicksPerDay);
    }

    [Fact]
    public void NewWorld_StartsInFullDaylight()
    {
        Assert.Equal(0.30, Cycle.TimeOfDay(0), 6);
        Assert.Equal(1f, Cycle.SkyFactor(0), 5);
    }

    [Fact]
    public void TimeOfDay_Wraps()
    {
        Assert.Equal(Cycle.TimeOfDay(123), Cycle.TimeOfDay(123 + Cycle.TicksPerDay), 9);
    }

    [Fact]
    public void Midnight_IsNightLevel_Noon_IsFull()
    {
        long ticksPerDay = Cycle.TicksPerDay;
        long midnight = (long)((1.0 - 0.30) * ticksPerDay);        // hora 0
        long noon = (long)((0.50 - 0.30) * ticksPerDay);            // hora 0,5

        Assert.Equal(0.15f, Cycle.SkyFactor(midnight), 4);
        Assert.Equal(1f, Cycle.SkyFactor(noon), 4);
    }

    [Fact]
    public void Transitions_AreContinuous_NoJumps()
    {
        float previous = Cycle.SkyFactor(0);
        for (long t = 1; t <= Cycle.TicksPerDay; t++)
        {
            float current = Cycle.SkyFactor(t);
            Assert.True(MathF.Abs(current - previous) < 0.002f, $"salto de {previous} para {current} no tick {t}");
            previous = current;
        }
    }
}
```

Run: `dotnet test --filter "FullyQualifiedName~DayCycleTests|FullyQualifiedName~LightingConfigTests"` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`data/lighting.json`:

```json
{
  "airAttenuation": 0.06,
  "solidAttenuation": 0.25,
  "dayLengthSeconds": 1440,
  "startTimeOfDay": 0.30,
  "nightSkyLevel": 0.15,
  "twilightFraction": 0.041666667,
  "playerHaloLevel": 0.3,
  "regionMargin": 18
}
```

`src/Zyon.Core/Lighting/LightingConfig.cs`:

```csharp
namespace Zyon.Core.Lighting;

/// <summary>Parâmetros da luz (data/lighting.json). Níveis em [0, 1]; frações do dia em [0, 1).</summary>
public sealed class LightingConfig
{
    /// <summary>Luz perdida ao entrar num tile de ar ou bloco não sólido.</summary>
    public float AirAttenuation { get; init; } = 0.06f;

    /// <summary>Luz perdida ao entrar num bloco sólido.</summary>
    public float SolidAttenuation { get; init; } = 0.25f;

    public double DayLengthSeconds { get; init; } = 1440;
    public double StartTimeOfDay { get; init; } = 0.30;
    public float NightSkyLevel { get; init; } = 0.15f;

    /// <summary>Duração do amanhecer e do anoitecer, como fração do dia.</summary>
    public double TwilightFraction { get; init; } = 1.0 / 24;

    public float PlayerHaloLevel { get; init; } = 0.3f;

    /// <summary>Tiles calculados além da área visível em cada lado (maior que o alcance de uma luz 1,0).</summary>
    public int RegionMargin { get; init; } = 18;
}
```

`src/Zyon.Core/Lighting/LightingConfigLoader.cs`:

```csharp
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.Lighting;

public static class LightingConfigLoader
{
    public const string FileName = "lighting.json";

    public static LightingConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        return Load(File.ReadAllText(path));
    }

    public static LightingConfig Load(string json)
    {
        LightingConfig c;
        try
        {
            c = JsonSerializer.Deserialize<LightingConfig>(json, RegistryLoader.Options)
                ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}", ex);
        }

        static void Require(bool ok, string message)
        {
            if (!ok) throw new DataException($"{FileName}: {message}");
        }

        Require(c.AirAttenuation > 0 && c.AirAttenuation <= 1, "'airAttenuation' deve estar em (0, 1]");
        Require(c.SolidAttenuation > 0 && c.SolidAttenuation <= 1, "'solidAttenuation' deve estar em (0, 1]");
        Require(c.DayLengthSeconds > 0 && double.IsFinite(c.DayLengthSeconds), "'dayLengthSeconds' deve ser > 0");
        Require(c.StartTimeOfDay >= 0 && c.StartTimeOfDay < 1, "'startTimeOfDay' deve estar em [0, 1)");
        Require(c.NightSkyLevel >= 0 && c.NightSkyLevel <= 1, "'nightSkyLevel' deve estar em [0, 1]");
        Require(c.TwilightFraction > 0 && c.TwilightFraction <= 0.25, "'twilightFraction' deve estar em (0, 0,25]");
        Require(c.PlayerHaloLevel >= 0 && c.PlayerHaloLevel <= 1, "'playerHaloLevel' deve estar em [0, 1]");
        Require(c.RegionMargin >= 0 && c.RegionMargin <= 256, "'regionMargin' deve estar entre 0 e 256");
        return c;
    }
}
```

(Se `DataException` não tiver o construtor `(string, Exception)`, use o de uma string — o plano 3 o adicionou.)

`src/Zyon.Core/Lighting/DayCycle.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Core.Lighting;

/// <summary>
/// Hora do dia derivada do Tick (que já vai no save do mundo). Amanhecer centrado em 0,25 e anoitecer em 0,75,
/// cada um com transição suave (smoothstep) de TwilightFraction do dia.
/// </summary>
public sealed class DayCycle(LightingConfig config)
{
    private const double Dawn = 0.25;
    private const double Dusk = 0.75;

    public long TicksPerDay { get; } = (long)Math.Round(config.DayLengthSeconds * Simulation.TicksPerSecond);

    /// <summary>Hora em [0, 1): 0 = meia-noite, 0,5 = meio-dia.</summary>
    public double TimeOfDay(long tick)
    {
        double t = (tick / (double)TicksPerDay + config.StartTimeOfDay) % 1.0;
        return t < 0 ? t + 1 : t;
    }

    /// <summary>Intensidade da luz do céu: NightSkyLevel à noite, 1 de dia.</summary>
    public float SkyFactor(long tick)
    {
        double t = TimeOfDay(tick);
        double half = config.TwilightFraction / 2;
        double day = Ramp(t, Dawn - half, Dawn + half) - Ramp(t, Dusk - half, Dusk + half);
        float night = config.NightSkyLevel;
        return (float)(night + (1 - night) * day);
    }

    private static double Ramp(double t, double from, double to)
    {
        if (t <= from) return 0;
        if (t >= to) return 1;
        double x = (t - from) / (to - from);
        return x * x * (3 - 2 * x);
    }
}
```

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → tudo verde.

- [ ] **Step 4: Commit**

```bash
git add data/lighting.json src/Zyon.Core/Lighting tests/Zyon.Core.Tests
git commit -m "feat(lighting): configuração da luz e ciclo de dia e noite" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Motor de luz

**Files:**
- Create: `src/Zyon.Core/Lighting/TileRect.cs`, `src/Zyon.Core/Lighting/LightMap.cs`, `src/Zyon.Core/Lighting/LightEmitter.cs`, `src/Zyon.Core/Lighting/LightEngine.cs`
- Test: `tests/Zyon.Core.Tests/LightEngineTests.cs`, `tests/Zyon.Core.Tests/LightingPerformanceTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Core.Tests/LightEngineTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class LightEngineTests
{
    private static readonly Registry R = TestData.Registry();
    private static readonly LightingConfig Config = TestData.LightingConfig();
    private static ushort Dirt => R.BlockId("dirt");
    private static ushort Torch => R.BlockId("torch");
    private static ushort Wall => R.WallId("dirt_wall");

    /// <summary>64×64: céu nas linhas 0..19, terra sólida com parede de fundo da linha 20 para baixo.</summary>
    private static TileWorld Ground()
    {
        var w = new TileWorld(64, 64);
        for (int y = 20; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            w.SetBlock(x, y, Dirt);
            w.SetWall(x, y, Wall);
        }
        return w;
    }

    private static LightMap Compute(TileWorld world, float sky = 1f, params LightEmitter[] extra)
    {
        using var view = new Simulation(R, world);
        var map = new LightMap();
        new LightEngine(Config).Compute(view, new TileRect(0, 0, world.Width - 1, world.Height - 1), sky, extra, map);
        return map;
    }

    [Fact]
    public void OpenSky_IsFullyLit_AndFollowsSkyFactor()
    {
        Assert.Equal(1f, Compute(Ground()).Get(10, 5), 5);
        Assert.Equal(0.15f, Compute(Ground(), sky: 0.15f).Get(10, 5), 5);
    }

    [Fact]
    public void ClosedCave_IsDark_BackgroundWallsBlockTheSky()
    {
        var w = Ground();
        for (int y = 40; y < 45; y++)
        for (int x = 20; x < 30; x++)
            w.SetBlock(x, y, 0);   // caverna com paredes de fundo

        Assert.Equal(0f, Compute(w).Get(25, 42), 5);
    }

    [Fact]
    public void ShaftToTheSurface_LetsTheSkyIn()
    {
        var w = Ground();
        for (int y = 20; y < 40; y++)
        {
            w.SetBlock(32, y, 0);
            w.SetWall(32, y, 0);   // poço sem parede: o céu desce
        }

        Assert.Equal(1f, Compute(w).Get(32, 39), 5);
    }

    [Fact]
    public void Torch_FallsOffByAirAttenuationPerTile()
    {
        var w = new TileWorld(64, 64);   // tudo ar, sem céu (paredes em tudo)
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            w.SetWall(x, y, Wall);
        w.SetBlock(30, 30, Torch);

        var map = Compute(w);

        Assert.Equal(1f, map.Get(30, 30), 5);
        Assert.Equal(1f - 0.06f, map.Get(31, 30), 4);
        Assert.Equal(1f - 0.06f * 5, map.Get(30, 35), 4);
        Assert.Equal(1f - 0.06f * 4, map.Get(32, 32), 4);   // distância Manhattan 4
        Assert.Equal(0f, map.Get(30, 30 + 17), 4);           // além do alcance
    }

    [Fact]
    public void SolidBlock_CutsTheLightSharply()
    {
        var w = new TileWorld(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            w.SetWall(x, y, Wall);
        w.SetBlock(30, 30, Torch);
        for (int y = 0; y < 64; y++)
            w.SetBlock(32, y, Dirt);   // muro sólido em x = 32

        var map = Compute(w);

        Assert.Equal(1f - 0.06f - 0.25f, map.Get(32, 30), 4);
        Assert.True(map.Get(34, 30) < map.Get(28, 30) - 0.2f, "o muro deveria fazer sombra");
    }

    [Fact]
    public void GlowingOre_EmitsItsLevel()
    {
        var w = Ground();
        w.SetBlock(40, 50, R.BlockId("ore_b"));

        Assert.Equal(0.35f, Compute(w).Get(40, 50), 5);
    }

    [Fact]
    public void ExtraEmitter_LikeThePlayerHalo_IsApplied()
    {
        var w = Ground();
        for (int y = 40; y < 45; y++)
        for (int x = 20; x < 30; x++)
            w.SetBlock(x, y, 0);

        var map = Compute(w, 1f, new LightEmitter(25, 42, 0.3f));

        Assert.Equal(0.3f, map.Get(25, 42), 5);
        Assert.Equal(0.3f - 0.06f, map.Get(26, 42), 4);
    }

    [Fact]
    public void RegionWithMargin_MatchesWholeWorld_InTheVisiblePart()
    {
        var w = Ground();
        w.SetBlock(5, 30, 0);
        for (int x = 3; x < 60; x += 7) { w.SetBlock(x, 35, 0); w.SetBlock(x, 34, Torch); }
        using var view = new Simulation(R, w);
        var engine = new LightEngine(Config);
        var whole = new LightMap();
        var part = new LightMap();
        var visible = new TileRect(25, 25, 40, 40);

        engine.Compute(view, new TileRect(0, 0, 63, 63), 1f, [], whole);
        engine.Compute(view, visible.Expand(Config.RegionMargin).ClampTo(view.Width, view.Height), 1f, [], part);

        for (int y = visible.MinY; y <= visible.MaxY; y++)
        for (int x = visible.MinX; x <= visible.MaxX; x++)
            Assert.Equal(whole.Get(x, y), part.Get(x, y), 4);
    }

    [Fact]
    public void LightMap_OutsideRegion_IsZero()
    {
        var map = Compute(Ground());

        Assert.Equal(0f, map.Get(-1, 5));
        Assert.Equal(0f, map.Get(64, 5));
    }
}
```

`tests/Zyon.Core.Tests/LightingPerformanceTests.cs`:

```csharp
using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

[Collection(PerformanceCollection.Name)]
public class LightingPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void VisibleRegion_ComputesUnderOneMillisecond()
    {
        var registry = TestData.Registry();
        var config = TestData.LightingConfig();
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(1050, 300, 3);
        ushort torch = registry.BlockId("torch");
        int fx = (int)generated.SpawnFeet.X, fy = (int)generated.SpawnFeet.Y;
        for (int i = -20; i <= 20; i += 5)
            if (generated.Tiles.GetBlock(fx + i, fy + 6) == 0) generated.Tiles.SetBlock(fx + i, fy + 6, torch);
        using var view = new Simulation(registry, generated.Tiles);
        var visible = new TileRect(fx - 20, fy - 11, fx + 19, fy + 11);   // 40×23 tiles = 640×360 px
        var region = visible.Expand(config.RegionMargin).ClampTo(view.Width, view.Height);
        var engine = new LightEngine(config);
        var map = new LightMap();

        for (int i = 0; i < 20; i++) engine.Compute(view, region, 1f, [new LightEmitter(fx, fy - 1, 0.3f)], map);
        const int runs = 200;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++) engine.Compute(view, region, 1f, [new LightEmitter(fx, fy - 1, 0.3f)], map);
        double avgMs = sw.Elapsed.TotalMilliseconds / runs;

        output.WriteLine($"Região {region.Width}×{region.Height}: {avgMs:F3} ms por cálculo, {engine.LastRounds} rodadas");
#if !DEBUG
        Assert.True(avgMs < 1.0, $"cálculo de luz levou {avgMs:F3} ms");
#endif
    }
}
```

Run: `dotnet test --filter "FullyQualifiedName~LightEngineTests"` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Core/Lighting/TileRect.cs`:

```csharp
namespace Zyon.Core.Lighting;

/// <summary>Retângulo inclusivo de tiles.</summary>
public readonly record struct TileRect(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public bool IsEmpty => MaxX < MinX || MaxY < MinY;

    public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

    public bool Contains(TileRect other) =>
        other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;

    public TileRect Expand(int by) => new(MinX - by, MinY - by, MaxX + by, MaxY + by);

    public TileRect ClampTo(int worldWidth, int worldHeight) =>
        new(Math.Max(0, MinX), Math.Max(0, MinY), Math.Min(worldWidth - 1, MaxX), Math.Min(worldHeight - 1, MaxY));
}
```

`src/Zyon.Core/Lighting/LightEmitter.cs`:

```csharp
namespace Zyon.Core.Lighting;

/// <summary>Luz extra que não vem de um bloco (ex.: o halo do jogador).</summary>
public readonly record struct LightEmitter(int X, int Y, float Level);
```

`src/Zyon.Core/Lighting/LightMap.cs`:

```csharp
namespace Zyon.Core.Lighting;

/// <summary>Níveis de luz [0, 1] de um retângulo de tiles, linha por linha. Reutilizável entre cálculos.</summary>
public sealed class LightMap
{
    private float[] levels = [];

    public TileRect Region { get; private set; }
    public int Width => Region.Width;
    public int Height => Region.Height;

    /// <summary>Buffer interno (pode ser maior que Width × Height; use só os primeiros Width × Height).</summary>
    public float[] Levels => levels;

    public void Reset(TileRect region)
    {
        Region = region;
        int size = region.IsEmpty ? 0 : region.Width * region.Height;
        if (levels.Length < size) levels = new float[size];
        Array.Clear(levels, 0, size);
    }

    public float Get(int x, int y) =>
        Region.Contains(x, y) ? levels[(y - Region.MinY) * Width + (x - Region.MinX)] : 0f;
}
```

`src/Zyon.Core/Lighting/LightEngine.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Core.Lighting;

/// <summary>
/// Calcula a luz de um retângulo do mundo: semeia céu (colunas abertas — ar sem parede — desde o topo do mundo),
/// blocos emissores e emissores extras; depois propaga com varreduras nas 4 direções até estabilizar.
/// Cada passo para um vizinho perde a atenuação do tile de destino.
/// </summary>
public sealed class LightEngine(LightingConfig config)
{
    private const int MaxRounds = 64;
    private float[] attenuation = [];

    public int LastRounds { get; private set; }

    public void Compute(IWorldView view, TileRect region, float skyFactor, IReadOnlyList<LightEmitter> extra, LightMap output)
    {
        region = region.ClampTo(view.Width, view.Height);
        output.Reset(region);
        if (region.IsEmpty) return;

        int w = region.Width, h = region.Height, size = w * h;
        if (attenuation.Length < size) attenuation = new float[size];
        float[] light = output.Levels;
        var registry = view.Registry;

        // Céu: desce por cada coluna enquanto for ar sem parede de fundo.
        for (int x = region.MinX; x <= region.MaxX; x++)
        {
            for (int y = 0; y <= region.MaxY; y++)
            {
                if (view.GetBlock(x, y) != 0 || view.GetWall(x, y) != 0) break;
                if (y >= region.MinY) light[(y - region.MinY) * w + (x - region.MinX)] = skyFactor;
            }
        }

        // Atenuação de cada tile e emissão dos blocos.
        for (int y = region.MinY; y <= region.MaxY; y++)
        for (int x = region.MinX; x <= region.MaxX; x++)
        {
            int i = (y - region.MinY) * w + (x - region.MinX);
            var block = registry.Block(view.GetBlock(x, y));
            attenuation[i] = block.Solid ? config.SolidAttenuation : config.AirAttenuation;
            if (block.Light > light[i]) light[i] = block.Light;
        }

        foreach (var e in extra)
        {
            if (!region.Contains(e.X, e.Y)) continue;
            int i = (e.Y - region.MinY) * w + (e.X - region.MinX);
            if (e.Level > light[i]) light[i] = e.Level;
        }

        int rounds = 0;
        bool changed;
        do
        {
            changed = false;
            rounds++;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 1; x < w; x++) changed |= Relax(light, row + x, row + x - 1);
                for (int x = w - 2; x >= 0; x--) changed |= Relax(light, row + x, row + x + 1);
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 1; y < h; y++) changed |= Relax(light, y * w + x, (y - 1) * w + x);
                for (int y = h - 2; y >= 0; y--) changed |= Relax(light, y * w + x, (y + 1) * w + x);
            }
        } while (changed && rounds < MaxRounds);
        LastRounds = rounds;
    }

    private bool Relax(float[] light, int target, int from)
    {
        float candidate = light[from] - attenuation[target];
        if (candidate <= light[target]) return false;
        light[target] = candidate;
        return true;
    }
}
```

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → tudo verde. Run: `dotnet test -c Release --filter "FullyQualifiedName~LightingPerformanceTests" --logger "console;verbosity=detailed"` → PASS e a linha `Região ... ms por cálculo`. Se passar de 1 ms, reporte o número (não otimize por conta própria).

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Core/Lighting tests/Zyon.Core.Tests
git commit -m "feat(lighting): motor de luz por varreduras em grade" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Textura de luz, política de recálculo e blocos não sólidos (Client)

**Files:**
- Create: `src/Zyon.Client/LightTexture.cs`, `src/Zyon.Client/LightRecomputePolicy.cs`
- Modify: `src/Zyon.Client/PlaceholderAtlas.cs`, `src/Zyon.Client/ChunkMeshBuilder.cs`
- Test: `tests/Zyon.Client.Tests/LightTextureTests.cs`, `tests/Zyon.Client.Tests/LightRecomputePolicyTests.cs`, casos novos em `PlaceholderAtlasTests.cs` e `ChunkMeshBuilderTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Client.Tests/LightTextureTests.cs`:

```csharp
using Zyon.Core.Lighting;

namespace Zyon.Client.Tests;

public class LightTextureTests
{
    [Fact]
    public void ToL8_MapsZeroToOneOntoBytes_RowByRow()
    {
        var map = new LightMap();
        map.Reset(new TileRect(10, 20, 12, 21));   // 3×2
        map.Levels[0] = 0f;
        map.Levels[1] = 0.5f;
        map.Levels[2] = 1f;
        map.Levels[5] = 1.2f;   // fora da faixa: satura
        var bytes = new byte[6];

        LightTexture.ToL8(map, bytes);

        Assert.Equal(new byte[] { 0, 128, 255, 0, 0, 255 }, bytes);
    }

    [Fact]
    public void ToL8_TooSmallBuffer_Throws()
    {
        var map = new LightMap();
        map.Reset(new TileRect(0, 0, 3, 3));

        Assert.Throws<ArgumentException>(() => LightTexture.ToL8(map, new byte[4]));
    }
}
```

`tests/Zyon.Client.Tests/LightRecomputePolicyTests.cs`:

```csharp
using Zyon.Core.Lighting;

namespace Zyon.Client.Tests;

public class LightRecomputePolicyTests
{
    private static readonly TileRect Visible = new(100, 50, 139, 72);
    private static readonly TileRect Computed = Visible.Expand(18 + 8);

    private static LightRecomputePolicy Ready()
    {
        var p = new LightRecomputePolicy(margin: 18);
        p.Computed(Computed, (120, 60), 1f);
        return p;
    }

    [Fact]
    public void FirstFrame_AlwaysComputes()
    {
        Assert.True(new LightRecomputePolicy(18).ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void NothingChanged_DoesNotRecompute()
    {
        Assert.False(Ready().ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void PlayerChangedTile_Recomputes()
    {
        Assert.True(Ready().ShouldRecompute(Visible, (121, 60), 1f));
    }

    [Fact]
    public void SkyFactorDrift_RecomputesOnlyAboveThreshold()
    {
        var p = Ready();

        Assert.False(p.ShouldRecompute(Visible, (120, 60), 0.995f));
        Assert.True(p.ShouldRecompute(Visible, (120, 60), 0.98f));
    }

    [Fact]
    public void TileChangedInsideTheRegion_Recomputes_OutsideDoesNot()
    {
        var p = Ready();
        p.MarkTileChanged(5000, 5000);
        Assert.False(p.ShouldRecompute(Visible, (120, 60), 1f));

        p.MarkTileChanged(Computed.MinX, Computed.MinY);
        Assert.True(p.ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void CameraMovingIntoTheMargin_Recomputes()
    {
        var p = Ready();

        Assert.False(p.ShouldRecompute(new TileRect(105, 50, 144, 72), (120, 60), 1f));   // ainda dentro da folga
        Assert.True(p.ShouldRecompute(new TileRect(110, 50, 149, 72), (120, 60), 1f));    // entrou na margem
    }

    [Fact]
    public void Computed_ClearsTheDirtyFlag()
    {
        var p = Ready();
        p.MarkTileChanged(120, 60);
        p.Computed(Computed, (120, 60), 1f);

        Assert.False(p.ShouldRecompute(Visible, (120, 60), 1f));
    }
}
```

Adicionar a `tests/Zyon.Client.Tests/PlaceholderAtlasTests.cs`:

```csharp
    [Fact]
    public void NonSolidMaterial_IsASmallStick_TransparentAround()
    {
        var layout = new TileAtlasLayout(2, 3);
        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown], [false, false]);
        var (x, y) = layout.CellOrigin(1, 0, 0);

        int Alpha(int px, int py) => rgba[((y + py) * layout.WidthPx + x + px) * 4 + 3];

        Assert.Equal(0, Alpha(0, 0));
        Assert.Equal(0, Alpha(2, 12));
        Assert.Equal(255, Alpha(7, 10));
        Assert.Equal(255, Alpha(8, 4));
    }
```

Adicionar a `tests/Zyon.Client.Tests/ChunkMeshBuilderTests.cs`:

```csharp
    [Fact]
    public void WallBehindNonSolidBlock_IsDrawn()
    {
        var r = TestData.Registry();
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, 1);
        w.SetBlock(5, 5, r.BlockId("torch"));
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }
```

(`Atlas` nesse arquivo tem 6 materiais; o id da tocha é 6. Se o teste de blocos usar o atlas com a tocha, troque o atlas desse arquivo para `new TileAtlasLayout(TestData.Registry().Blocks.Count, 3)` — ajuste mínimo, reporte.)

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/LightTexture.cs`:

```csharp
using Zyon.Core.Lighting;

namespace Zyon.Client;

/// <summary>Converte um LightMap em bytes L8 (0–255), linha por linha, para virar textura de 1 px por tile.</summary>
public static class LightTexture
{
    public static void ToL8(LightMap map, byte[] destination)
    {
        int size = map.Width * map.Height;
        if (destination.Length < size)
            throw new ArgumentException($"buffer de {destination.Length} bytes, precisa de {size}", nameof(destination));
        var levels = map.Levels;
        for (int i = 0; i < size; i++)
            destination[i] = (byte)Math.Clamp((int)MathF.Round(levels[i] * 255f), 0, 255);
    }
}
```

`src/Zyon.Client/LightRecomputePolicy.cs`:

```csharp
using Zyon.Core.Lighting;

namespace Zyon.Client;

/// <summary>
/// Decide se a luz precisa ser recalculada neste frame: na primeira vez, se um tile mudou dentro da região calculada,
/// se o jogador mudou de tile (o halo anda), se o fator do céu variou mais de 0,01 ou se a área visível chegou
/// perto demais da borda da região (dentro da margem).
/// </summary>
public sealed class LightRecomputePolicy(int margin)
{
    public const float SkyThreshold = 0.01f;

    private bool computedOnce;
    private bool dirty;
    private TileRect region;
    private (int X, int Y) playerTile;
    private float sky;

    public void MarkTileChanged(int x, int y)
    {
        if (computedOnce && region.Contains(x, y)) dirty = true;
    }

    public bool ShouldRecompute(TileRect visible, (int X, int Y) currentPlayerTile, float currentSky) =>
        !computedOnce
        || dirty
        || currentPlayerTile != playerTile
        || MathF.Abs(currentSky - sky) > SkyThreshold
        || !region.Expand(-margin).Contains(visible);

    public void Computed(TileRect computedRegion, (int X, int Y) currentPlayerTile, float currentSky)
    {
        computedOnce = true;
        dirty = false;
        region = computedRegion;
        playerTile = currentPlayerTile;
        sky = currentSky;
    }
}
```

(Atenção: quando a região foi limitada pela borda do mundo, `region.Expand(-margin)` encolhe também do lado da borda e a câmera presa na borda dispararia recálculo todo frame. Para evitar: o chamador passa ao `Computed` a região **antes** do `ClampTo` — o `LightOverlay` da Task 5 faz isso. Adicione um teste: `Computed(Visible.Expand(26), ...)` com `Visible` encostado em x = 0 e confirme que nada é recalculado com a mesma área visível.)

`src/Zyon.Client/PlaceholderAtlas.cs` — `BuildRgba` ganha um parâmetro opcional com a solidez por material:

```csharp
    public static byte[] BuildRgba(TileAtlasLayout layout, IReadOnlyList<Rgb?> colorsById, IReadOnlyList<bool>? solidById = null)
```

e, dentro do laço de cada material, antes de desenhar as células: se `solidById is not null && material < solidById.Count && !solidById[material]`, desenhar em **todas** as células (máscaras × variações) só um "palito": pixels com `px` em 6..9 e `py` em 3..15 recebem a cor base (com o ruído), e os de `py` em 3..5 recebem a cor clareada (`+80` por canal, saturando em 255) como chama; o resto da célula fica transparente (alpha 0). Os materiais sólidos continuam como hoje.

`src/Zyon.Client/ChunkMeshBuilder.cs` — trocar a linha que pula paredes atrás de blocos:

```csharp
            if (layer == TileLayer.Walls && view.GetBlock(x, y) != 0) continue;
```

por:

```csharp
            if (layer == TileLayer.Walls && view.Registry.Block(view.GetBlock(x, y)).Solid) continue;
```

(blocos não sólidos, como a tocha, deixam a parede de fundo visível ao redor.)

Em `src/Zyon.Game/Rendering/WorldRenderer.cs`, ao montar o atlas de blocos, passar a solidez: `PlaceholderAtlas.BuildRgba(layout, colors, view.Registry.Blocks.Select(b => b.Solid).ToList())` para o atlas de blocos (o de paredes continua sem esse argumento). Ajuste a assinatura do `BuildAtlas` local com um parâmetro opcional.

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → verde; `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0/0.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests src/Zyon.Game/Rendering/WorldRenderer.cs
git commit -m "feat(client): textura de luz, política de recálculo e blocos não sólidos no atlas" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Sobreposição de luz no jogo (Godot)

**Files:**
- Create: `src/Zyon.Game/Rendering/LightOverlay.cs`
- Modify: `src/Zyon.Game/GameFlow.cs`, `src/Zyon.Game/GameRoot.cs`, `src/Zyon.Game/Hud/DebugOverlay.cs`

- [ ] **Step 1: `GameFlow` carrega a configuração**

Em `GameFlow.EnsureInitialized`, depois de carregar o `WorldGenConfig`: `Lighting = LightingConfigLoader.LoadFromDirectory(DataDir);` com a propriedade `public static Zyon.Core.Lighting.LightingConfig Lighting { get; private set; } = null!;`.

- [ ] **Step 2: `LightOverlay`**

`src/Zyon.Game/Rendering/LightOverlay.cs`:

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Zyon.Client;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>
/// Calcula a luz da área visível (+ margem) e a desenha como uma textura de 1 px por tile, esticada ×16 com filtro
/// bilinear (degradê suave) e blend multiply sobre o mundo e as entidades. HUD e menus ficam fora (CanvasLayers).
/// </summary>
public partial class LightOverlay : Node2D
{
    private const int Slack = 8;

    private readonly LightMap map = new();
    private readonly List<LightEmitter> extra = new(1);
    private readonly Sprite2D sprite = new();
    private IWorldView view = null!;
    private LightingConfig config = null!;
    private LightEngine engine = null!;
    private DayCycle day = null!;
    private LightRecomputePolicy policy = null!;
    private int playerId;
    private byte[] buffer = [];
    private Image? image;
    private ImageTexture? texture;

    public int RecomputesLastUpdate { get; private set; }
    public double LastComputeMs { get; private set; }
    public float SkyFactor { get; private set; }

    public void Init(IWorldView worldView, LightingConfig lightingConfig, int localPlayerId)
    {
        view = worldView;
        config = lightingConfig;
        playerId = localPlayerId;
        engine = new LightEngine(config);
        day = new DayCycle(config);
        policy = new LightRecomputePolicy(config.RegionMargin);
        sprite.Centered = false;
        sprite.TextureFilter = TextureFilterEnum.Linear;
        sprite.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul };
        AddChild(sprite);
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged t) policy.MarkTileChanged(t.X, t.Y);
    }

    /// <summary>Nível de luz calculado de um tile (para o smoke test e depuração).</summary>
    public float LevelAt(int x, int y) => map.Get(x, y);

    public void UpdateLight(Rect2 visiblePixels)
    {
        RecomputesLastUpdate = 0;
        const float t = RenderMath.PixelsPerTile;
        var visible = new TileRect(
            Mathf.FloorToInt(visiblePixels.Position.X / t), Mathf.FloorToInt(visiblePixels.Position.Y / t),
            Mathf.FloorToInt(visiblePixels.End.X / t), Mathf.FloorToInt(visiblePixels.End.Y / t));
        (int, int) playerTile = (0, 0);
        if (view.TryGetEntityView(playerId, out var me))
            playerTile = ((int)Mathf.Floor(me.Position.X), (int)Mathf.Floor(me.Position.Y));
        SkyFactor = day.SkyFactor(view.Tick);
        if (!policy.ShouldRecompute(visible, playerTile, SkyFactor)) return;

        var unclamped = visible.Expand(config.RegionMargin + Slack);
        extra.Clear();
        extra.Add(new LightEmitter(playerTile.Item1, playerTile.Item2, config.PlayerHaloLevel));
        long start = Stopwatch.GetTimestamp();
        engine.Compute(view, unclamped, SkyFactor, extra, map);
        policy.Computed(unclamped, playerTile, SkyFactor);
        Upload();
        LastComputeMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        RecomputesLastUpdate = 1;
    }

    private void Upload()
    {
        int w = map.Width, h = map.Height;
        if (w <= 0 || h <= 0) return;
        if (buffer.Length != w * h) buffer = new byte[w * h];
        LightTexture.ToL8(map, buffer);
        if (image is null || texture is null || image.GetWidth() != w || image.GetHeight() != h)
        {
            image = Image.CreateFromData(w, h, false, Image.Format.L8, buffer);
            texture = ImageTexture.CreateFromImage(image);
            sprite.Texture = texture;
        }
        else
        {
            image.SetData(w, h, false, Image.Format.L8, buffer);
            texture.Update(image);
        }
        sprite.Position = new Vector2(map.Region.MinX * RenderMath.PixelsPerTile, map.Region.MinY * RenderMath.PixelsPerTile);
        sprite.Scale = new Vector2(RenderMath.PixelsPerTile, RenderMath.PixelsPerTile);
    }
}
```

- [ ] **Step 3: Ligar no `GameRoot`**

- Campo `private LightOverlay lightOverlay = null!;`.
- Na montagem, **depois** de `entityRenderer` e **antes** de `overlay` (o destaque do tile mirado e as rachaduras ficam por cima da luz, sem escurecer): `lightOverlay = new LightOverlay(); AddChild(lightOverlay); lightOverlay.Init(newSession.World, GameFlow.Lighting, playerId);`. Se a ordem atual de `AddChild` não permitir isso diretamente, use `MoveChild` para posicionar o nó logo depois do `entityRenderer` — reporte.
- No laço de eventos: `lightOverlay.OnEvent(gameEvent);`.
- Logo depois de `worldRenderer.UpdateVisible(camera.VisiblePixels());`: `lightOverlay.UpdateLight(camera.VisiblePixels());`.
- O fundo de céu (`RenderingServer.SetDefaultClearColor`) é coberto pela textura de luz, então escurece à noite sem código extra.

- [ ] **Step 4: F3**

Em `Hud/DebugOverlay.cs`, `DebugInfo` ganha `int LightRecomputes, double LightMs, float SkyFactor` (no fim) e o texto ganha a linha `luz: {recálculos neste frame}, {ms:0.000} ms, céu {fator:0.00}`. No `GameRoot`, passe `lightOverlay.RecomputesLastUpdate, lightOverlay.LastComputeMs, lightOverlay.SkyFactor` ao montar o `DebugInfo`.

- [ ] **Step 5: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0/0; `tools/smoke-test.sh 600` → OK; `dotnet test` → verde.

- [ ] **Step 6: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): sobreposição de luz suave com ciclo de dia e noite" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Smoke test da luz, documentação e verificação

**Files:**
- Modify: `src/Zyon.Game/SmokeTest.cs`, `src/Zyon.Game/GameRoot.cs`, `CLAUDE.md`

- [ ] **Step 1: Roteiro do smoke test coloca uma tocha**

Em `SmokeTest.cs`, depois da fase de mineração (quando o tile alvo já virou ar), acrescente uma fase: enviar `SelectHotbarSlot` para o slot da tocha (procure no inventário do jogador o slot cujo item é `torch` via `session.World.GetInventory` e o `Registry`) e segurar o botão primário mirando o tile minerado por 20 frames. Guarde a posição da tocha. Ao terminar, o `GameRoot` (no bloco que imprime `SMOKE OK`) confere, antes de imprimir:

```csharp
                        if (session.World.GetBlock(smoke.TorchTile.X, smoke.TorchTile.Y) != GameFlow.Registry.BlockId("torch"))
                            throw new System.InvalidOperationException("a tocha não foi colocada");
                        lightOverlay.UpdateLight(camera.VisiblePixels());
                        float lit = lightOverlay.LevelAt(smoke.TorchTile.X, smoke.TorchTile.Y);
                        if (lit < 0.99f)
                            throw new System.InvalidOperationException($"o tile da tocha deveria estar iluminado (nível {lit})");
```

e inclui `luz ok` na linha `SMOKE OK`. Se 600 frames não bastarem para o roteiro completo, aumente o padrão do `tools/smoke-test.sh` (e a observação no `CLAUDE.md`) — reporte o novo número.

- [ ] **Step 2: `CLAUDE.md`**

Na seção "Estrutura", acrescentar:

```markdown
- `src/Zyon.Core/Lighting` — luz: `LightEngine` (céu por colunas abertas — ar sem parede —, blocos com `"light"` e emissores extras; propaga por varreduras perdendo a atenuação do tile de destino), `DayCycle` (hora derivada do `Tick`; o save não muda), parâmetros em `data/lighting.json`. No jogo, `LightOverlay` calcula a área visível + `regionMargin` e desenha uma textura L8 de 1 px por tile, bilinear e multiply.
```

Na seção "Regras invioláveis":

```markdown
- Blocos novos entram no **fim** do `blocks.json` (ids são posicionais; inserir no meio muda o hash de referência da geração).
- `regionMargin` precisa ser maior que o alcance da luz mais forte (1 / `airAttenuation`); senão a borda da tela fica errada.
```

E, em "No jogo" (F3), mencionar a linha `luz: recálculos, ms, fator do céu`.

- [ ] **Step 3: Verificação final**

Run: `dotnet build -c Release` → 0 avisos/erros; `dotnet test` e `dotnet test -c Release` → verdes (inclui o teste de desempenho da luz); `tools/smoke-test.sh` → `SMOKE OK ... save ok, luz ok`; `git status --short` → só os arquivos desta tarefa.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Game CLAUDE.md tools
git commit -m "test(game): smoke test coloca tocha e confere a luz; docs da iluminação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Teste manual (pelo usuário)** — critérios do spec (seção 9):
  1. De dia, superfície clara; cavernas fechadas escuras, só com o halo.
  2. Colocar uma tocha (slot 2 do personagem novo): degradê suave; remover a tocha: a sombra volta.
  3. Esperar o anoitecer (por volta de 10 minutos de jogo a partir do início; F3 mostra o fator do céu): o céu e a superfície escurecem sem saltos e clareiam de novo.
  4. Minério B visível no escuro como brilho fraco.
  5. F3: `luz` abaixo de 1 ms; FPS sem queda.
