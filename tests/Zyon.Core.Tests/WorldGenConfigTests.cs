using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenConfigTests
{
    [Fact]
    public void RealFile_LoadsAndResolvesMaterials()
    {
        var r = TestData.Registry();
        var cfg = TestData.WorldGenConfig();

        var m = WorldGenMaterials.Resolve(cfg, r);

        Assert.Equal(r.BlockId("dirt"), m.Dirt);
        Assert.Equal(r.BlockId("stone"), m.Stone);
        Assert.Equal(r.BlockId("deep_stone"), m.DeepStone);
        Assert.Equal(r.WallId("dirt_wall"), m.DirtWall);
        Assert.Equal(r.WallId("stone_wall"), m.StoneWall);
        Assert.Equal(2, m.Ores.Count);
        Assert.Equal(r.BlockId("ore_a"), m.Ores[0].Block);
    }

    [Fact]
    public void EmptyJson_UsesDefaults()
    {
        var cfg = WorldGenConfigLoader.Load("{}");

        Assert.Equal(0.25f, cfg.SurfaceMin);
        Assert.Equal("stone", cfg.Materials.Stone);
        Assert.Empty(cfg.Ores);
    }

    [Theory]
    [InlineData("""{"surfaceMin": 0.5, "surfaceMax": 0.4}""", "surfaceMin")]
    [InlineData("""{"deepLayerStart": 0.2}""", "deepLayerStart")]
    [InlineData("""{"terrainOctaves": 0}""", "terrainOctaves")]
    [InlineData("""{"hillinessMin": 1.5}""", "hillinessMin")]
    [InlineData("""{"hillinessFrequency": 0}""", "hillinessFrequency")]
    [InlineData("""{"dirtDepth": 3, "dirtDepthVariation": 5}""", "dirtDepth")]
    [InlineData("""{"wormLengthMin": 50, "wormLengthMax": 10}""", "wormLength")]
    [InlineData("""{"spawnFlatWidth": 1}""", "spawnFlatWidth")]
    [InlineData("""{"materials": null}""", "materials")]
    [InlineData("""{"ores": [{"block": "ore_a", "minDepth": 0.8, "maxDepth": 0.5, "veinsPerMillionTiles": 1, "veinSizeMin": 1, "veinSizeMax": 2}]}""", "ores[0]")]
    [InlineData("""{"ores": [null]}""", "ores[0]")]
    [InlineData("""{"materials": {"dirt": null}}""", "materials.dirt")]
    [InlineData("""{"materials": {"stoneWall": ""}}""", "materials.stoneWall")]
    [InlineData("""{"caveThreshold": 1.5}""", "caveThreshold")]
    [InlineData("""{"dirtDepthVariation": -1}""", "dirtDepthVariation")]
    [InlineData("""{"ores": [{"block": "ore_a", "minDepth": 0.1, "maxDepth": 0.5, "veinsPerMillionTiles": 1, "veinSizeMin": 1, "veinSizeMax": 20000}]}""", "ores[0]")]
    [InlineData("""{"surfacMin": 0.3}""", "worldgen.json")]
    [InlineData("""{""", "worldgen.json")]
    public void InvalidConfig_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => WorldGenConfigLoader.Load(json));

        Assert.Contains("worldgen.json", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void UnknownMaterial_ThrowsOnResolve()
    {
        var cfg = WorldGenConfigLoader.Load("""{"materials": {"stone": "granito"}}""");

        var ex = Assert.Throws<DataException>(() => WorldGenMaterials.Resolve(cfg, TestData.Registry()));

        Assert.Contains("materials.stone", ex.Message);
        Assert.Contains("granito", ex.Message);
    }

    [Fact]
    public void UnknownOreBlock_ThrowsOnResolve()
    {
        var cfg = WorldGenConfigLoader.Load(
            """{"ores": [{"block": "mithril", "minDepth": 0.1, "maxDepth": 0.5, "veinsPerMillionTiles": 1, "veinSizeMin": 1, "veinSizeMax": 2}]}""");

        var ex = Assert.Throws<DataException>(() => WorldGenMaterials.Resolve(cfg, TestData.Registry()));

        Assert.Contains("ores[0].block", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zyon-wg-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<DataException>(() => WorldGenConfigLoader.LoadFromDirectory(dir));
            Assert.Contains("worldgen.json", ex.Message);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
