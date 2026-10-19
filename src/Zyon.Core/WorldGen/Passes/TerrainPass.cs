namespace Zyon.Core.WorldGen.Passes;

/// <summary>Define a superfície por ruído 1D e preenche tudo abaixo dela com rocha (as camadas refinam depois).</summary>
internal sealed class TerrainPass : IWorldGenPass
{
    public string Name => "terrain";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int height = world.Height;
        float min = cfg.SurfaceMin * height;
        float max = cfg.SurfaceMax * height;
        ushort fill = ctx.Materials.Stone;
        var noise = ctx.CreateNoise("terrain", cfg.TerrainFrequency, cfg.TerrainOctaves);

        Parallel.For(0, world.Width, x =>
        {
            float t = Math.Clamp((noise.GetNoise(x, 0f) + 1f) * 0.5f, 0f, 1f);
            int surface = (int)MathF.Round(min + (max - min) * t);
            ctx.Surface[x] = surface;
            for (int y = surface; y < height; y++)
                world.SetBlock(x, y, fill);
        });
    }
}
