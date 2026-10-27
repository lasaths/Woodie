using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>
/// Resolved anatomy of one natural Log or Moso bamboo specimen, in stock millimetres with X along the axis (base → tip).
/// Geometry (radius, ovality, node stations) and appearance (knots, noise phase, tint) draw from separate seeded streams
/// and are resolved once; Variation 0 leaves nominal taper, nodes and rings only.
/// </summary>
public sealed class NaturalFeatures
{
    const double Wobble = 0.05, OvalMax = 0.05, NodeJitter = 0.3;
    readonly double a1, a2, a3, b1, b2, b3, wallPhase;
    readonly double[] ridge, knotX, knotAngle, knotSize;

    NaturalFeatures(TimberSettings s, double lengthMm, double diameterMm)
    {
        Bamboo = s.Product == TimberProduct.Bamboo; Seed = s.Seed; Length = lengthMm; Diameter = diameterMm; Variation = s.Variation;
        Taper = double.IsNaN(s.Taper) ? 0 : s.Taper;
        Bark = Bamboo || double.IsNaN(s.BarkThickness) ? 0 : s.BarkThickness;
        var g = Stream(Seed, 1);   // geometry
        a1 = g.NextDouble() * 2 * Math.PI; a2 = g.NextDouble() * 2 * Math.PI; a3 = g.NextDouble() * 2 * Math.PI; wallPhase = g.NextDouble() * 2 * Math.PI;
        b1 = lengthMm * (0.5 + g.NextDouble()); b2 = lengthMm * (0.15 + 0.15 * g.NextDouble()); b3 = lengthMm * (0.25 + 0.2 * g.NextDouble());
        Ovality = Variation * OvalMax * (0.5 + 0.5 * g.NextDouble()) * (g.NextDouble() < 0.5 ? -1 : 1);
        if (Math.Min(Outer(0), Outer(lengthMm)) < 0.1 * diameterMm || Math.Min(Outer(0), Outer(lengthMm)) <= 1)
            throw new ArgumentException("Taper leaves less than 20% of the base diameter at the tip. Reduce Taper or Length.");
        var nodes = new List<double>(); var heights = new List<double>();
        if (Bamboo)
        {
            Wall = double.IsNaN(s.WallThickness) ? Math.Min(7, 0.2 * diameterMm / 2) : s.WallThickness;
            Spacing = double.IsNaN(s.NodeSpacing) ? 270 : s.NodeSpacing;
            Diaphragm = Math.Clamp(0.5 * Wall, 1, 6);
            RidgeHalfWidth = Math.Clamp(0.03 * Spacing, 2, 15);
            if (Wall <= 0 || Wall >= 0.4 * Math.Min(Outer(0), Outer(lengthMm)) * (1 - Wobble * Variation))
                throw new ArgumentException("Wall thickness must leave a hollow: it must be well under the culm radius.");
            double margin = Diaphragm / 2 + RidgeHalfWidth + 1, x = 0;
            while (true)
            {
                double jitter = (g.NextDouble() * 2 - 1) * NodeJitter * Variation, rise = g.NextDouble() * 2 - 1;
                x += Spacing * (1 + jitter);
                if (x > lengthMm - margin) break;
                nodes.Add(x); heights.Add(0.035 * (1 + 0.4 * Variation * rise));
                if (nodes.Count > 100) throw new ArgumentException("Bamboo would have more than 100 nodes. Increase Node Spacing or shorten the culm.");
            }
        }
        Nodes = nodes.ToArray(); ridge = heights.ToArray();
        var a = Stream(Seed, 2);   // appearance
        OffsetX = a.NextDouble() * 256; OffsetY = a.NextDouble() * 256;
        Tint = 1 + Variation * 0.04 * (a.NextDouble() * 2 - 1);
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
    public int KnotCount => knotX.Length;
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

    double Sway(double x) => 0.6 * Math.Sin(2 * Math.PI * x / b1 + a1) + 0.4 * Math.Sin(2 * Math.PI * x / b2 + a2);

    /// <summary>Stem radius (mm) before node ridges: nominal taper plus bounded variation.</summary>
    public double Outer(double x) => (Diameter / 2 - Taper * x / 2000) * (1 + Variation * Wobble * Sway(x));

    /// <summary>Bamboo wall thickness at x.</summary>
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
            if (Variation > 0) for (int i = 1; i < 64; i++) xs.Add(x0 + (x1 - x0) * i / 64);
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