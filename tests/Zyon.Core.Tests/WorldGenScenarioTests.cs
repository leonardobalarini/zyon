using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenScenarioTests
{
    [Fact]
    public void PlayerSpawnedOnGeneratedWorld_StandsStillAndCanWalk()
    {
        var registry = TestData.Registry();
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(800, 400, 123);
        var spawn = generated.PlayerSpawnCenter(registry.Physics);
        using var sim = new Simulation(registry, generated.Tiles);
        int player = sim.SpawnPlayer(spawn);

        for (int i = 0; i < 120; i++) sim.Step();
        Assert.True(sim.TryGetEntityView(player, out var idle));
        Assert.Equal(spawn.Y, idle.Position.Y, 3);
        Assert.Equal(spawn.X, idle.Position.X, 3);

        sim.SetInput(player, new PlayerInput(1f, false, false, 0, 0));
        for (int i = 0; i < 120; i++) sim.Step();
        Assert.True(sim.TryGetEntityView(player, out var walked));
        Assert.True(walked.Position.X > spawn.X, "o jogador não andou");
        Assert.InRange(walked.Position.Y, 0f, generated.Tiles.Height);
    }
}
