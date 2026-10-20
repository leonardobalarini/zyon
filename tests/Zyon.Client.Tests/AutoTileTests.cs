using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

public class AutoTileTests
{
    [Fact]
    public void IsolatedTile_HasEmptyMask()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(10, 10, 1);
        using var view = TestData.View(w);

        Assert.Equal(0, AutoTile.Mask(view, 10, 10, TileLayer.Blocks));
    }

    [Fact]
    public void Neighbours_SetTheirBits()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(10, 10, 1);
        w.SetBlock(10, 9, 2);    // norte (material diferente também conecta)
        w.SetBlock(11, 10, 1);   // leste
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.North | AutoTile.East, AutoTile.Mask(view, 10, 10, TileLayer.Blocks));
    }

    [Fact]
    public void WorldEdge_CountsAsConnected()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(0, 63, 1);
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.West | AutoTile.South, AutoTile.Mask(view, 0, 63, TileLayer.Blocks));
    }

    [Fact]
    public void Layers_AreIndependent()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(10, 10, 1);
        w.SetBlock(10, 9, 1);    // bloco não conecta parede
        w.SetWall(9, 10, 1);
        using var view = TestData.View(w);

        Assert.Equal(AutoTile.West, AutoTile.Mask(view, 10, 10, TileLayer.Walls));
    }

    [Fact]
    public void Variation_IsStableAndInRange()
    {
        var seen = new HashSet<int>();
        for (int x = 0; x < 200; x++)
        {
            int v = AutoTile.Variation(x, 7, 3);
            Assert.InRange(v, 0, 2);
            Assert.Equal(v, AutoTile.Variation(x, 7, 3));
            seen.Add(v);
        }
        Assert.Equal(3, seen.Count);
    }
}
