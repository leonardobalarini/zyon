using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Ação primária contextual: ferramenta minera (bloco de árvore derruba a árvore, ver TreeFelling), bloco é colocado.</summary>
internal sealed class ToolActionSystem : ISystem
{
    private static readonly Vector2 TileSize = new(1f, 1f);

    private readonly List<(Vector2 Center, Vector2 Size)> placementBlockers = new();
    private readonly List<(ItemStack Stack, Vector2 Position)> pendingDrops = new();
    private readonly List<(int X, int Y)> felled = new();

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

        if (!st.Mining || st.MineX != tx || st.MineY != ty || st.MineBlockId != blockId)
        {
            StopMining(sim, playerId, ref st);
            st.Mining = true;
            st.MineX = tx;
            st.MineY = ty;
            st.MineBlockId = blockId;
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
