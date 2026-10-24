namespace Zyon.Client;

/// <summary>
/// Gera um atlas provisório em RGBA8: cor base com ruído estável por pixel e borda escura de 2 px
/// nos lados sem vizinho (segundo a máscara). Materiais sem cor ficam transparentes.
/// </summary>
public static class PlaceholderAtlas
{
    private const int Border = 2;
    private const float BorderShade = 0.6f;
    private const int NoiseAmplitude = 8;
    private const int FlameLift = 80;

    public static byte[] BuildRgba(TileAtlasLayout layout, IReadOnlyList<Rgb?> colorsById, IReadOnlyList<bool>? solidById = null)
    {
        var rgba = new byte[layout.WidthPx * layout.HeightPx * 4];
        int size = TileAtlasLayout.TileSize;

        for (int material = 0; material < layout.MaterialCount; material++)
        {
            if (material >= colorsById.Count || colorsById[material] is not { } baseColor) continue;
            bool stick = solidById is not null && material < solidById.Count && !solidById[material];
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int variation = 0; variation < layout.Variations; variation++)
            {
                var (ox, oy) = layout.CellOrigin(material, mask, variation);
                for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    int noise = Noise(material, variation, px, py);
                    if (stick)
                    {
                        if (px < 6 || px > 9 || py < 3 || py > 15) continue;   // fora do palito: transparente
                        int lift = py <= 5 ? FlameLift : 0;
                        int j = ((oy + py) * layout.WidthPx + ox + px) * 4;
                        rgba[j] = Channel(baseColor.R, noise + lift, 1f);
                        rgba[j + 1] = Channel(baseColor.G, noise + lift, 1f);
                        rgba[j + 2] = Channel(baseColor.B, noise + lift, 1f);
                        rgba[j + 3] = 255;
                        continue;
                    }
                    bool edge = (py < Border && (mask & AutoTile.North) == 0)
                                || (py >= size - Border && (mask & AutoTile.South) == 0)
                                || (px < Border && (mask & AutoTile.West) == 0)
                                || (px >= size - Border && (mask & AutoTile.East) == 0);
                    float shade = edge ? BorderShade : 1f;
                    int i = ((oy + py) * layout.WidthPx + ox + px) * 4;
                    rgba[i] = Channel(baseColor.R, noise, shade);
                    rgba[i + 1] = Channel(baseColor.G, noise, shade);
                    rgba[i + 2] = Channel(baseColor.B, noise, shade);
                    rgba[i + 3] = 255;
                }
            }
        }
        return rgba;
    }

    private static byte Channel(byte value, int noise, float shade) =>
        (byte)Math.Clamp((int)((value + noise) * shade), 0, 255);

    private static int Noise(int material, int variation, int px, int py)
    {
        uint h = unchecked((uint)(material * 7919 + variation * 104729 + px * 31 + py * 1009));
        h ^= h >> 16;
        h = unchecked(h * 0x7feb352du);
        h ^= h >> 15;
        return (int)(h % (2 * NoiseAmplitude + 1)) - NoiseAmplitude;
    }
}
