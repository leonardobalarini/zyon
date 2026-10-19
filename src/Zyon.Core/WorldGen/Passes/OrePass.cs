namespace Zyon.Core.WorldGen.Passes;

/// <summary>Veios de minério por caminhada aleatória, só substituindo rocha e rocha profunda dentro da faixa de profundidade.</summary>
internal sealed class OrePass : IWorldGenPass
{
    public string Name => "ores";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;
        var rng = ctx.Random;
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
                for (int s = 0; s < size; s++)
                {
                    if (world.InBounds(x, y) && y >= yMin && y < yMax)
                    {
                        ushort current = world.GetBlock(x, y);
                        if (current == m.Stone || current == m.DeepStone)
                            world.SetBlock(x, y, block);
                    }
                    x += rng.Next(-1, 2);
                    y += rng.Next(-1, 2);
                }
            }
            ctx.Report((i + 1f) / m.Ores.Count);
        }
    }
}
