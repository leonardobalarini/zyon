using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class DataFilesTests
{
    [Fact]
    public void RealDataFiles_LoadWithoutErrors()
    {
        var r = TestData.Registry();

        foreach (var name in new[] { "dirt", "stone", "deep_stone", "ore_a", "ore_b" })
            Assert.True(r.TryGetBlockId(name, out _), $"bloco '{name}' ausente");
        Assert.True(r.TryGetWallId("dirt_wall", out _));
        Assert.True(r.TryGetWallId("stone_wall", out _));
        Assert.Equal(ItemKind.Tool, r.Item(r.ItemId("basic_pickaxe")).Kind);
    }

    /// <summary>Folhas e decorativos não dão item; tronco e galho dão madeira; o resto dá o item que o coloca de volta.</summary>
    [Fact]
    public void EveryBlockDropsAnItemThatPlacesIt_ExceptTreesAndDecorations()
    {
        var r = TestData.Registry();
        string[] noDrop = ["pine_leaves", "dry_grass", "frozen_shrub", "dry_bush"];

        foreach (var block in r.Blocks.Skip(1))
        {
            if (noDrop.Contains(block.Name))
            {
                Assert.Equal(0, block.DropItemId);
                continue;
            }
            Assert.NotEqual(0, block.DropItemId);
            if (!block.Tree) Assert.Equal(block.Id, r.Item(block.DropItemId).PlacesBlockId);
        }
    }

    [Fact]
    public void BiomeContent_Exists()
    {
        var r = TestData.Registry();
        string[] trees = ["dead_trunk", "dead_branch", "pine_trunk", "pine_leaves", "cactus"];

        foreach (var name in trees) Assert.True(r.Block(r.BlockId(name)).Tree, name);
        foreach (var name in new[] { "dead_trunk", "pine_trunk", "cactus" }) Assert.True(r.Block(r.BlockId(name)).Trunk, name);
        foreach (var name in new[] { "dead_branch", "pine_leaves" }) Assert.False(r.Block(r.BlockId(name)).Trunk, name);
        foreach (var name in new[] { "snow", "ice", "sand", "sandstone", "wood" })
        {
            Assert.True(r.Block(r.BlockId(name)).Solid, name);
            Assert.False(r.Block(r.BlockId(name)).Tree, name);
        }
        Assert.Equal(r.ItemId("wood"), r.Block(r.BlockId("dead_trunk")).DropItemId);
        foreach (var wall in new[] { "snow_wall", "ice_wall", "sand_wall", "sandstone_wall" })
            Assert.True(r.TryGetWallId(wall, out _), wall);
    }

    [Fact]
    public void PlayerStartsWithPickaxe()
    {
        var r = TestData.Registry();

        Assert.Contains(r.StarterItems, s => s.ItemId == r.ItemId("basic_pickaxe") && s.Count == 1);
    }
}
