# Zyon — Plano 3: Persistência — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Salvar e carregar mundos (`.zworld`, binário com paleta de nomes, RLE e Brotli) e personagens (`.zplayer`, JSON) com versionamento, validação, gravação atômica e fallback para `.bak`.

**Architecture:** Modelos em memória (`WorldSave`, `PlayerSave`) capturam o estado de uma `Simulation` e recriam uma nova. Serializadores (`WorldFile`, `PlayerFile`) leem e escrevem `Stream`s. `SaveFiles` cuida de arquivos: grava em `.tmp`, troca atomicamente mantendo o anterior como `.bak`, e na leitura cai para o `.bak` se o principal estiver ilegível — nunca sobrescreve em caso de erro. A `Simulation` ganha ganchos `internal` de restauração (relógio, itens com idade, jogador com inventário pronto).

**Tech Stack:** .NET 10, System.Text.Json, System.IO.Compression (Brotli), xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-10-15-zyon-fatia-vertical-design.md`, seção 8 (save) e 9 (erros). Pendências incorporadas dos planos 1–2: API de restauração da simulação, validação de slot/id/quantidade ao carregar inventário, paleta de nomes (ids são posicionais), impressão digital do `worldgen.json` no save, `Dirty` como flag exclusiva da persistência.

**Desvio consciente do spec:** o spec lista seed/dimensões/spawn no cabeçalho. Aqui o cabeçalho **não comprimido** tem só `magic` + versão do formato (para detectar a versão antes de descomprimir); todo o resto vai comprimido. Os campos são os mesmos.

---

## Convenções (valem para todas as tarefas)

- Tudo do `CLAUDE.md` continua valendo (coordenadas em tiles, Y para baixo, comentários em português, `TreatWarningsAsErrors`).
- Namespace novo: `Zyon.Core.Persistence`.
- Erros de dados/arquivo viram `DataException` com prefixo do formato (`.zworld: …`, `.zplayer: …`).
- Arquivos binários em little-endian via `BinaryWriter`/`BinaryReader` (UTF-8).
- **Shell:** Bash (Git Bash) a partir de `D:\Projetos\Zyon`.
- **Commits:** toda mensagem termina com o segundo `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. Nunca `--amend`.
- **Branch:** `feat/plano-3-persistencia` (criada pelo controlador).

## Mapa de arquivos

```
src/Zyon.Core/
  Sim/Simulation.cs                          # (mod) SpawnPlayer com inventário, RestoreTick, itens com idade, snapshot
  WorldGen/WorldGenConfigFingerprint.cs      # FNV-1a 64 do worldgen.json normalizado
  WorldGen/WorldGenerator.cs                 # (mod) GeneratedWorld.ConfigFingerprint
  Persistence/
    PlayerSave.cs        # PlayerSave + SavedSlot
    PlayerFile.cs        # .zplayer (JSON)
    WorldSave.cs         # WorldSave + WorldInfo + SavedDroppedItem
    WorldFile.cs         # .zworld (binário)
    SaveFiles.cs         # gravação atômica, .bak, LoadResult, SaveLoadException
tests/Zyon.Core.Tests/
  SimulationRestoreTests.cs  FingerprintTests.cs  PlayerFileTests.cs
  WorldSaveTests.cs  WorldFileTests.cs  SaveFilesTests.cs
  PersistenceScenarioTests.cs  PersistencePerformanceTests.cs
  TestData.cs                # (mod) RegistryWithEdits
```

---

### Task 1: Ganchos de restauração na `Simulation`

**Files:**
- Modify: `src/Zyon.Core/Sim/Simulation.cs`
- Test: `tests/Zyon.Core.Tests/SimulationRestoreTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/SimulationRestoreTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class SimulationRestoreTests
{
    [Fact]
    public void SpawnPlayer_WithInventory_UsesItExactly()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        var inv = new Inventory(r);
        inv.Set(7, new ItemStack(r.ItemId("dirt"), 37));

        int id = sim.SpawnPlayer(new Vector2(20f, 40f - r.Physics.PlayerHeight / 2), inv, selectedSlot: 3);

        var loaded = sim.GetInventory(id)!;
        Assert.True(loaded[0].IsEmpty);   // sem itens iniciais
        Assert.Equal(new ItemStack(r.ItemId("dirt"), 37), loaded[7]);
        Assert.Equal(3, sim.GetSelectedSlot(id));
    }

    [Fact]
    public void SpawnPlayer_WithInventory_InvalidSelectedSlot_Throws()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));

        Assert.Throws<ArgumentOutOfRangeException>(() => sim.SpawnPlayer(new Vector2(20f, 30f), new Inventory(r), 10));
    }

    [Fact]
    public void RestoreTick_SetsClock_AndStepContinuesFromIt()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));

        sim.RestoreTick(5000);
        sim.Step();

        Assert.Equal(5001, sim.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.RestoreTick(-1));
    }

    [Fact]
    public void DroppedItem_RestoredWithAge_DespawnsAccordingly()
    {
        var r = TestData.RegistryWithPhysics("""{"itemDespawnSeconds": 1}""");
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        var dirt = new ItemStack(r.ItemId("dirt"), 1);
        int old = sim.SpawnDroppedItem(dirt, new Vector2(10f, 39.625f), Vector2.Zero, 0, ageTicks: 50);
        int fresh = sim.SpawnDroppedItem(dirt, new Vector2(50f, 39.625f), Vector2.Zero, 0);

        for (int i = 0; i < 11; i++) sim.Step();

        Assert.False(sim.TryGetEntityView(old, out _));
        Assert.True(sim.TryGetEntityView(fresh, out _));
    }

    [Fact]
    public void SnapshotDroppedItems_ReturnsStateOrderedBySpawn()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        sim.SpawnDroppedItem(new ItemStack(r.ItemId("stone"), 3), new Vector2(30f, 39.625f), Vector2.Zero, 0, ageTicks: 7);
        sim.SpawnDroppedItem(new ItemStack(r.ItemId("dirt"), 5), new Vector2(10f, 39.625f), Vector2.Zero, 0, ageTicks: 9);

        var snapshot = sim.SnapshotDroppedItems();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal((new ItemStack(r.ItemId("stone"), 3), new Vector2(30f, 39.625f), 7), snapshot[0]);
        Assert.Equal((new ItemStack(r.ItemId("dirt"), 5), new Vector2(10f, 39.625f), 9), snapshot[1]);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~SimulationRestoreTests"`
Expected: FAIL na compilação — sobrecarga `SpawnPlayer(Vector2, Inventory, int)`, `RestoreTick`, parâmetro `ageTicks` e `SnapshotDroppedItems` não existem.

- [ ] **Step 3: Implementar**

Em `src/Zyon.Core/Sim/Simulation.cs`:

1. Substituir o método `SpawnPlayer(Vector2 position)` inteiro por:

