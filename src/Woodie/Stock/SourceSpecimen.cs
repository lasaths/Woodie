namespace Woodie.Stock;

/// <summary>Root specimen identity. Natural feature payloads are added when generators land.</summary>
public sealed class SourceSpecimen
{
    public SourceSpecimen(int seed) => Seed = seed;
    public int Seed { get; }
}
