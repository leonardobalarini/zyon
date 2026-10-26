using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.Biomes;

public static class BiomeConfigLoader
{
    public const string FileName = "biomes.json";

    public static readonly string[] Shapes = ["dead", "pine", "cactus"];

    public static BiomeConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        return Load(File.ReadAllText(path));
    }

    public static BiomeConfig Load(string json)
    {
        BiomeConfig config;
        try
        {
            config = JsonSerializer.Deserialize<BiomeConfig>(json, RegistryLoader.Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        Validate(config);
        return config;
    }

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw Fail(message);
    }

    private static void Validate(BiomeConfig c)
    {
        Require(c.Biomes is not null, "campo 'biomes' não pode ser nulo");
        Require(c.EdgeNoise >= 0, "'edgeNoise' deve ser >= 0");
        Require(c.Taper >= 1, "'taper' deve ser >= 1");
        Require(c.DetectWidth >= 1 && c.DetectHeight >= 1, "'detectWidth' e 'detectHeight' devem ser >= 1");
        Require(c.DetectThreshold >= 1, "'detectThreshold' deve ser >= 1");

        var ids = new HashSet<string>();
        int bands = 0, defaults = 0;
        for (int i = 0; i < c.Biomes!.Count; i++)
        {
            var b = c.Biomes[i];
            Require(b is not null, $"biomes[{i}]: entrada nula");
            string ctx = $"biomes[{i}] ('{b!.Id}')";
            Require(!string.IsNullOrEmpty(b.Id) && b.Id.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'),
                $"biomes[{i}]: 'id' deve ter só letras minúsculas, dígitos e '_'");
            Require(ids.Add(b.Id), $"{ctx}: id repetido");
            if (b.Band is { } band)
            {
                bands++;
                Require(b.Id != c.Default, $"{ctx}: o bioma padrão não pode ter 'band'");
                Require(band.WidthMin > 0 && band.WidthMin <= band.WidthMax && band.WidthMax < 0.5f,
                    $"{ctx}: 'band.widthMin' e 'band.widthMax' devem satisfazer 0 < min <= max < 0,5");
                Require(band.GapMin >= 0 && band.GapMin <= band.GapMax && band.GapMax < 0.5f,
                    $"{ctx}: 'band.gapMin' e 'band.gapMax' devem satisfazer 0 <= min <= max < 0,5");
            }
            else
            {
                defaults++;
                Require(b.Id == c.Default, $"{ctx}: só o bioma padrão ('default') fica sem 'band'");
            }
            foreach (var (field, value) in new[] { ("surfaceBlock", b.SurfaceBlock), ("shallowBlock", b.ShallowBlock),
                         ("surfaceWall", b.SurfaceWall), ("shallowWall", b.ShallowWall), ("decoration", b.Decoration) })
                Require(!string.IsNullOrWhiteSpace(value), $"{ctx}: campo '{field}' ausente");
            Require(b.Tree is not null, $"{ctx}: campo 'tree' ausente");
            var t = b.Tree!;
            Require(Shapes.Contains(t.Shape), $"{ctx}: 'tree.shape' '{t.Shape}' desconhecida (use {string.Join(", ", Shapes)})");
            Require(!string.IsNullOrWhiteSpace(t.Trunk) && !string.IsNullOrWhiteSpace(t.Crown), $"{ctx}: 'tree.trunk' e 'tree.crown' são obrigatórios");
            Require(t.HeightMin >= 3 && t.HeightMin <= t.HeightMax && t.HeightMax <= 32,
                $"{ctx}: 'tree.heightMin' e 'tree.heightMax' devem satisfazer 3 <= min <= max <= 32");
            Require(b.TreeChance is >= 0 and <= 1, $"{ctx}: 'treeChance' deve estar entre 0 e 1");
            Require(b.DecorationChance is >= 0 and <= 1, $"{ctx}: 'decorationChance' deve estar entre 0 e 1");
            Require(b.TreeSpacing >= 1, $"{ctx}: 'treeSpacing' deve ser >= 1");
            Require(b.Markers is not null && b.Markers.All(m => !string.IsNullOrWhiteSpace(m)), $"{ctx}: 'markers' com entrada vazia");
        }
        Require(ids.Contains(c.Default), $"'default' referencia bioma inexistente '{c.Default}'");
        Require(defaults == 1 && bands == 2, $"precisa de exatamente um bioma sem 'band' (o padrão) e dois com 'band' (há {defaults} e {bands})");
    }
}
