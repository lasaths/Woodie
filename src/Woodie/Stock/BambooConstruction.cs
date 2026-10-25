using Woodie.Appearance;

namespace Woodie.Stock;

/// <summary>
/// Engineered bamboo: culm → longitudinal split → machined strip → orientation + adhesive → finished stock.
/// Every strip is one <see cref="MaterialPiece"/> that maps its stock cell rigidly into a Moso source culm (generated internally,
/// one culm per ring of strips, so strips from one culm stay correlated and different culms vary independently).
/// Machining is the radial/tangential window below: skin and inner lining lie outside it and are never sampled, and the
/// window follows the wall rather than regenerating grain. Nodes are kept; a seeded axial offset staggers them between strips.
/// <para>Plain-pressed: wide (tangential) face on the press, radial = stock Z. Side-pressed: strips on edge, tangential = stock Z
/// (through the thickness) and radial = stock Y (across the width), so the top shows strip edges. Layered panels are flat strip
/// layers taken from <see cref="TimberLayups"/> with 0°/90° fibre.</para>
/// Provenance: representative strip sizes (about 20 × 4 mm), glue lines and plain/side/layered constructions follow the laminated
/// bamboo lumber study of Lorenzo et al. (https://discovery.ucl.ac.uk/10170654/3/Lorenzo_Size%20effect%20on%20the%20compressive%20strength%20of%20laminated%20bamboo%20lumber.pdf);
/// the numbers are visual presets, not values transcribed from it. Colour and adhesive tone are appearance adaptations.
/// shortcut: strips span the whole stock (no end joints), flat approximation of the split strip, one nominal wall window; upgrade with scarf/finger joints and per-culm wall tracking.
/// </summary>
public static class BambooConstruction
{
    // Source culm presets for strip stock (a mature Moso lower culm; thick enough to plane a 4 mm strip after removing skin and lining).
    const double DefaultStripWidth = 20, DefaultStripThickness = 4, DefaultDiameter = 100, DefaultWall = 10, DefaultSpacing = 270;
    const double SkinFraction = .14, LiningMm = .5;   // wall share planed off the outside (the model's skin zone) and lining planed off the inside
    const int MaxStrips = 50000;

    public static double StripWidth(double lamellaWidth) => double.IsNaN(lamellaWidth) ? DefaultStripWidth : lamellaWidth;
    public static double StripThickness(double veneerThickness) => double.IsNaN(veneerThickness) ? DefaultStripThickness : veneerThickness;

    // One block of strips: fibre/tangential/radial are stock axes (0 = X, 1 = Y, 2 = Z).
    sealed class Grid
    {
        public int Fibre, Tang, Rad, NT, NR, Start;
        public double[] Lo = new double[3], Size = new double[3];
        public double CT, CR;
    }

