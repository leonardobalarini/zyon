using Zyon.Core.Data;

namespace Zyon.Core.Persistence;

/// <summary>
/// Resultado de <see cref="SaveFiles.ReadWithBackup{T}"/>. Quando <see cref="FromBackup"/> é verdadeiro o save
/// principal estava corrompido e o progresso voltou ao backup: quem chama DEVE avisar o jogador.
/// <see cref="QuarantinedPath"/> é para onde o principal corrompido foi movido (null se não havia o que mover ou se a
/// movimentação falhou). <see cref="RecoveredFromTemp"/> indica que o principal não existia e o "arquivo.tmp" de uma
/// gravação interrompida no meio da troca foi lido e promovido a principal (é o save mais novo; não há perda).
/// </summary>
public readonly record struct LoadResult<T>(T Value, bool FromBackup, string? QuarantinedPath = null, bool RecoveredFromTemp = false);

/// <summary>
/// Não foi possível carregar o save (e, se for o caso, nem o backup). <see cref="Kind"/> diz o motivo.
/// Nenhum arquivo foi alterado.
/// </summary>
public sealed class SaveLoadException : SaveException
{
    public SaveLoadException(string filePath, SaveLoadFailure kind, Exception primaryError, Exception? backupError)
        : this(filePath, kind, primaryError, backupError,
            backupError is null ? " (sem backup disponível)" : $"; o backup também falhou: {backupError.Message}")
    {
    }

    private SaveLoadException(string filePath, SaveLoadFailure kind, Exception primaryError, Exception? backupError, string suffix)
        : base($"Não foi possível carregar '{filePath}': {primaryError.Message}{suffix}", primaryError)
    {
        FilePath = filePath;
        Kind = kind;
        BackupError = backupError;
    }

    /// <summary>O arquivo existe mas não pôde ser aberto (em uso por outro programa, sem permissão). O backup não foi tentado.</summary>
    internal static SaveLoadException Inaccessible(string filePath, Exception error) =>
        new(filePath, SaveLoadFailure.Inaccessible, error, null,
            " (o arquivo está em uso ou sem permissão; o backup não foi usado — tente de novo)");

    public string FilePath { get; }
    public Exception? BackupError { get; }
    public SaveLoadFailure Kind { get; }
}

/// <summary>Gravação atômica com backup e leitura com fallback. Caminhos são decididos por quem chama.</summary>
public static class SaveFiles
{
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";
    public const string CorruptSuffix = ".corrupt";
    public const string OrphanSuffix = ".orphan";

    /// <summary>Existe algo carregável para este save (principal, .tmp de recuperação ou .bak)?</summary>
    public static bool Exists(string path) =>
        File.Exists(path) || File.Exists(path + TempSuffix) || File.Exists(path + BackupSuffix);

    /// <summary>Apaga o save e todos os arquivos laterais (.tmp, .bak, .corrupt, .tmp.orphan).</summary>
    public static void Delete(string path)
    {
        foreach (string suffix in new[] { "", TempSuffix, BackupSuffix, CorruptSuffix, TempSuffix + OrphanSuffix })
        {
            string file = path + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>
    /// Nomes (sem extensão) dos saves carregáveis da pasta — principal, .tmp ou .bak — em ordem alfabética
    /// sem diferenciar maiúsculas. Arquivos só em quarentena (.corrupt) ou órfãos não aparecem.
    /// </summary>
    public static IReadOnlyList<string> List(string directory, string extension)
    {
        if (!Directory.Exists(directory)) return [];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string suffix in new[] { "", TempSuffix, BackupSuffix })
        {
            string ending = extension + suffix;
            foreach (string file in Directory.EnumerateFiles(directory, "*" + ending))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                    names.Add(fileName[..^ending.Length]);
            }
        }
        return names.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private const int MaxAttempts = 3;
    private const int RetryDelayMs = 100;

    /// <summary>
    /// Grava em "arquivo.tmp" e só então troca: o arquivo anterior vira "arquivo.bak". Uma falha no meio nunca corrompe
    /// o save atual. Gravações no mesmo caminho devem ser serializadas (uma única fila): duas ao mesmo tempo disputam o
    /// .tmp. Problemas de disco lançam IOException/UnauthorizedAccessException; exceções de <paramref name="write"/> sobem.
    /// </summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
            Directory.CreateDirectory(directory);

        string temp = path + TempSuffix;
        ClearStaleTemp(path, temp);
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
    /// Se o principal não existe mas há um "arquivo.tmp" legível (crash entre gravar o .tmp e trocar), ele é promovido
    /// a principal antes de se pensar no .bak; um .tmp ilegível fica onde está.
    /// Se principal e backup falharem, lança SaveLoadException sem alterar nada.
    /// <para>
    /// Contrato para o cliente: capture <see cref="SaveException"/>. <see cref="SaveIncompatibleException"/> = arquivo
    /// intacto mas inutilizável por esta versão do jogo; <see cref="SaveLoadException"/> = não carregou, e
    /// <see cref="SaveLoadException.Kind"/> diz o motivo (corrompido, ausente, inacessível). Qualquer outra exceção
    /// vinda de <paramref name="read"/> é bug e sobe inalterada, sem fallback nem quarentena.
    /// </para>
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

        if (primaryError is FileNotFoundException && TryRecoverTemp(path, read, out var recovered))
            return new LoadResult<T>(recovered, FromBackup: false, RecoveredFromTemp: true);

        string backup = path + BackupSuffix;
        if (!File.Exists(backup))
            throw new SaveLoadException(path, IsMissing(primaryError) ? SaveLoadFailure.Missing : SaveLoadFailure.Corrupt, primaryError, null);
        T value;
        try
        {
            value = ReadFile(backup, read);
        }
        catch (Exception ex) when (ex is DataException or IOException or UnauthorizedAccessException or SaveIncompatibleException)
        {
            var kind = IsMissing(primaryError) && IsMissing(ex) ? SaveLoadFailure.Missing : SaveLoadFailure.Corrupt;
            throw new SaveLoadException(path, kind, primaryError, ex);
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

    private static bool IsMissing(Exception ex) => ex is FileNotFoundException or DirectoryNotFoundException;

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

    /// <summary>
    /// Tira do caminho um .tmp de uma gravação anterior. Com o principal presente ele é lixo e é apagado; sem o principal
    /// ele pode ser a cópia mais nova (crash no meio da troca), então é preservado como "arquivo.tmp.orphan".
    /// </summary>
    private static void ClearStaleTemp(string path, string temp)
    {
        if (!File.Exists(temp))
            return;
        File.SetAttributes(temp, File.GetAttributes(temp) & ~FileAttributes.ReadOnly);
        if (File.Exists(path))
            File.Delete(temp);
        else
            File.Move(temp, temp + OrphanSuffix, overwrite: true);
    }

    private static bool TryRecoverTemp<T>(string path, Func<Stream, T> read, out T value)
    {
        value = default!;
        string temp = path + TempSuffix;
        if (!File.Exists(temp))
            return false;
        try
        {
            value = ReadFile(temp, read);
        }
        catch (Exception ex) when (ex is DataException or IOException or UnauthorizedAccessException or SaveIncompatibleException)
        {
            return false;
        }

        try
        {
            File.Move(temp, path);
        }
        catch (IOException)
        {
            // O conteúdo já foi lido; a próxima gravação preserva o .tmp como órfão.
        }
        catch (UnauthorizedAccessException)
        {
        }
        return true;
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
