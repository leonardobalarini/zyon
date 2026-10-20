using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public class ErrorMessagesTests
{
    [Fact]
    public void Incompatible_ExplainsNewerVersionOrRemovedContent()
    {
        var text = ErrorMessages.Describe(new SaveIncompatibleException(".zworld: bloco 'x' não existe mais"));

        Assert.Contains("versão mais nova", text);
        Assert.Contains("bloco 'x'", text);
    }

    [Theory]
    [InlineData(SaveLoadFailure.Inaccessible, "em uso")]
    [InlineData(SaveLoadFailure.Corrupt, "corrompido")]
    [InlineData(SaveLoadFailure.Missing, "não foi encontrado")]
    public void LoadFailure_DependsOnKind(SaveLoadFailure kind, string expected)
    {
        var ex = new SaveLoadException("C:/x.zworld", kind, new IOException("detalhe"), null);

        Assert.Contains(expected, ErrorMessages.Describe(ex));
    }

    [Fact]
    public void DataAndCancel_AndUnexpected()
    {
        Assert.Contains("nome inválido", ErrorMessages.Describe(new DataException(".zworld: nome inválido")));
        Assert.Equal("Cancelado.", ErrorMessages.Describe(new OperationCanceledException()));
        Assert.Contains("inesperado", ErrorMessages.Describe(new InvalidOperationException("bug")));
    }

    [Fact]
    public void Describe_UnwrapsAggregateExceptions()
    {
        var inner = new SaveIncompatibleException(".zworld: formato 9");

        Assert.Contains("formato 9", ErrorMessages.Describe(new AggregateException(inner)));
    }
}
