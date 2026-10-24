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
    public void HoldingABlockItem_ShowsItUpright_InTheHoldArmFrame()
    {
        var s = new PlayerAnimState();

        var pose = new PlayerAnimator(Art, R).Update(Player(vx: 9f, held: Torch), 1 / 60f, ref s);

        int hold = P.SwingFirst + PlayerAnimator.HoldSwingOffset;
        Assert.Equal(Torch, pose.ItemId);
        Assert.InRange(pose.BodyFrame, P.WalkFirst, P.WalkFirst + P.WalkCount - 1);   // o corpo continua andando
        Assert.Equal(hold, pose.ArmFrame);
        Assert.Equal(-90f - Art.ItemByItemId[Torch]!.PointDeg, pose.ItemRotationDeg, 3);
        Assert.Equal(0f, pose.ItemRotationDeg, 3);
        Assert.Equal(P.Hand[hold][0], pose.HandX);
        Assert.Equal(P.Hand[hold][1], pose.HandY);
    }

    [Fact]
    public void AimingAtOwnColumn_KeepsTheCoreFacing()
    {
        var anim = new PlayerAnimator(Art, R);
        var left = new PlayerAnimState();
        var right = new PlayerAnimState();

        // Jogador em x 10,5 minerando o tile 10 logo abaixo: o lado vem do Core, não da posição dentro do tile.
        Assert.True(anim.Update(Player(facing: -1, using_: true, aimX: 10, held: Pickaxe), 1 / 60f, ref left).FacingLeft);
        Assert.False(anim.Update(Player(facing: 1, using_: true, aimX: 10, held: Pickaxe), 1 / 60f, ref right).FacingLeft);
    }

    [Fact]
    public void NegativeDt_IsTreatedAsZero()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        anim.Update(Player(vx: 9f), 0.1f, ref s);
        float walk = s.WalkPhase;

        var pose = anim.Update(Player(vx: 9f), -1f, ref s);

        Assert.Equal(walk, s.WalkPhase);
        Assert.InRange(pose.BodyFrame, P.WalkFirst, P.WalkFirst + P.WalkCount - 1);

        var swing = new PlayerAnimState();
        anim.Update(Player(using_: true, aimX: 15, held: Pickaxe), 1 / 60f, ref swing);
        Assert.Equal(P.SwingFirst, anim.Update(Player(using_: true, aimX: 15, held: Pickaxe), -1f, ref swing).ArmFrame);
        Assert.True(swing.SwingPhase >= 0);
    }

    [Fact]
    public void ReleasingAndPressingAgain_RestartsTheSwing()
    {
        var anim = new PlayerAnimator(Art, R);
        var s = new PlayerAnimState();
        var using_ = Player(using_: true, aimX: 15, held: Pickaxe);
        for (int i = 0; i < 12; i++) anim.Update(using_, 1 / 60f, ref s);
        Assert.NotEqual(P.SwingFirst, anim.Update(using_, 1 / 60f, ref s).ArmFrame);

        anim.Update(Player(held: Pickaxe), 1 / 60f, ref s);

        Assert.Equal(P.SwingFirst, anim.Update(using_, 1 / 60f, ref s).ArmFrame);
    }

    [Fact]
    public void FasterTool_SwingsFaster()
    {
        var fastRegistry = Zyon.Core.Data.RegistryLoader.Load(Data("blocks.json"), Data("walls.json"),
            Data("items.json").Replace("\"speed\": 1.0", "\"speed\": 2.0"), Data("physics.json"), Data("player.json"));
        Assert.Equal(2f, fastRegistry.Item(Pickaxe).ToolSpeed);
        var fastArt = ArtCatalog.Resolve(Art.Manifest, fastRegistry);
        var slow = new PlayerAnimState();
        var fast = new PlayerAnimState();
        var e = Player(using_: true, aimX: 15, held: Pickaxe);
        var slowAnim = new PlayerAnimator(Art, R);
        var fastAnim = new PlayerAnimator(fastArt, fastRegistry);
        for (int i = 0; i < 4; i++)
        {
            slowAnim.Update(e, 1 / 60f, ref slow);
            fastAnim.Update(e, 1 / 60f, ref fast);
        }

        Assert.Equal(2 * slow.SwingPhase, fast.SwingPhase, 4);
    }

    private static string Data(string file) => File.ReadAllText(Path.Combine(TestData.DataDir, file));

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
