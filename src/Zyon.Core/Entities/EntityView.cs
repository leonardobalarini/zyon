using System.Numerics;
using Zyon.Core.Items;

namespace Zyon.Core.Entities;

/// <summary>
/// Cópia somente leitura do estado de uma entidade para a apresentação. Os campos depois de Item só têm
/// valor para jogadores: velocidade (tiles/s), no chão, lado (+1 direita, −1 esquerda), botão de uso
/// pressionado, tile mirado e item selecionado na hotbar (0 = mão vazia).
/// </summary>
public readonly record struct EntityView(
    int NetworkId,
    EntityKind Kind,
    Vector2 Position,
    Vector2 PreviousPosition,
    Vector2 Size,
    ItemStack Item,
    Vector2 Velocity = default,
    bool OnGround = false,
    int Facing = 1,
    bool UsingItem = false,
    int AimX = 0,
    int AimY = 0,
    ushort HeldItemId = 0);
