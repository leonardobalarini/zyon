namespace Zyon.Core.Lighting;

/// <summary>Níveis de luz [0, 1] de um retângulo de tiles, linha por linha. Reutilizável entre cálculos.</summary>
public sealed class LightMap
{
    private float[] levels = [];

    public TileRect Region { get; private set; }
    public int Width => Region.Width;
    public int Height => Region.Height;

    /// <summary>Buffer interno (pode ser maior que Width × Height; use só os primeiros Width × Height).</summary>
    public float[] Levels => levels;

    public void Reset(TileRect region)
    {
        Region = region;
        int size = region.IsEmpty ? 0 : region.Width * region.Height;
        if (levels.Length < size) levels = new float[size];
        Array.Clear(levels, 0, size);
    }

    public float Get(int x, int y) =>
        Region.Contains(x, y) ? levels[(y - Region.MinY) * Width + (x - Region.MinX)] : 0f;
}
