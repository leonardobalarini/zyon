using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

internal static class TestData
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Registry Registry() => RegistryLoader.LoadFromDirectory(DataDir);

    public static WorldGenConfig WorldGenConfig() => WorldGenConfigLoader.LoadFromDirectory(DataDir);

    /// <summary>Registry real, mas com physics.json substituído (campos ausentes usam os padrões).</summary>
    public static Registry RegistryWithPhysics(string physicsJson)
    {
        string Read(string file) => File.ReadAllText(Path.Combine(DataDir, file));
        return RegistryLoader.Load(Read(RegistryLoader.BlocksFile), Read(RegistryLoader.WallsFile),
            Read(RegistryLoader.ItemsFile), physicsJson, Read(RegistryLoader.PlayerFile));
    }
}
