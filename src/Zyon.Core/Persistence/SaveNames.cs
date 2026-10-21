using System.Globalization;
using System.Text;
using Zyon.Core.Data;

namespace Zyon.Core.Persistence;

/// <summary>Validação de nomes que viram nome de arquivo (personagens e mundos).</summary>
public static class SaveNames
{
    public const int MaxLength = 24;

    private static readonly string[] DeviceSuffixes = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "¹", "²", "³"]; // ¹ ² ³ também são dispositivos no Windows

    private static readonly string[] ReservedNames =
        ["CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$", .. DeviceSuffixes.Select(s => "COM" + s), .. DeviceSuffixes.Select(s => "LPT" + s)];

    /// <summary>
    /// Devolve o nome normalizado (NFC, sem espaços nas pontas) ou lança DataException
    /// "&lt;prefix&gt;: &lt;subject&gt; ('name') ...". Use sempre o valor devolvido.
    /// </summary>
    public static string Validate(string? name, string subject, string prefix)
    {
        DataException Fail(string message) => new($"{prefix}: {message}");

        string trimmed;
        try
        {
            trimmed = (name ?? "").Normalize(NormalizationForm.FormC).Trim();
        }
        catch (ArgumentException)
        {
            throw Fail($"{subject} ('name') contém caracteres inválidos");
        }
        if (trimmed.Length == 0) throw Fail($"{subject} ('name') vazio");
        if (trimmed.Length > MaxLength) throw Fail($"{subject} ('name') com mais de {MaxLength} caracteres");
        if (trimmed.Any(char.IsControl)) throw Fail($"{subject} ('name') contém caracteres de controle");
        if (trimmed.Any(c => char.GetUnicodeCategory(c) == UnicodeCategory.Format))
            throw Fail($"{subject} ('name') contém caracteres invisíveis de formatação");
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0)
            throw Fail($"{subject} ('name') contém caracteres inválidos");
        if (trimmed.EndsWith('.')) throw Fail($"{subject} ('name') não pode terminar com ponto");
        string stem = trimmed.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            throw Fail($"{subject} ('name') '{trimmed}' é reservado pelo sistema");
        return trimmed;
    }
}
