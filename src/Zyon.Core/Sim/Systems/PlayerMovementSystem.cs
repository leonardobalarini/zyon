using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

/// <summary>Converte o input em velocidade: aceleração, pulo variável, coyote time, gravidade.</summary>
internal sealed class PlayerMovementSystem : ISystem
{
    public void Update(Simulation sim)
    {
        var cfg = sim.Config;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Players, (ref Velocity vel, ref Body body, ref PlayerState st) =>
        {
            var input = st.Input;
            var v = vel.Value;

            float move = float.IsFinite(input.MoveX) ? Math.Clamp(input.MoveX, -1f, 1f) : 0f;
            if (move != 0f)
            {
                st.Facing = move > 0 ? 1 : -1;
                v.X = Approach(v.X, move * cfg.MaxMoveSpeed, cfg.MoveAcceleration * dt);
            }
            else
            {
                v.X = Approach(v.X, 0f, cfg.MoveDeceleration * dt);
            }

            if (body.OnGround) st.CoyoteTicks = cfg.CoyoteTicks;

            bool pressed = input.Jump && !st.PrevJump;
            bool released = !input.Jump && st.PrevJump;
            if (pressed && st.CoyoteTicks > 0)
            {
                v.Y = -cfg.JumpVelocity;
                st.CoyoteTicks = 0;
            }
            else if (released && v.Y < 0f)
            {
                v.Y *= cfg.JumpCutFactor;
            }

            // O decremento vem depois do teste: um aperto em qualquer dos CoyoteTicks ticks após sair do chão vale.
            if (!body.OnGround && st.CoyoteTicks > 0) st.CoyoteTicks--;

            v.Y = MathF.Min(v.Y + cfg.Gravity * dt, cfg.TerminalVelocity);
            st.PrevJump = input.Jump;
            vel.Value = v;
        });
    }

    private static float Approach(float current, float target, float maxDelta) =>
        current < target ? MathF.Min(current + maxDelta, target) : MathF.Max(current - maxDelta, target);
}
