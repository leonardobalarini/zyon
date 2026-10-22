namespace Zyon.Core.Lighting;

/// <summary>Luz extra que não vem de um bloco (ex.: o halo do jogador).</summary>
public readonly record struct LightEmitter(int X, int Y, float Level);
