using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Grasshopper;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class GeometryGallery
{
    internal sealed record Fixture(string Name, GeometryBase Geometry, string Mapping, Curve Guide = null) : IDisposable
    {
        public void Dispose() { Geometry.Dispose(); Guide?.Dispose(); }
    }

    internal static Curve Bow() => new Arc(new Point3d(0, 0, 0), new Point3d(900, 0, 220), new Point3d(1800, 0, 0)).ToNurbsCurve();

    internal static Extrusion BowedExtrusion(double depth = 27)
    {
        const double radius = (900d * 900 + 220d * 220) / 440;
        var centre = new Point3d(900, 0, 220 - radius);
        Point3d At(Point3d p, double offset) { var radial = p - centre; radial.Unitize(); return centre + radial * (radius + offset); }
        Point3d start = new(0, 0, 0), mid = new(900, 0, 220), end = new(1800, 0, 0);
        using var top = new Arc(At(start, depth / 2), At(mid, depth / 2), At(end, depth / 2)).ToNurbsCurve();
        using var bottom = new Arc(At(start, -depth / 2), At(mid, -depth / 2), At(end, -depth / 2)).ToNurbsCurve();
        using var profile = new PolyCurve();
        profile.Append(top); profile.Append(new LineCurve(top.PointAtEnd, bottom.PointAtEnd));
        bottom.Reverse(); profile.Append(bottom); profile.Append(new LineCurve(bottom.PointAtEnd, top.PointAtStart));
        return Extrusion.Create(profile, 500, true) ?? throw new Exception("Could not create the bowed strip extrusion");
    }

    static Mesh BowedMesh(Curve guide, double depth = 27)
    {
        var mesh = new Mesh();
        var parameters = guide.DivideByCount(64, true);
        foreach (double parameter in parameters)
        {
            var p = guide.PointAt(parameter); var x = guide.TangentAt(parameter); x.Unitize();
            var z = Vector3d.ZAxis - (Vector3d.ZAxis * x) * x; z.Unitize();
            var y = Vector3d.CrossProduct(z, x);
            foreach (var corner in new[] { (-250d, -depth / 2), (250d, -depth / 2), (250d, depth / 2), (-250d, depth / 2) })
                mesh.Vertices.Add(p + corner.Item1 * y + corner.Item2 * z);
        }
        for (int i = 0; i < parameters.Length - 1; i++) for (int j = 0; j < 4; j++)
            mesh.Faces.AddFace(i * 4 + j, i * 4 + (j + 1) % 4, (i + 1) * 4 + (j + 1) % 4, (i + 1) * 4 + j);
        mesh.Faces.AddFace(3, 2, 1, 0); int last = (parameters.Length - 1) * 4;
        mesh.Faces.AddFace(last, last + 1, last + 2, last + 3);
        mesh.Normals.ComputeNormals(); mesh.Unweld(Math.PI / 4, true);
        return mesh;
    }

    internal static List<Fixture> Fixtures(double depth)
    {
        var items = new List<Fixture>();
        items.Add(new("01 Solid Brep", new Box(Plane.WorldXY, new Interval(0, 1800), new Interval(0, 500), new Interval(0, depth)).ToBrep(), "Straight"));
        using var rectangle = new Rectangle3d(Plane.WorldXY, 1800, 500).ToNurbsCurve();
        items.Add(new("02 Extrusion", Extrusion.Create(rectangle, depth, true), "Straight"));
        items.Add(new("03 Flat surface", new PlaneSurface(Plane.WorldXY, new Interval(0, 1800), new Interval(0, 500)), "Straight"));
        items.Add(new("04 Closed mesh", Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 1800, 500, depth), 1, 1, 1), "Straight"));
        var open = new Mesh(); open.Vertices.Add(0, 0, 0); open.Vertices.Add(1800, 0, 0); open.Vertices.Add(1800, 500, 0); open.Vertices.Add(0, 500, 0); open.Faces.AddFace(0, 1, 2, 3);
        items.Add(new("05 Open mesh", open, "Straight"));
        using var hole = new Circle(new Plane(new Point3d(900, 250, 0), Vector3d.ZAxis), 120).ToNurbsCurve();
        var trimmed = Brep.CreatePlanarBreps(new Curve[] { rectangle, hole }, 0.001).Single();
        items.Add(new("06 Trimmed surface with hole", trimmed, "Straight"));
        var bow = Bow();
        var surface = Surface.CreateExtrusion(bow, new Vector3d(0, 500, 0));
        items.Add(new("07 Bowed surface - automatic", surface, "Bent surface"));
        items.Add(new("08 Bowed single-face Brep - automatic", surface.ToBrep(), "Bent surface"));
        var extrusion = BowedExtrusion(depth);
        items.Add(new("09 Bowed solid Brep - guide", extrusion.ToBrep(), "Guide curve", Bow()));
        items.Add(new("10 Bowed extrusion - guide", extrusion, "Guide curve", Bow()));
        items.Add(new("11 Bowed mesh - guide", BowedMesh(bow, depth), "Guide curve", bow));
        return items;
    }

    public static void Run()
    {
        foreach (var product in TimberProducts.MenuOrder) RunProduct(product);
        WriteMatchedPair();
        WriteMaterials();
    }

    static void RunProduct(TimberProduct product)
    {
        double depth = new TimberSettings { Product = product }.VirtualDepthMm;
        var fixtures = Fixtures(depth);
        using var doc = new GH_Document { Enabled = true };
        GH_Document.EnableSolutions = true;
        var settings = new TimberSettings { Product = product, Seed = 17, Resolution = 4, EdgeResolution = 0.5 };
        var shared = new SettingsParam { Name = product.Label() + " - millimetres", NickName = product.Label() };
        shared.SetPersistentData(new SettingsGoo(settings)); shared.CreateAttributes(); shared.Attributes.Pivot = new PointF(350, 80); doc.AddObject(shared, false);
        var expected = new List<(Fixture Fixture, PreviewComponent Component, int Faces)>();
        try
        {
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                var move = Transform.Translation((i % 3) * 2600, (i / 3) * 1100, 0);
                f.Geometry.Transform(move); f.Guide?.Transform(move);
                var originalBounds = f.Geometry.GetBoundingBox(true);
                using var part = PreparedPart.Create(f.Geometry, settings, 1, 0.001, basePlane: f.Guide == null ? null : Plane.WorldXY, guide: f.Guide);
                Assert(part.Mapping == f.Mapping, f.Name + " chose " + part.Mapping);
                Near(part.DepthMm, depth, 0.1);
                Assert(part.WidthMm >= 499 && part.WidthMm <= 501, f.Name + " lost physical width: " + part.WidthMm);
                Assert(f.Geometry.GetBoundingBox(true).Equals(originalBounds), f.Name + " changed input geometry");
                using (var metres = f.Geometry.Duplicate()) {
                    metres.Transform(Transform.Scale(Point3d.Origin, 0.001));
                    using var metreGuide = f.Guide?.DuplicateCurve(); metreGuide?.Transform(Transform.Scale(Point3d.Origin, 0.001));
                    using var metric = PreparedPart.Create(metres, settings, 1000, 0.000001, basePlane: metreGuide == null ? null : Plane.WorldXY, guide: metreGuide);
                    Near(metric.LengthMm, part.LengthMm, 0.5); Near(metric.WidthMm, part.WidthMm, 0.5); Near(metric.DepthMm, part.DepthMm, 0.5);
                }
                if (f.Mapping == "Bent surface" || f.Mapping == "Guide curve")
                {
                    using var reference = Bow(); Near(part.LengthMm, reference.GetLength(), 0.2);
                    Assert(originalBounds.Max.Z > 210 && originalBounds.Min.Z < 20, f.Name + " is not an upward-bowed strip");
                }
                if (i == 4) Assert(!part.Mesh.IsClosed && part.Mesh.GetBoundingBox(false).Max.Z == 0, "Open mesh was thickened");
                if (i == 5) {
                    foreach (var face in part.Mesh.Faces) {
                        var centre = (part.Mesh.Vertices.Point3dAt(face.A) + part.Mesh.Vertices.Point3dAt(face.B) + part.Mesh.Vertices.Point3dAt(face.C)) / 3;
                        Assert(centre.DistanceTo(new Point3d(6100, 1350, 0)) >= 115, "Trimmed hole was filled");
                    }
                }
                var pages = TextureAtlas.Prepare(part, settings);
                try { TextureAtlas.BakeMissing(pages, CancellationToken.None); }
                finally { foreach (var page in pages) page.Dispose(); }
                IGH_GeometricGoo goo = f.Geometry switch {
                    Extrusion e => new GH_Extrusion(e), Brep b => new GH_Brep(b), Surface s => new GH_Surface(s), Mesh m => new GH_Mesh(m), _ => throw new Exception("Fixture type") };
                var input = new Param_Geometry { Name = f.Name, NickName = f.Name, Hidden = true };
                input.SetPersistentData(goo); input.CreateAttributes(); input.Attributes.Pivot = new PointF(80, 220 + 180 * i); doc.AddObject(input, false);
                var preview = new PreviewComponent { NickName = f.Name, Hidden = true };
                preview.CreateAttributes(); preview.Attributes.Pivot = new PointF(720, 220 + 180 * i); doc.AddObject(preview, false);
                preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(shared);
                if (f.Guide != null) {
                    var guide = new Param_Curve { Name = "Longitudinal bend guide", NickName = "Guide (mm)", Hidden = true };
                    guide.SetPersistentData(new GH_Curve(f.Guide)); guide.CreateAttributes(); guide.Attributes.Pivot = new PointF(350, 160 + 180 * i); doc.AddObject(guide, false);
                    preview.Params.Input[4].AddSource(guide);
                    // The broad faces curve; planar end caps alone cannot establish thickness orientation.
                    ((Param_Plane)preview.Params.Input[3]).SetPersistentData(new GH_Plane(Plane.WorldXY));
                }
                expected.Add((f, preview, part.Mesh.Faces.Count));
            }
            doc.NewSolution(false);
            foreach (var item in expected) {
                var preview = item.Component;
                Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), item.Fixture.Name + ": " + string.Join("; ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
                var meshes = preview.Params.Output[0].VolatileData.AllData(true).OfType<GH_Mesh>().ToArray();
                Assert(meshes.Sum(m => m.Value.Faces.Count) == item.Faces, item.Fixture.Name + " lost preview faces");
                Assert(meshes.All(m => m.Value.TextureCoordinates.Count == m.Value.Vertices.Count), item.Fixture.Name + " lost UVs");
                Assert(preview.Message == item.Fixture.Mapping, item.Fixture.Name + " did not finish refinement");
                Console.WriteLine($"  {product} / {item.Fixture.Name}: {item.Fixture.Geometry.GetType().Name}; {preview.Message}; {item.Faces} triangles; {meshes.Length} atlas pages");
                preview.Hidden = false;
            }
            var archive = new GH_Archive(); Assert(archive.AppendObject(doc, "Definition"), "Gallery archive failed");
            string path = Path.Combine(Output, $"geometry-types-{product}.ghx");
            Assert(archive.WriteToFile(path, true, false), "Gallery write failed");
            Console.WriteLine("  Gallery: " + path);

        }
        finally { foreach (var fixture in fixtures) fixture.Dispose(); }
    }
    static void WriteMatchedPair()
    {
        using var doc = new GH_Document { Enabled = true };
        var settings = new SettingsParam { Name = "Pale timber board - physical millimetres", NickName = "Pale board / mm" };
        settings.SetPersistentData(new SettingsGoo(new TimberSettings {
            Product = TimberProduct.ThreeS, Seed = 17, Resolution = 1, EdgeResolution = 0.25, LamellaWidth = 500, KnotDensity = 0.25,
            EarlywoodA = Color.FromArgb(223, 213, 181), EarlywoodB = Color.FromArgb(234, 224, 198),
            Latewood = Color.FromArgb(194, 181, 151), Knot = Color.FromArgb(130, 110, 80) }));
        settings.CreateAttributes(); settings.Attributes.Pivot = new PointF(300, 80); doc.AddObject(settings, false);
        using var unbentGuide = Bow();
        double unbentLength = unbentGuide.GetLength();
        using var flat = new Box(Plane.WorldXY, new Interval(0, unbentLength), new Interval(0, 500), new Interval(800, 827)).ToBrep();
        Near(flat.GetBoundingBox(true).Diagonal.X, unbentLength, 0.001);
        using var extrusion = BowedExtrusion(); using var bent = extrusion.ToBrep(); using var guide = Bow();
        int index = 0;
        foreach (var geometry in new[] { flat, bent }) {
            var input = new Param_Geometry { Name = index == 0 ? $"Flat board - {unbentLength:0.##} x 500 x 27 mm" : "Same section - upward bow, 220 mm rise", Hidden = true };
            input.SetPersistentData(new GH_Brep(geometry)); input.CreateAttributes(); input.Attributes.Pivot = new PointF(80, 220 + index * 200); doc.AddObject(input, false);
            var preview = new PreviewComponent { NickName = index == 0 ? "Flat board" : "Upward-bowed board" };
            preview.CreateAttributes(); preview.Attributes.Pivot = new PointF(650, 220 + index * 200); doc.AddObject(preview, false);
            preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(settings);
            if (index == 1) {
                var curve = new Param_Curve { Name = "Longitudinal bend", Hidden = true }; curve.SetPersistentData(new GH_Curve(guide.DuplicateCurve()));
                curve.CreateAttributes(); curve.Attributes.Pivot = new PointF(300, 480); doc.AddObject(curve, false);
                preview.Params.Input[4].AddSource(curve);
                ((Param_Plane)preview.Params.Input[3]).SetPersistentData(new GH_Plane(Plane.WorldXY));
            }
            index++;
        }
        var archive = new GH_Archive(); Assert(archive.AppendObject(doc, "Definition"), "Matched pair archive failed");
        string path = Path.Combine(Output, "flat-and-upward-bowed.ghx");
        Assert(archive.WriteToFile(path, true, false), "Matched pair write failed");
        Console.WriteLine("  Matched pair: " + path);
    }

    static void WriteMaterials()
    {
        using var doc = new GH_Document { Enabled = true };
        using var lengthGuide = Bow(); double length = lengthGuide.GetLength();
        int row = 0;
        foreach (var product in TimberProducts.MenuOrder) {
            double depth = new TimberSettings { Product = product }.VirtualDepthMm;
            var material = new SettingsComponent(); material.CreateAttributes(); material.SelectProduct(product);
            material.SelectLook(TimberLook.CleanPale); material.Attributes.Pivot = new PointF(300, 100 + row * 380);
            doc.AddObject(material, false);
            using var flat = new Box(Plane.WorldXY, new Interval(0, length), new Interval(0, 500), new Interval(800, 800 + depth)).ToBrep();
            using var extrusion = BowedExtrusion(depth); using var bent = extrusion.ToBrep(); using var guide = Bow();
            int side = 0;
            foreach (var shape in new[] { flat, bent }) {
                var move = Transform.Translation((row % 2) * 2600, (row / 2) * 1400, 0);
                shape.Transform(move); if (side == 1) guide.Transform(move);
                var input = new Param_Geometry { Name = product.Label() + (side == 0 ? " flat" : " bowed"), Hidden = true };
                input.SetPersistentData(new GH_Brep(shape.DuplicateBrep())); input.CreateAttributes(); input.Attributes.Pivot = new PointF(40, 100 + row * 380 + side * 150); doc.AddObject(input, false);
                var preview = new PreviewComponent { NickName = product.Label() + (side == 0 ? " / Flat" : " / Bowed"), Hidden = false };
                preview.CreateAttributes(); preview.Attributes.Pivot = new PointF(700, 100 + row * 380 + side * 150); doc.AddObject(preview, false);
                preview.Params.Input[0].AddSource(input); preview.Params.Input[1].AddSource(material.Params.Output[0]);
                ((Param_Plane)preview.Params.Input[3]).SetPersistentData(new GH_Plane(Plane.WorldXY));
                if (side == 1) {
                    var curve = new Param_Curve { Name = product.Label() + " bend guide", Hidden = true };
                    curve.SetPersistentData(new GH_Curve(guide)); curve.CreateAttributes(); curve.Attributes.Pivot = new PointF(480, 290 + row * 380); doc.AddObject(curve, false);
                    preview.Params.Input[4].AddSource(curve);
                }
                side++;
            }
            row++;
        }
        var archive = new GH_Archive(); Assert(archive.AppendObject(doc, "Definition"), "Material gallery archive failed");
        // Stored geometry must outlive each row's temporary native Breps/curves.
        using (var reopened = new GH_Document { Enabled = true }) {
            var read = new GH_Archive(); Assert(read.Deserialize_Xml(archive.Serialize_Xml()) && read.ExtractObject(reopened, "Definition"), "Material gallery reopen failed");
            var geometry = reopened.Objects.OfType<Param_Geometry>().SelectMany(p => p.PersistentData.AllData(true)).OfType<GH_Brep>().ToArray();
            Assert(geometry.Length == 16 && geometry.All(g => g.Value != null && g.Value.IsValid), "Saved material gallery lost its geometry");
            var guides = reopened.Objects.OfType<Param_Curve>().SelectMany(p => p.PersistentData.AllData(true)).OfType<GH_Curve>().ToArray();
            Assert(guides.Length == 8 && guides.All(g => g.Value != null && g.Value.IsValid), "Saved material gallery lost its guides");
            foreach (var preview in reopened.Objects.OfType<PreviewComponent>()) preview.Hidden = true;
            reopened.NewSolution(false);
            foreach (var preview in reopened.Objects.OfType<PreviewComponent>()) {
                Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), "Reopened material preview failed: " + preview.NickName);
                Assert(preview.Params.Output[0].VolatileDataCount > 0 && preview.Params.Output[2].VolatileDataCount == 1, "Reopened material preview has no meshes/bounds: " + preview.NickName);
            }
        }
        Assert(archive.WriteToFile(Path.Combine(Output, "eight-materials.ghx"), true, false), "Material gallery write failed");
    }

}
