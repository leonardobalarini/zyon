namespace Zyon.Core.WorldGen.Passes;

/// <summary>Solo perto da superfície, rocha no meio e rocha profunda embaixo, com fronteiras irregulares por ruído.</summary>
internal sealed class LayersPass : IWorldGenPass
{
    public string Name => "layers";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        var m = ctx.Materials;
        int height = world.Height;
        var dirtNoise = ctx.CreateNoise("layers.dirt", cfg.LayerNoiseFrequency, 2);
        var deepNoise = ctx.CreateNoise("layers.deep", cfg.LayerNoiseFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = surface + cfg.DirtDepth + (int)MathF.Round(dirtNoise.GetNoise(x, 0f) * cfg.DirtDepthVariation);
            dirtBottom = Math.Clamp(dirtBottom, surface + 1, height);
            int deepStart = (int)MathF.Round(cfg.DeepLayerStart * height + deepNoise.GetNoise(x, 0f) * cfg.DeepLayerVariation);
            ctx.DirtBottom[x] = dirtBottom;

            for (int y = surface; y < height; y++)
                world.SetBlock(x, y, y < dirtBottom ? m.Dirt : y < deepStart ? m.Stone : m.DeepStone);
        });
    }
}
