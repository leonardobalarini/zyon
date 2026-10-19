using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class PersistencePerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void SmallWorld_SavesAndLoadsQuickly()
    {
        var registry = TestData.Registry();
#if DEBUG
        var (width, height) = (1050, 300);
#else
        var (width, height) = WorldSizes.Dimensions(WorldSize.Small);
#endif
        var save = WorldSave.FromGenerated(new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(width, height, 77));

        using var ms = new MemoryStream();
        var sw = Stopwatch.StartNew();
        WorldFile.Write(ms, save, registry);
        var writeTime = sw.Elapsed;
        ms.Position = 0;
        sw.Restart();
        var loaded = WorldFile.Read(ms, registry);
        var readTime = sw.Elapsed;

        output.WriteLine($"Mundo {width}×{height}: {ms.Length / 1024.0:F0} KiB | gravar {writeTime.TotalMilliseconds:F0} ms | ler {readTime.TotalMilliseconds:F0} ms");
        Assert.Equal(TileWorldHash.Compute(save.Tiles), TileWorldHash.Compute(loaded.Tiles));
#if !DEBUG
        Assert.True(writeTime.TotalSeconds < 1, $"gravação levou {writeTime.TotalSeconds:F2} s");
        Assert.True(readTime.TotalSeconds < 1, $"leitura levou {readTime.TotalSeconds:F2} s");
#endif
    }
}
