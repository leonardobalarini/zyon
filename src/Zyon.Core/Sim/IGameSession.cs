namespace Zyon.Core.Sim;

/// <summary>Ponto de troca entre jogo local e (futuro) jogo em rede. A apresentação só fala com isto.</summary>
public interface IGameSession : IDisposable
{
    int LocalPlayerId { get; }
    IWorldView World { get; }

    /// <summary>Fração [0, 1) entre o tick anterior e o próximo, para interpolar a renderização.</summary>
    float InterpolationAlpha { get; }

    void SetInput(PlayerInput input);
    void Send(ICommand command);
    void Update(double deltaSeconds);
    IReadOnlyList<IGameEvent> DrainEvents();
}
