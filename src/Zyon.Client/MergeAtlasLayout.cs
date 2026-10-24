namespace Zyon.Client;

/// <summary>merge.png: linha = posição na mergeOrder; coluna = lado × Variations + variação (lado 0 N, 1 L, 2 S, 3 O).</summary>
public sealed class MergeAtlasLayout(int rows, int variations)
{
    public const int Sides = 4;
    private const float InsetPx = 0.01f;

    public int Rows { get; } = Math.Max(1, rows);
    public int Variations { get; } = variations;
    public int WidthPx => Sides * Variations * TileAtlasLayout.TileSize;
    public int HeightPx => Rows * TileAtlasLayout.TileSize;

    public (float U0, float V0, float U1, float V1) Uv(int row, int side, int variation)
    {
        const int s = TileAtlasLayout.TileSize;
        float x = (side * Variations + variation) * s, y = row * s;
        return ((x + InsetPx) / WidthPx, (y + InsetPx) / HeightPx, (x + s - InsetPx) / WidthPx, (y + s - InsetPx) / HeightPx);
    }
}
