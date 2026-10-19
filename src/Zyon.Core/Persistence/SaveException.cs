namespace Zyon.Core.Persistence;

/// <summary>
/// Base dos erros de carga que o cliente deve mostrar ao jogador. <see cref="SaveFiles.ReadWithBackup{T}"/> só lança
/// subtipos desta classe para problemas de arquivo/dados; qualquer outra exceção é bug e deve subir.
/// </summary>
public abstract class SaveException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Motivo de uma <see cref="SaveLoadException"/>.</summary>
public enum SaveLoadFailure
{
    /// <summary>O conteúdo está corrompido (e o backup, se havia, também falhou).</summary>
    Corrupt,

    /// <summary>Não há save (nem backup) no caminho.</summary>
    Missing,

    /// <summary>O arquivo existe mas não pôde ser aberto (em uso, sem permissão); tentar de novo pode resolver.</summary>
    Inaccessible,
}
