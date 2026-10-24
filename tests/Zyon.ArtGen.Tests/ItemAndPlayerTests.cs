using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen.Tests;

public class ItemAndPlayerTests
{
    private static readonly Registry R = RegistryLoader.LoadFromDirectory(Path.Combine(RepoPaths.Root, "data"));
    private static readonly ArtSource Source = ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);

    [Fact]
    public void Icons_OneCellPerItem_BlockItemsCopyTheIsolatedBlock()
    {
        var tiles = TileGenerator.BuildBlocks(Source, R, out var rows);
        var icons = ItemIconGenerator.Build(Source, R, tiles, out var arts);

        Assert.Equal((R.Items.Count - 1) * 16, icons.Width);
        Assert.Equal(16, icons.Height);
        var dirt = arts["dirt"];
        int tx = 0, ty = rows["dirt"] * 16;   // máscara 0, variação 0
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            Assert.Equal(tiles.Get(tx + x, ty + y), icons.Get(dirt.Cell * 16 + x, y));
        Assert.Equal(-90f, dirt.PointDeg);
    }

    [Fact]
    public void Pickaxe_PointsDiagonally_AndHasPixelsAtTheGrip()
    {
        var tiles = TileGenerator.BuildBlocks(Source, R, out _);
        var icons = ItemIconGenerator.Build(Source, R, tiles, out var arts);
        var pick = arts["basic_pickaxe"];

        Assert.Equal(-45f, pick.PointDeg);
        Assert.NotEqual(0, icons.Get(pick.Cell * 16 + pick.GripX, pick.GripY).A);
    }

    [Fact]
    public void Player_StripsHaveTheRightSize()
    {
        var art = PlayerGenerator.Build(Source, out var layers, out var meta);

        Assert.Equal(meta.BodyFrames * meta.FrameWidth, layers["body"].Width);
        Assert.Equal(meta.FrameHeight, layers["body"].Height);
        Assert.Equal(meta.ArmFrames * meta.FrameWidth, layers[ArtManifest.ArmLayer].Width);
        Assert.Equal<string>(["body", "outfit", "hair"], meta.Layers);
        Assert.Equal(meta.ArmFrames, meta.Hand.Count);
        Assert.Equal(meta.ArmFrames, meta.ArmAngleDeg.Count);
        Assert.Same(art, meta);
    }

    [Fact]
    public void Player_FeetTouchTheBottom_AndWalkFramesDiffer()
    {
        PlayerGenerator.Build(Source, out var layers, out var meta);
        var outfit = layers["outfit"];
        int W = meta.FrameWidth;

        bool BottomRowHasPixels(int frame) => Enumerable.Range(0, W).Any(x => outfit.Get(frame * W + x, meta.FrameHeight - 1).A != 0);
        Assert.True(BottomRowHasPixels(meta.Idle));
        bool Same(int f1, int f2) => Enumerable.Range(0, W * meta.FrameHeight)
            .All(i => outfit.Get(f1 * W + i % W, i / W) == outfit.Get(f2 * W + i % W, i / W));
        Assert.False(Same(meta.WalkFirst, meta.WalkFirst + 2));
    }

    [Fact]
    public void Swing_ArmAnglesGoFromUpBackToForwardDown()
    {
        PlayerGenerator.Build(Source, out _, out var meta);

        Assert.Equal(90f - 200f, meta.ArmAngleDeg[meta.SwingFirst]);
        Assert.Equal(90f - 30f, meta.ArmAngleDeg[meta.SwingFirst + meta.SwingCount - 1]);
        var hand = meta.Hand[meta.SwingFirst + meta.SwingCount - 1];
        Assert.True(hand[0] > 16 && hand[1] > 16, "no fim do golpe a mão fica à frente e abaixo do ombro");
    }

    [Fact]
    public void HoldFrame_ArmPointsForwardAndDown()
    {
        PlayerGenerator.Build(Source, out _, out var meta);

        float angle = meta.ArmAngleDeg[meta.SwingFirst + PlayerAnimator.HoldSwingOffset];
        Assert.InRange(angle, 0.001f, 89.999f);
    }
}
