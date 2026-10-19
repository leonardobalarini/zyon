using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

internal sealed class ItemDespawnSystem : ISystem
{
    private readonly List<int> expired = new();

    public void Update(Simulation sim)
    {
        expired.Clear();
        int maxAge = (int)(sim.Config.ItemDespawnSeconds * Simulation.TicksPerSecond);
        sim.Ecs.Query(in Queries.Items, (ref NetworkId id, ref DroppedItem item) =>
        {
            if (item.AgeTicks >= maxAge) expired.Add(id.Value);
        });
        foreach (var id in expired)
            sim.DestroyEntity(id);
    }
}
