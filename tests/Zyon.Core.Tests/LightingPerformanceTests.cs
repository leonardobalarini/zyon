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
    public void VisibleRegion_ComputesUnderOneAndAHalfMilliseconds()
    {
        var registry = TestData.Registry();
        var config = TestData.LightingConfig();
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(1050, 300, 3);
        ushort torch = registry.BlockId("torch");
        int fx = (int)generated.SpawnFeet.X, fy = (int)generated.SpawnFeet.Y;
        for (int i = -20; i <= 20; i += 5)
            if (generated.Tiles.GetBlock(fx + i, fy + 6) == 0) generated.Tiles.SetBlock(fx + i, fy + 6, torch);
        using var view = new Simulation(registry, generated.Tiles);
        // Zoom padrão (ScreenScale, um pixel da arte por pixel da tela) em 1440p: 160×90 tiles. Em 1080p são 120×67.
        var visible = new TileRect(fx - 80, fy - 45, fx + 79, fy + 44);
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
        Assert.True(avgMs < 1.5, $"cálculo de luz levou {avgMs:F3} ms");
#endif
    }
}
