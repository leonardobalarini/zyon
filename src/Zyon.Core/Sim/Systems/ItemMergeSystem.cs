using System.Numerics;
using Arch.Core;
using Zyon.Core.Entities;

namespace Zyon.Core.Sim.Systems;

/// <summary>Funde itens iguais próximos, usando hash espacial de células de 1 tile. Roda a cada N ticks.</summary>
internal sealed class ItemMergeSystem : ISystem
{
    private readonly Dictionary<long, List<Entity>> cells = new();
    private readonly List<Entity> items = new();
    private readonly List<int> emptied = new();
    private readonly Stack<List<Entity>> pool = new();

    public void Update(Simulation sim)
    {
        var cfg = sim.Config;
        if (cfg.ItemMergeIntervalTicks <= 0 || sim.Tick % cfg.ItemMergeIntervalTicks != 0) return;

        foreach (var list in cells.Values)
        {
            list.Clear();
            pool.Push(list);
        }
        cells.Clear();
        items.Clear();
        emptied.Clear();
        sim.Ecs.Query(in Queries.Items, (Entity e, ref Position p) =>
        {
            items.Add(e);
            var key = Key((int)MathF.Floor(p.Value.X), (int)MathF.Floor(p.Value.Y));
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = pool.Count > 0 ? pool.Pop() : new List<Entity>();
            list.Add(e);
        });

        var ecs = sim.Ecs;
        foreach (var a in items)
        {
            ref var itemA = ref ecs.Get<DroppedItem>(a);
            if (itemA.Stack.IsEmpty) continue;
            int max = sim.Registry.Item(itemA.Stack.ItemId).MaxStack;
            var posA = ecs.Get<Position>(a).Value;
            int cx = (int)MathF.Floor(posA.X), cy = (int)MathF.Floor(posA.Y);

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (itemA.Stack.Count >= max) break;
                if (!cells.TryGetValue(Key(cx + dx, cy + dy), out var neighbours)) continue;
                foreach (var b in neighbours)
                {
                    if (b == a) continue;
                    ref var itemB = ref ecs.Get<DroppedItem>(b);
                    if (itemB.Stack.IsEmpty || itemB.Stack.ItemId != itemA.Stack.ItemId) continue;
                    if (Vector2.Distance(posA, ecs.Get<Position>(b).Value) > cfg.ItemMergeRadius) continue;

                    int n = Math.Min(max - itemA.Stack.Count, itemB.Stack.Count);
                    if (n <= 0) break;
                    itemA.Stack = itemA.Stack with { Count = itemA.Stack.Count + n };
                    itemB.Stack = itemB.Stack with { Count = itemB.Stack.Count - n };
                    if (itemB.Stack.IsEmpty)
                        emptied.Add(ecs.Get<NetworkId>(b).Value);
                }
            }
        }

        foreach (var id in emptied)
            sim.DestroyEntity(id);
    }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
}
