using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class BiomesPassTests
{
    private static WorldGenContext RunBiomes(int width, int height, int seed)
    {
        var ctx = Context(width, height, seed);
        foreach (var pass in new IWorldGenPass[] { new TerrainPass(), new MountainPass(), new BiomesPass() })
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    private static (int Left, int Right) Inner(WorldGenContext ctx)
    {
        var m = ctx.Mountain!;
        int center = ctx.World.Width / 2;
        return (Math.Min(center, Math.Min(m.FootX, m.GentleFootX)), Math.Max(center, Math.Max(m.FootX, m.GentleFootX)));
    }

    [Theory]
    [InlineData(4200, 1200, 1)]
    [InlineData(4200, 1200, 2)]
    [InlineData(6400, 1800, 3)]
    [InlineData(8400, 2400, 4)]
    [InlineData(1050, 300, 1)]
    [InlineData(512, 256, 7)]
    public void Bands_AreOnOppositeSides_AndTheMountainIsForest(int width, int height, int seed)
    {
        var ctx = RunBiomes(width, height, seed);
        var map = ctx.Biomes!;
        var (innerLeft, innerRight) = Inner(ctx);

        Assert.Equal(2, map.Bands.Count);
        Assert.NotEqual(map.Bands[0].Biome.Id, map.Bands[1].Biome.Id);
        Assert.True(map.Bands[0].Right <= innerLeft - BiomesPass.EdgeMargin, $"faixa esquerda termina em {map.Bands[0].Right}");
        Assert.True(map.Bands[1].Left >= innerRight + BiomesPass.EdgeMargin, $"faixa direita começa em {map.Bands[1].Left}");
        Assert.True(map.Set.EdgeNoise < BiomesPass.EdgeMargin);   // o ruído não leva a faixa até a montanha
        for (int x = innerLeft; x <= innerRight; x++)
            Assert.Same(map.Set.Default, map.At(x, ctx.Surface[x], ctx.Surface[x]));
        Assert.Contains(Enumerable.Range(0, width), x => map.At(x, ctx.Surface[x], ctx.Surface[x]) == map.Bands[0].Biome);
        Assert.Contains(Enumerable.Range(0, width), x => map.At(x, ctx.Surface[x], ctx.Surface[x]) == map.Bands[1].Biome);
    }

    [Theory]
    [InlineData(4200, 1200, 1)]
    [InlineData(6400, 1800, 2)]
    [InlineData(8400, 2400, 3)]
    [InlineData(1050, 300, 4)]
    public void BandWidths_FollowTheData(int width, int height, int seed)
    {
        var ctx = RunBiomes(width, height, seed);

        foreach (var (biome, left, right) in ctx.Biomes!.Bands)
            Assert.InRange(right - left + 1, (int)(biome.Band!.WidthMin * width) - 1, (int)MathF.Ceiling(biome.Band.WidthMax * width) + 1);
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(100, 64)]
    [InlineData(200, 100)]
    public void TinyWorlds_NeverThrow_AndKeepBandsInside(int width, int height)
    {
        var ctx = RunBiomes(width, height, 1);

        foreach (var (_, left, right) in ctx.Biomes!.Bands)
        {
            Assert.InRange(left, 0, width - 1);
            Assert.InRange(right, left + BiomesPass.MinBandWidth - 1, width - 1);
        }
    }

    [Fact]
    public void Side_VariesWithTheSeed()
    {
        var leftIds = Enumerable.Range(1, 20).Select(seed => RunBiomes(1050, 300, seed).Biomes!.Bands[0].Biome.Id).ToHashSet();

        Assert.Equal(2, leftIds.Count);
    }

    [Fact]
    public void SameSeed_SameBands()
    {
        var a = RunBiomes(1050, 300, 5).Biomes!.Bands.Select(b => (b.Biome.Id, b.Left, b.Right));
        var b = RunBiomes(1050, 300, 5).Biomes!.Bands.Select(b => (b.Biome.Id, b.Left, b.Right));

        Assert.Equal(a, b);   // por id: os registros de bioma de execuções diferentes têm listas (Markers) diferentes
    }

    [Theory]
    [InlineData(true, 2000, 420, 700, 4200, 881, 1580)]     // cabe
    [InlineData(true, 300, 50, 400, 600, 10, 250)]          // encolhe até a borda − 10
    [InlineData(true, 25, 5, 30, 64, 0, 19)]                // sobra pouco: 20 colunas encostadas na borda
    [InlineData(false, 2200, 420, 700, 4200, 2620, 3319)]
    [InlineData(false, 3000, 420, 1000, 4200, 3420, 4189)]
    [InlineData(false, 62, 6, 10, 64, 44, 63)]
    public void PlaceBand_FitsShrinksOrHugsTheBorder(bool leftSide, int inner, int gap, int bandWidth, int worldWidth, int left, int right)
    {
        Assert.Equal((left, right), BiomesPass.PlaceBand(leftSide, inner, gap, bandWidth, worldWidth));
    }

    [Fact]
    public void Taper_MovesTheEdgeIntoTheBandWithDepth()
    {
        var ctx = RunBiomes(1050, 300, 1);
        var map = ctx.Biomes!;
        var (biome, left, _) = map.Bands[0];
        int x = left + BiomesPass.EdgeMargin;           // dentro da faixa mesmo com o ruído (±EdgeNoise)
        int surface = ctx.Surface[x];
        int depth = map.Set.Taper * (BiomesPass.EdgeMargin + map.Set.EdgeNoise + 1);

        Assert.Same(biome, map.At(x, surface, surface));
        Assert.Same(map.Set.Default, map.At(x, surface + depth, surface));
    }
}
