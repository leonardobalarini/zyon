namespace Zyon.Core.Persistence;

/// <summary>
/// O arquivo está íntegro, mas esta versão do jogo não consegue usá-lo (formato mais novo, bloco/parede/item que sumiu
/// dos dados). Não é corrupção: a leitura não deve cair para o backup, que teria o mesmo problema ou seria mais antigo.
/// </summary>
public sealed class SaveIncompatibleException(string message) : Exception(message);
