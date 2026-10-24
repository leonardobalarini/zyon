namespace Zyon.ArtGen.Tests;

/// <summary>Raiz do repositório: sobe a partir da pasta do teste até achar o Zyon.slnx.</summary>
public static class RepoPaths
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Zyon.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Zyon.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }
}
