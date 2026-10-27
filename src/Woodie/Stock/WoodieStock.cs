using Rhino.Geometry;
using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>Current geometry that still references its original stock definition for sampling.</summary>
public sealed class WoodieStock : IDisposable
{
    public WoodieStock(GeometryBase geometry, StockDefinition definition, GeometryBase original = null,
        Plane? basePlane = null, Vector3d? fibre = null, Curve guide = null)
    {
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Stock geometry is null or invalid.");
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        try
        {
            Geometry = geometry.Duplicate();
            OriginalGeometry = (original ?? geometry).Duplicate();
            if (OriginalGeometry == null || !OriginalGeometry.IsValid) throw new ArgumentException("Original stock geometry is invalid.");
            BasePlane = basePlane;
            Fibre = fibre;
            Guide = guide?.DuplicateCurve();
        }
        catch { Dispose(); throw; }
    }

    public GeometryBase Geometry { get; private set; }
    public GeometryBase OriginalGeometry { get; private set; }
    public StockDefinition Definition { get; }
    public TimberSettings Settings => Definition.Settings;
    public Plane? BasePlane { get; }
    public Vector3d? Fibre { get; }
    public Curve Guide { get; private set; }

    public WoodieStock WithGeometry(GeometryBase geometry)
    {
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Geometry is null or invalid.");
        return new WoodieStock(geometry, Definition, OriginalGeometry, BasePlane, Fibre, Guide);
    }

    public WoodieStock Duplicate() => new(Geometry, Definition, OriginalGeometry, BasePlane, Fibre, Guide);

    public void Dispose()
    {
        Geometry?.Dispose(); OriginalGeometry?.Dispose(); Guide?.Dispose();
        Geometry = null; OriginalGeometry = null; Guide = null;
    }
}
