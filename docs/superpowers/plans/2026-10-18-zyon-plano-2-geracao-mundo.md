# Zyon — Plano 2: Geração do Mundo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gerar mundos finitos, determinísticos e jogáveis (terreno, camadas, cavernas, minérios, paredes, ponto de spawn) a partir de uma seed, com progresso reportável e configuração em JSON.

**Architecture:** Um `WorldGenerator` roda uma lista ordenada de `IWorldGenPass` sobre um `TileWorld`. Cada passe recebe um `WorldGenContext` com o mundo, os materiais resolvidos do `Registry`, a configuração (`data/worldgen.json`), um `Random` próprio (derivado de `seed + nome do passe` por FNV-1a) e ruído FastNoiseLite semeado pelo nome. Passes por coluna rodam em `Parallel.For`; passes com `Random` são sequenciais. Ao final o gerador limpa as flags `Dirty` e devolve um `GeneratedWorld` com o ponto de spawn.

**Tech Stack:** .NET 10, C# latest, FastNoiseLite (arquivo único, MIT, versionado no repo), xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`, seção 6. Pendências do plano 1 incorporadas: limpar `Chunk.Dirty` após gerar, evento `WorldGenProgress`.

---

## Convenções (valem para todas as tarefas)

- Tudo do plano 1 continua valendo (ver `CLAUDE.md`): coordenadas em tiles, Y cresce para baixo, comentários em português, identificadores em inglês, `TreatWarningsAsErrors`.
- Namespace novo: `Zyon.Core.WorldGen` (passes em `Zyon.Core.WorldGen.Passes`).
- **Determinismo:** mesma seed + mesma `WorldGenerator.Version` + mesmos dados ⇒ mesmo mundo, byte a byte, na mesma máquina. Código dentro de `Parallel.For` **nunca** usa `ctx.Random` nem chama `ctx.Report` — só ruído (puro) e escrita em tiles da própria coluna.
- **Mundo de teste dos passes:** 512×256, seed 7.
- **Shell:** Bash (Git Bash) a partir de `D:\Projetos\Zyon`.
- **Commits:** toda mensagem termina com o segundo `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`.
- **Branch:** `feat/plano-2-geracao-mundo` (criada pelo controlador antes da Task 0).

## Mapa de arquivos

```
THIRD_PARTY_NOTICES.md                         # licença da FastNoiseLite
data/worldgen.json                             # parâmetros e materiais da geração
src/Zyon.Core/
  Data/RegistryLoader.cs                       # (modificado) opções JSON passam a internal
  Sim/Events.cs                                # (modificado) + WorldGenProgress
  Tiles/TileWorldHash.cs                       # hash FNV-1a 64 de um TileWorld
  WorldGen/
    ThirdParty/FastNoiseLite.cs                # vendorizado, sem modificações
    WorldSize.cs                               # Small/Medium/Large → dimensões
    WorldGenConfig.cs                          # WorldGenConfig, MaterialNames, OreConfig
    WorldGenConfigLoader.cs                    # JSON → WorldGenConfig com validação
    WorldGenMaterials.cs                       # nomes → ids do Registry
    StableHash.cs                              # FNV-1a 32 para semear Random/ruído
    IWorldGenPass.cs
    WorldGenContext.cs
    WorldGenerator.cs                          # WorldGenerator + GeneratedWorld
    Passes/TerrainPass.cs  LayersPass.cs  CavesPass.cs  OrePass.cs  WallsPass.cs  SpawnPass.cs
tests/Zyon.Core.Tests/
  TestData.cs                                  # (modificado) + WorldGenConfig()
  WorldGenTestKit.cs  ListProgress.cs
  WorldGenConfigTests.cs  HashTests.cs  WorldGeneratorTests.cs
  WorldGenPassTests.cs  WorldGenDeterminismTests.cs  WorldGenScenarioTests.cs  WorldGenPerformanceTests.cs
```

---

### Task 0: Vendorizar a FastNoiseLite

**Files:**
- Create: `src/Zyon.Core/WorldGen/ThirdParty/FastNoiseLite.cs`, `THIRD_PARTY_NOTICES.md`

- [ ] **Step 1: Baixar o arquivo fixado num commit e conferir o hash**

```bash
mkdir -p src/Zyon.Core/WorldGen/ThirdParty
curl -sSfL -o src/Zyon.Core/WorldGen/ThirdParty/FastNoiseLite.cs \
  https://raw.githubusercontent.com/Auburn/FastNoiseLite/7ccfbc16eb1c932568f177d63a9ba51d89bbe516/CSharp/FastNoiseLite.cs
sha256sum src/Zyon.Core/WorldGen/ThirdParty/FastNoiseLite.cs
```

Expected: `605090835008f4ef58fc39cdfa15df5051a4f5575d5e01fd7d355b9f67b0160e`. Se o hash for diferente, **pare** e reporte — não use outro arquivo.

- [ ] **Step 2: Criar `THIRD_PARTY_NOTICES.md`**

```markdown
# Avisos de terceiros

## FastNoiseLite

- Arquivo: `src/Zyon.Core/WorldGen/ThirdParty/FastNoiseLite.cs` (sem modificações)
- Origem: https://github.com/Auburn/FastNoiseLite, commit `7ccfbc16eb1c932568f177d63a9ba51d89bbe516`
- Licença: MIT — Copyright (c) 2023 Jordan Peck, Copyright (c) 2023 Contributors. O texto completo da licença está no cabeçalho do próprio arquivo.
```

- [ ] **Step 3: Verificar o build**

Run: `dotnet build`
Expected: 0 avisos, 0 erros (o arquivo já foi validado com `Nullable` e `TreatWarningsAsErrors`).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(worldgen): vendorizar FastNoiseLite (MIT)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 1: Configuração da geração (`worldgen.json`)

**Files:**
- Modify: `src/Zyon.Core/Data/RegistryLoader.cs` (opções JSON de `private` para `internal`)
- Create: `src/Zyon.Core/WorldGen/WorldGenConfig.cs`, `WorldGenConfigLoader.cs`, `WorldGenMaterials.cs`
- Create: `data/worldgen.json`
- Modify: `tests/Zyon.Core.Tests/TestData.cs`
- Test: `tests/Zyon.Core.Tests/WorldGenConfigTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Em `tests/Zyon.Core.Tests/TestData.cs`, adicionar `using Zyon.Core.WorldGen;` e, dentro da classe `TestData`:

```csharp
    public static WorldGenConfig WorldGenConfig() => WorldGenConfigLoader.LoadFromDirectory(DataDir);
