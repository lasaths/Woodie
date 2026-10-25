namespace Woodie.Stock;

/// <summary>Root specimen identity (the seed) and, for natural stock, its resolved features.</summary>
public sealed class SourceSpecimen
{
    public SourceSpecimen(int seed, NaturalFeatures features = null) { Seed = seed; Features = features; }
    public int Seed { get; }
    public NaturalFeatures Features { get; }
}
