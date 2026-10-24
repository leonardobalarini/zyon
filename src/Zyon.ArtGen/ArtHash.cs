namespace Zyon.ArtGen;

/// <summary>Hash inteiro estável (mesmo resultado em qualquer plataforma e versão do .NET).</summary>
public static class ArtHash
{
    public static uint Get(int x, int y, int seed)
    {
        uint h = unchecked((uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u);
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        return h ^ (h >> 16);
    }

    /// <summary>Inteiro em [0, count).</summary>
    public static int Range(int x, int y, int seed, int count) => (int)(Get(x, y, seed) % (uint)count);

    /// <summary>Verdadeiro em <paramref name="percent"/>% dos casos.</summary>
    public static bool Chance(int x, int y, int seed, int percent) => Get(x, y, seed) % 100u < (uint)percent;
}
