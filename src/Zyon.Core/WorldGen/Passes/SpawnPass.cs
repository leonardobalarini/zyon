using System.Numerics;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Procura, a partir do centro e alternando direita/esquerda, SpawnFlatWidth colunas com a mesma superfície,
/// chão sólido e ClearanceRows linhas livres acima. Sem achar, aplaina o terreno no centro.
/// </summary>
internal sealed class SpawnPass : IWorldGenPass
{
    public const int ClearanceRows = 4;

    public string Name => "spawn";

    public void Apply(WorldGenContext ctx)
    {
        int flat = ctx.Config.SpawnFlatWidth;
        int center = ctx.World.Width / 2;

        for (int i = 0; i <= ctx.Config.SpawnSearchRadius; i++)
        {
            foreach (int x0 in i == 0 ? new[] { center } : new[] { center + i, center - i })
            {
                if (!IsFlat(ctx, x0, flat)) continue;
                ctx.SetSpawn(new Vector2(x0 + flat / 2f, ctx.Surface[x0]));
                return;
            }
        }

        Flatten(ctx, center, flat);
        ctx.SetSpawn(new Vector2(center + flat / 2f, ctx.Surface[center]));
    }

    private static bool IsFlat(WorldGenContext ctx, int x0, int flat)
    {
        var world = ctx.World;
        if (x0 < 0 || x0 + flat > world.Width) return false;
        int s = ctx.Surface[x0];
        if (s - ClearanceRows < 0) return false;
        for (int x = x0; x < x0 + flat; x++)
        {
            if (ctx.Surface[x] != s || world.GetBlock(x, s) == 0) return false;
            for (int y = s - ClearanceRows; y < s; y++)
                if (world.GetBlock(x, y) != 0) return false;
        }
        return true;
    }

    private static void Flatten(WorldGenContext ctx, int x0, int flat)
    {
        var world = ctx.World;
        int s = ctx.Surface[x0];
        ushort dirt = ctx.Materials.Dirt;
        for (int x = x0; x < x0 + flat; x++)
        {
            for (int y = 0; y < s; y++)   // limpa tudo acima, sem deixar saliência
            {
                world.SetBlock(x, y, 0);
                world.SetWall(x, y, 0);
            }
            world.SetWall(x, s, 0);       // a superfície é a linha s: sem parede em y <= s
            for (int y = s; y < Math.Max(ctx.Surface[x], s + 1); y++)
                world.SetBlock(x, y, dirt);
            for (int y = s + 1; y < ctx.Surface[x]; y++)   // linhas preenchidas ganham parede de terra
                world.SetWall(x, y, ctx.Materials.DirtWall);
            if (world.GetBlock(x, s) == 0)
                world.SetBlock(x, s, dirt);
            ctx.Surface[x] = s;
        }
    }
}
