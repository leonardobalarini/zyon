using System.Numerics;
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public class PlayerFileTests
{
    private static PlayerSave RoundTrip(PlayerSave save, Registry registry)
    {
        using var ms = new MemoryStream();
        PlayerFile.Write(ms, save);
        ms.Position = 0;
        return PlayerFile.Read(ms, registry);
    }

    private static PlayerSave ReadJson(string json) =>
        PlayerFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), TestData.Registry());

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var save = new PlayerSave("Leo", 2, [new SavedSlot(0, "basic_pickaxe", 1), new SavedSlot(15, "dirt", 37)]);

        var loaded = RoundTrip(save, TestData.Registry());

        Assert.Equal("Leo", loaded.Name);
        Assert.Equal(2, loaded.SelectedSlot);
        Assert.Equal(save.Slots, loaded.Slots);
    }

    [Fact]
    public void New_HasStarterItems()
    {
        var save = PlayerSave.New("Ana", TestData.Registry());

        Assert.Equal(0, save.SelectedSlot);
        Assert.Contains(new SavedSlot(0, "basic_pickaxe", 1), save.Slots);
    }

    [Fact]
    public void CaptureAndSpawnInto_RestoreInventoryAndSelection()
    {
        using var h = new SimHarness();
        h.Give("dirt", 12);
        h.Send(new Zyon.Core.Sim.SelectHotbarSlot(h.PlayerId, 1));

        var save = PlayerSave.Capture(h.Sim, h.PlayerId, "Leo");
        using var sim2 = new Zyon.Core.Sim.Simulation(h.Registry, TestWorlds.Flat(h.Registry, 64, 64, 40));
        int id2 = save.SpawnInto(sim2, new Vector2(20f, h.StandingY));

        var inv = sim2.GetInventory(id2)!;
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), inv[0].ItemId);
        Assert.Equal(new ItemStack(h.Registry.ItemId("dirt"), 12), inv[1]);
        Assert.True(inv[2].IsEmpty);
        Assert.Equal(1, sim2.GetSelectedSlot(id2));
    }

    [Fact]
    public void Write_ProducesReadableJsonWithItemNames()
    {
        using var ms = new MemoryStream();
        PlayerFile.Write(ms, new PlayerSave("Leo", 0, [new SavedSlot(3, "stone", 4)]));

        string json = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("\"formatVersion\": 1", json);
        Assert.Contains("\"stone\"", json);
    }

    [Theory]
    [InlineData("""{"formatVersion": 2, "name": "L", "selectedSlot": 0, "inventory": []}""", "versão mais nova")]
    [InlineData("""{"formatVersion": 0, "name": "L", "selectedSlot": 0, "inventory": []}""", "não suportada")]
    [InlineData("""{"name": "L", "selectedSlot": 0, "inventory": []}""", "formatVersion")]
    [InlineData("""{"formatVersion": 1, "name": " ", "selectedSlot": 0, "inventory": []}""", "name")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 10, "inventory": []}""", "selectedSlot")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0}""", "inventory")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [null]}""", "inventory[0]")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 50, "item": "dirt", "count": 1}]}""", "slot 50")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "dirt", "count": 1}, {"slot": 1, "item": "dirt", "count": 1}]}""", "repetido")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "mithril", "count": 1}]}""", "mithril")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "dirt", "count": 0}]}""", "quantidade")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "basic_pickaxe", "count": 2}]}""", "quantidade")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [], "extra": 1}""", ".zplayer")]
    [InlineData("""{"formatVersion": 1""", "JSON inválido")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ReadJson(json));

        Assert.Contains(".zplayer", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }
}
