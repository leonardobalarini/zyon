namespace Zyon.ArtGen.Tests;

public class BackdropGeneratorTests
{
    private static readonly Rgba[] Colors = [Rgba.Parse("#203040"), Rgba.Parse("#c0d0e0")];

    public static TheoryData<string, bool> Layers => new()
    {
        { "forest", false }, { "forest", true }, { "snow", false }, { "snow", true }, { "desert", false }, { "desert", true },
    };

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_IsTransparentOnTop_OpaqueAtTheBottom_AndUsesOnlyItsTwoColors(string style, bool near)
    {
        var img = BackdropGenerator.Draw(style, near, Colors, seed: 7);

        Assert.Equal((BackdropGenerator.Width, BackdropGenerator.Height), (img.Width, img.Height));
        for (int x = 0; x < img.Width; x++)
        {
            Assert.Equal(0, img.Get(x, 0).A);
            Assert.NotEqual(0, img.Get(x, img.Height - 1).A);
        }
        for (int y = 0; y < img.Height; y++)
        for (int x = 0; x < img.Width; x++)
        {
            var c = img.Get(x, y);
            Assert.True(c.A == 0 || c == Colors[0] || c == Colors[1], $"cor inesperada em ({x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void SameInput_SamePixels(string style, bool near)
    {
        var a = BackdropGenerator.Draw(style, near, Colors, seed: 3);
        var b = BackdropGenerator.Draw(style, near, Colors, seed: 3);

        Assert.True(a.Pixels.AsSpan().SequenceEqual(b.Pixels));
    }

    [Theory]
    [InlineData(48, 110)]
    [InlineData(120, 50)]
    public void Ridge_IsPeriodic_AndContinuousAcrossTheSeam(int step, int amplitude)
    {
        for (int x = 0; x < BackdropGenerator.Width; x++)
            Assert.Equal(BackdropGenerator.RidgeHeight(x, step, amplitude, 9), BackdropGenerator.RidgeHeight(x + BackdropGenerator.Width, step, amplitude, 9));
        int last = BackdropGenerator.RidgeHeight(BackdropGenerator.Width - 1, step, amplitude, 9);
        int first = BackdropGenerator.RidgeHeight(0, step, amplitude, 9);
        Assert.InRange(Math.Abs(last - first), 0, 3 * amplitude / (2 * step) + 1);
    }

    [Theory]
    [InlineData("snow")]
    [InlineData("desert")]
    public void FarLayer_FirstAndLastColumnsMatch(string style)
    {
        var img = BackdropGenerator.Draw(style, near: false, Colors, seed: 7);
        static int Top(Canvas c, int x) { int y = 0; while (c.Get(x, y).A == 0) y++; return y; }

        Assert.InRange(Math.Abs(Top(img, 0) - Top(img, img.Width - 1)), 0, 4);
    }

    [Fact]
    public void PutWrapped_WrapsAroundTheWidth()
    {
        var img = new Canvas(BackdropGenerator.Width, BackdropGenerator.Height);

        BackdropGenerator.PutWrapped(img, -1, 5, Colors[0]);
        BackdropGenerator.PutWrapped(img, BackdropGenerator.Width, 6, Colors[1]);

        Assert.Equal(Colors[0], img.Get(BackdropGenerator.Width - 1, 5));
        Assert.Equal(Colors[1], img.Get(0, 6));
    }

    [Fact]
    public void UnknownStyle_Throws()
    {
        Assert.Throws<ArgumentException>(() => BackdropGenerator.Draw("jungle", false, Colors, 1));
    }

    [Fact]
    public void RidgeHeight_RejectsStepThatDoesNotDivideWidth()
    {
        Assert.Throws<ArgumentException>(() => BackdropGenerator.RidgeHeight(0, 7, 10, 1));
    }
}
