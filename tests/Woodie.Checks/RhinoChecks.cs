using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Grasshopper;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class RhinoChecks
{
    public static void Run(bool examples, bool integration)
    {
        Check("Rotated plate, reversed and perpendicular fibre", Alignment);
        Check("Beam and inferred mesh orientation", MeshAlignment);
        Check("Surface, extrusion, trimmed Brep and invalid input", GeometryTypes);
        Check("Millimetre/metre mapping equivalence", Units);
        Check("Unit mismatch produces actionable diagnostics", UnitInputChecks.Diagnostic);
        Check("Explicit source units scale geometry in mm, m and inch documents", UnitInputChecks.Explicit);
        Check("Source-unit guides, planes, invalid units and legacy input archives", UnitInputChecks.GuidesAndArchives);
        Check("Geometry larger than 30 metres is accepted", LargeGeometry);
        Check("Custom buildup construction, thickness, serialization and cache", BuildupCutChecks.Layers);
        Check("Capped stock cuts preserve the material field and units", BuildupCutChecks.Cutting);
        if (integration) Check("Layer, buildup, cut and preview component workflow", BuildupCutChecks.Components);
        if(integration){Check("Native textured baking, reuse, persistence and cancellation",NativeBakingChecks.Core);Check("Native geometry UV layouts preserve source types and topology",NativeBakingChecks.Geometry);Check("Native stock-aware baking in mm, metres and inches",NativeBakingChecks.UnitsAndStock);Check("Native baking snapshot lifecycle and Show",NativeBakingChecks.Component);Check("Native stepped knot section at final example resolution",NativeBakingChecks.KnotSection);}
        Check("Curved surface uses physical arc lengths", SurfaceBend);
        Check("Guide bending, closed guide and cusp rejection", GuideBend);
        Check("Material-space cut orientation on straight and guided surfaces", MappedCutOrientation);
        Check("Rotated metre panel atlas has no false exterior glue", ExteriorGlueChecks.Atlas);
        Check("Atlas triangle coverage and gutters", AtlasCoverage);
        Check("Atlas winding, UVs, bounded pages, cache and PNG", Atlas);
        Check("Settings archive round trip", Serialization);
        Check("Stock model archive round trip", StockChecks.ArchiveRoundTrip);
        Check("Natural Log and Bamboo geometry: solids, volume, cuts", NaturalChecks.Geometry);
        if (integration) Check("Natural Stock component, archive, preview atlas and legacy wrap", NaturalChecks.Component);
        Check("Component GUIDs and item matching interface", Interface);
        Check("All timber presets solve and retain selection in archives", Presets);
        if (integration) Check("Material dropdowns, precedence, undo, migration and serialization", MaterialChecks.Interface);
        if (integration) Check("Grasshopper solve, broadcast, lifecycle and definition reopen", SolveAndReopen);
        if (examples) Check("Generate and reopen the release example", Examples);
        Check("Shipped trimmed surfaces retain fine texture resolution", ExampleDefinitions.SurfaceResolution);
        if (integration) Check("Shipped release example reopens and solves", () => ExampleDefinitions.Verify(Path.GetFullPath(Path.Combine(Output, "../../examples/Timber-preview.ghx"))));
        if (integration) Check("Combined cutting, knot and buildup example reopens and moves in mm, m and inches", () => TimberExamples.Verify(Path.GetFullPath(Path.Combine(Output, "../../examples"))));
    }

    static Brep Plate() => new Box(Plane.WorldXY, new Interval(0, 1200), new Interval(0, 600), new Interval(0, 120)).ToBrep();
    static PreparedPart Part(GeometryBase g, double scale = 1, Plane? plane = null, Vector3d? fibre = null, Curve guide = null) =>
        PreparedPart.Create(g, new TimberSettings(), scale, 0.001 / scale, plane, fibre, guide);

    static void Alignment()
    {
        using var b = Plate();
        var rotation = Transform.Rotation(0.63, new Vector3d(1, 2, 3), new Point3d(300, 100, 50)); b.Transform(rotation);
        var frame = Plane.WorldXY; frame.Transform(rotation);
        using var p = Part(b);
        Near(Math.Abs(p.Alignment.Frame.XAxis * frame.XAxis), 1);
        Near(p.Alignment.Box.X.Length, 1200, 0.01); Near(p.Alignment.Box.Y.Length, 600, 0.01); Near(p.Alignment.Box.Z.Length, 120, 0.01);
        using var reversed = Part(b, plane: frame, fibre: -frame.XAxis);
        Near(reversed.Alignment.Frame.XAxis * frame.XAxis, -1);
        Reject(() => Part(b, plane: frame, fibre: frame.ZAxis), "perpendicular");
        using var overrideFibre = Part(b, plane: frame, fibre: frame.YAxis);
        Near(overrideFibre.Alignment.Box.X.Length, 600, 0.01);
        foreach (var v in p.Mesh.Vertices)
        {
            p.Alignment.Frame.RemapToPlaneSpace((Point3d)v, out Point3d local);
            Assert(local.X >= -0.001 && local.Y >= -0.001 && local.Z >= -0.001 && local.X <= 1200.001 && local.Y <= 600.001 && local.Z <= 120.001,
                "Bounds do not contain full geometry within meshing precision");
        }
    }
    static void MeshAlignment()
    {
        using var beam = new Box(Plane.WorldXY, new Interval(0, 3200), new Interval(0, 100), new Interval(0, 240)).ToBrep();
        using var p = Part(beam); Near(p.LengthMm, 3200, 0.01);
        using var mesh = Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 1200, 600, 120), 1, 1, 1);
        var r = Transform.Rotation(0.8, new Vector3d(1, 3, 2), Point3d.Origin); mesh.Transform(r);
        var originalUvs = mesh.TextureCoordinates.ToArray();
        var x = Vector3d.XAxis; x.Transform(r);
        using var a = Part(mesh); using var b = Part(mesh);
        Near(Math.Abs(a.Alignment.Frame.XAxis * x), 1, 1e-4);
        Near(a.Alignment.Frame.XAxis * b.Alignment.Frame.XAxis, 1);
        Assert(mesh.TextureCoordinates.SequenceEqual(originalUvs), "Input mesh was modified");
    }
    static void GeometryTypes()
    {
        using var surface = new PlaneSurface(Plane.WorldXY, new Interval(0, 800), new Interval(0, 300));
        using var s = Part(surface); Near(s.DepthMm, 200); Assert(s.Remarks.Any(r => r.Contains("virtual")), "Virtual thickness missing");
        using var open = new Mesh(); open.Vertices.Add(0, 0, 0); open.Vertices.Add(800, 0, 0); open.Vertices.Add(800, 300, 0); open.Vertices.Add(0, 300, 0); open.Faces.AddFace(0, 1, 2, 3);
        using var m = Part(open); Near(m.DepthMm, 200); Near(m.Mesh.GetBoundingBox(false).Max.Z, 0);
        using var profile = new Rectangle3d(Plane.WorldXY, 600, 200).ToNurbsCurve();
        using var extrusion = Extrusion.Create(profile, 120, true);
        using var e = Part(extrusion); Assert(e.Mesh.Faces.Count > 0, "Extrusion not meshed");
        using var circle = new Circle(Plane.WorldXY, 200).ToNurbsCurve();
        using var disk = Brep.CreatePlanarBreps(circle, 0.001)[0]; using var d = Part(disk);
        Assert(d.Mesh.Vertices.All(v => new Point3d(v).DistanceTo(Point3d.Origin) <= 200.01), "Trim was lost");
        Reject(() => Part(new Mesh()), "invalid");
        Reject(() => Part(new LineCurve(Point3d.Origin, new Point3d(100, 0, 0))), "Supported");
    }
    static void Units()
    {
        using var mm = Plate(); using var metres = mm.DuplicateBrep(); metres.Transform(Transform.Scale(Point3d.Origin, 0.001));
        using var a = Part(mm); using var b = Part(metres, 1000);
        Near(a.LengthMm, b.LengthMm, 0.01); Near(a.DepthMm, b.DepthMm, 0.01);
        Near(a.WidthMm, b.WidthMm, 0.01);
    }

    static void LargeGeometry()
    {
        using var beam = new Box(Plane.WorldXY, new Interval(0, 60000), new Interval(0, 240), new Interval(0, 120)).ToBrep();
        using var part = Part(beam);
        Near(part.LengthMm, 60000, 0.01);
        var pages = TextureAtlas.Prepare(part, new TimberSettings { Product = TimberProduct.GL });
        try
        {
            Assert(pages.Count > 0 && pages.Count <= TextureAtlas.MaxPages, "Large geometry must retain bounded texture atlases");
            Assert(pages.All(p => p.Width <= TextureAtlas.Side && p.Height <= TextureAtlas.Side), "Large geometry exceeded texture dimensions");
        }
        finally { foreach (var page in pages) page.Dispose(); }
    }

    internal static Surface BentSurface()
    {
        var arc = new Arc(new Point3d(0, 0, 0), new Point3d(500, 0, 180), new Point3d(1000, 0, 0));
        using var curve = arc.ToNurbsCurve();
        return Surface.CreateExtrusion(curve, new Vector3d(0, 240, 0));
    }
    static void SurfaceBend()
    {
        using var s = BentSurface();
        var reference = Plane.WorldXY;
        var map = new SurfaceMap(s, reference, true);
        double u = s.Domain(0).Mid, v0 = s.Domain(1).Min, v1 = s.Domain(1).Max;
        Point3d a = map.Map(s.PointAt(u, v0)), b = map.Map(s.PointAt(u, v1));
        using var iso = s.IsoCurve(1, u);
        Near(Math.Abs(b.X - a.X), iso.GetLength(), 0.01);
        s.SetDomain(1, new Interval(20, 900));
        var reparam = new SurfaceMap(s, reference, true);
        Near(Math.Abs(reparam.Map(s.PointAt(u, 900)).X - reparam.Map(s.PointAt(u, 20)).X), iso.GetLength(), 0.01);
        using var part = Part(s); Assert(part.Mapping == "Bent surface", "Surface did not bend"); Near(part.DepthMm, 200);
    }
    internal static Curve Guide() => new Arc(new Point3d(0, 0, 0), new Point3d(500, 0, 180), new Point3d(1000, 0, 0)).ToNurbsCurve();
    static void GuideBend()
    {
        using var curve = Guide(); var map = new GuideMap(curve, Plane.WorldXY, 0.001);
        var a = map.Map(curve.PointAtStart); var b = map.Map(curve.PointAtEnd);
        Near(a.X, 0); Near(b.X, curve.GetLength(), 0.01); Near(a.Y, 0); Near(a.Z, 0);
        using var surface = BentSurface(); using var part = Part(surface, guide: curve);
        Assert(part.Mapping == "Guide curve", "Guide does not take precedence");
        using var closed = new Circle(100).ToNurbsCurve(); Reject(() => new GuideMap(closed, Plane.WorldXY, 0.001), "open");
        using var kink = new PolylineCurve(new[] { Point3d.Origin, new Point3d(100, 0, 0), new Point3d(100, 100, 0) });
        Reject(() => new GuideMap(kink, Plane.WorldXY, 0.001), "kink");
    }

    static void MappedCutOrientation()
    {
        using var guide = Guide(); using var bent = BentSurface();
        using var flat = new PlaneSurface(Plane.WorldXY, new Interval(0, 1000), new Interval(0, 240));
        using var straight = Part(flat); using var guided = Part(bent, guide: guide);
        foreach (var part in new[] { straight, guided })
        {
            var settings = new TimberSettings { Resolution = 20, EdgeResolution = 20 };
            var pages = TextureAtlas.Prepare(part, settings, .15);
            try
            {
                var first = pages.Select(p => TextureAtlas.Rasterize(p, CancellationToken.None)).ToArray();
                foreach (var page in pages) foreach (var tile in page.Tiles)
                {
                    var normal = tile.CutNormal;
                    Assert(normal.SquareLength > 0, "Mapped surface has no cut normal");
                    Near(normal.X * normal.X / normal.SquareLength, 0, 1e-8);
                    Near(normal.Y * normal.Y / normal.SquareLength, 0, 1e-8);
                }
                // Deliberately contradict geometric orientation with lighting normals.
                // A surface following the guide still represents longitudinal grain.
                for (int i = 0; i < part.Mesh.Normals.Count; i++) part.Mesh.Normals[i] = new Vector3f(1, 0, 0);
                var relit = TextureAtlas.Prepare(part, settings, .15);
                try
                {
                    for (int i = 0; i < relit.Count; i++)
                        Assert(first[i].SequenceEqual(TextureAtlas.Rasterize(relit[i], CancellationToken.None)), "Lighting normals changed material appearance");
                }
                finally { foreach (var page in relit) page.Dispose(); }
            }
            finally { foreach (var page in pages) page.Dispose(); }
        }
    }
    static void Atlas()
    {
        using var b = Plate(); using var part = Part(b);
        string cache = Path.Combine(Output, "atlas-cache");
        var pages = TextureAtlas.Prepare(part, new TimberSettings { Resolution = 10, EdgeResolution = 5 }, 0.15, cache);
        try
        {
            Assert(pages.Count > 0 && pages.Count <= TextureAtlas.MaxPages, "Atlas page bounds");
            Assert(pages.Sum(p => p.Mesh.Faces.Count) == part.Mesh.Faces.Count, "Faces were lost");
            foreach (var p in pages)
            {
                Assert(p.Width <= TextureAtlas.Side && p.Height <= TextureAtlas.Side, "Oversized page");
                Assert(p.Mesh.TextureCoordinates.Count == p.Mesh.Vertices.Count, "Missing UVs");
                Assert(p.Mesh.TextureCoordinates.All(t => t.X > 0 && t.X < 1 && t.Y > 0 && t.Y < 1), "UV out of range");
                for (int i = 0; i < p.Tiles.Count; i++)
                {
                    var tile = p.Tiles[i]; var source = part.Mesh.Faces[tile.Face];
                    var ids = new[] { source.A, source.B, source.C }; var actual = p.Mesh.Faces[i];
                    Near(p.Mesh.Vertices.Point3dAt(actual.A).DistanceTo(part.Mesh.Vertices.Point3dAt(ids[tile.Rotation])), 0);
                }
            }
            TextureAtlas.BakeMissing(pages, CancellationToken.None);
            var before = pages.Select(p => File.GetLastWriteTimeUtc(p.TexturePath)).ToArray();
            TextureAtlas.BakeMissing(pages, CancellationToken.None);
            Assert(pages.Select(p => File.GetLastWriteTimeUtc(p.TexturePath)).SequenceEqual(before), "Cache hit rewrote PNGs");
            var seed = TextureAtlas.Prepare(part, new TimberSettings { Seed = 1 }, 0.15, cache);
            Assert(seed[0].TexturePath != pages[0].TexturePath, "Settings were omitted from cache key");
            foreach (var p in seed) p.Dispose();
            Reject(() => TextureAtlas.Prepare(part, new TimberSettings { KnotDensity = 100, LamellaWidth = 1 }), "too many");
        }
        finally { foreach (var p in pages) p.Dispose(); }
    }
    static void Serialization()
    {
        var original = new SettingsGoo(new TimberSettings { Product = TimberProduct.KertoQ, Seed = 41, EarlywoodA = Color.FromArgb(120, 40, 20), KnotDensity = 0, Resolution = 1 });
        var archive = new GH_Archive(); Assert(archive.AppendObject(original, "settings"), "Settings write failed");
        string xml = archive.Serialize_Xml(); var restoredArchive = new GH_Archive(); Assert(restoredArchive.Deserialize_Xml(xml), "Invalid XML");
        var restored = new SettingsGoo(); Assert(restoredArchive.ExtractObject(restored, "settings"), "Settings read failed");
        Assert(original.Value.Key == restored.Value.Key, "Settings changed after serialization");
        Assert(double.IsNaN(restored.Value.EdgeResolution), "Optional default changed");
    }
    static void Interface()
    {
        var component = new PreviewComponent();
        Assert(component.ComponentGuid == new Guid("b3bcf727-0468-4fb8-8ee5-55fb8084ffb6"), "Preview GUID");
        Assert(new SettingsComponent().ComponentGuid == new Guid("7a74fc91-6ed6-447b-adb6-2e2fcd425383"), "Settings GUID");
        Assert(new SettingsParam().ComponentGuid == new Guid("06b27042-911f-4614-ac44-378a78736284"), "Parameter GUID");
        Assert(new PresetsComponent().ComponentGuid == new Guid("f17c33b1-9302-435d-a0e8-782a36f0c5e7"), "Presets GUID");
        Assert(component.Params.Input.All(p => p.Access == GH_ParamAccess.item), "Inputs must use item matching");
        Assert(component.Params.Output.Count == 4, "Expected four outputs");
        foreach (var item in new GH_Component[] { component, new SettingsComponent(), new PresetsComponent() })
            Assert(item.Category == "Display" && item.SubCategory == "Woodie", "Woodie components must appear in Display / Woodie");
        Assert(component.Description.Contains("3528223.3530081") && new SettingsComponent().Description.Contains("Larsson"), "Research credit missing from component descriptions");
        Assert(component.Icon_24x24.Width == 24 && new SettingsComponent().Icon_24x24.Height == 24, "Missing Phosphor icons");
        Assert(new PresetsComponent().Icon_24x24.Width == 24, "Missing Phosphor preset icon");
        var icons = new IGH_DocumentObject[] { component, new SettingsComponent(), new LayerComponent(), new BuildupComponent(), new CutComponent(), new PresetsComponent(), new SettingsParam(), new LayerParam() };
        var fingerprints = icons.Select(o => string.Join(",", Enumerable.Range(0,24*24).Select(i => o.Icon_24x24.GetPixel(i%24,i/24).ToArgb()))).ToArray();
        Assert(fingerprints.Distinct().Count()==icons.Length,"Every Woodie component and parameter needs a unique Phosphor icon");
    }

    static void Presets()
    {
        GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled = true };
        var component = new PresetsComponent(); component.CreateAttributes(); doc.AddObject(component, false);
        var input = (Param_Integer)component.Params.Input[0];
        foreach (TimberProduct product in Enum.GetValues<TimberProduct>())
        {
            input.PersistentData.Clear(); input.SetPersistentData(new GH_Integer((int)product));
            component.ExpireSolution(false); doc.NewSolution(false);
            var settings = component.Params.Output[0].VolatileData.AllData(true).OfType<SettingsGoo>().Single();
            Assert(settings.Value.Key == new TimberSettings { Product = product }.Key, "Preset changed the product defaults");
            Assert(component.Params.Output[1].VolatileData.AllData(true).Single().ToString() == product.ToString(), "Preset product label differs");
        }
        var archive = new GH_Archive(); Assert(archive.AppendObject(component, "preset"), "Preset archive write failed");
        var restored = new PresetsComponent();
        Assert(archive.ExtractObject(restored, "preset"), "Preset archive read failed");
        Assert(((GH_Integer)((Param_Integer)restored.Params.Input[0]).PersistentData.AllData(true).Single()).Value == (int)Enum.GetValues<TimberProduct>().Last(),
            "Preset selection did not survive save/reopen");
        input.PersistentData.Clear(); input.SetPersistentData(new GH_Integer(99));
        component.ExpireSolution(false); doc.NewSolution(false);
        // Grasshopper keeps a null placeholder in an item output when SolveInstance returns with an error.
        Assert((component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0 || input.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0) && !component.Params.Output[0].VolatileData.AllData(true).OfType<SettingsGoo>().Any(g => g.Value != null),
            "Invalid preset should report an error and clear its output; component=" + string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error)) +
            "; input=" + string.Join("; ", input.RuntimeMessages(GH_RuntimeMessageLevel.Error)) + "; output=" + component.Params.Output[0].VolatileDataCount + "; data=" + string.Join(",", input.VolatileData.AllData(true)));
    }
    static void SolveAndReopen()
    {
        // Register only Woodie with the normal component server so GUID-based archive restore can find it.
        string gha = Path.GetFullPath(Path.Combine(Output, "../../src/Woodie/bin/Release/net8.0/Woodie.gha"));
        IsolatedHost.LoadWoodie(gha);
        using var doc = new GH_Document();
        GH_Document.EnableSolutions = true;
        doc.Enabled = true;
        var geometry = new Param_Geometry { Hidden = true }; geometry.CreateAttributes();
        using var b = Plate(); using var b2 = Plate(); b2.Transform(Transform.Translation(0, 1000, 0));
        geometry.SetPersistentData((IEnumerable<IGH_GeometricGoo>)new IGH_GeometricGoo[] { new GH_Brep(b), new GH_Brep(b2) });
        var settings = new SettingsParam(); settings.CreateAttributes();
        settings.SetPersistentData(new SettingsGoo(new TimberSettings { Product = TimberProduct.KVH, Resolution = 10, EdgeResolution = 10 }));
        var preview = new PreviewComponent { Hidden = true }; preview.CreateAttributes();
        doc.AddObject(geometry, false); doc.AddObject(settings, false); doc.AddObject(preview, false);
        preview.Params.Input[0].AddSource(geometry); preview.Params.Input[1].AddSource(settings);
        doc.NewSolution(false);
        Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), string.Join("; ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        Assert(preview.Params.Output[0].VolatileDataCount >= 2, "Mesh output missing for item-matched inputs; state=" + doc.SolutionState +
            "; geometry=" + geometry.VolatileDataCount + "; settings=" + settings.VolatileDataCount + "; warnings=" + string.Join("; ", preview.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));
        Assert(preview.Params.Output[2].VolatileDataCount == 2, "Expected one box per geometry");
        Assert(preview.Params.Output[1].VolatileData.AllData(true).All(g => g.ToString() == "KVH"), "Shared settings did not broadcast");
        settings.PersistentData.Clear();
        settings.SetPersistentData((IEnumerable<SettingsGoo>)new[] {
            new SettingsGoo(new TimberSettings { Product = TimberProduct.KVH, Resolution = 10, EdgeResolution = 10 }),
            new SettingsGoo(new TimberSettings { Product = TimberProduct.GL, Resolution = 10, EdgeResolution = 10 }) });
        settings.ExpireSolution(false); doc.NewSolution(false);
        Assert(preview.Params.Output[1].VolatileData.AllData(true).Select(g => g.ToString()).Distinct().OrderBy(p => p).SequenceEqual(new[] { "GL", "KVH" }),
            "Per-item settings did not match geometry items");
        var archive = new GH_Archive(); Assert(archive.AppendObject(doc, "Definition"), "Definition save failed");
        var read = new GH_Archive(); Assert(read.Deserialize_Xml(archive.Serialize_Xml()), "Definition XML failed");
        using var restored = new GH_Document(); Assert(read.ExtractObject(restored, "Definition"), "Definition reopen failed");
        var restoredPreview = restored.Objects.OfType<PreviewComponent>().Single();
        restored.Enabled = true;
        restoredPreview.ExpireSolution(false);
        restored.NewSolution(false);
        Assert(restoredPreview.Params.Output[2].VolatileDataCount == 2, "Reopened definition did not solve; boxes=" + restoredPreview.Params.Output[2].VolatileDataCount +
            "; sources=" + restoredPreview.Params.Input[0].Sources.Count + "; errors=" + string.Join("; ", restoredPreview.RuntimeMessages(GH_RuntimeMessageLevel.Error)) +
            "; warnings=" + string.Join("; ", restoredPreview.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));
        ((Param_Boolean)preview.Params.Input[5]).PersistentData.Clear();
        ((Param_Boolean)preview.Params.Input[5]).SetPersistentData(new GH_Boolean(false));
        preview.ExpireSolution(false); doc.NewSolution(false);
        Assert(preview.Params.Output[0].VolatileDataCount == 0 && !preview.ClippingBox.IsValid, "Show=false retained stale output; meshes=" +
            preview.Params.Output[0].VolatileDataCount + "; valid bounds=" + preview.ClippingBox.IsValid);
        ((Param_Boolean)preview.Params.Input[5]).PersistentData.Clear();
        ((Param_Boolean)preview.Params.Input[5]).SetPersistentData(new GH_Boolean(true));
        preview.ExpireSolution(false); doc.NewSolution(false);
        Assert(preview.Params.Output[0].VolatileDataCount >= 2, "Show=true did not restore output");
        preview.ClearData(); Assert(!preview.ClippingBox.IsValid, "ClearData retained clipping bounds");
        doc.RemoveObject(preview, false);
    }
    static void Examples() => ExampleDefinitions.Write(Path.GetFullPath(Path.Combine(Output, "../../examples")));
}
