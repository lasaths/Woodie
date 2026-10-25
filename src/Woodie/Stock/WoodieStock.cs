using Rhino.Geometry;
using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>Current geometry that still references its original stock definition for sampling.</summary>
public sealed class WoodieStock : IDisposable
{
    public WoodieStock(GeometryBase geometry, StockDefinition definition)
    {
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Stock geometry is null or invalid.");
        Geometry = geometry.Duplicate();
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public GeometryBase Geometry { get; private set; }
    public StockDefinition Definition { get; }
    public TimberSettings Settings => Definition.Settings;

    public WoodieStock WithGeometry(GeometryBase geometry)
    {
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Geometry is null or invalid.");
        return new WoodieStock(geometry, Definition);
    }

    public void Dispose() { Geometry?.Dispose(); Geometry = null; }
}
