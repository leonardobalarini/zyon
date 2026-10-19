using Zyon.Core.Data;
using Zyon.Core.Entities;
using Zyon.Core.Items;

namespace Zyon.Core.Sim;

/// <summary>Acesso somente leitura ao estado do jogo para a apresentação.</summary>
public interface IWorldView
{
    Registry Registry { get; }
    int Width { get; }
    int Height { get; }
    long Tick { get; }
    ushort GetBlock(int x, int y);
    ushort GetWall(int x, int y);
    bool TryGetEntityView(int networkId, out EntityView view);
    int CopyEntities(List<EntityView> buffer);
    IReadOnlyInventory? GetInventory(int playerId);
    int GetSelectedSlot(int playerId);
}
