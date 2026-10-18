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
