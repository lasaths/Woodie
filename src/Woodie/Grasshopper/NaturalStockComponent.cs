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
        "Build a Log or bamboo culm Stock Model on a Line. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("5d7f3c1e-8a42-4b6f-9e0d-1c3a7b9f2e64");
    protected override Bitmap Icon => Icons.Tree;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddLineParameter("Line", "L", "Axis from base to tip, in Geometry Units", GH_ParamAccess.item);
        p.AddTextParameter("Kind", "K", "Log (default) or Bamboo", GH_ParamAccess.item, "Log");
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional seed, colours and resolution. Product is replaced.", GH_ParamAccess.item);
        p.AddNumberParameter("Diameter", "D", "Base diameter in mm, including bark", GH_ParamAccess.item);
        p.AddNumberParameter("Wall", "W", "Bamboo wall thickness in mm", GH_ParamAccess.item);
        p.AddNumberParameter("Node Spacing", "N", "Bamboo node spacing in mm", GH_ParamAccess.item);
        p.AddNumberParameter("Bark", "B", "Log bark thickness in mm. Use 0 for none.", GH_ParamAccess.item);
        p.AddNumberParameter("Taper", "T", "Diameter loss per metre, in mm", GH_ParamAccess.item);
        p.AddNumberParameter("Variation", "V", "Natural variation from 0 to 1", GH_ParamAccess.item);
        p.AddTextParameter("Geometry Units", "U", "Source units of the Line. Default is Document.", GH_ParamAccess.item);
        for (int i = 1; i < p.ParamCount; i++) p[i].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Stock Model for Cut and Preview", GH_ParamAccess.item);
        p.AddGeometryParameter("Geometry", "G", "Closed Brep in document units", GH_ParamAccess.item);
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