```

`tests/Zyon.Core.Tests/WorldGenConfigTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenConfigTests
{
    [Fact]
    public void RealFile_LoadsAndResolvesMaterials()
    {
        var r = TestData.Registry();
        var cfg = TestData.WorldGenConfig();

        var m = WorldGenMaterials.Resolve(cfg, r);

        Assert.Equal(r.BlockId("dirt"), m.Dirt);
        Assert.Equal(r.BlockId("stone"), m.Stone);
        Assert.Equal(r.BlockId("deep_stone"), m.DeepStone);
        Assert.Equal(r.WallId("dirt_wall"), m.DirtWall);
        Assert.Equal(r.WallId("stone_wall"), m.StoneWall);
        Assert.Equal(2, m.Ores.Count);
        Assert.Equal(r.BlockId("ore_a"), m.Ores[0].Block);
    }

    [Fact]
    public void EmptyJson_UsesDefaults()
    {
        var cfg = WorldGenConfigLoader.Load("{}");

        Assert.Equal(0.25f, cfg.SurfaceMin);
        Assert.Equal("stone", cfg.Materials.Stone);
        Assert.Empty(cfg.Ores);
    }

    [Theory]
    [InlineData("""{"surfaceMin": 0.5, "surfaceMax": 0.4}""", "surfaceMin")]
    [InlineData("""{"deepLayerStart": 0.2}""", "deepLayerStart")]
    [InlineData("""{"terrainOctaves": 0}""", "terrainOctaves")]
    [InlineData("""{"dirtDepth": 3, "dirtDepthVariation": 5}""", "dirtDepth")]
    [InlineData("""{"wormLengthMin": 50, "wormLengthMax": 10}""", "wormLength")]
    [InlineData("""{"spawnFlatWidth": 1}""", "spawnFlatWidth")]
    [InlineData("""{"materials": null}""", "materials")]
    [InlineData("""{"ores": [{"block": "ore_a", "minDepth": 0.8, "maxDepth": 0.5, "veinsPerMillionTiles": 1, "veinSizeMin": 1, "veinSizeMax": 2}]}""", "ores[0]")]
    [InlineData("""{"ores": [null]}""", "ores[0]")]
    [InlineData("""{"surfacMin": 0.3}""", "worldgen.json")]
    [InlineData("""{""", "worldgen.json")]
    public void InvalidConfig_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => WorldGenConfigLoader.Load(json));

        Assert.Contains("worldgen.json", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void UnknownMaterial_ThrowsOnResolve()
    {
        var cfg = WorldGenConfigLoader.Load("""{"materials": {"stone": "granito"}}""");

        var ex = Assert.Throws<DataException>(() => WorldGenMaterials.Resolve(cfg, TestData.Registry()));

        Assert.Contains("materials.stone", ex.Message);
        Assert.Contains("granito", ex.Message);
    }

    [Fact]
    public void UnknownOreBlock_ThrowsOnResolve()
    {
        var cfg = WorldGenConfigLoader.Load(
            """{"ores": [{"block": "mithril", "minDepth": 0.1, "maxDepth": 0.5, "veinsPerMillionTiles": 1, "veinSizeMin": 1, "veinSizeMax": 2}]}""");

        var ex = Assert.Throws<DataException>(() => WorldGenMaterials.Resolve(cfg, TestData.Registry()));

        Assert.Contains("ores[0].block", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zyon-wg-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<DataException>(() => WorldGenConfigLoader.LoadFromDirectory(dir));
            Assert.Contains("worldgen.json", ex.Message);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenConfigTests"`
Expected: FAIL na compilação — `The type or namespace name 'WorldGen' does not exist`.

- [ ] **Step 3: Tornar as opções JSON reutilizáveis**

Em `src/Zyon.Core/Data/RegistryLoader.cs`, trocar a declaração

```csharp
    private static readonly JsonSerializerOptions Options = new()
```

por

```csharp
    internal static readonly JsonSerializerOptions Options = new()
```

(nenhuma outra mudança no arquivo).

- [ ] **Step 4: Implementar a configuração**

`src/Zyon.Core/WorldGen/WorldGenConfig.cs`:

```csharp
namespace Zyon.Core.WorldGen;

/// <summary>Parâmetros da geração de mundo. Frações são relativas à altura do mundo; profundidades em tiles.</summary>
public sealed class WorldGenConfig
{
    public MaterialNames Materials { get; init; } = new();
    public float SurfaceMin { get; init; } = 0.25f;
    public float SurfaceMax { get; init; } = 0.35f;
    public float TerrainFrequency { get; init; } = 0.004f;
    public int TerrainOctaves { get; init; } = 4;
    public int DirtDepth { get; init; } = 12;
    public int DirtDepthVariation { get; init; } = 5;
    public float DeepLayerStart { get; init; } = 0.7f;
    public int DeepLayerVariation { get; init; } = 8;
    public float LayerNoiseFrequency { get; init; } = 0.03f;
    public float CaveFrequency { get; init; } = 0.025f;
    public float CaveThreshold { get; init; } = 0.6f;
    public int CaveMinDepth { get; init; } = 15;
    public int BottomSolidRows { get; init; } = 3;
    public float WormsPerThousandColumns { get; init; } = 4f;
    public int WormLengthMin { get; init; } = 80;
    public int WormLengthMax { get; init; } = 300;
    public float WormRadiusMin { get; init; } = 1.2f;
    public float WormRadiusMax { get; init; } = 2.5f;
    public List<OreConfig> Ores { get; init; } = [];
    public int SpawnSearchRadius { get; init; } = 300;
    public int SpawnFlatWidth { get; init; } = 4;
}

/// <summary>Nomes (ids textuais do Registry) dos materiais usados pela geração.</summary>
public sealed class MaterialNames
{
    public string Dirt { get; init; } = "dirt";
    public string Stone { get; init; } = "stone";
    public string DeepStone { get; init; } = "deep_stone";
    public string DirtWall { get; init; } = "dirt_wall";
    public string StoneWall { get; init; } = "stone_wall";
}

/// <summary>Veios de um minério. MinDepth/MaxDepth são frações da altura do mundo.</summary>
public sealed class OreConfig
{
    public string Block { get; init; } = "";
    public float MinDepth { get; init; }
    public float MaxDepth { get; init; }
    public float VeinsPerMillionTiles { get; init; }
    public int VeinSizeMin { get; init; }
    public int VeinSizeMax { get; init; }
}
```

`src/Zyon.Core/WorldGen/WorldGenConfigLoader.cs`:

```csharp
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen;

public static class WorldGenConfigLoader
{
    public const string FileName = "worldgen.json";

    public static WorldGenConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path))
            throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        return Load(File.ReadAllText(path));
    }

    public static WorldGenConfig Load(string json)
    {
        WorldGenConfig config;
        try
        {
            config = JsonSerializer.Deserialize<WorldGenConfig>(json, RegistryLoader.Options)
                     ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}");
        }
        Validate(config);
        return config;
    }

    private static void Validate(WorldGenConfig c)
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new DataException($"{FileName}: {message}");
        }

        Require(c.Materials is not null, "campo 'materials' não pode ser nulo");
        Require(c.Ores is not null, "campo 'ores' não pode ser nulo");
        Require(c.SurfaceMin > 0 && c.SurfaceMin < c.SurfaceMax && c.SurfaceMax < 1,
            "'surfaceMin' e 'surfaceMax' devem satisfazer 0 < surfaceMin < surfaceMax < 1");
        Require(c.DeepLayerStart > c.SurfaceMax && c.DeepLayerStart < 1,
            "'deepLayerStart' deve estar entre 'surfaceMax' e 1");
        Require(c.TerrainFrequency > 0 && c.LayerNoiseFrequency > 0 && c.CaveFrequency > 0,
            "frequências de ruído devem ser > 0");
        Require(c.TerrainOctaves >= 1, "'terrainOctaves' deve ser >= 1");
        Require(c.DirtDepth - c.DirtDepthVariation >= 1, "'dirtDepth' deve exceder 'dirtDepthVariation'");
        Require(c.DeepLayerVariation >= 0, "'deepLayerVariation' deve ser >= 0");
        Require(c.CaveMinDepth >= 1, "'caveMinDepth' deve ser >= 1");
        Require(c.BottomSolidRows >= 1, "'bottomSolidRows' deve ser >= 1");
        Require(c.WormsPerThousandColumns >= 0, "'wormsPerThousandColumns' deve ser >= 0");
        Require(c.WormLengthMin >= 1 && c.WormLengthMin <= c.WormLengthMax,
            "'wormLengthMin' e 'wormLengthMax' devem satisfazer 1 <= min <= max");
        Require(c.WormRadiusMin > 0 && c.WormRadiusMin <= c.WormRadiusMax,
            "'wormRadiusMin' e 'wormRadiusMax' devem satisfazer 0 < min <= max");
        Require(c.SpawnSearchRadius >= 0, "'spawnSearchRadius' deve ser >= 0");
        Require(c.SpawnFlatWidth >= 2, "'spawnFlatWidth' deve ser >= 2");

        for (int i = 0; i < c.Ores!.Count; i++)
        {
            var o = c.Ores[i];
            var ctx = $"ores[{i}]";
            Require(o is not null, $"{ctx}: entrada nula");
            Require(!string.IsNullOrWhiteSpace(o!.Block), $"{ctx}: campo 'block' ausente");
            Require(o.MinDepth >= 0 && o.MinDepth < o.MaxDepth && o.MaxDepth <= 1,
                $"{ctx}: 'minDepth' e 'maxDepth' devem satisfazer 0 <= min < max <= 1");
            Require(o.VeinsPerMillionTiles >= 0, $"{ctx}: 'veinsPerMillionTiles' deve ser >= 0");
            Require(o.VeinSizeMin >= 1 && o.VeinSizeMin <= o.VeinSizeMax,
                $"{ctx}: 'veinSizeMin' e 'veinSizeMax' devem satisfazer 1 <= min <= max");
        }
    }
}
```

`src/Zyon.Core/WorldGen/WorldGenMaterials.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen;

/// <summary>Ids numéricos dos materiais da geração, resolvidos pelo nome no Registry.</summary>
public sealed record WorldGenMaterials(
    ushort Dirt,
    ushort Stone,
    ushort DeepStone,
    ushort DirtWall,
    ushort StoneWall,
    IReadOnlyList<(ushort Block, OreConfig Config)> Ores)
{
    public static WorldGenMaterials Resolve(WorldGenConfig config, Registry registry)
    {
        ushort Block(string name, string field) =>
            registry.TryGetBlockId(name, out var id)
                ? id
                : throw new DataException($"{WorldGenConfigLoader.FileName}: '{field}' referencia bloco inexistente '{name}'");

        ushort Wall(string name, string field) =>
            registry.TryGetWallId(name, out var id)
                ? id
                : throw new DataException($"{WorldGenConfigLoader.FileName}: '{field}' referencia parede inexistente '{name}'");

        var m = config.Materials;
        var ores = config.Ores
            .Select((ore, i) => (Block(ore.Block, $"ores[{i}].block"), ore))
            .ToList();

        return new WorldGenMaterials(
            Block(m.Dirt, "materials.dirt"),
            Block(m.Stone, "materials.stone"),
            Block(m.DeepStone, "materials.deepStone"),
            Wall(m.DirtWall, "materials.dirtWall"),
            Wall(m.StoneWall, "materials.stoneWall"),
            ores);
    }
}
```

- [ ] **Step 5: Criar `data/worldgen.json`**

```json
{
  "materials": {
    "dirt": "dirt",
    "stone": "stone",
    "deepStone": "deep_stone",
    "dirtWall": "dirt_wall",
    "stoneWall": "stone_wall"
  },
  "surfaceMin": 0.25,
  "surfaceMax": 0.35,
  "terrainFrequency": 0.004,
  "terrainOctaves": 4,
  "dirtDepth": 12,
  "dirtDepthVariation": 5,
  "deepLayerStart": 0.7,
  "deepLayerVariation": 8,
  "layerNoiseFrequency": 0.03,
  "caveFrequency": 0.025,
  "caveThreshold": 0.6,
  "caveMinDepth": 15,
  "bottomSolidRows": 3,
  "wormsPerThousandColumns": 4,
  "wormLengthMin": 80,
  "wormLengthMax": 300,
  "wormRadiusMin": 1.2,
  "wormRadiusMax": 2.5,
  "ores": [
    { "block": "ore_a", "minDepth": 0.35, "maxDepth": 0.75, "veinsPerMillionTiles": 60, "veinSizeMin": 6, "veinSizeMax": 14 },
    { "block": "ore_b", "minDepth": 0.65, "maxDepth": 1.0, "veinsPerMillionTiles": 30, "veinSizeMin": 4, "veinSizeMax": 10 }
  ],
  "spawnSearchRadius": 300,
  "spawnFlatWidth": 4
}
```

- [ ] **Step 6: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenConfigTests"`
Expected: PASS, 16 testes (RealFile, EmptyJson, 11 casos de InvalidConfig, UnknownMaterial, UnknownOreBlock, LoadFromDirectory).

- [ ] **Step 7: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos (125 + 16 = 141).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(worldgen): configuração da geração em worldgen.json com validação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Hashes estáveis (`StableHash`, `TileWorldHash`)

**Files:**
- Create: `src/Zyon.Core/WorldGen/StableHash.cs`, `src/Zyon.Core/Tiles/TileWorldHash.cs`
- Test: `tests/Zyon.Core.Tests/HashTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/HashTests.cs`:

```csharp
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class HashTests
{
    [Theory]
    [InlineData(0, "terrain", 135734238)]
    [InlineData(42, "caves.cheese", 861942530)]
    [InlineData(-1, "", -485093455)]
    public void StableHash_MatchesReferenceFnv1a(int seed, string name, int expected)
    {
        Assert.Equal(expected, StableHash.Seed(seed, name));
    }

    [Fact]
    public void StableHash_DependsOnSeedAndName()
    {
        Assert.NotEqual(StableHash.Seed(1, "terrain"), StableHash.Seed(2, "terrain"));
        Assert.NotEqual(StableHash.Seed(1, "terrain"), StableHash.Seed(1, "caves"));
    }

    [Fact]
    public void TileWorldHash_EqualWorlds_EqualHash()
    {
        var a = new TileWorld(100, 40);
        var b = new TileWorld(100, 40);
        a.SetBlock(5, 5, 3);
        b.SetBlock(5, 5, 3);

        Assert.Equal(TileWorldHash.Compute(a), TileWorldHash.Compute(b));
    }

    [Fact]
    public void TileWorldHash_ChangesWithBlocksWallsAndSize()
    {
        var baseWorld = new TileWorld(100, 40);
        ulong h = TileWorldHash.Compute(baseWorld);

        var block = new TileWorld(100, 40);
        block.SetBlock(99, 39, 1);
        var wall = new TileWorld(100, 40);
        wall.SetWall(0, 0, 1);
        var size = new TileWorld(100, 41);

        Assert.NotEqual(h, TileWorldHash.Compute(block));
        Assert.NotEqual(h, TileWorldHash.Compute(wall));
        Assert.NotEqual(h, TileWorldHash.Compute(size));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~HashTests"`
Expected: FAIL na compilação — `StableHash` e `TileWorldHash` não existem.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/StableHash.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;

namespace Zyon.Core.WorldGen;

/// <summary>FNV-1a 32 bits de (seed, nome). Estável entre execuções e versões do .NET, ao contrário de string.GetHashCode.</summary>
internal static class StableHash
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    public static int Seed(int worldSeed, string name)
    {
        uint h = OffsetBasis;
        Span<byte> seedBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(seedBytes, worldSeed);
        foreach (byte b in seedBytes)
            h = (h ^ b) * Prime;
        foreach (byte b in Encoding.UTF8.GetBytes(name))
            h = (h ^ b) * Prime;
        return unchecked((int)h);
    }
}
```

`src/Zyon.Core/Tiles/TileWorldHash.cs`:

```csharp
namespace Zyon.Core.Tiles;

/// <summary>Hash FNV-1a 64 bits do conteúdo de um TileWorld (dimensões, blocos e paredes). Usado para verificar determinismo.</summary>
public static class TileWorldHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(TileWorld world)
    {
        ulong h = OffsetBasis;
        h = Mix32(h, (uint)world.Width);
        h = Mix32(h, (uint)world.Height);
        foreach (var chunk in world.Chunks)
        {
            foreach (ushort b in chunk.Blocks) h = Mix16(h, b);
            foreach (ushort w in chunk.Walls) h = Mix16(h, w);
        }
        return h;
    }

    private static ulong Mix16(ulong h, ushort v)
    {
        h = (h ^ (byte)v) * Prime;
        return (h ^ (byte)(v >> 8)) * Prime;
    }

    private static ulong Mix32(ulong h, uint v)
    {
        for (int i = 0; i < 4; i++)
            h = (h ^ (byte)(v >> (8 * i))) * Prime;
        return h;
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~HashTests"`
Expected: PASS, 6 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): hashes estáveis para sementes e conteúdo do mundo" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Contexto, contrato de passe e `WorldGenerator`

**Files:**
- Modify: `src/Zyon.Core/Sim/Events.cs` (+ `WorldGenProgress`)
- Create: `src/Zyon.Core/WorldGen/WorldSize.cs`, `IWorldGenPass.cs`, `WorldGenContext.cs`, `WorldGenerator.cs`
- Create: `tests/Zyon.Core.Tests/ListProgress.cs`, `tests/Zyon.Core.Tests/WorldGenTestKit.cs`
- Test: `tests/Zyon.Core.Tests/WorldGeneratorTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/ListProgress.cs`:

```csharp
namespace Zyon.Core.Tests;

/// <summary>IProgress síncrono (Progress&lt;T&gt; posta no SynchronizationContext e não serve para testes).</summary>
internal sealed class ListProgress<T> : IProgress<T>
{
    public List<T> Items { get; } = new();

    public void Report(T value) => Items.Add(value);
}
```

`tests/Zyon.Core.Tests/WorldGenTestKit.cs`:

```csharp
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

internal static class WorldGenTestKit
{
    public const int Width = 512;
    public const int Height = 256;
    public const int Seed = 7;

    public static WorldGenContext Context(int width = Width, int height = Height, int seed = Seed)
    {
        var registry = TestData.Registry();
        var config = TestData.WorldGenConfig();
        return new WorldGenContext(new TileWorld(width, height), registry, config,
            WorldGenMaterials.Resolve(config, registry), seed, _ => { });
    }

    /// <summary>Roda os passes em sequência num mundo de teste, do mesmo jeito que o gerador.</summary>
    public static WorldGenContext Run(params IWorldGenPass[] passes)
    {
        var ctx = Context();
        foreach (var pass in passes)
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }
}
```

`tests/Zyon.Core.Tests/WorldGeneratorTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGeneratorTests
{
    /// <summary>Escreve 200 blocos em posições sorteadas pelo Random do passe.</summary>
    private sealed class ScatterPass(string name, ushort block) : IWorldGenPass
    {
        public string Name => name;

        public void Apply(WorldGenContext ctx)
        {
            for (int i = 0; i < 200; i++)
                ctx.World.SetBlock(ctx.Random.Next(ctx.World.Width), ctx.Random.Next(ctx.World.Height), block);
        }
    }

    /// <summary>Consome aleatoriedade sem escrever nada.</summary>
    private sealed class ConsumeRandomPass : IWorldGenPass
    {
        public string Name => "consume";

        public void Apply(WorldGenContext ctx)
        {
            for (int i = 0; i < 1000; i++) ctx.Random.Next();
        }
    }

    private sealed class FixedSpawnPass : IWorldGenPass
    {
        public string Name => "spawn";

        public void Apply(WorldGenContext ctx)
        {
            ctx.Report(0.5f);
            ctx.SetSpawn(new Vector2(10.5f, 20f));
        }
    }

    private static WorldGenerator Generator(params IWorldGenPass[] passes) =>
        new(TestData.Registry(), TestData.WorldGenConfig(), passes);

    [Fact]
    public void Generate_RunsPassesAndReturnsSpawn()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        var world = gen.Generate(128, 64, seed: 3);

        Assert.Equal(128, world.Tiles.Width);
        Assert.Equal(3, world.Seed);
        Assert.Equal(WorldGenerator.Version, world.GeneratorVersion);
        Assert.Equal(new Vector2(10.5f, 20f), world.SpawnFeet);
        Assert.Contains(world.Tiles.Chunks, c => c.Blocks.Contains((ushort)1));
    }

    [Fact]
    public void SameSeed_SameWorld_DifferentSeed_DifferentWorld()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        ulong a = TileWorldHash.Compute(gen.Generate(128, 64, 5).Tiles);
        ulong b = TileWorldHash.Compute(gen.Generate(128, 64, 5).Tiles);
        ulong c = TileWorldHash.Compute(gen.Generate(128, 64, 6).Tiles);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void InsertingAPass_DoesNotChangeTheOthersRandomness()
    {
        var original = Generator(new ScatterPass("a", 1), new ScatterPass("b", 2), new FixedSpawnPass());
        var withExtra = Generator(new ScatterPass("a", 1), new ConsumeRandomPass(), new ScatterPass("b", 2), new FixedSpawnPass());

        Assert.Equal(
            TileWorldHash.Compute(original.Generate(128, 64, 9).Tiles),
            TileWorldHash.Compute(withExtra.Generate(128, 64, 9).Tiles));
    }

    [Fact]
    public void Progress_IsReportedPerPass_MonotonicAndEndsAtOne()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());
        var progress = new ListProgress<WorldGenProgress>();

        gen.Generate(128, 64, 1, progress);

        Assert.Equal("a", progress.Items[0].Pass);
        Assert.Equal(0f, progress.Items[0].Fraction);
        Assert.Contains(progress.Items, p => p.Pass == "spawn" && p.Fraction == 0.75f);
        Assert.Equal(1f, progress.Items[^1].Fraction);
        Assert.All(progress.Items, p => Assert.Equal(2, p.PassCount));
        for (int i = 1; i < progress.Items.Count; i++)
            Assert.True(progress.Items[i].Fraction >= progress.Items[i - 1].Fraction);
    }

    [Fact]
    public void Generate_ClearsDirtyFlags()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        var world = gen.Generate(128, 64, 1);

        Assert.All(world.Tiles.Chunks, c => Assert.False(c.Dirty));
    }

    [Fact]
    public void Generate_Cancelled_Throws()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => gen.Generate(128, 64, 1, cancellationToken: cts.Token));
    }

    [Fact]
    public void Generate_WithoutSpawnPass_Throws()
    {
        var gen = Generator(new ScatterPass("a", 1));

        Assert.Throws<InvalidOperationException>(() => gen.Generate(128, 64, 1));
    }

    [Fact]
    public void Generate_TooSmall_Throws()
    {
        var gen = Generator(new FixedSpawnPass());

        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(WorldGenerator.MinSize - 1, 128, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(128, WorldGenerator.MinSize - 1, 1));
    }

    [Theory]
    [InlineData(WorldSize.Small, 4200, 1200)]
    [InlineData(WorldSize.Medium, 6400, 1800)]
    [InlineData(WorldSize.Large, 8400, 2400)]
    public void WorldSizes_MatchSpec(WorldSize size, int width, int height)
    {
        Assert.Equal((width, height), WorldSizes.Dimensions(size));
    }

    [Fact]
    public void PlayerSpawnCenter_IsHalfAPlayerAboveTheFeet()
    {
        var gen = Generator(new FixedSpawnPass());
        var world = gen.Generate(128, 64, 1);
        var physics = TestData.Registry().Physics;

        var center = world.PlayerSpawnCenter(physics);

        Assert.Equal(new Vector2(10.5f, 20f - physics.PlayerHeight / 2), center);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGeneratorTests"`
Expected: FAIL na compilação — `IWorldGenPass`, `WorldGenContext`, `WorldGenerator`, `WorldGenProgress`, `WorldSize` não existem.

- [ ] **Step 3: Adicionar o evento de progresso**

Ao final de `src/Zyon.Core/Sim/Events.cs`:

```csharp

/// <summary>Progresso da geração de mundo: passe atual e fração total em [0, 1].</summary>
public sealed record WorldGenProgress(string Pass, int PassIndex, int PassCount, float Fraction) : IGameEvent;
```

- [ ] **Step 4: Implementar tamanhos, contrato e contexto**

`src/Zyon.Core/WorldGen/WorldSize.cs`:

```csharp
namespace Zyon.Core.WorldGen;

public enum WorldSize { Small, Medium, Large }

public static class WorldSizes
{
    public static (int Width, int Height) Dimensions(WorldSize size) => size switch
    {
        WorldSize.Small => (4200, 1200),
        WorldSize.Medium => (6400, 1800),
        WorldSize.Large => (8400, 2400),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };
}
```

`src/Zyon.Core/WorldGen/IWorldGenPass.cs`:

```csharp
namespace Zyon.Core.WorldGen;

/// <summary>Uma etapa da geração. O nome semeia o Random e os ruídos do passe, então deve ser único e estável.</summary>
public interface IWorldGenPass
{
    string Name { get; }
    void Apply(WorldGenContext ctx);
}
```

`src/Zyon.Core/WorldGen/WorldGenContext.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen;

/// <summary>
/// Estado compartilhado entre os passes. Dentro de Parallel.For use só ruído e escreva só na própria coluna:
/// Random e Report não são thread-safe nem determinísticos fora de código sequencial.
/// </summary>
public sealed class WorldGenContext
{
    private readonly Action<float> report;

    internal WorldGenContext(TileWorld world, Registry registry, WorldGenConfig config,
        WorldGenMaterials materials, int seed, Action<float> report)
    {
        World = world;
        Registry = registry;
        Config = config;
        Materials = materials;
        Seed = seed;
        this.report = report;
        Surface = new int[world.Width];
        DirtBottom = new int[world.Width];
    }

    public TileWorld World { get; }
    public Registry Registry { get; }
    public WorldGenConfig Config { get; }
    public WorldGenMaterials Materials { get; }
    public int Seed { get; }

    /// <summary>Random exclusivo do passe atual, derivado de seed + nome do passe.</summary>
    public Random Random { get; internal set; } = new(0);

    /// <summary>Linha do primeiro tile sólido de cada coluna (definida pelo passe de terreno).</summary>
    public int[] Surface { get; }

    /// <summary>Primeira linha abaixo da camada de solo de cada coluna (definida pelo passe de camadas).</summary>
    public int[] DirtBottom { get; }

    /// <summary>Ponto sob os pés do jogador no spawn: X no centro, Y na linha de cima do chão.</summary>
    public Vector2 SpawnFeet { get; private set; }

    public bool SpawnFound { get; private set; }

    public void SetSpawn(Vector2 feet)
    {
        SpawnFeet = feet;
        SpawnFound = true;
    }

    /// <summary>Progresso do passe atual em [0, 1]. Somente em código sequencial.</summary>
    public void Report(float fraction) => report(Math.Clamp(fraction, 0f, 1f));

    /// <summary>Ruído OpenSimplex2 semeado por seed + nome (fBm quando octaves > 1). GetNoise é puro e pode ser usado em paralelo.</summary>
    public FastNoiseLite CreateNoise(string name, float frequency, int octaves)
    {
        var noise = new FastNoiseLite(StableHash.Seed(Seed, name));
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        if (octaves > 1)
        {
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(octaves);
        }
        return noise;
    }
}
```

- [ ] **Step 5: Implementar o gerador**

`src/Zyon.Core/WorldGen/WorldGenerator.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen;

public sealed record GeneratedWorld(TileWorld Tiles, int Seed, int GeneratorVersion, Vector2 SpawnFeet)
{
    /// <summary>Centro do AABB do jogador em pé no ponto de spawn.</summary>
    public Vector2 PlayerSpawnCenter(PhysicsConfig physics) => SpawnFeet - new Vector2(0f, physics.PlayerHeight / 2);
}

/// <summary>Roda os passes em ordem. Mesma seed + mesma Version + mesmos dados ⇒ mesmo mundo.</summary>
public sealed class WorldGenerator
{
    /// <summary>Incremente sempre que a saída da geração mudar para a mesma seed.</summary>
    public const int Version = 1;
    public const int MinSize = 64;

    private readonly Registry registry;
    private readonly WorldGenConfig config;
    private readonly WorldGenMaterials materials;

    public WorldGenerator(Registry registry, WorldGenConfig config, IReadOnlyList<IWorldGenPass> passes)
    {
        this.registry = registry;
        this.config = config;
        Passes = passes;
        materials = WorldGenMaterials.Resolve(config, registry);
    }

    public IReadOnlyList<IWorldGenPass> Passes { get; }

    public GeneratedWorld Generate(int width, int height, int seed,
        IProgress<WorldGenProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (width < MinSize) throw new ArgumentOutOfRangeException(nameof(width), $"Largura mínima: {MinSize}");
        if (height < MinSize) throw new ArgumentOutOfRangeException(nameof(height), $"Altura mínima: {MinSize}");

        var world = new TileWorld(width, height);
        int current = 0;
        int count = Passes.Count;
        var ctx = new WorldGenContext(world, registry, config, materials, seed,
            f => progress?.Report(new WorldGenProgress(Passes[current].Name, current, count, (current + f) / count)));

        for (current = 0; current < count; current++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ctx.Report(0f);
            RunPass(ctx, Passes[current]);
            ctx.Report(1f);
        }

        if (!ctx.SpawnFound)
            throw new InvalidOperationException("Nenhum passe definiu o ponto de spawn.");

        foreach (var chunk in world.Chunks)
            chunk.Dirty = false;
        return new GeneratedWorld(world, seed, Version, ctx.SpawnFeet);
    }

    /// <summary>Prepara o Random do passe (seed + nome) e o executa.</summary>
    internal static void RunPass(WorldGenContext ctx, IWorldGenPass pass)
    {
        ctx.Random = new Random(StableHash.Seed(ctx.Seed, pass.Name));
        pass.Apply(ctx);
    }
}
```

- [ ] **Step 6: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGeneratorTests"`
Expected: PASS, 12 testes.

- [ ] **Step 7: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(worldgen): WorldGenerator com passes, Random por passe e progresso" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Passe de terreno

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/TerrainPass.cs`
- Test: `tests/Zyon.Core.Tests/WorldGenPassTests.cs` (arquivo novo; tarefas seguintes acrescentam testes nele)

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/WorldGenPassTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class WorldGenPassTests
{
    [Fact]
    public void Terrain_SurfaceWithinBand_AirAbove_SolidBelow()
    {
        var ctx = Run(new TerrainPass());
        var cfg = ctx.Config;
        int min = (int)MathF.Round(cfg.SurfaceMin * Height);
        int max = (int)MathF.Round(cfg.SurfaceMax * Height);

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x];
            Assert.InRange(s, min, max);
            Assert.Equal(0, ctx.World.GetBlock(x, s - 1));
            Assert.NotEqual(0, ctx.World.GetBlock(x, s));
            Assert.NotEqual(0, ctx.World.GetBlock(x, Height - 1));
        }
    }

    [Fact]
    public void Terrain_IsNotFlat()
    {
        var ctx = Run(new TerrainPass());

        Assert.True(ctx.Surface.Distinct().Count() > 3);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL na compilação — `Zyon.Core.WorldGen.Passes` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/Passes/TerrainPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>Define a superfície por ruído 1D e preenche tudo abaixo dela com rocha (as camadas refinam depois).</summary>
internal sealed class TerrainPass : IWorldGenPass
{
    public string Name => "terrain";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int height = world.Height;
        float min = cfg.SurfaceMin * height;
        float max = cfg.SurfaceMax * height;
        ushort fill = ctx.Materials.Stone;
        var noise = ctx.CreateNoise("terrain", cfg.TerrainFrequency, cfg.TerrainOctaves);

        Parallel.For(0, world.Width, x =>
        {
            float t = Math.Clamp((noise.GetNoise(x, 0f) + 1f) * 0.5f, 0f, 1f);
            int surface = (int)MathF.Round(min + (max - min) * t);
            ctx.Surface[x] = surface;
            for (int y = surface; y < height; y++)
                world.SetBlock(x, y, fill);
        });
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: PASS, 2 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): passe de terreno por ruído 1D" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Passe de camadas

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/LayersPass.cs`
- Modify (test): `tests/Zyon.Core.Tests/WorldGenPassTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

Adicionar dentro da classe `WorldGenPassTests`:

```csharp
    [Fact]
    public void Layers_DirtOnTop_StoneBelow_DeepStoneAtBottom()
    {
        var ctx = Run(new TerrainPass(), new LayersPass());
        var m = ctx.Materials;

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x];
            int d = ctx.DirtBottom[x];
            Assert.True(d > s, $"coluna {x}: DirtBottom {d} <= superfície {s}");
            Assert.Equal(m.Dirt, ctx.World.GetBlock(x, s));
            Assert.Equal(m.Dirt, ctx.World.GetBlock(x, d - 1));
            Assert.Equal(m.Stone, ctx.World.GetBlock(x, d));
            Assert.Equal(m.DeepStone, ctx.World.GetBlock(x, Height - 1));
        }
    }

    [Fact]
    public void Layers_BoundaryIsIrregular()
    {
        var ctx = Run(new TerrainPass(), new LayersPass());

        var depths = Enumerable.Range(0, Width).Select(x => ctx.DirtBottom[x] - ctx.Surface[x]);
        Assert.True(depths.Distinct().Count() > 2);
    }
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL na compilação — `LayersPass` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/Passes/LayersPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>Solo perto da superfície, rocha no meio e rocha profunda embaixo, com fronteiras irregulares por ruído.</summary>
internal sealed class LayersPass : IWorldGenPass
{
    public string Name => "layers";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        var m = ctx.Materials;
        int height = world.Height;
        var dirtNoise = ctx.CreateNoise("layers.dirt", cfg.LayerNoiseFrequency, 2);
        var deepNoise = ctx.CreateNoise("layers.deep", cfg.LayerNoiseFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = surface + cfg.DirtDepth + (int)MathF.Round(dirtNoise.GetNoise(x, 0f) * cfg.DirtDepthVariation);
            dirtBottom = Math.Clamp(dirtBottom, surface + 1, height);
            int deepStart = (int)MathF.Round(cfg.DeepLayerStart * height + deepNoise.GetNoise(x, 0f) * cfg.DeepLayerVariation);
            ctx.DirtBottom[x] = dirtBottom;

            for (int y = surface; y < height; y++)
                world.SetBlock(x, y, y < dirtBottom ? m.Dirt : y < deepStart ? m.Stone : m.DeepStone);
        });
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: PASS, 4 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): passe de camadas solo/rocha/rocha profunda" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Passe de cavernas

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/CavesPass.cs`
- Modify (test): `tests/Zyon.Core.Tests/WorldGenPassTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Adicionar dentro da classe `WorldGenPassTests`:

