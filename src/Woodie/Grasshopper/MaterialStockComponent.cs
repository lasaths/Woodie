using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Stock;

namespace Woodie.Grasshopper;

public sealed class MaterialStockComponent : GH_Component
{
    public MaterialStockComponent() : base("Material Stock", "MaterialStock",
        "Build a Stock Model from geometry and settings. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("c4e8a912-6f3b-4a1d-9c57-8e2d0b4f1a63");
    protected override Bitmap Icon => Icons.Package;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Mesh, Brep, Surface or Extrusion", GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Material or Buildup settings. Default is CLT.", GH_ParamAccess.item);
        p.AddVectorParameter("Fibre", "F", "Fibre direction in the base plane", GH_ParamAccess.item);
        p.AddPlaneParameter("Base Plane", "P", "Optional base plane. X is fibre. Z is thickness.", GH_ParamAccess.item);
        p.AddCurveParameter("Guide Curve", "C", "Optional open centreline for bent timber", GH_ParamAccess.item);
        p.AddTextParameter("Geometry Units", "U", "Source units. Default is Document.", GH_ParamAccess.item);
        for (int i = 1; i <= 5; i++) p[i].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Stock Model for Cut and Preview", GH_ParamAccess.item);
        p.AddGeometryParameter("Geometry", "G", "Stock geometry in document units", GH_ParamAccess.item);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        IGH_GeometricGoo goo = null;
        if (!da.GetData(0, ref goo)) return;
        var geometry = goo?.ScriptVariable() as GeometryBase;
        if (geometry == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Supply a Mesh, Brep, Surface or Extrusion."); return; }
        SettingsGoo settingsGoo = null;
        var settings = da.GetData(1, ref settingsGoo) && settingsGoo?.Value != null ? settingsGoo.Value.Duplicate() : new TimberSettings();
        Vector3d fibre = Vector3d.Unset; Plane plane = Plane.Unset; Curve guide = null;
        bool hasFibre = da.GetData(2, ref fibre), hasPlane = da.GetData(3, ref plane);
        da.GetData(4, ref guide);
        string units = "Document"; da.GetData(5, ref units);
        var doc = RhinoDoc.ActiveDoc;
        try
        {
            using var input = new GeometryUnits(geometry, guide, hasPlane ? plane : null, units, doc?.ModelUnitSystem ?? UnitSystem.Millimeters);
            using var part = PreparedPart.Create(input.Geometry, settings, input.ToMillimetres, doc?.ModelAbsoluteTolerance ?? .001,
                input.BasePlane, hasFibre ? fibre : null, input.Guide);
            var definition = StockDefinition.FromTimber(settings, part.LengthMm, part.WidthMm, part.DepthMm, part.ToleranceMm);
            using var stock = new WoodieStock(input.Geometry, definition, input.Geometry, input.BasePlane, hasFibre ? fibre : null, input.Guide);
            da.SetData(0, new StockGoo(stock.Duplicate()));
            da.SetData(1, input.Geometry.Duplicate());
            foreach (var remark in part.Remarks) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, remark);
        }
        catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
