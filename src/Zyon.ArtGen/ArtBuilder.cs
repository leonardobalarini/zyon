using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

public sealed record GeneratedArt(IReadOnlyDictionary<string, Canvas> Images, ArtManifest Manifest);

/// <summary>Gera toda a arte a partir de art/art.json e data/ (em memória), e grava em data/client/art/.</summary>
public static class ArtBuilder
{
    public const string SourcePath = "art/art.json";
    public const string OutputDir = "data/client/art";

    public static GeneratedArt Build(string repoRoot)
    {
        var registry = RegistryLoader.LoadFromDirectory(Path.Combine(repoRoot, "data"));
        var src = ArtSource.LoadFromFile(Path.Combine(repoRoot, SourcePath), registry);

        var tiles = TileGenerator.BuildBlocks(src, registry, out var tileRows);
        var walls = TileGenerator.BuildWalls(src, registry, out var wallRows);
        var merge = TileGenerator.BuildMerge(src, registry);
        var items = ItemIconGenerator.Build(src, registry, tiles, out var itemArts);
        var player = PlayerGenerator.Build(src, out var layers, out _);

        var manifest = new ArtManifest
        {
            Variations = src.Variations,
            Tiles = tileRows,
            Walls = wallRows,
            MergeOrder = [.. src.MergeOrder],
            Items = itemArts,
            Player = player,
        };
        ArtCatalog.Resolve(manifest, registry);   // a saída precisa passar na mesma validação do jogo

        var images = new Dictionary<string, Canvas>
        {
            [ArtManifest.TilesFile] = tiles,
            [ArtManifest.WallsFile] = walls,
            [ArtManifest.MergeFile] = merge,
            [ArtManifest.ItemsFile] = items,
        };
        foreach (var (layer, canvas) in layers) images[ArtManifest.PlayerFile(layer)] = canvas;
        return new GeneratedArt(images, manifest);
    }

    public static void Write(string repoRoot, GeneratedArt art)
    {
        string dir = Path.Combine(repoRoot, OutputDir);
        foreach (var (file, canvas) in art.Images)
        {
            string path = Path.Combine(dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Png.Encode(canvas));
        }
        File.WriteAllText(Path.Combine(dir, ArtManifest.FileName), art.Manifest.ToJson());
    }
}