```csharp
    [Fact]
    public void Caves_CarveAirUnderground()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        var cfg = ctx.Config;
        int floor = Height - cfg.BottomSolidRows;

        int carved = 0;
        for (int x = 0; x < Width; x++)
            for (int y = ctx.Surface[x] + 1; y < floor; y++)
                if (ctx.World.GetBlock(x, y) == 0) carved++;

        Assert.True(carved > 200, $"só {carved} tiles escavados");
    }

    [Fact]
    public void Caves_KeepSurfaceTileAndBottomRowsSolid()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        int floor = Height - ctx.Config.BottomSolidRows;

        for (int x = 0; x < Width; x++)
        {
            Assert.NotEqual(0, ctx.World.GetBlock(x, ctx.Surface[x]));
            for (int y = floor; y < Height; y++)
                Assert.NotEqual(0, ctx.World.GetBlock(x, y));
        }
    }
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL na compilação — `CavesPass` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/Passes/CavesPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Cavernas "queijo" (limiar de ruído 2D, em paralelo) e túneis "minhoca" (caminhada aleatória, sequencial).
/// Nunca escava a linha da superfície nem as últimas BottomSolidRows linhas.
/// </summary>
internal sealed class CavesPass : IWorldGenPass
{
    public string Name => "caves";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int floor = world.Height - cfg.BottomSolidRows;
        var cheese = ctx.CreateNoise("caves.cheese", cfg.CaveFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            for (int y = ctx.Surface[x] + cfg.CaveMinDepth; y < floor; y++)
                if (cheese.GetNoise(x, y) > cfg.CaveThreshold)
                    world.SetBlock(x, y, 0);
        });
        ctx.Report(0.5f);

        var rng = ctx.Random;
        int worms = (int)(world.Width * cfg.WormsPerThousandColumns / 1000f);
        for (int i = 0; i < worms; i++)
        {
            int startX = rng.Next(world.Width);
            int top = Math.Min(ctx.Surface[startX] + cfg.CaveMinDepth, floor - 1);
            float x = startX;
            float y = rng.Next(top, floor);
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            int length = rng.Next(cfg.WormLengthMin, cfg.WormLengthMax + 1);
            float radius = cfg.WormRadiusMin + (float)rng.NextDouble() * (cfg.WormRadiusMax - cfg.WormRadiusMin);

            for (int step = 0; step < length; step++)
            {
                Carve(ctx, x, y, radius, floor);
                angle += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                x += MathF.Cos(angle);
                y += MathF.Sin(angle) * 0.6f;
                if (x < 1 || x >= world.Width - 1 || y >= floor) break;
            }
            ctx.Report(0.5f + 0.5f * (i + 1) / worms);
        }
    }

    private static void Carve(WorldGenContext ctx, float cx, float cy, float radius, int floor)
    {
        var world = ctx.World;
        int x0 = (int)MathF.Floor(cx - radius), x1 = (int)MathF.Ceiling(cx + radius);
        int y0 = (int)MathF.Floor(cy - radius), y1 = (int)MathF.Ceiling(cy + radius);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (!world.InBounds(x, y) || y >= floor || y <= ctx.Surface[x]) continue;
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            if (dx * dx + dy * dy <= r2)
                world.SetBlock(x, y, 0);
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: PASS, 6 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): cavernas por ruído 2D e túneis por caminhada aleatória" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Passe de minérios

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/OrePass.cs`
- Modify (test): `tests/Zyon.Core.Tests/WorldGenPassTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

Adicionar dentro da classe `WorldGenPassTests`:

```csharp
    [Fact]
    public void Ores_AppearOnlyWithinTheirDepthBand()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass());

        foreach (var (block, ore) in ctx.Materials.Ores)
        {
            int yMin = (int)(ore.MinDepth * Height);
            int yMax = Math.Max(yMin + 1, (int)(ore.MaxDepth * Height));
            int count = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (ctx.World.GetBlock(x, y) != block) continue;
                count++;
                Assert.InRange(y, yMin, yMax - 1);
            }
            Assert.True(count > 0, $"minério {ore.Block} não apareceu");
        }
    }

    [Fact]
    public void Ores_NeverReplaceDirtOrAir()
    {
        var before = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        var after = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass());
        var m = after.Materials;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            ushort old = before.World.GetBlock(x, y);
            if (old == 0 || old == m.Dirt)
                Assert.Equal(old, after.World.GetBlock(x, y));
        }
    }
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL na compilação — `OrePass` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/Passes/OrePass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>Veios de minério por caminhada aleatória, só substituindo rocha e rocha profunda dentro da faixa de profundidade.</summary>
internal sealed class OrePass : IWorldGenPass
{
    public string Name => "ores";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;
        var rng = ctx.Random;
        long area = (long)world.Width * world.Height;

