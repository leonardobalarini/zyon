using System.Text.Json.Nodes;
using Zyon.Core.Biomes;
using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class BiomeConfigTests
{
    private static string RealJson => File.ReadAllText(Path.Combine(TestData.DataDir, BiomeConfigLoader.FileName));

    private static string Edit(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(RealJson)!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject Biome(JsonObject root, string id) =>
        root["biomes"]!.AsArray().Select(n => n!.AsObject()).First(b => b["id"]!.GetValue<string>() == id);

    [Fact]
    public void RealFile_LoadsAndResolves()
    {
        var r = TestData.Registry();
        var set = BiomeSet.Resolve(BiomeConfigLoader.LoadFromDirectory(TestData.DataDir), r);

        Assert.Equal(new[] { "forest", "snow", "desert" }, set.All.Select(b => b.Id));
        Assert.Equal(new[] { 0, 1, 2 }, set.All.Select(b => b.Index));
        Assert.Equal("forest", set.Default.Id);
        Assert.Null(set.Default.Band);
        Assert.Equal(new[] { "snow", "desert" }, set.Bands.Select(b => b.Id));
        var snow = set.Get("snow");
        Assert.Equal(r.BlockId("snow"), snow.SurfaceBlock);
        Assert.Equal(r.BlockId("ice"), snow.ShallowBlock);
        Assert.Equal(r.WallId("snow_wall"), snow.SurfaceWall);
        Assert.Equal(r.WallId("ice_wall"), snow.ShallowWall);
        Assert.Equal(TreeShape.Pine, snow.Tree.Shape);
        Assert.Equal(r.BlockId("pine_leaves"), snow.Tree.Crown);
        Assert.Equal(r.BlockId("frozen_shrub"), snow.Decoration);
        Assert.Equal(new[] { r.BlockId("snow"), r.BlockId("ice") }, snow.Markers);
        Assert.Equal(0.15f, snow.Band!.WidthMin);
        Assert.Equal(6, set.EdgeNoise);
        Assert.Equal(4, set.Taper);
        Assert.Equal((60, 40, 200), (set.DetectWidth, set.DetectHeight, set.DetectThreshold));
    }

    public static TheoryData<string, string> StructuralErrors => new()
    {
        { "default-unknown", "default" },
        { "default-with-band", "band" },
        { "three-bands", "band" },
        { "one-band", "band" },
        { "unknown-shape", "shape" },
        { "height-inverted", "heightMin" },
        { "chance-out-of-range", "treeChance" },
        { "taper-zero", "taper" },
        { "band-too-wide", "widthMax" },
        { "unknown-field", "JSON" },
        { "duplicate-id", "repetido" },
        { "tree-missing", "tree" },
        { "spacing-zero", "treeSpacing" },
    };

    private static string Broken(string name) => name switch
    {
        "default-unknown" => Edit(r => r["default"] = "jungle"),
        "default-with-band" => Edit(r => Biome(r, "forest")["band"] = Biome(r, "snow")["band"]!.DeepClone()),
        "three-bands" => Edit(r =>
        {
            r["default"] = "snow";
            Biome(r, "snow").Remove("band");
            Biome(r, "forest")["band"] = Biome(r, "desert")["band"]!.DeepClone();
            var extra = Biome(r, "desert").DeepClone().AsObject();
            extra["id"] = "dunes";
            r["biomes"]!.AsArray().Add(extra);
        }),
        "one-band" => Edit(r => Biome(r, "desert").Remove("band")),
        "unknown-shape" => Edit(r => Biome(r, "snow")["tree"]!["shape"] = "palm"),
        "height-inverted" => Edit(r => Biome(r, "desert")["tree"]!["heightMin"] = 7),
        "chance-out-of-range" => Edit(r => Biome(r, "forest")["treeChance"] = 1.5),
        "taper-zero" => Edit(r => r["taper"] = 0),
        "band-too-wide" => Edit(r => Biome(r, "snow")["band"]!["widthMax"] = 0.6),
        "unknown-field" => Edit(r => r["foo"] = 1),
        "duplicate-id" => Edit(r => Biome(r, "desert")["id"] = "snow"),
        "tree-missing" => Edit(r => Biome(r, "forest").Remove("tree")),
        "spacing-zero" => Edit(r => Biome(r, "forest")["treeSpacing"] = 0),
        _ => throw new ArgumentException(name),
    };

    [Theory]
    [MemberData(nameof(StructuralErrors))]
    public void InvalidFile_ThrowsNamingFileAndField(string name, string expected)
    {
        var ex = Assert.Throws<DataException>(() => BiomeConfigLoader.Load(Broken(name)));

        Assert.Contains(BiomeConfigLoader.FileName, ex.Message);
        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("surfaceBlock", "granito")]
    [InlineData("shallowWall", "granito_wall")]
    [InlineData("decoration", "flor")]
    public void UnknownContent_ThrowsOnResolve(string field, string value)
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "snow")[field] = value));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains(field, ex.Message);
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void TrunkThatIsNotATreeBlock_ThrowsOnResolve()
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "forest")["tree"]!["trunk"] = "dirt"));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains("tree.trunk", ex.Message);
    }

    [Fact]
    public void MarkerInTwoBiomes_ThrowsOnResolve()
    {
        var config = BiomeConfigLoader.Load(Edit(r => Biome(r, "desert")["markers"]!.AsArray().Add("snow")));

        var ex = Assert.Throws<DataException>(() => BiomeSet.Resolve(config, TestData.Registry()));

        Assert.Contains("marcador", ex.Message);
    }

    [Fact]
    public void WorldGenConfig_FromDirectory_HasBiomes_AndMaterialsResolveThem()
    {
        var config = TestData.WorldGenConfig();

        Assert.NotNull(config.Biomes);
        Assert.Equal("forest", Zyon.Core.WorldGen.WorldGenMaterials.Resolve(config, TestData.Registry()).Biomes!.Default.Id);
        Assert.Null(Zyon.Core.WorldGen.WorldGenConfigLoader.Load("{}").Biomes);
    }
}
