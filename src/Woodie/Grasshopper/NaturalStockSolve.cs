using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Stock;

namespace Woodie.Grasshopper;

/// <summary>Shared Line → natural Brep + Stock Model path for Log Stock and Bamboo Stock.</summary>
static class NaturalStockSolve
{
    public static void Run(GH_Component host, IGH_DataAccess da, TimberSettings settings, string units)
    {
        Line line = Line.Unset;
        if (!da.GetData(0, ref line)) return;
        var doc = RhinoDoc.ActiveDoc;
        try
        {
            using var input = new GeometryUnits(new LineCurve(line), null, null, units, doc?.ModelUnitSystem ?? UnitSystem.Millimeters);
            var axis = ((LineCurve)input.Geometry).Line;
            double toMm = input.ToMillimetres, lengthMm = axis.Length * toMm;
            var features = NaturalFeatures.Resolve(settings, lengthMm, settings.Diameter, settings.Diameter);
            using var brep = NaturalGeometry.Create(features, axis, 1 / toMm, doc?.ModelAbsoluteTolerance ?? .001);
            var frame = NaturalGeometry.Frame(axis);
            var bounds = brep.GetBoundingBox(frame);
            var definition = StockDefinition.FromNatural(settings, lengthMm, (bounds.Max.Y - bounds.Min.Y) * toMm, (bounds.Max.Z - bounds.Min.Z) * toMm);
            using var stock = new WoodieStock(brep, definition, brep, frame, frame.XAxis);
            da.SetData(0, new StockGoo(stock.Duplicate()));
            da.SetData(1, brep.DuplicateBrep());
        }
        catch (ArgumentException ex) { host.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }

    public static TimberSettings BaseSettings(IGH_DataAccess da, int settingsIndex, TimberProduct product)
    {
        SettingsGoo goo = null;
        var settings = da.GetData(settingsIndex, ref goo) && goo?.Value != null ? goo.Value.Duplicate() : new TimberSettings();
        settings.Product = product; settings.Layers = null; settings.FitToPart = false;
        return settings;
    }
}
