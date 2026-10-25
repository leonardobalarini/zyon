using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen;

/// <summary>
/// FNV-1a 64 da configuração normalizada (serializada em JSON) e do texto do molde do bunker. Vai no save para indicar se o mundo foi
/// gerado com outros parâmetros: WorldGenerator.Version não captura mudanças em data/worldgen.json.
/// </summary>
public static class WorldGenConfigFingerprint
{
    public static ulong Compute(WorldGenConfig config)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(config, RegistryLoader.Options);
        ulong h = 14695981039346656037UL;
        h = Mix(h, json);
        if (config.Bunker is { } bunker)
            h = Mix(h, System.Text.Encoding.UTF8.GetBytes(bunker.SourceText));   // o molde muda o mundo gerado
        return h;
    }

    private static ulong Mix(ulong h, byte[] bytes)
    {
        foreach (byte b in bytes)
            h = (h ^ b) * 1099511628211UL;
        return h;
    }
}
