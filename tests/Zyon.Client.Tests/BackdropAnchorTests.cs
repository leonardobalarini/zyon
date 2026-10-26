namespace Zyon.Client.Tests;

public class BackdropAnchorTests
{
    private const float Offset = BackdropAnchor.BaseOffsetRows;

    [Fact]
    public void FirstUpdate_IsTheAverageOfVisibleColumnsPlusOffset()
    {
        var anchor = new BackdropAnchor([10, 20, 30, 40, 50]);

        Assert.Equal(30f + Offset, anchor.Update(1, 3, 0.016f));
    }

    [Fact]
    public void ColumnsOutsideTheWorld_AreClipped()
    {
        var left = new BackdropAnchor([10, 20, 30, 40, 50]);
        var right = new BackdropAnchor([10, 20, 30, 40, 50]);
        var outside = new BackdropAnchor([10, 20, 30, 40, 50]);

        Assert.Equal(15f + Offset, left.Update(-100, 1, 0.016f));
        Assert.Equal(45f + Offset, right.Update(3, 100, 0.016f));
        Assert.Equal(50f + Offset, outside.Update(200, 300, 0.016f));
    }

    [Fact]
    public void LaterUpdates_ConvergeWithoutOvershooting()
    {
        var anchor = new BackdropAnchor([10, 10, 50, 50]);
        float start = anchor.Update(0, 1, 0f);
        float target = 50f + Offset;

        float previous = start;
        for (int i = 0; i < 300; i++)   // 5 s = 10 meias-vidas
        {
            float now = anchor.Update(2, 3, 1f / 60);
            Assert.True(now > previous, $"quadro {i}: {now} não se aproximou de {target}");
            Assert.True(now <= target, $"quadro {i}: {now} passou do alvo {target}");
            previous = now;
        }
        Assert.Equal(target, previous, 0.5f);
    }

    [Fact]
    public void HalfLife_CoversHalfTheDistance()
    {
        var anchor = new BackdropAnchor([0, 100]);
        anchor.Update(0, 0, 0f);

        Assert.Equal(50f + Offset, anchor.Update(1, 1, BackdropAnchor.HalfLifeSeconds), 0.01f);
    }

    [Fact]
    public void SurfaceRows_AreTheFirstSolidBlock_IgnoringNonSolid()
    {
        var registry = TestData.Registry();
        var w = new Zyon.Core.Tiles.TileWorld(3, 10);
        w.SetBlock(0, 4, registry.BlockId("dirt"));
        w.SetBlock(1, 2, registry.BlockId("torch"));
        w.SetBlock(1, 7, registry.BlockId("dirt"));
        using var view = TestData.View(w);

        Assert.Equal([4, 7, 10], BackdropAnchor.SurfaceRows(view));
    }

    [Fact]
    public void ZeroDelta_DoesNotMove()
    {
        var anchor = new BackdropAnchor([0, 100]);
        float start = anchor.Update(0, 0, 0f);

        Assert.Equal(start, anchor.Update(1, 1, 0f));
    }
}
