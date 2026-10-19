using System.Diagnostics;
using System.Numerics;
using Xunit.Abstractions;
using Zyon.Core.Items;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void Tick_With5000DroppedItems_Under2ms()
    {
        var r = TestData.Registry();
        var world = new TileWorld(420, 80);
        ushort stone = r.BlockId("stone");
        var floors = Enumerable.Range(0, 25).Select(k => 3 + 3 * k).ToArray();   // linhas 3, 6, ..., 75
        foreach (int y in floors)
            for (int x = 0; x < world.Width; x++)
                world.SetBlock(x, y, stone);

        using var sim = new Simulation(r, world);
        sim.SpawnPlayer(new Vector2(2f, 3f - r.Physics.PlayerHeight / 2));
        var dirt = new ItemStack(r.ItemId("dirt"), 1);
        float half = r.Physics.ItemSize / 2;
        foreach (int y in floors)
            for (int i = 0; i < 200; i++)
                sim.SpawnDroppedItem(dirt, new Vector2(5.5f + 2 * i, y - half), Vector2.Zero, 0);

        for (int i = 0; i < 60; i++) sim.Step();   // aquecimento (JIT, assentamento)

        const int measured = 300;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < measured; i++) sim.Step();
        sw.Stop();
        double avgMs = sw.Elapsed.TotalMilliseconds / measured;

        var entities = new List<Zyon.Core.Entities.EntityView>();
        output.WriteLine($"Entidades: {sim.CopyEntities(entities)} | média por tick: {avgMs:F3} ms");
        Assert.Equal(5001, entities.Count);
#if !DEBUG
        Assert.True(avgMs < 2.0, $"tick médio de {avgMs:F3} ms excede 2 ms");
#endif
    }
}
