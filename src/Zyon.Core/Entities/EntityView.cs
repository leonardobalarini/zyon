using System.Numerics;
using Zyon.Core.Items;

namespace Zyon.Core.Entities;

/// <summary>Cópia somente leitura do estado de uma entidade para a apresentação.</summary>
public readonly record struct EntityView(
    int NetworkId,
    EntityKind Kind,
    Vector2 Position,
    Vector2 PreviousPosition,
    Vector2 Size,
    ItemStack Item);
