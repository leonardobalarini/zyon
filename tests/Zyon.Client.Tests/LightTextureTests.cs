using Zyon.Core.Lighting;

namespace Zyon.Client.Tests;

public class LightTextureTests
{
    [Fact]
    public void ToL8_MapsZeroToOneOntoBytes_RowByRow()
    {
        var map = new LightMap();
        map.Reset(new TileRect(10, 20, 12, 21));   // 3×2
        map.Levels[0] = 0f;
        map.Levels[1] = 0.5f;
        map.Levels[2] = 1f;
        map.Levels[5] = 1.2f;   // fora da faixa: satura
        var bytes = new byte[6];

        LightTexture.ToL8(map, bytes);

        Assert.Equal(new byte[] { 0, 128, 255, 0, 0, 255 }, bytes);
    }

    [Fact]
    public void ToL8_TooSmallBuffer_Throws()
    {
        var map = new LightMap();
        map.Reset(new TileRect(0, 0, 3, 3));

        Assert.Throws<ArgumentException>(() => LightTexture.ToL8(map, new byte[4]));
    }
}
