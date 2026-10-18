using System.Numerics;

namespace Zyon.Core.Physics;

public readonly record struct MoveResult(Vector2 Position, bool HitX, bool HitY, bool Landed, bool SteppedUp);

/// <summary>Movimento AABB contra a grade: eixo X depois Y, subpassos de no máximo 0,5 tile, auto-step de 1 tile.</summary>
public static class TileMover
{
    public const float MaxStep = 0.5f;

    public static MoveResult Move(TileCollider collider, Vector2 position, Vector2 size, Vector2 delta, bool allowStepUp)
    {
        float longest = MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y));
        int steps = Math.Max(1, (int)MathF.Ceiling(longest / MaxStep));
        var step = delta / steps;
        var pos = position;
        bool hitX = false, hitY = false, landed = false, steppedUp = false;

        for (int i = 0; i < steps; i++)
        {
            if (step.X != 0 && !hitX)
            {
                var (p, hit, stepped) = MoveX(collider, pos, size, step.X, allowStepUp);
                pos = p;
                hitX |= hit;
                steppedUp |= stepped;
            }
            if (step.Y != 0 && !hitY)
            {
                var (p, hit) = MoveY(collider, pos, size, step.Y);
                pos = p;
                if (hit)
                {
                    hitY = true;
                    landed = step.Y > 0;
                }
            }
        }

        return new MoveResult(pos, hitX, hitY, landed, steppedUp);
    }

    private static (Vector2 Pos, bool Hit, bool SteppedUp) MoveX(
        TileCollider c, Vector2 pos, Vector2 size, float dx, bool allowStepUp)
    {
        var target = pos with { X = pos.X + dx };
        if (!c.BoxHasSolid(target, size)) return (target, false, false);

        if (allowStepUp)
        {
            var raisedTarget = target with { Y = target.Y - 1f };
            var raisedHere = pos with { Y = pos.Y - 1f };
            if (!c.BoxHasSolid(raisedTarget, size) && !c.BoxHasSolid(raisedHere, size))
                return (raisedTarget, false, true);
        }

        float half = size.X / 2;
        if (dx > 0)
        {
            int col = (int)MathF.Floor(target.X + half - Aabb.Epsilon);
            return (pos with { X = col - half }, true, false);
        }
        else
        {
            int col = (int)MathF.Floor(target.X - half + Aabb.Epsilon);
            return (pos with { X = col + 1 + half }, true, false);
        }
    }

    private static (Vector2 Pos, bool Hit) MoveY(TileCollider c, Vector2 pos, Vector2 size, float dy)
    {
        var target = pos with { Y = pos.Y + dy };
        if (!c.BoxHasSolid(target, size)) return (target, false);

        float half = size.Y / 2;
        if (dy > 0)
        {
            int row = (int)MathF.Floor(target.Y + half - Aabb.Epsilon);
            return (pos with { Y = row - half }, true);
        }
        else
        {
            int row = (int)MathF.Floor(target.Y - half + Aabb.Epsilon);
            return (pos with { Y = row + 1 + half }, true);
        }
    }
}
