using Arch.Core;
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
        var wood = h.Registry.ItemId("wood");
        int woodCount = 0, otherCount = 0;
        h.Sim.Ecs.Query(in Queries.Items, (ref DroppedItem d) =>
        {
            if (d.Stack.ItemId == wood) woodCount += d.Stack.Count; else otherCount += d.Stack.Count;
        });
        Assert.Equal(8, woodCount);
        Assert.Equal(0, otherCount);
    }

    [Fact]
    public void BreakingABranch_RemovesOnlyTheBranch()
    {
        using var h = new SimHarness();
        PlantDeadTree(h);
        h.Idle(5);
        h.Sim.DrainEvents();

        h.Run(TicksToMine(h, "dead_branch"), Use(23, 35));

        Assert.Equal(0, h.Tiles.GetBlock(23, 35));
        for (int y = 34; y <= 39; y++) Assert.Equal(h.Registry.BlockId("dead_trunk"), h.Tiles.GetBlock(22, y));
        Assert.Equal(h.Registry.BlockId("dead_branch"), h.Tiles.GetBlock(21, 36));
        Assert.Equal(h.Registry.BlockId("dead_branch"), h.Tiles.GetBlock(20, 36));
        var events = h.Sim.DrainEvents();
        Assert.Single(events.OfType<TileChanged>(), e => e.Block == 0);
        Assert.Single(events.OfType<EntitySpawned>(), e => e.Kind == EntityKind.DroppedItem);
    }

    [Fact]
    public void BreakingAPineLeaf_RemovesOnlyTheLeaf()
    {
        using var h = new SimHarness();
        ushort trunk = h.Registry.BlockId("pine_trunk"), leaves = h.Registry.BlockId("pine_leaves");
        for (int y = 34; y <= 39; y++) h.Tiles.SetBlock(22, y, trunk);
        h.Tiles.SetBlock(21, 36, leaves);
        h.Tiles.SetBlock(23, 36, leaves);
        h.Idle(5);
        h.Sim.DrainEvents();

        h.Run(TicksToMine(h, "pine_leaves"), Use(21, 36));

        Assert.Equal(0, h.Tiles.GetBlock(21, 36));
        Assert.Equal(leaves, h.Tiles.GetBlock(23, 36));
        for (int y = 34; y <= 39; y++) Assert.Equal(trunk, h.Tiles.GetBlock(22, y));
        Assert.Single(h.Sim.DrainEvents().OfType<TileChanged>(), e => e.Block == 0);
    }

    [Fact]
    public void BreakingTheGroundUnderATree_DoesNotFellIt()
    {
        using var h = new SimHarness();
        PlantDeadTree(h);
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dead_trunk"));   // tronco vizinho na linha de baixo: nada sustenta a árvore, quebrar o chão não derruba nada

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
        world.SetBlock(12, 16, trunk);    // longe de qualquer tronco: fica
        world.SetBlock(11, 16, trunk);    // abaixo de (11,15), linha 16 > 15: fica
        world.SetBlock(12, 15, r.BlockId("dirt"));
        world.SetBlock(10, 15, 0);        // o tile cortado já foi quebrado
        var result = new List<(int X, int Y)>();

        TreeFelling.Collect(world, r, 10, 15, result);

        Assert.Contains((11, 15), result);
        Assert.Contains((10, 10), result);
        Assert.DoesNotContain((10, 16), result);
        Assert.DoesNotContain((11, 16), result);
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

        Assert.Equal(TreeFelling.MaxBlocks - 1, result.Count);
    }
}
