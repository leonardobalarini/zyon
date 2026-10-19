using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Entities;

/// <summary>Id estável usado em eventos, rede e save. Separado do id interno do Arch.</summary>
public record struct NetworkId(int Value);

/// <summary>Centro do AABB, em tiles.</summary>
public record struct Position(Vector2 Value);

/// <summary>Posição no tick anterior, para interpolação na apresentação.</summary>
public record struct PreviousPosition(Vector2 Value);

/// <summary>Tiles por segundo.</summary>
public record struct Velocity(Vector2 Value);

public struct Body
{
    public Vector2 Size;
    public bool CanStepUp;
    public bool OnGround;
}

public struct PlayerState
{
    public PlayerInput Input;
    public bool PrevJump;
    public int CoyoteTicks;
    public int Facing;
    public int SelectedSlot;
    public int PlaceCooldown;
    public bool Mining;
    public int MineX;
    public int MineY;
    public float MineProgress;
    public ushort MineBlockId;
}

public record struct InventoryRef(Inventory Inventory);

public struct DroppedItem
{
    public ItemStack Stack;
    public int AgeTicks;
    public int PickupDelayTicks;
}
