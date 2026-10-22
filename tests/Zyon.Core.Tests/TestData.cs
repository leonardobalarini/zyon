using System.Text.Json.Nodes;
using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

internal static class TestData
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Registry Registry() => RegistryLoader.LoadFromDirectory(DataDir);

    /// <summary>Registry real, mas só com a picareta inicial (os testes de inventário assumem o slot 1 livre). O SimHarness usa este por padrão.</summary>
    public static Registry RegistryPickaxeOnly()
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        return RegistryLoader.Load(Read(RegistryLoader.BlocksFile), Read(RegistryLoader.WallsFile),
            Read(RegistryLoader.ItemsFile), Read(RegistryLoader.PhysicsFile),
            """{"starterItems":[{"item":"basic_pickaxe","count":1}]}""");
    }

    public static WorldGenConfig WorldGenConfig() => WorldGenConfigLoader.LoadFromDirectory(DataDir);

    /// <summary>Registry real, mas com physics.json substituído (campos ausentes usam os padrões).</summary>
    public static Registry RegistryWithPhysics(string physicsJson)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        return RegistryLoader.Load(Read(RegistryLoader.BlocksFile), Read(RegistryLoader.WallsFile),
            Read(RegistryLoader.ItemsFile), physicsJson, Read(RegistryLoader.PlayerFile));
    }

    /// <summary>Registry real com blocks.json/items.json editados (ex.: reordenar ou remover entradas).</summary>
    public static Registry RegistryWithEdits(Action<JsonArray>? editBlocks = null, Action<JsonArray>? editItems = null)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        var blocks = JsonNode.Parse(Read(RegistryLoader.BlocksFile))!.AsArray();
        var items = JsonNode.Parse(Read(RegistryLoader.ItemsFile))!.AsArray();
        editBlocks?.Invoke(blocks);
        editItems?.Invoke(items);
        return RegistryLoader.Load(blocks.ToJsonString(), Read(RegistryLoader.WallsFile), items.ToJsonString(),
            Read(RegistryLoader.PhysicsFile), Read(RegistryLoader.PlayerFile));
    }

    public static void RemoveById(JsonArray array, string id)
    {
        var node = array.First(n => n!["id"]!.GetValue<string>() == id);
        array.Remove(node);
    }
}
