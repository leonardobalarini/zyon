using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Integra a velocidade contra a grade de tiles para toda entidade física.</summary>
internal sealed class PhysicsSystem : ISystem
{
    public void Update(Simulation sim)
    {
        var collider = sim.Collider;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Physical, (ref Position pos, ref PreviousPosition prev, ref Velocity vel, ref Body body) =>
        {
            prev.Value = pos.Value;
            var v = vel.Value;
            if (v != Vector2.Zero)
            {
                var result = TileMover.Move(collider, pos.Value, body.Size, v * dt, body.CanStepUp && body.OnGround);
                pos.Value = result.Position;
                if (result.HitX) v.X = 0f;
                if (result.HitY) v.Y = 0f;
                vel.Value = v;
            }
            body.OnGround = collider.IsGrounded(pos.Value, body.Size);
        });
    }
}
