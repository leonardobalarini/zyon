using Zyon.Core.Lighting;

namespace Zyon.Client.Tests;

public class LightRecomputePolicyTests
{
    private static readonly TileRect Visible = new(100, 50, 139, 72);
    private static readonly TileRect Computed = Visible.Expand(18 + 8);

    private static LightRecomputePolicy Ready()
    {
        var p = new LightRecomputePolicy(margin: 18);
        p.Computed(Computed, (120, 60), 1f);
        return p;
    }

    [Fact]
    public void FirstFrame_AlwaysComputes()
    {
        Assert.True(new LightRecomputePolicy(18).ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void NothingChanged_DoesNotRecompute()
    {
        Assert.False(Ready().ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void PlayerChangedTile_Recomputes()
    {
        Assert.True(Ready().ShouldRecompute(Visible, (121, 60), 1f));
    }

    [Fact]
    public void SkyFactorDrift_RecomputesOnlyAboveThreshold()
    {
        var p = Ready();

        Assert.False(p.ShouldRecompute(Visible, (120, 60), 0.995f));
        Assert.True(p.ShouldRecompute(Visible, (120, 60), 0.98f));
    }

    [Fact]
    public void TileChangedInsideTheRegion_Recomputes_OutsideDoesNot()
    {
        var p = Ready();
        p.MarkTileChanged(5000, 5000);
        Assert.False(p.ShouldRecompute(Visible, (120, 60), 1f));

        p.MarkTileChanged(Computed.MinX, Computed.MinY);
        Assert.True(p.ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void CameraMovingIntoTheMargin_Recomputes()
    {
        var p = Ready();

        Assert.False(p.ShouldRecompute(new TileRect(105, 50, 144, 72), (120, 60), 1f));   // ainda dentro da folga
        Assert.True(p.ShouldRecompute(new TileRect(110, 50, 149, 72), (120, 60), 1f));    // entrou na margem
    }

    [Fact]
    public void Computed_ClearsTheDirtyFlag()
    {
        var p = Ready();
        p.MarkTileChanged(120, 60);
        p.Computed(Computed, (120, 60), 1f);

        Assert.False(p.ShouldRecompute(Visible, (120, 60), 1f));
    }

    [Fact]
    public void CameraPinnedAtWorldEdge_DoesNotRecomputeEveryFrame()
    {
        var visible = new TileRect(0, 50, 39, 72);   // encostada em x = 0
        var p = new LightRecomputePolicy(margin: 18);
        p.Computed(visible.Expand(18 + 8), (20, 60), 1f);   // região sem ClampTo

        Assert.False(p.ShouldRecompute(visible, (20, 60), 1f));
    }
}
