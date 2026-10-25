using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class FingerprintTests
{
    [Fact]
    public void SameConfig_SameFingerprint()
    {
        Assert.Equal(
            WorldGenConfigFingerprint.Compute(TestData.WorldGenConfig()),
            WorldGenConfigFingerprint.Compute(TestData.WorldGenConfig()));
    }

    [Fact]
    public void ChangedParameter_ChangesFingerprint()
    {
        var a = WorldGenConfigFingerprint.Compute(WorldGenConfigLoader.Load("{}"));
        var b = WorldGenConfigFingerprint.Compute(WorldGenConfigLoader.Load("""{"caveThreshold": 0.4}"""));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BunkerTemplate_IsPartOfTheFingerprint()
    {
        var withBunker = TestData.WorldGenConfig();
        var withoutBunker = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")));

        Assert.NotEqual(WorldGenConfigFingerprint.Compute(withoutBunker), WorldGenConfigFingerprint.Compute(withBunker));
    }

    private static ulong FingerprintWithBunkerText(string text)
    {
        var config = WorldGenConfigLoader.Load("{}");
        config.Bunker = Zyon.Core.WorldGen.Structures.StructureLoader.Load("bunker", text);
        return WorldGenConfigFingerprint.Compute(config);
    }

    [Fact]
    public void BunkerTemplate_FingerprintIgnoresLineEndings()
    {
        var lf = File.ReadAllText(Path.Combine(TestData.DataDir, "structures", "bunker.json")).ReplaceLineEndings("\n");
        var crlf = lf.Replace("\n", "\r\n");

        Assert.Equal(FingerprintWithBunkerText(lf), FingerprintWithBunkerText(crlf));
    }

    [Fact]
    public void BunkerTemplate_DifferentContent_DifferentFingerprint()
    {
        var text = File.ReadAllText(Path.Combine(TestData.DataDir, "structures", "bunker.json")).ReplaceLineEndings("\n");
        var changed = text.Replace("\"HH........#..........#", "\"HH.C......#..........#");
        Assert.NotEqual(text, changed);

        Assert.NotEqual(FingerprintWithBunkerText(text), FingerprintWithBunkerText(changed));
    }

    [Fact]
    public void GeneratedWorld_CarriesTheFingerprint()
    {
        var config = TestData.WorldGenConfig();
        var world = new WorldGenerator(TestData.Registry(), config).Generate(128, 128, 1);

        Assert.Equal(WorldGenConfigFingerprint.Compute(config), world.ConfigFingerprint);
    }
}
