using System.Globalization;

namespace Zyon.ArtGen;

public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    public static Rgba Parse(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' ||
            !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint v))
            throw new FormatException($"cor inválida '{hex}' (use #rrggbb)");
        return new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    /// <summary>Multiplica R, G e B (alpha fica igual), saturando em 0..255.</summary>
    public Rgba Scale(float f) => new(Channel(R, f), Channel(G, f), Channel(B, f), A);

    private static byte Channel(byte v, float f) => (byte)Math.Clamp((int)MathF.Round(v * f), 0, 255);
}
