namespace Zyon.Client.Tests;

public class BiomeAmbienceTests
{
    private static readonly ClientVisuals Visuals = ClientVisuals.Load("""
        {
          "forest": { "sky": "#000000", "layers": ["f1", "f2"] },
          "snow":   { "sky": "#c8c8c8", "layers": ["s1", "s2"] },
          "desert": { "sky": "#640000", "layers": ["d1", "d2"] }
        }
        """, ["forest", "snow", "desert"]);

    [Fact]
    public void Start_IsTheInitialBiome()
    {
        var a = new BiomeAmbience(Visuals, "forest");

        Assert.Equal(new Rgb(0, 0, 0), a.Sky);
        Assert.Equal(1f, a.Weight("forest"));
        Assert.Equal(0f, a.Weight("snow"));
    }

    [Fact]
    public void Weight_OfUnknownBiome_Throws()
    {
        var a = new BiomeAmbience(Visuals, "forest");

        Assert.Throws<ArgumentException>(() => a.Weight("jungle"));
    }

    [Fact]
    public void Change_IsLinearOverTwoSeconds()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");

        a.Advance(1f);
        Assert.Equal(new Rgb(100, 100, 100), a.Sky);
        Assert.Equal(0.5f, a.Weight("forest"), 3);
        Assert.Equal(0.5f, a.Weight("snow"), 3);

        a.Advance(1.5f);
        Assert.Equal(new Rgb(200, 200, 200), a.Sky);
        Assert.Equal(0f, a.Weight("forest"));
        Assert.Equal(1f, a.Weight("snow"));
    }

    [Fact]
    public void ChangeMidway_StartsFromTheCurrentColorAndWeights()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");
        a.Advance(1f);                      // céu (100, 100, 100), floresta 0,5, neve 0,5

        a.SetBiome("desert");
        a.Advance(1f);

        Assert.Equal(new Rgb(100, 50, 50), a.Sky);   // metade do caminho de (100,100,100) a (100,0,0)
        Assert.Equal(0f, a.Weight("forest"), 3);
        Assert.Equal(0f, a.Weight("snow"), 3);
        Assert.Equal(0.5f, a.Weight("desert"), 3);
    }

    [Fact]
    public void SameBiome_DoesNotRestartTheTransition()
    {
        var a = new BiomeAmbience(Visuals, "forest");
        a.SetBiome("snow");
        a.Advance(1f);

        a.SetBiome("snow");
        a.Advance(1f);

        Assert.Equal(new Rgb(200, 200, 200), a.Sky);
    }

    [Fact]
    public void UnknownBiome_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BiomeAmbience(Visuals, "forest").SetBiome("jungle"));
    }
}
