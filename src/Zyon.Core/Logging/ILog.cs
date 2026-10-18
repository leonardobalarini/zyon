namespace Zyon.Core.Logging;

public interface ILog
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    private NullLog() { }

    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
