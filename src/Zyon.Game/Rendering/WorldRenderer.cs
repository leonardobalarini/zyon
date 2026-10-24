using System.Collections.Generic;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>
/// Mantém nós só para os chunks visíveis (+ margem), reaproveitando-os. Chunks novos na tela são
/// montados na hora; chunks alterados (TileChanged) são reconstruídos com orçamento por frame.
/// </summary>
public partial class WorldRenderer : Node2D
{
    private const int MarginChunks = 1;
    private const int RebuildBudgetPerFrame = 6;

    private readonly Dictionary<(int, int), ChunkNode> active = new();
    private readonly Stack<ChunkNode> pool = new();
    private readonly ChunkMeshData scratch = new();
    private readonly List<(int, int)> leaving = new();
    private IWorldView view = null!;
    private TileAtlasLayout blockLayout = null!;
    private TileAtlasLayout wallLayout = null!;
    private MergeAtlasLayout mergeLayout = null!;
    private ArtCatalog art = null!;
    private ArtTextures textures = null!;
    private DirtyChunkTracker dirty = null!;
    private int chunksX;
    private int chunksY;

    public int ActiveChunkCount => active.Count;

    /// <summary>Chunks montados ou remontados na última chamada de <see cref="UpdateVisible"/>.</summary>
    public int RebuildsLastUpdate { get; private set; }

    /// <summary>Quantos de <see cref="RebuildsLastUpdate"/> foram chunks que entraram na faixa visível.</summary>
    public int NewChunksLastUpdate { get; private set; }

    public void Init(IWorldView worldView, ArtCatalog artCatalog, ArtTextures artTextures)
    {
        view = worldView;
        art = artCatalog;
        textures = artTextures;
        chunksX = (view.Width + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        chunksY = (view.Height + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        dirty = new DirtyChunkTracker(chunksX, chunksY);
        // Linhas dos layouts = linhas reais das texturas (ArtTextures já conferiu que cobrem as linhas do catálogo):
        // as UVs só ficam certas se o layout tiver a mesma altura do PNG.
        int variations = art.Manifest.Variations;
        blockLayout = new TileAtlasLayout(Rows(textures.Tiles), variations);
        wallLayout = new TileAtlasLayout(Rows(textures.Walls), variations);
        mergeLayout = new MergeAtlasLayout(Rows(textures.Merge), variations);
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged t)
            dirty.MarkTile(t.X, t.Y);
    }

    public void UpdateVisible(Rect2 visiblePixels)
    {
        var range = ChunkVisibility.Range(visiblePixels.Position.X, visiblePixels.Position.Y,
            visiblePixels.Size.X, visiblePixels.Size.Y, MarginChunks, chunksX, chunksY);

        RebuildsLastUpdate = 0;
        NewChunksLastUpdate = 0;
        leaving.Clear();
        foreach (var key in active.Keys)
            if (!range.Contains(key.Item1, key.Item2)) leaving.Add(key);
        foreach (var key in leaving)
        {
            var node = active[key];
            node.Visible = false;
            pool.Push(node);
            active.Remove(key);
        }

        for (int cy = range.MinY; cy <= range.MaxY; cy++)
        for (int cx = range.MinX; cx <= range.MaxX; cx++)
        {
            if (active.ContainsKey((cx, cy))) continue;
            var node = pool.Count > 0 ? pool.Pop() : CreateNode();
            node.Position = new Vector2(cx * ChunkVisibility.ChunkPixels, cy * ChunkVisibility.ChunkPixels);
            node.Visible = true;
            Rebuild(node, cx, cy);
            NewChunksLastUpdate++;
            active[(cx, cy)] = node;
        }

        for (int built = 0; built < RebuildBudgetPerFrame && dirty.TryTake(out var c);)
        {
            if (!active.TryGetValue(c, out var node)) continue;   // fora da tela: será montado ao aparecer
            Rebuild(node, c.Cx, c.Cy);
            built++;
        }
    }

    private ChunkNode CreateNode()
    {
        var node = new ChunkNode();
        node.Init(textures.Walls, textures.Tiles, textures.Merge);
        AddChild(node);
        return node;
    }

    private void Rebuild(ChunkNode node, int cx, int cy)
    {
        RebuildsLastUpdate++;
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Walls, wallLayout, art.WallRowByWallId, scratch);
        node.SetWalls(scratch);
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Blocks, blockLayout, art.TileRowByBlockId, scratch);
        node.SetBlocks(scratch);
        ChunkMeshBuilder.BuildMerge(view, cx, cy, mergeLayout, art.MergeRankByBlockId, scratch);
        node.SetMerge(scratch);
    }

    private static int Rows(Texture2D atlas) => atlas.GetHeight() / TileAtlasLayout.TileSize;
}
