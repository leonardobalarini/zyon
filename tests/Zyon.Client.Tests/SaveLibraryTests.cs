using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public sealed class SaveLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-lib-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Paths_UseValidatedNames_InTheirFolders()
    {
        var lib = new SaveLibrary(root);

        Assert.Equal(Path.Combine(root, "worlds", "Terra.zworld"), lib.WorldPath(" Terra "));
        Assert.Equal(Path.Combine(root, "players", "Ana.zplayer"), lib.PlayerPath("Ana"));
        Assert.Throws<DataException>(() => lib.WorldPath("a/b"));
    }

    [Fact]
    public void CreatePlayer_WritesFile_AndListsIt_AndRejectsDuplicates()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();

        var ana = lib.CreatePlayer("Ana", r);

        Assert.Equal("Ana", ana.Name);
        Assert.Equal(new[] { "Ana" }, lib.Players());
        Assert.Equal(ana.Slots, lib.LoadPlayer("Ana", r).Value.Slots);
        var ex = Assert.Throws<DataException>(() => lib.CreatePlayer("ana", r));
        Assert.Contains("já existe", ex.Message);
    }

    [Fact]
    public void Lists_AreEmpty_BeforeAnySave()
    {
        var lib = new SaveLibrary(root);

        Assert.Empty(lib.Worlds());
        Assert.Empty(lib.Players());
        Assert.False(lib.WorldExists("x"));
    }
}
