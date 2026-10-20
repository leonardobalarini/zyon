using System.Numerics;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Client.Tests;

public sealed class GameSaverTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-saver-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private (SaveLibrary Lib, LoadedGame Game) NewGame()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var game = GameLoader.Run(lib, r, WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir),
            new LaunchRequest(player, [], "Terra", true, 256, 128, 9), null, CancellationToken.None);
        return (lib, game);
    }

    [Fact]
    public void Save_WritesWorldAndPlayer_ThatLoadBackIdentical()
    {
        var (lib, game) = NewGame();
        var r = game.Simulation.Registry;
        using var saver = new GameSaver(r, game);
        using var sim = game.Simulation;
        var (x, y) = ((int)game.Info.SpawnFeet.X + 1, (int)game.Info.SpawnFeet.Y);
        sim.SetInput(game.PlayerId, new PlayerInput(0f, false, true, x, y));
        for (int i = 0; i < 90; i++) sim.Step();

        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));

        Assert.Null(saver.LastError);
        Assert.Equal(1, saver.CompletedSaves);
        var world = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, r)).Value;
        Assert.Equal(TileWorldHash.Compute(sim.Tiles), TileWorldHash.Compute(world.Tiles));
        Assert.Equal(sim.Tick, world.Tick);
        Assert.True(world.PlayerPositions.ContainsKey("Ana"));
        var player = lib.LoadPlayer("Ana", r).Value;
        Assert.Equal(PlayerSave.Capture(sim, game.PlayerId, "Ana").Slots, player.Slots);
    }

    [Fact]
    public void ConsecutiveSaves_AreWrittenInOrder_AndKeepABackup()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;

        saver.Save(sim);
        for (int i = 0; i < 10; i++) sim.Step();
        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));

        var latest = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, sim.Registry)).Value;
        Assert.Equal(10, latest.Tick);
        Assert.True(File.Exists(game.WorldPath + SaveFiles.BackupSuffix));
        Assert.Equal(2, saver.CompletedSaves);
    }

    [Fact]
    public void KeepsPositionsOfCharactersNotInThisSession()
    {
        var (_, game) = NewGame();
        var known = new Dictionary<string, Vector2> { ["Bia"] = new Vector2(3f, 4f) };
        game = game with { KnownPositions = known };
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;

        saver.Save(sim);
        saver.Flush(TimeSpan.FromSeconds(10));

        var world = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, sim.Registry)).Value;
        Assert.Equal(new Vector2(3f, 4f), world.PlayerPositions["Bia"]);
        Assert.True(world.PlayerPositions.ContainsKey("Ana"));
    }

    [Fact]
    public void WriteFailure_IsReported_NotThrown()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;
        File.SetAttributes(game.WorldPath, FileAttributes.ReadOnly);
        try
        {
            saver.Save(sim);
            saver.Flush(TimeSpan.FromSeconds(10));

            Assert.NotNull(saver.LastError);
            Assert.Equal(0, saver.CompletedSaves);
        }
        finally
        {
            File.SetAttributes(game.WorldPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void UnexpectedWriteFailure_IsReportedAsUnexpected_AndNextSaveRecovers()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;
        var original = saver.WriteFile;
        saver.WriteFile = (_, _) => throw new InvalidOperationException("bug");

        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));

        Assert.IsType<InvalidOperationException>(saver.LastError);
        Assert.IsType<InvalidOperationException>(saver.UnexpectedError);
        Assert.Equal(0, saver.CompletedSaves);

        saver.WriteFile = original;
        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));
        Assert.Null(saver.LastError);
        Assert.Equal(1, saver.CompletedSaves);
        Assert.IsType<InvalidOperationException>(saver.UnexpectedError);
    }

    [Fact]
    public void IoFailure_IsNotUnexpected()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;
        saver.WriteFile = (_, _) => throw new IOException("disk");
        saver.Save(sim);
        saver.Flush(TimeSpan.FromSeconds(10));
        Assert.IsType<IOException>(saver.LastError);
        Assert.Null(saver.UnexpectedError);
    }

    [Fact]
    public void Flush_AndDispose_NeverThrow_AndSaveAfterDisposeThrows()
    {
        var (_, game) = NewGame();
        var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;
        saver.WriteFile = (_, _) => throw new InvalidOperationException("bug");
        saver.Save(sim);

        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));
        saver.Dispose();
        saver.Dispose();
        Assert.Throws<ObjectDisposedException>(() => saver.Save(sim));
    }
}
