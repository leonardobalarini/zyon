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
    [InlineData("""{"formatVersion": 0, "name": "L", "selectedSlot": 0, "inventory": []}""", "não suportada")]
    [InlineData("""{"name": "L", "selectedSlot": 0, "inventory": []}""", "formatVersion")]
    [InlineData("""{"formatVersion": 1, "name": " ", "selectedSlot": 0, "inventory": []}""", "name")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 10, "inventory": []}""", "selectedSlot")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0}""", "inventory")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [null]}""", "inventory[0]")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 50, "item": "dirt", "count": 1}]}""", "slot 50")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "dirt", "count": 1}, {"slot": 1, "item": "dirt", "count": 1}]}""", "repetido")]
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../x")]
    [InlineData("a:b")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Leo.")]
    [InlineData("abcdefghijklmnopqrstuvwxy")]
    [InlineData("Le\no")]
    [InlineData("CON.txt")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("Leo​")]
    [InlineData("Leo‮abc")]
    [InlineData("﻿Leo")]
    [InlineData("CONIN$")]
    [InlineData("conout$")]
    [InlineData("COM¹")]
    [InlineData("LPT²")]
    [InlineData("COM³.txt")]
    public void ValidateName_Invalid_Throws(string name)
    {
        var ex = Assert.Throws<DataException>(() => PlayerSave.ValidateName(name));

        Assert.Contains(".zplayer", ex.Message);
    }

    [Fact]
    public void ValidateName_TrimsValidName() => Assert.Equal("Leo", PlayerSave.ValidateName(" Leo "));

    [Fact]
    public void ValidateName_ExactlyMaxLength_IsValid()
    {
        string name = new('a', PlayerSave.MaxNameLength);

        Assert.Equal(24, name.Length);
        Assert.Equal(name, PlayerSave.ValidateName(name));
    }

    [Fact]
    public void ValidateName_ReturnsNfc() => Assert.Equal("Léo", PlayerSave.ValidateName("Léo"));

    [Theory]
    [InlineData("""{"formatVersion": 2, "name": "L", "selectedSlot": 0, "inventory": []}""", "versão mais nova")]
    [InlineData("""{"formatVersion": 1, "name": "L", "selectedSlot": 0, "inventory": [{"slot": 1, "item": "mithril", "count": 1}]}""", "mithril")]
    public void IncompatibleFile_ThrowsSaveIncompatible(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<SaveIncompatibleException>(() => ReadJson(json));

        Assert.Contains(".zplayer", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void Capture_InvalidName_Throws()
    {
        using var h = new SimHarness();

        var ex = Assert.Throws<DataException>(() => PlayerSave.Capture(h.Sim, h.PlayerId, "CON"));
        Assert.Contains(".zplayer", ex.Message);
    }

    [Fact]
    public void Capture_ReturnsNormalizedName()
    {
        using var h = new SimHarness();

        Assert.Equal("Léo", PlayerSave.Capture(h.Sim, h.PlayerId, " Léo ").Name);
    }

    [Fact]
    public void Write_InvalidName_ThrowsAndWritesNothing()
    {
        using var ms = new MemoryStream();

        Assert.Throws<DataException>(() => PlayerFile.Write(ms, new PlayerSave("a/b", 0, [])));
        Assert.Equal(0, ms.Length);
    }

    [Fact]
    public void SpawnInto_SelectedSlotOutsideHotbar_Throws()
    {
        using var h = new SimHarness();
        var save = new PlayerSave("Leo", 12, []);

        var ex = Assert.Throws<DataException>(() => save.SpawnInto(h.Sim, new Vector2(20f, h.StandingY)));
        Assert.Contains(".zplayer", ex.Message);
        Assert.Contains("12", ex.Message);
    }

    [Fact]
    public void SpawnInto_UnknownItem_Throws()
    {
        using var h = new SimHarness();
        var save = new PlayerSave("Leo", 0, [new SavedSlot(0, "mithril", 1)]);

        var ex = Assert.Throws<SaveIncompatibleException>(() => save.SpawnInto(h.Sim, new Vector2(20f, h.StandingY)));
        Assert.Contains("mithril", ex.Message);
    }
}
