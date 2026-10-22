using System.Numerics;
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

public class ChunkMeshBuilderTests
{
    private static readonly TileAtlasLayout Atlas = new(6, 3);

    [Fact]
    public void EmptyChunk_HasNoQuads()
    {
        using var view = TestData.View(new TileWorld(64, 64));
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);

        Assert.Equal(0, data.QuadCount);
        Assert.Empty(data.Indices);
    }

    [Fact]
    public void FullChunk_HasOneQuadPerTile()
    {
        var w = new TileWorld(64, 64);
        for (int y = 32; y < 64; y++)
            for (int x = 32; x < 64; x++)
                w.SetBlock(x, y, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, data);

        Assert.Equal(Chunk.Area, data.QuadCount);
        Assert.Equal(Chunk.Area * 4, data.Vertices.Count);
        Assert.Equal(Chunk.Area * 6, data.Indices.Count);
    }

    [Fact]
    public void Quad_IsPlacedInChunkLocalPixels_WithAtlasUvs()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(35, 33, 2);   // local (3, 1) do chunk (1, 1), isolado
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, data);

        Assert.Equal(1, data.QuadCount);
        Assert.Equal(new Vector2(48f, 16f), data.Vertices[0]);
        Assert.Equal(new Vector2(64f, 32f), data.Vertices[2]);
        var uv = Atlas.Uv(2, 0, AutoTile.Variation(35, 33, Atlas.Variations));
        Assert.Equal(new Vector2(uv.U0, uv.V0), data.Uvs[0]);
        Assert.Equal(new Vector2(uv.U1, uv.V1), data.Uvs[2]);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, data.Indices);
    }

    [Fact]
    public void WallBehindBlock_IsSkipped()
    {
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, 1);
        w.SetWall(6, 5, 1);
        w.SetBlock(6, 5, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void WallBehindNonSolidBlock_IsDrawn()
    {
        var r = TestData.Registry();
        var w = new TileWorld(64, 64);
        w.SetWall(5, 5, 1);
        w.SetBlock(5, 5, r.BlockId("torch"));
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Walls, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void Build_ReusesTheOutputBuffers()
    {
        var w = new TileWorld(64, 64);
        w.SetBlock(1, 1, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);
        ChunkMeshBuilder.Build(view, 0, 0, TileLayer.Blocks, Atlas, data);

        Assert.Equal(1, data.QuadCount);
    }

    [Fact]
    public void PartialChunkAtWorldEdge_HasQuadsOnlyForExistingTiles()
    {
        var w = new TileWorld(40, 40);
        for (int y = 32; y < 40; y++)
            for (int x = 32; x < 40; x++)
                w.SetBlock(x, y, 1);
        using var view = TestData.View(w);
        var data = new ChunkMeshData();

        ChunkMeshBuilder.Build(view, 1, 1, TileLayer.Blocks, Atlas, data);

        Assert.Equal(64, data.QuadCount);
    }
}
