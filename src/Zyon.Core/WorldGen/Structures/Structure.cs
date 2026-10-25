namespace Zyon.Core.WorldGen.Structures;

/// <summary>Tile do molde resolvido: ids de bloco e parede (0 = ar / sem parede). <c>Keep</c> não altera o mundo.</summary>
public readonly record struct StructureTile(ushort Block, ushort Wall, bool Keep);

/// <summary>Molde com ids do Registry, pronto para ser carimbado no mundo. Imutável.</summary>
public sealed class Structure
{
    private readonly StructureTile[] tiles;

    public Structure(string name, int width, int height, StructureTile[] tiles, (int X, int Y) anchor, (int X, int Y) spawn)
    {
        if (width < 1 || height < 1) throw new ArgumentException("largura e altura devem ser ao menos 1");
        if (tiles.Length != width * height) throw new ArgumentException("tamanho dos tiles diferente de largura × altura", nameof(tiles));
        if (anchor.X < 0 || anchor.X >= width || anchor.Y < 0 || anchor.Y >= height) throw new ArgumentException("âncora fora do molde", nameof(anchor));
        if (spawn.X < 0 || spawn.X >= width || spawn.Y < 0 || spawn.Y >= height) throw new ArgumentException("spawn fora do molde", nameof(spawn));
        Name = name;
        Width = width;
        Height = height;
        this.tiles = tiles.ToArray();
        Anchor = anchor;
        Spawn = spawn;
    }

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public (int X, int Y) Anchor { get; }
    public (int X, int Y) Spawn { get; }

    public StructureTile this[int x, int y] => tiles[y * Width + x];

    /// <summary>Cópia espelhada na horizontal (portão do outro lado).</summary>
    public Structure Mirrored()
    {
        var flipped = new StructureTile[tiles.Length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            flipped[y * Width + (Width - 1 - x)] = tiles[y * Width + x];
        return new Structure(Name, Width, Height, flipped, (Width - 1 - Anchor.X, Anchor.Y), (Width - 1 - Spawn.X, Spawn.Y));
    }

    /// <summary>Primeira linha da coluna que não é <c>Keep</c>, ou −1 se a coluna inteira é <c>Keep</c>.</summary>
    public int TopRow(int x)
    {
        for (int y = 0; y < Height; y++)
            if (!this[x, y].Keep) return y;
        return -1;
    }
}
