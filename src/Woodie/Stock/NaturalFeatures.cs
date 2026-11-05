using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>
/// Resolved anatomy of one natural Log or bamboo specimen, in stock millimetres with X along the axis (base → tip).
/// Geometry (radius, ovality, node stations) and appearance (knots, noise phase, tint) draw from separate seeded streams
/// and are resolved once. Bamboo vascular identity is a 2D cellular field in wall-polar (θ, t) — no axial cell index —
/// so cuts keep the same bundles at Larsson speed (O(9) neighbours, same cost class as softwood Perlin).
/// Variation 0 leaves nominal taper and nodes only.
/// </summary>
public sealed class NaturalFeatures
{
    // NodeJitter: residual multiplicative noise at Variation=1, kept under the ~12% among-culm IL CV ceiling (Li et al. 2025).
    const double Wobble = 0.05, OvalMax = 0.05, NodeJitter = 0.08, RcilSteepness = 8;
    readonly double a1, a2, a3, b1, b2, b3, wallPhase;
    readonly double[] ridge, knotX, knotAngle, knotSize;

    NaturalFeatures(TimberSettings s, double lengthMm, double diameterMm)
    {
        Bamboo = s.Product == TimberProduct.Bamboo; Seed = s.Seed; Length = lengthMm; Diameter = diameterMm; Variation = s.Variation;
        Taper = double.IsNaN(s.Taper) ? 0 : s.Taper;
        Bark = Bamboo || double.IsNaN(s.BarkThickness) ? 0 : s.BarkThickness;
        var species = Bamboo ? BambooSpecies.For(s.Species) : BambooSpecies.Default;
        Skin = species.Skin;
        FreeFibreStrands = Bamboo ? Math.Clamp(species.FreeFibreStrands, 0, 2) : 0;
        var g = Stream(Seed, 1);   // geometry
        a1 = g.NextDouble() * 2 * Math.PI; a2 = g.NextDouble() * 2 * Math.PI; a3 = g.NextDouble() * 2 * Math.PI; wallPhase = g.NextDouble() * 2 * Math.PI;
        b1 = lengthMm * (0.5 + g.NextDouble()); b2 = lengthMm * (0.15 + 0.15 * g.NextDouble()); b3 = lengthMm * (0.25 + 0.2 * g.NextDouble());
        Ovality = Variation * OvalMax * (0.5 + 0.5 * g.NextDouble()) * (g.NextDouble() < 0.5 ? -1 : 1);
        if (Math.Min(Outer(0), Outer(lengthMm)) < 0.1 * diameterMm || Math.Min(Outer(0), Outer(lengthMm)) <= 1)
            throw new ArgumentException("Taper leaves less than 20% of the base diameter at the tip. Reduce Taper or Length.");
        var nodes = new List<double>(); var heights = new List<double>();
        if (Bamboo)
        {
            Wall = double.IsNaN(s.WallThickness) ? Math.Min(species.Wall, 0.2 * diameterMm / 2) : s.WallThickness;
            Spacing = double.IsNaN(s.NodeSpacing) ? species.Spacing : s.NodeSpacing;
            Diaphragm = Math.Clamp(0.5 * Wall, 1, 6);
            RidgeHalfWidth = Math.Clamp(0.03 * Spacing, 2, 15);
            if (Wall <= 0 || Wall >= 0.4 * Math.Min(Outer(0), Outer(lengthMm)) * (1 - Wobble * Variation))
                throw new ArgumentException("Wall thickness must leave a hollow: it must be well under the culm radius.");
            PlaceBambooNodes(nodes, heights, g, species, lengthMm);
        }
        Nodes = nodes.ToArray(); ridge = heights.ToArray();
        var a = Stream(Seed, 2);   // appearance
        OffsetX = a.NextDouble() * 256; OffsetY = a.NextDouble() * 256;
        Tint = 1 + Variation * 0.1 * (a.NextDouble() * 2 - 1);
        int count = Bamboo ? 0 : (int)Math.Round(s.EffectiveKnotDensity * lengthMm / 1000 * 2 * Variation);
        knotX = new double[count]; knotAngle = new double[count]; knotSize = new double[count];
        for (int i = 0; i < count; i++) { knotX[i] = a.NextDouble() * lengthMm; knotAngle[i] = a.NextDouble() * 2 * Math.PI; knotSize[i] = 4 + 14 * a.NextDouble() * a.NextDouble(); }
    }

