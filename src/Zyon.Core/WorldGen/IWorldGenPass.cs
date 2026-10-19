namespace Zyon.Core.WorldGen;

/// <summary>Uma etapa da geração. O nome semeia o Random e os ruídos do passe, então deve ser único e estável.</summary>
public interface IWorldGenPass
{
    string Name { get; }
    void Apply(WorldGenContext ctx);
}
