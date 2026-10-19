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

    /// <summary>
    /// Captura o estado atual. Os tiles não são copiados: serialize (WorldFile.Write) de forma síncrona na thread da
    /// simulação, entre dois Steps; só os bytes resultantes podem ser gravados em disco em segundo plano.
    /// <paramref name="previousPositions"/> preserva as posições de personagens que não estão na sessão atual.
    /// </summary>
    public static WorldSave Capture(Simulation sim, WorldInfo info, IReadOnlyDictionary<string, int> playerIdsByName,
        IReadOnlyDictionary<string, Vector2>? previousPositions = null)
    {
        var items = sim.SnapshotDroppedItems()
            .Select(d => new SavedDroppedItem(sim.Registry.Item(d.Stack.ItemId).Name, d.Stack.Count, d.Position, d.AgeTicks))
            .ToList();
        var positions = previousPositions is null
            ? new Dictionary<string, Vector2>()
            : new Dictionary<string, Vector2>(previousPositions);
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
