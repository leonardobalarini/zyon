using Zyon.Core.Data;

namespace Zyon.Core.Persistence;

/// <summary>
/// Resultado de <see cref="SaveFiles.ReadWithBackup{T}"/>. Quando <see cref="FromBackup"/> é verdadeiro o save
/// principal estava corrompido e o progresso voltou ao backup: quem chama DEVE avisar o jogador.
/// <see cref="QuarantinedPath"/> é para onde o principal corrompido foi movido (null se não havia o que mover ou se a
/// movimentação falhou).
/// </summary>
public readonly record struct LoadResult<T>(T Value, bool FromBackup, string? QuarantinedPath = null);

/// <summary>Não foi possível carregar o save (e, se for o caso, nem o backup). Nenhum arquivo foi alterado.</summary>
public sealed class SaveLoadException : Exception
{
    public SaveLoadException(string filePath, Exception primaryError, Exception? backupError)
        : this(filePath, primaryError, backupError,
            backupError is null ? " (sem backup disponível)" : $"; o backup também falhou: {backupError.Message}")
    {
    }

    private SaveLoadException(string filePath, Exception primaryError, Exception? backupError, string suffix)
        : base($"Não foi possível carregar '{filePath}': {primaryError.Message}{suffix}", primaryError)
    {
        FilePath = filePath;
        BackupError = backupError;
    }

    /// <summary>O arquivo existe mas não pôde ser aberto (em uso por outro programa, sem permissão). O backup não foi tentado.</summary>
    internal static SaveLoadException Inaccessible(string filePath, Exception error) =>
        new(filePath, error, null, " (o arquivo está em uso ou sem permissão; o backup não foi usado — tente de novo)");

    public string FilePath { get; }
    public Exception? BackupError { get; }
}

/// <summary>Gravação atômica com backup e leitura com fallback. Caminhos são decididos por quem chama.</summary>
public static class SaveFiles
{
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";
    public const string CorruptSuffix = ".corrupt";

    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 100;

    /// <summary>Grava em "arquivo.tmp" e só então troca: o arquivo anterior vira "arquivo.bak". Uma falha no meio nunca corrompe o save atual.</summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
            Directory.CreateDirectory(directory);

        string temp = path + TempSuffix;
        DeleteStaleTemp(temp);
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

        Retry(() =>
        {
            if (File.Exists(path))
                File.Replace(temp, path, path + BackupSuffix, ignoreMetadataErrors: true);
            else
                File.Move(temp, path);
        });
    }

    /// <summary>
    /// Lê o arquivo. Só cai para o .bak se o principal estiver corrompido ou ausente; nesse caso o principal ruim é
    /// movido para "arquivo.corrupt", para que o próximo save não o transforme no .bak (o que apagaria o backup bom).
    /// Save incompatível (SaveIncompatibleException) propaga sem tocar no backup. Arquivo em uso/sem permissão é
    /// tentado de novo algumas vezes e então vira SaveLoadException, também sem usar o backup.
    /// Se principal e backup falharem, lança SaveLoadException sem alterar nada.
    /// </summary>
    public static LoadResult<T> ReadWithBackup<T>(string path, Func<Stream, T> read)
    {
        Exception primaryError;
        try
        {
            return new LoadResult<T>(ReadFile(path, read), FromBackup: false);
        }
        catch (Exception ex) when (IsCorruption(ex))
        {
            primaryError = ex;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw SaveLoadException.Inaccessible(path, ex);
        }

        string backup = path + BackupSuffix;
        if (!File.Exists(backup))
            throw new SaveLoadException(path, primaryError, null);
        T value;
        try
        {
            value = ReadFile(backup, read);
        }
        catch (Exception ex) when (ex is DataException or IOException or UnauthorizedAccessException or SaveIncompatibleException)
        {
            throw new SaveLoadException(path, primaryError, ex);
        }

        return new LoadResult<T>(value, FromBackup: true, Quarantine(path));
    }

    /// <summary>Abre e lê; erros de acesso que não indicam corrupção (arquivo em uso) são tentados de novo.</summary>
    private static T ReadFile<T>(string path, Func<Stream, T> read)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return read(file);
            }
            catch (IOException ex) when (!IsCorruption(ex) && attempt < MaxAttempts)
            {
                Thread.Sleep(RetryDelayMs * attempt);
            }
        }
    }

    private static bool IsCorruption(Exception ex) =>
        ex is DataException or FileNotFoundException or DirectoryNotFoundException or EndOfStreamException;

    private static string? Quarantine(string path)
    {
        if (!File.Exists(path))
            return null;
        string target = path + CorruptSuffix;
        try
        {
            File.Move(path, target, overwrite: true);
            return target;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void DeleteStaleTemp(string temp)
    {
        if (!File.Exists(temp))
            return;
        File.SetAttributes(temp, File.GetAttributes(temp) & ~FileAttributes.ReadOnly);
        File.Delete(temp);
    }

    private static void Retry(Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < MaxAttempts)
            {
                Thread.Sleep(RetryDelayMs * attempt);
            }
        }
    }

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
