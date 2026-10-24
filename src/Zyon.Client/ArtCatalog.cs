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

    /// <summary>
    /// Tamanho que o PNG precisa ter para o manifesto (ver "Formatos" no plano 6). Folhas de células 16×16 podem ter
    /// linhas/células sobrando, mas a dimensão livre é múltipla de 16; as tiras do jogador têm tamanho exato.
    /// </summary>
    public ArtImageSize ExpectedSize(string file)
    {
        const int s = TileAtlasLayout.TileSize;
        var m = Manifest;
        var p = m.Player;
        if (file == ArtManifest.TilesFile)
            return ArtImageSize.Sheet(AutoTile.MaskCount * m.Variations * s, exactWidth: true, (MaxOrMinus1(m.Tiles.Values) + 1) * s);
        if (file == ArtManifest.WallsFile)
            return ArtImageSize.Sheet(AutoTile.MaskCount * m.Variations * s, exactWidth: true, (MaxOrMinus1(m.Walls.Values) + 1) * s);
        if (file == ArtManifest.MergeFile)
            return ArtImageSize.Sheet(MergeAtlasLayout.Sides * m.Variations * s, exactWidth: true, Math.Max(1, m.MergeOrder.Count) * s);
        if (file == ArtManifest.ItemsFile)
            return new ArtImageSize((MaxOrMinus1(m.Items.Values.Select(i => i!.Cell)) + 1) * s, s, WidthIsMinimum: true, HeightIsMinimum: false);
        if (file == ArtManifest.PlayerFile(ArtManifest.ArmLayer))
            return ArtImageSize.Exact(p.ArmFrames * p.FrameWidth, p.FrameHeight);
        if (p.Layers.Any(l => ArtManifest.PlayerFile(l) == file))
            return ArtImageSize.Exact(p.BodyFrames * p.FrameWidth, p.FrameHeight);
        throw new ArgumentException($"arte: arquivo desconhecido '{file}'", nameof(file));
    }

    /// <summary>Lança DataException com o arquivo e o tamanho esperado se o PNG lido não servir.</summary>
    public void CheckSize(string file, int width, int height)
    {
        var expected = ExpectedSize(file);
        if (!expected.Matches(width, height))
            throw new DataException($"arte: '{PathOf(file)}' tem {width}×{height} px; esperado {expected}");
    }

    private static int MaxOrMinus1(IEnumerable<int> values) => values.DefaultIfEmpty(-1).Max();

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

        if (m.TileSize != TileAtlasLayout.TileSize) throw Fail($"tileSize precisa ser {TileAtlasLayout.TileSize} (é {m.TileSize})");
        if (m.Variations < 1) throw Fail($"variations precisa ser >= 1 (é {m.Variations})");
        if (m.Tiles is null) throw Fail("tiles ausente");
        if (m.Walls is null) throw Fail("walls ausente");
        if (m.MergeOrder is null) throw Fail("mergeOrder ausente");
        if (m.Items is null) throw Fail("items ausente");
        if (m.Player is null) throw Fail("player ausente");
        if (m.Player.Layers is null || m.Player.Layers.Any(l => string.IsNullOrEmpty(l))) throw Fail("player.layers ausente ou com nome vazio");
        if (m.Player.Hand is null) throw Fail("player.hand ausente");
        if (m.Player.ArmAngleDeg is null) throw Fail("player.armAngleDeg ausente");
        foreach (var (name, row) in m.Tiles)
            if (row < 0) throw Fail($"tiles.{name}: linha {row} negativa");
        foreach (var (name, row) in m.Walls)
            if (row < 0) throw Fail($"walls.{name}: linha {row} negativa");
        foreach (var (name, art) in m.Items)
        {
            if (art is null) throw Fail($"items.{name} ausente");
            if (art.Cell < 0) throw Fail($"items.{name}.cell: célula {art.Cell} negativa");
        }

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
            string name = m.MergeOrder[i] ?? throw Fail($"mergeOrder[{i}] ausente");
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
        if (p.Hand.Count != p.ArmFrames || p.Hand.Any(h => h is null || h.Length != 2))
            throw Fail($"player.hand precisa de {p.ArmFrames} pontos (x, y)");
        if (p.ArmAngleDeg.Count != p.ArmFrames) throw Fail($"player.armAngleDeg precisa de {p.ArmFrames} valores");
        if (p.FrameWidth < 1 || p.FrameHeight < 1 || p.BodyFrames < 1 || p.ArmFrames < p.BodyFrames)
            throw Fail("player: tamanho de quadro ou contagem de quadros inválidos");
        if (p.Idle < 0) throw Fail($"player.idle precisa ser >= 0 (é {p.Idle})");
        if (p.WalkFirst < 0) throw Fail($"player.walkFirst precisa ser >= 0 (é {p.WalkFirst})");
        if (p.Air < 0) throw Fail($"player.air precisa ser >= 0 (é {p.Air})");
        if (p.SwingFirst < 0) throw Fail($"player.swingFirst precisa ser >= 0 (é {p.SwingFirst})");
        if (p.Idle >= p.BodyFrames) throw Fail($"player.idle precisa ser < bodyFrames (é {p.Idle})");
        if (p.SwingCount <= PlayerAnimator.HoldSwingOffset)
            throw Fail($"player.swingCount precisa ser > {PlayerAnimator.HoldSwingOffset} (quadro de segurar bloco)");
        if (p.SwingFirst + p.SwingCount > p.ArmFrames || p.WalkFirst + p.WalkCount > p.BodyFrames || p.Air >= p.BodyFrames)
            throw Fail("player: índices de quadro fora das tiras");

        return new ArtCatalog(m, directory, tileRows, wallRows, ranks, items);
    }
}

/// <summary>Tamanho esperado de um PNG da arte. Dimensão "mínima" = pelo menos esse valor e múltipla de 16.</summary>
public readonly record struct ArtImageSize(int Width, int Height, bool WidthIsMinimum, bool HeightIsMinimum)
{
    public static ArtImageSize Exact(int width, int height) => new(width, height, false, false);

    public static ArtImageSize Sheet(int width, bool exactWidth, int minHeight) => new(width, minHeight, !exactWidth, true);

    public bool Matches(int width, int height) => Fits(width, Width, WidthIsMinimum) && Fits(height, Height, HeightIsMinimum);

    public override string ToString() => $"largura {Describe(Width, WidthIsMinimum)}, altura {Describe(Height, HeightIsMinimum)}";

    private static bool Fits(int actual, int expected, bool minimum) =>
        minimum ? actual >= expected && actual % TileAtlasLayout.TileSize == 0 : actual == expected;

    private static string Describe(int value, bool minimum) =>
        minimum ? $">= {value} (múltipla de {TileAtlasLayout.TileSize})" : value.ToString();
}
