using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.Tests;

public class BunkerTemplateTests
{
    [Fact]
    public void BunkerTemplate_LoadsAndResolves_WithGateOnTheLeft()
    {
        var r = TestData.Registry();
        var t = StructureLoader.LoadFromDirectory(TestData.DataDir, "bunker");
        var s = t.Resolve(r);

        Assert.Equal(40, s.Width);
        Assert.Equal(10, s.Height);
        Assert.Equal((0, 7), s.Anchor);
        Assert.Equal((15, 7), s.Spawn);
        Assert.Equal(r.WallId("metal_wall"), s[s.Spawn.X, s.Spawn.Y].Wall);
    }

    [Fact]
    public void BunkerTemplate_UsesEveryBunkerBlock()
    {
        var r = TestData.Registry();
        var s = StructureLoader.LoadFromDirectory(TestData.DataDir, "bunker").Resolve(r);
        var used = new HashSet<ushort>();
        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
            used.Add(s[x, y].Block);

        foreach (var name in new[] { "metal_plate", "hazard_plate", "bunker_lamp", "crate", "pipe", "vent", "panel" })
            Assert.Contains(r.BlockId(name), used);
    }
}
