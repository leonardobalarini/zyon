namespace Zyon.Core.Tests;

/// <summary>IProgress síncrono (Progress&lt;T&gt; posta no SynchronizationContext e não serve para testes).</summary>
internal sealed class ListProgress<T> : IProgress<T>
{
    public List<T> Items { get; } = new();

    public void Report(T value) => Items.Add(value);
}
