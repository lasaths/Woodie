using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino;
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
            NickName = "Woodie · Complete gallery",
            UserText = "WOODIE | 8 PRODUCTS × 11 GEOMETRY CASES\n\nWorks in millimetre, metre and inch Rhino documents.\nEvery preview declares Geometry Units = Millimetres.\nSource geometry remains unchanged.\n\nViewport rows = products; columns = geometry cases.\nFlat/bowed solid pairs are visible initially and have\nequal centreline lengths. Enable other preview\ncomponents or a product group's preview to explore.\n\nEach product uses its own representative thickness.\nOpen surfaces stay open; presets are visual only."
        };
        note.CreateAttributes(); note.Attributes.Bounds = new RectangleF(20, 0, 440, 230); doc.AddObject(note, false);
        using var referenceGuide = GeometryGallery.Bow();
        double length = referenceGuide.GetLength();
        int row = 0;
        foreach (var product in TimberProducts.MenuOrder) {
            double depth = new TimberSettings { Product = product }.VirtualDepthMm;
            float column = row * 1000;
            var members = new List<Guid>();
            void Add(IGH_DocumentObject obj) { doc.AddObject(obj, false); members.Add(obj.InstanceGuid); }
            var material = new SettingsComponent(); material.CreateAttributes();
            material.SelectProduct(product); material.SelectLook(TimberLook.CleanPale);
            material.Attributes.Pivot = new PointF(column + 350, 320); Add(material);
            ((Param_Number)material.Params.Input[2]).PersistentData.Clear();
            ((Param_Number)material.Params.Input[2]).SetPersistentData(8d);
            var units = new Param_String { Name = "Source coordinate units", NickName = "Millimetres" };
            units.SetPersistentData("Millimetres"); units.CreateAttributes(); units.Attributes.Pivot = new PointF(column + 160, 540); Add(units);
            var fixtures = GeometryGallery.Fixtures(depth);
            fixtures[0].Dispose();
            fixtures[0] = new GeometryGallery.Fixture("01 Flat solid Brep (matched length)",
                new Box(Plane.WorldXY, new Interval(0, length), new Interval(0, 500), new Interval(0, depth)).ToBrep(), "Straight");
            try {
                for (int index = 0; index < fixtures.Count; index++) {
                    var fixture = fixtures[index];
                    int slot = index == 0 ? 0 : index == 8 ? 1 : index < 8 ? index + 1 : index;
                    var move = Transform.Translation(slot * 2600, row * 1400, 0);
                    fixture.Geometry.Transform(move); fixture.Guide?.Transform(move);
                    IGH_GeometricGoo goo = fixture.Geometry switch {
                        Extrusion extrusion => new GH_Extrusion((Extrusion)extrusion.Duplicate()),
                        Brep brep => new GH_Brep(brep.DuplicateBrep()),
                        Surface surface => new GH_Surface((Surface)surface.Duplicate()),
                        Mesh mesh => new GH_Mesh(mesh.DuplicateMesh()), _ => throw new Exception("Unsupported example fixture")
                    };
                    float y = 750 + index * 200;
                    var input = new Param_Geometry { Name = product.Label() + " / " + fixture.Name, NickName = fixture.Name, Hidden = true };
                    input.SetPersistentData(goo); input.CreateAttributes(); input.Attributes.Pivot = new PointF(column + 100, y); Add(input);
                    var preview = new PreviewComponent { NickName = product.Label() + " / " + fixture.Name, Hidden = index != 0 && index != 8 };
                    preview.CreateAttributes(); preview.Attributes.Pivot = new PointF(column + 700, y); Add(preview);
                    preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(material.Params.Output[0]);
                    preview.Params.Input[6].AddSource(units);
                    if (fixture.Guide != null) {
                        var guide = new Param_Curve { Name = product.Label() + " / " + fixture.Name + " guide", NickName = "Guide (mm)", Hidden = true };
                        guide.SetPersistentData(new GH_Curve(fixture.Guide.DuplicateCurve())); guide.CreateAttributes();
                        guide.Attributes.Pivot = new PointF(column + 400, y + 80); Add(guide);
                        preview.Params.Input[4].AddSource(guide);
                        ((Param_Plane)preview.Params.Input[3]).SetPersistentData(new GH_Plane(Plane.WorldXY));
                    }
                }
            } finally { foreach (var fixture in fixtures) fixture.Dispose(); }
            var group = new GH_Group { NickName = product.Label() + $" · {depth:0.#} mm · 11 cases", Colour = Color.FromArgb(65, 190, 160, 120) };
            foreach (var id in members) group.AddObject(id);
            doc.AddObject(group, false); row++;
        }
        var archive = new GH_Archive(); Assert(archive.AppendObject(doc, "Definition"), "Gallery serialization failed");
        string path = Path.Combine(directory, "Timber-preview.ghx");
        Assert(archive.WriteToFile(path, true, false), "Could not write complete gallery");
        Verify(path); Console.WriteLine("  Wrote and reopened 88-case gallery: " + path);
    }

    internal static void Verify(string path)
    {
        var rhino = RhinoDoc.ActiveDoc ?? throw new Exception("Example verification needs an active test Rhino document");
        var previousUnits = rhino.ModelUnitSystem; double previousTolerance = rhino.ModelAbsoluteTolerance;
        var dimensions = new Dictionary<string, double[]>();
        try {
            foreach (var documentUnits in new[] { UnitSystem.Millimeters, UnitSystem.Meters }) {
                rhino.ModelUnitSystem = documentUnits;
                double toMm = RhinoMath.UnitScale(documentUnits, UnitSystem.Millimeters);
                rhino.ModelAbsoluteTolerance = .001 / toMm;
                using var reopened = new GH_Document { Enabled = true };
                var archive = new GH_Archive();
                Assert(archive.ReadFromFile(path) && archive.ExtractObject(reopened, "Definition"), "Complete gallery did not reopen");
                Assert(reopened.Objects.OfType<SettingsComponent>().Count() == 8, "Gallery needs all eight materials");
                Assert(!reopened.Objects.OfType<PresetsComponent>().Any(), "Legacy presets are not release examples");
                var inputs = reopened.Objects.OfType<Param_Geometry>().ToArray();
                Assert(inputs.Length == 88, "Gallery needs all 8 × 11 geometry cases");
                Assert(inputs.SelectMany(p => p.PersistentData.AllData(true)).All(g => g.IsValid), "Invalid embedded gallery geometry");
                var previews = reopened.Objects.OfType<PreviewComponent>().ToArray();
                Assert(previews.Length == 88, "Missing gallery previews");
                Assert(previews.Count(p => !p.Hidden) == 16, "Initial view should show eight flat/bowed pairs");
                foreach (var preview in previews) {
                    Assert(preview.Params.Input[6].SourceCount == 1, "Example source units must be explicit");
                    preview.Hidden = true;
                }
                foreach (var product in TimberProducts.MenuOrder) {
                    var flat = inputs.Single(p => p.Name == product.Label() + " / 01 Flat solid Brep (matched length)");
                    var bowed = inputs.Single(p => p.Name == product.Label() + " / 09 Bowed solid Brep - guide");
                    var a = ((GH_Brep)flat.PersistentData.AllData(true).Single()).Value;
                    var b = ((GH_Brep)bowed.PersistentData.AllData(true).Single()).Value;
                    using var guide = GeometryGallery.Bow();
                    Near(a.GetBoundingBox(true).Diagonal.X, guide.GetLength(), .02);
                    Near(a.GetVolume(), b.GetVolume(), Math.Max(1, a.GetVolume() * .00001));
                }
                reopened.NewSolution(false);
                foreach (var preview in previews) {
                    Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), documentUnits + " gallery failed: " + preview.NickName + ": " + string.Join("; ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
                    Assert(preview.Params.Output[0].VolatileDataCount > 0, "Gallery mesh output missing");
                    var box = ((GH_Box)preview.Params.Output[2].VolatileData.AllData(true).Single()).Value;
                    var actual = new[] { box.X.Length * toMm, box.Y.Length * toMm, box.Z.Length * toMm };
                    if (documentUnits == UnitSystem.Millimeters) dimensions[preview.NickName] = actual;
                    else for (int axis = 0; axis < 3; axis++) Near(actual[axis], dimensions[preview.NickName][axis], .7);
                    foreach (var mesh in preview.Params.Output[0].VolatileData.AllData(true).OfType<GH_Mesh>())
                        Assert(mesh.Value.IsValid && mesh.Value.TextureCoordinates.Count == mesh.Value.Vertices.Count, "Invalid gallery mesh/UVs");
                }
                Console.WriteLine($"  Release gallery: 88 cases solve in {documentUnits}");
            }
        } finally { rhino.ModelUnitSystem = previousUnits; rhino.ModelAbsoluteTolerance = previousTolerance; }
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
