using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Grasshopper;
using Woodie.Rendering;
using Woodie.Stock;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class NaturalChecks
{
    static TimberSettings Log(int seed = 7, double variation = .5, double bark = 5) =>
        new() { Product = TimberProduct.Log, Seed = seed, Diameter = 200, Taper = 10, BarkThickness = bark, Variation = variation };
    static TimberSettings Bamboo(int seed = 7, double variation = .5) =>
        new() { Product = TimberProduct.Bamboo, Seed = seed, Diameter = 80, WallThickness = 7, NodeSpacing = 270, Taper = 5, Variation = variation };
    static NaturalFeatures Resolve(TimberSettings s, double length = 3000) => NaturalFeatures.Resolve(s, length, s.Diameter, s.Diameter);
    static double Luma(int c) => ((c >> 16 & 255) + (c >> 8 & 255) + (c & 255)) / 3.0;
    static double Red(int c) => c >> 16 & 255;
    static double Green(int c) => c >> 8 & 255;

    public static void Features()
    {
        var a = Resolve(Bamboo()); var b = Resolve(Bamboo()); var other = Resolve(Bamboo(8));
        Assert(a.Nodes.SequenceEqual(b.Nodes) && a.Ovality == b.Ovality, "Seed must identify one specimen");
        Assert(!a.Nodes.SequenceEqual(other.Nodes), "Different seeds should differ in node stations");
        // Variation 0: nominal taper and regular nodes whatever the seed.
        var nominal = Resolve(Bamboo(3, 0)); var nominal2 = Resolve(Bamboo(4, 0));
        Assert(nominal.Nodes.Count == 11 && nominal.Nodes.SequenceEqual(nominal2.Nodes), "Nominal bamboo should have eleven regular nodes in 3 m");
        for (int k = 0; k < nominal.Nodes.Count; k++) Near(nominal.Nodes[k], 270.0 * (k + 1), 1e-9);
        for (double x = 0; x <= 3000; x += 125) { Near(nominal.Outer(x), 40 - 5 * x / 2000, 1e-9); Near(nominal2.Outer(x), nominal.Outer(x), 1e-12); }
        Assert(nominal.Ovality == 0 && Resolve(Log(3, 0)).KnotCount == 0, "Variation 0 must have no ovality or knots");
        // Bounded variation.
        foreach (double v in new[] { .25, .5, 1 }) {
            var f = Resolve(Log(11, v));
            for (double x = 0; x <= 3000; x += 50) Assert(Math.Abs(f.Outer(x) / (100 - 5 * x / 1000.0) - 1) <= .05 * v + 1e-9, "Section variation exceeds its bound");
            Assert(Math.Abs(f.Ovality) <= .05 * v + 1e-12, "Ovality exceeds its bound");
        }
        // Geometry never consumes appearance draws: knot density changes appearance only.
        var knots = Log(5); var bare = Log(5); bare.KnotDensity = 0;
        var withKnots = Resolve(knots); var withoutKnots = Resolve(bare);
        Assert(withKnots.KnotCount > 0 && withoutKnots.KnotCount == 0 && withKnots.Ovality == withoutKnots.Ovality && withKnots.Outer(1234) == withoutKnots.Outer(1234), "Geometry and appearance streams are not separate");
        // Hollow internodes, solid diaphragms, annular ends.
        var culm = Resolve(Bamboo(3, 0)); double wallInner = culm.Inner(135);
        Assert(!culm.Solid(135, 0) && !culm.Solid(0, 0) && !culm.Solid(3000, 0), "Internodes and ends must be hollow");
        Assert(culm.Solid(270, 0) && culm.Solid(270 + culm.Diaphragm / 2 - .01, 0) && !culm.Solid(270 + culm.Diaphragm / 2 + .01, 0), "Diaphragm should span ±half its thickness");
        Assert(culm.Solid(135, wallInner + .1) && !culm.Solid(135, culm.Outer(135) + .1) && culm.Solid(270, culm.Outer(270) + culm.Ridge(270) - .01), "Wall and ridge extents");
        Assert(culm.Ridge(270) > 1 && culm.Ridge(135) == 0, "Nodes carry an external ridge");
        var profile = culm.Profile();
        Assert(profile.Count == 3 + 3 * culm.Nodes.Count + 1, "Bamboo profile should have rims, outer face, cavity walls and two diaphragm faces per node");
        Assert(Resolve(Log(1)).Profile().Count == 3, "Log profile is two end discs and one stem face");
        Reject(() => Resolve(Log(1, 2)), "variation");
        Reject(() => NaturalFeatures.Resolve(new TimberSettings { Product = TimberProduct.Log, Diameter = 100, Taper = 100 }, 3000, 100, 100), "Taper");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Diameter = 80, WallThickness = 7, NodeSpacing = 20 }), "100 nodes");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Diameter = 80, WallThickness = 40 }), "hollow");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Log, Diameter = 100, BarkThickness = 60 }, 1000), "Bark");
    }

    public static void Appearance()
    {
        foreach (var settings in new[] { Log(), Log(7, .5, 0), Bamboo() })
        {
            var f = Resolve(settings, 2000);
            var model = StockDefinition.FromNatural(settings, 2000, 2 * f.Outer(0) * (1 + Math.Abs(f.Ovality)), 2 * f.Outer(0) * (1 + Math.Abs(f.Ovality)));
            var again = StockDefinition.FromNatural(settings, 2000, 2 * f.Outer(0) * (1 + Math.Abs(f.Ovality)), 2 * f.Outer(0) * (1 + Math.Abs(f.Ovality)));
            for (int i = 0; i < 50; i++) { double x = 10 + i * 39, y = 20 + i * 3, z = 30 + i * 2; Assert(model.Sample(x, y, z) == again.Sample(x, y, z) && model.Sample(x, y, z, 1, .3, 0) == again.Sample(x, y, z, 1, .3, 0), "Natural sampling is nondeterministic"); }
        }
        // Log: bark is the outer region, timber sits inside, fresh end cuts show timber.
        var log = Log(); var lf = Resolve(log, 2000); double w = 2 * lf.Outer(0) * (1 + Math.Abs(lf.Ovality));
        var stock = StockDefinition.FromNatural(log, 2000, w, w); var peeled = StockDefinition.FromNatural(Log(7, .5, 0), 2000, w, w);
        double R = lf.Outer(1000), sy = 1 + lf.Ovality;
        int bark = stock.Sample(1000, w / 2 + (R - 2) * sy, w / 2), wood = stock.Sample(1000, w / 2 + (R - 30) * sy, w / 2), bare = peeled.Sample(1000, w / 2 + (R - 2) * sy, w / 2);
        Assert(Luma(bark) < 110 && Luma(wood) > 120 && Luma(bare) > 120, $"Bark/timber contrast missing: {Luma(bark):0} {Luma(wood):0} {Luma(bare):0}");
        Assert(Luma(stock.Sample(1000, w / 2 + 10, w / 2, 1, 0, 0)) > 120 && Luma(stock.Sample(0, w / 2 + 10, w / 2)) > 120, "Fresh end faces must reveal timber");
        // Variation 0: appearance depends on nominal anatomy only, not the seed.
        var a = StockDefinition.FromNatural(Log(1, 0), 2000, w, w); var b = StockDefinition.FromNatural(Log(2, 0), 2000, w, w);
        var c1 = StockDefinition.FromNatural(Log(1), 2000, w, w); var c2 = StockDefinition.FromNatural(Log(2), 2000, w, w);
        bool differs = false;
        for (int i = 0; i < 400; i++) {
            double x = 7 + i * 4.9, y = w / 2 + (i % 17 - 8) * 5, z = w / 2 + (i % 13 - 6) * 5;
            Assert(a.Sample(x, y, z) == b.Sample(x, y, z), "Variation 0 must not depend on the seed");
            differs |= c1.Sample(x, y, z) != c2.Sample(x, y, z);
        }
        Assert(differs, "Variation above 0 should make specimens differ");
        // Bamboo: skin vs wall vs node band vs diaphragm.
        var culm = Bamboo(3, 0); var bf = Resolve(culm, 3000); var cs = StockDefinition.FromNatural(culm, 3000, 80, 80);
        double ro = bf.Outer(135), ri = bf.Inner(135);
        int skin = cs.Sample(135, 40 + ro - .05, 40), inner = cs.Sample(135, 40 + ri + .3, 40), node = cs.Sample(270, 40 + (ri + ro) / 2, 40), mid = cs.Sample(135, 40 + (ri + ro) / 2, 40);
        Assert(Green(skin) > Red(skin) && Red(inner) > Green(inner), "Bamboo skin should be green-olive and wall interior warm");
        Assert(node != mid, "Node fibre appearance should differ from an internode wall");
        Assert(Luma(cs.Sample(270, 40, 40)) > 150 && Luma(cs.Sample(135, 40 + ri - 1, 40)) > 150, "Diaphragm and lining should be pale");
        var definition = StockDefinition.FromTimber(culm, 3000, 80, 80);
        Assert(definition.Sample(135, 40 + ro, 40) == cs.Sample(135, 40 + ro, 40) && definition.Construction.Pieces.Single().Specimen.Features != null && definition.Construction.Pieces[0].Specimen.Seed == 3, "FromTimber must dispatch natural products");
        // Unset diameter wraps the cross-section bounds (legacy Product=Log on a user Brep).
        var wrapped = StockDefinition.FromTimber(new TimberSettings { Product = TimberProduct.Log, Variation = 0, BarkThickness = 5 }, 1000, 300, 220);
        Near(wrapped.Construction.Pieces[0].Specimen.Features.Diameter, 300);
        // Natural products still resolve through the shared layup/palette paths.
        foreach (var product in new[] { TimberProduct.Log, TimberProduct.Bamboo }) {
            var s = new TimberSettings { Product = product };
            Assert(TimberLayups.Resolve(s, s.VirtualDepthMm, 300).Count == 1 && s.Palette() != null && s.VirtualDepthMm == (product == TimberProduct.Log ? 200 : 80), "Natural product defaults");
            new TimberPanel(product, 1800, 500, s.VirtualDepthMm, 1).Col(10, 10, 10);
        }
        Assert(!TimberProducts.MenuOrder.Contains(TimberProduct.Log) && !TimberProducts.MenuOrder.Contains(TimberProduct.Bamboo) && (int)TimberProduct.Log == 8 && (int)TimberProduct.Bamboo == 9, "Product ids");
        Assert(Log().Key != Log(7, .4).Key && Log().Key != Log(7, .5, 0).Key && new TimberSettings().Key == new TimberSettings { Variation = .1 }.Key, "Natural settings key");
    }

    // Rhino-hosted geometry, component, preview and cut.
    public static void Geometry()
    {
        foreach (var settings in new[] { Log(), Bamboo(), Log(2, 0, 0), Bamboo(4, 0) })
        {
            var f = Resolve(settings, 2400); var axis = new Line(new Point3d(100, 50, 20), new Point3d(100 + 2400 * .6, 50 + 2400 * .8, 20));
            using var brep = NaturalGeometry.Create(f, axis, 1, .001);
            Assert(brep.IsValidWithLog(out string log) && brep.IsSolid, $"{settings.Product} is not a valid solid: faces {brep.Faces.Count} solid {brep.IsSolid} {brep.SolidOrientation} {log}");
            var frame = NaturalGeometry.Frame(axis); var box = brep.GetBoundingBox(frame);
            Near(box.Max.X - box.Min.X, 2400, .01); Near(box.Min.X, 0, .01);
            double volume = 0, step = 1;
            for (double x = step / 2; x < 2400; x += step) {
                double ro = f.Outer(x) + f.Ridge(x), ri = f.Bamboo ? f.Inner(x) : 0;
                if (f.Bamboo) { f.NearNode(x, out double d); if (d <= f.Diaphragm / 2) ri = 0; }
                volume += Math.PI * (ro * ro - ri * ri) * (1 + f.Ovality) * (1 - f.Ovality) * step;
            }
            var tri = Mesh.CreateFromBrep(brep, new MeshingParameters(MeshingParameters.Default) { RefineGrid = true, GridAngle = Math.PI / 180, MinimumEdgeLength = 0 });
            Near(brep.GetVolume() / volume, 1, .02);
            if (f.Bamboo) Assert(brep.GetVolume() < .7 * Math.PI * 40 * 40 * 2400, "Bamboo must be hollow");
            var cutPlane = new Plane(axis.PointAt(.5), axis.Direction);
            using var cut = TimberCut.Create(brep, cutPlane, false, .001);
            Assert(cut.Geometry.Count == 1 && cut.Geometry[0] is Brep piece && piece.IsSolid && piece.GetVolume() > .3 * brep.GetVolume() && piece.GetVolume() < .7 * brep.GetVolume(), $"Cut natural stock {settings.Product}: {cut.Geometry.Count} pieces, {string.Join(",", cut.Geometry.OfType<Brep>().Select(p => p.IsSolid + " " + p.GetVolume().ToString("0")))} of {brep.GetVolume():0}");
        }
    }

    public static void Component()
    {
        IsolatedHost.LoadWoodie(typeof(NaturalStockComponent).Assembly.Location); GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled = true };
        var natural = new NaturalStockComponent(); natural.CreateAttributes(); doc.AddObject(natural, false);
        var bamboo = new NaturalStockComponent(); bamboo.CreateAttributes(); doc.AddObject(bamboo, false);
        ((Param_Line)natural.Params.Input[0]).SetPersistentData(new Line(Point3d.Origin, new Point3d(2400, 0, 0)));
        ((Param_Line)bamboo.Params.Input[0]).SetPersistentData(new Line(new Point3d(0, 500, 0), new Point3d(0, 500, 3000)));
        ((Param_String)bamboo.Params.Input[1]).PersistentData.Clear(); ((Param_String)bamboo.Params.Input[1]).SetPersistentData("Bamboo");
        doc.NewSolution(false);
        foreach (var c in new[] { natural, bamboo }) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), c.Name + ": " + string.Join(";", c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        var logStock = natural.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        var culmStock = bamboo.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        Assert(logStock.Settings.Product == TimberProduct.Log && logStock.Settings.Diameter == 200 && logStock.Settings.BarkThickness == 5, "Log defaults");
        Assert(culmStock.Settings.Product == TimberProduct.Bamboo && culmStock.Settings.Diameter == 80 && culmStock.Settings.WallThickness == 7 && culmStock.Settings.NodeSpacing == 270, "Bamboo defaults");
        Near(culmStock.Definition.LengthMm, 3000, .01); Assert(culmStock.BasePlane.HasValue && culmStock.Fibre.HasValue && culmStock.Geometry is Brep { IsSolid: true }, "Natural stock mapping");
        // Archive keeps the natural definition.
        var archive = new GH_Archive(); Assert(archive.AppendObject(new StockGoo(culmStock), "Stock"), "Natural stock write");
        var copy = new StockGoo(); Assert(archive.ExtractObject(copy, "Stock") && copy.Value.Settings.NodeSpacing == 270 && copy.Value.Settings.Product == TimberProduct.Bamboo, "Natural stock archive");
        for (int i = 0; i < 30; i++) { double x = 10 + i * 97, y = 30 + i, z = 35; Assert(copy.Value.Definition.Sample(x, y, z) == culmStock.Definition.Sample(x, y, z), "Natural archive changed samples"); }
        // Hollow culms must stay under the preview triangle cap with the natural mesh limits.
        using var culmPart = PreparedPart.Create(culmStock.Geometry, culmStock.Settings.Duplicate(), 1, .001, culmStock.BasePlane, culmStock.Fibre, null, culmStock.OriginalGeometry);
        Assert(culmPart.Mesh.Faces.Count > 0 && culmPart.Mesh.Faces.Count <= 200000, $"Bamboo culm preview mesh has {culmPart.Mesh.Faces.Count} triangles");
        // Vertical half-cut through the culm must keep fixed stock mapping for Timber Preview.
        var mid = culmStock.Geometry.GetBoundingBox(true).Center;
        using var half = TimberCut.Create(culmStock.Geometry, new Plane(mid, Vector3d.XAxis), false, .001);
        Assert(half.Geometry.Count == 1 && half.Outlines.Count > 0, "Bamboo vertical section should keep one capped piece with outlines");
        using var cutStock = culmStock.WithGeometry(half.Geometry[0]);
        using var cutPart = PreparedPart.Create(cutStock.Geometry, cutStock.Settings.Duplicate(), 1, .001, cutStock.BasePlane, cutStock.Fibre, null, cutStock.OriginalGeometry);
        Assert(cutPart.Mesh.Faces.Count > 0 && cutPart.Mesh.Faces.Count <= 200000, $"Bamboo section preview mesh has {cutPart.Mesh.Faces.Count} triangles");
        // Stock Model preview path builds the natural definition and rasterises it.
        var stock = logStock; var settings = stock.Settings.Duplicate(); settings.Resolution = 8; settings.EdgeResolution = 8;
        using var part = PreparedPart.Create(stock.Geometry, settings, 1, .001, stock.BasePlane, stock.Fibre, null, stock.OriginalGeometry);
        Near(part.LengthMm, 2400, .05);
        var pages = TextureAtlas.Prepare(part, settings, .25, Path.Combine(Output, "natural-atlas"));
        try {
            Assert(pages.Count > 0 && pages[0].Panel.Value is StockDefinition d && d.Construction.Pieces[0].Specimen.Features != null, "Preview did not build the natural stock definition");
            var pixels = TextureAtlas.Rasterize(pages[0], CancellationToken.None);
            Assert(pixels.Select(p => p & 0xffffff).Distinct().Count() > 20 && pixels.Any(p => Luma(p) < 110) && pixels.Any(p => Luma(p) > 120), "Log texture lacks bark and timber");
        } finally { foreach (var page in pages) page.Dispose(); }
        // Legacy Settings on a user Brep wrap the stock's bounds.
        using var user = new Box(Plane.WorldXY, new Interval(0, 1000), new Interval(0, 200), new Interval(0, 200)).ToBrep();
        using var legacy = PreparedPart.Create(user, new TimberSettings { Product = TimberProduct.Log, Variation = 0 }, 1, .001, Plane.WorldXY);
        var legacyPages = TextureAtlas.Prepare(legacy, new TimberSettings { Product = TimberProduct.Log, Variation = 0 }, .1, Path.Combine(Output, "natural-atlas"));
        foreach (var page in legacyPages) page.Dispose();
    }
}
