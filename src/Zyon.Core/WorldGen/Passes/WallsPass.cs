namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Paredes de fundo em todo tile abaixo da superfície (inclusive dentro das cavernas): as do bioma do tile na camada
/// de cima e na rocha rasa, rocha na rocha profunda.
/// </summary>
internal sealed class WallsPass : IWorldGenPass
{
    public string Name => "walls";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var m = ctx.Materials;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");

        Parallel.For(0, world.Width, x =>
        {
            int surface = ctx.Surface[x];
            int dirtBottom = ctx.DirtBottom[x];
            int deepStart = ctx.DeepStart[x];
            for (int y = surface + 1; y < world.Height; y++)
            {
                ushort wall;
                if (y >= deepStart)
                    wall = m.StoneWall;
                else
                {
                    var biome = biomes.At(x, y, surface);
                    wall = y < dirtBottom ? biome.SurfaceWall : biome.ShallowWall;
                }
                world.SetWall(x, y, wall);
            }
        });
    }
}
