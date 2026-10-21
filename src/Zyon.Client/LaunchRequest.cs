using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>O que a tela de carregamento deve fazer: criar um mundo novo ou carregar um existente, com este personagem.</summary>
public sealed record LaunchRequest(
    PlayerSave Player,
    IReadOnlyList<string> Warnings,
    string WorldName,
    bool CreateNew,
    int Width,
    int Height,
    int Seed);
