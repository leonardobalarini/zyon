using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Physics;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class TileMoverTests
{
    private readonly Registry r = TestData.Registry();
    private static readonly Vector2 Box = new(1f, 1f);
    private static readonly Vector2 PlayerSize = new(1.25f, 2.75f);

    private (TileWorld World, TileCollider Collider) Ground(int groundY = 10)
    {
        var w = TestWorlds.Flat(r, 40, 20, groundY);
        return (w, new TileCollider(w, r));
    }

    [Fact]
    public void Falling_LandsOnGround()
    {
        var (_, c) = Ground();

        var res = TileMover.Move(c, new Vector2(5f, 5f), Box, new Vector2(0f, 10f), false);

        Assert.Equal(9.5f, res.Position.Y, 4);
        Assert.True(res.HitY);
        Assert.True(res.Landed);
    }

    [Fact]
    public void HugeDelta_DoesNotTunnelThroughThinFloor()
    {
        var w = new TileWorld(40, 40);
        for (int x = 0; x < 40; x++) w.SetBlock(x, 10, r.BlockId("dirt"));
        var c = new TileCollider(w, r);

        var res = TileMover.Move(c, new Vector2(5f, 5f), Box, new Vector2(0f, 100f), false);

        Assert.Equal(9.5f, res.Position.Y, 4);
    }

    [Fact]
    public void Wall_BlocksHorizontalMovement()
    {
        var (w, c) = Ground();
        for (int y = 0; y < 10; y++) w.SetBlock(10, y, r.BlockId("dirt"));

        var res = TileMover.Move(c, new Vector2(8f, 9.5f), Box, new Vector2(5f, 0f), false);

        Assert.Equal(9.5f, res.Position.X, 4);
        Assert.True(res.HitX);
    }

    [Fact]
    public void WorldEdge_IsSolid()
    {
        var (_, c) = Ground();

        var res = TileMover.Move(c, new Vector2(3f, 9.5f), Box, new Vector2(-10f, 0f), false);

        Assert.Equal(0.5f, res.Position.X, 4);
        Assert.True(res.HitX);
    }

    [Fact]
    public void StepUp_ClimbsOneTileLedge()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: true);

        Assert.True(res.SteppedUp);
        Assert.False(res.HitX);
        Assert.Equal(start.Y - 1f, res.Position.Y, 4);
    }

    [Fact]
    public void StepUp_DoesNotClimbTwoTiles()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        w.SetBlock(10, 8, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: true);

        Assert.True(res.HitX);
        Assert.False(res.SteppedUp);
        Assert.Equal(10f - PlayerSize.X / 2, res.Position.X, 4);
        Assert.Equal(start.Y, res.Position.Y, 4);
    }

    [Fact]
    public void StepUp_NotAllowed_Blocks()
    {
        var (w, c) = Ground();
        w.SetBlock(10, 9, r.BlockId("dirt"));
        var start = new Vector2(8.9f, 10f - PlayerSize.Y / 2);

        var res = TileMover.Move(c, start, PlayerSize, new Vector2(0.5f, 0f), allowStepUp: false);

        Assert.True(res.HitX);
        Assert.False(res.SteppedUp);
    }

    [Fact]
    public void IsGrounded_TrueOnlyWhenTouchingFloor()
    {
        var (_, c) = Ground();

        Assert.True(c.IsGrounded(new Vector2(5f, 9.5f), Box));
        Assert.False(c.IsGrounded(new Vector2(5f, 9f), Box));
    }

    [Fact]
    public void Aabb_Overlaps_ExcludesTouchingEdges()
    {
        Assert.True(Aabb.Overlaps(new Vector2(0, 0), Box, new Vector2(0.9f, 0), Box));
        Assert.False(Aabb.Overlaps(new Vector2(0, 0), Box, new Vector2(1f, 0), Box));
    }
}
