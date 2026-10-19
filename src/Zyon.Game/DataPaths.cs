using Godot;

namespace Zyon.Game;

/// <summary>Pasta data/: "res://data" num jogo exportado, ou a pasta data/ do repositório em desenvolvimento.</summary>
public static class DataPaths
{
    public static string Resolve()
    {
        string exported = ProjectSettings.GlobalizePath("res://data");
        if (System.IO.Directory.Exists(exported)) return exported;
        string dev = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "data"));
        if (System.IO.Directory.Exists(dev)) return dev;
        throw new System.IO.DirectoryNotFoundException($"Pasta data/ não encontrada (procurei '{exported}' e '{dev}')");
    }
}
