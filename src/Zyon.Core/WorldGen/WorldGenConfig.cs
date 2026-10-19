namespace Zyon.Core.WorldGen;

/// <summary>Parâmetros da geração de mundo. Frações são relativas à altura do mundo; profundidades em tiles.</summary>
public sealed class WorldGenConfig
{
    public MaterialNames Materials { get; init; } = new();
    public float SurfaceMin { get; init; } = 0.25f;
    public float SurfaceMax { get; init; } = 0.35f;
    public float TerrainFrequency { get; init; } = 0.004f;
    public int TerrainOctaves { get; init; } = 4;
    public int DirtDepth { get; init; } = 12;
    public int DirtDepthVariation { get; init; } = 5;
    public float DeepLayerStart { get; init; } = 0.7f;
    public int DeepLayerVariation { get; init; } = 8;
    public float LayerNoiseFrequency { get; init; } = 0.03f;
    public float CaveFrequency { get; init; } = 0.025f;
    public float CaveThreshold { get; init; } = 0.6f;
    public int CaveMinDepth { get; init; } = 15;
    public int BottomSolidRows { get; init; } = 3;
    public float WormsPerThousandColumns { get; init; } = 4f;
    public int WormLengthMin { get; init; } = 80;
    public int WormLengthMax { get; init; } = 300;
    public float WormRadiusMin { get; init; } = 1.2f;
    public float WormRadiusMax { get; init; } = 2.5f;
    public List<OreConfig> Ores { get; init; } = [];
    public int SpawnSearchRadius { get; init; } = 300;
    public int SpawnFlatWidth { get; init; } = 4;
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
