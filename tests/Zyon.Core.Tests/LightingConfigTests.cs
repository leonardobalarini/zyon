using Zyon.Core.Data;
using Zyon.Core.Lighting;

namespace Zyon.Core.Tests;

public class LightingConfigTests
{
    [Fact]
    public void RealFile_Loads()
    {
        var c = TestData.LightingConfig();

        Assert.Equal(0.06f, c.AirAttenuation);
        Assert.Equal(0.25f, c.SolidAttenuation);
        Assert.Equal(1440, c.DayLengthSeconds);
        Assert.Equal(18, c.RegionMargin);
    }

    [Theory]
    [InlineData("""{"airAttenuation": 0}""", "airAttenuation")]
    [InlineData("""{"solidAttenuation": 1.5}""", "solidAttenuation")]
    [InlineData("""{"dayLengthSeconds": 0}""", "dayLengthSeconds")]
    [InlineData("""{"startTimeOfDay": 1}""", "startTimeOfDay")]
    [InlineData("""{"nightSkyLevel": 2}""", "nightSkyLevel")]
    [InlineData("""{"twilightFraction": 0.5}""", "twilightFraction")]
    [InlineData("""{"playerHaloLevel": -1}""", "playerHaloLevel")]
    [InlineData("""{"regionMargin": -1}""", "regionMargin")]
    [InlineData("""{"airAtenuation": 0.1}""", "lighting.json")]
    [InlineData("""{""", "lighting.json")]
    public void Invalid_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => LightingConfigLoader.Load(json));

        Assert.Contains("lighting.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }
}
