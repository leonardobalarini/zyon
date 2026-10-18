using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Physics;

/// <summary>Consultas de solidez na grade. Fora do mundo é sólido.</summary>
public sealed class TileCollider(TileWorld world, Registry registry)
{
    private const float GroundProbe = 0.05f;

    public bool IsSolid(int x, int y) => !world.InBounds(x, y) || registry.Block(world.GetBlock(x, y)).Solid;

    /// <summary>Há tile sólido sobrepondo a área aberta (minX, maxX) × (minY, maxY)?</summary>
    public bool AreaHasSolid(float minX, float minY, float maxX, float maxY)
    {
        int x0 = (int)MathF.Floor(minX + Aabb.Epsilon);
        int x1 = (int)MathF.Floor(maxX - Aabb.Epsilon);
        int y0 = (int)MathF.Floor(minY + Aabb.Epsilon);
        int y1 = (int)MathF.Floor(maxY - Aabb.Epsilon);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (IsSolid(x, y))
                    return true;
        return false;
    }

    public bool BoxHasSolid(Vector2 center, Vector2 size) =>
        AreaHasSolid(center.X - size.X / 2, center.Y - size.Y / 2, center.X + size.X / 2, center.Y + size.Y / 2);

    public bool IsGrounded(Vector2 center, Vector2 size)
    {
        float bottom = center.Y + size.Y / 2;
        return AreaHasSolid(center.X - size.X / 2, bottom, center.X + size.X / 2, bottom + GroundProbe);
    }
}
