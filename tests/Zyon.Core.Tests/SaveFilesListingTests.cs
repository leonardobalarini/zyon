using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public sealed class SaveFilesListingTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-list-" + Guid.NewGuid());

    public SaveFilesListingTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private void Touch(string file) => File.WriteAllText(Path.Combine(dir, file), "x");

    [Fact]
    public void List_ReturnsBaseNames_IncludingRecoverableOnes_SortedIgnoringCase()
    {
        Touch("beta.zworld");
        Touch("beta.zworld.bak");
        Touch("Alfa.zworld");
        Touch("gama.zworld.tmp");        // só .tmp: recuperável
        Touch("delta.zworld.bak");       // só .bak: recuperável
        Touch("eco.zworld.corrupt");     // só quarentena: não lista
        Touch("leo.zplayer");            // outra extensão

        Assert.Equal(new[] { "Alfa", "beta", "delta", "gama" }, SaveFiles.List(dir, ".zworld"));
    }

    [Fact]
    public void List_MissingDirectory_IsEmpty()
    {
        Assert.Empty(SaveFiles.List(Path.Combine(dir, "nao-existe"), ".zworld"));
    }

    [Fact]
    public void Exists_TrueForMainTempOrBackup()
    {
        string path = Path.Combine(dir, "m.zworld");
        Assert.False(SaveFiles.Exists(path));
        Touch("m.zworld.bak");
        Assert.True(SaveFiles.Exists(path));
    }

    [Fact]
    public void Delete_RemovesMainAndAllSidecars()
    {
        foreach (var f in new[] { "m.zworld", "m.zworld.bak", "m.zworld.tmp", "m.zworld.corrupt", "m.zworld.tmp.orphan" })
            Touch(f);
        Touch("outro.zworld");

        SaveFiles.Delete(Path.Combine(dir, "m.zworld"));

        Assert.Equal(new[] { "outro.zworld" }, Directory.GetFiles(dir).Select(Path.GetFileName));
    }
}
