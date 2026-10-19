using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public sealed class SaveFilesTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-saves-" + Guid.NewGuid());
    private string SavePath => Path.Combine(dir, "mundo.zworld");

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static void WriteText(Stream s, string text) => s.Write(Encoding.UTF8.GetBytes(text));

    /// <summary>Leitor de teste: falha com DataException se o conteúdo for "corrompido".</summary>
    private static string ReadText(Stream s)
    {
        string text = new StreamReader(s).ReadToEnd();
        return text == "corrompido" ? throw new DataException("teste: conteúdo corrompido") : text;
    }

    [Fact]
    public void WriteAtomic_CreatesFileAndDirectory_LeavesNoTemp()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + SaveFiles.TempSuffix));
        Assert.False(File.Exists(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void SecondWrite_KeepsPreviousAsBackup()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v2"));

        Assert.Equal("v2", File.ReadAllText(SavePath));
        Assert.Equal("v1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void FailingWriter_LeavesOriginalUntouched()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Throws<InvalidOperationException>(() => SaveFiles.WriteAtomic(SavePath, s =>
        {
            WriteText(s, "meio");
            throw new InvalidOperationException("falha no meio da gravação");
        }));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.False(File.Exists(SavePath + SaveFiles.TempSuffix));
    }

    [Fact]
    public void ReadWithBackup_ReadsMainFile()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("v1", result.Value);
        Assert.False(result.FromBackup);
    }

    [Fact]
    public void CorruptedMain_FallsBackToBackup()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "corrompido"));

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("v1", result.Value);
        Assert.True(result.FromBackup);
    }

    [Fact]
    public void BothCorrupted_ThrowsAndNeverTouchesFiles()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(SavePath, "corrompido");
        File.WriteAllText(SavePath + SaveFiles.BackupSuffix, "corrompido");

        var ex = Assert.Throws<SaveLoadException>(() => SaveFiles.ReadWithBackup(SavePath, ReadText));

        Assert.Equal(SavePath, ex.FilePath);
        Assert.NotNull(ex.BackupError);
        Assert.Equal("corrompido", File.ReadAllText(SavePath));
        Assert.Equal("corrompido", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void MissingFileWithoutBackup_Throws()
    {
        var ex = Assert.Throws<SaveLoadException>(() => SaveFiles.ReadWithBackup(SavePath, ReadText));

        Assert.IsAssignableFrom<IOException>(ex.InnerException);
        Assert.Null(ex.BackupError);
    }
}
