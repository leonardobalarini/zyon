# Zyon — Plano 1: Núcleo da Simulação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir o `Zyon.Core` — biblioteca .NET pura que simula o mundo em tiles, o jogador (movimento, física, mineração, colocação), itens caídos e inventário, controlada por inputs/comandos e observada por eventos — totalmente testada via `dotnet test`.

**Architecture:** Tiles em arrays densos por chunk (`TileWorld`); entidades (jogador, itens caídos) no ECS Arch; uma `Simulation` com tick fixo de 60 Hz processa inputs e comandos, roda sistemas em ordem fixa e emite eventos; `LocalSession` implementa `IGameSession` para a camada de apresentação futura. Definições de conteúdo e constantes de física vêm de JSON em `data/`.

**Tech Stack:** .NET 8 (`net8.0`, compilado com o SDK 10 instalado), C# latest, Arch 2.1.0 (ECS), System.Text.Json, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`

**Sequência de planos da fatia vertical:**
1. **Núcleo da simulação** — este plano
2. Geração do mundo (passes, FastNoiseLite, determinismo, progresso)
3. Persistência (formato `.zworld`/`.zplayer`, paleta, migração, gravação atômica)
4. Client + Godot (autotiling, malhas de chunk, entidades, câmera, HUD, menus, smoke test)

---

## Convenções (valem para todas as tarefas)

- **Coordenadas:** unidades de tile; o tile `(x, y)` ocupa `[x, x+1) × [y, y+1)`; **Y cresce para baixo**. `Position` é o **centro** do AABB da entidade.
- **Namespaces:** `Zyon.Core.Data`, `Zyon.Core.Items`, `Zyon.Core.Tiles`, `Zyon.Core.Physics`, `Zyon.Core.Entities`, `Zyon.Core.Sim`, `Zyon.Core.Logging`. **Não** usar `Zyon.Core.World` (colide com `Arch.Core.World`) nem `Zyon.Core.Simulation` (colide com a classe `Simulation`).
- **ECS:** nunca criar/destruir entidades dentro de um `Query` — colete numa lista e aplique depois do `Query`.
- **Shell:** os comandos usam Bash (Git Bash) a partir da raiz `D:\Projetos\Zyon`.
- **Commits:** toda mensagem termina com a linha `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (passada como segundo `-m`).

## Mapa de arquivos

```
.gitignore
Directory.Build.props                 # net8.0, nullable, warnings como erro
Zyon.slnx
CLAUDE.md                             # regras do projeto para a IA
data/
  blocks.json  walls.json  items.json  physics.json  player.json
src/Zyon.Core/
  Zyon.Core.csproj
  Logging/ILog.cs                     # ILog + NullLog
  Data/Defs.cs                        # BlockDef, WallDef, ItemDef, ItemKind
  Data/DataException.cs
  Data/PhysicsConfig.cs               # constantes ajustáveis
  Data/Registry.cs                    # nomes <-> ids, defs, física, itens iniciais
  Data/RegistryLoader.cs              # JSON -> Registry com validação
  Items/ItemStack.cs
  Items/Inventory.cs                  # Inventory + IReadOnlyInventory
  Tiles/Chunk.cs
  Tiles/TileWorld.cs
  Physics/Aabb.cs                     # sobreposição de caixas
  Physics/TileCollider.cs             # consulta de solidez na grade
  Physics/TileMover.cs                # movimento eixo a eixo com subpassos e auto-step
  Entities/Components.cs              # componentes ECS
  Entities/EntityKind.cs
  Entities/EntityView.cs              # visão somente leitura para a apresentação
  Sim/PlayerInput.cs
  Sim/Commands.cs
  Sim/Events.cs
  Sim/IWorldView.cs
  Sim/ISystem.cs
  Sim/Queries.cs                      # QueryDescriptions compartilhadas
  Sim/Simulation.cs
  Sim/IGameSession.cs
  Sim/LocalSession.cs
  Sim/Systems/PlayerMovementSystem.cs
  Sim/Systems/PhysicsSystem.cs
  Sim/Systems/ToolActionSystem.cs     # minerar / colocar
  Sim/Systems/ItemPickupSystem.cs     # gravidade/ímã/coleta de itens
  Sim/Systems/ItemMergeSystem.cs
  Sim/Systems/ItemDespawnSystem.cs
tests/Zyon.Core.Tests/
  Zyon.Core.Tests.csproj
  TestData.cs  TestWorlds.cs  SimHarness.cs
  RegistryLoaderTests.cs  DataFilesTests.cs  TileWorldTests.cs  InventoryTests.cs
  TileMoverTests.cs  SimulationCommandTests.cs  PlayerMovementTests.cs
  ToolActionTests.cs  ItemSystemsTests.cs  LocalSessionTests.cs
  ScenarioTests.cs  PerformanceTests.cs
```

---

### Task 0: Estrutura da solução

**Files:**
- Create: `.gitignore`, `Directory.Build.props`, `Zyon.slnx`, `CLAUDE.md`
- Create: `src/Zyon.Core/Zyon.Core.csproj`
- Create: `tests/Zyon.Core.Tests/Zyon.Core.Tests.csproj`

- [ ] **Step 1: Criar `.gitignore`**

```gitignore
bin/
obj/
.vs/
.idea/
*.user
TestResults/
.godot/
```

- [ ] **Step 2: Criar `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Criar `src/Zyon.Core/Zyon.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Arch" Version="2.1.0" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Zyon.Core.Tests" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Criar `tests/Zyon.Core.Tests/Zyon.Core.Tests.csproj`**

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
    <ProjectReference Include="..\..\src\Zyon.Core\Zyon.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="..\..\data\**\*.json" LinkBase="data" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Criar a solução e adicionar os projetos**

```bash
dotnet new sln -n Zyon --format slnx
dotnet sln Zyon.slnx add src/Zyon.Core/Zyon.Core.csproj tests/Zyon.Core.Tests/Zyon.Core.Tests.csproj
```

Expected: duas linhas "Projeto ... adicionado à solução" (ou equivalente em inglês).

- [ ] **Step 6: Criar `CLAUDE.md`**

```markdown
# Zyon

Jogo 2D de mineração/construção/sobrevivência inspirado em Terraria, ambientação pós-guerra (em definição).
Motor: Godot 4 (.NET) com C#. A lógica do jogo vive em bibliotecas .NET puras.

- Spec da fatia vertical: `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`
- Planos: `docs/superpowers/plans/`

## Estrutura

- `src/Zyon.Core` — simulação (mundo, entidades ECS/Arch, física, inventário, sessão). O que um servidor dedicado rodaria.
- `tests/Zyon.Core.Tests` — xUnit.
- `data/` — definições JSON (blocos, paredes, itens, física, jogador). Conteúdo novo entra aqui, nunca hardcoded.

## Regras invioláveis

- `Zyon.Core` (e o futuro `Zyon.Client`) **nunca** referenciam Godot.
- Comandos (`ICommand`) e eventos (`IGameEvent`) são `record` imutáveis e serializáveis.
- Toda validação de regra de jogo acontece no Core; comando inválido é descartado em silêncio (contador + log debug).
- Coordenadas em unidades de tile, Y cresce para baixo, `Position` é o centro do AABB.
- Namespaces: `Zyon.Core.Tiles` (não `World` — colide com `Arch.Core.World`), `Zyon.Core.Sim` (não `Simulation`).
- Nunca criar/destruir entidades dentro de um `Query` do Arch: colete e aplique depois.
- Godot: usar somente API do Godot 4 (nunca Godot 3).

## Comandos

- Build: `dotnet build`
- Testes: `dotnet test`
- Desempenho (só vale em Release): `dotnet test -c Release --filter Category=Performance`

