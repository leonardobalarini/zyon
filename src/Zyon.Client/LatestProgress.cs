namespace Zyon.Client;

/// <summary>
/// IProgress que só guarda o último valor (seguro entre threads). A UI lê Latest no seu próprio frame,
/// em vez de receber callbacks de outra thread.
/// </summary>
public sealed class LatestProgress<T> : IProgress<T> where T : class
{
    private T? latest;

    public T? Latest => Volatile.Read(ref latest);

    public void Report(T value) => Volatile.Write(ref latest, value);
}
