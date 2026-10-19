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
        return text switch
        {
            "corrompido" => throw new DataException("teste: conteúdo corrompido"),
            "incompativel" => throw new SaveIncompatibleException("teste: feito por uma versão mais nova"),
            _ => text,
        };
    }

    private string CorruptPath => SavePath + SaveFiles.CorruptSuffix;

    private void Seed(string main, string backup)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(SavePath, main);
        File.WriteAllText(SavePath + SaveFiles.BackupSuffix, backup);
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
        Assert.Equal(SaveLoadFailure.Corrupt, ex.Kind);
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
        Assert.Equal(SaveLoadFailure.Missing, ex.Kind);
    }

    [Fact]
    public void FallbackThenTwoSaves_KeepsAGoodBackup()
    {
        Seed("corrompido", "good1");

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);
        Assert.Equal("good1", result.Value);
        Assert.True(result.FromBackup);
        Assert.Equal(CorruptPath, result.QuarantinedPath);

        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "good2"));
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "good3"));

        Assert.Equal("good3", File.ReadAllText(SavePath));
        Assert.Equal("good2", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
        Assert.Equal("corrompido", File.ReadAllText(CorruptPath));
    }

    [Fact]
    public void IncompatibleSave_DoesNotFallBack()
    {
        Seed("incompativel", "good1");

        Assert.Throws<SaveIncompatibleException>(() => SaveFiles.ReadWithBackup(SavePath, ReadText));

        Assert.Equal("incompativel", File.ReadAllText(SavePath));
        Assert.Equal("good1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
        Assert.False(File.Exists(CorruptPath));
    }

    [Fact]
    public void LockedFile_DoesNotFallBack()
    {
        Seed("good2", "good1");
        bool backupRead = false;

        using (new FileStream(SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var ex = Assert.Throws<SaveLoadException>(() => SaveFiles.ReadWithBackup(SavePath, s =>
            {
                backupRead = true;
                return ReadText(s);
            }));
            Assert.Null(ex.BackupError);
            Assert.IsAssignableFrom<IOException>(ex.InnerException);
            Assert.Equal(SaveLoadFailure.Inaccessible, ex.Kind);
        }

        Assert.False(backupRead);
        Assert.Equal("good2", File.ReadAllText(SavePath));
        Assert.Equal("good1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
        Assert.False(File.Exists(CorruptPath));
    }

    [Fact]
    public void StaleReadOnlyTemp_DoesNotBlockSaving()
    {
        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v0"));
        string temp = SavePath + SaveFiles.TempSuffix;
        File.WriteAllText(temp, "lixo de uma gravação interrompida");
        File.SetAttributes(temp, FileAttributes.ReadOnly);

        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.Equal("v0", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
        Assert.False(File.Exists(temp));
        Assert.False(File.Exists(temp + SaveFiles.OrphanSuffix));
    }

    private string TempPath => SavePath + SaveFiles.TempSuffix;

    [Fact]
    public void MissingMain_ValidTemp_RecoversTemp()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(TempPath, "good2");
        File.WriteAllText(SavePath + SaveFiles.BackupSuffix, "good1");

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("good2", result.Value);
        Assert.False(result.FromBackup);
        Assert.True(result.RecoveredFromTemp);
        Assert.Equal("good2", File.ReadAllText(SavePath));
        Assert.False(File.Exists(TempPath));
        Assert.Equal("good1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
    }

    [Fact]
    public void MissingMain_CorruptTemp_FallsBackToBackup()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(TempPath, "corrompido");
        File.WriteAllText(SavePath + SaveFiles.BackupSuffix, "good1");

        var result = SaveFiles.ReadWithBackup(SavePath, ReadText);

        Assert.Equal("good1", result.Value);
        Assert.True(result.FromBackup);
        Assert.False(result.RecoveredFromTemp);
        Assert.Equal("corrompido", File.ReadAllText(TempPath));
        Assert.False(File.Exists(SavePath));
    }

    [Fact]
    public void WriteWithMissingMain_KeepsExistingTempAsOrphan()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(TempPath, "talvez o save mais novo");
        File.SetAttributes(TempPath, FileAttributes.ReadOnly);

        SaveFiles.WriteAtomic(SavePath, s => WriteText(s, "v1"));

        Assert.Equal("v1", File.ReadAllText(SavePath));
        Assert.False(File.Exists(TempPath));
        Assert.Equal("talvez o save mais novo", File.ReadAllText(TempPath + SaveFiles.OrphanSuffix));
    }

    [Fact]
    public void LoadErrors_AreSaveExceptions()
    {
        Assert.True(typeof(SaveException).IsAssignableFrom(typeof(SaveLoadException)));
        Assert.True(typeof(SaveException).IsAssignableFrom(typeof(SaveIncompatibleException)));
    }

    [Fact]
    public void ReaderBug_PropagatesUnchanged_AndNothingIsQuarantined()
    {
        Seed("good2", "good1");

        Assert.Throws<NullReferenceException>(() =>
            SaveFiles.ReadWithBackup<string>(SavePath, _ => throw new NullReferenceException("bug no leitor")));

        Assert.Equal("good2", File.ReadAllText(SavePath));
        Assert.Equal("good1", File.ReadAllText(SavePath + SaveFiles.BackupSuffix));
        Assert.False(File.Exists(CorruptPath));
    }
}
