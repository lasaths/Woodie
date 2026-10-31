using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>Original stock bounds, settings and construction. Sampling is stock → piece → source → anatomy.</summary>
public sealed class StockDefinition : IMaterialModel
{
    StockDefinition(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        IReadOnlyList<TimberLayer> layers, Construction construction, double toleranceMm)
    {
        Settings = settings;
        LengthMm = lengthMm;
        WidthMm = widthMm;
        DepthMm = depthMm;
        Layers = layers;
        Construction = construction;
        ToleranceMm = toleranceMm;
    }

    public TimberSettings Settings { get; }
    public double LengthMm { get; }
    public double WidthMm { get; }
    public double DepthMm { get; }
    public double ToleranceMm { get; }
    public IReadOnlyList<TimberLayer> Layers { get; }
    public Construction Construction { get; }

    public static StockDefinition FromTimber(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        double toleranceMm = .01, IReadOnlyList<TimberLayer> layers = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        if (!(lengthMm > 0) || !(widthMm > 0) || !(depthMm > 0) || !double.IsFinite(lengthMm + widthMm + depthMm))
            throw new ArgumentException("Stock dimensions must be finite and positive.");
        if (!(toleranceMm > 0) || !double.IsFinite(toleranceMm)) throw new ArgumentException("Stock tolerance must be positive.");
        if (settings.IsNatural) return FromNatural(settings, lengthMm, widthMm, depthMm, toleranceMm, layers);
        if (settings.IsEngineeredBamboo) return FromBamboo(settings, lengthMm, widthMm, depthMm, toleranceMm, layers);
        layers ??= TimberLayups.Resolve(settings, depthMm, widthMm, toleranceMm);
        var panel = new TimberPanel(settings.Product, lengthMm, widthMm, depthMm, settings.Seed,
            settings.EffectiveKnotDensity, settings.Palette(), settings.LamellaWidth, settings.VeneerThickness, layers);
        var specimen = new SourceSpecimen(settings.Seed);
        var piece = new MaterialPiece(specimen, panel, 0, 0, 0, lengthMm, widthMm, depthMm);
        return new StockDefinition(settings.Duplicate(), lengthMm, widthMm, depthMm, layers, new Construction(new[] { piece }), toleranceMm);
    }

    /// <summary>One natural Log or Bamboo piece; features resolve once from the settings' seed. Dimensions are the stock's mapped bounds in mm.</summary>
    public static StockDefinition FromNatural(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        double toleranceMm = .01, IReadOnlyList<TimberLayer> layers = null)
    {
        var features = NaturalFeatures.Resolve(settings, lengthMm, widthMm, depthMm);
        var palette = settings.Palette();
        IMaterialModel model = features.Bamboo ? new BambooMaterialModel(features, palette, widthMm, depthMm) : new LogMaterialModel(features, palette, widthMm, depthMm);
        var piece = new MaterialPiece(new SourceSpecimen(settings.Seed, features), model, 0, 0, 0, lengthMm, widthMm, depthMm);
        layers ??= TimberLayups.Make(settings.Product, depthMm, widthMm);
        return new StockDefinition(settings.Duplicate(), lengthMm, widthMm, depthMm, layers, new Construction(new[] { piece }), toleranceMm);
    }

    /// <summary>Laminated bamboo lumber or layered panel: many machined strips, each mapped into a source culm. Dimensions are stock mm.</summary>
    public static StockDefinition FromBamboo(TimberSettings settings, double lengthMm, double widthMm, double depthMm,
        double toleranceMm = .01, IReadOnlyList<TimberLayer> layers = null)
    {
        layers ??= TimberLayups.Resolve(settings, depthMm, widthMm, toleranceMm);
        return new StockDefinition(settings.Duplicate(), lengthMm, widthMm, depthMm, layers,
            BambooConstruction.Build(settings, lengthMm, widthMm, depthMm, layers), toleranceMm);
    }

    public int Sample(double x, double y, double z) => Sample(x, y, z, 0, 0, 0, false);

    public int Sample(double x, double y, double z, double nx, double ny, double nz) => Sample(x, y, z, nx, ny, nz, true);

    public double KnotInfluence(double x, double y, double z)
    {
        var piece = Construction.PieceAt(x, y, z);
        piece.ToSource(x, y, z, out var sx, out var sy, out var sz);
        return piece.Model.KnotInfluence(sx, sy, sz);
    }

    public bool TryFibreDirection(double x, double y, double z, out double dx, out double dy, out double dz)
    {
        var piece = Construction.PieceAt(x, y, z);
        piece.ToSource(x, y, z, out var sx, out var sy, out var sz);
        if (!piece.Model.TryFibreDirection(sx, sy, sz, out dx, out dy, out dz)) return false;
        // Direction is a free vector in source millimetres; piece frames match stock axes for solid timber.
        return true;
    }

    /// <summary>Stem/knot skeleton and force-flow curves when the stock is a single TimberPanel piece.</summary>
    public (List<List<(double X, double Y, double Z)>> Skeleton, List<List<(double X, double Y, double Z)>> Flow) TraceFibreStrands(int across = 7, int through = 5, double stepMm = 12)
    {
        if (Construction.Pieces.Count == 1 && Construction.Pieces[0].Model is TimberPanel panel)
            return panel.TraceFibreStrands(across, through, stepMm);
        return (new List<List<(double X, double Y, double Z)>>(), new List<List<(double X, double Y, double Z)>>());
    }

    /// <summary>Fibre streamlines clipped to a material box when the stock is a single TimberPanel piece.</summary>
    public List<List<(double X, double Y, double Z)>> TraceFibreFlow(double x0, double x1, double y0, double y1, double z0, double z1, int across = 10, int through = 8, double stepMm = 8)
    {
        if (Construction.Pieces.Count == 1 && Construction.Pieces[0].Model is TimberPanel panel)
            return panel.TraceFibreFlow(x0, x1, y0, y1, z0, z1, across, through, stepMm);
        return new List<List<(double X, double Y, double Z)>>();
    }

    int Sample(double x, double y, double z, double nx, double ny, double nz, bool oriented)
    {
        var piece = Construction.PieceAt(x, y, z);
        piece.ToSource(x, y, z, out var sx, out var sy, out var sz);
        if (!oriented) return piece.Model.Sample(sx, sy, sz);
        piece.ToSourceNormal(nx, ny, nz, out var snx, out var sny, out var snz);
        return piece.Model.Sample(sx, sy, sz, snx, sny, snz);
    }
}
