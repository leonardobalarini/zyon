using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>Um chunk na tela: malha de paredes (mais escura) e malha de blocos.</summary>
public partial class ChunkNode : Node2D
{
    private static readonly Color WallTint = new(0.85f, 0.85f, 0.85f);

    private readonly MeshInstance2D walls = new();
    private readonly MeshInstance2D blocks = new();

    public void Init(Texture2D wallAtlas, Texture2D blockAtlas)
    {
        walls.Texture = wallAtlas;
        walls.Modulate = WallTint;
        blocks.Texture = blockAtlas;
        AddChild(walls);
        AddChild(blocks);
    }

    public void SetWalls(ChunkMeshData data) => walls.Mesh = ToMesh(data);

    public void SetBlocks(ChunkMeshData data) => blocks.Mesh = ToMesh(data);

    private static ArrayMesh? ToMesh(ChunkMeshData data)
    {
        if (data.Indices.Count == 0) return null;
        var vertices = new Vector2[data.Vertices.Count];
        var uvs = new Vector2[data.Uvs.Count];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = data.Vertices[i].ToGodot();
            uvs[i] = data.Uvs[i].ToGodot();
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