        for (int i = 0; i < m.Ores.Count; i++)
        {
            var (block, ore) = m.Ores[i];
            int veins = (int)(area * ore.VeinsPerMillionTiles / 1_000_000.0);
            int yMin = (int)(ore.MinDepth * world.Height);
            int yMax = Math.Max(yMin + 1, (int)(ore.MaxDepth * world.Height));

            for (int v = 0; v < veins; v++)
            {
                int x = rng.Next(world.Width);
                int y = rng.Next(yMin, yMax);
                int size = rng.Next(ore.VeinSizeMin, ore.VeinSizeMax + 1);
                for (int s = 0; s < size; s++)
                {
                    if (world.InBounds(x, y) && y >= yMin && y < yMax)
                    {
                        ushort current = world.GetBlock(x, y);
                        if (current == m.Stone || current == m.DeepStone)
                            world.SetBlock(x, y, block);
                    }
                    x += rng.Next(-1, 2);
                    y += rng.Next(-1, 2);
                }
            }
            ctx.Report((i + 1f) / m.Ores.Count);
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: PASS, 8 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): veios de minério por faixa de profundidade" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Passe de paredes

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/WallsPass.cs`
- Modify (test): `tests/Zyon.Core.Tests/WorldGenPassTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

Adicionar dentro da classe `WorldGenPassTests`:

```csharp
    [Fact]
    public void Walls_FillBelowSurface_IncludingCaves_AndNothingAbove()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new WallsPass());
        var m = ctx.Materials;

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            ushort wall = ctx.World.GetWall(x, y);
            if (y <= ctx.Surface[x])
                Assert.Equal(0, wall);
            else
                Assert.Equal(y < ctx.DirtBottom[x] ? m.DirtWall : m.StoneWall, wall);
        }
    }
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: FAIL na compilação — `WallsPass` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/Passes/WallsPass.cs`:

```csharp
namespace Zyon.Core.WorldGen.Passes;

/// <summary>Paredes de fundo em todo tile abaixo da superfície (inclusive dentro das cavernas): solo na camada de solo, rocha abaixo.</summary>
internal sealed class WallsPass : IWorldGenPass
{
    public string Name => "walls";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;

        Parallel.For(0, world.Width, x =>
        {
            int dirtBottom = ctx.DirtBottom[x];
            for (int y = ctx.Surface[x] + 1; y < world.Height; y++)
                world.SetWall(x, y, y < dirtBottom ? m.DirtWall : m.StoneWall);
        });
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests"`
Expected: PASS, 9 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): paredes de fundo abaixo da superfície" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Passe de spawn e passes padrão

