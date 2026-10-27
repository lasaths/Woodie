using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class LayerComponent : GH_Component
{
    public LayerComponent() : base("Timber Layer", "TimberLayer",
        "Define one layer for Timber Buildup. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("64bfcb66-86a8-40cf-81b2-6495c79b6b7a");
    protected override Bitmap Icon => Icons.Layer;
    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddNumberParameter("Thickness", "T", "Layer thickness in mm", GH_ParamAccess.item, 30);
        p.AddIntegerParameter("Direction", "D", "Longitudinal (0°) or cross (90°) to stock fibre", GH_ParamAccess.item, 0);
        ((Param_Integer)p[1]).AddNamedValue("Longitudinal", 0); ((Param_Integer)p[1]).AddNamedValue("Cross", 1);
        p.AddIntegerParameter("Construction", "C", "Solid, boards or continuous veneer", GH_ParamAccess.item, 1);
        foreach (var type in Enum.GetValues<LayerConstruction>()) ((Param_Integer)p[2]).AddNamedValue(type.ToString(), (int)type);
        p.AddNumberParameter("Board Width", "W", "Board width in mm", GH_ParamAccess.item, 150);
        p.AddIntegerParameter("End Joints", "J", "Board end joints. Solid and veneer need None.", GH_ParamAccess.item, 0);
        foreach (var type in Enum.GetValues<LayerJoints>()) ((Param_Integer)p[4]).AddNamedValue(type.ToString(), (int)type);
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new LayerParam(), "Layer", "L", "Layer for Timber Buildup", GH_ParamAccess.item);
        p.AddTextParameter("Description", "D", "Layer description", GH_ParamAccess.item);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        double thickness = 30, width = 150; int direction = 0, construction = 1, joints = 0;
        if (!da.GetData(0, ref thickness) || !da.GetData(1, ref direction) || !da.GetData(2, ref construction) || !da.GetData(3, ref width) || !da.GetData(4, ref joints)) return;
        try {
            if (direction != 0 && direction != 1) throw new ArgumentException("Direction must be Longitudinal (0) or Cross (1).");
            var layer = new LayerDefinition(thickness, direction == 0, (LayerConstruction)construction, width, (LayerJoints)joints);
            layer.Validate(); var goo = new LayerGoo(layer); da.SetData(0, goo); da.SetData(1, goo.ToString());
        } catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
}
