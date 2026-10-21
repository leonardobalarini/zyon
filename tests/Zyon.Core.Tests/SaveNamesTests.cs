using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public class SaveNamesTests
{
    [Theory]
    [InlineData(" Terra Nova ", "Terra Nova")]
    [InlineData("Mundo-1", "Mundo-1")]
    public void Valid_ReturnsTrimmedNfc(string input, string expected)
    {
        Assert.Equal(expected, SaveNames.Validate(input, "nome do mundo", ".zworld"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("CON")]
    [InlineData("mundo.")]
    [InlineData("Mundo​")]
    public void Invalid_ThrowsWithSubjectAndPrefix(string input)
    {
        var ex = Assert.Throws<DataException>(() => SaveNames.Validate(input, "nome do mundo", ".zworld"));

        Assert.StartsWith(".zworld:", ex.Message);
        Assert.Contains("nome do mundo", ex.Message);
    }

    [Fact]
    public void PlayerValidateName_StillUsesPlayerMessages()
    {
        var ex = Assert.Throws<DataException>(() => PlayerSave.ValidateName("CON"));

        Assert.StartsWith(".zplayer:", ex.Message);
        Assert.Contains("nome do personagem", ex.Message);
    }
}
