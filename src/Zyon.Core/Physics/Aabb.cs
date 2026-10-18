using System.Numerics;

namespace Zyon.Core.Physics;

public static class Aabb
{
    /// <summary>Folga de contato. Precisa superar o ulp de float nas maiores coordenadas do mundo (até ~8400; meio ulp em 8192..16384 é ~4,9e-4), senão encostar vira sobrepor.</summary>
    public const float Epsilon = 2e-3f;

    /// <summary>Caixas definidas por centro e tamanho. Bordas apenas encostadas não contam.</summary>
    public static bool Overlaps(Vector2 centerA, Vector2 sizeA, Vector2 centerB, Vector2 sizeB) =>
        MathF.Abs(centerA.X - centerB.X) < (sizeA.X + sizeB.X) / 2 - Epsilon &&
        MathF.Abs(centerA.Y - centerB.Y) < (sizeA.Y + sizeB.Y) / 2 - Epsilon;
}
