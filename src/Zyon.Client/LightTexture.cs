using Zyon.Core.Lighting;

namespace Zyon.Client;

/// <summary>Converte um LightMap em bytes L8 (0–255), linha por linha, para virar textura de 1 px por tile.</summary>
public static class LightTexture
{
    public static void ToL8(LightMap map, byte[] destination)
    {
        int size = map.Width * map.Height;
        if (destination.Length < size)
            throw new ArgumentException($"buffer de {destination.Length} bytes, precisa de {size}", nameof(destination));
        var levels = map.Levels;
        for (int i = 0; i < size; i++)
            destination[i] = (byte)Math.Clamp((int)MathF.Round(levels[i] * 255f), 0, 255);
    }
}
