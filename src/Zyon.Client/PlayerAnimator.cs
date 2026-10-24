using Zyon.Core.Data;
using Zyon.Core.Entities;

namespace Zyon.Client;

/// <summary>Estado de animação de um jogador entre frames (um por jogador na tela).</summary>
public struct PlayerAnimState
{
    public float WalkPhase;
    public float SwingPhase;
    public bool WasSwinging;
}

/// <summary>
/// Pose de um frame. Coordenadas no quadro do jogador virado para a direita; FacingLeft espelha tudo.
/// ItemRotationDeg é o ângulo de tela do ícone com o ponto de encaixe na mão (HandX, HandY).
/// </summary>
public readonly record struct PlayerPose(int BodyFrame, int ArmFrame, bool FacingLeft, ushort ItemId, float ItemRotationDeg, int HandX, int HandY);

/// <summary>
/// Escolhe os quadros do jogador: parado, andando (cadência proporcional à velocidade), no ar e golpe (braço em arco
/// enquanto o botão de uso está pressionado com um item na mão). Ao usar, olha para a mira; senão, para o lado do Core.
/// </summary>
public sealed class PlayerAnimator(ArtCatalog art, Registry registry)
{
    /// <summary>Duração de uma volta do golpe com velocidade de ferramenta 1.</summary>
    public const float SwingSeconds = 0.35f;

    /// <summary>Quadros de caminhada por tile percorrido.</summary>
    public const float WalkFramesPerTile = 4f;

    /// <summary>
    /// Quadro do golpe usado para segurar um bloco/tocha parado (SwingFirst + 3: braço à frente e um pouco para baixo),
    /// para o item ficar à frente do corpo e não escondido atrás do braço caído.
    /// </summary>
    public const int HoldSwingOffset = 3;

    private const float WalkThreshold = 0.1f;

    public PlayerPose Update(in EntityView e, float dt, ref PlayerAnimState s)
    {
        dt = MathF.Max(0f, dt);
        var p = art.Manifest.Player;
        var held = e.HeldItemId == 0 ? null : registry.Item(e.HeldItemId);
        bool swinging = e.UsingItem && held is not null;
        // Mirando a própria coluna (ex.: minerar logo abaixo) a posição dentro do tile não diz o lado: mantém o do Core.
        bool facingLeft = swinging && e.AimX != (int)MathF.Floor(e.Position.X) ? e.AimX + 0.5f < e.Position.X : e.Facing < 0;

        int body;
        if (!e.OnGround)
        {
            body = p.Air;
            s.WalkPhase = 0;
        }
        else if (MathF.Abs(e.Velocity.X) > WalkThreshold)
        {
            s.WalkPhase = (s.WalkPhase + MathF.Abs(e.Velocity.X) * dt * WalkFramesPerTile) % p.WalkCount;
            body = p.WalkFirst + (int)s.WalkPhase;
        }
        else
        {
            body = p.Idle;
            s.WalkPhase = 0;
        }

        bool holdingBlock = held is not null && held.PlacesBlockId != 0;
        int arm = body;
        if (holdingBlock && !swinging)
            arm = p.SwingFirst + HoldSwingOffset;
        if (swinging)
        {
            float speed = held!.ToolSpeed > 0 ? held.ToolSpeed : 1f;
            if (!s.WasSwinging) s.SwingPhase = 0;
            else s.SwingPhase = (s.SwingPhase + dt * speed / SwingSeconds) % 1f;
            arm = p.SwingFirst + Math.Min(p.SwingCount - 1, (int)(s.SwingPhase * p.SwingCount));
        }
        s.WasSwinging = swinging;

        ushort shown = 0;
        float rotation = 0;
        if (held is not null && (swinging || holdingBlock))
        {
            shown = held.Id;
            var itemArt = art.ItemByItemId[held.Id]!;
            rotation = swinging ? p.ArmAngleDeg[arm] - itemArt.PointDeg : -90f - itemArt.PointDeg;
        }
        var hand = p.Hand[arm];
        return new PlayerPose(body, arm, facingLeft, shown, rotation, hand[0], hand[1]);
    }
}
