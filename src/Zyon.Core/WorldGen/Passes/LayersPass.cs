namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Camada de cima e rocha rasa com os blocos do bioma de cada tile (BiomeMap: afina com a profundidade), rocha profunda
/// embaixo (igual em todo lugar), com fronteiras irregulares por ruído.
/// </summary>
internal sealed class LayersPass : IWorldGenPass
{
    public string Name => "layers";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        var m = ctx.Materials;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        int height = world.Height;
        var dirtNoise = ctx.CreateNoise("layers.dirt", cfg.LayerNoiseFrequency, 2);
        var deepNoise = ctx.CreateNoise("layers.deep", cfg.LayerNoiseFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = surface + cfg.DirtDepth + (int)MathF.Round(dirtNoise.GetNoise(x, 0f) * cfg.DirtDepthVariation);
            dirtBottom = Math.Clamp(dirtBottom, surface + 1, height);
            int deepStart = (int)MathF.Round(cfg.DeepLayerStart * height + deepNoise.GetNoise(x, 0f) * cfg.DeepLayerVariation);
            deepStart = Math.Max(deepStart, dirtBottom + 1);
            ctx.DirtBottom[x] = dirtBottom;
            ctx.DeepStart[x] = deepStart;

            for (int y = surface; y < height; y++)
            {
                ushort block;
                if (y >= deepStart)
                    block = m.DeepStone;
                else
                {
                    var biome = biomes.At(x, y, surface);
                    block = y < dirtBottom ? biome.SurfaceBlock : biome.ShallowBlock;
                }
                world.SetBlock(x, y, block);
            }
        });
    }
}
