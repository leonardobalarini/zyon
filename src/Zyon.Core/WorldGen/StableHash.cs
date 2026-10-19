using System.Buffers.Binary;
using System.Text;

namespace Zyon.Core.WorldGen;

/// <summary>FNV-1a 32 bits de (seed, nome). Estável entre execuções e versões do .NET, ao contrário de string.GetHashCode.</summary>
internal static class StableHash
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    public static int Seed(int worldSeed, string name)
    {
        uint h = OffsetBasis;
        Span<byte> seedBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(seedBytes, worldSeed);
        foreach (byte b in seedBytes)
            h = (h ^ b) * Prime;
        foreach (byte b in Encoding.UTF8.GetBytes(name))
            h = (h ^ b) * Prime;
        return unchecked((int)h);
    }
}