```csharp
    /// <summary>Cria um jogador novo com os itens iniciais do Registry.</summary>
    public int SpawnPlayer(Vector2 position)
    {
        var inventory = new Inventory(Registry);
        foreach (var stack in Registry.StarterItems)
            inventory.Add(stack.ItemId, stack.Count);
        return SpawnPlayer(position, inventory, selectedSlot: 0);
    }

    /// <summary>Cria um jogador com um inventário já montado (ex.: vindo de um save). Não adiciona itens iniciais.</summary>
    internal int SpawnPlayer(Vector2 position, Inventory inventory, int selectedSlot)
    {
        if ((uint)selectedSlot >= Inventory.HotbarSize)
            throw new ArgumentOutOfRangeException(nameof(selectedSlot), $"Slot selecionado {selectedSlot} fora da hotbar");

        var playerSize = new Vector2(Config.PlayerWidth, Config.PlayerHeight);
        int id = nextNetworkId++;
        var entity = Ecs.Create(
            new NetworkId(id),
            new Position(position),
            new PreviousPosition(position),
            new Velocity(Vector2.Zero),
            new Body { Size = playerSize, CanStepUp = true, OnGround = Collider.IsGrounded(position, playerSize) },
            new PlayerState { Facing = 1, SelectedSlot = selectedSlot },
            new InventoryRef(inventory));
        byNetworkId[id] = entity;
        Emit(new EntitySpawned(id, EntityKind.Player, position));
        return id;
    }

    /// <summary>Restaura o relógio ao carregar um mundo. Use antes do primeiro Step.</summary>
    internal void RestoreTick(long tick)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        Tick = tick;
    }

    /// <summary>Itens caídos com o estado necessário para salvar, na ordem em que foram criados.</summary>
    internal List<(ItemStack Stack, Vector2 Position, int AgeTicks)> SnapshotDroppedItems()
    {
        var found = new List<(int Id, ItemStack Stack, Vector2 Position, int AgeTicks)>();
        Ecs.Query(in Queries.Items, (ref NetworkId id, ref Position pos, ref DroppedItem item) =>
            found.Add((id.Value, item.Stack, pos.Value, item.AgeTicks)));
        found.Sort((a, b) => a.Id.CompareTo(b.Id));
        return found.Select(f => (f.Stack, f.Position, f.AgeTicks)).ToList();
    }
```

2. Trocar a assinatura e o corpo de `SpawnDroppedItem` para aceitar a idade:

```csharp
    internal int SpawnDroppedItem(ItemStack stack, Vector2 position, Vector2 velocity, int pickupDelayTicks, int ageTicks = 0)
    {
        int id = nextNetworkId++;
        float size = Config.ItemSize;
        var entity = Ecs.Create(
            new NetworkId(id),
            new Position(position),
            new PreviousPosition(position),
            new Velocity(velocity),
            new Body { Size = new Vector2(size, size) },
            new DroppedItem { Stack = stack, PickupDelayTicks = pickupDelayTicks, AgeTicks = ageTicks });
        byNetworkId[id] = entity;
        Emit(new EntitySpawned(id, EntityKind.DroppedItem, position));
        return id;
    }
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~SimulationRestoreTests"` → PASS, 5 testes. Depois `dotnet test` → todos verdes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): ganchos de restauração na Simulation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Impressão digital da configuração de geração

**Files:**
- Create: `src/Zyon.Core/WorldGen/WorldGenConfigFingerprint.cs`
- Modify: `src/Zyon.Core/WorldGen/WorldGenerator.cs` (`GeneratedWorld` ganha `ConfigFingerprint`)
- Test: `tests/Zyon.Core.Tests/FingerprintTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/FingerprintTests.cs`:

```csharp
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class FingerprintTests
{
    [Fact]
    public void SameConfig_SameFingerprint()
    {
        Assert.Equal(
            WorldGenConfigFingerprint.Compute(TestData.WorldGenConfig()),
            WorldGenConfigFingerprint.Compute(TestData.WorldGenConfig()));
    }

    [Fact]
    public void ChangedParameter_ChangesFingerprint()
    {
        var a = WorldGenConfigFingerprint.Compute(WorldGenConfigLoader.Load("{}"));
        var b = WorldGenConfigFingerprint.Compute(WorldGenConfigLoader.Load("""{"caveThreshold": 0.4}"""));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GeneratedWorld_CarriesTheFingerprint()
    {
        var config = TestData.WorldGenConfig();
        var world = new WorldGenerator(TestData.Registry(), config).Generate(128, 128, 1);

        Assert.Equal(WorldGenConfigFingerprint.Compute(config), world.ConfigFingerprint);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~FingerprintTests"`
Expected: FAIL na compilação — `WorldGenConfigFingerprint` e `ConfigFingerprint` não existem.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/WorldGen/WorldGenConfigFingerprint.cs`:

```csharp
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen;

/// <summary>
/// FNV-1a 64 da configuração normalizada (serializada em JSON). Vai no save para indicar se o mundo foi
/// gerado com outros parâmetros: WorldGenerator.Version não captura mudanças em data/worldgen.json.
/// </summary>
public static class WorldGenConfigFingerprint
{
    public static ulong Compute(WorldGenConfig config)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(config, RegistryLoader.Options);
        ulong h = 14695981039346656037UL;
        foreach (byte b in json)
            h = (h ^ b) * 1099511628211UL;
        return h;
    }
}
```

Em `src/Zyon.Core/WorldGen/WorldGenerator.cs`:

1. trocar a declaração do record por:

```csharp
public sealed record GeneratedWorld(TileWorld Tiles, int Seed, int GeneratorVersion, ulong ConfigFingerprint, Vector2 SpawnFeet)
```

(o corpo do record, com `PlayerSpawnCenter`, não muda);

2. adicionar o campo `private readonly ulong fingerprint;`, atribuído no construtor de 3 argumentos com `fingerprint = WorldGenConfigFingerprint.Compute(config);`;

3. no `return` de `Generate`, passar o campo: `return new GeneratedWorld(world, seed, Version, fingerprint, ctx.SpawnFeet);`.

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~FingerprintTests"` → PASS, 3 testes. Depois `dotnet test` → todos verdes (o hash de referência da geração não muda: só os metadados mudaram).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(worldgen): impressão digital da configuração no mundo gerado" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Personagem — `PlayerSave` e `.zplayer`

**Files:**
- Create: `src/Zyon.Core/Persistence/PlayerSave.cs`, `src/Zyon.Core/Persistence/PlayerFile.cs`
- Test: `tests/Zyon.Core.Tests/PlayerFileTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/PlayerFileTests.cs`:

