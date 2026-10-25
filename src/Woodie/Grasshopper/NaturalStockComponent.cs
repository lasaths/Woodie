using System.Drawing;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Stock;

namespace Woodie.Grasshopper;

public sealed class NaturalStockComponent : GH_Component
{
    public NaturalStockComponent() : base("Natural Stock", "NaturalStock",
        "Generate a tapered natural Log or a hollow Moso bamboo culm along a Line (base to tip) with its stock model. Seed, colours and resolution come from Settings. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("5d7f3c1e-8a42-4b6f-9e0d-1c3a7b9f2e64");
    protected override Bitmap Icon => Icons.Tree;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddLineParameter("Line", "L", "Straight axis from the base to the tip, in Geometry Units", GH_ParamAccess.item);
        p.AddTextParameter("Kind", "K", "Log (default) or Bamboo", GH_ParamAccess.item, "Log");
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional Material Settings: seed (specimen identity), colours and resolution. Product and buildup are replaced.", GH_ParamAccess.item);
        p.AddNumberParameter("Diameter", "D", "Base diameter in mm including bark; default 200 (Log) or 80 (Bamboo)", GH_ParamAccess.item);
        p.AddNumberParameter("Wall", "W", "Bamboo culm wall thickness in mm; default 7", GH_ParamAccess.item);
        p.AddNumberParameter("Node Spacing", "N", "Nominal bamboo internode length in mm; default 270", GH_ParamAccess.item);
        p.AddNumberParameter("Bark", "B", "Log bark thickness in mm; default 5, 0 for debarked", GH_ParamAccess.item);
        p.AddNumberParameter("Taper", "T", "Diameter lost per metre of length in mm; default 10 (Log) or 5 (Bamboo)", GH_ParamAccess.item);
        p.AddNumberParameter("Variation", "V", "Natural Variation 0..1: bounded section waviness, ovality, node jitter, knots and tint; 0 is nominal only. Default 0.5", GH_ParamAccess.item);
        p.AddTextParameter("Geometry Units", "U", "Source units of the Line; Document by default. Dimensions are always millimetres.", GH_ParamAccess.item);
        for (int i = 1; i < p.ParamCount; i++) p[i].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Stock model for Timber Cut and Timber Preview", GH_ParamAccess.item);
        p.AddGeometryParameter("Geometry", "G", "Ordinary closed Brep in document units", GH_ParamAccess.item);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        Line line = Line.Unset;
        if (!da.GetData(0, ref line)) return;
        string kind = "Log"; da.GetData(1, ref kind);
        var product = (kind ?? "").Trim().ToLowerInvariant() switch { "" or "log" => TimberProduct.Log, "bamboo" or "moso" => TimberProduct.Bamboo, _ => (TimberProduct?)null } ;
        if (product == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Kind must be Log or Bamboo."); return; }
        bool log = product == TimberProduct.Log;
        SettingsGoo settingsGoo = null;
        var settings = da.GetData(2, ref settingsGoo) && settingsGoo?.Value != null ? settingsGoo.Value.Duplicate() : new TimberSettings();
        settings.Product = product.Value; settings.Layers = null; settings.FitToPart = false;
        double value = 0;
        settings.Diameter = da.GetData(3, ref value) ? value : double.IsNaN(settings.Diameter) ? (log ? 200 : 80) : settings.Diameter;
        settings.WallThickness = da.GetData(4, ref value) ? value : double.IsNaN(settings.WallThickness) ? 7 : settings.WallThickness;
        settings.NodeSpacing = da.GetData(5, ref value) ? value : double.IsNaN(settings.NodeSpacing) ? 270 : settings.NodeSpacing;
        settings.BarkThickness = da.GetData(6, ref value) ? value : double.IsNaN(settings.BarkThickness) ? 5 : settings.BarkThickness;
        settings.Taper = da.GetData(7, ref value) ? value : double.IsNaN(settings.Taper) ? (log ? 10 : 5) : settings.Taper;
        if (da.GetData(8, ref value)) settings.Variation = value;
        string units = "Document"; da.GetData(9, ref units);
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
        catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
