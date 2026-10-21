using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class PresetsComponent : GH_Component
{
    public PresetsComponent() : base("Timber Presets", "TimberPresets",
        "Choose a timber product preset. Right-click the Preset input to select a named product. " + ResearchCredit.Timber,
        "Display", "Woodie") { }

    public override Guid ComponentGuid => new("f17c33b1-9302-435d-a0e8-782a36f0c5e7");
    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.Presets;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddIntegerParameter("Preset", "P", "Right-click this input to choose CLT, ThreeS, GL, KertoQ or KVH", GH_ParamAccess.item, 0);
        var parameter = (Param_Integer)p[0];
        foreach (TimberProduct product in Enum.GetValues<TimberProduct>())
            parameter.AddNamedValue(product.ToString(), (int)product);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Product-default appearance settings; connect to Timber Preview", GH_ParamAccess.item);
        p.AddTextParameter("Product", "P", "Product name; connect to Timber Display Settings for custom appearance controls", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        int selected = 0;
        if (!da.GetData(0, ref selected)) return;
        if (!Enum.IsDefined(typeof(TimberProduct), selected))
        {
            Message = null;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Choose a named timber preset: CLT (0), ThreeS (1), GL (2), KertoQ (3), or KVH (4).");
            return;
        }
        var product = (TimberProduct)selected;
        var settings = new TimberSettings { Product = product };
        da.SetData(0, new SettingsGoo(settings));
        da.SetData(1, product.ToString());
        Message = product.ToString();
    }
}
