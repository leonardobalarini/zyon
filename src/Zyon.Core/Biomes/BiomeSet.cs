using Zyon.Core.Data;

namespace Zyon.Core.Biomes;

public enum TreeShape { Dead, Pine, Cactus }

public sealed record BiomeTree(TreeShape Shape, ushort Trunk, ushort Crown, int HeightMin, int HeightMax);

public sealed record BiomeBand(float WidthMin, float WidthMax, float GapMin, float GapMax);

/// <summary>Um bioma com ids do Registry. Index = posição no biomes.json.</summary>
public sealed record Biome(
    int Index, string Id, BiomeBand? Band,
    ushort SurfaceBlock, ushort ShallowBlock, ushort SurfaceWall, ushort ShallowWall,
    BiomeTree Tree, float TreeChance, int TreeSpacing,
    ushort Decoration, float DecorationChance, IReadOnlyList<ushort> Markers);

/// <summary>Biomas de data/biomes.json resolvidos no Registry.</summary>
public sealed class BiomeSet
{
    private BiomeSet(IReadOnlyList<Biome> all, Biome @default, BiomeConfig c)
    {
        All = all;
        Default = @default;
        Bands = all.Where(b => b.Band is not null).ToList();
        EdgeNoise = c.EdgeNoise;
        Taper = c.Taper;
        DetectWidth = c.DetectWidth;
        DetectHeight = c.DetectHeight;
        DetectThreshold = c.DetectThreshold;
    }

    public IReadOnlyList<Biome> All { get; }
    public Biome Default { get; }

    /// <summary>Os dois biomas com faixa, na ordem do arquivo.</summary>
    public IReadOnlyList<Biome> Bands { get; }

    public int EdgeNoise { get; }
    public int Taper { get; }
    public int DetectWidth { get; }
    public int DetectHeight { get; }
    public int DetectThreshold { get; }

    public Biome Get(string id) => All.FirstOrDefault(b => b.Id == id) ?? throw new KeyNotFoundException($"Bioma desconhecido: '{id}'");

    public static BiomeSet Resolve(BiomeConfig config, Registry registry)
    {
        string file = BiomeConfigLoader.FileName;
        var markerOwner = new Dictionary<ushort, string>();
        var all = new List<Biome>();
        for (int i = 0; i < config.Biomes.Count; i++)
        {
            var e = config.Biomes[i]!;
            string ctx = $"{file}: biomes[{i}] ('{e.Id}')";

            ushort Block(string name, string field) =>
                registry.TryGetBlockId(name, out var id) ? id : throw new DataException($"{ctx}: '{field}' referencia bloco inexistente '{name}'");
            ushort Wall(string name, string field) =>
                registry.TryGetWallId(name, out var id) ? id : throw new DataException($"{ctx}: '{field}' referencia parede inexistente '{name}'");
            ushort TreeBlock(string name, string field)
            {
                ushort id = Block(name, field);
                return registry.Block(id).Tree ? id : throw new DataException($"{ctx}: '{field}' precisa ser um bloco de árvore (\"tree\": true), '{name}' não é");
            }

            var t = e.Tree!;
            var tree = new BiomeTree(t.Shape switch { "dead" => TreeShape.Dead, "pine" => TreeShape.Pine, _ => TreeShape.Cactus },
                TreeBlock(t.Trunk, "tree.trunk"), TreeBlock(t.Crown, "tree.crown"), t.HeightMin, t.HeightMax);

            var markers = new List<ushort>();
            foreach (var name in e.Markers)
            {
                ushort id = Block(name!, "markers");
                if (markerOwner.TryGetValue(id, out var other))
                    throw new DataException($"{ctx}: marcador '{name}' já é do bioma '{other}' (cada bloco marca um bioma só)");
                markerOwner[id] = e.Id;
                markers.Add(id);
            }

            var band = e.Band is { } b ? new BiomeBand(b.WidthMin, b.WidthMax, b.GapMin, b.GapMax) : null;
            all.Add(new Biome(i, e.Id, band,
                Block(e.SurfaceBlock, "surfaceBlock"), Block(e.ShallowBlock, "shallowBlock"),
                Wall(e.SurfaceWall, "surfaceWall"), Wall(e.ShallowWall, "shallowWall"),
                tree, e.TreeChance, e.TreeSpacing, Block(e.Decoration, "decoration"), e.DecorationChance, markers));
        }
        return new BiomeSet(all, all.First(b => b.Id == config.Default), config);
    }
}