```csharp
using System.Numerics;
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public class PlayerFileTests
{
    private static PlayerSave RoundTrip(PlayerSave save, Registry registry)
    {
        using var ms = new MemoryStream();
        PlayerFile.Write(ms, save);
        ms.Position = 0;
        return PlayerFile.Read(ms, registry);
    }

    private static PlayerSave ReadJson(string json) =>
        PlayerFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), TestData.Registry());

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var save = new PlayerSave("Leo", 2, [new SavedSlot(0, "basic_pickaxe", 1), new SavedSlot(15, "dirt", 37)]);

        var loaded = RoundTrip(save, TestData.Registry());

        Assert.Equal("Leo", loaded.Name);
        Assert.Equal(2, loaded.SelectedSlot);
        Assert.Equal(save.Slots, loaded.Slots);
    }

    [Fact]
    public void New_HasStarterItems()
    {
        var save = PlayerSave.New("Ana", TestData.Registry());

        Assert.Equal(0, save.SelectedSlot);
        Assert.Contains(new SavedSlot(0, "basic_pickaxe", 1), save.Slots);
    }

    [Fact]
    public void CaptureAndSpawnInto_RestoreInventoryAndSelection()
    {
        using var h = new SimHarness();
        h.Give("dirt", 12);
        h.Send(new Zyon.Core.Sim.SelectHotbarSlot(h.PlayerId, 1));

        var save = PlayerSave.Capture(h.Sim, h.PlayerId, "Leo");
        using var sim2 = new Zyon.Core.Sim.Simulation(h.Registry, TestWorlds.Flat(h.Registry, 64, 64, 40));
        int id2 = save.SpawnInto(sim2, new Vector2(20f, h.StandingY));

        var inv = sim2.GetInventory(id2)!;
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), inv[0].ItemId);
        Assert.Equal(new ItemStack(h.Registry.ItemId("dirt"), 12), inv[1]);
        Assert.True(inv[2].IsEmpty);
        Assert.Equal(1, sim2.GetSelectedSlot(id2));
    }

    [Fact]
    public void Write_ProducesReadableJsonWithItemNames()
    {
        using var ms = new MemoryStream();
        PlayerFile.Write(ms, new PlayerSave("Leo", 0, [new SavedSlot(3, "stone", 4)]));

        string json = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("\"formatVersion\": 1", json);
        Assert.Contains("\"stone\"", json);
    }

    [Theory]
    [InlineData("""{"formatVersion": 2, "name": "L", "selectedSlot": 0, "inventory": []}""", "versão mais nova")]
    [InlineData("""{"formatVersion": 0, "name": "L", "selectedSlot": 0, "inventory": []}""", "não suportada")]
    [InlineData("""{"name": "L", "selectedSlot": 0, "inventory": []}""", "formatVersion")]
    [InlineData("""{"formatVersion": 1, "name": " ", "selectedSlot": 0, "inventory": []}""", "name")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 10, "inventory": []}""", "selectedSlot")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0}""", "inventory")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [null]}""", "inventory[0]")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 50, "item": "dirt", "count": 1}]}""", "slot 50")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "dirt", "count": 1}, {"slot": 1, "item": "dirt", "count": 1}]}""", "repetido")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "mithril", "count": 1}]}""", "mithril")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "dirt", "count": 0}]}""", "quantidade")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "basic_pickaxe", "count": 2}]}""", "quantidade")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [], "extra": 1}""", ".zplayer")]
    [InlineData("""{"formatVersion": 1""", "JSON inválido")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ReadJson(json));

        Assert.Contains(".zplayer", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~PlayerFileTests"`
Expected: FAIL na compilação — `Zyon.Core.Persistence` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Persistence/PlayerSave.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Persistence;

/// <summary>Um slot ocupado do inventário salvo, com o item referenciado pelo nome.</summary>
public sealed record SavedSlot(int Slot, string Item, int Count);

/// <summary>Personagem independente de mundo: nome, slot selecionado e inventário.</summary>
public sealed record PlayerSave(string Name, int SelectedSlot, IReadOnlyList<SavedSlot> Slots)
{
    /// <summary>Personagem novo com os itens iniciais do Registry.</summary>
    public static PlayerSave New(string name, Registry registry)
    {
        var inventory = new Inventory(registry);
        foreach (var stack in registry.StarterItems)
            inventory.Add(stack.ItemId, stack.Count);
        return new PlayerSave(name, 0, ToSlots(inventory, registry));
    }

    public static PlayerSave Capture(Simulation sim, int playerId, string name)
    {
        var inventory = sim.GetInventory(playerId)
                        ?? throw new ArgumentException($"Jogador {playerId} não existe", nameof(playerId));
        return new PlayerSave(name, sim.GetSelectedSlot(playerId), ToSlots(inventory, sim.Registry));
    }

    /// <summary>Cria o jogador na simulação com este inventário (sem itens iniciais). Os nomes já foram validados na leitura.</summary>
    public int SpawnInto(Simulation sim, Vector2 position)
    {
        var inventory = new Inventory(sim.Registry);
        foreach (var slot in Slots)
            inventory.Set(slot.Slot, new ItemStack(sim.Registry.ItemId(slot.Item), slot.Count));
        return sim.SpawnPlayer(position, inventory, SelectedSlot);
    }

    private static List<SavedSlot> ToSlots(IReadOnlyInventory inventory, Registry registry)
    {
        var slots = new List<SavedSlot>();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var stack = inventory[i];
            if (!stack.IsEmpty)
                slots.Add(new SavedSlot(i, registry.Item(stack.ItemId).Name, stack.Count));
        }
        return slots;
    }
}
```

`src/Zyon.Core/Persistence/PlayerFile.cs`:

```csharp
using System.Text.Json;
using Zyon.Core.Data;
using Zyon.Core.Items;

namespace Zyon.Core.Persistence;

/// <summary>Formato .zplayer: JSON legível com itens referenciados pelo nome.</summary>
public static class PlayerFile
{
    public const int FormatVersion = 1;
    public const string Extension = ".zplayer";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Write(Stream stream, PlayerSave save)
    {
        var dto = new PlayerDto
        {
            FormatVersion = FormatVersion,
            Name = save.Name,
            SelectedSlot = save.SelectedSlot,
            Inventory = save.Slots.Select(s => (SlotDto?)new SlotDto { Slot = s.Slot, Item = s.Item, Count = s.Count }).ToList(),
        };
        JsonSerializer.Serialize(stream, dto, WriteOptions);
    }

