namespace Zyon.Core.WorldGen.Passes;

/// <summary>Paredes de fundo em todo tile abaixo da superfície (inclusive dentro das cavernas): solo na camada de solo, rocha abaixo.</summary>
internal sealed class WallsPass : IWorldGenPass
{
    public string Name => "walls";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;

        Parallel.For(0, world.Width, x =>
        {
            int dirtBottom = ctx.DirtBottom[x];
            for (int y = ctx.Surface[x] + 1; y < world.Height; y++)
                world.SetWall(x, y, y < dirtBottom ? m.DirtWall : m.StoneWall);
        });
    }
}
