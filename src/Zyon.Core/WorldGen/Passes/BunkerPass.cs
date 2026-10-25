using System.Numerics;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Carimba o molde do bunker com o portão no pé da face íngreme da montanha (espelhado se a face olha para a direita),
/// completa com rocha as linhas de teto que faltarem, abre um patamar diante do portão e põe o spawn no tile marcado.
/// </summary>
internal sealed class BunkerPass : IWorldGenPass
{
    /// <summary>Colunas livres diante do portão.</summary>
    public const int PlateauWidth = 3;

    /// <summary>Linhas livres acima do chão do patamar (o vão do portão tem 4).</summary>
    public const int PlateauClearRows = 5;

    public string Name => "bunker";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var mountain = ctx.Mountain ?? throw new InvalidOperationException("o passe 'bunker' precisa do passe 'mountain' antes");
        var template = ctx.Materials.Bunker
            ?? throw new InvalidOperationException("molde do bunker não carregado: use WorldGenConfigLoader.LoadFromDirectory");
        var s = mountain.FacesLeft ? template : template.Mirrored();
        int outward = mountain.FacesLeft ? -1 : 1;   // lado de fora do portão

        int x0 = Math.Clamp(mountain.FootX - s.Anchor.X, PlateauWidth + 1, world.Width - s.Width - PlateauWidth - 1);
        int gateX = x0 + s.Anchor.X;
        int y0 = Math.Clamp(ctx.Surface[gateX] - 1 - s.Anchor.Y, ctx.Config.BunkerRoofRows,
            world.Height - s.Height - ctx.Config.BottomSolidRows);
        int anchorY = y0 + s.Anchor.Y;

        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
        {
            var t = s[x, y];
            if (t.Keep) continue;
            world.SetBlock(x0 + x, y0 + y, t.Block);
            world.SetWall(x0 + x, y0 + y, t.Wall);
        }

        for (int x = 0; x < s.Width; x++)
            CoverRoof(ctx, x0 + x, y0 + s.TopRow(x), s.TopRow(x) >= 0);

        for (int i = 1; i <= PlateauWidth; i++)
            OpenPlateau(ctx, gateX + outward * i, anchorY);

        ctx.Bunker = new BunkerPlacement(x0, y0, s);
        ctx.SetSpawn(new Vector2(x0 + s.Spawn.X + 0.5f, y0 + s.Spawn.Y + 1));
    }

    /// <summary>As BunkerRoofRows linhas acima do topo do molde ficam sólidas (ar vira rocha, com parede abaixo da nova superfície).</summary>
    private static void CoverRoof(WorldGenContext ctx, int x, int top, bool hasTop)
    {
        if (!hasTop) return;
        var world = ctx.World;
        for (int y = Math.Max(0, top - ctx.Config.BunkerRoofRows); y < top; y++)
            if (world.GetBlock(x, y) == 0)
                world.SetBlock(x, y, ctx.Materials.Stone);

        int surface = FirstBlockRow(world, x);
        for (int y = surface + 1; y < top; y++)
            if (world.GetWall(x, y) == 0)
                world.SetWall(x, y, ctx.Materials.StoneWall);
        ctx.Surface[x] = surface;
    }

    /// <summary>Coluna diante do portão: livre (sem bloco nem parede) acima do chão do portão e com chão sólido.</summary>
    private static void OpenPlateau(WorldGenContext ctx, int x, int anchorY)
    {
        var world = ctx.World;
        for (int y = Math.Max(0, anchorY - PlateauClearRows + 1); y <= anchorY; y++)
        {
            world.SetBlock(x, y, 0);
            world.SetWall(x, y, 0);
        }
        if (world.GetBlock(x, anchorY + 1) == 0)
            world.SetBlock(x, anchorY + 1, ctx.Materials.Dirt);
        ctx.Surface[x] = FirstBlockRow(world, x);
    }

    private static int FirstBlockRow(TileWorld world, int x)
    {
        int y = 0;
        while (y < world.Height - 1 && world.GetBlock(x, y) == 0) y++;
        return y;
    }
}