    public bool Bamboo { get; }
    public int Seed { get; }
    public double Length { get; }
    public double Diameter { get; }
    public double Taper { get; }
    public double Bark { get; }
    public double Variation { get; }
    public double Wall { get; }
    public double Spacing { get; }
    public double Diaphragm { get; }
    public double RidgeHalfWidth { get; }
    /// <summary>Constant elliptical stretch: stock Y scales by 1+Ovality and Z by 1−Ovality.</summary>
    public double Ovality { get; }
    public IReadOnlyList<double> Nodes { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double Tint { get; }
    /// <summary>Exterior culm skin RGB from the bamboo species preset; unused for Log.</summary>
    public double[] Skin { get; }
    /// <summary>Grosser free fibre strands (0 / 1 / 2) from the bamboo species preset.</summary>
    public int FreeFibreStrands { get; }
    public int KnotCount => knotX.Length;
    /// <summary>True when bamboo uses the axial-persistent wall-polar bundle field.</summary>
    public bool HasVolumetricBundles => Bamboo;
    public (double X, double Angle, double Size) Knot(int i) => (knotX[i], knotAngle[i], knotSize[i]);

    /// <summary>Resolve once. A null Diameter on the settings wraps the supplied cross-section (legacy/boxed stock).</summary>
    public static NaturalFeatures Resolve(TimberSettings settings, double lengthMm, double widthMm, double depthMm)
    {
        if (settings == null || !settings.IsNatural) throw new ArgumentException("Natural stock needs Log or Bamboo settings.");
        settings.Validate();
        if (!(lengthMm > 0) || !(widthMm > 0) || !(depthMm > 0) || !double.IsFinite(lengthMm + widthMm + depthMm))
            throw new ArgumentException("Stock dimensions must be finite and positive.");
        double diameter = double.IsNaN(settings.Diameter) ? Math.Max(widthMm, depthMm) : settings.Diameter;
        var features = new NaturalFeatures(settings, lengthMm, diameter);
        if (features.Bark >= features.Outer(lengthMm)) throw new ArgumentException("Bark must be thinner than the stem radius.");
        return features;
    }

    // Independent deterministic streams: geometry never consumes appearance draws, nor the reverse.
    static Random Stream(int seed, int salt) => new(unchecked(seed * 16777619 ^ salt * 40503 ^ 0x5bd1e995));

    /// <summary>
    /// Place nodes from a size-independent RIN–RCIL logistic (Inoue / Tan family): Spacing sets mean segment count,
    /// CulmInternodes windows the stock onto a full culm, PeakRin sets the longest-internode locus.
    /// Variation 0 uses a basal window (typical structural pole); Variation &gt; 0 picks a seeded window and soft IL jitter.
    /// </summary>
    void PlaceBambooNodes(List<double> nodes, List<double> heights, Random g, BambooSpeciesPreset species, double lengthMm)
    {
        int segments = Math.Max(2, (int)Math.Round(lengthMm / Spacing));
        if (segments - 1 > 100) throw new ArgumentException("Bamboo would have more than 100 nodes. Increase Node Spacing or shorten the culm.");
        int full = Math.Max(segments, species.CulmInternodes);
        // Near-full stocks use the whole culm curve; shorter poles are a contiguous RIN window.
        int start = segments >= (int)(0.85 * full) ? 0
            : Variation <= 0 ? 0                                                  // basal fragment (Lorenzo et al. lower sections)
            : (int)(g.NextDouble() * (full - segments + 1));
        double peak = Math.Clamp(species.PeakRin, 0.35, 0.65), sum = 0;
        var weights = new double[segments];
        for (int j = 0; j < segments; j++)
        {
            double w = Rcil((start + j + 1) / (double)full, peak) - Rcil((start + j) / (double)full, peak);
            if (Variation > 0) w *= 1 + (g.NextDouble() * 2 - 1) * NodeJitter * Variation;
            weights[j] = Math.Max(w, 1e-9); sum += weights[j];
        }
        double margin = Diaphragm / 2 + RidgeHalfWidth + 1, x = 0;
        for (int j = 0; j < segments - 1; j++)
        {
            x += lengthMm * weights[j] / sum;
            double rise = Variation <= 0 ? 0 : g.NextDouble() * 2 - 1;
            if (x < margin || x > lengthMm - margin) continue;
            // Ridge ~6–8% of radius so silhouette bulges read on contact sheets (geometry, not paint-only).
            nodes.Add(x); heights.Add(0.07 * (1 + 0.35 * Variation * rise));
        }
        if (nodes.Count == 0) throw new ArgumentException("Bamboo node spacing left no room for nodes. Increase Length or reduce Node Spacing.");
    }

    /// <summary>Normalised logistic RCIL at relative internode number u (inflection at peak). Cite: Inoue et al. 2012/2017; Tan et al. 2024 (logistic family; MBE omitted without published per-stage coeffs).</summary>
    static double Rcil(double u, double peak)
    {
        double S(double x) => 1 / (1 + Math.Exp(-RcilSteepness * (x - peak)));
        return (S(u) - S(0)) / (S(1) - S(0));
    }

    /// <summary>Speichinger-style radial occupancy for pith/skin empty bands and cell survival.</summary>
    public static double BundleSurvive(double tc)
    {
        double s = SmoothStep(0.08, 0.28, tc) * (0.20 + 0.80 * Math.Pow(Math.Max(0, (Math.Exp(2.9 * tc) - 1) / (Math.Exp(2.9) - 1)), 0.92));
        // Speichinger ~1% skin VB-free; keep outer cortex (t≲0.97) dense for side streaks.
        s *= SmoothStep(0.056, 0.10, tc) * (1.0 - SmoothStep(0.97, 0.99, tc));
        if (tc > 0.85) s *= Lerp(1.0, 0.92, (tc - 0.85) / 0.15);
        return s;
    }

    /// <summary>
    /// Vascular flower at wall point. 2D cellular field in (arc, wall-depth) only — no axial cell index — so identity
    /// is constant along the culm. Cost matches softwood: ≤9 neighbour cells + cheap integer hashes (no Perlin).
    /// <paramref name="angWarp"/> warps the query (Peng) without changing the field.
    /// </summary>
    public (double Sheath, double Lumen) SampleBundle(double ri, double ro, double r, double ang, double angWarp = 0)
    {
        if (!Bamboo) return (0, 0);
        double wall = ro - ri; if (!(wall > 1e-6)) return (0, 0);
        double tc = (r - ri) / wall; if (tc < 0.05 || tc > 0.99) return (0, 0);
        double areaMm2 = Lerp(0.194, 0.061, Math.Pow(tc, 0.9));
        // Shang-scale floor — dense outer packing without single-pixel charcoal.
        double spacing = Math.Max(0.50, Math.Sqrt(areaMm2) * Lerp(2.6, 0.85, Math.Pow(tc, 1.25)));
        double survive = BundleSurvive(tc);
        double aspect = Lerp(1.55, 0.48, tc);
        double clover = Math.Pow(1 - tc, 1.25);
        // Fixed reference radius so (θ,t) identity does not drift with taper along x.
        double rRef = Math.Max(Diameter * 0.5 - Wall * 0.5, spacing);
        double u = (ang + angWarp) * rRef / spacing, v = tc * Wall / spacing;
        return BundleField2D(u, v, OffsetX * Variation, OffsetY * Variation, aspect, survive, clover, FreeFibreStrands);
    }

    // Flower VB (Grosser/Dixon) in cell units. clover=1 → four caps; clover=0 → skull.
    static (double Sheath, double Lumen) BundleField2D(double u, double v, double ox, double oy, double aspect, double survive, double clover, int freeFibre)
    {
        int iu = (int)Math.Floor(u), iv = (int)Math.Floor(v);
        double su = Math.Max(0.55, Math.Sqrt(aspect)), sv = Math.Max(0.55, 1 / Math.Sqrt(aspect));
        double best = 9, blu = 0, blv = 0;
        for (int dv = -1; dv <= 1; dv++)
        for (int du = -1; du <= 1; du++)
        {
            int cx = iu + du, cy = iv + dv;
            if (Hash01(cx, cy + 41, 0, ox) > survive) continue;
            double jx = Hash01(cx, cy, 1, ox), jy = Hash01(cx + 19, cy - 7, 2, oy);
            double lu = u - (cx + jx), lv = v - (cy + jy);
            double d = Math.Sqrt(lu * lu * su * su + lv * lv * sv * sv);
            if (d < best) { best = d; blu = lu; blv = lv; }
        }
        if (best > 8) return (0, 0);
        double fibre = SoftCap(blu, blv, 0.0, 0.0, su, sv) * 0.70;
        fibre = Math.Max(fibre, Math.Max(SoftCap(blu, blv, 0.0, 0.18, su, sv), SoftCap(blu, blv, 0.0, -0.18, su, sv)));
        if (clover > 0.05)
            fibre = Math.Max(fibre, clover * Math.Max(SoftCap(blu, blv, 0.18, 0.0, su, sv), SoftCap(blu, blv, -0.18, 0.0, su, sv)));
        if (freeFibre >= 1) fibre = Math.Max(fibre, SoftCap(blu, blv, 0.0, 0.40, su, sv) * 0.85);
        if (freeFibre >= 2) fibre = Math.Max(fibre, SoftCap(blu, blv, 0.0, -0.38, su, sv) * 0.8);
        double vs = Lerp(0.07, 0.13, clover);
        double lumen = Math.Max(SoftCap(blu, blv, vs, 0.0, su, sv), SoftCap(blu, blv, -vs, 0.0, su, sv));
        return (Math.Min(1, fibre), lumen * SmoothStep(0.42, 0.08, best));
    }

    static double SoftCap(double lu, double lv, double cu, double cv, double su, double sv)
    {
        double d = Math.Sqrt((lu - cu) * (lu - cu) * su * su + (lv - cv) * (lv - cv) * sv * sv);
        // Discrete flower lobes (preview-scale); Late amber keeps contrast non-charcoal.
        return SmoothStep(0.20, 0.045, d);
    }

    // Integer hash — faster than Perlin for cellular jitter (TimberPanel still owns grain noise).
    static double Hash01(int x, int y, int z, double salt)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
            n ^= (uint)(salt * 4294967.0);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xffffff) * (1.0 / 16777215.0);
        }
    }

    static double Lerp(double a, double b, double t) => a + (b - a) * t;
    static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge0 == edge1) return x < edge0 ? 0 : 1;
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    double Sway(double x) => 0.6 * Math.Sin(2 * Math.PI * x / b1 + a1) + 0.4 * Math.Sin(2 * Math.PI * x / b2 + a2);

    /// <summary>Stem radius (mm) before node ridges: nominal taper plus bounded variation.</summary>
    public double Outer(double x) => (Diameter / 2 - Taper * x / 2000) * (1 + Variation * Wobble * Sway(x));

    /// <summary>Bamboo wall thickness at x.</summary>
    // shortcut: sinusoid Variation only; add species t(z) taper when strip machining uses a local window.
    public double WallAt(double x) => Wall * (1 + 0.15 * Variation * Math.Sin(2 * Math.PI * x / b3 + wallPhase));

    /// <summary>Inner (hollow) radius at x for bamboo.</summary>
    public double Inner(double x) => Outer(x) - WallAt(x);

    /// <summary>External node ridge height (mm) at x; zero for logs.</summary>
    public double Ridge(double x)
    {
        int i = NearNode(x, out double d);
        return i >= 0 && d < RidgeHalfWidth ? ridge[i] * Outer(Nodes[i]) * 0.5 * (1 + Math.Cos(Math.PI * d / RidgeHalfWidth)) : 0;
    }

    /// <summary>Index of the nearest node and the distance to it; -1 for logs.</summary>
    public int NearNode(double x, out double distance)
    {
        distance = double.PositiveInfinity;
        if (Nodes.Count == 0) return -1;
        int lo = 0, hi = Nodes.Count - 1;
        while (lo < hi) { int mid = (lo + hi) / 2; if (Nodes[mid] < x) lo = mid + 1; else hi = mid; }
        int best = lo;
        if (lo > 0 && Math.Abs(Nodes[lo - 1] - x) < Math.Abs(Nodes[lo] - x)) best = lo - 1;
        distance = Math.Abs(Nodes[best] - x);
        return best;
    }

    /// <summary>True where the nominal anatomy has material (hollow internodes, annular ends and solid diaphragms for bamboo).</summary>
    public bool Solid(double x, double r)
    {
        if (x < 0 || x > Length || r > Outer(x) + Ridge(x)) return false;
        if (!Bamboo) return true;
        NearNode(x, out double d);
        return r >= Inner(x) || d <= Diaphragm / 2;
    }

    /// <summary>Axis profile polylines (x, r) in stock mm, each a revolved face, with material on the right of travel.</summary>
    public List<List<(double X, double R)>> Profile()
    {
        var pieces = new List<List<(double X, double R)>>();
        double L = Length, h = Diaphragm / 2;
        List<(double X, double R)> Run(double x0, double x1, Func<double, double> radius, IEnumerable<double> extra = null)
        {
            var xs = new SortedSet<double> { x0, x1 };
            if (extra != null) foreach (double e in extra) if (e > x0 && e < x1) xs.Add(e);
            // shortcut: equal steps of min(100 mm, b2/8) so long hollow runs stay under the 200k preview mesh cap; densify if radius checks fail on short logs.
            if (Variation > 0 && x1 > x0)
            {
                double step = Math.Min(100, b2 / 8);
                int n = Math.Max(1, (int)Math.Ceiling((x1 - x0) / step));
                for (int i = 1; i < n; i++) xs.Add(x0 + (x1 - x0) * i / (double)n);
            }
            return xs.Select(x => (x, radius(x))).ToList();
        }
        if (!Bamboo)
        {
            pieces.Add(new() { (0, 0), (0, Outer(0)) });
            pieces.Add(Run(0, L, Outer));
            pieces.Add(new() { (L, Outer(L)), (L, 0) });
            return pieces;
        }
        var stations = Nodes.SelectMany(n => new[] { -1.0, -0.75, -0.5, -0.25, 0, 0.25, 0.5, 0.75, 1 }.Select(f => n + f * RidgeHalfWidth));
        pieces.Add(new() { (0, Inner(0)), (0, Outer(0)) });
        pieces.Add(Run(0, L, x => Outer(x) + Ridge(x), stations));
        pieces.Add(new() { (L, Outer(L)), (L, Inner(L)) });
        double right = L;
        for (int i = Nodes.Count - 1; i >= -1; i--)
        {
            double left = i < 0 ? 0 : Nodes[i] + h;
            var wall = Run(left, right, Inner); wall.Reverse();   // leftwards along the cavity
            pieces.Add(wall);
            if (i < 0) break;
            pieces.Add(new() { (left, Inner(left)), (left, 0) });                                              // diaphragm face toward the tip, down
            pieces.Add(new() { (Nodes[i] - h, 0), (Nodes[i] - h, Inner(Nodes[i] - h)) });                       // diaphragm face toward the base, up
            right = Nodes[i] - h;
        }
        return pieces;
    }
}