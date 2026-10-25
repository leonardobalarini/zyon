namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Ergue uma montanha perto do centro sobre o terreno: paredão (1 − u⁴) do lado do centro, onde fica o portão do
/// bunker, e descida suave (1 − smoothstep) do outro. Preenche com rocha (as camadas refinam depois). Em mundos
/// pequenos, offset, larguras e altura encolhem; o pico nunca sobe acima de surfaceMin × altura / 2.
/// </summary>
internal sealed class MountainPass : IWorldGenPass
{
    public string Name => "mountain";

    public void Apply(WorldGenContext ctx)
    {
        var cfg = ctx.Config;
        var world = ctx.World;
        int width = world.Width;
        bool facesLeft = cfg.MountainOffset >= 0;
        int dir = facesLeft ? 1 : -1;   // do pico para o lado suave

        float available = width / 2f - 2f;
        float k = Math.Min(1f, available / (Math.Abs(cfg.MountainOffset) + cfg.MountainGentleWidth));
        int offset = (int)MathF.Round(Math.Abs(cfg.MountainOffset) * k);
        int steep = Math.Max(4, (int)MathF.Round(cfg.MountainSteepWidth * k));
        int gentle = Math.Max(4, (int)MathF.Round(cfg.MountainGentleWidth * k));

        int peakX = Math.Clamp(width / 2 + dir * offset, 0, width - 1);
        int footX = Math.Clamp(peakX - dir * steep, 0, width - 1);
        int minRow = (int)(cfg.SurfaceMin * world.Height / 2);
        float height = Math.Min(cfg.MountainHeight * k, Math.Max(0, ctx.Surface[footX] - minRow));
        var noise = ctx.CreateNoise("mountain.shape", cfg.MountainNoiseFrequency, 2);
        ushort fill = ctx.Materials.Stone;

        for (int i = -steep; i <= gentle; i++)
        {
            int x = peakX + dir * i;
            if (x < 0 || x >= width) continue;
            float u = i < 0 ? -i / (float)steep : i / (float)gentle;
            float shape = i < 0 ? 1f - u * u * u * u : 1f - u * u * (3f - 2f * u);
            int rise = (int)MathF.Round((height + noise.GetNoise(x, 0f) * cfg.MountainNoiseAmplitude) * shape);
            if (rise <= 0) continue;
            int top = Math.Max(minRow, ctx.Surface[x] - rise);
            for (int y = top; y < ctx.Surface[x]; y++)
                world.SetBlock(x, y, fill);
            ctx.Surface[x] = top;
        }

        ctx.Mountain = new MountainInfo(peakX, footX, facesLeft);
    }
}
