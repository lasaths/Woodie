using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Geometry;

namespace Woodie.Grasshopper;

public sealed class CutComponent : GH_Component
{
    public CutComponent() : base("Timber Cut", "TimberCut", "Cut and cap solid timber geometry. Connect original Stock to Timber Preview's Stock Geometry to keep grain and knots fixed as the cut moves. " + ResearchCredit.Timber, "Display", "Woodie") { }
    public override Guid ComponentGuid => new("3df0b43b-bbb6-4850-a7e8-f06f8567269e");
    protected override Bitmap Icon => Icons.Cut;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddGeometryParameter("Stock", "S", "Original closed Brep, solid extrusion or closed mesh in Rhino document units", GH_ParamAccess.item);
        p.AddPlaneParameter("Cutting Plane", "P", "Plane in Rhino document units; negative side retained by default", GH_ParamAccess.item);
        p.AddBooleanParameter("Keep Positive", "+", "True retains the plane normal's positive side; false retains its negative side", GH_ParamAccess.item, false);
        p.AddTextParameter("Geometry Units", "U", "Source units of Stock and Cutting Plane origin; Document by default. Outputs are in Rhino document units.", GH_ParamAccess.item);
        p[3].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Capped remaining timber; connect to Timber Preview Geometry", GH_ParamAccess.list);
        p.AddGeometryParameter("Stock", "S", "Uncut stock in document units; source geometry is unchanged. Connect to Timber Preview Stock Geometry.", GH_ParamAccess.item);
        p.AddCurveParameter("Section Outlines", "C", "Intersection curves on the cutting plane", GH_ParamAccess.list);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        IGH_GeometricGoo goo = null; Plane plane = Plane.Unset; bool positive = false;
        if (!da.GetData(0, ref goo) || !da.GetData(1, ref plane)) return;
        da.GetData(2, ref positive);
        var stock = goo?.ScriptVariable() as GeometryBase;
        string units = "Document"; da.GetData(3, ref units);
        try {
            if (stock == null) throw new ArgumentException("Supply closed Brep, solid Extrusion or closed Mesh stock.");
            using var converted = new GeometryUnits(stock, null, plane, units, RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Millimeters);
            using var cut = TimberCut.Create(converted.Geometry, converted.BasePlane.Value, positive, RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? .001);
            da.SetDataList(0, cut.Geometry.Select(g => g.Duplicate())); da.SetData(1, converted.Geometry.Duplicate()); da.SetDataList(2, cut.Outlines.Select(c => c.DuplicateCurve()));
            if (cut.Remark != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, cut.Remark);
        } catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
