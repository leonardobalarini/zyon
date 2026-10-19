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

    [Fact]
    public void EveryBlockDropsAnItemThatPlacesIt()
    {
        var r = TestData.Registry();

        foreach (var block in r.Blocks.Skip(1))
        {
            Assert.NotEqual(0, block.DropItemId);
            Assert.Equal(block.Id, r.Item(block.DropItemId).PlacesBlockId);
        }
    }

    [Fact]
    public void PlayerStartsWithPickaxe()
    {
        var r = TestData.Registry();

        Assert.Contains(r.StarterItems, s => s.ItemId == r.ItemId("basic_pickaxe") && s.Count == 1);
    }
}
