using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Items;

namespace Zyon.Client.Tests;

public class PlayerAnimatorTests
{
    private static readonly Zyon.Core.Data.Registry R = TestData.Registry();
    private static readonly ArtCatalog Art = ArtCatalog.LoadFromDirectory(TestData.DataDir, R);
    private static readonly PlayerArt P = Art.Manifest.Player;
    private static ushort Pickaxe => R.ItemId("basic_pickaxe");
    private static ushort Torch => R.ItemId("torch");

    private static EntityView Player(float vx = 0, bool onGround = true, int facing = 1, bool using_ = false, int aimX = 0, ushort held = 0) =>
        new(1, EntityKind.Player, new Vector2(10.5f, 20f), new Vector2(10.5f, 20f), new Vector2(1.25f, 2.75f), ItemStack.Empty,
            new Vector2(vx, 0), onGround, facing, using_, aimX, 20, held);

    [Fact]
    public void Standing_IsIdle_NoItemShownForTools()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();

        var pose = anim.Update(Player(held: Pickaxe), 1 / 60f, ref s);

        Assert.Equal(P.Idle, pose.BodyFrame);
        Assert.Equal(P.Idle, pose.ArmFrame);
        Assert.Equal((ushort)0, pose.ItemId);
        Assert.False(pose.FacingLeft);
    }

    [Fact]
    public void Walking_CyclesThroughWalkFrames_FasterWhenFaster()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        var frames = new HashSet<int>();
        for (int i = 0; i < 60; i++) frames.Add(anim.Update(Player(vx: 9f), 1 / 60f, ref s).BodyFrame);

        Assert.All(frames, f => Assert.InRange(f, P.WalkFirst, P.WalkFirst + P.WalkCount - 1));
        Assert.Equal(P.WalkCount, frames.Count);

        var slow = new PlayerAnimState();
        var fast = new PlayerAnimState();
        anim.Update(Player(vx: 2f), 0.1f, ref slow);
        anim.Update(Player(vx: 8f), 0.1f, ref fast);
        Assert.True(fast.WalkPhase > slow.WalkPhase);
    }

    [Fact]
    public void InAir_UsesAirFrame()
    {
        var s = new PlayerAnimState();
        Assert.Equal(P.Air, new PlayerAnimator(Art, R).Update(Player(vx: 5f, onGround: false), 1 / 60f, ref s).BodyFrame);
    }

    [Fact]
    public void Using_SwingsTheArm_ShowsTheTool_AndFacesTheAim()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        var arms = new List<int>();
        PlayerPose pose = default;
        float period = PlayerAnimator.SwingSeconds / R.Item(Pickaxe).ToolSpeed;
        int steps = (int)MathF.Round(period * 60);
        for (int i = 0; i < steps; i++)
        {
            pose = anim.Update(Player(facing: 1, using_: true, aimX: 5, held: Pickaxe), 1 / 60f, ref s);
            arms.Add(pose.ArmFrame);
        }

        Assert.True(pose.FacingLeft);                     // mira à esquerda do jogador (x 5 < 10,5)
        Assert.Equal(Pickaxe, pose.ItemId);
        Assert.Equal(P.SwingFirst, arms[0]);
        Assert.Contains(P.SwingFirst + P.SwingCount - 1, arms);
        Assert.True(arms.Zip(arms.Skip(1)).All(p => p.Second >= p.First), "o arco avança numa volta");
        var item = Art.ItemByItemId[Pickaxe]!;
        Assert.Equal(P.ArmAngleDeg[pose.ArmFrame] - item.PointDeg, pose.ItemRotationDeg, 3);
        Assert.Equal(P.Hand[pose.ArmFrame][0], pose.HandX);
    }

    [Fact]
    public void HoldingABlockItem_ShowsItUpright_WithoutUsing()
    {
        var s = new PlayerAnimState();

        var pose = new PlayerAnimator(Art, R).Update(Player(held: Torch), 1 / 60f, ref s);

        Assert.Equal(Torch, pose.ItemId);
        Assert.Equal(0f, pose.ItemRotationDeg, 3);
    }

    [Fact]
    public void NotUsing_FacesTheMovementSide()
    {
        var s = new PlayerAnimState();
        Assert.True(new PlayerAnimator(Art, R).Update(Player(vx: -3f, facing: -1), 1 / 60f, ref s).FacingLeft);
    }

    [Fact]
    public void UsingWithEmptyHand_DoesNotSwing()
    {
        var s = new PlayerAnimState();
        var pose = new PlayerAnimator(Art, R).Update(Player(using_: true, aimX: 5), 1 / 60f, ref s);

        Assert.Equal(P.Idle, pose.ArmFrame);
        Assert.Equal((ushort)0, pose.ItemId);
    }
}
