using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

public class ChunkMeshBuilderTests
{
    private static readonly Registry R = TestData.Registry();
    private static readonly ArtCatalog Art = ArtCatalog.LoadFromDirectory(TestData.DataDir, R);
    private static readonly MergeAtlasLayout Merge = new(Art.Manifest.MergeOrder.Count, Art.Manifest.Variations);
    private static readonly TileAtlasLayout Atlas = new(R.Blocks.Count - 1, Art.Manifest.Variations);
    private static readonly TileAtlasLayout WallAtlas = new(R.Walls.Count - 1, Art.Manifest.Variations);
    private static readonly ushort Dirt = R.BlockId("dirt"), Stone = R.BlockId("stone");
    private static readonly ushort DirtWall = R.WallId("dirt_wall");

    [Fact]
    public void EmptyChunk_HasNoQuads()
    {
        using var view = TestData.View(new TileWorld(64, 64));
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);

        Assert.Equal(0, data.QuadCount);
        Assert.Empty(data.Indices);
    }

    [Fact]
    public void FullChunk_HasOneQuadPerTile()
    {
        var w = new TileWorld(64, 64);
        for (int y = 32; y < 64; y++)
            for (int x = 32; x < 64; x++)
                w.SetBlock(x, y, Dirt);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);

        Assert.Equal(Chunk.Area, data.QuadCount);
        Assert.Equal(Chunk.Area * 4, data.Vertices.Count);
        Assert.Equal(Chunk.Area * 6, data.Indices.Count);
    }

    [Fact]
    public void Quad_IsPlacedInChunkLocalPixels_WithAtlasUvs()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(35, 33, Stone);   // local (3, 1) do chunk (1, 1), isolado
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);

        Assert.Equal(1, data.QuadCount);
        Assert.Equal(new Vector2(48f, 16f), data.Vertices[0]);
        Assert.Equal(new Vector2(64f, 32f), data.Vertices[2]);
        var uv = Atlas.Uv(Art.TileRowByBlockId[Stone], 0, AutoTile.Variation(35, 33, Atlas.Variations));
        Assert.Equal(new Vector2(uv.U0, uv.V0), data.Uvs[0]);
        Assert.Equal(new Vector2(uv.U1, uv.V1), data.Uvs[2]);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, data.Indices);
    }

    [Fact]
    public void WallBehindBlock_IsSkipped()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, DirtWall);
        w.SetWall(6, 5, DirtWall);
        w.SetBlock(6, 5, Dirt);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, WallAtlas, Art.WallRowByWallId, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void WallBehindNonSolidBlock_IsDrawn()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, DirtWall);
        w.SetBlock(5, 5, R.BlockId("torch"));
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, WallAtlas, Art.WallRowByWallId, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void Build_ReusesTheOutputBuffers()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(1, 1, Dirt);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);
        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void PartialChunkAtWorldEdge_HasQuadsOnlyForExistingTiles()
    {
        var w = new TileWorld(40, 40);
        for (int y = 32; y < 40; y++)
            for (int x = 32; x < 40; x++)
                w.SetBlock(x, y, Dirt);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, Art.TileRowByBlockId, data);

        Assert.Equal(64, data.QuadCount);
    }

    [Fact]
    public void Merge_SofterNeighbour_AddsOneQuadOnThatSide()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(5, 5, R.BlockId("stone"));
        w.SetBlock(5, 4, R.BlockId("dirt"));      // terra (mais mole) ao norte da rocha
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.BuildMerge(view, 0, 0, Merge, Art.MergeRankByBlockId, data);

        Assert.Equal(1, data.QuadCount);
        Assert.Equal(new System.Numerics.Vector2(5 * 16, 5 * 16), data.Vertices[0]);
        var uv = Merge.Uv(Art.Manifest.MergeOrder.IndexOf("dirt"), side: 0, AutoTile.Variation(5, 5, Merge.Variations));
        Assert.Equal(uv.U0, data.Uvs[0].X, 5);
        Assert.Equal(uv.V0, data.Uvs[0].Y, 5);
    }

    [Fact]
    public void Merge_NeverWithAirNonSolidHarderOrSameMaterial()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(5, 5, R.BlockId("dirt"));
        w.SetBlock(6, 5, R.BlockId("stone"));     // mais duro: a borda vai no tile da rocha, não no da terra
        w.SetBlock(4, 5, R.BlockId("dirt"));      // mesmo material
        w.SetBlock(5, 4, R.BlockId("torch"));     // não sólido
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.BuildMerge(view, 0, 0, Merge, Art.MergeRankByBlockId, data);

        Assert.Equal(1, data.QuadCount);                                      // só a borda da terra sobre a rocha (lado O da rocha)
        Assert.Equal(new System.Numerics.Vector2(6 * 16, 5 * 16), data.Vertices[0]);
    }

    [Fact]
    public void Blocks_UseTheArtRow_NotTheId()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(1, 1, R.BlockId("stone"));
        using var view = TestData.View(w);
        var data = new ChunkMeshData();
        var layout = new TileAtlasLayout(R.Blocks.Count - 1, Art.Manifest.Variations);

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, layout, Art.TileRowByBlockId, data);

        var uv = layout.Uv(Art.TileRowByBlockId[R.BlockId("stone")], 0, AutoTile.Variation(1, 1, layout.Variations));
        Assert.Equal(uv.V0, data.Uvs[0].Y, 5);
    }
}
