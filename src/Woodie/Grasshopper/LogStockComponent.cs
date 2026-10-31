using System.Drawing;
using Grasshopper.Kernel;
using Woodie.Appearance;
using Woodie.Stock;

namespace Woodie.Grasshopper;

/// <summary>Natural softwood log from a base→tip Line. Formerly Natural Stock (same GUID).</summary>
public sealed class LogStockComponent : GH_Component
{
    public LogStockComponent() : base("Log Stock", "LogStock",
        "Build a tapered softwood log Stock Model on a Line. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    // Keep Natural Stock GUID so older definitions reopen as Log Stock.
    public override Guid ComponentGuid => new("5d7f3c1e-8a42-4b6f-9e0d-1c3a7b9f2e64");
    protected override Bitmap Icon => Icons.Tree;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddLineParameter("Line", "L", "Axis from base to tip, in Geometry Units", GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional seed, colours and resolution. Product is replaced with Log.", GH_ParamAccess.item);
        p.AddNumberParameter("Diameter", "D", "Base diameter in mm, including bark", GH_ParamAccess.item);
        p.AddNumberParameter("Bark", "B", "Bark thickness in mm. Use 0 for none.", GH_ParamAccess.item);
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
        var settings = NaturalStockSolve.BaseSettings(da, 1, TimberProduct.Log);
        settings.Species = default; // Log ignores species; clear any Bamboo value carried on wired Settings.
        double value = 0;
        settings.Diameter = da.GetData(2, ref value) ? value : double.IsNaN(settings.Diameter) ? 200 : settings.Diameter;
        settings.BarkThickness = da.GetData(3, ref value) ? value : double.IsNaN(settings.BarkThickness) ? 5 : settings.BarkThickness;
        settings.Taper = da.GetData(4, ref value) ? value : double.IsNaN(settings.Taper) ? 10 : settings.Taper;
        if (da.GetData(5, ref value)) settings.Variation = value;
        string units = "Document"; da.GetData(6, ref units);
        NaturalStockSolve.Run(this, da, settings, units);
    }
}
