using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Geometry;
using Woodie.Stock;

namespace Woodie.Grasshopper;

public sealed class CutComponent : GH_Component
{
    public CutComponent() : base("Timber Cut", "TimberCut",
        "Cut timber on a plane and cap the cut face. Prefer Stock Model. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("3df0b43b-bbb6-4850-a7e8-f06f8567269e");
    protected override Bitmap Icon => Icons.Cut;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Legacy closed solid. Prefer Stock Model.", GH_ParamAccess.item);
        p.AddPlaneParameter("Cutting Plane", "P", "Cutting plane. Keeps the negative side by default.", GH_ParamAccess.item);
        p.AddBooleanParameter("Keep Positive", "+", "True keeps the positive side of the plane", GH_ParamAccess.item, false);
        p.AddTextParameter("Geometry Units", "U", "Source units. Default is Document.", GH_ParamAccess.item);
        p[0].Optional = true;
        p[3].Optional = true;
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Optional Stock Model to cut", GH_ParamAccess.item);
        p[4].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Capped remaining timber", GH_ParamAccess.list);
        p.AddGeometryParameter("Stock Geometry", "SG", "Uncut original geometry", GH_ParamAccess.item);
        p.AddCurveParameter("Section Outlines", "C", "Intersection curves on the cutting plane", GH_ParamAccess.list);
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Cut Stock Model with the original material", GH_ParamAccess.list);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        StockGoo stockModel = null; bool hasModel = Params.Input.Count > 4 && da.GetData(4, ref stockModel) && stockModel?.Value != null;
        IGH_GeometricGoo goo = null; Plane plane = Plane.Unset; bool positive = false;
        bool hasLegacy = da.GetData(0, ref goo);
        if (!da.GetData(1, ref plane)) return;
        da.GetData(2, ref positive);
        string units = "Document"; da.GetData(3, ref units);
        try {
            if (hasModel && hasLegacy)
                throw new ArgumentException("Use either Stock Model or legacy Geometry, not both.");
            var documentUnits = RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Millimeters;
            double tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? .001;
            if (hasModel) {
                var model = stockModel.Value;
                // Stock models stay in document units; Geometry Units only scales the cutting plane origin.
                using var cut = TimberCut.Create(model.Geometry, GeometryUnits.ConvertPlane(plane, units, documentUnits), positive, tolerance);
                var models = new List<StockGoo>();
                foreach (var piece in cut.Geometry) {
                    using var next = model.WithGeometry(piece);
                    models.Add(new StockGoo(next.Duplicate()));
                }
                da.SetDataList(0, cut.Geometry.Select(g => g.Duplicate()));
                da.SetData(1, model.OriginalGeometry.Duplicate());
                da.SetDataList(2, cut.Outlines.Select(c => c.DuplicateCurve()));
                da.SetDataList(3, models);
                if (cut.Remark != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, cut.Remark);
                return;
            }
            if (!hasLegacy) throw new ArgumentException("Supply Geometry or a Stock Model.");
            var stock = goo?.ScriptVariable() as GeometryBase;
            if (stock == null) throw new ArgumentException("Supply closed Brep, solid Extrusion or closed Mesh geometry.");
            using var legacy = new GeometryUnits(stock, null, plane, units, documentUnits);
            using var result = TimberCut.Create(legacy.Geometry, legacy.BasePlane.Value, positive, tolerance);
            da.SetDataList(0, result.Geometry.Select(g => g.Duplicate())); da.SetData(1, legacy.Geometry.Duplicate()); da.SetDataList(2, result.Outlines.Select(c => c.DuplicateCurve()));
            if (result.Remark != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, result.Remark);
        } catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
