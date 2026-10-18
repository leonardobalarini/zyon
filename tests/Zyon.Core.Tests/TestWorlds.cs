using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

internal static class TestWorlds
{
    /// <summary>Mundo com o bloco <paramref name="block"/> preenchendo todas as linhas y >= groundY.</summary>
    public static TileWorld Flat(Registry registry, int width, int height, int groundY, string block = "dirt")
    {
        var world = new TileWorld(width, height);
        ushort id = registry.BlockId(block);
        for (int y = groundY; y < height; y++)
            for (int x = 0; x < width; x++)
                world.SetBlock(x, y, id);
        return world;
    }
}
