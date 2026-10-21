namespace Zyon.Core.Tests;

/// <summary>Testes de desempenho rodam sozinhos, depois dos demais: em paralelo, a disputa de CPU distorce os tempos.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    public const string Name = "Desempenho";
}