    /// <summary>Lê e valida contra o Registry atual (itens inexistentes, slots e quantidades inválidos falham).</summary>
    public static PlayerSave Read(Stream stream, Registry registry)
    {
        PlayerDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<PlayerDto>(stream, RegistryLoader.Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        int version = dto.FormatVersion ?? throw Fail("campo 'formatVersion' ausente");
        if (version > FormatVersion)
            throw Fail($"feito por uma versão mais nova do jogo (formato {version}, suportado até {FormatVersion})");
        if (version < 1)
            throw Fail($"versão de formato {version} não suportada");
        // Migrações futuras entram aqui, em sequência: v1 → v2 → ... até FormatVersion.

        if (string.IsNullOrWhiteSpace(dto.Name)) throw Fail("campo 'name' ausente");
        int selected = dto.SelectedSlot ?? throw Fail("campo 'selectedSlot' ausente");
        if ((uint)selected >= Inventory.HotbarSize)
            throw Fail($"'selectedSlot' {selected} fora da hotbar (0..{Inventory.HotbarSize - 1})");

        var entries = dto.Inventory ?? throw Fail("campo 'inventory' ausente");
        var seen = new HashSet<int>();
        var slots = new List<SavedSlot>();
        for (int i = 0; i < entries.Count; i++)
        {
            var ctx = $"inventory[{i}]";
            var entry = entries[i] ?? throw Fail($"{ctx}: entrada nula");
            int slot = entry.Slot ?? throw Fail($"{ctx}: campo 'slot' ausente");
            if (!Inventory.IsValidSlot(slot)) throw Fail($"{ctx}: slot {slot} inválido");
            if (!seen.Add(slot)) throw Fail($"{ctx}: slot {slot} repetido");
            string item = entry.Item ?? throw Fail($"{ctx}: campo 'item' ausente");
            if (!registry.TryGetItemId(item, out var itemId))
                throw Fail($"{ctx}: item '{item}' não existe mais nos dados do jogo");
            int count = entry.Count ?? throw Fail($"{ctx}: campo 'count' ausente");
            int max = registry.Item(itemId).MaxStack;
            if (count < 1 || count > max)
                throw Fail($"{ctx}: quantidade {count} fora de 1..{max}");
            slots.Add(new SavedSlot(slot, item, count));
        }

        return new PlayerSave(dto.Name!, selected, slots);
    }

    private static DataException Fail(string message) => new($"{Extension}: {message}");

    private sealed class PlayerDto
    {
        public int? FormatVersion { get; set; }
        public string? Name { get; set; }
        public int? SelectedSlot { get; set; }
        public List<SlotDto?>? Inventory { get; set; }
    }

    private sealed class SlotDto
    {
        public int? Slot { get; set; }
        public string? Item { get; set; }
        public int? Count { get; set; }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~PlayerFileTests"` → PASS, 18 testes (4 Facts + 14 casos). Depois `dotnet test` → todos verdes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(persistence): personagem salvo em .zplayer com validação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Mundo em memória — `WorldSave`

**Files:**
- Create: `src/Zyon.Core/Persistence/WorldSave.cs`
- Test: `tests/Zyon.Core.Tests/WorldSaveTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/WorldSaveTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Persistence;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldSaveTests
{
    private static readonly WorldInfo Info = new(Seed: 9, GeneratorVersion: 1, ConfigFingerprint: 42UL, SpawnFeet: new Vector2(20f, 40f));

    [Fact]
    public void Capture_RecordsTilesTickItemsAndPlayers()
    {
        using var h = new SimHarness();
        h.Sim.SpawnDroppedItem(new ItemStack(h.Registry.ItemId("stone"), 4), new Vector2(50.5f, 39.625f), Vector2.Zero, 0, ageTicks: 30);
        h.Idle(10);

        var save = WorldSave.Capture(h.Sim, Info, new Dictionary<string, int> { ["Leo"] = h.PlayerId });

        Assert.Same(h.Tiles, save.Tiles);
        Assert.Equal(10, save.Tick);
        var item = Assert.Single(save.DroppedItems);
        Assert.Equal("stone", item.Item);
        Assert.Equal(4, item.Count);
        Assert.Equal(40, item.AgeTicks);
        Assert.Equal(h.PlayerPosition, save.PlayerPositions["Leo"]);
    }

    [Fact]
    public void CreateSimulation_RestoresTickAndItems()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 777,
            [new SavedDroppedItem("dirt", 3, new Vector2(30.5f, 39.625f), 12)],
            new Dictionary<string, Vector2>());

        using var sim = save.CreateSimulation(r);

        Assert.Equal(777, sim.Tick);
        var restored = Assert.Single(sim.SnapshotDroppedItems());
        Assert.Equal(new ItemStack(r.ItemId("dirt"), 3), restored.Stack);
        Assert.Equal(new Vector2(30.5f, 39.625f), restored.Position);
        Assert.Equal(12, restored.AgeTicks);
    }

    [Fact]
    public void CreateSimulation_UnknownItem_Throws()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0,
            [new SavedDroppedItem("mithril", 1, new Vector2(30.5f, 39.625f), 0)],
            new Dictionary<string, Vector2>());

        var ex = Assert.Throws<DataException>(() => save.CreateSimulation(r));
        Assert.Contains("mithril", ex.Message);
    }

    [Fact]
    public void PlayerPosition_UsesSavedPosition_OrFallsBackToSpawn()
    {
        var physics = TestData.Registry().Physics;
        var save = new WorldSave(Info, new Zyon.Core.Tiles.TileWorld(64, 64), 0, [],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(5f, 6f) });

        Assert.Equal(new Vector2(5f, 6f), save.PlayerPosition("Leo", physics));
        Assert.Equal(new Vector2(20f, 40f - physics.PlayerHeight / 2), save.PlayerPosition("Ana", physics));
    }

    [Fact]
    public void FromGenerated_StartsAtTickZeroWithWorldInfo()
    {
        var gen = new WorldGenerator(TestData.Registry(), TestData.WorldGenConfig()).Generate(128, 128, 5);

        var save = WorldSave.FromGenerated(gen);

        Assert.Same(gen.Tiles, save.Tiles);
        Assert.Equal(0, save.Tick);
        Assert.Equal(new WorldInfo(5, gen.GeneratorVersion, gen.ConfigFingerprint, gen.SpawnFeet), save.Info);
        Assert.Empty(save.DroppedItems);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldSaveTests"`
Expected: FAIL na compilação — `WorldSave`, `WorldInfo`, `SavedDroppedItem` não existem.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Persistence/WorldSave.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Logging;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Persistence;

/// <summary>Metadados de origem do mundo, gravados no save.</summary>
public sealed record WorldInfo(int Seed, int GeneratorVersion, ulong ConfigFingerprint, Vector2 SpawnFeet)
{
    public static WorldInfo From(GeneratedWorld world) =>
        new(world.Seed, world.GeneratorVersion, world.ConfigFingerprint, world.SpawnFeet);
}

/// <summary>Item caído salvo, referenciado pelo nome.</summary>
public sealed record SavedDroppedItem(string Item, int Count, Vector2 Position, int AgeTicks);

/// <summary>Estado de um mundo em memória: tiles, relógio, itens caídos e posição de cada personagem que já esteve nele.</summary>
public sealed class WorldSave(
    WorldInfo info,
    TileWorld tiles,
    long tick,
    IReadOnlyList<SavedDroppedItem> droppedItems,
    IReadOnlyDictionary<string, Vector2> playerPositions)
{
    public WorldInfo Info { get; } = info;
    public TileWorld Tiles { get; } = tiles;
    public long Tick { get; } = tick;
    public IReadOnlyList<SavedDroppedItem> DroppedItems { get; } = droppedItems;
    public IReadOnlyDictionary<string, Vector2> PlayerPositions { get; } = playerPositions;

    public static WorldSave FromGenerated(GeneratedWorld world) =>
        new(WorldInfo.From(world), world.Tiles, 0, [], new Dictionary<string, Vector2>());

    /// <summary>Captura o estado atual. Os tiles não são copiados: grave antes do próximo Step.</summary>
    public static WorldSave Capture(Simulation sim, WorldInfo info, IReadOnlyDictionary<string, int> playerIdsByName)
    {
        var items = sim.SnapshotDroppedItems()
            .Select(d => new SavedDroppedItem(sim.Registry.Item(d.Stack.ItemId).Name, d.Stack.Count, d.Position, d.AgeTicks))
            .ToList();
        var positions = new Dictionary<string, Vector2>();
        foreach (var (name, id) in playerIdsByName)
            if (sim.TryGetEntityView(id, out var view))
                positions[name] = view.Position;
        return new WorldSave(info, sim.Tiles, sim.Tick, items, positions);
    }

    /// <summary>Centro do jogador ao entrar: a posição salva neste mundo, ou o ponto de spawn.</summary>
    public Vector2 PlayerPosition(string playerName, PhysicsConfig physics) =>
        PlayerPositions.TryGetValue(playerName, out var saved)
            ? saved
            : Info.SpawnFeet - new Vector2(0f, physics.PlayerHeight / 2);

    /// <summary>Cria a simulação com tiles, relógio e itens caídos. Jogadores entram depois via PlayerSave.SpawnInto.</summary>
    public Simulation CreateSimulation(Registry registry, ILog? log = null)
    {
        var sim = new Simulation(registry, Tiles, log);
        try
        {
            sim.RestoreTick(Tick);
            foreach (var item in DroppedItems)
            {
                if (!registry.TryGetItemId(item.Item, out var id))
                    throw new DataException($"save do mundo: item caído '{item.Item}' não existe mais nos dados do jogo");
                sim.SpawnDroppedItem(new ItemStack(id, item.Count), item.Position, Vector2.Zero, 0, item.AgeTicks);
            }
            return sim;
        }
        catch
        {
            sim.Dispose();
            throw;
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldSaveTests"` → PASS, 5 testes. Depois `dotnet test` → todos verdes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(persistence): WorldSave captura e recria a simulação" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Formato binário `.zworld`

**Files:**
- Create: `src/Zyon.Core/Persistence/WorldFile.cs`
- Modify: `tests/Zyon.Core.Tests/TestData.cs` (+ `RegistryWithEdits`)
- Test: `tests/Zyon.Core.Tests/WorldFileTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Em `tests/Zyon.Core.Tests/TestData.cs`, adicionar `using System.Text.Json.Nodes;` e, dentro da classe:

```csharp
    /// <summary>Registry real com blocks.json/items.json editados (ex.: reordenar ou remover entradas).</summary>
    public static Registry RegistryWithEdits(Action<JsonArray>? editBlocks = null, Action<JsonArray>? editItems = null)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        var blocks = JsonNode.Parse(Read(RegistryLoader.BlocksFile))!.AsArray();
        var items = JsonNode.Parse(Read(RegistryLoader.ItemsFile))!.AsArray();
        editBlocks?.Invoke(blocks);
        editItems?.Invoke(items);
        return RegistryLoader.Load(blocks.ToJsonString(), Read(RegistryLoader.WallsFile), items.ToJsonString(),
            Read(RegistryLoader.PhysicsFile), Read(RegistryLoader.PlayerFile));
    }

    public static void RemoveById(JsonArray array, string id)
    {
        var node = array.First(n => n!["id"]!.GetValue<string>() == id);
        array.Remove(node);
    }
```

`tests/Zyon.Core.Tests/WorldFileTests.cs`:

```csharp
using System.Numerics;
using System.Text.Json.Nodes;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldFileTests
{
    private static readonly WorldInfo Info = new(3, 1, 0xABCDEFUL, new Vector2(257f, 75f));

    private static byte[] Write(WorldSave save, Registry registry)
    {
        using var ms = new MemoryStream();
        WorldFile.Write(ms, save, registry);
        return ms.ToArray();
    }

    private static WorldSave Read(byte[] bytes, Registry registry) => WorldFile.Read(new MemoryStream(bytes), registry);

    private static WorldSave Sample(Registry r)
    {
        var tiles = new WorldGenerator(r, TestData.WorldGenConfig()).Generate(512, 256, 3).Tiles;
        return new WorldSave(Info, tiles, 1234,
            [
                new SavedDroppedItem("dirt", 5, new Vector2(10.5f, 20.25f), 30),
                new SavedDroppedItem("ore_b", 1, new Vector2(400.125f, 200f), 0),
            ],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(100.5f, 60.625f), ["Ana"] = new Vector2(1f, 2f) });
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var r = TestData.Registry();
        var save = Sample(r);

        var loaded = Read(Write(save, r), r);

        Assert.Equal(save.Info, loaded.Info);
        Assert.Equal(save.Tick, loaded.Tick);
        Assert.Equal(TileWorldHash.Compute(save.Tiles), TileWorldHash.Compute(loaded.Tiles));
        Assert.Equal(save.DroppedItems, loaded.DroppedItems);
        Assert.Equal(save.PlayerPositions.OrderBy(p => p.Key), loaded.PlayerPositions.OrderBy(p => p.Key));
    }

    [Fact]
    public void Write_ClearsDirty_AndLoadedWorldIsClean()
    {
        var r = TestData.Registry();
        var save = Sample(r);
        save.Tiles.SetBlock(5, 5, r.BlockId("dirt"));

        var bytes = Write(save, r);

        Assert.All(save.Tiles.Chunks, c => Assert.False(c.Dirty));
        Assert.All(Read(bytes, r).Tiles.Chunks, c => Assert.False(c.Dirty));
    }

    [Fact]
    public void FileStartsWithMagicAndVersion()
    {
        var r = TestData.Registry();

        var bytes = Write(Sample(r), r);

        Assert.Equal("ZYONWLD"u8.ToArray(), bytes[..7]);
        Assert.Equal(WorldFile.FormatVersion, BitConverter.ToInt32(bytes, 7));
    }

    [Fact]
    public void Palette_RemapsIdsWhenDataOrderChanges()
    {
        var original = TestData.Registry();
        var reordered = TestData.RegistryWithEdits(editBlocks: blocks =>
        {
            var copy = blocks.Select(n => n!.DeepClone()).Reverse().ToList();
            blocks.Clear();
            foreach (var n in copy) blocks.Add(n);
        });
        var save = Sample(original);

        var loaded = Read(Write(save, original), reordered);

        for (int y = 0; y < save.Tiles.Height; y += 3)
        for (int x = 0; x < save.Tiles.Width; x += 3)
            Assert.Equal(
                original.Block(save.Tiles.GetBlock(x, y)).Name,
                reordered.Block(loaded.Tiles.GetBlock(x, y)).Name);
    }

    [Fact]
    public void RemovedUnusedBlock_StillLoads()
    {
        var r = TestData.Registry();
        var flat = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0, [], new Dictionary<string, Vector2>());
        var withoutOreB = TestData.RegistryWithEdits(
            b => TestData.RemoveById(b, "ore_b"),
            i => TestData.RemoveById(i, "ore_b"));

        var loaded = Read(Write(flat, r), withoutOreB);

        Assert.Equal(withoutOreB.BlockId("dirt"), loaded.Tiles.GetBlock(0, 40));
    }

    [Fact]
    public void RemovedUsedBlock_Throws()
    {
        var r = TestData.Registry();
        var tiles = TestWorlds.Flat(r, 64, 64, 40);
        tiles.SetBlock(3, 50, r.BlockId("ore_b"));
        var save = new WorldSave(Info, tiles, 0, [], new Dictionary<string, Vector2>());
        var withoutOreB = TestData.RegistryWithEdits(
            b => TestData.RemoveById(b, "ore_b"),
            i => TestData.RemoveById(i, "ore_b"));

        var ex = Assert.Throws<DataException>(() => Read(Write(save, r), withoutOreB));
        Assert.Contains(".zworld", ex.Message);
        Assert.Contains("ore_b", ex.Message);
    }

    [Fact]
    public void BadMagic_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);
        bytes[0] = (byte)'X';

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("não é um arquivo de mundo", ex.Message);
    }

    [Fact]
    public void FutureVersion_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);
        BitConverter.GetBytes(99).CopyTo(bytes, 7);

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("versão mais nova", ex.Message);
    }

    [Fact]
    public void Truncated_ThrowsDataException()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);

        var ex = Assert.Throws<DataException>(() => Read(bytes[..(bytes.Length / 2)], r));
        Assert.Contains("corrompido", ex.Message);
    }

    [Fact]
    public void FlatWorld_CompressesWell()
    {
        var r = TestData.Registry();
        var flat = new WorldSave(Info, TestWorlds.Flat(r, 512, 256, 100), 0, [], new Dictionary<string, Vector2>());

        Assert.True(Write(flat, r).Length < 4096, "RLE + Brotli deveria deixar um mundo plano minúsculo");
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~WorldFileTests"`
Expected: FAIL na compilação — `WorldFile` não existe.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Persistence/WorldFile.cs`:

```csharp
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Persistence;

/// <summary>
/// Formato .zworld. Sem compressão: magic "ZYONWLD" + versão do formato (int32).
/// Comprimido com Brotli: versão do gerador, seed, impressão digital da config, largura, altura, spawn, tick,
/// paleta de blocos e de paredes (só os ids presentes), chunks em RLE, itens caídos e posições dos jogadores.
/// </summary>
public static class WorldFile
{
    public const int FormatVersion = 1;
    public const string Extension = ".zworld";
    private const int MaxDimension = 100_000;

    private static ReadOnlySpan<byte> Magic => "ZYONWLD"u8;

    public static void Write(Stream stream, WorldSave save, Registry registry)
    {
        stream.Write(Magic);
        using (var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            header.Write(FormatVersion);

        using (var brotli = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var w = new BinaryWriter(brotli, Encoding.UTF8, leaveOpen: true))
        {
            var info = save.Info;
            var tiles = save.Tiles;
            w.Write(info.GeneratorVersion);
            w.Write(info.Seed);
            w.Write(info.ConfigFingerprint);
            w.Write(tiles.Width);
            w.Write(tiles.Height);
            w.Write(info.SpawnFeet.X);
            w.Write(info.SpawnFeet.Y);
            w.Write(save.Tick);

            WritePalette(w, UsedIds(tiles, c => c.Blocks), id => registry.Block(id).Name);
            WritePalette(w, UsedIds(tiles, c => c.Walls), id => registry.Wall(id).Name);
            foreach (var chunk in tiles.Chunks)
            {
                WriteRle(w, chunk.Blocks);
                WriteRle(w, chunk.Walls);
            }

            w.Write(save.DroppedItems.Count);
            foreach (var item in save.DroppedItems)
            {
                w.Write(item.Item);
                w.Write(item.Count);
                w.Write(item.Position.X);
                w.Write(item.Position.Y);
                w.Write(item.AgeTicks);
            }

            w.Write(save.PlayerPositions.Count);
            foreach (var (name, position) in save.PlayerPositions.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                w.Write(name);
                w.Write(position.X);
                w.Write(position.Y);
            }
        }

        foreach (var chunk in save.Tiles.Chunks)
            chunk.Dirty = false;
    }

    public static WorldSave Read(Stream stream, Registry registry)
    {
        try
        {
            return ReadCore(stream, registry);
        }
        catch (EndOfStreamException)
        {
            throw Corrupt("fim inesperado do arquivo");
        }
        catch (InvalidDataException ex)
        {
            throw Corrupt(ex.Message);
        }
    }

    private static WorldSave ReadCore(Stream stream, Registry registry)
    {
        Span<byte> magic = stackalloc byte[7];
        stream.ReadExactly(magic);
        if (!magic.SequenceEqual(Magic))
            throw Fail("não é um arquivo de mundo do Zyon");

        using var header = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        int version = header.ReadInt32();
        if (version > FormatVersion)
            throw Fail($"feito por uma versão mais nova do jogo (formato {version}, suportado até {FormatVersion})");
        if (version < 1)
            throw Fail($"versão de formato {version} não suportada");
        // Migrações futuras: ao criar o formato 2, mantenha a leitura do 1 e converta para o 2 aqui.

        using var brotli = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
        using var r = new BinaryReader(brotli, Encoding.UTF8, leaveOpen: true);

        int generatorVersion = r.ReadInt32();
        int seed = r.ReadInt32();
        ulong fingerprint = r.ReadUInt64();
        int width = r.ReadInt32();
        int height = r.ReadInt32();
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            throw Corrupt($"dimensões inválidas {width}×{height}");
        var spawn = new Vector2(r.ReadSingle(), r.ReadSingle());
        long tick = r.ReadInt64();
        if (tick < 0) throw Corrupt("tick negativo");

        var blockRemap = ReadPalette(r, name =>
            registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' do save não existe mais nos dados do jogo"));
        var wallRemap = ReadPalette(r, name =>
            registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' do save não existe mais nos dados do jogo"));

        var tiles = new TileWorld(width, height);
        foreach (var chunk in tiles.Chunks)
        {
            ReadRle(r, chunk.Blocks, blockRemap, "bloco");
            ReadRle(r, chunk.Walls, wallRemap, "parede");
            chunk.Dirty = false;
        }

        int itemCount = r.ReadInt32();
        if (itemCount < 0) throw Corrupt("contagem de itens negativa");
        var items = new List<SavedDroppedItem>(itemCount);
        for (int i = 0; i < itemCount; i++)
            items.Add(new SavedDroppedItem(r.ReadString(), r.ReadInt32(), new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadInt32()));

        int playerCount = r.ReadInt32();
        if (playerCount < 0) throw Corrupt("contagem de jogadores negativa");
        var positions = new Dictionary<string, Vector2>(playerCount);
        for (int i = 0; i < playerCount; i++)
            positions[r.ReadString()] = new Vector2(r.ReadSingle(), r.ReadSingle());

        return new WorldSave(new WorldInfo(seed, generatorVersion, fingerprint, spawn), tiles, tick, items, positions);
    }

    private static List<ushort> UsedIds(TileWorld tiles, Func<Chunk, ushort[]> layer)
    {
        var used = new bool[ushort.MaxValue + 1];
        foreach (var chunk in tiles.Chunks)
            foreach (ushort id in layer(chunk))
                used[id] = true;
        var ids = new List<ushort>();
        for (int id = 1; id < used.Length; id++)
            if (used[id]) ids.Add((ushort)id);
        return ids;
    }

    private static void WritePalette(BinaryWriter w, List<ushort> ids, Func<ushort, string> name)
    {
        w.Write(ids.Count);
        foreach (ushort id in ids)
        {
            w.Write(id);
            w.Write(name(id));
        }
    }

    /// <summary>Tabela id-do-save → id-atual (0 = ausente da paleta).</summary>
    private static ushort[] ReadPalette(BinaryReader r, Func<string, ushort> resolve)
    {
        int count = r.ReadInt32();
        if (count < 0 || count > ushort.MaxValue) throw Corrupt("paleta inválida");
        var remap = new ushort[ushort.MaxValue + 1];
        for (int i = 0; i < count; i++)
        {
            ushort saved = r.ReadUInt16();
            string name = r.ReadString();
            if (saved == 0) throw Corrupt("id 0 na paleta");
            remap[saved] = resolve(name);
        }
        return remap;
    }

    private static void WriteRle(BinaryWriter w, ushort[] data)
    {
        int i = 0;
        while (i < data.Length)
        {
            ushort value = data[i];
            int run = 1;
            while (i + run < data.Length && data[i + run] == value) run++;
            w.Write(value);
            w.Write((ushort)run);
            i += run;
        }
    }

    private static void ReadRle(BinaryReader r, ushort[] target, ushort[] remap, string kind)
    {
        int i = 0;
        while (i < target.Length)
        {
            ushort saved = r.ReadUInt16();
            int run = r.ReadUInt16();
            if (run == 0 || i + run > target.Length) throw Corrupt("sequência RLE inválida");
            ushort id = 0;
            if (saved != 0)
            {
                id = remap[saved];
                if (id == 0) throw Corrupt($"{kind} {saved} ausente da paleta");
            }
            Array.Fill(target, id, i, run);
            i += run;
        }
    }

    private static DataException Fail(string message) => new($"{Extension}: {message}");

    private static DataException Corrupt(string message) => Fail($"arquivo corrompido — {message}");
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~WorldFileTests"` → PASS, 10 testes. Depois `dotnet test` → todos verdes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(persistence): formato .zworld com paleta, RLE e Brotli" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Arquivos — gravação atômica e `.bak`

**Files:**
- Create: `src/Zyon.Core/Persistence/SaveFiles.cs`
- Test: `tests/Zyon.Core.Tests/SaveFilesTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

`tests/Zyon.Core.Tests/SaveFilesTests.cs`:

```csharp
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public sealed class SaveFilesTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-saves-" + Guid.NewGuid());
    private string SavePath => Path.Combine(dir, "mundo.zworld");

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static void WriteText(Stream s, string text) => s.Write(Encoding.UTF8.GetBytes(text));

    /// <summary>Leitor de teste: falha com DataException se o conteúdo for "corrompido".</summary>
    private static string ReadText(Stream s)
    {
        string text = new StreamReader(s).ReadToEnd();
        return text == "corrompido" ? throw new DataException("teste: conteúdo corrompido") : text;
    }

    [Fact]
    public void WriteAtomic_CreatesFileAndDirectory_LeavesNoTemp()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + SaveFiles.TempSuffix));
        Assert.False(File.Exists(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void SecondWrite_KeepsPreviousAsBackup()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v2"));

        Assert.Equal("v2", File.ReadAllText(SavePath));
        Assert.Equal("v1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void FailingWriter_LeavesOriginalUntouched()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Throws<InvalidOperationException>(() => SaveFiles.WriteAtomic(SavePath, s =>
        {
            WriteText(s, "meio");
            throw new InvalidOperationException("falha no meio da gravação");
        }));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + SaveFiles.TempSuffix));
    }

    [Fact]
    public void ReadWithBackup_ReadsMainFile()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("v1", result.Value);
        Assert.False(result.FromBackup);
    }

    [Fact]
    public void CorruptedMain_FallsBackToBackup()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "corrompido"));

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("v1", result.Value);
        Assert.True(result.FromBackup);
    }

    [Fact]
    public void BothCorrupted_ThrowsAndNeverTouchesFiles()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(SavePath, "corrompido");
        File.WriteAllText(SavePath + SaveFiles.BackupSuffix, "corrompido");

        var ex = Assert.Throws<SaveLoadException>(() => SaveFiles.ReadWithBackup(SavePath, ReadText));

        Assert.Equal(SavePath, ex.FilePath);
        Assert.NotNull(ex.BackupError);
        Assert.Equal("corrompido", File.ReadAllText(SavePath));
        Assert.Equal("corrompido", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void MissingFileWithoutBackup_Throws()
    {
        var ex = Assert.Throws<SaveLoadException>(() => SaveFiles.ReadWithBackup(SavePath, ReadText));

        Assert.IsAssignableFrom<IOException>(ex.InnerException);
        Assert.Null(ex.BackupError);
    }
}
```

- [ ] **Step 2: Rodar e confirmar a falha**

Run: `dotnet test --filter "FullyQualifiedName~SaveFilesTests"`
Expected: FAIL na compilação — `SaveFiles` e `SaveLoadException` não existem.

- [ ] **Step 3: Implementar**

`src/Zyon.Core/Persistence/SaveFiles.cs`:

```csharp
using Zyon.Core.Data;

