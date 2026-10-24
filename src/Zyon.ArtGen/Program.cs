using Zyon.ArtGen;
using Zyon.Core.Data;

string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
try
{
    var art = ArtBuilder.Build(root);
    ArtBuilder.Write(root, art);
    Console.WriteLine($"Arte gerada em {Path.Combine(root, ArtBuilder.OutputDir)} ({art.Images.Count} imagens)");
    return 0;
}
catch (DataException ex)
{
    Console.Error.WriteLine($"erro: {ex.Message}");
    return 1;
}
