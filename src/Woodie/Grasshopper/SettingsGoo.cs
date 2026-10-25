using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class SettingsGoo : GH_Goo<TimberSettings>
{
    public SettingsGoo() { }
    public SettingsGoo(TimberSettings value) { Value = value; }
    public override bool IsValid => Value != null;
    public override string TypeName => "Woodie Timber Settings";
    public override string TypeDescription => "Timber appearance settings for Woodie";
    public override IGH_Goo Duplicate() => new SettingsGoo(Value?.Duplicate());
    public override string ToString() => Value?.ToString() ?? "Null Woodie settings";
    public override object ScriptVariable() => Value;
    public override bool CastFrom(object source)
    {
        if (source is TimberSettings settings) { Value = settings.Duplicate(); return true; }
        return false;
    }
    public override bool Write(GH_IWriter writer)
    {
        writer.SetBoolean("HasValue", Value != null);
        if (Value == null) return true;
        writer.SetInt32("Version", 3); writer.SetInt32("Look", (int)Value.Look); writer.SetInt32("Product", (int)Value.Product); writer.SetInt32("Seed", Value.Seed);
        writer.SetBoolean("FitToPart", Value.FitToPart);
        writer.SetInt32("LayerCount", Value.Layers?.Count ?? 0);
        if (Value.Layers != null) for (int i = 0; i < Value.Layers.Count; i++) LayerArchive.Write(writer.CreateChunk("Layer", i), Value.Layers[i]);
        foreach (var property in typeof(TimberSettings).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(double)))
            writer.SetDouble(property.Name, (double)property.GetValue(Value));
        foreach (var property in typeof(TimberSettings).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(Color?)))
            if (property.GetValue(Value) is Color color) writer.SetInt32(property.Name, color.ToArgb());
        return true;
    }
    public override bool Read(GH_IReader reader)
    {
        Value = null;
        if (!reader.ItemExists("HasValue") || !reader.GetBoolean("HasValue")) return true;
        if (!reader.ItemExists("Version") || (reader.GetInt32("Version") < 1 || reader.GetInt32("Version") > 3)) return false;
        var value = new TimberSettings { Product = (TimberProduct)reader.GetInt32("Product"), Seed = reader.GetInt32("Seed") };
        if (reader.ItemExists("Look")) value.Look = (TimberLook)reader.GetInt32("Look");
        if (reader.GetInt32("Version") >= 3) {
            value.FitToPart = reader.GetBoolean("FitToPart");
            int count = reader.GetInt32("LayerCount");
            if (count < 0 || count > 4096) return false;
            if (count > 0) {
                try { value.Layers = Enumerable.Range(0, count).Select(i => LayerArchive.Read(reader.FindChunk("Layer", i) ?? throw new ArgumentException("Missing layer archive"))).ToArray(); }
                catch (ArgumentException) { return false; }
            }
        }
        foreach (var property in typeof(TimberSettings).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(double)))
            if (reader.ItemExists(property.Name)) property.SetValue(value, reader.GetDouble(property.Name));
        foreach (var property in typeof(TimberSettings).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(Color?)))
            if (reader.ItemExists(property.Name)) property.SetValue(value, Color.FromArgb(reader.GetInt32(property.Name)));
        try { value.Validate(); Value = value; return true; } catch (ArgumentException) { return false; }
    }
}

public sealed class SettingsParam : GH_PersistentParam<SettingsGoo>
{
    public SettingsParam() : base("Timber Display Settings", "TDS", "Woodie timber appearance settings", "Display", "Woodie") { }
    public override Guid ComponentGuid => new("06b27042-911f-4614-ac44-378a78736284");
    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.SettingsParameter;
    protected override GH_GetterResult Prompt_Singular(ref SettingsGoo value) => GH_GetterResult.cancel;
    protected override GH_GetterResult Prompt_Plural(ref List<SettingsGoo> values) => GH_GetterResult.cancel;
}