namespace Zyon.Core.Persistence;

public readonly record struct LoadResult<T>(T Value, bool FromBackup);

/// <summary>O save principal e o backup estão ilegíveis. Nenhum arquivo foi alterado.</summary>
public sealed class SaveLoadException(string filePath, Exception primaryError, Exception? backupError)
    : Exception(
        $"Não foi possível carregar '{filePath}': {primaryError.Message}"
        + (backupError is null ? " (sem backup disponível)" : $"; o backup também falhou: {backupError.Message}"),
        primaryError)
{
    public string FilePath { get; } = filePath;
    public Exception? BackupError { get; } = backupError;
}

/// <summary>Gravação atômica com backup e leitura com fallback. Caminhos são decididos por quem chama.</summary>
public static class SaveFiles
{
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";

    /// <summary>Grava em "arquivo.tmp" e só então troca: o arquivo anterior vira "arquivo.bak". Uma falha no meio nunca corrompe o save atual.</summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
            Directory.CreateDirectory(directory);

        string temp = path + TempSuffix;
        try
        {
            using var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None);
            write(file);
            file.Flush(flushToDisk: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        if (File.Exists(path))
            File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
        else
            File.Move(temp, path);
    }

    /// <summary>Lê o arquivo; se falhar, tenta o .bak. Se ambos falharem, lança SaveLoadException sem alterar nada.</summary>
    public static LoadResult<T> ReadWithBackup<T>(string path, Func<Stream, T> read)
    {
        Exception primaryError;
        try
        {
            return new LoadResult<T>(ReadFile(path, read), FromBackup: false);
        }
        catch (Exception ex) when (IsLoadError(ex))
        {
            primaryError = ex;
        }

        string backup = path + BackupSuffix;
        if (!File.Exists(backup))
            throw new SaveLoadException(path, primaryError, null);
        try
        {
            return new LoadResult<T>(ReadFile(backup, read), FromBackup: true);
        }
        catch (Exception ex) when (IsLoadError(ex))
        {
            throw new SaveLoadException(path, primaryError, ex);
        }
    }

