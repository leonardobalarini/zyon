namespace Zyon.Core.Data;

/// <summary>Light: luz emitida pelo bloco, em [0, 1] (0 = não emite). Tree: parte de árvore (quebrar derruba o que está ligado acima).</summary>
public sealed record BlockDef(ushort Id, string Name, float Hardness, bool Solid, ushort DropItemId, float Light = 0f, bool Tree = false);

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
