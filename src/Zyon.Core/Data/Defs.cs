namespace Zyon.Core.Data;

public sealed record BlockDef(ushort Id, string Name, float Hardness, bool Solid, ushort DropItemId);

public sealed record WallDef(ushort Id, string Name);

public enum ItemKind { None, Block, Tool }

public sealed record ItemDef(
    ushort Id,
    string Name,
    string DisplayName,
    int MaxStack,
    ItemKind Kind,
    ushort PlacesBlockId,
    float ToolPower,
    float ToolSpeed);
