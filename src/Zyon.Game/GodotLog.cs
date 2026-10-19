using Godot;
using Zyon.Core.Logging;

namespace Zyon.Game;

public sealed class GodotLog : ILog
{
    public void Debug(string message)
    {
        if (OS.IsDebugBuild()) GD.Print($"[debug] {message}");
    }

    public void Info(string message) => GD.Print(message);
    public void Warn(string message) => GD.PushWarning(message);
    public void Error(string message) => GD.PushError(message);
}
