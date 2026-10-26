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
    }

    [Fact]
    public void BreakingTheGroundUnderATree_DoesNotFellIt()
    {
        using var h = new SimHarness();
        PlantDeadTree(h);
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dead_trunk"));   // raiz vizinha na linha de baixo

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
        world.SetBlock(12, 16, trunk);    // ligado só por baixo: fica
        world.SetBlock(12, 15, r.BlockId("dirt"));
        world.SetBlock(10, 15, 0);        // o tile cortado já foi quebrado
        var result = new List<(int X, int Y)>();

        TreeFelling.Collect(world, r, 10, 15, result);

        Assert.Contains((11, 15), result);
        Assert.Contains((10, 10), result);
        Assert.DoesNotContain((10, 16), result);
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

        Assert.Equal(TreeFelling.MaxBlocks, result.Count);
    }
}
