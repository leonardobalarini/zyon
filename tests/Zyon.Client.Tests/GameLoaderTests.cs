using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Client.Tests;

public sealed class GameLoaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-loader-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static WorldGenConfig Config() => WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir);

    [Fact]
    public void CreateWorld_GeneratesWritesAndStartsTheGame()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var progress = new LatestProgress<Zyon.Core.Sim.WorldGenProgress>();
        var req = new LaunchRequest(player, [], "Terra", true, 256, 128, 5);

        var game = GameLoader.Run(lib, r, Config(), req, progress, CancellationToken.None);
        using var _ = game.Simulation;

        Assert.True(File.Exists(lib.WorldPath("Terra")));
        Assert.Equal(1f, progress.Latest!.Fraction);
        Assert.True(game.Simulation.TryGetEntityView(game.PlayerId, out var me));
        Assert.Equal(game.Info.SpawnFeet.Y - r.Physics.PlayerHeight / 2, me.Position.Y);
        Assert.Equal("Ana", game.PlayerName);
        Assert.Empty(game.Warnings);
    }

    [Fact]
    public void LoadWorld_RestoresTheSavedWorld()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var created = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 256, 128, 5), null, CancellationToken.None);
        ulong hash = TileWorldHash.Compute(created.Simulation.Tiles);
        created.Simulation.Dispose();

        var loaded = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, ["aviso do personagem"], "Terra", false, 0, 0, 0), null, CancellationToken.None);
        using var _ = loaded.Simulation;

        Assert.Equal(hash, TileWorldHash.Compute(loaded.Simulation.Tiles));
        Assert.Equal(new[] { "aviso do personagem" }, loaded.Warnings);
    }

    [Fact]
    public void CreateWorld_WithExistingName_Throws()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, CancellationToken.None).Simulation.Dispose();

        var ex = Assert.Throws<Zyon.Core.Data.DataException>(() =>
            GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "terra", true, 128, 128, 5), null, CancellationToken.None));
        Assert.Contains("já existe", ex.Message);
    }

    [Fact]
    public void CreateWorld_Cancelled_ThrowsAndWritesNothing()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, cts.Token));
        Assert.False(SaveFiles.Exists(lib.WorldPath("Terra")));
    }

    [Fact]
    public void LoadWorld_FromBackup_AddsWarning()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, CancellationToken.None).Simulation.Dispose();
        string path = lib.WorldPath("Terra");
        File.Copy(path, path + SaveFiles.BackupSuffix);
        File.WriteAllText(path, "lixo");

        var game = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", false, 0, 0, 0), null, CancellationToken.None);
        using var _ = game.Simulation;

        var warning = Assert.Single(game.Warnings);
        Assert.Contains("backup", warning);
    }
}
