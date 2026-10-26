namespace Zyon.Core.WorldGen.Passes;

/// <summary>Veios de minério por caminhada aleatória, só substituindo rocha rasa (de qualquer bioma) e rocha profunda dentro da faixa de profundidade.</summary>
internal sealed class OrePass : IWorldGenPass
{
    public string Name => "ores";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;
        var rng = ctx.Random;
        var replaceable = new bool[ctx.Registry.Blocks.Count];
        replaceable[m.Stone] = replaceable[m.DeepStone] = true;
        if (ctx.Biomes is { } biomes)
            foreach (var biome in biomes.Set.All)
                replaceable[biome.ShallowBlock] = true;
        long area = (long)world.Width * world.Height;

        for (int i = 0; i < m.Ores.Count; i++)
        {
            var (block, ore) = m.Ores[i];
            int veins = (int)(area * ore.VeinsPerMillionTiles / 1_000_000.0);
            int yMin = (int)(ore.MinDepth * world.Height);
            int yMax = Math.Max(yMin + 1, (int)(ore.MaxDepth * world.Height));

            for (int v = 0; v < veins; v++)
            {
                int x = rng.Next(world.Width);
                int y = rng.Next(yMin, yMax);
                int size = rng.Next(ore.VeinSizeMin, ore.VeinSizeMax + 1);
                int placed = 0;
                // Caminhada em direções cardeais: conta só tiles realmente trocados; limita os passos para sempre terminar.
                for (int step = 0; step < 4 * size && placed < size; step++)
                {
                    if (world.InBounds(x, y) && y >= yMin && y < yMax)
                    {
                        ushort current = world.GetBlock(x, y);
                        if (replaceable[current])
                        {
                            world.SetBlock(x, y, block);
                            placed++;
                        }
                    }
                    switch (rng.Next(4))
                    {
                        case 0: x++; break;
                        case 1: x--; break;
                        case 2: y++; break;
                        default: y--; break;
                    }
                }
            }
            ctx.Report((i + 1f) / m.Ores.Count);
        }
    }
}