    private static T ReadFile<T>(string path, Func<Stream, T> read)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return read(file);
    }

    private static bool IsLoadError(Exception ex) => ex is DataException or IOException or UnauthorizedAccessException;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `dotnet test --filter "FullyQualifiedName~SaveFilesTests"` → PASS, 7 testes. Depois `dotnet test` → todos verdes.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(persistence): gravação atômica com backup e leitura com fallback" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Cenário de ponta a ponta, desempenho e documentação

**Files:**
- Test: `tests/Zyon.Core.Tests/PersistenceScenarioTests.cs`, `tests/Zyon.Core.Tests/PersistencePerformanceTests.cs`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Escrever os testes**

`tests/Zyon.Core.Tests/PersistenceScenarioTests.cs`:

```csharp
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public sealed class PersistenceScenarioTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-e2e-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void MineSaveQuitLoad_RestoresWorldAndCharacter()
    {
        var registry = TestData.Registry();
        var physics = registry.Physics;
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(800, 400, 5);
        var world = WorldSave.FromGenerated(generated);
        var character = PlayerSave.New("Leo", registry);
        string worldPath = Path.Combine(dir, "mundo" + WorldFile.Extension);
        string playerPath = Path.Combine(dir, "Leo" + PlayerFile.Extension);

        int targetX = (int)generated.SpawnFeet.X + 1;
        int targetY = (int)generated.SpawnFeet.Y;
        ulong hashAtSave;
        System.Numerics.Vector2 positionAtSave;
        long tickAtSave;

        using (var sim = world.CreateSimulation(registry))
        {
            int player = character.SpawnInto(sim, world.PlayerPosition("Leo", physics));
            sim.SetInput(player, new PlayerInput(0f, false, true, targetX, targetY));
            for (int i = 0; i < 60; i++) sim.Step();
            sim.SetInput(player, default);
            for (int i = 0; i < 120; i++) sim.Step();
            Assert.Equal(0, sim.GetBlock(targetX, targetY));
            Assert.Contains(Enumerable.Range(0, 50), s => sim.GetInventory(player)![s].ItemId == registry.ItemId("dirt"));

            var worldToSave = WorldSave.Capture(sim, world.Info, new Dictionary<string, int> { ["Leo"] = player });
            var characterToSave = PlayerSave.Capture(sim, player, "Leo");
            SaveFiles.WriteAtomic(worldPath, s => WorldFile.Write(s, worldToSave, registry));
            SaveFiles.WriteAtomic(playerPath, s => PlayerFile.Write(s, characterToSave));
            hashAtSave = TileWorldHash.Compute(sim.Tiles);
            sim.TryGetEntityView(player, out var view);
            positionAtSave = view.Position;
            tickAtSave = sim.Tick;
        }

        var loadedWorld = SaveFiles.ReadWithBackup(worldPath, s => WorldFile.Read(s, registry)).Value;
        var loadedCharacter = SaveFiles.ReadWithBackup(playerPath, s => PlayerFile.Read(s, registry)).Value;
        using var sim2 = loadedWorld.CreateSimulation(registry);
        int player2 = loadedCharacter.SpawnInto(sim2, loadedWorld.PlayerPosition("Leo", physics));

        Assert.Equal(hashAtSave, TileWorldHash.Compute(sim2.Tiles));
        Assert.Equal(0, sim2.GetBlock(targetX, targetY));
        Assert.Equal(tickAtSave, sim2.Tick);
        Assert.True(sim2.TryGetEntityView(player2, out var restored));
        Assert.Equal(positionAtSave, restored.Position);
        Assert.Equal(generated.Seed, loadedWorld.Info.Seed);
        Assert.Contains(Enumerable.Range(0, 50), s => sim2.GetInventory(player2)![s].ItemId == registry.ItemId("dirt"));
    }
}
```

