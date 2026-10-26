using Zyon.Core.Biomes;
using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ClientVisualsTests
{
    private static readonly string[] Ids = ["forest", "snow", "desert"];

    private const string Valid = """
        {
          "forest": { "sky": "#010203", "layers": ["forest_far", "forest_near"] },
          "snow":   { "sky": "#040506", "layers": ["snow_far", "snow_near"] },
          "desert": { "sky": "#070809", "layers": ["desert_far", "desert_near"] }
        }
        """;

    [Fact]
    public void RealFile_HasEveryBiomeOfTheData_AndItsArtExists()
    {
        var ids = BiomeConfigLoader.LoadFromDirectory(TestData.DataDir).Biomes.Select(b => b!.Id);
        var v = ClientVisuals.LoadFromDirectory(TestData.DataDir, ids);

        Assert.Equal(new Rgb(0x7f, 0x9f, 0xbf), v.For("forest").Sky);
        Assert.Equal(new[] { "snow_far", "snow_near" }, v.For("snow").Layers);
        v.RequireArt(ArtCatalog.LoadFromDirectory(TestData.DataDir, TestData.Registry()).Manifest);
    }

    [Fact]
    public void Valid_Loads_WithCommentsAndTrailingCommas()
    {
        var v = ClientVisuals.Load("// céus\n" + Valid.TrimEnd().TrimEnd('}') + ",}", Ids);

        Assert.Equal(new Rgb(7, 8, 9), v.For("desert").Sky);
    }

    [Theory]
    [InlineData("{ not json", "JSON")]
    [InlineData("""{"forest": {"sky": "#zz0000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "#zz0000")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "layers")]
    [InlineData("""{"forest": {"layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "sky")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}}""", "desert")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"]}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}, "jungle": {"sky": "#000000", "layers": ["a", "b"]}}""", "jungle")]
    [InlineData("""{"forest": {"sky": "#000000", "layers": ["a", "b"], "fog": 1}, "snow": {"sky": "#000000", "layers": ["a", "b"]}, "desert": {"sky": "#000000", "layers": ["a", "b"]}}""", "JSON")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json, Ids));

        Assert.Contains(ClientVisuals.DisplayName, ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void LayerWithoutArt_Throws()
    {
        var v = ClientVisuals.Load(Valid, Ids);
        var manifest = new ArtManifest { Backgrounds = ["forest_far", "forest_near", "snow_far", "snow_near", "desert_far"] };

        var ex = Assert.Throws<DataException>(() => v.RequireArt(manifest));

        Assert.Contains("desert_near", ex.Message);
    }
}
