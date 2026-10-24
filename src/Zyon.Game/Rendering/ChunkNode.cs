using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Um chunk na tela: malha de paredes, de blocos e das bordas de mescla (por cima dos blocos). Cada camada tem uma
/// única ArrayMesh, refeita no lugar (ClearSurfaces + AddSurfaceFromArrays), e buffers gerenciados reaproveitados.
/// As paredes já vêm escurecidas na arte (sem tinta aqui).
/// </summary>
public partial class ChunkNode : Node2D
{
    private readonly MeshInstance2D walls = new();
    private readonly MeshInstance2D blocks = new();
    private readonly MeshInstance2D merge = new();
    private readonly LayerMesh wallMesh = new();
    private readonly LayerMesh blockMesh = new();
    private readonly LayerMesh mergeMesh = new();

    public void Init(Texture2D wallAtlas, Texture2D blockAtlas, Texture2D mergeAtlas)
    {
        Setup(walls, wallAtlas, wallMesh);
        Setup(blocks, blockAtlas, blockMesh);
        Setup(merge, mergeAtlas, mergeMesh);
    }

    public void SetWalls(ChunkMeshData data) => wallMesh.Set(data);

    public void SetBlocks(ChunkMeshData data) => blockMesh.Set(data);

    public void SetMerge(ChunkMeshData data) => mergeMesh.Set(data);

    private void Setup(MeshInstance2D instance, Texture2D atlas, LayerMesh mesh)
    {
        instance.Texture = atlas;
        instance.Mesh = mesh.Mesh;
        instance.TextureFilter = TextureFilterEnum.Nearest;
        AddChild(instance);
    }

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
