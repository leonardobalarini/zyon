using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>Textos mostrados ao jogador para erros e avisos de save.</summary>
public static class ErrorMessages
{
    public static string BackupWarning(string what, string name, string? quarantinedPath) =>
        $"O {what} '{name}' estava danificado; foi carregado o último backup."
        + (quarantinedPath is null ? "" : $" O arquivo danificado foi guardado em '{quarantinedPath}'.");

    private static string Friendly(string message)
    {
        foreach (string prefix in new[] { ".zworld: ", ".zplayer: " })
            if (message.StartsWith(prefix, StringComparison.Ordinal))
            {
                message = message[prefix.Length..];
                break;
            }
        return message.Replace("('name') ", "");
    }

    public static string Describe(Exception error)
    {
        if (error is AggregateException { InnerExceptions.Count: 1 } aggregate)
            error = aggregate.InnerExceptions[0];
        return error switch
        {
            SaveIncompatibleException e =>
                $"Este save é de uma versão mais nova do jogo ou usa conteúdo que foi removido. ({e.Message})",
            SaveLoadException { Kind: SaveLoadFailure.Inaccessible } =>
                "O arquivo está em uso ou sem permissão. Feche outros programas que possam estar com ele aberto e tente de novo.",
            SaveLoadException { Kind: SaveLoadFailure.Missing } =>
                "O save não foi encontrado.",
            SaveLoadException e =>
                $"O save está corrompido e não havia backup utilizável. ({e.Message})",
            IOException =>
                $"Não foi possível ler ou gravar no disco (espaço, permissão ou arquivo em uso). Detalhe: {error.Message}",
            UnauthorizedAccessException =>
                $"Sem permissão para acessar a pasta ou o arquivo de save. Detalhe: {error.Message}",
            DataException e => Friendly(e.Message),
            OperationCanceledException => "Cancelado.",
            _ => $"Erro inesperado: {error.GetType().Name}: {error.Message}",
        };
    }
}
