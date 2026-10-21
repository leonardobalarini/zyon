using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

[Collection(PerformanceCollection.Name)]
public class WorldGenPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void SmallWorld_GeneratesUnder10Seconds()
    {
        var generator = new WorldGenerator(TestData.Registry(), TestData.WorldGenConfig());
#if DEBUG
        // Em Debug só garante que o mundo pequeno inteiro roda; a meta de tempo vale em Release.
        var (width, height) = (1050, 300);
#else
        var (width, height) = WorldSizes.Dimensions(WorldSize.Small);
#endif

        var sw = Stopwatch.StartNew();
        var world = generator.Generate(width, height, 2026);
        sw.Stop();

        output.WriteLine($"Mundo {width}×{height} gerado em {sw.Elapsed.TotalSeconds:F2} s | hash {TileWorldHash.Compute(world.Tiles):X16}");
#if !DEBUG
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"geração levou {sw.Elapsed.TotalSeconds:F2} s");
#endif
    }
}
