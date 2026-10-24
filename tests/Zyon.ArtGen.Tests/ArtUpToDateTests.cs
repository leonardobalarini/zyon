namespace Zyon.ArtGen.Tests;

/// <summary>Os arquivos em data/client/art/ precisam ser exatamente a saída atual do gerador.</summary>
public class ArtUpToDateTests
{
    [Fact]
    public void CommittedArt_MatchesTheGenerator()
    {
        var generated = ArtBuilder.Build(RepoPaths.Root);
        string dir = Path.Combine(RepoPaths.Root, ArtBuilder.OutputDir);

        foreach (var (file, canvas) in generated.Images)
        {
            string path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), $"{file} não existe — rode tools/gen-art.sh");
            var committed = Png.Decode(File.ReadAllBytes(path));
            Assert.True(committed.Width == canvas.Width && committed.Height == canvas.Height && committed.Pixels.AsSpan().SequenceEqual(canvas.Pixels),
                $"{file} está desatualizado — rode tools/gen-art.sh e commite o resultado");
        }
        string manifest = File.ReadAllText(Path.Combine(dir, Zyon.Client.ArtManifest.FileName)).ReplaceLineEndings("\n");
        Assert.True(manifest == generated.Manifest.ToJson(), "art.json desatualizado — rode tools/gen-art.sh");
    }
}