**Files:**
- Create: `src/Zyon.Core/WorldGen/Passes/SpawnPass.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenerator.cs` (passes padrão + construtor de conveniência)
- Modify (test): `tests/Zyon.Core.Tests/WorldGenPassTests.cs`
- Test: `tests/Zyon.Core.Tests/WorldGenDeterminismTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Adicionar dentro da classe `WorldGenPassTests`:

```csharp
    private static void AssertValidSpawn(WorldGenContext ctx)
    {
        Assert.True(ctx.SpawnFound);
        int feetY = (int)ctx.SpawnFeet.Y;
        Assert.Equal(ctx.SpawnFeet.Y, feetY);   // pés exatamente no topo de um tile
        int flat = ctx.Config.SpawnFlatWidth;
        int x0 = (int)MathF.Floor(ctx.SpawnFeet.X - flat / 2f);
        for (int x = x0; x < x0 + flat; x++)
        {
            Assert.NotEqual(0, ctx.World.GetBlock(x, feetY));
            for (int y = feetY - SpawnPass.ClearanceRows; y < feetY; y++)
                Assert.Equal(0, ctx.World.GetBlock(x, y));
        }
    }

    [Fact]
    public void Spawn_OnNaturalTerrain_IsFlatSolidAndClear()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass(), new WallsPass(), new SpawnPass());

        AssertValidSpawn(ctx);
        Assert.InRange(ctx.SpawnFeet.X, Width / 2f - ctx.Config.SpawnSearchRadius - 4, Width / 2f + ctx.Config.SpawnSearchRadius + 4);
    }

    [Fact]
    public void Spawn_WithoutFlatGround_FlattensAtCenter()
    {
        var ctx = Context();
        ushort dirt = ctx.Materials.Dirt;
        for (int x = 0; x < Width; x++)
        {
            ctx.Surface[x] = 100 + (x % 2);   // zigue-zague: nunca 2 colunas iguais seguidas
            for (int y = ctx.Surface[x]; y < Height; y++)
                ctx.World.SetBlock(x, y, dirt);
        }

        WorldGenerator.RunPass(ctx, new SpawnPass());

        AssertValidSpawn(ctx);
        Assert.Equal(Width / 2 + ctx.Config.SpawnFlatWidth / 2f, ctx.SpawnFeet.X);
    }
