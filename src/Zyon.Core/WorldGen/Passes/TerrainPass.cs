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
        float mid = (min + max) / 2f;
        float half = (max - min) / 2f;
        ushort fill = ctx.Materials.Stone;
        var noise = ctx.CreateNoise("terrain", cfg.TerrainFrequency, cfg.TerrainOctaves);
        var hillNoise = ctx.CreateNoise("terrain.hilliness", cfg.HillinessFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            // Modulação: regiões de baixa "acidentação" viram planícies, as de alta viram colinas.
            float t = Math.Clamp((hillNoise.GetNoise(x, 0f) + 1f) * 0.5f, 0f, 1f);
            float a = cfg.HillinessMin + (1f - cfg.HillinessMin) * (t * t * (3f - 2f * t));
            float n = Math.Clamp(noise.GetNoise(x, 0f), -1f, 1f);
            int surface = (int)MathF.Round(mid + n * half * a);
            ctx.Surface[x] = surface;
            for (int y = surface; y < height; y++)
                world.SetBlock(x, y, fill);
        });
    }
}
