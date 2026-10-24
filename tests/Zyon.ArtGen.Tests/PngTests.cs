namespace Zyon.ArtGen.Tests;

public class PngTests
{
    private static Canvas Sample()
    {
        var c = new Canvas(5, 3);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 5; x++)
            c.Set(x, y, new Rgba((byte)(x * 50), (byte)(y * 100), (byte)(x + y), (byte)(x == 0 ? 0 : 255)));
        return c;
    }

    [Fact]
    public void EncodeThenDecode_GivesTheSamePixels()
    {
        var original = Sample();

        var back = Png.Decode(Png.Encode(original));

        Assert.Equal(5, back.Width);
        Assert.Equal(3, back.Height);
        Assert.Equal(original.Pixels, back.Pixels);
    }

    [Fact]
    public void Encode_StartsWithPngSignature_AndIsDeterministic()
    {
        var a = Png.Encode(Sample());
        var b = Png.Encode(Sample());

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, a[..8]);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Decode_RejectsGarbage()
    {
        Assert.Throws<InvalidDataException>(() => Png.Decode([1, 2, 3]));
    }

    [Fact]
    public void Rgba_ParsesHex_AndScalesWithClamp()
    {
        var c = Rgba.Parse("#804020");

        Assert.Equal(new Rgba(128, 64, 32), c);
        Assert.Equal(new Rgba(192, 96, 48), c.Scale(1.5f));
        Assert.Equal(new Rgba(255, 160, 80), c.Scale(2.5f));   // 320 satura em 255
        Assert.Throws<FormatException>(() => Rgba.Parse("804020"));
    }

    [Fact]
    public void Hash_IsStable()
    {
        Assert.Equal(ArtHash.Get(3, 7, 11), ArtHash.Get(3, 7, 11));
        Assert.NotEqual(ArtHash.Get(3, 7, 11), ArtHash.Get(4, 7, 11));
        Assert.InRange(ArtHash.Range(3, 7, 11, 5), 0, 4);
    }
}
