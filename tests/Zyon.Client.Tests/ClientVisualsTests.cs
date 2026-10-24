using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ClientVisualsTests
{
    [Fact]
    public void RealFile_LoadsSky()
    {
        var v = ClientVisuals.LoadFromDirectory(TestData.DataDir);

        Assert.Equal(new Rgb(0x7f, 0x9f, 0xbf), v.Sky);
    }

    [Theory]
    [InlineData("{ not json", "JSON")]
    [InlineData("""{"sky": "#zz0000"}""", "#zz0000")]
    [InlineData("""{"sky": "# 12345"}""", "sky")]
    [InlineData("""{"sky": "#000000", "blocks": {}}""", "JSON")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json));

        Assert.Contains("visuals.json", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void MissingSky_SaysFieldIsAbsent()
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load("{}"));

        Assert.Contains("sky", ex.Message);
        Assert.Contains("ausente", ex.Message);
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreAccepted()
    {
        var v = ClientVisuals.Load("{\n// comentário\n\"sky\": \"#010203\",\n}");

        Assert.Equal(new Rgb(1, 2, 3), v.Sky);
    }
}
