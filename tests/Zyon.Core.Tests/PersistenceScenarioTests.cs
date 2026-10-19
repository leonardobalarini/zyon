using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public sealed class PersistenceScenarioTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-e2e-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void MineSaveQuitLoad_RestoresWorldAndCharacter()
    {
        var registry = TestData.Registry();
        var physics = registry.Physics;
        var generated = new WorldGenerator(registry, TestData.WorldGenConfig()).Generate(800, 400, 5);
        var world = WorldSave.FromGenerated(generated);
        var character = PlayerSave.New("Leo", registry);
        string worldPath = Path.Combine(dir, "mundo" + WorldFile.Extension);
        string playerPath = Path.Combine(dir, "Leo" + PlayerFile.Extension);

        int targetX = (int)generated.SpawnFeet.X + 1;
        int targetY = (int)generated.SpawnFeet.Y;
        ulong hashAtSave;
        System.Numerics.Vector2 positionAtSave;
        long tickAtSave;

        using (var sim = world.CreateSimulation(registry))
        {
            int player = character.SpawnInto(sim, world.PlayerPosition("Leo", physics));
            sim.SetInput(player, new PlayerInput(0f, false, true, targetX, targetY));
            for (int i = 0; i < 60; i++) sim.Step();
            sim.SetInput(player, default);
            for (int i = 0; i < 120; i++) sim.Step();
            Assert.Equal(0, sim.GetBlock(targetX, targetY));
            Assert.Contains(Enumerable.Range(0, 50), s => sim.GetInventory(player)![s].ItemId == registry.ItemId("dirt"));

            var worldToSave = WorldSave.Capture(sim, world.Info, new Dictionary<string, int> { ["Leo"] = player });
            var characterToSave = PlayerSave.Capture(sim, player, "Leo");
            SaveFiles.WriteAtomic(worldPath, s => WorldFile.Write(s, worldToSave, registry));
            SaveFiles.WriteAtomic(playerPath, s => PlayerFile.Write(s, characterToSave));
            hashAtSave = TileWorldHash.Compute(sim.Tiles);
            sim.TryGetEntityView(player, out var view);
            positionAtSave = view.Position;
            tickAtSave = sim.Tick;
        }

        var loadedWorld = SaveFiles.ReadWithBackup(worldPath, s => WorldFile.Read(s, registry)).Value;
        var loadedCharacter = SaveFiles.ReadWithBackup(playerPath, s => PlayerFile.Read(s, registry)).Value;
        using var sim2 = loadedWorld.CreateSimulation(registry);
        int player2 = loadedCharacter.SpawnInto(sim2, loadedWorld.PlayerPosition("Leo", physics));

        Assert.Equal(hashAtSave, TileWorldHash.Compute(sim2.Tiles));
        Assert.Equal(0, sim2.GetBlock(targetX, targetY));
        Assert.Equal(tickAtSave, sim2.Tick);
        Assert.True(sim2.TryGetEntityView(player2, out var restored));
        Assert.Equal(positionAtSave, restored.Position);
        Assert.Equal(generated.Seed, loadedWorld.Info.Seed);
        Assert.Contains(Enumerable.Range(0, 50), s => sim2.GetInventory(player2)![s].ItemId == registry.ItemId("dirt"));
    }
}
