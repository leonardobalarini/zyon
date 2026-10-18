using System.Numerics;
using Arch.Core;
using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Logging;
using Zyon.Core.Physics;
using Zyon.Core.Tiles;
using Zyon.Core.Sim.Systems;

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
        systems =
        [
            new PlayerMovementSystem(),
            new PhysicsSystem(),
            new ToolActionSystem(),
            new ItemPickupSystem(),
            new ItemMergeSystem(),
            new ItemDespawnSystem(),
        ];
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

        var playerSize = new Vector2(Config.PlayerWidth, Config.PlayerHeight);
        int id = nextNetworkId++;
        var entity = Ecs.Create(
            new NetworkId(id),
            new Position(position),
            new PreviousPosition(position),
            new Velocity(Vector2.Zero),
            new Body { Size = playerSize, CanStepUp = true, OnGround = Collider.IsGrounded(position, playerSize) },
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
