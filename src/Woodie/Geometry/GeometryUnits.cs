using Rhino;
using Rhino.Geometry;

namespace Woodie.Geometry;

// Own only conversion copies. Rhino input geometry and guides are never transformed in place.
internal sealed class GeometryUnits : IDisposable
{
    public GeometryBase Geometry { get; }
    public Curve Guide { get; }
    public Plane? BasePlane { get; }
    public double ToMillimetres { get; }
    public double Conversion { get; }
    public UnitSystem Source { get; }
    public UnitSystem Document { get; }
    readonly bool owns;

    public GeometryUnits(GeometryBase geometry, Curve guide, Plane? plane, string source, UnitSystem document)
    {
        if (document is UnitSystem.None or UnitSystem.Unset or UnitSystem.CustomUnits)
            throw new ArgumentException("Set physical Rhino document units before previewing timber (for example Millimetres or Metres).");
        Document = document;
        Source = Parse(source) ?? document;
        ToMillimetres = RhinoMath.UnitScale(document, UnitSystem.Millimeters);
        Conversion = RhinoMath.UnitScale(Source, document);
        if (!(ToMillimetres > 0) || !(Conversion > 0) || !double.IsFinite(ToMillimetres + Conversion))
            throw new ArgumentException("Geometry Units must have a valid physical scale.");
        owns = Math.Abs(Conversion - 1) > 1e-12;
        Geometry = owns ? geometry.Duplicate() : geometry;
        Guide = owns ? guide?.DuplicateCurve() : guide;
        BasePlane = plane;
        if (!owns) return;
        try {
            var transform = Transform.Scale(Point3d.Origin, Conversion);
            if (!Geometry.Transform(transform) || (Guide != null && !Guide.Transform(transform)))
                throw new ArgumentException("Could not convert input geometry to the Rhino document units.");
            if (plane.HasValue) { var converted = plane.Value; converted.Transform(transform); BasePlane = converted; }
        } catch { Dispose(); throw; }
    }

    static UnitSystem? Parse(string text)
    {
        switch ((text ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "")) {
            case "": case "document": case "rhino": return null;
            case "mm": case "millimeter": case "millimeters": case "millimetre": case "millimetres": return UnitSystem.Millimeters;
            case "cm": case "centimeter": case "centimeters": case "centimetre": case "centimetres": return UnitSystem.Centimeters;
            case "m": case "meter": case "meters": case "metre": case "metres": return UnitSystem.Meters;
            case "in": case "inch": case "inches": return UnitSystem.Inches;
            case "ft": case "foot": case "feet": return UnitSystem.Feet;
            default: throw new ArgumentException("Unknown Geometry Units. Use Document, Millimetres (mm), Centimetres (cm), Metres (m), Inches or Feet.");
        }
    }

    public static string BudgetMessage(Exception error, string source, UnitSystem document) =>
        error.Message + $" Input units: {(string.IsNullOrWhiteSpace(source) ? "Document" : source)}; Rhino document units: {document}. " +
        "Check Geometry Units before changing appearance settings. If these coordinates were modelled in millimetres, set Geometry Units to Millimetres; otherwise use the correct source units. " +
        "For a correctly sized part, increase Lamella Width, reduce Knot Density, or split the part.";

    public void Dispose() { if (owns) { Geometry?.Dispose(); Guide?.Dispose(); } }
}