    public static Construction Build(TimberSettings settings, double lengthMm, double widthMm, double depthMm, IReadOnlyList<TimberLayer> layers)
    {
        double stripW = StripWidth(settings.LamellaWidth), stripT = StripThickness(settings.VeneerThickness);
        var grids = new List<Grid>(); long total = 0;
        void Add(int fibre, int tang, int rad, double z0, double[] size)
        {
            var g = new Grid { Fibre = fibre, Tang = tang, Rad = rad, Size = size, Start = (int)total };
            g.Lo[2] = z0;
            g.NT = Math.Max(1, (int)Math.Ceiling(size[tang] / stripW - 1e-9)); g.NR = Math.Max(1, (int)Math.Ceiling(size[rad] / stripT - 1e-9));
            g.CT = size[tang] / g.NT; g.CR = size[rad] / g.NR;   // exact tiling: strips are planed to fit the stock, never overhang it
            total += (long)g.NT * g.NR;
            if (total > MaxStrips) throw new ArgumentException("Too many bamboo strips for this stock; increase Lamella Width or Veneer Thickness (strip width/thickness).");
            grids.Add(g);
        }
        if (settings.Product == TimberProduct.BambooLumberSide)
            Add(0, 2, 1, 0, new[] { lengthMm, widthMm, depthMm });
        else
            foreach (var layer in layers)
                Add(layer.alongX ? 0 : 1, layer.alongX ? 1 : 0, 2, layer.z0, new[] { lengthMm, widthMm, layer.z1 - layer.z0 });

        // Source culms: Moso, Product forced to Bamboo so NaturalFeatures/BambooMaterialModel resolve exactly like natural stock.
        var source = settings.Duplicate(); source.Product = TimberProduct.Bamboo; source.Layers = null; source.FitToPart = false;
        if (double.IsNaN(source.Diameter)) source.Diameter = DefaultDiameter;
        if (double.IsNaN(source.WallThickness)) source.WallThickness = DefaultWall;
        double diameter = source.Diameter, spacing = double.IsNaN(source.NodeSpacing) ? DefaultSpacing : source.NodeSpacing;
        double culmLength = Math.Max(lengthMm, widthMm) + spacing;   // room for the per-strip node stagger
        var palette = settings.Palette();
        var culms = new List<(SourceSpecimen Specimen, IMaterialModel Model)>();
        (SourceSpecimen Specimen, IMaterialModel Model) Culm(int group)
        {
            while (culms.Count <= group)
            {
                var s = source.Duplicate(); s.Seed = unchecked(settings.Seed + culms.Count * 7919);
                var features = NaturalFeatures.Resolve(s, culmLength, diameter, diameter);
                culms.Add((new SourceSpecimen(s.Seed, features), new BambooMaterialModel(features, palette, diameter, diameter)));
            }
            return culms[group];
        }

        // Machining window (flat strip planed from the curved wall): outer corners stay inside the outer surface, inner face outside the cavity.
        var first = Culm(0).Specimen.Features;
        double outer = first.Outer(0) - SkinFraction * first.Wall, inner = first.Outer(0) - first.Wall + LiningMm;
        double chord = Math.Sqrt(Math.Max(outer * outer - stripW * stripW / 4, 0));
        if (chord - stripT - inner <= 0) throw new ArgumentException("Strip thickness and width do not fit the culm wall; use thinner or narrower strips or a thicker wall.");
        double rho = (chord + inner) / 2;                                  // radial window centre
        int ring = Math.Max(1, (int)Math.Floor(2 * Math.PI * rho / stripW));   // strips split from one culm

        var pieces = new MaterialPiece[total];
        for (int gi = 0; gi < grids.Count; gi++)
        {
            var g = grids[gi];
            for (int ir = 0; ir < g.NR; ir++)
                for (int it = 0; it < g.NT; it++)
                {
                    int k = g.Start + ir * g.NT + it;
                    var (specimen, model) = Culm(k / ring);
                    double theta = 2 * Math.PI * (k % ring) / ring, c = Math.Cos(theta), sn = Math.Sin(theta);
                    var rnd = new Random(unchecked(settings.Seed * 7919 + k * 104729 + 17));
                    double offset = rnd.NextDouble() * spacing, sg = rnd.NextDouble() < .5 ? -1 : 1;   // axial stagger; strip turned 180° about its fibre

                    var min = new double[3]; var max = new double[3];
                    min[g.Fibre] = 0; max[g.Fibre] = g.Size[g.Fibre];
                    min[g.Tang] = g.Lo[g.Tang] + it * g.CT; max[g.Tang] = min[g.Tang] + g.CT;
                    min[g.Rad] = g.Lo[g.Rad] + ir * g.CR; max[g.Rad] = min[g.Rad] + g.CR;
                    var centre = new double[3]; for (int a = 0; a < 3; a++) centre[a] = (min[a] + max[a]) / 2;

                    // Rows of the rotation: source x = fibre, source (y, z) = radial/tangential frame at angle theta (sg turns the strip 180° about its fibre).
                    var m = new double[3, 3];
                    m[0, g.Fibre] = 1;
                    m[1, g.Rad] = sg * c; m[1, g.Tang] = -sg * sn;
                    m[2, g.Rad] = sg * sn; m[2, g.Tang] = sg * c;
                    double half = diameter / 2;
                    var basePoint = new[] { centre[g.Fibre] + offset, half + rho * c, half + rho * sn };
                    var o = new double[3];   // MaterialPiece maps s = M (p − o); M is orthogonal so o = centre − Mᵀ·base
                    for (int j = 0; j < 3; j++) o[j] = centre[j] - (m[0, j] * basePoint[0] + m[1, j] * basePoint[1] + m[2, j] * basePoint[2]);

                    // Neighbours receive adhesive; stock boundaries never do. Source frame flips with the strip.
                    bool tLo = it > 0, tHi = it < g.NT - 1, rLo = ir > 0 || gi > 0, rHi = ir < g.NR - 1 || gi < grids.Count - 1;
                    var strip = new StripModel(model, offset, offset + g.Size[g.Fibre], half, rho, g.CT / 2, g.CR / 2, c, sn, sg < 0 ? tHi : tLo, sg < 0 ? tLo : tHi, sg < 0 ? rHi : rLo, sg < 0 ? rLo : rHi, palette);
                    pieces[k] = new MaterialPiece(specimen, strip, min[0], min[1], min[2], max[0], max[1], max[2], o[0], o[1], o[2],
                        m[0, 0], m[0, 1], m[0, 2], m[1, 0], m[1, 1], m[1, 2], m[2, 0], m[2, 1], m[2, 2]);
                }
        }

        int Locate(double x, double y, double z)
        {
            int gi = 0; while (gi < grids.Count - 1 && z >= grids[gi].Lo[2] + grids[gi].Size[2]) gi++;
            var g = grids[gi];
            int it = Math.Clamp((int)Math.Floor((Axis(x, y, z, g.Tang) - g.Lo[g.Tang]) / g.CT), 0, g.NT - 1);
            int ir = Math.Clamp((int)Math.Floor((Axis(x, y, z, g.Rad) - g.Lo[g.Rad]) / g.CR), 0, g.NR - 1);
            return g.Start + ir * g.NT + it;
        }
        return new Construction(pieces, Locate);
    }

