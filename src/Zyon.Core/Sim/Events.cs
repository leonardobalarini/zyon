using System.Numerics;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim;

public interface IGameEvent;

public sealed record TileChanged(int X, int Y, ushort Block, ushort Wall) : IGameEvent;

public sealed record EntitySpawned(int NetworkId, EntityKind Kind, Vector2 Position) : IGameEvent;

public sealed record EntityDespawned(int NetworkId) : IGameEvent;

public sealed record InventoryChanged(int PlayerId, IReadOnlyList<int> Slots) : IGameEvent;

public sealed record HotbarSelectionChanged(int PlayerId, int Slot) : IGameEvent;

/// <summary>Progress 0 significa que a mineração parou (limpar overlay).</summary>
public sealed record MiningProgressChanged(int PlayerId, int X, int Y, float Progress) : IGameEvent;

/// <summary>Progresso da geração de mundo: passe atual e fração total em [0, 1].</summary>
public sealed record WorldGenProgress(string Pass, int PassIndex, int PassCount, float Fraction) : IGameEvent;
