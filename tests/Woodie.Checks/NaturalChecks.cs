using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Woodie.Appearance;
using Woodie.Baking;
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
        // Variation 0: nominal taper and deterministic basal RIN–RCIL nodes (not equal spacing), seed-independent.
        var nominal = Resolve(Bamboo(3, 0)); var nominal2 = Resolve(Bamboo(4, 0));
        Assert(nominal.Nodes.Count >= 8 && nominal.Nodes.Count <= 12 && nominal.Nodes.SequenceEqual(nominal2.Nodes), "Nominal bamboo should share a deterministic basal node set in 3 m");
        var gaps = InternodeLengths(nominal);
        Assert(gaps[0] < gaps[^1] && gaps[^1] > gaps[gaps.Length / 2] * 0.9, "Basal stock window must lengthen internodes away from the base (RIN–RCIL)");
        Assert(gaps.Max() / gaps.Min() > 1.2, "Internode lengths must vary along the culm");
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
        var culm = Resolve(Bamboo(3, 0)); double n0 = culm.Nodes[0], midGap = (culm.Nodes[0] + (culm.Nodes.Count > 1 ? culm.Nodes[1] : 3000)) / 2;
        double wallInner = culm.Inner(midGap);
        Assert(!culm.Solid(midGap, 0) && !culm.Solid(0, 0) && !culm.Solid(3000, 0), "Internodes and ends must be hollow");
        Assert(culm.Solid(n0, 0) && culm.Solid(n0 + culm.Diaphragm / 2 - .01, 0) && !culm.Solid(n0 + culm.Diaphragm / 2 + .01, 0), "Diaphragm should span ±half its thickness");
        Assert(culm.Solid(midGap, wallInner + .1) && !culm.Solid(midGap, culm.Outer(midGap) + .1) && culm.Solid(n0, culm.Outer(n0) + culm.Ridge(n0) - .01), "Wall and ridge extents");
        Assert(culm.Ridge(n0) > 1 && culm.Ridge(midGap) == 0, "Nodes carry an external ridge");
        var profile = culm.Profile();
        Assert(profile.Count == 3 + 3 * culm.Nodes.Count + 1, "Bamboo profile should have rims, outer face, cavity walls and two diaphragm faces per node");
        Assert(Resolve(Log(1)).Profile().Count == 3, "Log profile is two end discs and one stem face");
        Reject(() => Resolve(Log(1, 2)), "variation");
        Reject(() => NaturalFeatures.Resolve(new TimberSettings { Product = TimberProduct.Log, Diameter = 100, Taper = 100 }, 3000, 100, 100), "Taper");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Diameter = 80, WallThickness = 7, NodeSpacing = 20 }), "100 nodes");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Diameter = 80, WallThickness = 40 }), "hollow");
        Reject(() => Resolve(new TimberSettings { Product = TimberProduct.Log, Diameter = 100, BarkThickness = 60 }, 1000), "Bark");
        // Scaffolding length: Ø80 keeps the hollow guard (Ø75 tip wall fails at 8 m). Absurd length hits taper.
        var scaffold = new TimberSettings { Product = TimberProduct.Bamboo, Species = BambooSpeciesId.Moso, Diameter = 80, Taper = 5, WallThickness = 7, NodeSpacing = 270, Variation = .8 };
        Assert(NaturalFeatures.Resolve(scaffold, 8000, 80, 80).Length == 8000, "8 m Moso with trade-range Ø80 must resolve");
        try { NaturalFeatures.Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Species = BambooSpeciesId.Moso, Diameter = 75, Taper = 5, WallThickness = 7, NodeSpacing = 270, Variation = .8 }, 6_000_000, 75, 75); Assert(false, "6 km taper must throw"); }
        catch (ArgumentException ex) { Assert(ex.Message.Contains("Taper leaves less than 20%"), "Taper guard message: " + ex.Message); }
        try { NaturalFeatures.Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Diameter = 75, WallThickness = 7, NodeSpacing = 270, Species = BambooSpeciesId.Moso }, 6_000_000, 75, 75); Assert(false, "Unset taper must hit the node cap"); }
        catch (ArgumentException ex) { Assert(ex.Message.Contains("more than 100 nodes"), "Unset taper path: " + ex.Message); }
    }

    static double[] InternodeLengths(NaturalFeatures f)
    {
        var xs = new List<double> { 0 }; xs.AddRange(f.Nodes); xs.Add(f.Length);
        var gaps = new double[xs.Count - 1];
        for (int i = 0; i < gaps.Length; i++) gaps[i] = xs[i + 1] - xs[i];
        return gaps;
    }

    /// <summary>Baseline preview mesh face counts for scaffolding-length Moso (docs/development/bamboo-preview-performance.md).</summary>
    public static void ProbeMeshBudget()
    {
        IsolatedHost.Prepare();
        using var core = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/safemode", "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        using var testDoc = Rhino.RhinoDoc.Create(null);
        testDoc.ModelUnitSystem = Rhino.UnitSystem.Millimeters;
        IsolatedHost.LoadBuiltIns();

        // Defaults match Bamboo Stock (Moso midpoint Ø75, Variation 0.8).
        var settings = new TimberSettings {
            Product = TimberProduct.Bamboo, Species = BambooSpeciesId.Moso, Seed = 0,
            Diameter = 75, WallThickness = 7, NodeSpacing = 270, Taper = 5, Variation = .8
        };
        settings.Validate();
        double angle = settings.GridAngle; // 5°
        void Count(string label, TimberSettings s, double lengthMm, double unitToMm, double axisDocument)
        {
            try
            {
                var features = NaturalFeatures.Resolve(s, lengthMm, s.Diameter, s.Diameter);
                double mmToDocument = 1 / unitToMm, tolerance = .001;
                double maxEdge = Math.Max(10 / unitToMm, tolerance * 20);
                var axis = new Line(Point3d.Origin, new Point3d(0, 0, axisDocument));
                using var brep = NaturalGeometry.Create(features, axis, mmToDocument, tolerance);
                var parameters = new MeshingParameters(MeshingParameters.Default)
                { RefineGrid = false, GridAngle = angle, MinimumEdgeLength = 0, MaximumEdgeLength = maxEdge };
                Mesh[] pieces = Mesh.CreateFromBrep(brep, parameters);
                Assert(pieces != null && pieces.Length > 0, label + ": meshing failed");
                using var mesh = new Mesh();
                foreach (var piece in pieces) { mesh.Append(piece); piece.Dispose(); }
                mesh.Faces.ConvertQuadsToTriangles();
                mesh.Faces.CullDegenerateFaces();
                mesh.Compact();
                Console.WriteLine($"{label}: faces={mesh.Faces.Count} maxEdge={maxEdge:0.###} runs={features.Profile().Count} nodes={features.Nodes.Count} Ø={s.Diameter}");
            }
            catch (ArgumentException ex) { Console.WriteLine($"{label}: FAIL {ex.Message}"); }
        }
        Count("6m mm-doc", settings, 6000, 1, 6000);
        Count("6m m-doc", settings, 6000, 1000, 6);
        // Ø75 + taper 5 mm/m fails the hollow guard near 8 m; trade-range Ø80 still leaves a wall.
        var longPole = settings.Duplicate(); longPole.Diameter = 80; longPole.Validate();
        Count("8m mm-doc Ø80", longPole, 8000, 1, 8000);
        Count("8m m-doc Ø80", longPole, 8000, 1000, 8);

        void CountPrepared(string label, TimberSettings s, double lengthMm, double unitToMm, double axisDocument)
        {
            try
            {
                var features = NaturalFeatures.Resolve(s, lengthMm, s.Diameter, s.Diameter);
                var axis = new Line(Point3d.Origin, new Point3d(0, 0, axisDocument));
                using var brep = NaturalGeometry.Create(features, axis, 1 / unitToMm, .001);
                var frame = NaturalGeometry.Frame(axis);
                using var part = PreparedPart.Create(brep, s.Duplicate(), unitToMm, .001, frame, frame.XAxis);
                Console.WriteLine($"{label}: preparedFaces={part.Mesh.Faces.Count} lengthMm={part.LengthMm:0.#} remarks={string.Join(";", part.Remarks)}");
            }
            catch (ArgumentException ex) { Console.WriteLine($"{label}: prepared FAIL {ex.Message}"); }
        }
        CountPrepared("6m prepared mm", settings, 6000, 1, 6000);
        CountPrepared("8m prepared mm Ø80", longPole, 8000, 1, 8000);
        CountPrepared("8m prepared m Ø80", longPole, 8000, 1000, 8);
    }

    public static void ProbeBundles()
    {
        var culm = Bamboo(3, 0); var bf = Resolve(culm, 3000); var cs = StockDefinition.FromNatural(culm, 3000, 80, 80);
        double gap = (bf.Nodes[0] + bf.Nodes[1]) / 2, ro = bf.Outer(gap), ri = bf.Inner(gap), rr = ri + 0.72 * (ro - ri);
        Console.WriteLine($"ri={ri:0.##} ro={ro:0.##} rr={rr:0.##}");
        double min = 999, max = -999; int jumps = 0; double prev = -1;
        for (int i = 0; i < 96; i++) {
            double ang = i * (2 * Math.PI / 96);
            double L = Luma(cs.Sample(gap, 40 + rr * Math.Cos(ang), 40 + rr * Math.Sin(ang), 1, 0, 0));
            min = Math.Min(min, L); max = Math.Max(max, L);
            if (i > 0 && Math.Abs(L - prev) > 22) jumps++;
            prev = L;
        }
        Console.WriteLine($"luma {min:0}-{max:0} jumps={jumps}");
        for (int k = 0; k < 8; k++) {
            double t = k / 7.0, r = ri + t * (ro - ri);
            Console.WriteLine($"  t={t:0.00} L={Luma(cs.Sample(gap, 40 + r, 40, 1, 0, 0)):0}");
        }
        // Axial identity: persistent tubes keep end-grain angular luma correlated along x; cellular fields do not.
        double Corr(double x0, double x1)
        {
            const double t = 0.72;
            double r0 = bf.Inner(x0) + t * (bf.Outer(x0) - bf.Inner(x0));
            double r1 = bf.Inner(x1) + t * (bf.Outer(x1) - bf.Inner(x1));
            double m0 = 0, m1 = 0; var a = new double[48]; var b = new double[48];
            for (int i = 0; i < 48; i++) {
                double ang = i * (2 * Math.PI / 48);
                a[i] = Luma(cs.Sample(x0, 40 + r0 * Math.Cos(ang), 40 + r0 * Math.Sin(ang), 1, 0, 0));
                b[i] = Luma(cs.Sample(x1, 40 + r1 * Math.Cos(ang), 40 + r1 * Math.Sin(ang), 1, 0, 0));
                m0 += a[i]; m1 += b[i];
            }
            m0 /= 48; m1 /= 48; double num = 0, d0 = 0, d1 = 0;
            for (int i = 0; i < 48; i++) { double da = a[i] - m0, db = b[i] - m1; num += da * db; d0 += da * da; d1 += db * db; }
            return num / Math.Sqrt(d0 * d1);
        }
        Console.WriteLine($"volumetric={bf.HasVolumetricBundles}");
        Console.WriteLine($"endgrain angular corr dx=1/5/20/50 mm: {Corr(gap, gap + 1):0.000} {Corr(gap, gap + 5):0.000} {Corr(gap, gap + 20):0.000} {Corr(gap, gap + 50):0.000}");
        Console.WriteLine($"TryFibreDirection mid-wall: {cs.TryFibreDirection(gap, 40 + rr, 40, out var fx, out var fy, out var fz)} ({fx:0.###},{fy:0.###},{fz:0.###})");
        Console.WriteLine($"WallAt base/mid/tip: {bf.WallAt(0):0.##} {bf.WallAt(1500):0.##} {bf.WallAt(3000):0.##}");
        var wobble = Bamboo(3, 1); var wf = Resolve(wobble, 3000);
        Console.WriteLine($"WallAt Variation=1 base/mid/tip: {wf.WallAt(0):0.##} {wf.WallAt(1500):0.##} {wf.WallAt(3000):0.##}");
        ReportSampleThroughput();
    }

    /// <summary>Bamboo Sample must stay in the same cost class as softwood Col (atlas raster does millions of hits).</summary>
    public static void SampleThroughput()
    {
        var (bambooMs, woodMs, n) = MeasureSampleThroughput();
        double ratio = bambooMs / Math.Max(woodMs, 1e-9);
        Console.WriteLine($"Sample throughput: {n:0} hits · bamboo {bambooMs:0.0} ms · wood {woodMs:0.0} ms · ratio {ratio:0.00}×");
        Assert(ratio < 2.5, $"Bamboo Sample is too slow vs wood ({ratio:0.00}×; keep under 2.5×)");
    }

    static (double BambooMs, double WoodMs, int N) MeasureSampleThroughput()
    {
        const int n = 200_000;
        var culm = Bamboo(3, 0.5); var bf = Resolve(culm, 3000);
        var bamboo = StockDefinition.FromNatural(culm, 3000, 80, 80);
        var wood = new TimberPanel(TimberProduct.SolidTimber, 3000, 80, 80, 3, 0.4, new TimberSettings { Product = TimberProduct.SolidTimber, Seed = 3 }.Palette());
        double gap = (bf.Nodes[0] + bf.Nodes[1]) / 2, ri = bf.Inner(gap), ro = bf.Outer(gap), rr = ri + 0.72 * (ro - ri);
        // Warmup
        for (int i = 0; i < 2000; i++) { bamboo.Sample(gap, 40 + rr, 40, 1, 0, 0); wood.Col(gap, 40, 40, 1, 0, 0); }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < n; i++) {
            double ang = i * 0.017, x = gap + (i % 50) * 0.2;
            bamboo.Sample(x, 40 + rr * Math.Cos(ang), 40 + rr * Math.Sin(ang), 1, 0, 0);
        }
        double bambooMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        for (int i = 0; i < n; i++) {
            double y = 10 + (i % 60), z = 10 + (i / 60 % 60);
            wood.Col(20 + i * 0.01, y, z, 1, 0, 0);
        }
        return (bambooMs, sw.Elapsed.TotalMilliseconds, n);
    }

    static void ReportSampleThroughput()
    {
        var (bambooMs, woodMs, n) = MeasureSampleThroughput();
        Console.WriteLine($"Sample throughput: {n:0} hits · bamboo {bambooMs:0.0} ms · wood {woodMs:0.0} ms · ratio {bambooMs / Math.Max(woodMs, 1e-9):0.00}×");
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
        double n0 = bf.Nodes[0], gap = (bf.Nodes[0] + (bf.Nodes.Count > 1 ? bf.Nodes[1] : 3000)) / 2;
        double ro = bf.Outer(gap), ri = bf.Inner(gap);
        int skin = cs.Sample(gap, 40 + ro - .05, 40), inner = cs.Sample(gap, 40 + ri + .3, 40), node = cs.Sample(n0, 40 + (ri + ro) / 2, 40), mid = cs.Sample(gap, 40 + (ri + ro) / 2, 40);
        Assert(Red(skin) > Green(skin) && Red(inner) > Green(inner), "Cured bamboo skin and wall interior should read warm, not living green");
        Assert(node != mid, "Node fibre appearance should differ from an internode wall");
        Assert(Luma(cs.Sample(n0, 40, 40)) > 150 && Luma(cs.Sample(gap, 40 + ri - 1, 40)) > 150, "Diaphragm and lining should be pale");
        // End-cut wall: outer third darker/more varied than inner third (vascular packing toward cortex).
        double lumaInner = 0, lumaOuter = 0; int nSamp = 0;
        for (int i = 0; i < 48; i++) {
            double ang = i * (2 * Math.PI / 48), ca = Math.Cos(ang), sa = Math.Sin(ang);
            lumaInner += Luma(cs.Sample(gap, 40 + (ri + 0.2 * (ro - ri)) * ca, 40 + (ri + 0.2 * (ro - ri)) * sa, 1, 0, 0));
            lumaOuter += Luma(cs.Sample(gap, 40 + (ri + 0.88 * (ro - ri)) * ca, 40 + (ri + 0.88 * (ro - ri)) * sa, 1, 0, 0));
            nSamp++;
        }
        Assert(lumaOuter / nSamp < lumaInner / nSamp - 6, $"Outer wall should read darker than lining on end cuts ({lumaOuter / nSamp:0} vs {lumaInner / nSamp:0})");
        Assert(Luma(cs.Sample(n0, 40 + ro - .05, 40, 0, 1, 0)) + 12 < Luma(skin) || cs.Sample(n0, 40 + ro - .05, 40, 0, 1, 0) != skin, "Node ring should darken the exterior skin");
        // Discrete bundles: along an outer arc, luma should jump between pale parenchyma and dark sheath.
        int jumps = 0; double rr = ri + 0.72 * (ro - ri);
        double prev = Luma(cs.Sample(gap, 40 + rr, 40, 1, 0, 0));
        for (int i = 1; i < 96; i++) {
            double ang = i * (2 * Math.PI / 96);
            double L = Luma(cs.Sample(gap, 40 + rr * Math.Cos(ang), 40 + rr * Math.Sin(ang), 1, 0, 0));
            if (Math.Abs(L - prev) > 22) jumps++; prev = L;
        }
        Assert(jumps >= 10, $"Outer wall should show discrete bundle contrast (jumps {jumps})");
        // Mid-wall should not be empty parenchyma: at least some darker samples than the lining.
        double midDark = 999;
        for (int i = 0; i < 48; i++) {
            double ang = i * (2 * Math.PI / 48), rm = ri + 0.55 * (ro - ri);
            midDark = Math.Min(midDark, Luma(cs.Sample(gap, 40 + rm * Math.Cos(ang), 40 + rm * Math.Sin(ang), 1, 0, 0)));
        }
        Assert(midDark < lumaInner / nSamp - 15, $"Mid-wall should carry some vascular bundles ({midDark:0} vs lining {lumaInner / nSamp:0})");
        // Larsson-style continuity: same wall depth t (not fixed r) keeps end-grain angular structure along the axis.
        Assert(bf.HasVolumetricBundles, "Culm must use the axial-persistent vascular field");
        double AxialCorr(double x0, double x1)
        {
            double t = 0.72;
            double r0 = bf.Inner(x0) + t * (bf.Outer(x0) - bf.Inner(x0));
            double r1 = bf.Inner(x1) + t * (bf.Outer(x1) - bf.Inner(x1));
            double m0 = 0, m1 = 0; var a = new double[48]; var b = new double[48];
            for (int i = 0; i < 48; i++) {
                double ang = i * (2 * Math.PI / 48);
                a[i] = Luma(cs.Sample(x0, 40 + r0 * Math.Cos(ang), 40 + r0 * Math.Sin(ang), 1, 0, 0));
                b[i] = Luma(cs.Sample(x1, 40 + r1 * Math.Cos(ang), 40 + r1 * Math.Sin(ang), 1, 0, 0));
                m0 += a[i]; m1 += b[i];
            }
            m0 /= 48; m1 /= 48; double num = 0, d0 = 0, d1 = 0;
            for (int i = 0; i < 48; i++) { double da = a[i] - m0, db = b[i] - m1; num += da * db; d0 += da * da; d1 += db * db; }
            return num / Math.Sqrt(d0 * d1);
        }
        double corr1 = AxialCorr(gap, gap + 1), corr5 = AxialCorr(gap, gap + 5);
        Assert(corr1 > 0.92 && corr5 > 0.85, $"Axial end-grain identity too weak (corr 1 mm={corr1:0.000}, 5 mm={corr5:0.000})");
        Assert(cs.TryFibreDirection(gap, 40 + rr, 40, out var fdx, out var fdy, out var fdz) && fdx > 0.7, $"Bamboo fibre should follow the culm axis ({fdx:0.###},{fdy:0.###},{fdz:0.###})");
        Assert(BambooSpecies.For(BambooSpeciesId.Moso).FreeFibreStrands == 0 && BambooSpecies.For(BambooSpeciesId.Guadua).FreeFibreStrands == 1 && BambooSpecies.For(BambooSpeciesId.Asper).FreeFibreStrands == 2, "Grosser Type I/III/IV free-fibre counts");
        var asper = Bamboo(3, 0); asper.Species = BambooSpeciesId.Asper; asper.Validate();
        var af = Resolve(asper, 3000);
        Assert(af.FreeFibreStrands == 2 && bf.FreeFibreStrands == 0, "Species free-fibre strands must reach NaturalFeatures");
        var definition = StockDefinition.FromTimber(culm, 3000, 80, 80);
        Assert(definition.Sample(135, 40 + ro, 40) == cs.Sample(135, 40 + ro, 40) && definition.Construction.Pieces.Single().Specimen.Features != null && definition.Construction.Pieces[0].Specimen.Seed == 3, "FromTimber must dispatch natural products");
        // Unset diameter wraps the cross-section bounds (legacy Product=Log on a user Brep).
        var wrapped = StockDefinition.FromTimber(new TimberSettings { Product = TimberProduct.Log, Variation = 0, BarkThickness = 5 }, 1000, 300, 220);
        Near(wrapped.Construction.Pieces[0].Specimen.Features.Diameter, 300);
        // Natural products still resolve through the shared layup/palette paths.
        foreach (var product in new[] { TimberProduct.Log, TimberProduct.Bamboo }) {
            var s = new TimberSettings { Product = product };
            Assert(TimberLayups.Resolve(s, s.VirtualDepthMm, 300).Count == 1 && s.Palette() != null && s.VirtualDepthMm == (product == TimberProduct.Log ? 200 : BambooSpecies.Default.Diameter), "Natural product defaults");
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
        // Profile coarsening must keep sway: Brep radius matches Outer+Ridge between stations (within 1 mm).
        AssertOuterRadius(Resolve(Log(7, .5), 1000), 1000);
        AssertOuterRadius(Resolve(new TimberSettings { Product = TimberProduct.Bamboo, Species = BambooSpeciesId.Moso, Seed = 7, Diameter = 75, WallThickness = 7, NodeSpacing = 270, Taper = 5, Variation = .8 }, 1500), 1500);
    }

    static void AssertOuterRadius(NaturalFeatures f, double lengthMm)
    {
        var axis = new Line(Point3d.Origin, new Point3d(lengthMm, 0, 0));
        using var brep = NaturalGeometry.Create(f, axis, 1, .001);
        var frame = NaturalGeometry.Frame(axis);
        var stations = new SortedSet<double>();
        foreach (var run in f.Profile()) foreach (var p in run) stations.Add(p.X);
        var xs = stations.ToList();
        for (int i = 0; i < xs.Count - 1; i++)
        {
            if (xs[i + 1] - xs[i] < 2) continue;
            double x = 0.5 * (xs[i] + xs[i + 1]);
            double expect = f.Outer(x) + f.Ridge(x);
            // Radial line in +Y so ClosestPoint cannot slide along the axis to a fatter station.
            using var ray = new LineCurve(frame.PointAt(x, 0, 0), frame.PointAt(x, expect * (1 + Math.Abs(f.Ovality)) * 3, 0));
            Assert(Intersection.CurveBrep(ray, brep, .01, out _, out Point3d[] hits) && hits.Length > 0, $"Radial hit failed at x={x:0.#}");
            double Radius(Point3d p) { frame.RemapToPlaneSpace(p, out Point3d local); double ry = local.Y / (1 + f.Ovality), rz = local.Z / (1 - f.Ovality); return Math.Sqrt(ry * ry + rz * rz); }
            double r = hits.Max(Radius);
            Assert(Math.Abs(r - expect) <= 1, $"{(f.Bamboo ? "Bamboo" : "Log")}-radius at x={x:0.#}: got {r:0.###} expect {expect:0.###}");
        }
    }

    public static void Component()
    {
        IsolatedHost.LoadWoodie(typeof(LogStockComponent).Assembly.Location); GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled = true };
        var log = new LogStockComponent(); log.CreateAttributes(); doc.AddObject(log, false);
        var bamboo = new BambooStockComponent(); bamboo.CreateAttributes(); doc.AddObject(bamboo, false);
        ((Param_Line)log.Params.Input[0]).SetPersistentData(new Line(Point3d.Origin, new Point3d(2400, 0, 0)));
        ((Param_Line)bamboo.Params.Input[0]).SetPersistentData(new Line(new Point3d(0, 500, 0), new Point3d(0, 500, 3000)));
        doc.NewSolution(false);
        foreach (var c in new GH_Component[] { log, bamboo }) Assert(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), c.Name + ": " + string.Join(";", c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        var logStock = log.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        var culmStock = bamboo.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        Assert(log.ComponentGuid == new Guid("5d7f3c1e-8a42-4b6f-9e0d-1c3a7b9f2e64"), "Log Stock keeps Natural Stock GUID");
        Assert(bamboo.ComponentGuid == new Guid("d65cfaa6-137e-4547-91b5-33e5960ea8ca"), "Bamboo Stock GUID");
        Assert(bamboo.Attributes is BambooStockAttributes && bamboo.PanelExpanded && bamboo.SelectedSpecies == BambooSpeciesId.Moso && !bamboo.SpeciesControlled && !bamboo.Advanced, "Bamboo species panel defaults");
        Assert(bamboo.Params.Input.Count == 4 && bamboo.Params.Input[3] is Param_Interval, "Bamboo Stock starts with Line, Species, Settings and Diameter Domain");
        Assert(logStock.Settings.Product == TimberProduct.Log && logStock.Settings.Diameter == 200 && logStock.Settings.BarkThickness == 5, "Log defaults");
        Assert(culmStock.Settings.Product == TimberProduct.Bamboo && culmStock.Settings.Species == BambooSpeciesId.Moso && culmStock.Settings.Diameter == 75 && culmStock.Settings.WallThickness == 7 && culmStock.Settings.NodeSpacing == 270 && culmStock.Settings.Variation == .8, "Bamboo defaults");
        Near(culmStock.Definition.LengthMm, 3000, .01); Assert(culmStock.BasePlane.HasValue && culmStock.Fibre.HasValue && culmStock.Geometry is Brep { IsSolid: true }, "Bamboo stock mapping");
        Assert(BambooSpecies.All.Length == 3 && !BambooSpecies.TryParse("nigra", out _), "Library is construction species only");
        Assert(BambooSpecies.TryParse("Guadua bamboo", out var phrase) && phrase.Id == BambooSpeciesId.Guadua, "Species phrase parse");
        Assert(BambooSpecies.TryParse("moso", out var mosoAlias) && mosoAlias.Id == BambooSpeciesId.Moso
            && BambooSpecies.TryParse("Asper", out var asperName) && asperName.Id == BambooSpeciesId.Asper, "Species alias and name parse");
        // Panel selection without a Species wire.
        bamboo.SelectSpecies(BambooSpeciesId.Asper); doc.NewSolution(false);
        Assert(bamboo.SelectedSpecies == BambooSpeciesId.Asper && bamboo.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value.Settings.Diameter == 140, "Panel Asper selection");
        // Stored Species text drives Solve; panel stays free so SelectSpecies can clear it.
        var guadua = new BambooStockComponent(); guadua.CreateAttributes(); doc.AddObject(guadua, false);
        ((Param_Line)guadua.Params.Input[0]).SetPersistentData(new Line(new Point3d(0, 1000, 0), new Point3d(0, 1000, 2000)));
        ((Param_String)guadua.Params.Input[1]).PersistentData.Clear(); ((Param_String)guadua.Params.Input[1]).SetPersistentData("Guadua");
        doc.NewSolution(false);
        Assert(!guadua.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), "Guadua Bamboo Stock failed");
        Assert(!guadua.SpeciesControlled && guadua.SelectedSpecies == BambooSpeciesId.Guadua, "Stored Species text selects Guadua without locking the panel");
        var guaduaStock = guadua.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        Assert(guaduaStock.Settings.Species == BambooSpeciesId.Guadua && guaduaStock.Settings.Diameter == 126.5 && guaduaStock.Settings.WallThickness == 12 && guaduaStock.Settings.NodeSpacing == 310, "Guadua defaults");
        guadua.SelectSpecies(BambooSpeciesId.Moso); doc.NewSolution(false);
        Assert(guadua.SelectedSpecies == BambooSpeciesId.Moso && ((Param_String)guadua.Params.Input[1]).PersistentDataCount == 0
            && guadua.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value.Settings.Diameter == 75, "Panel clears stored Species and applies Moso range");
        // Species archive round trip.
        var speciesArchive = new GH_Archive(); Assert(speciesArchive.AppendObject(bamboo, "Bamboo"), "Bamboo component write");
        var restoredBamboo = new BambooStockComponent(); Assert(speciesArchive.ExtractObject(restoredBamboo, "Bamboo") && restoredBamboo.SelectedSpecies == BambooSpeciesId.Asper, "Bamboo species archive");
        var mosoSkin = Resolve(culmStock.Settings).Skin; var guaduaSkin = Resolve(guaduaStock.Settings).Skin;
        Assert(mosoSkin[1] < mosoSkin[0] && guaduaSkin[1] < guaduaSkin[0], "Cured skins must read warm, not living green");
        Assert(mosoSkin[0] != guaduaSkin[0] || mosoSkin[1] != guaduaSkin[1], "Species skins should differ");
        var archive = new GH_Archive(); Assert(archive.AppendObject(new StockGoo(culmStock), "Stock"), "Bamboo stock write");
        var copy = new StockGoo(); Assert(archive.ExtractObject(copy, "Stock") && copy.Value.Settings.NodeSpacing == 270 && copy.Value.Settings.Product == TimberProduct.Bamboo, "Bamboo stock archive");
        for (int i = 0; i < 30; i++) { double x = 10 + i * 97, y = 30 + i, z = 35; Assert(copy.Value.Definition.Sample(x, y, z) == culmStock.Definition.Sample(x, y, z), "Bamboo archive changed samples"); }
        // Preview mesh budget at 5° grid: use a 1.5 m Moso specimen, not the 3 m Asper panel pick.
        var preview = new BambooStockComponent(); preview.CreateAttributes(); doc.AddObject(preview, false);
        ((Param_Line)preview.Params.Input[0]).SetPersistentData(new Line(new Point3d(0, 1500, 0), new Point3d(0, 1500, 1500)));
        doc.NewSolution(false);
        Assert(!preview.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), "Preview Bamboo Stock failed");
        var previewStock = preview.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
        using var culmPart = PreparedPart.Create(previewStock.Geometry, previewStock.Settings.Duplicate(), 1, .001, previewStock.BasePlane, previewStock.Fibre, null, previewStock.OriginalGeometry);
        Assert(culmPart.Mesh.Faces.Count > 8000 && culmPart.Mesh.Faces.Count <= 200000, $"Bamboo culm preview mesh has {culmPart.Mesh.Faces.Count} triangles");
        using var draftPart = PreparedPart.Create(previewStock.Geometry, previewStock.Settings.Duplicate(), 1, .001, previewStock.BasePlane, previewStock.Fibre, null, previewStock.OriginalGeometry, PreviewLod.Draft);
        Assert(draftPart.Mesh.Faces.Count > 0 && draftPart.Mesh.Faces.Count < culmPart.Mesh.Faces.Count / 2,
            $"Draft tube ({draftPart.Mesh.Faces.Count}) should be under half of Full ({culmPart.Mesh.Faces.Count})");
        Assert(draftPart.Remarks.Any(r => r.Contains("draft tube")), "Draft preview should remark draft tube");
        // Geometry-only: longest AABB axis becomes the culm frame (not the short world-X edge).
        using var noFrame = PreparedPart.Create(previewStock.Geometry, previewStock.Settings.Duplicate(), 1, .001);
        Assert(noFrame.Remarks.Any(r => r.Contains("tube mesh")), "Geometry-only long bamboo should still use the parametric tube");
        var mid = previewStock.Geometry.GetBoundingBox(true).Center;
        using var half = TimberCut.Create(previewStock.Geometry, new Plane(mid, Vector3d.XAxis), false, .001);
        Assert(half.Geometry.Count == 1 && half.Outlines.Count > 0, "Bamboo vertical section should keep one capped piece with outlines");
        using var cutStock = previewStock.WithGeometry(half.Geometry[0]);
        using var cutPart = PreparedPart.Create(cutStock.Geometry, cutStock.Settings.Duplicate(), 1, .001, cutStock.BasePlane, cutStock.Fibre, null, cutStock.OriginalGeometry);
        Assert(cutPart.Mesh.Faces.Count > 0 && cutPart.Mesh.Faces.Count <= 200000, $"Bamboo section preview mesh has {cutPart.Mesh.Faces.Count} triangles");
        Assert(!cutPart.Remarks.Any(r => r.Contains("tube mesh")), "Cut bamboo with stock must not use the full-length parametric tube");
        // Draft coarsens MakeMesh on non-tube paths (engineered board).
        using var board = Brep.CreateFromBox(new BoundingBox(Point3d.Origin, new Point3d(2000, 100, 40)));
        using var boardFull = PreparedPart.Create(board, new TimberSettings { Product = TimberProduct.CLT }, 1, .001, Plane.WorldXY);
        using var boardDraft = PreparedPart.Create(board, new TimberSettings { Product = TimberProduct.CLT }, 1, .001, Plane.WorldXY, previewLod: PreviewLod.Draft);
        Assert(boardDraft.Mesh.Faces.Count <= boardFull.Mesh.Faces.Count, $"Draft board ({boardDraft.Mesh.Faces.Count}) should not exceed Full ({boardFull.Mesh.Faces.Count})");
        Assert(boardDraft.Remarks.Any(r => r.Contains("coarser mesh")), "Draft MakeMesh should remark coarser mesh");

        // Scaffolding preview budget (docs/development/bamboo-preview-performance.md). Moso needs Ø80 at 8 m (default Ø75 fails the hollow guard).
        void AssertScaffoldPreview(int seed, BambooSpeciesId species, string label)
        {
            var longPole = new BambooStockComponent(); longPole.CreateAttributes(); doc.AddObject(longPole, false);
            ((Param_Line)longPole.Params.Input[0]).SetPersistentData(new Line(new Point3d(0, 2000 + seed * 10 + (int)species, 0), new Point3d(0, 2000 + seed * 10 + (int)species, 8000)));
            if (species != BambooSpeciesId.Moso) longPole.SelectSpecies(species);
            // Moso default Ø75 and Asper mid Ø140 fail the hollow guard at 8 m; pin diameters that still resolve.
            if (species == BambooSpeciesId.Moso) ((Param_Interval)longPole.Params.Input[3]).SetPersistentData(new Interval(80, 80));
            else if (species == BambooSpeciesId.Asper) ((Param_Interval)longPole.Params.Input[3]).SetPersistentData(new Interval(180, 180));
            ((SettingsParam)longPole.Params.Input[2]).SetPersistentData(new SettingsGoo(new TimberSettings { Seed = seed }));
            doc.NewSolution(false);
            Assert(!longPole.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(), label + ": " + string.Join(";", longPole.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
            var stock8 = longPole.Params.Output[0].VolatileData.AllData(true).OfType<StockGoo>().Single().Value;
            using var part8 = PreparedPart.Create(stock8.Geometry, stock8.Settings.Duplicate(), 1, .001, stock8.BasePlane, stock8.Fibre, null, stock8.OriginalGeometry);
            Console.WriteLine($"{label}: faces={part8.Mesh.Faces.Count} Ø={stock8.Settings.Diameter} seed={seed}");
            Assert(part8.Mesh.Faces.Count <= 150000, $"{label} preview has {part8.Mesh.Faces.Count} triangles (want ≤150000)");
            Assert(part8.Remarks.Any(r => r.Contains("tube mesh")), label + " should use the parametric tube");
        }
        foreach (int seed in new[] { 0, 1, 2 }) AssertScaffoldPreview(seed, BambooSpeciesId.Moso, $"8m Moso seed {seed}");
        AssertScaffoldPreview(0, BambooSpeciesId.Guadua, "8m Guadua");
        AssertScaffoldPreview(0, BambooSpeciesId.Asper, "8m Asper");

        // Metre document: resolve in mm, place the solid at mmToDocument 0.001, prepare with unitToMm 1000.
        var metreSettings = new TimberSettings { Product = TimberProduct.Bamboo, Species = BambooSpeciesId.Moso, Seed = 0, Diameter = 80, Taper = 5, WallThickness = 7, NodeSpacing = 270, Variation = .8 };
        metreSettings.Validate();
        var metreFeatures = NaturalFeatures.Resolve(metreSettings, 8000, 80, 80);
        var metreAxis = new Line(Point3d.Origin, new Point3d(0, 0, 8));
        using var metreBrep = NaturalGeometry.Create(metreFeatures, metreAxis, 0.001, .001);
        var metreFrame = NaturalGeometry.Frame(metreAxis);
        using var metrePart = PreparedPart.Create(metreBrep, metreSettings.Duplicate(), 1000, .001, metreFrame, metreFrame.XAxis);
        Console.WriteLine($"8m metre-doc: faces={metrePart.Mesh.Faces.Count}");
        Assert(metrePart.Mesh.Faces.Count <= 150000, $"Metre-doc 8 m preview has {metrePart.Mesh.Faces.Count} triangles");

        // Bake stays on the revolved solid; dense first pass remeshes at 15° and surfaces a remark.
        using (var bakeUv = NativeUvGeometry.Create(metreBrep, Rhino.UnitSystem.Meters, 64, CancellationToken.None, Math.PI / 180))
        {
            Console.WriteLine($"8m bake metre-doc 1°: faces={bakeUv.Samples.Faces.Count} remark={bakeUv.Remark}");
            Assert(bakeUv.Samples.Faces.Count <= 200000, $"8 m bake has {bakeUv.Samples.Faces.Count} triangles");
            Assert(!string.IsNullOrEmpty(bakeUv.Remark) && bakeUv.Remark.Contains("15°"), "Over-budget bake must remark the 15° remesh");
        }

        var stock = logStock; var settings = stock.Settings.Duplicate(); settings.Resolution = 8; settings.EdgeResolution = 8;
        using var part = PreparedPart.Create(stock.Geometry, settings, 1, .001, stock.BasePlane, stock.Fibre, null, stock.OriginalGeometry);
        Near(part.LengthMm, 2400, .05);
        var pages = TextureAtlas.Prepare(part, settings, .25, Path.Combine(Output, "natural-atlas"));
        try {
            Assert(pages.Count > 0 && pages[0].Panel.Value is StockDefinition d && d.Construction.Pieces[0].Specimen.Features != null, "Preview did not build the log stock definition");
            var pixels = TextureAtlas.Rasterize(pages[0], CancellationToken.None);
            Assert(pixels.Select(p => p & 0xffffff).Distinct().Count() > 20 && pixels.Any(p => Luma(p) < 110) && pixels.Any(p => Luma(p) > 120), "Log texture lacks bark and timber");
        } finally { foreach (var page in pages) page.Dispose(); }
        using var user = new Box(Plane.WorldXY, new Interval(0, 1000), new Interval(0, 200), new Interval(0, 200)).ToBrep();
        using var legacy = PreparedPart.Create(user, new TimberSettings { Product = TimberProduct.Log, Variation = 0 }, 1, .001, Plane.WorldXY);
        var legacyPages = TextureAtlas.Prepare(legacy, new TimberSettings { Product = TimberProduct.Log, Variation = 0 }, .1, Path.Combine(Output, "natural-atlas"));
        foreach (var page in legacyPages) page.Dispose();
    }
}