Toda mudança termina com `dotnet test` passando.
```

- [ ] **Step 7: Verificar o build**

Run: `dotnet build`
Expected: `Build succeeded` / `Compilação com êxito`, 0 erros.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: estrutura da solução Zyon.Core e testes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 1: Definições de dados e `Registry`

**Files:**
- Create: `src/Zyon.Core/Logging/ILog.cs`
- Create: `src/Zyon.Core/Data/Defs.cs`, `src/Zyon.Core/Data/DataException.cs`, `src/Zyon.Core/Data/PhysicsConfig.cs`, `src/Zyon.Core/Data/Registry.cs`, `src/Zyon.Core/Data/RegistryLoader.cs`
- Create: `src/Zyon.Core/Items/ItemStack.cs`
- Test: `tests/Zyon.Core.Tests/RegistryLoaderTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/RegistryLoaderTests.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class RegistryLoaderTests
{
    private const string Blocks = """[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt"}]""";
    private const string Walls = """[{"id":"dirt_wall"}]""";
    private const string Items = """
        [
          {"id":"dirt","name":"Terra","maxStack":999,"kind":"Block","placesBlock":"dirt"},
          {"id":"pick","name":"Picareta","maxStack":1,"kind":"Tool","power":1,"speed":1.5}
        ]
        """;
    private const string Physics = "{}";
    private const string Player = """{"starterItems":[{"item":"pick","count":1}]}""";

    private static Registry Load(string? blocks = null, string? walls = null, string? items = null,
        string? physics = null, string? player = null) =>
        RegistryLoader.Load(blocks ?? Blocks, walls ?? Walls, items ?? Items, physics ?? Physics, player ?? Player);

    [Fact]
    public void Load_AssignsIdsFromOne_AndReservesZero()
    {
        var r = Load();

        Assert.Equal(1, r.BlockId("dirt"));
        Assert.Equal("air", r.Block(0).Name);
        Assert.False(r.Block(0).Solid);
        Assert.Equal("none", r.Wall(0).Name);
        Assert.Equal(1, r.WallId("dirt_wall"));
        Assert.Equal(1, r.ItemId("dirt"));
        Assert.Equal(2, r.ItemId("pick"));
        Assert.Equal(ItemKind.None, r.Item(0).Kind);
    }

    [Fact]
    public void Load_ResolvesCrossReferences()
    {
        var r = Load();

        Assert.Equal(r.ItemId("dirt"), r.Block(r.BlockId("dirt")).DropItemId);
        var dirtItem = r.Item(r.ItemId("dirt"));
        Assert.Equal(ItemKind.Block, dirtItem.Kind);
        Assert.Equal(r.BlockId("dirt"), dirtItem.PlacesBlockId);
        Assert.Equal("Terra", dirtItem.DisplayName);
        var pick = r.Item(r.ItemId("pick"));
        Assert.Equal(ItemKind.Tool, pick.Kind);
        Assert.Equal(1.5f, pick.ToolSpeed);
    }

    [Fact]
    public void Load_ResolvesStarterItems()
    {
        var r = Load();

        var starter = Assert.Single(r.StarterItems);
        Assert.Equal(r.ItemId("pick"), starter.ItemId);
        Assert.Equal(1, starter.Count);
    }

    [Fact]
    public void Load_EmptyPhysics_UsesDefaults()
    {
        var r = Load();

        Assert.Equal(60f, r.Physics.Gravity);
        Assert.Equal(5f, r.Physics.ReachTiles);
    }

    [Fact]
    public void Load_PhysicsOverride_IsApplied()
    {
        var r = Load(physics: """{"gravity": 10}""");

        Assert.Equal(10f, r.Physics.Gravity);
    }

    [Fact]
    public void Load_DuplicateId_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks:
            """[{"id":"dirt","hardness":1,"solid":true},{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("blocks.json", ex.Message);
        Assert.Contains("duplicado", ex.Message);
    }

    [Fact]
    public void Load_UnknownReference_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(items:
            """[{"id":"x","name":"X","maxStack":1,"kind":"Block","placesBlock":"nope"}]""",
            player: """{"starterItems":[]}""",
            blocks: """[{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("items.json", ex.Message);
        Assert.Contains("placesBlock", ex.Message);
    }

    [Fact]
    public void Load_MissingField_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks: """[{"id":"dirt","solid":true}]"""));

        Assert.Contains("hardness", ex.Message);
    }

    [Fact]
    public void Load_ReservedName_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks: """[{"id":"air","hardness":1,"solid":true}]"""));

        Assert.Contains("reservado", ex.Message);
    }

    [Fact]
    public void Load_InvalidKind_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(items:
            """[{"id":"pick","name":"P","maxStack":1,"kind":"Weapon"}]""",
            blocks: """[{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("kind", ex.Message);
    }

    [Fact]
    public void Load_UnknownPhysicsField_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(physics: """{"gravvity": 10}"""));

        Assert.Contains("physics.json", ex.Message);
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(walls: "[{"));

        Assert.Contains("walls.json", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zyon-empty-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<DataException>(() => RegistryLoader.LoadFromDirectory(dir));
            Assert.Contains("blocks.json", ex.Message);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void BlockId_Unknown_Throws()
    {
        var r = Load();

        Assert.Throws<KeyNotFoundException>(() => r.BlockId("nope"));
        Assert.False(r.TryGetBlockId("nope", out _));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~RegistryLoaderTests"`
Expected: FAIL na compilação — `The type or namespace name 'Data' does not exist in the namespace 'Zyon.Core'`.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Logging/ILog.cs`:

```csharp
namespace Zyon.Core.Logging;

public interface ILog
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    private NullLog() { }

    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
```

`src/Zyon.Core/Items/ItemStack.cs`:

```csharp
namespace Zyon.Core.Items;

public readonly record struct ItemStack(ushort ItemId, int Count)
{
    public static ItemStack Empty => default;

    public bool IsEmpty => ItemId == 0 || Count <= 0;
}
```

`src/Zyon.Core/Data/Defs.cs`:

```csharp
namespace Zyon.Core.Data;

public sealed record BlockDef(ushort Id, string Name, float Hardness, bool Solid, ushort DropItemId);

public sealed record WallDef(ushort Id, string Name);

public enum ItemKind { None, Block, Tool }

public sealed record ItemDef(
    ushort Id,
    string Name,
    string DisplayName,
    int MaxStack,
    ItemKind Kind,
    ushort PlacesBlockId,
    float ToolPower,
    float ToolSpeed);
```

`src/Zyon.Core/Data/DataException.cs`:

```csharp
namespace Zyon.Core.Data;

public sealed class DataException(string message) : Exception(message);
```

`src/Zyon.Core/Data/PhysicsConfig.cs`:

```csharp
namespace Zyon.Core.Data;

/// <summary>Constantes ajustáveis de física e gameplay. Unidades: tiles, segundos, ticks.</summary>
public sealed class PhysicsConfig
{
    public float Gravity { get; init; } = 60f;
    public float TerminalVelocity { get; init; } = 30f;
    public float MaxMoveSpeed { get; init; } = 9f;
    public float MoveAcceleration { get; init; } = 60f;
    public float MoveDeceleration { get; init; } = 80f;
    public float JumpVelocity { get; init; } = 22f;
    public float JumpCutFactor { get; init; } = 0.45f;
    public int CoyoteTicks { get; init; } = 6;
    public float PlayerWidth { get; init; } = 1.25f;
    public float PlayerHeight { get; init; } = 2.75f;
    public float ReachTiles { get; init; } = 5f;
    public int PlaceCooldownTicks { get; init; } = 8;
    public float MiningRate { get; init; } = 2f;
    public float ItemSize { get; init; } = 0.75f;
    public float ItemGroundFriction { get; init; } = 0.8f;
    public float MagnetRadius { get; init; } = 3f;
    public float MagnetAcceleration { get; init; } = 80f;
    public float MagnetMaxSpeed { get; init; } = 20f;
    public float ItemMergeRadius { get; init; } = 1f;
    public int ItemMergeIntervalTicks { get; init; } = 30;
    public float ItemDespawnSeconds { get; init; } = 300f;
    public int DropPickupDelayTicks { get; init; } = 90;
    public float DropImpulseX { get; init; } = 6f;
    public float DropImpulseY { get; init; } = 6f;
}
```

`src/Zyon.Core/Data/Registry.cs`:

```csharp
using Zyon.Core.Items;

namespace Zyon.Core.Data;

/// <summary>Definições de conteúdo carregadas dos JSONs. O id 0 é sempre "air"/"none".</summary>
public sealed class Registry
{
    private readonly BlockDef[] blocks;
    private readonly WallDef[] walls;
    private readonly ItemDef[] items;
    private readonly Dictionary<string, ushort> blockIds;
    private readonly Dictionary<string, ushort> wallIds;
    private readonly Dictionary<string, ushort> itemIds;

    internal Registry(BlockDef[] blocks, WallDef[] walls, ItemDef[] items,
        PhysicsConfig physics, IReadOnlyList<ItemStack> starterItems)
    {
        this.blocks = blocks;
        this.walls = walls;
        this.items = items;
        blockIds = blocks.ToDictionary(b => b.Name, b => b.Id);
        wallIds = walls.ToDictionary(w => w.Name, w => w.Id);
        itemIds = items.ToDictionary(i => i.Name, i => i.Id);
        Physics = physics;
        StarterItems = starterItems;
    }

    public PhysicsConfig Physics { get; }
    public IReadOnlyList<ItemStack> StarterItems { get; }
    public IReadOnlyList<BlockDef> Blocks => blocks;
    public IReadOnlyList<WallDef> Walls => walls;
    public IReadOnlyList<ItemDef> Items => items;

    public BlockDef Block(ushort id) => blocks[id];
    public WallDef Wall(ushort id) => walls[id];
    public ItemDef Item(ushort id) => items[id];

    public ushort BlockId(string name) => Lookup(blockIds, name, "Bloco");
    public ushort WallId(string name) => Lookup(wallIds, name, "Parede");
    public ushort ItemId(string name) => Lookup(itemIds, name, "Item");

    public bool TryGetBlockId(string name, out ushort id) => blockIds.TryGetValue(name, out id);
    public bool TryGetWallId(string name, out ushort id) => wallIds.TryGetValue(name, out id);
    public bool TryGetItemId(string name, out ushort id) => itemIds.TryGetValue(name, out id);

    private static ushort Lookup(Dictionary<string, ushort> map, string name, string kind) =>
        map.TryGetValue(name, out var id) ? id : throw new KeyNotFoundException($"{kind} desconhecido: '{name}'");
}
```

`src/Zyon.Core/Data/RegistryLoader.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Items;

namespace Zyon.Core.Data;

public static class RegistryLoader
{
    public const string BlocksFile = "blocks.json";
    public const string WallsFile = "walls.json";
    public const string ItemsFile = "items.json";
    public const string PhysicsFile = "physics.json";
    public const string PlayerFile = "player.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Registry LoadFromDirectory(string dataDir)
    {
        string Read(string file)
        {
            var path = Path.Combine(dataDir, file);
            if (!File.Exists(path))
                throw new DataException($"{file}: arquivo não encontrado em '{dataDir}'");
            return File.ReadAllText(path);
        }

        return Load(Read(BlocksFile), Read(WallsFile), Read(ItemsFile), Read(PhysicsFile), Read(PlayerFile));
    }

    public static Registry Load(string blocksJson, string wallsJson, string itemsJson,
        string physicsJson, string playerJson)
    {
        var blockDtos = Parse<List<BlockDto>>(blocksJson, BlocksFile);
        var wallDtos = Parse<List<WallDto>>(wallsJson, WallsFile);
        var itemDtos = Parse<List<ItemDto>>(itemsJson, ItemsFile);
        var physics = Parse<PhysicsConfig>(physicsJson, PhysicsFile);
        var player = Parse<PlayerDto>(playerJson, PlayerFile);

        var blockIds = AssignIds(blockDtos.Select(b => b.Id).ToList(), BlocksFile, "air");
        var wallIds = AssignIds(wallDtos.Select(w => w.Id).ToList(), WallsFile, "none");
        var itemIds = AssignIds(itemDtos.Select(i => i.Id).ToList(), ItemsFile, "none");

        var blocks = new BlockDef[blockDtos.Count + 1];
        blocks[0] = new BlockDef(0, "air", 0f, false, 0);
        for (int i = 0; i < blockDtos.Count; i++)
        {
            var d = blockDtos[i];
            var ctx = $"{BlocksFile}[{i}] ('{d.Id}')";
            float hardness = d.Hardness ?? throw Missing(ctx, "hardness");
            if (hardness < 0) throw new DataException($"{ctx}: campo 'hardness' não pode ser negativo");
            bool solid = d.Solid ?? throw Missing(ctx, "solid");
            ushort drop = d.Drops is null ? (ushort)0 : Resolve(itemIds, d.Drops, ctx, "drops");
            blocks[i + 1] = new BlockDef((ushort)(i + 1), d.Id!, hardness, solid, drop);
        }

        var walls = new WallDef[wallDtos.Count + 1];
        walls[0] = new WallDef(0, "none");
        for (int i = 0; i < wallDtos.Count; i++)
            walls[i + 1] = new WallDef((ushort)(i + 1), wallDtos[i].Id!);

        var items = new ItemDef[itemDtos.Count + 1];
        items[0] = new ItemDef(0, "none", "", 0, ItemKind.None, 0, 0f, 0f);
        for (int i = 0; i < itemDtos.Count; i++)
        {
            var d = itemDtos[i];
            var ctx = $"{ItemsFile}[{i}] ('{d.Id}')";
            string display = d.Name ?? throw Missing(ctx, "name");
            int maxStack = d.MaxStack ?? throw Missing(ctx, "maxStack");
            if (maxStack < 1) throw new DataException($"{ctx}: campo 'maxStack' deve ser >= 1");
            var kind = d.Kind switch
            {
                "Block" => ItemKind.Block,
                "Tool" => ItemKind.Tool,
                null => throw Missing(ctx, "kind"),
                _ => throw new DataException($"{ctx}: campo 'kind' inválido '{d.Kind}' (use Block ou Tool)"),
            };
            ushort places = 0;
            float power = 0f, speed = 0f;
            if (kind == ItemKind.Block)
            {
                places = Resolve(blockIds, d.PlacesBlock ?? throw Missing(ctx, "placesBlock"), ctx, "placesBlock");
            }
            else
            {
                power = d.Power ?? throw Missing(ctx, "power");
                speed = d.Speed ?? throw Missing(ctx, "speed");
                if (power <= 0) throw new DataException($"{ctx}: campo 'power' deve ser > 0");
                if (speed <= 0) throw new DataException($"{ctx}: campo 'speed' deve ser > 0");
            }
            items[i + 1] = new ItemDef((ushort)(i + 1), d.Id!, display, maxStack, kind, places, power, speed);
        }

        var starter = new List<ItemStack>();
        var starterDtos = player.StarterItems ?? [];
        for (int i = 0; i < starterDtos.Count; i++)
        {
            var s = starterDtos[i];
            var ctx = $"{PlayerFile} starterItems[{i}]";
            ushort id = Resolve(itemIds, s.Item ?? throw Missing(ctx, "item"), ctx, "item");
            int count = s.Count ?? throw Missing(ctx, "count");
            if (count < 1 || count > items[id].MaxStack)
                throw new DataException($"{ctx}: campo 'count' deve estar entre 1 e {items[id].MaxStack}");
            starter.Add(new ItemStack(id, count));
        }

        return new Registry(blocks, walls, items, physics, starter);
    }

    private static T Parse<T>(string json, string file) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new DataException($"{file}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{file}: JSON inválido — {ex.Message}");
        }
    }

    private static Dictionary<string, ushort> AssignIds(List<string?> names, string file, string reserved)
    {
        if (names.Count >= ushort.MaxValue)
            throw new DataException($"{file}: excesso de entradas ({names.Count})");
        var ids = new Dictionary<string, ushort>();
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                throw new DataException($"{file}[{i}]: campo 'id' ausente");
            if (name == reserved)
                throw new DataException($"{file}[{i}]: id '{name}' é reservado");
            if (!ids.TryAdd(name, (ushort)(i + 1)))
                throw new DataException($"{file}[{i}]: id duplicado '{name}'");
        }
        return ids;
    }

    private static ushort Resolve(Dictionary<string, ushort> ids, string name, string ctx, string field) =>
        ids.TryGetValue(name, out var id)
            ? id
            : throw new DataException($"{ctx}: campo '{field}' referencia id inexistente '{name}'");

    private static DataException Missing(string ctx, string field) =>
        new($"{ctx}: campo obrigatório '{field}' ausente");

    private sealed class BlockDto
    {
        public string? Id { get; set; }
        public float? Hardness { get; set; }
        public bool? Solid { get; set; }
        public string? Drops { get; set; }
    }

    private sealed class WallDto
    {
        public string? Id { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? MaxStack { get; set; }
        public string? Kind { get; set; }
        public string? PlacesBlock { get; set; }
        public float? Power { get; set; }
        public float? Speed { get; set; }
    }

    private sealed class PlayerDto
    {
        public List<StarterItemDto>? StarterItems { get; set; }
    }

    private sealed class StarterItemDto
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~RegistryLoaderTests"`
Expected: PASS, 14 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): registry de dados carregado de JSON com validação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Arquivos de dados reais

**Files:**
- Create: `data/blocks.json`, `data/walls.json`, `data/items.json`, `data/physics.json`, `data/player.json`
- Create: `tests/Zyon.Core.Tests/TestData.cs`
- Test: `tests/Zyon.Core.Tests/DataFilesTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

`tests/Zyon.Core.Tests/TestData.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

internal static class TestData
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Registry Registry() => RegistryLoader.LoadFromDirectory(DataDir);

    /// <summary>Registry real, mas com physics.json substituído (campos ausentes usam os padrões).</summary>
    public static Registry RegistryWithPhysics(string physicsJson)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        return RegistryLoader.Load(Read(RegistryLoader.BlocksFile), Read(RegistryLoader.WallsFile),
            Read(RegistryLoader.ItemsFile), physicsJson, Read(RegistryLoader.PlayerFile));
    }
}
```

`tests/Zyon.Core.Tests/DataFilesTests.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class DataFilesTests
{
    [Fact]
    public void RealDataFiles_LoadWithoutErrors()
    {
        var r = TestData.Registry();

        foreach (var name in new[] { "dirt", "stone", "deep_stone", "ore_a", "ore_b" })
            Assert.True(r.TryGetBlockId(name, out _), $"bloco '{name}' ausente");
        Assert.True(r.TryGetWallId("dirt_wall", out _));
        Assert.True(r.TryGetWallId("stone_wall", out _));
        Assert.Equal(ItemKind.Tool, r.Item(r.ItemId("basic_pickaxe")).Kind);
    }

    [Fact]
    public void EveryBlockDropsAnItemThatPlacesIt()
    {
        var r = TestData.Registry();

        foreach (var block in r.Blocks.Skip(1))
        {
            Assert.NotEqual(0, block.DropItemId);
            Assert.Equal(block.Id, r.Item(block.DropItemId).PlacesBlockId);
        }
    }

    [Fact]
    public void PlayerStartsWithPickaxe()
    {
        var r = TestData.Registry();

        Assert.Contains(r.StarterItems, s => s.ItemId == r.ItemId("basic_pickaxe") && s.Count == 1);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~DataFilesTests"`
Expected: FAIL — `DataException: blocks.json: arquivo não encontrado`.

- [ ] **Step 3: Criar os arquivos de dados**

`data/blocks.json`:

```json
[
  { "id": "dirt",       "hardness": 1.0, "solid": true, "drops": "dirt" },
  { "id": "stone",      "hardness": 2.0, "solid": true, "drops": "stone" },
  { "id": "deep_stone", "hardness": 3.5, "solid": true, "drops": "deep_stone" },
  { "id": "ore_a",      "hardness": 2.5, "solid": true, "drops": "ore_a" },
  { "id": "ore_b",      "hardness": 4.0, "solid": true, "drops": "ore_b" }
]
```

`data/walls.json`:

```json
[
  { "id": "dirt_wall" },
  { "id": "stone_wall" }
]
```

`data/items.json`:

```json
[
  { "id": "dirt",          "name": "Terra",           "maxStack": 999, "kind": "Block", "placesBlock": "dirt" },
  { "id": "stone",         "name": "Rocha",           "maxStack": 999, "kind": "Block", "placesBlock": "stone" },
  { "id": "deep_stone",    "name": "Rocha profunda",  "maxStack": 999, "kind": "Block", "placesBlock": "deep_stone" },
  { "id": "ore_a",         "name": "Minério A",       "maxStack": 999, "kind": "Block", "placesBlock": "ore_a" },
  { "id": "ore_b",         "name": "Minério B",       "maxStack": 999, "kind": "Block", "placesBlock": "ore_b" },
  { "id": "basic_pickaxe", "name": "Picareta básica", "maxStack": 1,   "kind": "Tool",  "power": 1.0, "speed": 1.0 }
]
```

`data/physics.json` (mesmos valores dos padrões de `PhysicsConfig`):

```json
{
  "gravity": 60,
  "terminalVelocity": 30,
  "maxMoveSpeed": 9,
  "moveAcceleration": 60,
  "moveDeceleration": 80,
  "jumpVelocity": 22,
  "jumpCutFactor": 0.45,
  "coyoteTicks": 6,
  "playerWidth": 1.25,
  "playerHeight": 2.75,
  "reachTiles": 5,
  "placeCooldownTicks": 8,
  "miningRate": 2,
  "itemSize": 0.75,
  "itemGroundFriction": 0.8,
  "magnetRadius": 3,
  "magnetAcceleration": 80,
  "magnetMaxSpeed": 20,
  "itemMergeRadius": 1,
  "itemMergeIntervalTicks": 30,
  "itemDespawnSeconds": 300,
  "dropPickupDelayTicks": 90,
  "dropImpulseX": 6,
  "dropImpulseY": 6
}
```

`data/player.json`:

```json
{
  "starterItems": [
    { "item": "basic_pickaxe", "count": 1 }
  ]
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~DataFilesTests"`
Expected: PASS, 3 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(data): definições provisórias de blocos, paredes, itens e física" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `Chunk` e `TileWorld`

**Files:**
- Create: `src/Zyon.Core/Tiles/Chunk.cs`, `src/Zyon.Core/Tiles/TileWorld.cs`
- Create: `tests/Zyon.Core.Tests/TestWorlds.cs`
- Test: `tests/Zyon.Core.Tests/TileWorldTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/TestWorlds.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

internal static class TestWorlds
{
    /// <summary>Mundo com o bloco <paramref name="block"/> preenchendo todas as linhas y >= groundY.</summary>
    public static TileWorld Flat(Registry registry, int width, int height, int groundY, string block = "dirt")
    {
        var world = new TileWorld(width, height);
        ushort id = registry.BlockId(block);
        for (int y = groundY; y < height; y++)
            for (int x = 0; x < width; x++)
                world.SetBlock(x, y, id);
        return world;
    }
}
```

`tests/Zyon.Core.Tests/TileWorldTests.cs`:

```csharp
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class TileWorldTests
{
    [Fact]
    public void NewWorld_IsAllAir()
    {
        var w = new TileWorld(64, 64);

        Assert.Equal(0, w.GetBlock(10, 10));
        Assert.Equal(0, w.GetWall(10, 10));
    }

    [Fact]
    public void SetAndGet_RoundTrip_AcrossChunkBoundary()
    {
        var w = new TileWorld(100, 50);

        w.SetBlock(31, 5, 1);
        w.SetBlock(32, 5, 2);
        w.SetWall(32, 33, 3);

        Assert.Equal(1, w.GetBlock(31, 5));
        Assert.Equal(2, w.GetBlock(32, 5));
        Assert.Equal(3, w.GetWall(32, 33));
        Assert.Equal(0, w.GetWall(31, 5));
    }

    [Fact]
    public void ChunkCount_RoundsUpPartialChunks()
    {
        var w = new TileWorld(100, 40);

        Assert.Equal(4, w.ChunksX);
        Assert.Equal(2, w.ChunksY);
        Assert.Equal(8, w.Chunks.Count());
    }

    [Fact]
    public void SetBlock_MarksOwningChunkDirty()
    {
        var w = new TileWorld(64, 64);

        w.SetBlock(40, 3, 1);

        Assert.True(w.GetChunk(1, 0).Dirty);
        Assert.False(w.GetChunk(0, 0).Dirty);
    }

    [Fact]
    public void OutOfBounds_GetReturnsAir_SetThrows()
    {
        var w = new TileWorld(64, 64);

        Assert.False(w.InBounds(-1, 0));
        Assert.False(w.InBounds(64, 0));
        Assert.Equal(0, w.GetBlock(-1, 0));
        Assert.Equal(0, w.GetBlock(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetBlock(64, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetWall(0, -1, 1));
    }

    [Fact]
    public void InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileWorld(0, 10));
    }

    [Fact]
    public void FlatHelper_FillsFromGroundDown()
    {
        var r = TestData.Registry();
        var w = TestWorlds.Flat(r, 64, 64, 40);

        Assert.Equal(0, w.GetBlock(5, 39));
        Assert.Equal(r.BlockId("dirt"), w.GetBlock(5, 40));
        Assert.Equal(r.BlockId("dirt"), w.GetBlock(63, 63));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~TileWorldTests"`
Expected: FAIL na compilação — `The type or namespace name 'Tiles' does not exist`.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Tiles/Chunk.cs`:

```csharp
namespace Zyon.Core.Tiles;

/// <summary>Bloco 32×32 de tiles em arrays paralelos (structure of arrays).</summary>
public sealed class Chunk(int chunkX, int chunkY)
{
    public const int Size = 32;
    public const int Shift = 5;
    public const int Mask = Size - 1;
    public const int Area = Size * Size;

    public int ChunkX { get; } = chunkX;
    public int ChunkY { get; } = chunkY;
    public ushort[] Blocks { get; } = new ushort[Area];
    public ushort[] Walls { get; } = new ushort[Area];

    /// <summary>Alterado desde a última vez que alguém limpou a flag (render, save, rede).</summary>
    public bool Dirty { get; set; }

    public static int Index(int localX, int localY) => (localY << Shift) + localX;
}
```

`src/Zyon.Core/Tiles/TileWorld.cs`:

```csharp
namespace Zyon.Core.Tiles;

public sealed class TileWorld
{
    private readonly Chunk[] chunks;

    public TileWorld(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        ChunksX = (width + Chunk.Size - 1) / Chunk.Size;
        ChunksY = (height + Chunk.Size - 1) / Chunk.Size;
        chunks = new Chunk[ChunksX * ChunksY];
        for (int cy = 0; cy < ChunksY; cy++)
            for (int cx = 0; cx < ChunksX; cx++)
                chunks[cy * ChunksX + cx] = new Chunk(cx, cy);
    }

    public int Width { get; }
    public int Height { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public IEnumerable<Chunk> Chunks => chunks;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Chunk GetChunk(int chunkX, int chunkY) => chunks[chunkY * ChunksX + chunkX];

    public ushort GetBlock(int x, int y) =>
        InBounds(x, y) ? ChunkAt(x, y).Blocks[LocalIndex(x, y)] : (ushort)0;

    public ushort GetWall(int x, int y) =>
        InBounds(x, y) ? ChunkAt(x, y).Walls[LocalIndex(x, y)] : (ushort)0;

    public void SetBlock(int x, int y, ushort id)
    {
        EnsureInBounds(x, y);
        var chunk = ChunkAt(x, y);
        chunk.Blocks[LocalIndex(x, y)] = id;
        chunk.Dirty = true;
    }

    public void SetWall(int x, int y, ushort id)
    {
        EnsureInBounds(x, y);
        var chunk = ChunkAt(x, y);
        chunk.Walls[LocalIndex(x, y)] = id;
        chunk.Dirty = true;
    }

    private Chunk ChunkAt(int x, int y) => chunks[(y >> Chunk.Shift) * ChunksX + (x >> Chunk.Shift)];

    private static int LocalIndex(int x, int y) => Chunk.Index(x & Chunk.Mask, y & Chunk.Mask);

    private void EnsureInBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) fora do mundo {Width}×{Height}");
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~TileWorldTests"`
Expected: PASS, 7 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): TileWorld com chunks 32x32 em arrays densos" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `Inventory`

**Files:**
- Create: `src/Zyon.Core/Items/Inventory.cs`
- Test: `tests/Zyon.Core.Tests/InventoryTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/InventoryTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Items;

namespace Zyon.Core.Tests;

public class InventoryTests
{
    private readonly Registry r = TestData.Registry();
    private ushort Dirt => r.ItemId("dirt");
    private ushort Stone => r.ItemId("stone");
    private ushort Pick => r.ItemId("basic_pickaxe");

    [Fact]
    public void Add_ToEmpty_FillsHotbarFirst()
    {
        var inv = new Inventory(r);
        var changed = new List<int>();

        int left = inv.Add(Dirt, 5, changed);

        Assert.Equal(0, left);
        Assert.Equal(new ItemStack(Dirt, 5), inv[0]);
        Assert.Equal(new[] { 0 }, changed);
    }

    [Fact]
    public void Add_CompletesExistingStackBeforeEmptySlot()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Stone, 1));
        inv.Set(3, new ItemStack(Dirt, 10));

        inv.Add(Dirt, 5);

        Assert.Equal(new ItemStack(Dirt, 15), inv[3]);
        Assert.True(inv[1].IsEmpty);
    }

    [Fact]
    public void Add_OverflowsAcrossStacks()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 998));

        int left = inv.Add(Dirt, 5);

        Assert.Equal(0, left);
        Assert.Equal(999, inv[0].Count);
        Assert.Equal(new ItemStack(Dirt, 4), inv[1]);
    }

    [Fact]
    public void Add_WhenFull_ReturnsLeftover()
    {
        var inv = new Inventory(r);
        for (int i = 0; i < Inventory.Slots; i++)
            inv.Set(i, new ItemStack(Pick, 1));

        Assert.False(inv.CanAccept(Dirt));
        Assert.Equal(7, inv.Add(Dirt, 7));
    }

    [Fact]
    public void Add_RespectsMaxStackOfOne()
    {
        var inv = new Inventory(r);

        inv.Add(Pick, 2);

        Assert.Equal(new ItemStack(Pick, 1), inv[0]);
        Assert.Equal(new ItemStack(Pick, 1), inv[1]);
    }

    [Fact]
    public void Move_ToEmpty_MovesStack()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 3));
        var changed = new List<int>();

        Assert.True(inv.Move(0, 12, changed));

        Assert.True(inv[0].IsEmpty);
        Assert.Equal(new ItemStack(Dirt, 3), inv[12]);
        Assert.Equal(new[] { 0, 12 }, changed);
    }

    [Fact]
    public void Move_DifferentItems_Swaps()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 3));
        inv.Set(1, new ItemStack(Stone, 4));

        Assert.True(inv.Move(0, 1));

        Assert.Equal(new ItemStack(Stone, 4), inv[0]);
        Assert.Equal(new ItemStack(Dirt, 3), inv[1]);
    }

    [Fact]
    public void Move_SameItem_MergesAndKeepsExcessInSource()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 10));
        inv.Set(1, new ItemStack(Dirt, 995));

        Assert.True(inv.Move(0, 1));

        Assert.Equal(new ItemStack(Dirt, 6), inv[0]);
        Assert.Equal(new ItemStack(Dirt, 999), inv[1]);
    }

    [Fact]
    public void Move_Invalid_ReturnsFalse()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 1));
        inv.Set(1, new ItemStack(Dirt, 999));

        Assert.False(inv.Move(0, 0));
        Assert.False(inv.Move(0, 50));
        Assert.False(inv.Move(-1, 2));
        Assert.False(inv.Move(5, 6));     // origem vazia
        Assert.False(inv.Move(0, 1));     // destino cheio do mesmo item
    }

    [Fact]
    public void TakeAll_And_ConsumeOne()
    {
        var inv = new Inventory(r);
        inv.Set(2, new ItemStack(Dirt, 2));

        Assert.True(inv.ConsumeOne(2));
        Assert.Equal(1, inv[2].Count);
        Assert.True(inv.ConsumeOne(2));
        Assert.True(inv[2].IsEmpty);
        Assert.False(inv.ConsumeOne(2));

        inv.Set(4, new ItemStack(Stone, 9));
        Assert.Equal(new ItemStack(Stone, 9), inv.TakeAll(4));
        Assert.True(inv[4].IsEmpty);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~InventoryTests"`
Expected: FAIL na compilação — `The type or namespace name 'Inventory' could not be found`.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Items/Inventory.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Items;

public interface IReadOnlyInventory
{
    int SlotCount { get; }
    ItemStack this[int slot] { get; }
}

/// <summary>50 slots: 0–9 hotbar, 10–49 mochila.</summary>
public sealed class Inventory(Registry registry) : IReadOnlyInventory
{
    public const int Slots = 50;
    public const int HotbarSize = 10;

    private readonly ItemStack[] slots = new ItemStack[Slots];

    public int SlotCount => Slots;

    public ItemStack this[int slot] => slots[slot];

    public static bool IsValidSlot(int slot) => (uint)slot < Slots;

    public void Set(int slot, ItemStack stack) => slots[slot] = stack.IsEmpty ? ItemStack.Empty : stack;

    public bool CanAccept(ushort itemId)
    {
        int max = registry.Item(itemId).MaxStack;
        foreach (var s in slots)
            if (s.IsEmpty || (s.ItemId == itemId && s.Count < max))
                return true;
        return false;
    }

    /// <summary>Adiciona completando pilhas existentes e depois slots vazios (hotbar primeiro). Retorna a sobra.</summary>
    public int Add(ushort itemId, int count, ICollection<int>? changed = null)
    {
        if (itemId == 0 || count <= 0) return Math.Max(count, 0);
        int max = registry.Item(itemId).MaxStack;
        int remaining = count;

        for (int i = 0; i < Slots && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.IsEmpty || s.ItemId != itemId || s.Count >= max) continue;
            int n = Math.Min(max - s.Count, remaining);
            slots[i] = s with { Count = s.Count + n };
            remaining -= n;
            changed?.Add(i);
        }

        for (int i = 0; i < Slots && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int n = Math.Min(max, remaining);
            slots[i] = new ItemStack(itemId, n);
            remaining -= n;
            changed?.Add(i);
        }

        return remaining;
    }

    /// <summary>Mesmo item: funde até maxStack (excedente fica na origem). Caso contrário: troca.</summary>
    public bool Move(int from, int to, ICollection<int>? changed = null)
    {
        if (!IsValidSlot(from) || !IsValidSlot(to) || from == to) return false;
        var a = slots[from];
        if (a.IsEmpty) return false;
        var b = slots[to];

        if (!b.IsEmpty && b.ItemId == a.ItemId)
        {
            int max = registry.Item(a.ItemId).MaxStack;
            int n = Math.Min(max - b.Count, a.Count);
            if (n <= 0) return false;
            slots[to] = b with { Count = b.Count + n };
            slots[from] = a.Count - n == 0 ? ItemStack.Empty : a with { Count = a.Count - n };
        }
        else
        {
            slots[from] = b;
            slots[to] = a;
        }

        changed?.Add(from);
        changed?.Add(to);
        return true;
    }

    public ItemStack TakeAll(int slot)
    {
        var s = slots[slot];
        slots[slot] = ItemStack.Empty;
        return s;
    }

    public bool ConsumeOne(int slot)
    {
        var s = slots[slot];
        if (s.IsEmpty) return false;
        slots[slot] = s.Count == 1 ? ItemStack.Empty : s with { Count = s.Count - 1 };
        return true;
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~InventoryTests"`
Expected: PASS, 10 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): inventário de 50 slots com fusão, troca e sobra" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Física em grade (`Aabb`, `TileCollider`, `TileMover`)

**Files:**
- Create: `src/Zyon.Core/Physics/Aabb.cs`, `src/Zyon.Core/Physics/TileCollider.cs`, `src/Zyon.Core/Physics/TileMover.cs`
- Test: `tests/Zyon.Core.Tests/TileMoverTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/TileMoverTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Physics;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class TileMoverTests
{
    private readonly Registry r = TestData.Registry();
    private static readonly Vector2 Box = new(1f, 1f);
    private static readonly Vector2 PlayerSize = new(1.25f, 2.75f);

    private (TileWorld World, TileCollider Collider) Ground(int groundY = 10)
    {
        var w = TestWorlds.Flat(r, 40, 20, groundY);
        return (w, new TileCollider(w, r));
    }

    [Fact]
    public void Falling_LandsOnGround()
    {
        var (_, c) = Ground();

        var res = TileMover.Move(c, new Vector2(5f, 5f), Box, new Vector2(0f, 10f), false);

        Assert.Equal(9.5f, res.Position.Y, 4);
        Assert.True(res.HitY);
        Assert.True(res.Landed);
    }

    [Fact]
    public void HugeDelta_DoesNotTunnelThroughThinFloor()
    {
        var w = new TileWorld(40, 40);
        for (int x = 0; x < 40; x++) w.SetBlock(x, 10, r.BlockId("dirt"));
        var c = new TileCollider(w, r);

        var res = TileMover.Move(c, new Vector2(5f, 5f), Box, new Vector2(0f, 100f), false);

        Assert.Equal(9.5f, res.Position.Y, 4);
    }

    [Fact]
    public void Wall_BlocksHorizontalMovement()
    {
        var (w, c) = Ground();
        for (int y = 0; y < 10; y++) w.SetBlock(10, y, r.BlockId("dirt"));

        var res = TileMover.Move(c, new Vector2(8f, 9.5f), Box, new Vector2(5f, 0f), false);

        Assert.Equal(9.5f, res.Position.X, 4);
        Assert.True(res.HitX);
    }

    [Fact]
    public void WorldEdge_IsSolid()
    {
        var (_, c) = Ground();

        var res = TileMover.Move(c, new Vector2(3f, 9.5f), Box, new Vector2(-10f, 0f), false);

        Assert.Equal(0.5f, res.Position.X, 4);
        Assert.True(res.HitX);
    }

    [Fact]
    public void StepUp_ClimbsOneTileLedge()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: true);

        Assert.True(res.SteppedUp);
        Assert.False(res.HitX);
        Assert.Equal(start.Y - 1f, res.Position.Y, 4);
    }

    [Fact]
    public void StepUp_DoesNotClimbTwoTiles()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        w.SetBlock(10, 8, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: true);

        Assert.True(res.HitX);
        Assert.False(res.SteppedUp);
        Assert.Equal(10f - PlayerSize.X / 2, res.Position.X, 4);
        Assert.Equal(start.Y, res.Position.Y, 4);
    }

    [Fact]
    public void StepUp_NotAllowed_Blocks()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: false);

        Assert.True(res.HitX);
        Assert.False(res.SteppedUp);
    }

    [Fact]
    public void IsGrounded_TrueOnlyWhenTouchingFloor()
    {
        var (_, c) = Ground();

        Assert.True(c.IsGrounded(new Vector2(5f, 9.5f), Box));
        Assert.False(c.IsGrounded(new Vector2(5f, 9f), Box));
    }

    [Fact]
    public void Aabb_Overlaps_ExcludesTouchingEdges()
    {
        Assert.True(Aabb.Overlaps(new Vector2(0, 0), Box, new Vector2(0.9f, 0), Box));
        Assert.False(Aabb.Overlaps(new Vector2(0, 0), Box, new Vector2(1f, 0), Box));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~TileMoverTests"`
Expected: FAIL na compilação — `The type or namespace name 'Physics' does not exist`.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Physics/Aabb.cs`:

```csharp
using System.Numerics;

namespace Zyon.Core.Physics;

public static class Aabb
{
    public const float Epsilon = 1e-4f;

    /// <summary>Caixas definidas por centro e tamanho. Bordas apenas encostadas não contam.</summary>
    public static bool Overlaps(Vector2 centerA, Vector2 sizeA, Vector2 centerB, Vector2 sizeB) =>
        MathF.Abs(centerA.X - centerB.X) < (sizeA.X + sizeB.X) / 2 - Epsilon &&
        MathF.Abs(centerA.Y - centerB.Y) < (sizeA.Y + sizeB.Y) / 2 - Epsilon;
}
```

`src/Zyon.Core/Physics/TileCollider.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Physics;

/// <summary>Consultas de solidez na grade. Fora do mundo é sólido.</summary>
public sealed class TileCollider(TileWorld world, Registry registry)
{
    private const float GroundProbe = 0.05f;

    public bool IsSolid(int x, int y) => !world.InBounds(x, y) || registry.Block(world.GetBlock(x, y)).Solid;

    /// <summary>Há tile sólido sobrepondo a área aberta (minX, maxX) × (minY, maxY)?</summary>
    public bool AreaHasSolid(float minX, float minY, float maxX, float maxY)
    {
        int x0 = (int)MathF.Floor(minX + Aabb.Epsilon);
        int x1 = (int)MathF.Floor(maxX - Aabb.Epsilon);
        int y0 = (int)MathF.Floor(minY + Aabb.Epsilon);
        int y1 = (int)MathF.Floor(maxY - Aabb.Epsilon);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (IsSolid(x, y))
                    return true;
        return false;
    }

    public bool BoxHasSolid(Vector2 center, Vector2 size) =>
        AreaHasSolid(center.X - size.X / 2, center.Y - size.Y / 2, center.X + size.X / 2, center.Y + size.Y / 2);

    public bool IsGrounded(Vector2 center, Vector2 size)
    {
        float bottom = center.Y + size.Y / 2;
        return AreaHasSolid(center.X - size.X / 2, bottom, center.X + size.X / 2, bottom + GroundProbe);
    }
}
```

`src/Zyon.Core/Physics/TileMover.cs`:

```csharp
using System.Numerics;

namespace Zyon.Core.Physics;

public readonly record struct MoveResult(Vector2 Position, bool HitX, bool HitY, bool Landed, bool SteppedUp);

/// <summary>Movimento AABB contra a grade: eixo X depois Y, subpassos de no máximo 0,5 tile, auto-step de 1 tile.</summary>
public static class TileMover
{
    public const float MaxStep = 0.5f;

    public static MoveResult Move(TileCollider collider, Vector2 position, Vector2 size, Vector2 delta, bool allowStepUp)
    {
        float longest = MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y));
        int steps = Math.Max(1, (int)MathF.Ceiling(longest / MaxStep));
        var step = delta / steps;
        var pos = position;
        bool hitX = false, hitY = false, landed = false, steppedUp = false;

        for (int i = 0; i < steps; i++)
        {
            if (step.X != 0 && !hitX)
            {
                var (p, hit, stepped) = MoveX(collider, pos, size, step.X, allowStepUp);
                pos = p;
                hitX |= hit;
                steppedUp |= stepped;
            }
            if (step.Y != 0 && !hitY)
            {
                var (p, hit) = MoveY(collider, pos, size, step.Y);
                pos = p;
                if (hit)
                {
                    hitY = true;
                    landed = step.Y > 0;
                }
            }
        }

        return new MoveResult(pos, hitX, hitY, landed, steppedUp);
    }

    private static (Vector2 Pos, bool Hit, bool SteppedUp) MoveX(
        TileCollider c, Vector2 pos, Vector2 size, float dx, bool allowStepUp)
    {
        var target = pos with { X = pos.X + dx };
        if (!c.BoxHasSolid(target, size)) return (target, false, false);

        if (allowStepUp)
        {
            var raisedTarget = target with { Y = target.Y - 1f };
            var raisedHere = pos with { Y = pos.Y - 1f };
            if (!c.BoxHasSolid(raisedTarget, size) && !c.BoxHasSolid(raisedHere, size))
                return (raisedTarget, false, true);
        }

        float half = size.X / 2;
        if (dx > 0)
        {
            int col = (int)MathF.Floor(target.X + half - Aabb.Epsilon);
            return (pos with { X = col - half }, true, false);
        }
        else
        {
            int col = (int)MathF.Floor(target.X - half + Aabb.Epsilon);
            return (pos with { X = col + 1 + half }, true, false);
        }
    }

    private static (Vector2 Pos, bool Hit) MoveY(TileCollider c, Vector2 pos, Vector2 size, float dy)
    {
        var target = pos with { Y = pos.Y + dy };
        if (!c.BoxHasSolid(target, size)) return (target, false);

        float half = size.Y / 2;
        if (dy > 0)
        {
            int row = (int)MathF.Floor(target.Y + half - Aabb.Epsilon);
            return (pos with { Y = row - half }, true);
        }
        else
        {
            int row = (int)MathF.Floor(target.Y - half + Aabb.Epsilon);
            return (pos with { Y = row + 1 + half }, true);
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~TileMoverTests"`
Expected: PASS, 9 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): colisão AABB em grade com subpassos e auto-step" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Esqueleto da `Simulation` (ECS, comandos, eventos, visão)

**Files:**
- Create: `src/Zyon.Core/Entities/Components.cs`, `src/Zyon.Core/Entities/EntityKind.cs`, `src/Zyon.Core/Entities/EntityView.cs`
- Create: `src/Zyon.Core/Sim/PlayerInput.cs`, `src/Zyon.Core/Sim/Commands.cs`, `src/Zyon.Core/Sim/Events.cs`, `src/Zyon.Core/Sim/IWorldView.cs`, `src/Zyon.Core/Sim/ISystem.cs`, `src/Zyon.Core/Sim/Queries.cs`, `src/Zyon.Core/Sim/Simulation.cs`
- Create: `tests/Zyon.Core.Tests/SimHarness.cs`
- Test: `tests/Zyon.Core.Tests/SimulationCommandTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/SimHarness.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

/// <summary>Mundo plano 64×64 com chão em y=40 e um jogador em pé em x=20.</summary>
internal sealed class SimHarness : IDisposable
{
    public SimHarness(int width = 64, int height = 64, int groundY = 40, float playerX = 20f, Registry? registry = null)
    {
        Registry = registry ?? TestData.Registry();
        GroundY = groundY;
        Tiles = TestWorlds.Flat(Registry, width, height, groundY);
        Sim = new Simulation(Registry, Tiles);
        PlayerId = Sim.SpawnPlayer(new Vector2(playerX, groundY - Registry.Physics.PlayerHeight / 2));
    }

    public Registry Registry { get; }
    public TileWorld Tiles { get; }
    public Simulation Sim { get; }
    public int PlayerId { get; }
    public int GroundY { get; }

    public float StandingY => GroundY - Registry.Physics.PlayerHeight / 2;

    public Vector2 PlayerPosition =>
        Sim.TryGetEntityView(PlayerId, out var v) ? v.Position : throw new InvalidOperationException("jogador sumiu");

    public Inventory Inventory => (Inventory)Sim.GetInventory(PlayerId)!;

    public void Run(int ticks, PlayerInput input)
    {
        Sim.SetInput(PlayerId, input);
        for (int i = 0; i < ticks; i++)
            Sim.Step();
    }

    public void Idle(int ticks) => Run(ticks, default);

    public void Send(ICommand command)
    {
        Sim.Enqueue(command);
        Sim.Step();
    }

    public void Give(string item, int count) => Inventory.Add(Registry.ItemId(item), count);

    public void Dispose() => Sim.Dispose();
}
```

`tests/Zyon.Core.Tests/SimulationCommandTests.cs`:

```csharp
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class SimulationCommandTests
{
    [Fact]
    public void SpawnPlayer_GivesStarterItems_AndEmitsSpawn()
    {
        using var h = new SimHarness();

        var events = h.Sim.DrainEvents();

        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[0].ItemId);
        var spawned = Assert.Single(events.OfType<EntitySpawned>());
        Assert.Equal(h.PlayerId, spawned.NetworkId);
        Assert.Equal(EntityKind.Player, spawned.Kind);
    }

    [Fact]
    public void Step_AdvancesTick()
    {
        using var h = new SimHarness();

        h.Idle(3);

        Assert.Equal(3, h.Sim.Tick);
    }

    [Fact]
    public void SelectHotbarSlot_Valid_ChangesSelection()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new SelectHotbarSlot(h.PlayerId, 3));

        Assert.Equal(3, h.Sim.GetSelectedSlot(h.PlayerId));
        Assert.Contains(new HotbarSelectionChanged(h.PlayerId, 3), h.Sim.DrainEvents());
    }

    [Fact]
    public void SelectHotbarSlot_OutOfRange_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new SelectHotbarSlot(h.PlayerId, 10));

        Assert.Equal(0, h.Sim.GetSelectedSlot(h.PlayerId));
        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void Command_ForUnknownPlayer_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new SelectHotbarSlot(999, 1));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void MoveInventoryItem_MovesAndEmitsChange()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new MoveInventoryItem(h.PlayerId, 0, 5));

        Assert.True(h.Inventory[0].IsEmpty);
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[5].ItemId);
        var changed = Assert.Single(h.Sim.DrainEvents().OfType<InventoryChanged>());
        Assert.Equal(new[] { 0, 5 }, changed.Slots);
    }

    [Fact]
    public void MoveInventoryItem_Invalid_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new MoveInventoryItem(h.PlayerId, 7, 8));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void DropItem_EmptiesSlot_AndSpawnsDroppedItem()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new DropItem(h.PlayerId, 0));

        Assert.True(h.Inventory[0].IsEmpty);
        var events = h.Sim.DrainEvents();
        var spawned = Assert.Single(events.OfType<EntitySpawned>());
        Assert.Equal(EntityKind.DroppedItem, spawned.Kind);
        Assert.True(h.Sim.TryGetEntityView(spawned.NetworkId, out var view));
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), view.Item.ItemId);
    }

    [Fact]
    public void DropItem_EmptySlot_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new DropItem(h.PlayerId, 9));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void WorldView_ExposesTilesAndEntities()
    {
        using var h = new SimHarness();
        IWorldView view = h.Sim;
        var buffer = new List<EntityView>();

        Assert.Equal(64, view.Width);
        Assert.Equal(h.Registry.BlockId("dirt"), view.GetBlock(0, 40));
        Assert.Equal(1, view.CopyEntities(buffer));
        Assert.Equal(EntityKind.Player, buffer[0].Kind);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~SimulationCommandTests"`
Expected: FAIL na compilação — `The type or namespace name 'Sim' does not exist`.

- [ ] **Step 3: Implementar componentes e tipos de entidade**

`src/Zyon.Core/Entities/EntityKind.cs`:

```csharp
namespace Zyon.Core.Entities;

public enum EntityKind { Player, DroppedItem }
```

`src/Zyon.Core/Entities/Components.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Entities;

/// <summary>Id estável usado em eventos, rede e save. Separado do id interno do Arch.</summary>
public record struct NetworkId(int Value);

/// <summary>Centro do AABB, em tiles.</summary>
public record struct Position(Vector2 Value);

/// <summary>Posição no tick anterior, para interpolação na apresentação.</summary>
public record struct PreviousPosition(Vector2 Value);

/// <summary>Tiles por segundo.</summary>
public record struct Velocity(Vector2 Value);

public struct Body
{
    public Vector2 Size;
    public bool CanStepUp;
    public bool OnGround;
}

public struct PlayerState
{
    public PlayerInput Input;
    public bool PrevJump;
    public int CoyoteTicks;
    public int Facing;
    public int SelectedSlot;
    public int PlaceCooldown;
    public bool Mining;
    public int MineX;
    public int MineY;
    public float MineProgress;
}

public record struct InventoryRef(Inventory Inventory);

public struct DroppedItem
{
    public ItemStack Stack;
    public int AgeTicks;
    public int PickupDelayTicks;
}
```

`src/Zyon.Core/Entities/EntityView.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Items;

namespace Zyon.Core.Entities;

/// <summary>Cópia somente leitura do estado de uma entidade para a apresentação.</summary>
public readonly record struct EntityView(
    int NetworkId,
    EntityKind Kind,
    Vector2 Position,
    Vector2 PreviousPosition,
    Vector2 Size,
    ItemStack Item);
```

- [ ] **Step 4: Implementar input, comandos, eventos e interfaces**

`src/Zyon.Core/Sim/PlayerInput.cs`:

```csharp
namespace Zyon.Core.Sim;

/// <summary>Estado contínuo de controle de um jogador, enviado a cada tick.</summary>
public readonly record struct PlayerInput(float MoveX, bool Jump, bool Primary, int AimX, int AimY);
```

`src/Zyon.Core/Sim/Commands.cs`:

```csharp
namespace Zyon.Core.Sim;

public interface ICommand
{
    int PlayerId { get; }
}

public sealed record SelectHotbarSlot(int PlayerId, int Slot) : ICommand;

public sealed record MoveInventoryItem(int PlayerId, int From, int To) : ICommand;

public sealed record DropItem(int PlayerId, int Slot) : ICommand;
```

`src/Zyon.Core/Sim/Events.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim;

public interface IGameEvent;

public sealed record TileChanged(int X, int Y, ushort Block, ushort Wall) : IGameEvent;

public sealed record EntitySpawned(int NetworkId, EntityKind Kind, Vector2 Position) : IGameEvent;

public sealed record EntityDespawned(int NetworkId) : IGameEvent;

public sealed record InventoryChanged(int PlayerId, IReadOnlyList<int> Slots) : IGameEvent;

public sealed record HotbarSelectionChanged(int PlayerId, int Slot) : IGameEvent;

/// <summary>Progress 0 significa que a mineração parou (limpar overlay).</summary>
public sealed record MiningProgressChanged(int PlayerId, int X, int Y, float Progress) : IGameEvent;
```

`src/Zyon.Core/Sim/IWorldView.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;

namespace Zyon.Core.Sim;

/// <summary>Acesso somente leitura ao estado do jogo para a apresentação.</summary>
public interface IWorldView
{
    Registry Registry { get; }
    int Width { get; }
    int Height { get; }
    long Tick { get; }
    ushort GetBlock(int x, int y);
    ushort GetWall(int x, int y);
    bool TryGetEntityView(int networkId, out EntityView view);
    int CopyEntities(List<EntityView> buffer);
    IReadOnlyInventory? GetInventory(int playerId);
    int GetSelectedSlot(int playerId);
}
```

`src/Zyon.Core/Sim/ISystem.cs`:

```csharp
namespace Zyon.Core.Sim;

internal interface ISystem
{
    void Update(Simulation sim);
}
```

`src/Zyon.Core/Sim/Queries.cs`:

```csharp
using Arch.Core;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim;

internal static class Queries
{
    public static readonly QueryDescription Players = new QueryDescription()
        .WithAll<NetworkId, Position, PreviousPosition, Velocity, Body, PlayerState, InventoryRef>();

    public static readonly QueryDescription Items = new QueryDescription()
        .WithAll<NetworkId, Position, PreviousPosition, Velocity, Body, DroppedItem>();

    public static readonly QueryDescription Physical = new QueryDescription()
        .WithAll<Position, PreviousPosition, Velocity, Body>();
}
```

- [ ] **Step 5: Implementar a `Simulation`**

`src/Zyon.Core/Sim/Simulation.cs`:

```csharp
using System.Numerics;
using Arch.Core;
using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Logging;
using Zyon.Core.Physics;
using Zyon.Core.Tiles;

namespace Zyon.Core.Sim;

/// <summary>Estado autoritativo do jogo. Avança em ticks fixos de 60 Hz.</summary>
public sealed class Simulation : IWorldView, IDisposable
{
    public const int TicksPerSecond = 60;
    public const float Dt = 1f / TicksPerSecond;

    private readonly Queue<ICommand> commands = new();
    private readonly List<IGameEvent> events = new();
    private readonly Dictionary<int, Entity> byNetworkId = new();
    private readonly List<ISystem> systems;
    private int nextNetworkId = 1;

    public Simulation(Registry registry, TileWorld tiles, ILog? log = null)
    {
        Registry = registry;
        Tiles = tiles;
        Log = log ?? NullLog.Instance;
        Collider = new TileCollider(tiles, registry);
        Ecs = World.Create();
        systems = new List<ISystem>();
    }

    public Registry Registry { get; }
    public TileWorld Tiles { get; }
    public ILog Log { get; }
    public long Tick { get; private set; }
    public int InvalidCommandCount { get; private set; }
    public int Width => Tiles.Width;
    public int Height => Tiles.Height;

    internal TileCollider Collider { get; }
    internal World Ecs { get; }
    internal PhysicsConfig Config => Registry.Physics;

    public ushort GetBlock(int x, int y) => Tiles.GetBlock(x, y);
    public ushort GetWall(int x, int y) => Tiles.GetWall(x, y);

    public int SpawnPlayer(Vector2 position)
    {
        var inventory = new Inventory(Registry);
        foreach (var stack in Registry.StarterItems)
            inventory.Add(stack.ItemId, stack.Count);

        int id = nextNetworkId++;
        var entity = Ecs.Create(
            new NetworkId(id),
            new Position(position),
            new PreviousPosition(position),
            new Velocity(Vector2.Zero),
            new Body { Size = new Vector2(Config.PlayerWidth, Config.PlayerHeight), CanStepUp = true },
            new PlayerState { Facing = 1 },
            new InventoryRef(inventory));
        byNetworkId[id] = entity;
        Emit(new EntitySpawned(id, EntityKind.Player, position));
        return id;
    }

    public void SetInput(int playerId, PlayerInput input)
    {
        if (TryGetEntity(playerId, out var e) && Ecs.Has<PlayerState>(e))
            Ecs.Get<PlayerState>(e).Input = input;
    }

    public void Enqueue(ICommand command) => commands.Enqueue(command);

    public void Step()
    {
        ProcessCommands();
        foreach (var system in systems)
            system.Update(this);
        Tick++;
    }

    public IReadOnlyList<IGameEvent> DrainEvents()
    {
        var drained = events.ToArray();
        events.Clear();
        return drained;
    }

    public bool TryGetEntityView(int networkId, out EntityView view)
    {
        if (!TryGetEntity(networkId, out var e))
        {
            view = default;
            return false;
        }
        view = MakeView(e);
        return true;
    }

    public int CopyEntities(List<EntityView> buffer)
    {
        buffer.Clear();
        foreach (var e in byNetworkId.Values)
            buffer.Add(MakeView(e));
        return buffer.Count;
    }

    public IReadOnlyInventory? GetInventory(int playerId) =>
        TryGetEntity(playerId, out var e) && Ecs.Has<InventoryRef>(e) ? Ecs.Get<InventoryRef>(e).Inventory : null;

    public int GetSelectedSlot(int playerId) =>
        TryGetEntity(playerId, out var e) && Ecs.Has<PlayerState>(e) ? Ecs.Get<PlayerState>(e).SelectedSlot : -1;

    public void Dispose() => World.Destroy(Ecs);

    internal int SpawnDroppedItem(ItemStack stack, Vector2 position, Vector2 velocity, int pickupDelayTicks)
    {
        int id = nextNetworkId++;
        float size = Config.ItemSize;
        var entity = Ecs.Create(
            new NetworkId(id),
            new Position(position),
            new PreviousPosition(position),
            new Velocity(velocity),
            new Body { Size = new Vector2(size, size) },
            new DroppedItem { Stack = stack, PickupDelayTicks = pickupDelayTicks });
        byNetworkId[id] = entity;
        Emit(new EntitySpawned(id, EntityKind.DroppedItem, position));
        return id;
    }

    internal void DestroyEntity(int networkId)
    {
        if (!byNetworkId.Remove(networkId, out var e)) return;
        Ecs.Destroy(e);
        Emit(new EntityDespawned(networkId));
    }

    internal bool TryGetEntity(int networkId, out Entity entity) => byNetworkId.TryGetValue(networkId, out entity);

    internal void Emit(IGameEvent gameEvent) => events.Add(gameEvent);

    private EntityView MakeView(Entity e)
    {
        bool isPlayer = Ecs.Has<PlayerState>(e);
        return new EntityView(
            Ecs.Get<NetworkId>(e).Value,
            isPlayer ? EntityKind.Player : EntityKind.DroppedItem,
            Ecs.Get<Position>(e).Value,
            Ecs.Get<PreviousPosition>(e).Value,
            Ecs.Get<Body>(e).Size,
            isPlayer ? ItemStack.Empty : Ecs.Get<DroppedItem>(e).Stack);
    }

    private void ProcessCommands()
    {
        while (commands.TryDequeue(out var command))
        {
            if (Apply(command)) continue;
            InvalidCommandCount++;
            Log.Debug($"Comando inválido descartado: {command}");
        }
    }

    private bool Apply(ICommand command)
    {
        if (!TryGetEntity(command.PlayerId, out var e) || !Ecs.Has<PlayerState>(e)) return false;
        var inventory = Ecs.Get<InventoryRef>(e).Inventory;

        switch (command)
        {
            case SelectHotbarSlot c:
                if ((uint)c.Slot >= Inventory.HotbarSize) return false;
                Ecs.Get<PlayerState>(e).SelectedSlot = c.Slot;
                Emit(new HotbarSelectionChanged(c.PlayerId, c.Slot));
                return true;

            case MoveInventoryItem c:
            {
                var changed = new List<int>(2);
                if (!inventory.Move(c.From, c.To, changed)) return false;
                Emit(new InventoryChanged(c.PlayerId, changed));
                return true;
            }

            case DropItem c:
            {
                if (!Inventory.IsValidSlot(c.Slot) || inventory[c.Slot].IsEmpty) return false;
                var stack = inventory.TakeAll(c.Slot);
                float facing = Ecs.Get<PlayerState>(e).Facing;
                var origin = Ecs.Get<Position>(e).Value;
                SpawnDroppedItem(stack,
                    origin + new Vector2(facing * 0.8f, -0.5f),
                    new Vector2(facing * Config.DropImpulseX, -Config.DropImpulseY),
                    Config.DropPickupDelayTicks);
                Emit(new InventoryChanged(c.PlayerId, [c.Slot]));
                return true;
            }

            default:
                return false;
        }
    }
}
```

- [ ] **Step 6: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~SimulationCommandTests"`
Expected: PASS, 10 testes.

- [ ] **Step 7: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos os testes.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(core): Simulation com ECS Arch, comandos, eventos e IWorldView" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Movimento do jogador e sistema de física

**Files:**
- Create: `src/Zyon.Core/Sim/Systems/PlayerMovementSystem.cs`, `src/Zyon.Core/Sim/Systems/PhysicsSystem.cs`
- Modify: `src/Zyon.Core/Sim/Simulation.cs` (lista de sistemas no construtor)
- Test: `tests/Zyon.Core.Tests/PlayerMovementTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/PlayerMovementTests.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class PlayerMovementTests
{
    private static readonly PlayerInput Right = new(1f, false, false, 0, 0);
    private static readonly PlayerInput JumpHeld = new(0f, true, false, 0, 0);

    [Fact]
    public void Idle_PlayerStaysOnGround()
    {
        using var h = new SimHarness();

        h.Idle(60);

        Assert.Equal(h.StandingY, h.PlayerPosition.Y, 3);
        Assert.Equal(20f, h.PlayerPosition.X, 3);
    }

    [Fact]
    public void FloatingPlayer_FallsToGround()
    {
        using var h = new SimHarness();
        h.Sim.SetInput(h.PlayerId, default);
        // Remove o chão sob o jogador por 5 linhas e deixa um piso mais baixo.
        for (int y = 40; y < 45; y++)
            for (int x = 15; x < 26; x++)
                h.Tiles.SetBlock(x, y, 0);

        h.Idle(120);

        Assert.Equal(45f - h.Registry.Physics.PlayerHeight / 2, h.PlayerPosition.Y, 3);
    }

    [Fact]
    public void WalkRight_AcceleratesAndMoves()
    {
        using var h = new SimHarness();

        h.Run(60, Right);

        Assert.True(h.PlayerPosition.X - 20f > 7f, $"andou só {h.PlayerPosition.X - 20f}");
    }

    [Fact]
    public void ReleasingMove_Decelerates()
    {
        using var h = new SimHarness();
        h.Run(30, Right);

        h.Idle(30);
        float x1 = h.PlayerPosition.X;
        h.Idle(10);

        Assert.Equal(x1, h.PlayerPosition.X, 3);
    }

    [Fact]
    public void Jump_RisesThenLands()
    {
        using var h = new SimHarness();
        h.Idle(5);
        float minY = float.MaxValue;

        for (int i = 0; i < 40; i++)
        {
            h.Run(1, JumpHeld);
            minY = MathF.Min(minY, h.PlayerPosition.Y);
        }
        h.Idle(120);

        Assert.True(h.StandingY - minY > 3.5f, $"pulo de só {h.StandingY - minY}");
        Assert.Equal(h.StandingY, h.PlayerPosition.Y, 3);
    }

    [Fact]
    public void ShortTap_JumpsLowerThanHeldJump()
    {
        float Peak(int holdTicks)
        {
            using var h = new SimHarness();
            h.Idle(5);
            float minY = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                h.Run(1, i < holdTicks ? JumpHeld : default);
                minY = MathF.Min(minY, h.PlayerPosition.Y);
            }
            return h.StandingY - minY;
        }

        Assert.True(Peak(3) < Peak(40) * 0.6f);
    }

    [Fact]
    public void CannotJumpInMidAir()
    {
        using var h = new SimHarness();
        h.Idle(5);
        h.Run(30, JumpHeld);   // sobe e começa a cair
        h.Idle(1);              // solta
        float before = h.PlayerPosition.Y;

        h.Run(3, JumpHeld);     // tenta pular no ar

        Assert.True(h.PlayerPosition.Y >= before, "pulou no ar");
    }

    [Fact]
    public void Walking_StepsUpOneTileLedge()
    {
        using var h = new SimHarness();
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dirt"));

        h.Run(60, Right);

        Assert.True(h.PlayerPosition.X > 25f, $"parou em {h.PlayerPosition.X}");
    }

    [Fact]
    public void Walking_IsBlockedByTwoTileWall()
    {
        using var h = new SimHarness();
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dirt"));
        h.Tiles.SetBlock(23, 38, h.Registry.BlockId("dirt"));

        h.Run(60, Right);

        Assert.Equal(23f - h.Registry.Physics.PlayerWidth / 2, h.PlayerPosition.X, 3);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~PlayerMovementTests"`
Expected: FAIL — `WalkRight_AcceleratesAndMoves` e `Jump_RisesThenLands` falham (o jogador não se move, pois não há sistemas); `FloatingPlayer_FallsToGround` falha (sem gravidade).

- [ ] **Step 3: Implementar os sistemas**

`src/Zyon.Core/Sim/Systems/PlayerMovementSystem.cs`:

```csharp
using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

/// <summary>Converte o input em velocidade: aceleração, pulo variável, coyote time, gravidade.</summary>
internal sealed class PlayerMovementSystem : ISystem
{
    public void Update(Simulation sim)
    {
        var cfg = sim.Config;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Players, (ref Velocity vel, ref Body body, ref PlayerState st) =>
        {
            var input = st.Input;
            var v = vel.Value;

            float move = Math.Clamp(input.MoveX, -1f, 1f);
            if (move != 0f)
            {
                st.Facing = move > 0 ? 1 : -1;
                v.X = Approach(v.X, move * cfg.MaxMoveSpeed, cfg.MoveAcceleration * dt);
            }
            else
            {
                v.X = Approach(v.X, 0f, cfg.MoveDeceleration * dt);
            }

            st.CoyoteTicks = body.OnGround ? cfg.CoyoteTicks : Math.Max(0, st.CoyoteTicks - 1);

            bool pressed = input.Jump && !st.PrevJump;
            bool released = !input.Jump && st.PrevJump;
            if (pressed && st.CoyoteTicks > 0)
            {
                v.Y = -cfg.JumpVelocity;
                st.CoyoteTicks = 0;
            }
            else if (released && v.Y < 0f)
            {
                v.Y *= cfg.JumpCutFactor;
            }

            v.Y = MathF.Min(v.Y + cfg.Gravity * dt, cfg.TerminalVelocity);
            st.PrevJump = input.Jump;
            vel.Value = v;
        });
    }

    private static float Approach(float current, float target, float maxDelta) =>
        current < target ? MathF.Min(current + maxDelta, target) : MathF.Max(current - maxDelta, target);
}
```

`src/Zyon.Core/Sim/Systems/PhysicsSystem.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Integra a velocidade contra a grade de tiles para toda entidade física.</summary>
internal sealed class PhysicsSystem : ISystem
{
    public void Update(Simulation sim)
    {
        var collider = sim.Collider;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Physical, (ref Position pos, ref PreviousPosition prev, ref Velocity vel, ref Body body) =>
        {
            prev.Value = pos.Value;
            var v = vel.Value;
            if (v != Vector2.Zero)
            {
                var result = TileMover.Move(collider, pos.Value, body.Size, v * dt, body.CanStepUp && body.OnGround);
                pos.Value = result.Position;
                if (result.HitX) v.X = 0f;
                if (result.HitY) v.Y = 0f;
                vel.Value = v;
            }
            body.OnGround = collider.IsGrounded(pos.Value, body.Size);
        });
    }
}
```

- [ ] **Step 4: Registrar os sistemas na `Simulation`**

Em `src/Zyon.Core/Sim/Simulation.cs`, adicionar `using Zyon.Core.Sim.Systems;` e trocar a linha `systems = new List<ISystem>();` por:

```csharp
        systems =
        [
            new PlayerMovementSystem(),
            new PhysicsSystem(),
        ];
```

- [ ] **Step 5: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~PlayerMovementTests"`
Expected: PASS, 9 testes.

- [ ] **Step 6: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos os testes.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): movimento do jogador e integração de física no tick" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Minerar e colocar blocos

**Files:**
- Create: `src/Zyon.Core/Sim/Systems/ToolActionSystem.cs`
- Modify: `src/Zyon.Core/Sim/Simulation.cs` (lista de sistemas)
- Test: `tests/Zyon.Core.Tests/ToolActionTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/ToolActionTests.cs`:

```csharp
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ToolActionTests
{
    private static PlayerInput Use(int x, int y) => new(0f, false, true, x, y);

    /// <summary>Ticks necessários para quebrar terra com a picareta inicial, a partir dos dados.</summary>
    private static int TicksToMineDirt(SimHarness h)
    {
        var r = h.Registry;
        var pick = r.Item(r.ItemId("basic_pickaxe"));
        float hardness = r.Block(r.BlockId("dirt")).Hardness;
        float perSecond = pick.ToolPower * pick.ToolSpeed * r.Physics.MiningRate / hardness;
        return (int)MathF.Ceiling(Simulation.TicksPerSecond / perSecond);
    }

    [Fact]
    public void MiningDirt_BreaksAfterEnoughTicks_AndDropsItem()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);
        h.Idle(5);
        h.Sim.DrainEvents();

        h.Run(needed - 5, Use(21, 40));
        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));

        h.Run(10, Use(21, 40));
        Assert.Equal(0, h.Tiles.GetBlock(21, 40));

        var events = h.Sim.DrainEvents();
        Assert.Contains(new TileChanged(21, 40, 0, 0), events);
        Assert.Contains(events.OfType<EntitySpawned>(), e => e.Kind == EntityKind.DroppedItem);
        Assert.Contains(events.OfType<MiningProgressChanged>(), e => e.Progress > 0f);
    }

    [Fact]
    public void ChangingTarget_ResetsProgress()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);

        h.Run(needed - 5, Use(21, 40));
        h.Run(needed - 5, Use(22, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
        Assert.NotEqual(0, h.Tiles.GetBlock(22, 40));
    }

    [Fact]
    public void ReleasingButton_ResetsProgress()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);

        h.Run(needed - 5, Use(21, 40));
        h.Idle(1);
        h.Run(needed - 5, Use(21, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
    }

    [Fact]
    public void OutOfReach_DoesNotMine()
    {
        using var h = new SimHarness();

        h.Run(200, Use(30, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(30, 40));
    }

    [Fact]
    public void PlacingBlock_ConsumesItem()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Sim.DrainEvents();

        h.Run(1, Use(22, 39));

        Assert.Equal(h.Registry.BlockId("dirt"), h.Tiles.GetBlock(22, 39));
        Assert.Equal(4, h.Inventory[1].Count);
        var events = h.Sim.DrainEvents();
        Assert.Contains(new TileChanged(22, 39, h.Registry.BlockId("dirt"), 0), events);
        Assert.Contains(events.OfType<InventoryChanged>(), e => e.Slots.Contains(1));
    }

    [Fact]
    public void Placing_RequiresAnchor()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(5, Use(22, 36));

        Assert.Equal(0, h.Tiles.GetBlock(22, 36));
    }

    [Fact]
    public void Placing_WallCountsAsAnchor()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Tiles.SetWall(22, 36, h.Registry.WallId("dirt_wall"));

        h.Run(1, Use(22, 36));

        Assert.Equal(h.Registry.BlockId("dirt"), h.Tiles.GetBlock(22, 36));
    }

    [Fact]
    public void Placing_InsidePlayer_IsBlocked()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(5, Use(20, 39));

        Assert.Equal(0, h.Tiles.GetBlock(20, 39));
    }

    [Fact]
    public void Placing_HasCooldown()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(1, Use(22, 39));
        h.Run(1, Use(23, 39));
        Assert.Equal(0, h.Tiles.GetBlock(23, 39));

        h.Run(h.Registry.Physics.PlaceCooldownTicks, Use(23, 39));
        Assert.NotEqual(0, h.Tiles.GetBlock(23, 39));
    }

    [Fact]
    public void EmptySlot_DoesNothing()
    {
        using var h = new SimHarness();
        h.Send(new SelectHotbarSlot(h.PlayerId, 4));

        h.Run(100, Use(21, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~ToolActionTests"`
Expected: FAIL — `MiningDirt_BreaksAfterEnoughTicks_AndDropsItem`, `PlacingBlock_ConsumesItem`, `Placing_WallCountsAsAnchor` e `Placing_HasCooldown` falham (ainda não há sistema de ação).

- [ ] **Step 3: Implementar o sistema**

`src/Zyon.Core/Sim/Systems/ToolActionSystem.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Ação primária contextual: ferramenta minera, bloco é colocado.</summary>
internal sealed class ToolActionSystem : ISystem
{
    private static readonly Vector2 TileSize = new(1f, 1f);

    private readonly List<(Vector2 Center, Vector2 Size)> placementBlockers = new();
    private readonly List<(ItemStack Stack, Vector2 Position)> pendingDrops = new();

    public void Update(Simulation sim)
    {
        placementBlockers.Clear();
        pendingDrops.Clear();

        sim.Ecs.Query(in Queries.Players, (ref Position p, ref Body b) => placementBlockers.Add((p.Value, b.Size)));

        sim.Ecs.Query(in Queries.Players,
            (ref NetworkId id, ref Position pos, ref PlayerState st, ref InventoryRef inv) =>
                Process(sim, id.Value, pos.Value, ref st, inv.Inventory));

        foreach (var (stack, position) in pendingDrops)
            sim.SpawnDroppedItem(stack, position, Vector2.Zero, 0);
    }

    private void Process(Simulation sim, int playerId, Vector2 playerCenter, ref PlayerState st, Inventory inventory)
    {
        var cfg = sim.Config;
        if (st.PlaceCooldown > 0) st.PlaceCooldown--;

        var input = st.Input;
        var stack = inventory[st.SelectedSlot];
        if (!input.Primary || stack.IsEmpty)
        {
            StopMining(sim, playerId, ref st);
            return;
        }

        var item = sim.Registry.Item(stack.ItemId);
        int tx = input.AimX, ty = input.AimY;
        bool inReach = Vector2.Distance(playerCenter, new Vector2(tx + 0.5f, ty + 0.5f)) <= cfg.ReachTiles;

        switch (item.Kind)
        {
            case ItemKind.Tool:
                Mine(sim, playerId, ref st, item, tx, ty, inReach);
                break;

            case ItemKind.Block:
                StopMining(sim, playerId, ref st);
                if (st.PlaceCooldown == 0 && inReach && CanPlace(sim, tx, ty))
                {
                    sim.Tiles.SetBlock(tx, ty, item.PlacesBlockId);
                    inventory.ConsumeOne(st.SelectedSlot);
                    sim.Emit(new TileChanged(tx, ty, item.PlacesBlockId, sim.Tiles.GetWall(tx, ty)));
                    sim.Emit(new InventoryChanged(playerId, [st.SelectedSlot]));
                    st.PlaceCooldown = cfg.PlaceCooldownTicks;
                }
                break;

            default:
                StopMining(sim, playerId, ref st);
                break;
        }
    }

    private void Mine(Simulation sim, int playerId, ref PlayerState st, ItemDef tool, int tx, int ty, bool inReach)
    {
        ushort blockId = sim.Tiles.GetBlock(tx, ty);
        if (!inReach || blockId == 0)
        {
            StopMining(sim, playerId, ref st);
            return;
        }

        if (!st.Mining || st.MineX != tx || st.MineY != ty)
        {
            StopMining(sim, playerId, ref st);
            st.Mining = true;
            st.MineX = tx;
            st.MineY = ty;
            st.MineProgress = 0f;
        }

        var block = sim.Registry.Block(blockId);
        float rate = tool.ToolPower * tool.ToolSpeed * sim.Config.MiningRate;
        st.MineProgress = block.Hardness <= 0f ? 1f : st.MineProgress + rate * Simulation.Dt / block.Hardness;

        if (st.MineProgress < 1f)
        {
            sim.Emit(new MiningProgressChanged(playerId, tx, ty, st.MineProgress));
            return;
        }

        sim.Tiles.SetBlock(tx, ty, 0);
        sim.Emit(new TileChanged(tx, ty, 0, sim.Tiles.GetWall(tx, ty)));
        if (block.DropItemId != 0)
            pendingDrops.Add((new ItemStack(block.DropItemId, 1), new Vector2(tx + 0.5f, ty + 0.5f)));
        StopMining(sim, playerId, ref st);
    }

    private static void StopMining(Simulation sim, int playerId, ref PlayerState st)
    {
        if (!st.Mining) return;
        st.Mining = false;
        st.MineProgress = 0f;
        sim.Emit(new MiningProgressChanged(playerId, st.MineX, st.MineY, 0f));
    }

    private bool CanPlace(Simulation sim, int tx, int ty)
    {
        var tiles = sim.Tiles;
        if (!tiles.InBounds(tx, ty) || tiles.GetBlock(tx, ty) != 0) return false;

        bool anchored = tiles.GetWall(tx, ty) != 0
                        || tiles.GetBlock(tx - 1, ty) != 0
                        || tiles.GetBlock(tx + 1, ty) != 0
                        || tiles.GetBlock(tx, ty - 1) != 0
                        || tiles.GetBlock(tx, ty + 1) != 0;
        if (!anchored) return false;

        var tileCenter = new Vector2(tx + 0.5f, ty + 0.5f);
        foreach (var (center, size) in placementBlockers)
            if (Aabb.Overlaps(center, size, tileCenter, TileSize))
                return false;
        return true;
    }
}
```

- [ ] **Step 4: Registrar o sistema**

Em `src/Zyon.Core/Sim/Simulation.cs`, a lista de sistemas passa a ser:

```csharp
        systems =
        [
            new PlayerMovementSystem(),
            new PhysicsSystem(),
            new ToolActionSystem(),
        ];
```

- [ ] **Step 5: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~ToolActionTests"`
Expected: PASS, 10 testes.

- [ ] **Step 6: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos os testes.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): mineração com progresso e colocação de blocos validada" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Itens caídos — coleta, fusão e despawn

**Files:**
- Create: `src/Zyon.Core/Sim/Systems/ItemPickupSystem.cs`, `src/Zyon.Core/Sim/Systems/ItemMergeSystem.cs`, `src/Zyon.Core/Sim/Systems/ItemDespawnSystem.cs`
- Modify: `src/Zyon.Core/Sim/Simulation.cs` (lista de sistemas)
- Test: `tests/Zyon.Core.Tests/ItemSystemsTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/ItemSystemsTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ItemSystemsTests
{
    private static ItemStack Dirt(SimHarness h, int count = 1) => new(h.Registry.ItemId("dirt"), count);

    private static float RestingY(SimHarness h) => h.GroundY - h.Registry.Physics.ItemSize / 2;

    [Fact]
    public void DroppedItem_FallsAndRestsOnGround()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, 30f), Vector2.Zero, 0);

        h.Idle(120);

        Assert.True(h.Sim.TryGetEntityView(id, out var view));
        Assert.Equal(RestingY(h), view.Position.Y, 3);
    }

    [Fact]
    public void ItemNearPlayer_IsPickedUp()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(22f, RestingY(h)), Vector2.Zero, 0);
        h.Sim.DrainEvents();

        h.Idle(60);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
        Assert.Equal(Dirt(h), h.Inventory[1]);
        var events = h.Sim.DrainEvents();
        Assert.Contains(new EntityDespawned(id), events);
        Assert.Contains(events.OfType<InventoryChanged>(), e => e.Slots.Contains(1));
    }

    [Fact]
    public void ItemFarFromPlayer_IsNotPickedUp()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(40f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(60);

        Assert.True(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void PickupDelay_IsRespected()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(20f, RestingY(h)), Vector2.Zero, 90);

        h.Idle(30);
        Assert.True(h.Sim.TryGetEntityView(id, out _));

        h.Idle(90);
        Assert.False(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void FullInventory_LeavesItemOnGround()
    {
        using var h = new SimHarness();
        for (int i = 1; i < Inventory.Slots; i++)
            h.Inventory.Set(i, new ItemStack(h.Registry.ItemId("basic_pickaxe"), 1));
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(21f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(60);

        Assert.True(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void NearbyEqualItems_Merge()
    {
        using var h = new SimHarness();
        int a = h.Sim.SpawnDroppedItem(Dirt(h, 2), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);
        int b = h.Sim.SpawnDroppedItem(Dirt(h, 3), new Vector2(45.5f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(h.Registry.Physics.ItemMergeIntervalTicks + 1);

        // Qualquer um dos dois pode absorver o outro, dependendo da ordem de iteração do ECS.
        bool aAlive = h.Sim.TryGetEntityView(a, out var viewA);
        bool bAlive = h.Sim.TryGetEntityView(b, out var viewB);
        Assert.True(aAlive ^ bAlive, "deveria sobrar exatamente uma pilha");
        Assert.Equal(5, (aAlive ? viewA : viewB).Item.Count);
    }

    [Fact]
    public void DistantItems_DoNotMerge()
    {
        using var h = new SimHarness();
        int a = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);
        int b = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(48f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(h.Registry.Physics.ItemMergeIntervalTicks + 1);

        Assert.True(h.Sim.TryGetEntityView(a, out _));
        Assert.True(h.Sim.TryGetEntityView(b, out _));
    }

    [Fact]
    public void OldItems_Despawn()
    {
        var registry = TestData.RegistryWithPhysics("""{"itemDespawnSeconds": 1}""");
        using var h = new SimHarness(registry: registry);
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(Simulation.TicksPerSecond + 1);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void DroppedItem_IsRecollectedAfterDelay()
    {
        using var h = new SimHarness();
        h.Send(new DropItem(h.PlayerId, 0));
        Assert.True(h.Inventory[0].IsEmpty);

        // O impulso joga o item ~3 tiles à frente, fora do raio do ímã: o jogador anda até ele.
        h.Idle(h.Registry.Physics.DropPickupDelayTicks);
        h.Run(30, new PlayerInput(1f, false, false, 0, 0));
        h.Idle(60);

        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[0].ItemId);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~ItemSystemsTests"`
Expected: FAIL — itens não caem (sem gravidade para itens), não são coletados, não se fundem nem somem.

- [ ] **Step 3: Implementar os sistemas**

`src/Zyon.Core/Sim/Systems/ItemPickupSystem.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Forças sobre itens caídos (gravidade, atrito, ímã) e coleta pelo jogador.</summary>
internal sealed class ItemPickupSystem : ISystem
{
    private readonly List<(int Id, Vector2 Center, Vector2 Size, Inventory Inventory)> players = new();
    private readonly List<int> collected = new();
    private readonly List<int> changedSlots = new();

    public void Update(Simulation sim)
    {
        players.Clear();
        collected.Clear();
        sim.Ecs.Query(in Queries.Players, (ref NetworkId id, ref Position p, ref Body b, ref InventoryRef inv) =>
            players.Add((id.Value, p.Value, b.Size, inv.Inventory)));

        var cfg = sim.Config;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Items,
            (ref NetworkId id, ref Position pos, ref Velocity vel, ref Body body, ref DroppedItem item) =>
            {
                item.AgeTicks++;
                if (item.PickupDelayTicks > 0) item.PickupDelayTicks--;
                var v = vel.Value;
                bool magnetized = false;

                if (item.PickupDelayTicks == 0)
                {
                    int best = -1;
                    float bestDistance = cfg.MagnetRadius;
                    for (int i = 0; i < players.Count; i++)
                    {
                        float d = Vector2.Distance(players[i].Center, pos.Value);
                        if (d <= bestDistance && players[i].Inventory.CanAccept(item.Stack.ItemId))
                        {
                            best = i;
                            bestDistance = d;
                        }
                    }

                    if (best >= 0)
                    {
                        var player = players[best];
                        magnetized = true;

                        if (Aabb.Overlaps(pos.Value, body.Size, player.Center, player.Size))
                        {
                            changedSlots.Clear();
                            int left = player.Inventory.Add(item.Stack.ItemId, item.Stack.Count, changedSlots);
                            if (changedSlots.Count > 0)
                                sim.Emit(new InventoryChanged(player.Id, changedSlots.ToArray()));
                            if (left == 0)
                            {
                                collected.Add(id.Value);
                                vel.Value = Vector2.Zero;
                                return;
                            }
                            item.Stack = item.Stack with { Count = left };
                        }

                        var toPlayer = player.Center - pos.Value;
                        if (toPlayer.LengthSquared() > 1e-6f)
                            v += Vector2.Normalize(toPlayer) * cfg.MagnetAcceleration * dt;
                        if (v.Length() > cfg.MagnetMaxSpeed)
                            v = Vector2.Normalize(v) * cfg.MagnetMaxSpeed;
                    }
                }

                if (!magnetized)
                {
                    v.Y = MathF.Min(v.Y + cfg.Gravity * dt, cfg.TerminalVelocity);
                    if (body.OnGround) v.X *= cfg.ItemGroundFriction;
                }

                vel.Value = v;
            });

        foreach (var id in collected)
            sim.DestroyEntity(id);
    }
}
```

`src/Zyon.Core/Sim/Systems/ItemMergeSystem.cs`:

```csharp
using System.Numerics;
using Arch.Core;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

/// <summary>Funde itens iguais próximos, usando hash espacial de células de 1 tile. Roda a cada N ticks.</summary>
internal sealed class ItemMergeSystem : ISystem
{
    private readonly Dictionary<long, List<Entity>> cells = new();
    private readonly List<Entity> items = new();
    private readonly List<int> emptied = new();

    public void Update(Simulation sim)
    {
        var cfg = sim.Config;
        if (cfg.ItemMergeIntervalTicks <= 0 || sim.Tick % cfg.ItemMergeIntervalTicks != 0) return;

        cells.Clear();
        items.Clear();
        emptied.Clear();
        sim.Ecs.Query(in Queries.Items, (Entity e, ref Position p) =>
        {
            items.Add(e);
            var key = Key((int)MathF.Floor(p.Value.X), (int)MathF.Floor(p.Value.Y));
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = new List<Entity>();
            list.Add(e);
        });

        var ecs = sim.Ecs;
        foreach (var a in items)
        {
            ref var itemA = ref ecs.Get<DroppedItem>(a);
            if (itemA.Stack.IsEmpty) continue;
            int max = sim.Registry.Item(itemA.Stack.ItemId).MaxStack;
            var posA = ecs.Get<Position>(a).Value;
            int cx = (int)MathF.Floor(posA.X), cy = (int)MathF.Floor(posA.Y);

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (itemA.Stack.Count >= max) break;
                if (!cells.TryGetValue(Key(cx + dx, cy + dy), out var neighbours)) continue;
                foreach (var b in neighbours)
                {
                    if (b == a) continue;
                    ref var itemB = ref ecs.Get<DroppedItem>(b);
                    if (itemB.Stack.IsEmpty || itemB.Stack.ItemId != itemA.Stack.ItemId) continue;
                    if (Vector2.Distance(posA, ecs.Get<Position>(b).Value) > cfg.ItemMergeRadius) continue;

                    int n = Math.Min(max - itemA.Stack.Count, itemB.Stack.Count);
                    if (n <= 0) break;
                    itemA.Stack = itemA.Stack with { Count = itemA.Stack.Count + n };
                    itemB.Stack = itemB.Stack with { Count = itemB.Stack.Count - n };
                    if (itemB.Stack.IsEmpty)
                        emptied.Add(ecs.Get<NetworkId>(b).Value);
                }
            }
        }

        foreach (var id in emptied)
            sim.DestroyEntity(id);
    }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
}
```

`src/Zyon.Core/Sim/Systems/ItemDespawnSystem.cs`:

```csharp
using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

internal sealed class ItemDespawnSystem : ISystem
{
    private readonly List<int> expired = new();

    public void Update(Simulation sim)
    {
        expired.Clear();
        int maxAge = (int)(sim.Config.ItemDespawnSeconds * Simulation.TicksPerSecond);
        sim.Ecs.Query(in Queries.Items, (ref NetworkId id, ref DroppedItem item) =>
        {
            if (item.AgeTicks >= maxAge) expired.Add(id.Value);
        });
        foreach (var id in expired)
            sim.DestroyEntity(id);
    }
}
```

- [ ] **Step 4: Registrar os sistemas na ordem do spec**

Em `src/Zyon.Core/Sim/Simulation.cs`, a lista de sistemas passa a ser:

```csharp
        systems =
        [
            new PlayerMovementSystem(),
            new PhysicsSystem(),
            new ToolActionSystem(),
            new ItemPickupSystem(),
            new ItemMergeSystem(),
            new ItemDespawnSystem(),
        ];
```

- [ ] **Step 5: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~ItemSystemsTests"`
Expected: PASS, 9 testes.

- [ ] **Step 6: Rodar a suíte inteira**

Run: `dotnet test`
Expected: PASS, todos os testes. Atenção a `ToolActionTests.MiningDirt_BreaksAfterEnoughTicks_AndDropsItem`: o item caído agora pode ser coletado, mas o teste só verifica o evento `EntitySpawned`, então continua válido.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): itens caídos com ímã, coleta, fusão e despawn" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `IGameSession` e `LocalSession`

**Files:**
- Create: `src/Zyon.Core/Sim/IGameSession.cs`, `src/Zyon.Core/Sim/LocalSession.cs`
- Test: `tests/Zyon.Core.Tests/LocalSessionTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/LocalSessionTests.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class LocalSessionTests
{
    private static (LocalSession Session, SimHarness Harness) Create()
    {
        var h = new SimHarness();
        return (new LocalSession(h.Sim, h.PlayerId), h);
    }

    [Fact]
    public void Update_RunsWholeTicks_AndKeepsRemainderAsAlpha()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(3.5 / Simulation.TicksPerSecond);

        Assert.Equal(3, session.World.Tick);
        Assert.Equal(0.5f, session.InterpolationAlpha, 3);
    }

    [Fact]
    public void Update_AccumulatesSmallDeltas()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(0.6 / Simulation.TicksPerSecond);
        session.Update(0.6 / Simulation.TicksPerSecond);

        Assert.Equal(1, session.World.Tick);
    }

    [Fact]
    public void Update_CapsTicksPerCall_AndDropsBacklog()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(1.0);
        Assert.Equal(LocalSession.MaxTicksPerUpdate, session.World.Tick);

        session.Update(0.0);
        Assert.Equal(LocalSession.MaxTicksPerUpdate, session.World.Tick);
    }

    [Fact]
    public void SendAndSetInput_ReachTheLocalPlayer()
    {
        var (session, h) = Create();
        using var _s = session;
        session.DrainEvents();

        session.Send(new SelectHotbarSlot(session.LocalPlayerId, 2));
        session.SetInput(new PlayerInput(1f, false, false, 0, 0));
        session.Update(30.0 / Simulation.TicksPerSecond);

        Assert.Equal(2, session.World.GetSelectedSlot(session.LocalPlayerId));
        Assert.True(h.PlayerPosition.X > 20.5f);
        Assert.Contains(new HotbarSelectionChanged(session.LocalPlayerId, 2), session.DrainEvents());
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~LocalSessionTests"`
Expected: FAIL na compilação — `The type or namespace name 'LocalSession' could not be found`.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Sim/IGameSession.cs`:

```csharp
namespace Zyon.Core.Sim;

/// <summary>Ponto de troca entre jogo local e (futuro) jogo em rede. A apresentação só fala com isto.</summary>
public interface IGameSession : IDisposable
{
    int LocalPlayerId { get; }
    IWorldView World { get; }

    /// <summary>Fração [0, 1) entre o tick anterior e o próximo, para interpolar a renderização.</summary>
    float InterpolationAlpha { get; }

    void SetInput(PlayerInput input);
    void Send(ICommand command);
    void Update(double deltaSeconds);
    IReadOnlyList<IGameEvent> DrainEvents();
}
```

`src/Zyon.Core/Sim/LocalSession.cs`:

```csharp
namespace Zyon.Core.Sim;

/// <summary>Roda a Simulation no mesmo processo, com acumulador de tempo para tick fixo.</summary>
public sealed class LocalSession(Simulation simulation, int localPlayerId) : IGameSession
{
    public const int MaxTicksPerUpdate = 5;
    private const double TickSeconds = 1.0 / Simulation.TicksPerSecond;

    private double accumulator;

    public int LocalPlayerId { get; } = localPlayerId;
    public IWorldView World => simulation;
    public float InterpolationAlpha { get; private set; }

    public void SetInput(PlayerInput input) => simulation.SetInput(LocalPlayerId, input);

    public void Send(ICommand command) => simulation.Enqueue(command);

    public void Update(double deltaSeconds)
    {
        accumulator += deltaSeconds;
        int ticks = 0;
        while (accumulator >= TickSeconds && ticks < MaxTicksPerUpdate)
        {
            simulation.Step();
            accumulator -= TickSeconds;
            ticks++;
        }
        if (accumulator >= TickSeconds)
            accumulator = 0;
        InterpolationAlpha = (float)(accumulator / TickSeconds);
    }

    public IReadOnlyList<IGameEvent> DrainEvents() => simulation.DrainEvents();

    public void Dispose() => simulation.Dispose();
}
```

Observação: em `Update_RunsWholeTicks_AndKeepsRemainderAsAlpha`, a sessão e o harness compartilham a mesma `Simulation`. Só a sessão é descartada (`using var _s`); o harness não deve ser descartado de novo, para não destruir o mundo do Arch duas vezes.

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~LocalSessionTests"`
Expected: PASS, 4 testes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): IGameSession e LocalSession com tick fixo e interpolação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Cenários de ponta a ponta e desempenho

**Files:**
- Test: `tests/Zyon.Core.Tests/ScenarioTests.cs`, `tests/Zyon.Core.Tests/PerformanceTests.cs`

- [ ] **Step 1: Escrever os testes**

`tests/Zyon.Core.Tests/ScenarioTests.cs`:

```csharp
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ScenarioTests
{
    private static PlayerInput Use(int x, int y) => new(0f, false, true, x, y);

    [Fact]
    public void MiningThreeBlocks_EndsWithThreeItemsInInventory()
    {
        using var h = new SimHarness();
        h.Idle(5);

        h.Run(40, Use(21, 40));
        h.Run(40, Use(22, 40));
        h.Run(40, Use(23, 40));
        h.Run(60, new PlayerInput(1f, false, false, 0, 0));
        h.Idle(60);

        Assert.Equal(0, h.Tiles.GetBlock(21, 40));
        Assert.Equal(0, h.Tiles.GetBlock(22, 40));
        Assert.Equal(0, h.Tiles.GetBlock(23, 40));
        int dirt = Enumerable.Range(0, h.Inventory.SlotCount)
            .Select(i => h.Inventory[i])
            .Where(s => s.ItemId == h.Registry.ItemId("dirt"))
            .Sum(s => s.Count);
        Assert.Equal(3, dirt);
    }

    [Fact]
    public void PlaceThenMineBack_KeepsItemCount()
    {
        using var h = new SimHarness();
        h.Give("dirt", 1);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Run(1, Use(22, 39));
        Assert.True(h.Inventory[1].IsEmpty);

        h.Send(new SelectHotbarSlot(h.PlayerId, 0));
        h.Run(40, Use(22, 39));
        h.Idle(60);

        Assert.Equal(0, h.Tiles.GetBlock(22, 39));
        Assert.Equal(1, h.Inventory[1].Count);
    }
}
```

`tests/Zyon.Core.Tests/PerformanceTests.cs`:

```csharp
using System.Diagnostics;
using System.Numerics;
using Xunit.Abstractions;
using Zyon.Core.Items;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void Tick_With5000DroppedItems_Under2ms()
    {
        var r = TestData.Registry();
        var world = new TileWorld(420, 80);
        ushort stone = r.BlockId("stone");
        var floors = Enumerable.Range(0, 25).Select(k => 3 + 3 * k).ToArray();   // linhas 3, 6, ..., 75
        foreach (int y in floors)
            for (int x = 0; x < world.Width; x++)
                world.SetBlock(x, y, stone);

        using var sim = new Simulation(r, world);
        sim.SpawnPlayer(new Vector2(2f, 3f - r.Physics.PlayerHeight / 2));
        var dirt = new ItemStack(r.ItemId("dirt"), 1);
        float half = r.Physics.ItemSize / 2;
        foreach (int y in floors)
            for (int i = 0; i < 200; i++)
                sim.SpawnDroppedItem(dirt, new Vector2(5.5f + 2 * i, y - half), Vector2.Zero, 0);

        for (int i = 0; i < 60; i++) sim.Step();   // aquecimento (JIT, assentamento)

        const int measured = 300;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < measured; i++) sim.Step();
        sw.Stop();
        double avgMs = sw.Elapsed.TotalMilliseconds / measured;

        var entities = new List<Zyon.Core.Entities.EntityView>();
        output.WriteLine($"Entidades: {sim.CopyEntities(entities)} | média por tick: {avgMs:F3} ms");
        Assert.Equal(5001, entities.Count);
#if !DEBUG
        Assert.True(avgMs < 2.0, $"tick médio de {avgMs:F3} ms excede 2 ms");
#endif
    }
}
```

- [ ] **Step 2: Rodar os cenários**

Run: `dotnet test --filter "FullyQualifiedName~ScenarioTests"`
Expected: PASS, 2 testes. Se falhar, **não ajuste o teste para passar**: use superpowers:systematic-debugging para achar a causa no Core.

- [ ] **Step 3: Rodar o teste de desempenho em Release**

Run: `dotnet test -c Release --filter "Category=Performance" --logger "console;verbosity=detailed"`
Expected: PASS, com a linha `Entidades: 5001 | média por tick: X ms` e X < 2. Se ficar acima de 2 ms, use superpowers:systematic-debugging com um profiler (`dotnet-trace`) para achar o gargalo antes de otimizar.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test(core): cenários de ponta a ponta e desempenho com 5000 itens" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Verificação final

- [ ] **Step 1: Build limpo sem warnings**

Run: `dotnet build -c Release`
Expected: `0 Aviso(s)` / `0 Warning(s)`, `0 Erro(s)` / `0 Error(s)`.

- [ ] **Step 2: Suíte completa em Debug**

Run: `dotnet test`
Expected: PASS, todos os testes (cerca de 88).

- [ ] **Step 3: Suíte completa em Release (inclui a asserção de desempenho)**

Run: `dotnet test -c Release`
Expected: PASS, todos os testes.

- [ ] **Step 4: Confirmar que o Core não referencia Godot**

Run: `grep -ri "godot" src/ || echo "ok: nenhuma referência"`
Expected: `ok: nenhuma referência`

- [ ] **Step 5: Confirmar a árvore de trabalho limpa**

Run: `git status --short`
Expected: saída vazia.
