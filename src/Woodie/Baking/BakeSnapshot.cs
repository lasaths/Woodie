using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
namespace Woodie.Baking;

// Own the solved inputs, not volatile Grasshopper references or preview pages.
internal sealed class BakeSnapshot : IDisposable
{
    internal GeometryBase Geometry { get; private set; }
    internal GeometryBase Stock { get; private set; }
    internal Curve Guide { get; private set; }
    internal TimberSettings Settings { get; }
    internal Plane? Plane { get; private set; }
    internal Vector3d? Fibre { get; }
    internal UnitSystem Units { get; private set; }
    internal BakeSnapshot(GeometryBase geometry, TimberSettings settings, UnitSystem units, Plane? plane = null, Vector3d? fibre = null, Curve guide = null, GeometryBase stock = null)
    {
        Settings = settings.Duplicate(); Units = units; Plane = plane; Fibre = fibre;
        try { Geometry = geometry.Duplicate(); Guide = guide?.DuplicateCurve(); Stock = stock?.Duplicate(); }
        catch { Dispose(); throw; }
    }
    internal BakeSnapshot CopyFor(UnitSystem target)
    {
        if (target is UnitSystem.None or UnitSystem.Unset or UnitSystem.CustomUnits) throw new ArgumentException("Set physical Rhino document units before baking timber.");
        var copy = new BakeSnapshot(Geometry, Settings, Units, Plane, Fibre, Guide, Stock);
        try
        {
            double scale = RhinoMath.UnitScale(Units, target);
            if (!(scale > 0) || !double.IsFinite(scale)) throw new ArgumentException("Invalid bake document units.");
            if (Math.Abs(scale - 1) > 1e-12)
            {
                var transform = Transform.Scale(Point3d.Origin, scale);
                if (!copy.Geometry.Transform(transform) || (copy.Guide != null && !copy.Guide.Transform(transform)) || (copy.Stock != null && !copy.Stock.Transform(transform))) throw new ArgumentException("Could not convert bake geometry into target document units.");
                if (copy.Plane.HasValue) { var p = copy.Plane.Value; p.Transform(transform); copy.Plane = p; }
            }
            copy.Units = target; return copy;
        }
        catch { copy.Dispose(); throw; }
    }
    internal PreparedPart Prepare(double tolerance) => PreparedPart.Create(Geometry, Settings, RhinoMath.UnitScale(Units, UnitSystem.Millimeters), tolerance, Plane, Fibre, Guide, Stock);
    public void Dispose() { Geometry?.Dispose(); Guide?.Dispose(); Stock?.Dispose(); Geometry = null; Guide = null; Stock = null; }
}
