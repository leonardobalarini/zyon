using Zyon.Core.Data;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.Tests;

public class StructureLoaderTests
{
    // 6×6: casco de metal, interior com parede, portão (D) à esquerda, spawn (S) no meio, "keep" no canto.
    private const string Valid = """
    {
      "legend": {
        "#": { "block": "metal_plate", "wall": "metal_wall" },
        ".": { "wall": "metal_wall" },
        "S": { "wall": "metal_wall", "spawn": true },
        "D": { "wall": "metal_wall", "anchor": true },
        " ": "keep"
      },
      "rows": [
        "##### ",
        "#....#",
        ".....#",
        ".....#",
        "D.S..#",
        "######"
      ]
    }
    """;

    private static string With(string rowsJson) =>
        Valid[..Valid.IndexOf("\"rows\"", StringComparison.Ordinal)] + "\"rows\": " + rowsJson + "\n}";

    [Fact]
    public void Load_ReadsSizeMarkersAndCells()
    {
        var t = StructureLoader.Load("teste", Valid);

        Assert.Equal(6, t.Width);
        Assert.Equal(6, t.Height);
        Assert.Equal((0, 4), t.Anchor);
        Assert.Equal((2, 4), t.Spawn);
        Assert.True(t[5, 0].Keep);
        Assert.Equal("metal_plate", t[0, 0].Block);
        Assert.Null(t[1, 1].Block);
        Assert.Equal("metal_wall", t[1, 1].Wall);
        Assert.Equal(Valid, t.SourceText);
    }

    [Fact]
    public void Resolve_MapsNamesToIds_AndKeep()
    {
        var r = TestData.Registry();
        var s = StructureLoader.Load("teste", Valid).Resolve(r);

        Assert.Equal(r.BlockId("metal_plate"), s[0, 0].Block);
        Assert.Equal(r.WallId("metal_wall"), s[0, 0].Wall);
        Assert.Equal(0, s[1, 1].Block);
        Assert.True(s[5, 0].Keep);
        Assert.Equal(0, s.TopRow(0));
        Assert.Equal(1, s.TopRow(5));
    }

    [Fact]
    public void Mirrored_FlipsColumnsAndMarkers()
    {
        var s = StructureLoader.Load("teste", Valid).Resolve(TestData.Registry());
        var m = s.Mirrored();

        Assert.Equal((5, 4), m.Anchor);
        Assert.Equal((3, 4), m.Spawn);
        Assert.True(m[0, 0].Keep);
        Assert.Equal(s[0, 0], m[5, 0]);
    }

    [Theory]
    [InlineData("""["#####","####"]""", "linha 2: largura 4 diferente da primeira (5)")]
    [InlineData("""["##x##","D.S.#","#####"]""", "linha 1, coluna 3: caractere 'x' fora da legenda")]
    [InlineData("""["#####","D...#","#####"]""", "nenhum tile marcado com 'spawn'")]
    [InlineData("""["#####","DSS.#","#####"]""", "linha 2, coluna 3: segundo tile marcado com 'spawn'")]
    [InlineData("""["#####","..S.#","#####"]""", "nenhum tile marcado com 'anchor'")]
    [InlineData("""[]""", "'rows' ausente ou vazio")]
    public void Load_InvalidRows_ThrowsWithPosition(string rows, string expected)
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", With(rows)));

        Assert.Contains("structures/teste.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("""{"legend":{"ab":"keep"},"rows":["a"]}""", "a chave 'ab' deve ter um caractere")]
    [InlineData("""{"legend":{"#":"nada"},"rows":["#"]}""", "use \"keep\" ou um objeto")]
    [InlineData("""{"legend":{"#":{"blok":"x"}},"rows":["#"]}""", "campo 'blok' desconhecido")]
    [InlineData("""{"rows":["#"]}""", "campo 'legend' ausente")]
    [InlineData("""não é json""", "JSON inválido")]
    public void Load_InvalidLegend_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Resolve_UnknownBlock_ThrowsWithLegendChar()
    {
        var json = Valid.Replace("\"block\": \"metal_plate\"", "\"block\": \"granito\"");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("legenda '#': bloco inexistente 'granito'", ex.Message);
    }

    [Fact]
    public void Resolve_SpawnWithoutFloor_Throws()
    {
        var json = With("""["#####","#...#","#...#","D.S.#","##.##"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("linha 4, coluna 3: o spawn precisa de chão sólido logo abaixo", ex.Message);
    }

    [Fact]
    public void Resolve_SpawnWithoutHeadroom_Throws()
    {
        var json = With("""["#####","#####","D.S.#","#####"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("o spawn precisa de 3 colunas × 3 linhas livres", ex.Message);
    }

    [Fact]
    public void Resolve_AnchorWithoutFloor_Throws()
    {
        var json = With("""["#####","#...#","#...#","D.S.#",".####"]""");
        var ex = Assert.Throws<DataException>(() => StructureLoader.Load("teste", json).Resolve(TestData.Registry()));

        Assert.Contains("linha 4, coluna 1: a âncora precisa de chão sólido logo abaixo", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var ex = Assert.Throws<DataException>(() => StructureLoader.LoadFromDirectory(TestData.DataDir, "nao_existe"));

        Assert.Contains("structures/nao_existe.json", ex.Message);
    }
}
