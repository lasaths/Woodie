using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Grasshopper;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class ExampleDefinitions
{
    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        using var doc = new GH_Document { Enabled = true };
        var note = new GH_Panel {
            NickName = "Woodie · Timber preview",
            UserText = "WOODIE | TIMBER PREVIEW\n\nUse a millimetre Rhino document.\nChoose Product / Appearance on Timber Material.\nThe triangle below its icon hides the controls.\n\nFlat and bowed boards: same centreline length,\n500 x 27 mm section; 220 mm bow rise.\nGuide Curve controls longitudinal grain.\n\nVisual presets only: changing Product does not\nchange geometry or specify a manufactured layup."
        };
        note.CreateAttributes();
        note.Attributes.Bounds = new RectangleF(30, 20, 400, 200);
        doc.AddObject(note, false);
        var material = new SettingsComponent();
        material.CreateAttributes();
        material.SelectProduct(TimberProduct.ThreeS);
        material.SelectLook(TimberLook.CleanPale);
        material.Attributes.Pivot = new PointF(350, 280);
        doc.AddObject(material, false);

        using var guide = GeometryGallery.Bow();
        double length = guide.GetLength();
        using var flat = new Box(Plane.WorldXY, new Interval(0, length), new Interval(800, 1300), new Interval(0, 27)).ToBrep();
        using var extrusion = GeometryGallery.BowedExtrusion();
        using var bowed = extrusion.ToBrep();
        int index = 0;
        foreach (var shape in new[] { flat, bowed })
        {
            var input = new Param_Geometry {
                Name = index == 0 ? "Flat board (mm)" : "Upward-bowed board (mm)",
                NickName = index == 0 ? "Flat" : "Bowed",
                Hidden = true
            };
            input.SetPersistentData(new GH_Brep(shape.DuplicateBrep()));
            input.CreateAttributes();
            input.Attributes.Pivot = new PointF(90, 290 + index * 230);
            doc.AddObject(input, false);
            var preview = new PreviewComponent { NickName = index == 0 ? "Flat timber" : "Bowed timber" };
            preview.CreateAttributes();
            preview.Attributes.Pivot = new PointF(730, 290 + index * 230);
            doc.AddObject(preview, false);
            preview.Params.Input[0].AddSource(input);
            preview.Params.Input[1].AddSource(material.Params.Output[0]);
            ((Param_Plane)preview.Params.Input[3]).SetPersistentData(new GH_Plane(Plane.WorldXY));
            if (index == 1)
            {
                var curve = new Param_Curve { Name = "Longitudinal bend guide", NickName = "Guide", Hidden = true };
                curve.SetPersistentData(new GH_Curve(guide.DuplicateCurve()));
                curve.CreateAttributes();
                curve.Attributes.Pivot = new PointF(390, 540);
                doc.AddObject(curve, false);
                preview.Params.Input[4].AddSource(curve);
            }
            index++;
        }

        var archive = new GH_Archive();
        Assert(archive.AppendObject(doc, "Definition"), "Could not serialize release example");
        string path = Path.Combine(directory, "Timber-preview.ghx");
        Assert(archive.WriteToFile(path, true, false), "Could not write release example");
        Verify(path);
        Console.WriteLine("  Wrote and reopened " + path);
    }

    internal static void Verify(string path)
    {
        using var reopened = new GH_Document { Enabled = true };
        var archive = new GH_Archive();
        Assert(archive.ReadFromFile(path) && archive.ExtractObject(reopened, "Definition"), "Release example did not reopen");
        Assert(reopened.Objects.OfType<SettingsComponent>().Count() == 1, "Example must use the combined material component");
        Assert(!reopened.Objects.OfType<PresetsComponent>().Any(), "Example must not use the legacy preset component");
        var shapes = reopened.Objects.OfType<Param_Geometry>().SelectMany(p => p.PersistentData.AllData(true)).OfType<GH_Brep>().ToArray();
        Assert(shapes.Length == 2 && shapes.All(g => g.Value != null && g.Value.IsValid && g.Value.IsSolid), "Example must embed two valid solid boards");
        var guides = reopened.Objects.OfType<Param_Curve>().SelectMany(p => p.PersistentData.AllData(true)).OfType<GH_Curve>().ToArray();
        Assert(guides.Length == 1 && guides[0].Value.IsValid && !guides[0].Value.IsClosed, "Example guide is missing or invalid");
        Near(shapes[0].Value.GetBoundingBox(true).Diagonal.X, guides[0].Value.GetLength(), 0.01);
        foreach (var shape in shapes) Near(shape.Value.GetVolume() / guides[0].Value.GetLength() / 500, 27, 0.02);
        var previews = reopened.Objects.OfType<PreviewComponent>().ToArray();
        Assert(previews.Length == 2, "Example must contain two timber previews");
        foreach (var preview in previews) preview.Hidden = true;
        reopened.NewSolution(false);
        foreach (var preview in previews)
        {
            Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), "Release example preview failed");
            Assert(preview.Params.Output[0].VolatileDataCount > 0 && preview.Params.Output[2].VolatileDataCount == 1, "Example mesh or box output missing");
            foreach (var mesh in preview.Params.Output[0].VolatileData.AllData(true).OfType<GH_Mesh>())
                Assert(mesh.Value.IsValid && mesh.Value.TextureCoordinates.Count == mesh.Value.Vertices.Count, "Example mesh or UVs invalid");
        }
    }

    internal static Mesh BentSolid(Curve guide)
    {
        var mesh = new Mesh();
        double[] parameters = guide.DivideByCount(64, true);
        foreach (double t in parameters)
        {
            Point3d p = guide.PointAt(t); Vector3d x = guide.TangentAt(t); x.Unitize();
            Vector3d z = Vector3d.ZAxis - (Vector3d.ZAxis * x) * x; z.Unitize();
            Vector3d y = Vector3d.CrossProduct(z, x);
            foreach (var corner in new[] { (-120, -100), (120, -100), (120, 100), (-120, 100) })
                mesh.Vertices.Add(p + corner.Item1 * y + corner.Item2 * z);
        }
        for (int i = 0; i < parameters.Length - 1; i++)
            for (int j = 0; j < 4; j++) mesh.Faces.AddFace(i * 4 + j, i * 4 + (j + 1) % 4, (i + 1) * 4 + (j + 1) % 4, (i + 1) * 4 + j);
        mesh.Faces.AddFace(3, 2, 1, 0);
        int last = (parameters.Length - 1) * 4; mesh.Faces.AddFace(last, last + 1, last + 2, last + 3);
        mesh.Normals.ComputeNormals(); mesh.Unweld(Math.PI / 4, true);
        return mesh;
    }
}
