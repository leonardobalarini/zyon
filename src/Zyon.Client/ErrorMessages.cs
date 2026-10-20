namespace Zyon.Client;

/// <summary>Textos mostrados ao jogador para erros e avisos de save.</summary>
public static class ErrorMessages
{
    public static string BackupWarning(string what, string name, string? quarantinedPath) =>
        $"O {what} '{name}' estava danificado; foi carregado o último backup."
        + (quarantinedPath is null ? "" : $" O arquivo danificado foi guardado em '{quarantinedPath}'.");
}
