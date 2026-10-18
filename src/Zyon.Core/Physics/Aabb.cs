using System.Numerics;

namespace Zyon.Core.Physics;

public static class Aabb
{
    public const float Epsilon = 1e-4f;

    /// <summary>Caixas definidas por centro e tamanho. Bordas apenas encostadas não contam.</summary>
    public static bool Overlaps(Vector2 centerA, Vector2 sizeA, Vector2 centerB, Vector2 sizeB) =>
        MathF.Abs(centerA.X - centerB.X) < (sizeA.X + sizeB.X) / 2 - Epsilon &&
        MathF.Abs(centerA.Y - centerB.Y) < (sizeA.Y + sizeB.Y) / 2 - Epsilon;
}
