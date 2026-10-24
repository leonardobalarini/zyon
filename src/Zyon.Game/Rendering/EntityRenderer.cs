using System.Collections.Generic;
using Godot;
using Zyon.Client;
using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>
/// Jogadores em camadas animadas (<see cref="PlayerNode"/>); itens caídos em lote com MultiMesh (um draw call),
/// cada instância com a célula do ícone em INSTANCE_CUSTOM.x.
/// </summary>
public partial class EntityRenderer : Node2D
{
    private const float ItemPixels = 12f;

    // A UV do quad (0..1 na largura) vira a da célula INSTANCE_CUSTOM.x de uma tira de "cells" células.
    private const string ItemShader = """
        shader_type canvas_item;
        uniform float cells;
        void vertex() {
            UV = vec2((UV.x + INSTANCE_CUSTOM.x) / cells, UV.y);
        }
        """;

    private readonly List<EntityView> buffer = new();
    private readonly Dictionary<int, PlayerNode> players = new();
    private readonly HashSet<int> seen = new();
    private readonly List<int> gone = new();
    private readonly HashSet<int> localBodyFrames = new();
    private IWorldView view = null!;
    private ArtCatalog art = null!;
    private ArtTextures textures = null!;
    private PlayerAnimator animator = null!;
    private MultiMesh items = null!;
    private int localPlayerId;

    /// <summary>Quadros de corpo diferentes já mostrados para o jogador local (smoke test).</summary>
    public int LocalBodyFramesSeen => localBodyFrames.Count;

    /// <summary>O jogador local já foi mostrado num quadro de golpe (smoke test).</summary>
    public bool LocalSwingSeen { get; private set; }

    public void Init(IWorldView worldView, ArtCatalog artCatalog, ArtTextures artTextures, int localPlayer)
    {
        view = worldView;
        art = artCatalog;
        textures = artTextures;
        localPlayerId = localPlayer;
        animator = new PlayerAnimator(art, view.Registry);
        // Formato (cores/dados extras) antes de InstanceCount: o Godot só aceita mudá-lo com 0 instâncias.
        items = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = false,
            UseCustomData = true,
            Mesh = ItemQuad(),
        };
        var material = new ShaderMaterial { Shader = new Shader { Code = ItemShader } };
        material.SetShaderParameter("cells", (float)(textures.Items.GetWidth() / TileAtlasLayout.TileSize));
        AddChild(new MultiMeshInstance2D
        {
            Multimesh = items,
            Texture = textures.Items,
            TextureFilter = TextureFilterEnum.Nearest,
            Material = material,
        });
    }

    public void Refresh(float alpha, float dt)
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
            var position = RenderMath.Interpolate(e.PreviousPosition, e.Position, alpha);
            if (e.Kind == EntityKind.DroppedItem)
            {
                var center = RenderMath.ToPixels(position).ToGodot();
                items.SetInstanceTransform2D(i, new Transform2D(0f, center));
                var itemArt = e.Item.ItemId < art.ItemByItemId.Count ? art.ItemByItemId[e.Item.ItemId] : null;
                items.SetInstanceCustomData(i, new Color(itemArt?.Cell ?? 0, 0, 0, 0));
                i++;
                continue;
            }

            seen.Add(e.NetworkId);
            if (!players.TryGetValue(e.NetworkId, out var node))
            {
                node = new PlayerNode();
                node.Init(art, textures);
                AddChild(node);
                players[e.NetworkId] = node;
            }
            // Origem do nó nos pés: centro do AABB + meia altura, em pixels inteiros (o mesmo arredondamento da câmera).
            node.Position = RenderMath.SnapToPixel(RenderMath.ToPixels(position + new System.Numerics.Vector2(0, e.Size.Y / 2))).ToGodot();
            var pose = animator.Update(e, dt, ref node.State);
            node.Apply(pose);
            if (e.NetworkId == localPlayerId)
            {
                localBodyFrames.Add(pose.BodyFrame);
                LocalSwingSeen |= pose.ArmFrame >= art.Manifest.Player.SwingFirst;
            }
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

    /// <summary>Quad centrado de ItemPixels, UV (0,0) no canto de cima à esquerda (Y para baixo, como a tela).</summary>
    private static ArrayMesh ItemQuad()
    {
        const float h = ItemPixels / 2;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[] { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h) };
        arrays[(int)Mesh.ArrayType.TexUV] = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
