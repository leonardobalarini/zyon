using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public sealed class DevLaunchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-dev-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Dev_CreatesPlayer_AndAlwaysRecreatesTheDevWorld()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        Directory.CreateDirectory(lib.WorldsDir);
        File.WriteAllText(lib.WorldPath("dev"), "velho");

        var req = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--dev", "--seed", "7"]));

        Assert.Equal("Dev", req.Player.Name);
        Assert.Equal("dev", req.WorldName);
        Assert.True(req.CreateNew);
        Assert.Equal(7, req.Seed);
        Assert.False(SaveFiles.Exists(lib.WorldPath("dev")));
        Assert.Contains("Dev", lib.Players());
    }

    [Fact]
    public void NamedWorld_LoadsWhenItExists_CreatesWhenNot()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();

        var first = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--world", "Casa", "--player", "Ana"]));
        Directory.CreateDirectory(lib.WorldsDir);
        File.WriteAllText(lib.WorldPath("Casa"), "existe");
        var second = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--world", "Casa", "--player", "Ana"]));

        Assert.True(first.CreateNew);
        Assert.False(second.CreateNew);
        Assert.Equal("Ana", second.Player.Name);
    }

    [Fact]
    public void NamedWorld_ExistenceCheckIgnoresCase()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        Directory.CreateDirectory(lib.WorldsDir);
        File.WriteAllText(Path.Combine(lib.WorldsDir, "Casa.zworld"), "existe");

        var req = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--world", "casa", "--player", "Ana"]));

        Assert.False(req.CreateNew);
    }
}
