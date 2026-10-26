using System.Text.Json.Serialization;
using Zyon.Core.Biomes;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.WorldGen;

/// <summary>Parâmetros da geração de mundo. Frações são relativas à altura do mundo; profundidades em tiles.</summary>
public sealed class WorldGenConfig
{
    public MaterialNames Materials { get; init; } = new();
    public float SurfaceMin { get; init; } = 0.25f;
    public float SurfaceMax { get; init; } = 0.35f;
    public float TerrainFrequency { get; init; } = 0.0025f;
    public int TerrainOctaves { get; init; } = 5;
    public float HillinessFrequency { get; init; } = 0.0008f;
    public float HillinessMin { get; init; } = 0.1f;
    public int DirtDepth { get; init; } = 12;
    public int DirtDepthVariation { get; init; } = 5;
    public float DeepLayerStart { get; init; } = 0.7f;
    public int DeepLayerVariation { get; init; } = 8;
    public float LayerNoiseFrequency { get; init; } = 0.03f;
    public float CaveFrequency { get; init; } = 0.018f;
    public float CaveThreshold { get; init; } = 0.5f;
    public int CaveMinDepth { get; init; } = 15;
    public int BottomSolidRows { get; init; } = 3;
    public float WormsPerThousandColumns { get; init; } = 12f;
    public int WormLengthMin { get; init; } = 150;
    public int WormLengthMax { get; init; } = 500;
    public float WormRadiusMin { get; init; } = 1.5f;
    public float WormRadiusMax { get; init; } = 3.0f;
    public float ShallowWormFraction { get; init; } = 0.3f;
    public int ShallowWormDepth { get; init; } = 60;
    public List<OreConfig> Ores { get; init; } = [];
    /// <summary>Distância do pico ao centro do mundo; positivo = à direita (portão voltado para o centro, à esquerda).</summary>
    public int MountainOffset { get; init; } = 40;
    /// <summary>Colunas do pé ao pico do lado do portão (paredão).</summary>
    public int MountainSteepWidth { get; init; } = 30;
    /// <summary>Colunas do pico ao pé do lado suave.</summary>
    public int MountainGentleWidth { get; init; } = 80;
    public float MountainHeight { get; init; } = 45f;
    public float MountainNoiseFrequency { get; init; } = 0.05f;
    public float MountainNoiseAmplitude { get; init; } = 4f;
    /// <summary>Nome do molde em data/structures (sem extensão).</summary>
    public string BunkerStructure { get; init; } = "bunker";
    /// <summary>Linhas sólidas garantidas acima do teto do bunker.</summary>
    public int BunkerRoofRows { get; init; } = 3;

    /// <summary>Molde do bunker, carregado por <see cref="WorldGenConfigLoader.LoadFromDirectory"/> (null com <c>Load(json)</c>).</summary>
    [JsonIgnore]
    public StructureTemplate? Bunker { get; internal set; }

    /// <summary>Biomas (data/biomes.json), carregados por <see cref="WorldGenConfigLoader.LoadFromDirectory"/> (null com <c>Load(json)</c>).</summary>
    [JsonIgnore]
    public BiomeConfig? Biomes { get; internal set; }
}

/// <summary>Nomes (ids textuais do Registry) dos materiais usados pela geração.</summary>
public sealed class MaterialNames
{
    public string Dirt { get; init; } = "dirt";
    public string Stone { get; init; } = "stone";
    public string DeepStone { get; init; } = "deep_stone";
    public string DirtWall { get; init; } = "dirt_wall";
    public string StoneWall { get; init; } = "stone_wall";
}

/// <summary>Veios de um minério. MinDepth/MaxDepth são frações da altura do mundo.</summary>
public sealed class OreConfig
{
    public string Block { get; init; } = "";
    public float MinDepth { get; init; }
    public float MaxDepth { get; init; }
    public float VeinsPerMillionTiles { get; init; }
    public int VeinSizeMin { get; init; }
    public int VeinSizeMax { get; init; }
}
