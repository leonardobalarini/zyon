using System.Numerics;
using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Physics;

namespace Zyon.Core.Sim.Systems;

/// <summary>Forças sobre itens caídos (gravidade, atrito, ímã) e coleta pelo jogador.</summary>
internal sealed class ItemPickupSystem : ISystem
{
    private readonly List<(int Id, Vector2 Center, Vector2 Size, Inventory Inventory)> players = new();
    private readonly List<int> collected = new();
    private readonly List<int> changedSlots = new();

    public void Update(Simulation sim)
    {
        players.Clear();
        collected.Clear();
        sim.Ecs.Query(in Queries.Players, (ref NetworkId id, ref Position p, ref Body b, ref InventoryRef inv) =>
            players.Add((id.Value, p.Value, b.Size, inv.Inventory)));

        var cfg = sim.Config;
        const float dt = Simulation.Dt;

        sim.Ecs.Query(in Queries.Items,
            (ref NetworkId id, ref Position pos, ref Velocity vel, ref Body body, ref DroppedItem item) =>
            {
                if (sim.Collider.BoxHasSolid(pos.Value, body.Size))
                {
                    pos.Value = FreeSpotAbove(sim.Collider, pos.Value, body.Size);
                    vel.Value = Vector2.Zero;
                }

                item.AgeTicks++;
                if (item.PickupDelayTicks > 0) item.PickupDelayTicks--;
                var v = vel.Value;
                bool magnetized = false;

                if (item.PickupDelayTicks == 0)
                {
                    int best = -1;
                    float bestDistance = cfg.MagnetRadius;
                    for (int i = 0; i < players.Count; i++)
                    {
                        float d = Vector2.Distance(players[i].Center, pos.Value);
                        if (d <= bestDistance && players[i].Inventory.CanAccept(item.Stack.ItemId))
                        {
                            best = i;
                            bestDistance = d;
                        }
                    }

                    if (best >= 0)
                    {
                        var player = players[best];
                        magnetized = true;

                        if (Aabb.Overlaps(pos.Value, body.Size, player.Center, player.Size))
                        {
                            changedSlots.Clear();
                            int left = player.Inventory.Add(item.Stack.ItemId, item.Stack.Count, changedSlots);
                            if (changedSlots.Count > 0)
                                sim.Emit(new InventoryChanged(player.Id, changedSlots.ToArray()));
                            if (left == 0)
                            {
                                collected.Add(id.Value);
                                vel.Value = Vector2.Zero;
                                return;
                            }
                            item.Stack = item.Stack with { Count = left };
                        }

                        var toPlayer = player.Center - pos.Value;
                        if (toPlayer.LengthSquared() > 1e-6f)
                            v += Vector2.Normalize(toPlayer) * cfg.MagnetAcceleration * dt;
                        if (v.Length() > cfg.MagnetMaxSpeed)
                            v = Vector2.Normalize(v) * cfg.MagnetMaxSpeed;
                    }
                }

                if (!magnetized)
                {
                    v.Y = MathF.Min(v.Y + cfg.Gravity * dt, cfg.TerminalVelocity);
                    if (body.OnGround) v.X *= cfg.ItemGroundFriction;
                }

                vel.Value = v;
            });

        foreach (var id in collected)
            sim.DestroyEntity(id);
    }

    // Itens não bloqueiam a colocação de blocos; sem isto um item ficaria preso dentro do bloco para sempre.
    private static Vector2 FreeSpotAbove(TileCollider collider, Vector2 position, Vector2 size)
    {
        var p = position;
        for (int i = 0; i < 32; i++)
        {
            p.Y -= 1f;
            if (!collider.BoxHasSolid(p, size)) return p;
        }
        return position;
    }
}
