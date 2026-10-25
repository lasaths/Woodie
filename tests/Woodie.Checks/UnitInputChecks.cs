using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Grasshopper;
using static Woodie.Checks.Program;

namespace Woodie.Checks;
internal static class UnitInputChecks
{
    public static void Diagnostic()
    {
        using var ownedRhino = RhinoDoc.ActiveDoc == null ? RhinoDoc.Create(null) : null;
        var rhino = RhinoDoc.ActiveDoc ?? ownedRhino;
        var previous = rhino.ModelUnitSystem;
        try {
            rhino.ModelUnitSystem = UnitSystem.Meters;
            using var doc = new GH_Document { Enabled = true };
            var preview = Make(doc);
            doc.NewSolution(false);
            string message = string.Join(" ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Error));
            Assert(message.Contains("Geometry Units") && message.Contains("Meters") && message.Contains("mm"),
                "Budget error must explain document/source units and physical size; got: " + message);
        } finally { rhino.ModelUnitSystem = previous; }
    }

    public static void Explicit()
    {
        using var ownedRhino = RhinoDoc.ActiveDoc == null ? RhinoDoc.Create(null) : null;
        var rhino = RhinoDoc.ActiveDoc ?? ownedRhino; var previous = rhino.ModelUnitSystem;
        try {
            foreach (var units in new[] { UnitSystem.Millimeters, UnitSystem.Meters, UnitSystem.Inches }) {
                rhino.ModelUnitSystem = units;
                using var doc = new GH_Document { Enabled = true };
                var preview = Make(doc);
                Assert(preview.Params.Input.Count == 8, "Missing optional Geometry Units / Stock Geometry inputs");
                ((Param_String)preview.Params.Input[6]).SetPersistentData("Millimetres");
                doc.NewSolution(false);
                Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), string.Join("; ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
                var box = ((GH_Box)preview.Params.Output[2].VolatileData.AllData(true).Single()).Value;
                double toMm = RhinoMath.UnitScale(units, UnitSystem.Millimeters);
                Near(box.X.Length * toMm, 1800, .02); Near(box.Y.Length * toMm, 500, .02); Near(box.Z.Length * toMm, 27, .02);
                var original = (GH_Brep)((Param_Geometry)preview.Params.Input[0]).PersistentData.AllData(true).Single();
                Near(original.Value.GetBoundingBox(true).Diagonal.X, 1800, .01);
                Assert(preview.Params.Output[0].VolatileDataCount > 0, "Missing unit-converted meshes");
            }
        } finally { rhino.ModelUnitSystem = previous; }
    }

    public static void GuidesAndArchives()
    {
        using var guide = GeometryGallery.Bow();
        using var extrusion = GeometryGallery.BowedExtrusion();
        var shift = Transform.Translation(2000, 3000, 500);
        guide.Transform(shift); extrusion.Transform(shift);
        var plane = new Plane(new Point3d(2000, 3000, 500), Vector3d.ZAxis);
        double length = guide.GetLength();
        using (var converted = new Woodie.Geometry.GeometryUnits(extrusion, guide, plane, "mm", UnitSystem.Meters)) {
            Near(converted.Guide.GetLength(), length * .001, .00001);
            Near(converted.BasePlane.Value.Origin.DistanceTo(new Point3d(2, 3, .5)), 0, 1e-9);
            using var part = Woodie.Geometry.PreparedPart.Create(converted.Geometry, new TimberSettings { Product=TimberProduct.ThreeS },
                converted.ToMillimetres, .000001, converted.BasePlane, guide:converted.Guide);
            Near(part.LengthMm, length, .2); Near(part.DepthMm, 27, .2);
        }
        Near(guide.GetLength(), length, .00001); Near(plane.Origin.X, 2000);
        Reject(() => new Woodie.Geometry.GeometryUnits(extrusion, null, null, "banana", UnitSystem.Meters), "Unknown Geometry Units");
        Reject(() => new Woodie.Geometry.GeometryUnits(extrusion, null, null, "mm", UnitSystem.None), "physical Rhino document units");

        using var doc = new GH_Document { Enabled=true };
        var old = Make(doc);
        old.Params.UnregisterInputParameter(old.Params.Input[7], true);
        old.Params.UnregisterInputParameter(old.Params.Input[6], true);
        old.Params.OnParametersChanged();
        var archive = new GH_IO.Serialization.GH_Archive();
        Assert(archive.AppendObject(old, "Preview"), "Legacy preview serialization failed");
        var restored = new PreviewComponent();
        Assert(archive.ExtractObject(restored, "Preview"), "Legacy six-input preview did not restore");
        Assert(restored.Params.Input.Count == 8 && restored.Params.Input[6].Name == "Geometry Units" && restored.Params.Input[7].Optional, "Legacy preview must gain optional units and stock inputs");
        Assert(((Param_String)restored.Params.Input[6]).PersistentDataCount == 0, "Legacy geometry must retain Document units");
    }

    static PreviewComponent Make(GH_Document doc)
    {
        GH_Document.EnableSolutions = true;
        var preview = new PreviewComponent { Hidden = true }; preview.CreateAttributes(); doc.AddObject(preview, false);
        using var shape = new Box(Plane.WorldXY,new Interval(0,1800),new Interval(0,500),new Interval(0,27)).ToBrep();
        ((Param_Geometry)preview.Params.Input[0]).SetPersistentData(new GH_Brep(shape.DuplicateBrep()));
        ((SettingsParam)preview.Params.Input[1]).SetPersistentData(new SettingsGoo(new TimberSettings { Product=TimberProduct.ThreeS, Resolution=20, EdgeResolution=5, KnotDensity=0 }));
        return preview;
    }
}
