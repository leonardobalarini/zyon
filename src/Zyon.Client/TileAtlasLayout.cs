namespace Zyon.Client;

/// <summary>
/// Atlas de 16×16 px: uma linha por material (id), e em cada linha uma célula por (máscara, variação),
/// na ordem máscara × Variations + variação.
/// </summary>
public sealed class TileAtlasLayout(int materialCount, int variations)
{
    public const int TileSize = RenderMath.PixelsPerTile;
    private const float InsetPx = 0.01f;   // evita costuras entre células com filtro nearest

    public int MaterialCount { get; } = materialCount;
    public int Variations { get; } = variations;
    public int WidthPx => AutoTile.MaskCount * Variations * TileSize;
    public int HeightPx => MaterialCount * TileSize;

    public (int X, int Y) CellOrigin(int material, int mask, int variation) =>
        ((mask * Variations + variation) * TileSize, material * TileSize);

    public (float U0, float V0, float U1, float V1) Uv(int material, int mask, int variation)
    {
        var (x, y) = CellOrigin(material, mask, variation);
        float w = WidthPx, h = HeightPx;
        return ((x + InsetPx) / w, (y + InsetPx) / h, (x + TileSize - InsetPx) / w, (y + TileSize - InsetPx) / h);
    }
}
