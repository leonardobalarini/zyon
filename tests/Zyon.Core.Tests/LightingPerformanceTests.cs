using System.Diagnostics;
using Xunit.Abstractions;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

[Collection(PerformanceCollection.Name)]
public class LightingPerformanceTests(ITestOutputHelper output)
{
    // Mesmo valor de Zyon.Client.LightRecomputePolicy.RegionSlack (o Core não referencia o Client): o jogo calcula
    // a área visível + regionMargin + essa folga.
    private const int RegionSlack = 8;

    [Fact]
    [Trait("Category", "Performance")]
    public void VisibleRegion_ComputesUnderOneMillisecond()
    {
        var registry = TestData.Registry();
        var config = TestData.LightingConfig();
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(1050, 300, 3);
        ushort torch = registry.BlockId("torch");
        int fx = (int)generated.SpawnFeet.X, fy = (int)generated.SpawnFeet.Y;
        for (int i = -20; i <= 20; i += 5)
            if (generated.Tiles.GetBlock(fx + i, fy + 6) == 0) generated.Tiles.SetBlock(fx + i, fy + 6, torch);
        using var view = new Simulation(registry, generated.Tiles);
        var visible = new TileRect(fx - 20, fy - 11, fx + 19, fy + 11);   // 40×23 tiles = 640×360 px
        var region = visible.Expand(config.RegionMargin + RegionSlack).ClampTo(view.Width, view.Height);
        var engine = new LightEngine(config);
        var map = new LightMap();

        for (int i = 0; i < 20; i++) engine.Compute(view, region, 1f, [new LightEmitter(fx, fy - 1, 0.3f)], map);
        const int runs = 200;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++) engine.Compute(view, region, 1f, [new LightEmitter(fx, fy - 1, 0.3f)], map);
        double avgMs = sw.Elapsed.TotalMilliseconds / runs;

        output.WriteLine($"Região {region.Width}×{region.Height}: {avgMs:F3} ms por cálculo, {engine.LastRounds} rodadas");
#if !DEBUG
        Assert.True(avgMs < 1.0, $"cálculo de luz levou {avgMs:F3} ms");
#endif
    }
}
