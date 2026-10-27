using System.Drawing;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class BuildupComponent : GH_Component
{
    public BuildupComponent() : base("Timber Buildup", "TimberBuildup",
        "Set physical layers on Timber Material settings. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("bd7ed564-700b-499c-a377-1515f17ee464");
    protected override Bitmap Icon => Icons.Buildup;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Settings from Timber Material. Default is CLT.", GH_ParamAccess.item); p[0].Optional = true;
        p.AddParameter(new LayerParam(), "Layers", "L", "Layers from bottom to top. Empty uses the product preset.", GH_ParamAccess.list); p[1].Optional = true;
        p.AddNumberParameter("Reference Thickness", "T", "Schedule depth in mm", GH_ParamAccess.item); p[2].Optional = true;
        p.AddBooleanParameter("Fit to part", "Fit", "Scale layers to the measured stock thickness", GH_ParamAccess.item, false);
        p.AddPlaneParameter("Diagram Plane", "P", "Section diagram plane. Default is World XY.", GH_ParamAccess.item); p[4].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Settings with custom buildup", GH_ParamAccess.item);
        p.AddParameter(new LayerParam(), "Layers", "L", "Resolved layers", GH_ParamAccess.list);
        p.AddNumberParameter("Thickness", "T", "Total reference thickness in mm", GH_ParamAccess.item);
        p.AddTextParameter("Schedule", "S", "Layer schedule from bottom to top", GH_ParamAccess.list);
        p.AddCurveParameter("Section Diagram", "D", "Schematic layer rectangles, 200 mm wide", GH_ParamAccess.list);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        SettingsGoo input = null; da.GetData(0, ref input);
        var settings = input?.Value?.Duplicate() ?? new TimberSettings();
        var supplied = new List<LayerGoo>(); da.GetDataList(1, supplied);
        bool fit = false; da.GetData(3, ref fit); Plane plane = Plane.WorldXY; da.GetData(4, ref plane);
        try {
            if (!plane.IsValid) throw new ArgumentException("Diagram Plane is invalid.");
            if (supplied.Count > 0) {
                if (supplied.Any(g => g?.Value == null)) throw new ArgumentException("Layers contains an empty layer.");
                settings.Layers = supplied.Select(g => g.Value).ToArray();
            }
            settings.FitToPart = fit; settings.Validate();
            double height = settings.VirtualDepthMm; da.GetData(2, ref height);
            if (!(height > 0) || !double.IsFinite(height)) throw new ArgumentException("Reference Thickness must be finite and positive (mm).");
            if (settings.Layers == null) settings.Layers = TimberLayups.Recipes(settings, height);
            var layers = TimberLayups.Resolve(settings, height, 200);
            var units = RhinoDoc.ActiveDoc?.ModelUnitSystem ?? UnitSystem.Millimeters;
            if (units is UnitSystem.None or UnitSystem.Unset or UnitSystem.CustomUnits) throw new ArgumentException("Set physical Rhino document units for the section diagram.");
            double scale = RhinoMath.UnitScale(UnitSystem.Millimeters, units);
            if (!(scale > 0) || !double.IsFinite(scale)) throw new ArgumentException("Set physical Rhino document units for the section diagram.");
            var diagrams = layers.Select(l => new Rectangle3d(plane, new Interval(0, 200 * scale), new Interval(l.z0 * scale, l.z1 * scale)).ToNurbsCurve()).ToList();
            var schedule = layers.Select((l, i) => FormattableString.Invariant($"{i + 1}: {l.z1 - l.z0:0.###} mm | {(l.alongX ? "Longitudinal 0°" : "Cross 90°")} | {settings.Layers[i].Construction} | z {l.z0:0.###}–{l.z1:0.###} mm | {settings.Layers[i].Joints} end joints")).ToArray();
            da.SetData(0, new SettingsGoo(settings)); da.SetDataList(1, TimberLayups.Recipes(settings, height).Select(l => new LayerGoo(l)));
            da.SetData(2, height); da.SetDataList(3, schedule); da.SetDataList(4, diagrams);
            Message = "Custom buildup" + (fit ? " · fit" : " · exact");
        } catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