`tests/Zyon.Core.Tests/PersistencePerformanceTests.cs`:

```csharp
using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class PersistencePerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void SmallWorld_SavesAndLoadsQuickly()
    {
        var registry = TestData.Registry();
#if DEBUG
        var (width, height) = (1050, 300);
#else
        var (width, height) = WorldSizes.Dimensions(WorldSize.Small);
#endif
        var save = WorldSave.FromGenerated(new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(width, height, 77));

        using var ms = new MemoryStream();
        var sw = Stopwatch.StartNew();
        WorldFile.Write(ms, save, registry);
        var writeTime = sw.Elapsed;
        ms.Position = 0;
        sw.Restart();
        var loaded = WorldFile.Read(ms, registry);
        var readTime = sw.Elapsed;

        output.WriteLine($"Mundo {width}×{height}: {ms.Length / 1024.0:F0} KiB | gravar {writeTime.TotalMilliseconds:F0} ms | ler {readTime.TotalMilliseconds:F0} ms");
        Assert.Equal(TileWorldHash.Compute(save.Tiles), TileWorldHash.Compute(loaded.Tiles));
#if !DEBUG
        Assert.True(writeTime.TotalSeconds < 1, $"gravação levou {writeTime.TotalSeconds:F2} s");
        Assert.True(readTime.TotalSeconds < 1, $"leitura levou {readTime.TotalSeconds:F2} s");
#endif
    }
}
```

