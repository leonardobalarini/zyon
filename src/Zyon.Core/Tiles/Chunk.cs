namespace Zyon.Core.Tiles;

/// <summary>Bloco 32×32 de tiles em arrays paralelos (structure of arrays).</summary>
public sealed class Chunk(int chunkX, int chunkY)
{
    public const int Size = 32;
    public const int Shift = 5;
    public const int Mask = Size - 1;
    public const int Area = Size * Size;

    public int ChunkX { get; } = chunkX;
    public int ChunkY { get; } = chunkY;
    public ushort[] Blocks { get; } = new ushort[Area];
    public ushort[] Walls { get; } = new ushort[Area];

    /// <summary>Pertence somente à persistência (save): alterado desde o último salvamento. Renderização deve usar eventos <c>TileChanged</c>. Quem gera/carrega um mundo deve limpar a flag depois.</summary>
    public bool Dirty { get; set; }

    public static int Index(int localX, int localY) => (localY << Shift) + localX;
}
