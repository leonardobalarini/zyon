using Zyon.Core.Data;

namespace Zyon.Core.Persistence;

public readonly record struct LoadResult<T>(T Value, bool FromBackup);

/// <summary>O save principal e o backup estão ilegíveis. Nenhum arquivo foi alterado.</summary>
public sealed class SaveLoadException(string filePath, Exception primaryError, Exception? backupError)
    : Exception(
        $"Não foi possível carregar '{filePath}': {primaryError.Message}"
        + (backupError is null ? " (sem backup disponível)" : $"; o backup também falhou: {backupError.Message}"),
        primaryError)
{
    public string FilePath { get; } = filePath;
    public Exception? BackupError { get; } = backupError;
}

/// <summary>Gravação atômica com backup e leitura com fallback. Caminhos são decididos por quem chama.</summary>
public static class SaveFiles
{
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";

    /// <summary>Grava em "arquivo.tmp" e só então troca: o arquivo anterior vira "arquivo.bak". Uma falha no meio nunca corrompe o save atual.</summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
            Directory.CreateDirectory(directory);

        string temp = path + TempSuffix;
        try
        {
            using var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None);
            write(file);
            file.Flush(flushToDisk: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        if (File.Exists(path))
            File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
        else
            File.Move(temp, path);
    }

    /// <summary>Lê o arquivo; se falhar, tenta o .bak. Se ambos falharem, lança SaveLoadException sem alterar nada.</summary>
    public static LoadResult<T> ReadWithBackup<T>(string path, Func<Stream, T> read)
    {
        Exception primaryError;
        try
        {
            return new LoadResult<T>(ReadFile(path, read), FromBackup: false);
        }
        catch (Exception ex) when (IsLoadError(ex))
        {
            primaryError = ex;
        }

        string backup = path + BackupSuffix;
        if (!File.Exists(backup))
            throw new SaveLoadException(path, primaryError, null);
        try
        {
            return new LoadResult<T>(ReadFile(backup, read), FromBackup: true);
        }
        catch (Exception ex) when (IsLoadError(ex))
        {
            throw new SaveLoadException(path, primaryError, ex);
        }
    }

    private static T ReadFile<T>(string path, Func<Stream, T> read)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return read(file);
    }

    private static bool IsLoadError(Exception ex) => ex is DataException or IOException or UnauthorizedAccessException;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
