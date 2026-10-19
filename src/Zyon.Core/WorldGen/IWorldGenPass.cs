namespace Zyon.Core.WorldGen;

/// <summary>
/// Uma etapa da geração. O nome semeia o Random do passe, então deve ser único e estável.
/// Os ruídos são semeados pelo nome dado a ctx.CreateNoise: use nomes prefixados pelo do passe (ex.: "caves.cheese").
/// </summary>
public interface IWorldGenPass
{
    string Name { get; }
    void Apply(WorldGenContext ctx);
}
