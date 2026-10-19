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
    public void GeneratedWorld_CarriesTheFingerprint()
    {
        var config = TestData.WorldGenConfig();
        var world = new WorldGenerator(TestData.Registry(), config).Generate(128, 128, 1);

        Assert.Equal(WorldGenConfigFingerprint.Compute(config), world.ConfigFingerprint);
    }
}
