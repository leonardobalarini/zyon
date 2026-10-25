using Zyon.Core.Data;

namespace Zyon.Core.WorldGen.Structures;

/// <summary>Entrada da legenda: bloco e parede por nome (null = ar / sem parede). <c>Keep</c> não altera o tile.</summary>
public sealed record StructureCell(char Symbol, string? Block, string? Wall, bool Keep);

/// <summary>Molde lido de <c>data/structures</c>, com nomes ainda não resolvidos. Imutável.</summary>
public sealed class StructureTemplate
{
    private readonly StructureCell[] cells;

    internal StructureTemplate(string name, string sourceText, int width, int height, StructureCell[] cells,
        (int X, int Y) anchor, (int X, int Y) spawn)
    {
        Name = name;
        SourceText = sourceText;
        Width = width;
        Height = height;
        this.cells = cells;
        Anchor = anchor;
        Spawn = spawn;
    }

    public string Name { get; }

    /// <summary>Texto do arquivo (entra na impressão digital da configuração da geração).</summary>
    public string SourceText { get; }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Tile de ar no pé do vão do portão; o tile abaixo dele é chão.</summary>
    public (int X, int Y) Anchor { get; }

    /// <summary>Tile onde ficam os pés do jogador ao nascer.</summary>
    public (int X, int Y) Spawn { get; }

    public StructureCell this[int x, int y] => cells[y * Width + x];

    /// <summary>Resolve os nomes no Registry e confere o chão e o espaço do spawn e o chão da âncora.</summary>
    public Structure Resolve(Registry registry)
    {
        DataException Fail(string message) => new($"{StructureLoader.FilePath(Name)}: {message}");

        var tiles = new StructureTile[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c.Keep)
            {
                tiles[i] = new StructureTile(0, 0, true);
                continue;
            }
            ushort block = 0, wall = 0;
            if (c.Block is not null && !registry.TryGetBlockId(c.Block, out block))
                throw Fail($"legenda '{c.Symbol}': bloco inexistente '{c.Block}'");
            if (c.Wall is not null && !registry.TryGetWallId(c.Wall, out wall))
                throw Fail($"legenda '{c.Symbol}': parede inexistente '{c.Wall}'");
            tiles[i] = new StructureTile(block, wall, false);
        }

        var s = new Structure(Name, Width, Height, tiles, Anchor, Spawn);
        bool Solid(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && !s[x, y].Keep && s[x, y].Block != 0 && registry.Block(s[x, y].Block).Solid;
        bool Free(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height && !s[x, y].Keep && (s[x, y].Block == 0 || !registry.Block(s[x, y].Block).Solid);

        var (ax, ay) = Anchor;
        if (!Free(ax, ay) || !Solid(ax, ay + 1))
            throw Fail($"linha {ay + 1}, coluna {ax + 1}: a âncora precisa de chão sólido logo abaixo");

        var (sx, sy) = Spawn;
        if (!Solid(sx, sy + 1))
            throw Fail($"linha {sy + 1}, coluna {sx + 1}: o spawn precisa de chão sólido logo abaixo");
        for (int y = sy - 2; y <= sy; y++)
        for (int x = sx - 1; x <= sx + 1; x++)
            if (!Free(x, y))
                throw Fail($"linha {sy + 1}, coluna {sx + 1}: o spawn precisa de 3 colunas × 3 linhas livres (bloqueado em linha {y + 1}, coluna {x + 1})");
        return s;
    }
}
