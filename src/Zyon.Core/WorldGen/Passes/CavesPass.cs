namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Cavernas "queijo" (limiar de ruído 2D, em paralelo) e túneis "minhoca" (caminhada aleatória, sequencial).
/// Nunca escava a linha da superfície nem as últimas BottomSolidRows linhas.
/// </summary>
internal sealed class CavesPass : IWorldGenPass
{
    public string Name => "caves";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int floor = world.Height - cfg.BottomSolidRows;
        var cheese = ctx.CreateNoise("caves.cheese", cfg.CaveFrequency, 2);

        Parallel.For(0, world.Width, x =>
        {
            for (int y = ctx.Surface[x] + cfg.CaveMinDepth; y < floor; y++)
                if (cheese.GetNoise(x, y) > cfg.CaveThreshold)
                    world.SetBlock(x, y, 0);
        });
        ctx.Report(0.5f);

        var rng = ctx.Random;
        int worms = (int)(world.Width * cfg.WormsPerThousandColumns / 1000f);
        for (int i = 0; i < worms; i++)
        {
            int startX = rng.Next(world.Width);
            bool shallow = rng.NextDouble() < cfg.ShallowWormFraction;
            int top = Math.Min(ctx.Surface[startX] + cfg.CaveMinDepth, floor - 1);
            int bottom = shallow ? Math.Min(top + cfg.ShallowWormDepth, floor) : floor;
            float x = startX;
            float y = rng.Next(top, Math.Max(bottom, top + 1));
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            int length = rng.Next(cfg.WormLengthMin, cfg.WormLengthMax + 1);
            float radius = cfg.WormRadiusMin + (float)rng.NextDouble() * (cfg.WormRadiusMax - cfg.WormRadiusMin);

            for (int step = 0; step < length; step++)
            {
                Carve(ctx, x, y, radius, floor);
                angle += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                x += DeterministicMath.Cos(angle);
                y += DeterministicMath.Sin(angle) * 0.6f;
                // Ao alcançar a superfície, volta a descer (Y cresce para baixo)
                if (y <= ctx.Surface[Math.Clamp((int)x, 0, world.Width - 1)] && DeterministicMath.Sin(angle) < 0)
                    angle = -angle;
                if (x < 1 || x >= world.Width - 1 || y >= floor) break;
            }
            ctx.Report(0.5f + 0.5f * (i + 1) / worms);
        }
    }

    private static void Carve(WorldGenContext ctx, float cx, float cy, float radius, int floor)
    {
        var world = ctx.World;
        int x0 = (int)MathF.Floor(cx - radius), x1 = (int)MathF.Ceiling(cx + radius);
        int y0 = (int)MathF.Floor(cy - radius), y1 = (int)MathF.Ceiling(cy + radius);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (!world.InBounds(x, y) || y >= floor || y <= ctx.Surface[x]) continue;
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            if (dx * dx + dy * dy <= r2)
                world.SetBlock(x, y, 0);
        }
    }
}