    static double Axis(double x, double y, double z, int axis) => axis == 0 ? x : axis == 1 ? y : z;

    /// <summary>Culm anatomy for one strip, in source coordinates, plus adhesive on the faces shared with neighbouring strips.</summary>
    sealed class StripModel : IMaterialModel
    {
        readonly IMaterialModel inner;
        readonly double xLo, xHi, cy, rho, halfT, halfR, c, s, glueW, glueK, gr, gg, gb;
        readonly bool tLo, tHi, rLo, rHi;

        public StripModel(IMaterialModel inner, double xLo, double xHi, double centre, double rho, double halfT, double halfR, double cos, double sin,
            bool tLo, bool tHi, bool rLo, bool rHi, TimberPalette palette)
        {
            this.inner = inner; this.xLo = xLo; this.xHi = xHi; cy = centre; this.rho = rho; this.halfT = halfT; this.halfR = halfR; c = cos; s = sin;
            this.tLo = tLo; this.tHi = tHi; this.rLo = rLo; this.rHi = rHi;
            glueW = palette.GlueW; glueK = palette.GlueK; gr = palette.Glue[0] * palette.Gain; gg = palette.Glue[1] * palette.Gain; gb = palette.Glue[2] * palette.Gain;
        }

        // Stock end faces sit at the strip's axial ends, which are not source x = 0: report them as end grain (normal along the fibre).
        public int Sample(double x, double y, double z) =>
            x - xLo < 1e-3 || xHi - x < 1e-3 ? Glue(inner.Sample(x, y, z, 1, 0, 0), y, z) : Glue(inner.Sample(x, y, z), y, z);
        public int Sample(double x, double y, double z, double nx, double ny, double nz) => Glue(inner.Sample(x, y, z, nx, ny, nz), y, z);

        int Glue(int argb, double sy, double sz)
        {
            double dy = sy - cy, dz = sz - cy, a = -dy * s + dz * c, b = dy * c + dz * s - rho, gap = double.MaxValue;
            if (tLo) gap = Math.Min(gap, a + halfT);
            if (tHi) gap = Math.Min(gap, halfT - a);
            if (rLo) gap = Math.Min(gap, b + halfR);
            if (rHi) gap = Math.Min(gap, halfR - b);
            if (!(gap < glueW)) return argb;
            double k = glueK;
            int r = (int)Math.Clamp((argb >> 16 & 255) * (1 - k) + gr * k, 0, 255), g = (int)Math.Clamp((argb >> 8 & 255) * (1 - k) + gg * k, 0, 255), bl = (int)Math.Clamp((argb & 255) * (1 - k) + gb * k, 0, 255);
            return (255 << 24) | (r << 16) | (g << 8) | bl;
        }
    }
}