- [ ] **Step 2: Rodar os testes**

Run: `dotnet test --filter "FullyQualifiedName~PersistenceScenarioTests|FullyQualifiedName~PersistencePerformanceTests"`
Expected: PASS, 2 testes. Se o cenário falhar, **não afrouxe o teste**: use superpowers:systematic-debugging.

- [ ] **Step 3: Desempenho em Release**

Run: `dotnet test -c Release --filter "FullyQualifiedName~PersistencePerformanceTests" --logger "console;verbosity=detailed"`
Expected: PASS com a linha `Mundo 4200×1200: X KiB | gravar Y ms | ler Z ms`, ambos < 1000 ms. Se não, reporte os números — não otimize por conta própria.

- [ ] **Step 4: Atualizar o `CLAUDE.md`**

Na seção "Estrutura", depois do item de `WorldGen`, adicionar:

```markdown
- `src/Zyon.Core/Persistence` — saves: `WorldSave`/`WorldFile` (`.zworld`, binário: magic + versão sem compressão, resto em Brotli com paleta de nomes e RLE) e `PlayerSave`/`PlayerFile` (`.zplayer`, JSON). `SaveFiles` grava atomicamente (`.tmp` → troca, anterior vira `.bak`) e lê com fallback para o `.bak`.
```

Na seção "Regras invioláveis", adicionar:

```markdown
- Mudou o que vai num save? Incremente `WorldFile.FormatVersion`/`PlayerFile.FormatVersion` e escreva a migração da versão anterior; saves antigos são migrados, nunca descartados.
- Saves referenciam blocos/paredes/itens **pelo nome**; um nome que sumiu dos dados faz a carga falhar com mensagem clara (nunca carregar com ids errados).
- Carga com erro nunca sobrescreve arquivos: use `SaveFiles.ReadWithBackup` e mostre a `SaveLoadException` ao jogador.
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test(persistence): cenário salvar/carregar e desempenho; docs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Verificação final

- [ ] **Step 1:** `dotnet build -c Release` → 0 avisos, 0 erros.
- [ ] **Step 2:** `dotnet test` e `dotnet test -c Release` → todos verdes nas duas configurações.
- [ ] **Step 3:** `grep -ri "godot" src/ || echo "ok: nenhuma referência"` → `ok: nenhuma referência`.
- [ ] **Step 4:** `git status --short` → vazio.
