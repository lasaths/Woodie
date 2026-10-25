using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>Original stock bounds, settings and construction. Sampling is stock → piece → source → anatomy.</summary>
public sealed class StockDefinition : IMaterialModel
{
    StockDefinition(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        IReadOnlyList<TimberLayer> layers, Construction construction)
    {
        Settings = settings;
        LengthMm = lengthMm;
        WidthMm = widthMm;
        DepthMm = depthMm;
        Layers = layers;
        Construction = construction;
    }

    public TimberSettings Settings { get; }
    public double LengthMm { get; }
    public double WidthMm { get; }
    public double DepthMm { get; }
    public IReadOnlyList<TimberLayer> Layers { get; }
    public Construction Construction { get; }

    public static StockDefinition FromTimber(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        double toleranceMm = .01, IReadOnlyList<TimberLayer> layers = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        if (!(lengthMm > 0) || !(widthMm > 0) || !(depthMm > 0) || !double.IsFinite(lengthMm + widthMm + depthMm))
            throw new ArgumentException("Stock dimensions must be finite and positive.");
        layers ??= TimberLayups.Resolve(settings, depthMm, widthMm, toleranceMm);
        var panel = new TimberPanel(settings.Product, lengthMm, widthMm, depthMm, settings.Seed,
            settings.EffectiveKnotDensity, settings.Palette(), settings.LamellaWidth, settings.VeneerThickness, layers);
        var specimen = new SourceSpecimen(settings.Seed);
        var piece = new MaterialPiece(specimen, panel, 0, 0, 0, lengthMm, widthMm, depthMm);
        return new StockDefinition(settings.Duplicate(), lengthMm, widthMm, depthMm, layers, new Construction(new[] { piece }));
    }

    public int Sample(double x, double y, double z) => Sample(x, y, z, 0, 0, 0, false);

    public int Sample(double x, double y, double z, double nx, double ny, double nz) => Sample(x, y, z, nx, ny, nz, true);

    int Sample(double x, double y, double z, double nx, double ny, double nz, bool oriented)
    {
        var piece = Construction.PieceAt(x, y, z);
        piece.ToSource(x, y, z, out var sx, out var sy, out var sz);
        if (!oriented) return piece.Model.Sample(sx, sy, sz);
        piece.ToSourceNormal(nx, ny, nz, out var snx, out var sny, out var snz);
        return piece.Model.Sample(sx, sy, sz, snx, sny, snz);
    }
}