```

`tests/Zyon.Core.Tests/WorldGenDeterminismTests.cs`:

```csharp
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenDeterminismTests
{
    private static WorldGenerator Generator() => new(TestData.Registry(), TestData.WorldGenConfig());

    [Fact]
    public void DefaultPasses_AreInSpecOrder()
    {
        Assert.Equal(
            new[] { "terrain", "layers", "caves", "ores", "walls", "spawn" },
            Generator().Passes.Select(p => p.Name));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalWorld()
    {
        var a = Generator().Generate(512, 256, 1234);
        var b = Generator().Generate(512, 256, 1234);

        Assert.Equal(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
        Assert.Equal(a.SpawnFeet, b.SpawnFeet);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentWorlds()
    {
        var a = Generator().Generate(512, 256, 1);
        var b = Generator().Generate(512, 256, 2);

        Assert.NotEqual(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests|FullyQualifiedName~WorldGenDeterminismTests"`
Expected: FAIL na compilação — `SpawnPass` e o construtor `WorldGenerator(Registry, WorldGenConfig)` não existem.

- [ ] **Step 3: Implementar o passe de spawn**

`src/Zyon.Core/WorldGen/Passes/SpawnPass.cs`:

```csharp
using System.Numerics;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Procura, a partir do centro e alternando direita/esquerda, SpawnFlatWidth colunas com a mesma superfície,
/// chão sólido e ClearanceRows linhas livres acima. Sem achar, aplaina o terreno no centro.
/// </summary>
internal sealed class SpawnPass : IWorldGenPass
{
    public const int ClearanceRows = 4;

    public string Name => "spawn";

    public void Apply(WorldGenContext ctx)
    {
        int flat = ctx.Config.SpawnFlatWidth;
        int center = ctx.World.Width / 2;

        for (int i = 0; i <= ctx.Config.SpawnSearchRadius; i++)
        {
            foreach (int x0 in i == 0 ? new[] { center } : new[] { center + i, center - i })
            {
                if (!IsFlat(ctx, x0, flat)) continue;
                ctx.SetSpawn(new Vector2(x0 + flat / 2f, ctx.Surface[x0]));
                return;
            }
        }

        Flatten(ctx, center, flat);
        ctx.SetSpawn(new Vector2(center + flat / 2f, ctx.Surface[center]));
    }

    private static bool IsFlat(WorldGenContext ctx, int x0, int flat)
    {
        var world = ctx.World;
        if (x0 < 0 || x0 + flat > world.Width) return false;
        int s = ctx.Surface[x0];
        if (s - ClearanceRows < 0) return false;
        for (int x = x0; x < x0 + flat; x++)
        {
            if (ctx.Surface[x] != s || world.GetBlock(x, s) == 0) return false;
            for (int y = s - ClearanceRows; y < s; y++)
                if (world.GetBlock(x, y) != 0) return false;
        }
        return true;
    }

    private static void Flatten(WorldGenContext ctx, int x0, int flat)
    {
        var world = ctx.World;
        int s = ctx.Surface[x0];
        ushort dirt = ctx.Materials.Dirt;
        for (int x = x0; x < x0 + flat; x++)
        {
            for (int y = Math.Max(0, s - ClearanceRows); y < s; y++)
                world.SetBlock(x, y, 0);
            for (int y = s; y < Math.Max(ctx.Surface[x], s + 1); y++)
                world.SetBlock(x, y, dirt);
            if (world.GetBlock(x, s) == 0)
                world.SetBlock(x, s, dirt);
            ctx.Surface[x] = s;
        }
    }
}
```

- [ ] **Step 4: Passes padrão no gerador**

Em `src/Zyon.Core/WorldGen/WorldGenerator.cs`:

1. adicionar `using Zyon.Core.WorldGen.Passes;` no topo;
2. logo antes do construtor existente, adicionar:

```csharp
    /// <summary>Gerador com os passes padrão do spec (seção 6).</summary>
    public WorldGenerator(Registry registry, WorldGenConfig config)
        : this(registry, config, DefaultPasses())
    {
    }

    public static IReadOnlyList<IWorldGenPass> DefaultPasses() =>
    [
        new TerrainPass(),
        new LayersPass(),
        new CavesPass(),
        new OrePass(),
        new WallsPass(),
        new SpawnPass(),
    ];
```

- [ ] **Step 5: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenPassTests|FullyQualifiedName~WorldGenDeterminismTests"`
Expected: PASS, 14 testes (11 de passes + 3 de determinismo).

- [ ] **Step 6: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(worldgen): ponto de spawn e passes padrão do gerador" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Cenário jogável, desempenho e documentação

**Files:**
- Test: `tests/Zyon.Core.Tests/WorldGenScenarioTests.cs`, `tests/Zyon.Core.Tests/WorldGenPerformanceTests.cs`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Escrever os testes**

`tests/Zyon.Core.Tests/WorldGenScenarioTests.cs`:

```csharp
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenScenarioTests
{
    [Fact]
    public void PlayerSpawnedOnGeneratedWorld_StandsStillAndCanWalk()
    {
        var registry = TestData.Registry();
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(800, 400, 123);
        var spawn = generated.PlayerSpawnCenter(registry.Physics);
        using var sim = new Simulation(registry, generated.Tiles);
        int player = sim.SpawnPlayer(spawn);

        for (int i = 0; i < 120; i++) sim.Step();
        Assert.True(sim.TryGetEntityView(player, out var idle));
        Assert.Equal(spawn.Y, idle.Position.Y, 3);
        Assert.Equal(spawn.X, idle.Position.X, 3);

        sim.SetInput(player, new PlayerInput(1f, false, false, 0, 0));
        for (int i = 0; i < 120; i++) sim.Step();
        Assert.True(sim.TryGetEntityView(player, out var walked));
        Assert.True(walked.Position.X > spawn.X, "o jogador não andou");
        Assert.InRange(walked.Position.Y, 0f, generated.Tiles.Height);
    }
}
```

`tests/Zyon.Core.Tests/WorldGenPerformanceTests.cs`:

```csharp
using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void SmallWorld_GeneratesUnder10Seconds()
    {
        var generator = new WorldGenerator(TestData.Registry(), TestData.WorldGenConfig());
#if DEBUG
        // Em Debug só garante que o mundo pequeno inteiro roda; a meta de tempo vale em Release.
        var (width, height) = (1050, 300);
#else
        var (width, height) = WorldSizes.Dimensions(WorldSize.Small);
#endif

        var sw = Stopwatch.StartNew();
        var world = generator.Generate(width, height, 2026);
        sw.Stop();

        output.WriteLine($"Mundo {width}×{height} gerado em {sw.Elapsed.TotalSeconds:F2} s | hash {TileWorldHash.Compute(world.Tiles):X16}");
#if !DEBUG
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"geração levou {sw.Elapsed.TotalSeconds:F2} s");
#endif
    }
}
```

- [ ] **Step 2: Rodar os testes**

Run: `dotnet test --filter "FullyQualifiedName~WorldGenScenarioTests|FullyQualifiedName~WorldGenPerformanceTests"`
Expected: PASS, 2 testes. Se o cenário falhar, **não afrouxe o teste**: use superpowers:systematic-debugging (imprima o spawn, a superfície e as colunas sob os pés).

- [ ] **Step 3: Rodar o desempenho em Release**

Run: `dotnet test -c Release --filter "FullyQualifiedName~WorldGenPerformanceTests" --logger "console;verbosity=detailed"`
Expected: PASS com a linha `Mundo 4200×1200 gerado em X s` e X < 10. Se passar de 10 s, reporte o número — não otimize por conta própria.

- [ ] **Step 4: Atualizar o `CLAUDE.md`**

Na seção "Estrutura", depois do item de `data/`, adicionar:

```markdown
- `src/Zyon.Core/WorldGen` — geração de mundo: `WorldGenerator` roda passes (`terrain`, `layers`, `caves`, `ores`, `walls`, `spawn`); parâmetros em `data/worldgen.json`; FastNoiseLite vendorizada em `WorldGen/ThirdParty` (ver `THIRD_PARTY_NOTICES.md`, não editar).
```

Na seção "Regras invioláveis", adicionar:

```markdown
- Geração determinística: dentro de `Parallel.For` use só ruído e escreva só na própria coluna; nunca `ctx.Random` nem `ctx.Report`. Mudou a saída da geração para a mesma seed? Incremente `WorldGenerator.Version`.
- Nomes de passes semeiam Random e ruído: renomear um passe muda o mundo gerado.
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test(worldgen): cenário jogável e desempenho do mundo pequeno; docs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Verificação final

- [ ] **Step 1: Build limpo**

Run: `dotnet build -c Release`
Expected: 0 avisos, 0 erros.

- [ ] **Step 2: Suíte completa em Debug e em Release**

Run: `dotnet test` e depois `dotnet test -c Release`
Expected: PASS, todos os testes nas duas configurações.

- [ ] **Step 3: Core sem Godot e arquivo vendorizado intacto**

Run: `grep -ri "godot" src/ || echo "ok: nenhuma referência"` e `sha256sum src/Zyon.Core/WorldGen/ThirdParty/FastNoiseLite.cs`
Expected: `ok: nenhuma referência` e o hash `605090835008f4ef58fc39cdfa15df5051a4f5575d5e01fd7d355b9f67b0160e`.

- [ ] **Step 4: Árvore limpa**

Run: `git status --short`
Expected: saída vazia.
