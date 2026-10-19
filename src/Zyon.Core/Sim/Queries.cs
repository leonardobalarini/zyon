using Arch.Core;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim;

internal static class Queries
{
    public static readonly QueryDescription Players = new QueryDescription()
        .WithAll<NetworkId, Position, PreviousPosition, Velocity, Body, PlayerState, InventoryRef>();

    public static readonly QueryDescription Items = new QueryDescription()
        .WithAll<NetworkId, Position, PreviousPosition, Velocity, Body, DroppedItem>();

    public static readonly QueryDescription Physical = new QueryDescription()
        .WithAll<Position, PreviousPosition, Velocity, Body>();
}
