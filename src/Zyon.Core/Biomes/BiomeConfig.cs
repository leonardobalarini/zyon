namespace Zyon.Core.Biomes;

/// <summary>data/biomes.json como escrito (nomes do Registry). Validado por <see cref="BiomeConfigLoader"/> e resolvido por <see cref="BiomeSet.Resolve"/>.</summary>
public sealed class BiomeConfig
{
    /// <summary>Bioma de tudo fora das faixas (o único sem <c>band</c>).</summary>
    public string Default { get; init; } = "";
    /// <summary>Deslocamento máximo, em colunas, das divisas das faixas (ruído).</summary>
    public int EdgeNoise { get; init; } = 6;
    /// <summary>A d linhas abaixo da superfície a divisa anda d / taper colunas para dentro da faixa.</summary>
    public int Taper { get; init; } = 4;
    public int DetectWidth { get; init; } = 60;
    public int DetectHeight { get; init; } = 40;
    /// <summary>Marcadores necessários na área de detecção para o bioma valer.</summary>
    public int DetectThreshold { get; init; } = 200;
    public List<BiomeEntry?> Biomes { get; init; } = [];
}

public sealed class BiomeEntry
{
    public string Id { get; init; } = "";
    /// <summary>Faixa de largura e distância (frações da largura do mundo); ausente = bioma padrão.</summary>
    public BandConfig? Band { get; init; }
    public string SurfaceBlock { get; init; } = "";
    public string ShallowBlock { get; init; } = "";
    public string SurfaceWall { get; init; } = "";
    public string ShallowWall { get; init; } = "";
    public TreeConfig? Tree { get; init; }
    public float TreeChance { get; init; }
    public int TreeSpacing { get; init; } = 1;
    public string Decoration { get; init; } = "";
    public float DecorationChance { get; init; }
    public List<string?> Markers { get; init; } = [];
}

public sealed class BandConfig
{
    public float WidthMin { get; init; }
    public float WidthMax { get; init; }
    public float GapMin { get; init; }
    public float GapMax { get; init; }
}

public sealed class TreeConfig
{
    /// <summary>dead, pine ou cactus (formato desenhado pelo passe trees).</summary>
    public string Shape { get; init; } = "";
    public string Trunk { get; init; } = "";
    public string Crown { get; init; } = "";
    public int HeightMin { get; init; }
    public int HeightMax { get; init; }
}
