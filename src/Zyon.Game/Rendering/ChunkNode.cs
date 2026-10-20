using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Um chunk na tela: malha de paredes (mais escura) e malha de blocos. Cada camada tem uma única
/// ArrayMesh, refeita no lugar (ClearSurfaces + AddSurfaceFromArrays), e buffers gerenciados reaproveitados.
/// </summary>
public partial class ChunkNode : Node2D
{
    private static readonly Color WallTint = new(0.85f, 0.85f, 0.85f);

    private readonly MeshInstance2D walls = new();
    private readonly MeshInstance2D blocks = new();
    private readonly LayerMesh wallMesh = new();
    private readonly LayerMesh blockMesh = new();

    public void Init(Texture2D wallAtlas, Texture2D blockAtlas)
    {
        walls.Texture = wallAtlas;
        walls.Modulate = WallTint;
        walls.Mesh = wallMesh.Mesh;
        blocks.Texture = blockAtlas;
        blocks.Mesh = blockMesh.Mesh;
        AddChild(walls);
        AddChild(blocks);
    }

    public void SetWalls(ChunkMeshData data) => wallMesh.Set(data);

    public void SetBlocks(ChunkMeshData data) => blockMesh.Set(data);

    /// <summary>
    /// Malha de uma camada. AddSurfaceFromArrays copia o array inteiro, então os buffers têm o tamanho
    /// exato da contagem atual e só são realocados quando ela muda.
    /// </summary>
    private sealed class LayerMesh
    {
        private readonly Godot.Collections.Array arrays = new();
        private Vector2[] vertices = [];
        private Vector2[] uvs = [];
        private int[] indices = [];

        public ArrayMesh Mesh { get; } = new();

        public LayerMesh() => arrays.Resize((int)Godot.Mesh.ArrayType.Max);

        public void Set(ChunkMeshData data)
        {
            Mesh.ClearSurfaces();
            if (data.Indices.Count == 0) return;

            if (vertices.Length != data.Vertices.Count) vertices = new Vector2[data.Vertices.Count];
            if (uvs.Length != data.Uvs.Count) uvs = new Vector2[data.Uvs.Count];
            if (indices.Length != data.Indices.Count) indices = new int[data.Indices.Count];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = data.Vertices[i].ToGodot();
            for (int i = 0; i < uvs.Length; i++) uvs[i] = data.Uvs[i].ToGodot();
            data.Indices.CopyTo(indices);

            arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvs;
            arrays[(int)Godot.Mesh.ArrayType.Index] = indices;
            Mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        }
    }
}
