namespace Zyon.Core.Data;

/// <summary>Constantes ajustáveis de física e gameplay. Unidades: tiles, segundos, ticks.</summary>
public sealed class PhysicsConfig
{
    public float Gravity { get; init; } = 60f;
    public float TerminalVelocity { get; init; } = 30f;
    public float MaxMoveSpeed { get; init; } = 9f;
    public float MoveAcceleration { get; init; } = 60f;
    public float MoveDeceleration { get; init; } = 80f;
    public float JumpVelocity { get; init; } = 22f;
    public float JumpCutFactor { get; init; } = 0.45f;
    public int CoyoteTicks { get; init; } = 6;
    public float PlayerWidth { get; init; } = 1.25f;
    public float PlayerHeight { get; init; } = 2.75f;
    public float ReachTiles { get; init; } = 5f;
    public int PlaceCooldownTicks { get; init; } = 8;
    public float MiningRate { get; init; } = 2f;
    public float ItemSize { get; init; } = 0.75f;
    public float ItemGroundFriction { get; init; } = 0.8f;
    public float MagnetRadius { get; init; } = 3f;
    public float MagnetAcceleration { get; init; } = 80f;
    public float MagnetMaxSpeed { get; init; } = 20f;
    public float ItemMergeRadius { get; init; } = 1f;
    public int ItemMergeIntervalTicks { get; init; } = 30;
    public float ItemDespawnSeconds { get; init; } = 300f;
    public int DropPickupDelayTicks { get; init; } = 90;
    public float DropImpulseX { get; init; } = 6f;
    public float DropImpulseY { get; init; } = 6f;
}
