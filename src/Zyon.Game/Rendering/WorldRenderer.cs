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
    private const int Variations = 3;

    private readonly Dictionary<(int, int), ChunkNode> active = new();
    private readonly Stack<ChunkNode> pool = new();
    private readonly ChunkMeshData scratch = new();
    private readonly List<(int, int)> leaving = new();
    private IWorldView view = null!;
    private TileAtlasLayout blockLayout = null!;
    private TileAtlasLayout wallLayout = null!;
    private Texture2D blockAtlas = null!;
    private Texture2D wallAtlas = null!;
    private DirtyChunkTracker dirty = null!;
    private int chunksX;
    private int chunksY;

    public int ActiveChunkCount => active.Count;

    /// <summary>Chunks montados ou remontados na última chamada de <see cref="UpdateVisible"/>.</summary>
    public int RebuildsLastUpdate { get; private set; }

    /// <summary>Quantos de <see cref="RebuildsLastUpdate"/> foram chunks que entraram na faixa visível.</summary>
    public int NewChunksLastUpdate { get; private set; }

    public void Init(IWorldView worldView, ClientVisuals visuals)
    {
        view = worldView;
        chunksX = (view.Width + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        chunksY = (view.Height + Zyon.Core.Tiles.Chunk.Size - 1) / Zyon.Core.Tiles.Chunk.Size;
        dirty = new DirtyChunkTracker(chunksX, chunksY);
        blockLayout = new TileAtlasLayout(view.Registry.Blocks.Count, Variations);
        wallLayout = new TileAtlasLayout(view.Registry.Walls.Count, Variations);
        blockAtlas = BuildAtlas(blockLayout, visuals.BlockColors);
        wallAtlas = BuildAtlas(wallLayout, visuals.WallColors);
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
        node.Init(wallAtlas, blockAtlas);
        AddChild(node);
        return node;
    }

    private void Rebuild(ChunkNode node, int cx, int cy)
    {
        RebuildsLastUpdate++;
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Walls, wallLayout, scratch);
        node.SetWalls(scratch);
        ChunkMeshBuilder.Build(view, cx, cy, TileLayer.Blocks, blockLayout, scratch);
        node.SetBlocks(scratch);
    }

    private static Texture2D BuildAtlas(TileAtlasLayout layout, IReadOnlyList<Rgb?> colors)
    {
        byte[] rgba = PlaceholderAtlas.BuildRgba(layout, colors);
        var image = Image.CreateFromData(layout.WidthPx, layout.HeightPx, false, Image.Format.Rgba8, rgba);
        return ImageTexture.CreateFromImage(image);
    }
}
