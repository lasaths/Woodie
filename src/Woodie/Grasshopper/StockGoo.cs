using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Woodie.Stock;

namespace Woodie.Grasshopper;

public sealed class StockGoo : GH_Goo<WoodieStock>
{
    public StockGoo() { }
    public StockGoo(WoodieStock value) { Value = value; }
    public override bool IsValid => Value?.Geometry != null && Value.OriginalGeometry != null && Value.Definition != null;
    public override string TypeName => "Woodie Stock Model";
    public override string TypeDescription => "Current timber geometry with its original stock definition";
    public override IGH_Goo Duplicate() => new StockGoo(Value?.Duplicate());
    public override string ToString() => Value == null ? "Null Woodie stock" : $"Woodie stock · {Value.Settings}";
    public override object ScriptVariable() => Value;
    public override bool CastFrom(object source)
    {
        if (source is WoodieStock stock) { Value = stock.Duplicate(); return true; }
        return false;
    }
    public override bool Write(GH_IWriter writer)
    {
        writer.SetBoolean("HasValue", Value != null);
        if (Value == null) return true;
        writer.SetInt32("Version", 1);
        writer.SetDouble("LengthMm", Value.Definition.LengthMm);
        writer.SetDouble("WidthMm", Value.Definition.WidthMm);
        writer.SetDouble("DepthMm", Value.Definition.DepthMm);
        new SettingsGoo(Value.Settings).Write(writer.CreateChunk("Settings"));
        WriteGeometry(writer.CreateChunk("Geometry"), Value.Geometry);
        WriteGeometry(writer.CreateChunk("Original"), Value.OriginalGeometry);
        writer.SetBoolean("HasPlane", Value.BasePlane.HasValue);
        if (Value.BasePlane.HasValue) {
            var pl = Value.BasePlane.Value;
            writer.SetPlane("Plane", new GH_IO.Types.GH_Plane(
                new GH_IO.Types.GH_Point3D(pl.OriginX, pl.OriginY, pl.OriginZ),
                new GH_IO.Types.GH_Point3D(pl.XAxis.X, pl.XAxis.Y, pl.XAxis.Z),
                new GH_IO.Types.GH_Point3D(pl.YAxis.X, pl.YAxis.Y, pl.YAxis.Z)));
        }
        writer.SetBoolean("HasFibre", Value.Fibre.HasValue);
        if (Value.Fibre.HasValue) writer.SetPoint3D("Fibre", new GH_IO.Types.GH_Point3D(Value.Fibre.Value.X, Value.Fibre.Value.Y, Value.Fibre.Value.Z));
        writer.SetBoolean("HasGuide", Value.Guide != null);
        if (Value.Guide != null) WriteGeometry(writer.CreateChunk("Guide"), Value.Guide);
        return true;
    }
    public override bool Read(GH_IReader reader)
    {
        Value = null;
        if (!reader.ItemExists("HasValue") || !reader.GetBoolean("HasValue")) return true;
        if (!reader.ItemExists("Version") || reader.GetInt32("Version") != 1) return false;
        var settingsChunk = reader.FindChunk("Settings");
        if (settingsChunk == null) return false;
        var settingsGoo = new SettingsGoo();
        if (!settingsGoo.Read(settingsChunk) || settingsGoo.Value == null) return false;
        GeometryBase geometry = null, original = null; Curve guide = null;
        try
        {
            geometry = ReadGeometry(reader.FindChunk("Geometry") ?? throw new ArgumentException("Missing geometry"));
            original = ReadGeometry(reader.FindChunk("Original") ?? throw new ArgumentException("Missing original geometry"));
            if (reader.GetBoolean("HasGuide"))
                guide = ReadGeometry(reader.FindChunk("Guide") ?? throw new ArgumentException("Missing guide")) as Curve
                    ?? throw new ArgumentException("Archived guide is not a curve.");
            Plane? plane = null;
            if (reader.GetBoolean("HasPlane")) {
                var gh = reader.GetPlane("Plane");
                plane = new Plane(new Point3d(gh.Origin.x, gh.Origin.y, gh.Origin.z),
                    new Vector3d(gh.XAxis.x, gh.XAxis.y, gh.XAxis.z),
                    new Vector3d(gh.YAxis.x, gh.YAxis.y, gh.YAxis.z));
            }
            Vector3d? fibre = null;
            if (reader.GetBoolean("HasFibre")) {
                var pt = reader.GetPoint3D("Fibre");
                fibre = new Vector3d(pt.x, pt.y, pt.z);
            }
            var definition = StockDefinition.FromTimber(settingsGoo.Value, reader.GetDouble("LengthMm"), reader.GetDouble("WidthMm"), reader.GetDouble("DepthMm"));
            Value = new WoodieStock(geometry, definition, original, plane, fibre, guide);
            return true;
        }
        catch (ArgumentException) { return false; }
        finally { geometry?.Dispose(); original?.Dispose(); guide?.Dispose(); }
    }

    static void WriteGeometry(GH_IWriter writer, GeometryBase geometry)
    {
        GeometryBase payload = geometry is Extrusion extrusion ? extrusion.ToBrep() : geometry;
        bool owns = !ReferenceEquals(payload, geometry);
        try
        {
            var goo = GH_Convert.ToGeometricGoo(payload) ?? throw new ArgumentException("Unsupported stock geometry type.");
            writer.SetString("Type", goo.GetType().AssemblyQualifiedName);
            goo.Write(writer.CreateChunk("Data"));
        }
        finally { if (owns) payload.Dispose(); }
    }

    static GeometryBase ReadGeometry(GH_IReader reader)
    {
        string typeName = reader.GetString("Type");
        var data = reader.FindChunk("Data") ?? throw new ArgumentException("Missing geometry data.");
        var type = Type.GetType(typeName) ?? throw new ArgumentException("Unknown archived geometry type.");
        if (Activator.CreateInstance(type) is not IGH_Goo goo) throw new ArgumentException("Archived geometry type is not a Grasshopper goo.");
        if (!goo.Read(data)) throw new ArgumentException("Could not read archived stock geometry.");
        return goo.ScriptVariable() as GeometryBase ?? throw new ArgumentException("Archived stock geometry is empty.");
    }
}

public sealed class StockParam : GH_PersistentParam<StockGoo>
{
    public StockParam() : base("Stock Model", "Stock", "Woodie stock model with fixed material definition", "Display", "Woodie") { }
    public override Guid ComponentGuid => new("a6c2e1f0-4b8d-4d91-9f3a-2c7e5b1d0842");
    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.SettingsParameter;
    protected override GH_GetterResult Prompt_Singular(ref StockGoo value) => GH_GetterResult.cancel;
    protected override GH_GetterResult Prompt_Plural(ref List<StockGoo> values) => GH_GetterResult.cancel;
}
