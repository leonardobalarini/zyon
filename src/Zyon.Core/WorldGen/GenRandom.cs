namespace Zyon.Core.WorldGen;

/// <summary>
/// Gerador pseudoaleatório próprio (SplitMix64) para a geração de mundo. Ao contrário de System.Random
/// com seed, a sequência é fixa e não depende da versão do .NET. Não é thread-safe.
/// </summary>
public sealed class GenRandom
{
    private ulong state;

    public GenRandom(int seed)
    {
        state = (ulong)(uint)seed ^ 0x9E3779B97F4A7C15UL;
    }

    private ulong NextUInt64()
    {
        ulong z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>Inteiro em [0, int.MaxValue).</summary>
    public int Next() => (int)(NextUInt32() >> 1) % int.MaxValue;

    /// <summary>Inteiro em [0, maxExclusive).</summary>
    public int Next(int maxExclusive) => Next(0, maxExclusive);

    /// <summary>Inteiro em [minInclusive, maxExclusive). Retorna min quando min == max.</summary>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "minInclusive deve ser <= maxExclusive");
        ulong range = (ulong)((long)maxExclusive - minInclusive);
        if (range == 0) return minInclusive;
        return (int)(minInclusive + (long)(range * NextUInt32() >> 32));
    }

    /// <summary>Double em [0, 1) a partir dos 53 bits mais altos.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
