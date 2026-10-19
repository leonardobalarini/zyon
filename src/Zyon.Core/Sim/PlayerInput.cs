namespace Zyon.Core.Sim;

/// <summary>Estado contínuo de controle de um jogador, enviado a cada tick.</summary>
public readonly record struct PlayerInput(float MoveX, bool Jump, bool Primary, int AimX, int AimY);
