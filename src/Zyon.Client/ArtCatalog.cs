using Zyon.Core.Data;

namespace Zyon.Client;

/// <summary>Manifesto de arte resolvido por id. Toda peça de conteúdo dos dados precisa de arte.</summary>
public sealed class ArtCatalog
{
    private ArtCatalog(ArtManifest manifest, string directory, int[] tileRows, int[] wallRows, int[] mergeRanks, ItemArt?[] items)
    {
        Manifest = manifest;
        Directory = directory;
        TileRowByBlockId = tileRows;
        WallRowByWallId = wallRows;
        MergeRankByBlockId = mergeRanks;
        ItemByItemId = items;
    }

    public ArtManifest Manifest { get; }

    /// <summary>Pasta dos PNGs (vazia se resolvido sem disco).</summary>
    public string Directory { get; }

    public IReadOnlyList<int> TileRowByBlockId { get; }
    public IReadOnlyList<int> WallRowByWallId { get; }

    /// <summary>Posição na mergeOrder (menor = mais mole); −1 = não mescla.</summary>
    public IReadOnlyList<int> MergeRankByBlockId { get; }

    public IReadOnlyList<ItemArt?> ItemByItemId { get; }

    public string PathOf(string file) => Path.Combine(Directory, file.Replace('/', Path.DirectorySeparatorChar));

    public static ArtCatalog LoadFromDirectory(string dataDir, Registry registry)
    {
        string dir = Path.Combine(dataDir, "client", "art");
        string path = Path.Combine(dir, ArtManifest.FileName);
        if (!File.Exists(path)) throw new DataException($"{ArtManifest.FileName}: arquivo não encontrado em '{path}' (rode tools/gen-art.sh)");
        var catalog = Resolve(ArtManifest.FromJson(File.ReadAllText(path)), registry, dir);
        var files = new List<string> { ArtManifest.TilesFile, ArtManifest.WallsFile, ArtManifest.MergeFile, ArtManifest.ItemsFile };
        files.AddRange(catalog.Manifest.Player.Layers.Select(ArtManifest.PlayerFile));
        files.Add(ArtManifest.PlayerFile(ArtManifest.ArmLayer));
        foreach (var f in files)
            if (!File.Exists(catalog.PathOf(f))) throw new DataException($"arte: arquivo '{catalog.PathOf(f)}' não encontrado");
        return catalog;
    }

    public static ArtCatalog Resolve(ArtManifest m, Registry registry, string directory = "")
    {
        static DataException Fail(string message) => new($"{ArtManifest.FileName}: {message}");

        var tileRows = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        foreach (var (name, row) in m.Tiles)
            tileRows[registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' não existe")] = row;
        for (int id = 1; id < tileRows.Length; id++)
            if (tileRows[id] < 0) throw Fail($"falta a arte do bloco '{registry.Block((ushort)id).Name}'");

        var wallRows = Enumerable.Repeat(-1, registry.Walls.Count).ToArray();
        foreach (var (name, row) in m.Walls)
            wallRows[registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' não existe")] = row;
        for (int id = 1; id < wallRows.Length; id++)
            if (wallRows[id] < 0) throw Fail($"falta a arte da parede '{registry.Wall((ushort)id).Name}'");

        var ranks = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        for (int i = 0; i < m.MergeOrder.Count; i++)
        {
            string name = m.MergeOrder[i];
            if (!registry.TryGetBlockId(name, out var id)) throw Fail($"mergeOrder: bloco '{name}' não existe");
            if (!registry.Block(id).Solid) throw Fail($"mergeOrder: bloco '{name}' não é sólido");
            if (ranks[id] >= 0) throw Fail($"mergeOrder: bloco '{name}' repetido");
            ranks[id] = i;
        }

        var items = new ItemArt?[registry.Items.Count];
        foreach (var (name, art) in m.Items)
            items[registry.TryGetItemId(name, out var id) ? id : throw Fail($"item '{name}' não existe")] = art;
        for (int id = 1; id < items.Length; id++)
            if (items[id] is null) throw Fail($"falta a arte do item '{registry.Item((ushort)id).Name}'");

        var p = m.Player;
        if (p.Hand.Count != p.ArmFrames || p.Hand.Any(h => h.Length != 2))
            throw Fail($"player.hand precisa de {p.ArmFrames} pontos (x, y)");
        if (p.ArmAngleDeg.Count != p.ArmFrames) throw Fail($"player.armAngleDeg precisa de {p.ArmFrames} valores");
        if (p.SwingFirst + p.SwingCount > p.ArmFrames || p.WalkFirst + p.WalkCount > p.BodyFrames || p.Air >= p.BodyFrames)
            throw Fail("player: índices de quadro fora das tiras");

        return new ArtCatalog(m, directory, tileRows, wallRows, ranks, items);
    }
}
