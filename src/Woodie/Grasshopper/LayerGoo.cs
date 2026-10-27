using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class LayerGoo : GH_Goo<LayerDefinition>
{
    public LayerGoo() { }
    public LayerGoo(LayerDefinition value) { Value = value; }
    public override bool IsValid { get { try { Value?.Validate(); return Value != null; } catch (ArgumentException) { return false; } } }
    public override string TypeName => "Woodie Timber Layer";
    public override string TypeDescription => "Physical timber layer.";
    public override IGH_Goo Duplicate() => new LayerGoo(Value == null ? null : Value with { });
    public override object ScriptVariable() => Value;
    public override string ToString() => Value == null ? "Null layer" : $"{Value.Thickness:0.###} mm · {(Value.AlongX ? "Longitudinal" : "Cross")} · {Value.Construction}";
    public override bool CastFrom(object source) { if (source is LayerDefinition layer) { Value = layer with { }; return true; } return false; }
    public override bool Write(GH_IWriter writer) { writer.SetBoolean("HasValue", Value != null); if (Value != null) LayerArchive.Write(writer, Value); return true; }
    public override bool Read(GH_IReader reader) { Value = null; if (!reader.ItemExists("HasValue") || !reader.GetBoolean("HasValue")) return true; try { Value = LayerArchive.Read(reader); return true; } catch (ArgumentException) { return false; } }
}

internal static class LayerArchive
{
    public static void Write(GH_IWriter writer, LayerDefinition layer)
    {
        writer.SetDouble("Thickness", layer.Thickness); writer.SetBoolean("AlongX", layer.AlongX);
        writer.SetInt32("Construction", (int)layer.Construction); writer.SetDouble("BoardWidth", layer.BoardWidth);
        writer.SetInt32("Joints", (int)layer.Joints); writer.SetDouble("SegmentMin", layer.SegmentMin);
        writer.SetDouble("SegmentMax", layer.SegmentMax); writer.SetBoolean("ContinuousSheet", layer.ContinuousSheet);
        writer.SetBoolean("NearPith", layer.NearPith);
    }
    public static LayerDefinition Read(GH_IReader reader)
    {
        var layer = new LayerDefinition(reader.GetDouble("Thickness"), reader.GetBoolean("AlongX"),
            (LayerConstruction)reader.GetInt32("Construction"), reader.GetDouble("BoardWidth"),
            (LayerJoints)reader.GetInt32("Joints"), reader.GetDouble("SegmentMin"), reader.GetDouble("SegmentMax"), reader.GetBoolean("ContinuousSheet"), reader.ItemExists("NearPith") && reader.GetBoolean("NearPith"));
        layer.Validate(); return layer;
    }
}

public sealed class LayerParam : GH_PersistentParam<LayerGoo>
{
    public LayerParam() : base("Timber Layer", "Layer", "Layer for Timber Buildup", "Display", "Woodie") { }
    public override Guid ComponentGuid => new("8f46bd7c-dd0e-4b29-9a07-86d21c6d2fb1");
    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.LayerParameter;
    protected override GH_GetterResult Prompt_Singular(ref LayerGoo value) => GH_GetterResult.cancel;
    protected override GH_GetterResult Prompt_Plural(ref List<LayerGoo> values) => GH_GetterResult.cancel;
}
