using System.Collections.Generic;
using Godot;
using Zyon.Client;
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>Jogadores como retângulos provisórios; itens caídos em lote com MultiMesh (um draw call).</summary>
public partial class EntityRenderer : Node2D
{
    private const float ItemPixels = 12f;

    private readonly List<EntityView> buffer = new();
    private readonly Dictionary<int, ColorRect> players = new();
    private readonly HashSet<int> seen = new();
    private readonly List<int> gone = new();
    private IWorldView view = null!;
    private ClientVisuals visuals = null!;
    private MultiMesh items = null!;

    public void Init(IWorldView worldView, ClientVisuals clientVisuals)
    {
        view = worldView;
        visuals = clientVisuals;
        items = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = new QuadMesh { Size = new Vector2(ItemPixels, ItemPixels) },
        };
        AddChild(new MultiMeshInstance2D { Multimesh = items });
    }

    public void Refresh(float alpha)
    {
        view.CopyEntities(buffer);
        seen.Clear();
        int itemCount = 0;
        foreach (var e in buffer)
            if (e.Kind == EntityKind.DroppedItem) itemCount++;
        if (items.InstanceCount < itemCount)
            items.InstanceCount = Mathf.Max(itemCount, items.InstanceCount * 2);
        items.VisibleInstanceCount = itemCount;

        int i = 0;
        foreach (var e in buffer)
        {
            var center = RenderMath.ToPixels(RenderMath.Interpolate(e.PreviousPosition, e.Position, alpha)).ToGodot();
            if (e.Kind == EntityKind.DroppedItem)
            {
                items.SetInstanceTransform2D(i, new Transform2D(0f, center));
                items.SetInstanceColor(i, visuals.ItemColor(e.Item.ItemId).ToGodot());
                i++;
                continue;
            }

            seen.Add(e.NetworkId);
            if (!players.TryGetValue(e.NetworkId, out var rect))
            {
                rect = new ColorRect { Color = visuals.Player.ToGodot() };
                AddChild(rect);
                players[e.NetworkId] = rect;
            }
            var size = RenderMath.ToPixels(e.Size).ToGodot();
            rect.Size = size;
            rect.Position = center - size / 2;
        }

        gone.Clear();
        foreach (var id in players.Keys)
            if (!seen.Contains(id)) gone.Add(id);
        foreach (var id in gone)
        {
            players[id].QueueFree();
            players.Remove(id);
        }
    }
}
