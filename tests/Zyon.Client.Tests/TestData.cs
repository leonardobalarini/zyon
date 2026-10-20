using Zyon.Core.Data;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Client.Tests;

internal static class TestData
{
    public static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    public static Registry Registry() => RegistryLoader.LoadFromDirectory(DataDir);

    /// <summary>Simulação sobre um mundo vazio do tamanho dado (IWorldView para os testes de apresentação).</summary>
    public static Simulation View(TileWorld world) => new(Registry(), world);
}
