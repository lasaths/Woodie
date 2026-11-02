using Woodie.Appearance;
using static Woodie.Appearance.TimberPanel;

namespace Woodie.Stock;

/// <summary>
/// Cylindrical anatomy about the stock axis: X runs base → tip and the section is centred in the stock's YZ bounds.
/// Any elliptical stretch applied to the geometry is undone first, so rings and walls follow the surface.
/// </summary>
public abstract class NaturalModel : IMaterialModel
{
    protected readonly NaturalFeatures F;
    protected readonly TimberPalette Pal;
    protected readonly double CentreY, CentreZ;
    readonly double sy, sz, ox, oy;

    protected NaturalModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm)
    {
        F = features; Pal = palette; CentreY = widthMm / 2; CentreZ = depthMm / 2; sy = 1 + F.Ovality; sz = 1 - F.Ovality;
        ox = F.OffsetX * F.Variation; oy = F.OffsetY * F.Variation;   // variation 0: seed-independent noise phase
    }

    protected double OffsetX => ox;
    protected double OffsetY => oy;

    public int Sample(double x, double y, double z) => Sample(x, y, z, 0, 0, 0, false);
    public int Sample(double x, double y, double z, double nx, double ny, double nz) => Sample(x, y, z, nx, ny, nz, true);
    public double KnotInfluence(double x, double y, double z) => 0;
    public virtual bool TryFibreDirection(double x, double y, double z, out double dx, out double dy, out double dz)
    { dx = dy = dz = 0; return false; }

    int Sample(double x, double y, double z, double nx, double ny, double nz, bool oriented)
    {
        double dy = (y - CentreY) / sy, dz = (z - CentreZ) / sz, r = Math.Sqrt(dy * dy + dz * dz);
        double end = x < 1e-3 || x > F.Length - 1e-3 ? 1 : 0;   // axial share of the geometric cut normal; end faces are 1
        double scale = Math.Max(Math.Abs(nx), Math.Max(Math.Abs(ny), Math.Abs(nz)));
        if (oriented && scale > 0 && double.IsFinite(scale)) { nx /= scale; ny /= scale; nz /= scale; end = nx * nx / (nx * nx + ny * ny + nz * nz); }
        var c = Shade(Math.Clamp(x, 0, F.Length), r, dy, dz, end);
        double g = Pal.Gain, mx = Math.Max(c.R, Math.Max(c.G, c.B));
        if (mx > Pal.Cap) { c.R *= Pal.Cap / mx; c.G *= Pal.Cap / mx; c.B *= Pal.Cap / mx; }
        return (255 << 24) | (Channel(c.R * g * Pal.GR) << 16) | (Channel(c.G * g * Pal.GG) << 8) | Channel(c.B * g * Pal.GB);
    }

    static int Channel(double v) => double.IsNaN(v) ? 255 : (int)Math.Max(0, Math.Min(255, v));
    protected double[] Lerp3(double[] a, double[] b, double t) => new[] { Lerp(a[0], b[0], t), Lerp(a[1], b[1], t), Lerp(a[2], b[2], t) };

    protected abstract (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end);
}

/// <summary>Softwood stem: growth rings parallel to the surface, bounded noise, knots and an outer bark region.</summary>
public sealed class LogMaterialModel : NaturalModel
{
    public LogMaterialModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm) : base(features, palette, widthMm, depthMm) { }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end)
    {
        double R = F.Outer(x), v = F.Variation, ox = OffsetX;
        if (F.Bark > 0 && r >= R - F.Bark)
        {
            // Bark is the outer material region only; a fresh cut shows timber inside it.
            double fissure = SmoothStep(0.05, 0.55, Math.Abs(Perlin(x * 0.015 + ox, dy * 0.06 + OffsetY, dz * 0.06)));
            double k = F.Tint * (0.9 + 0.2 * fissure);
            return ((46 + 58 * fissure) * k, (34 + 48 * fissure) * k, (26 + 34 * fissure) * k);
        }
        double ca = r > 1e-9 ? dy / r : 1, sa = r > 1e-9 ? dz / r : 0, ang = Math.Atan2(dz, dy);
        double n = v * Pal.NoiseK * (Perlin(x * 0.004, ca * 1.2 + ox, sa * 1.2) * 4 + Perlin(x * 0.01, ca * 4 + ox, sa * 4 + r * 0.02) * 1.5);
        double knot = 0, ring = 0;
        for (int i = 0; i < F.KnotCount; i++)
        {
            var (kx, ka, ks) = F.Knot(i);
            double kr = ks * r / R, du = x - (kx + 0.35 * r);
            if (!(kr > 1e-6) || Math.Abs(du) > 12 * kr) continue;
            double d = Math.Sqrt(du * du + Math.Pow(Math.Abs(Math.IEEERemainder(ang - ka, 2 * Math.PI)) * r, 2));
            if (d < kr) { knot = Math.Max(knot, 1 - d / kr); ring = Math.Max(ring, SmoothStep(0.75, 1, d / kr)); }
            else n -= 1.5 * kr * Math.Exp(-(d - kr) / kr);
        }
        double rr = (R - r + n) / Pal.RS, f = rr - Math.Floor(rr);
        double ew = 0.5 + 0.5 * v * Perlin((r + n) / 14.0, x * 0.002, ox);
        double fibre = v * (Perlin(x * 0.08, dy * 2, dz * 2) * 0.5 + 0.5) * 0.08;
        if (end > 0) fibre = Lerp(fibre, (0.5 + 0.5 * Perlin(x * 0.6, dy * 0.8, dz * 0.8)) * 0.06, end);
        double shape = (2 * ew - 1) * 0.75;
        double late = Lerp(Pal.LateK, Pal.EndLateK >= 0 ? Pal.EndLateK : Pal.LateK, end)
            * SmoothStep(0.6 + 0.035 * shape, 0.9 + 0.015 * shape, f) * (1 - SmoothStep(0.96 + 0.008 * shape, 1, f));
        var c = new double[3];
        for (int k = 0; k < 3; k++) c[k] = Lerp(Lerp(Pal.E0[k], Pal.E1[k], ew), Pal.Late[k], late);
        c[2] *= 1.01; c[1] *= 0.98;
        double t = F.Tint * (1 - fibre); c[0] *= t; c[1] *= Math.Pow(t, Pal.TG); c[2] *= Math.Pow(t, Pal.TB);
        c[1] = Math.Min(c[1], c[0] - Pal.HueG); c[2] = Math.Min(c[2], Math.Min(c[0] - Pal.HueB, c[1] - (Pal.HueB - Pal.HueG)));
        double lum = (c[0] + c[1] + c[2]) / 3; if (lum < Pal.WarmL) { c[1] -= Pal.WarmG * (Pal.WarmL - lum); c[2] -= Pal.WarmB * (Pal.WarmL - lum); }
        if (knot > 0) { double kk = 0.40 + 0.10 * (Math.Sin(knot * 40) * 0.5 + 0.5) - 0.22 * ring; for (int k = 0; k < 3; k++) c[k] = Lerp(c[k], Pal.Knot[k] * kk / 0.5, 0.95); }
        return (c[0], c[1], c[2]);
    }
}

/// <summary>
/// Bamboo culm: hollow internodes, persistent vascular bundles (dense small outer → sparse large inner), thin epidermis, node ridges, diaphragms.
/// Bundles are resolved once on <see cref="NaturalFeatures"/> in wall-polar (θ, t) and sampled like Larsson growth fields — axial cuts keep identity.
/// </summary>
public sealed class BambooMaterialModel : NaturalModel
{
    public BambooMaterialModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm) : base(features, palette, widthMm, depthMm) { }

    public override bool TryFibreDirection(double x, double y, double z, out double dx, out double dy, out double dz)
    {
        double dy0 = (y - CentreY) / (1 + F.Ovality), dz0 = (z - CentreZ) / (1 - F.Ovality);
        double r = Math.Sqrt(dy0 * dy0 + dz0 * dz0), ang = Math.Atan2(dz0, dy0);
        F.NearNode(x, out double nd);
        double nodeHalf = Math.Max(Math.Max(1.2, F.RidgeHalfWidth), 6.5);
        double nodeW = double.IsInfinity(nd) ? 0 : Math.Exp(-0.5 * Math.Pow(nd / Math.Max(nodeHalf * 0.4, 1.2), 2));
        // Internode fibres align with the culm axis; near nodes divert toward the tangential (Peng).
        double tx = -Math.Sin(ang), ty = Math.Cos(ang);
        dx = 1 - 0.55 * nodeW;
        dy = tx * 0.85 * nodeW;
        dz = ty * 0.85 * nodeW;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-9) { dx = dy = dz = 0; return false; }
        dx /= len; dy /= len; dz /= len;
        return r <= F.Outer(x) + F.Ridge(x) + 1e-3;
    }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end)
    {
        // Scalar RGB (no Lerp3 allocations) and few Perlin calls — atlas raster hits this millions of times.
        double ox = OffsetX, oy = OffsetY, ri = F.Inner(x), ro = F.Outer(x), tint = F.Tint;
        F.NearNode(x, out double nd);
        double ang = Math.Atan2(dz, dy);
        double nodeHalf = Math.Max(Math.Max(1.2, F.RidgeHalfWidth), 6.5);
        double nodeW = 0, nodeCore = 0, nodeHalo = 0;
        if (!double.IsInfinity(nd) && nd < nodeHalf * 2)
        {
            double ndAdj = Math.Abs(nd + 0.9 * Perlin(ang * 2.4 + ox, oy * 0.35, 1.7));
            nodeCore = Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 0.22, 0.8), 2));
            nodeHalo = Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 0.55, 2.0), 2));
            nodeW = Math.Max(nodeCore, 0.7 * nodeHalo);
        }
        double cr, cg, cb;
        if (r < ri - 1e-3)
        {
            bool disc = nd <= F.Diaphragm / 2 + 1e-3;
            double cloud = 0.5 + 0.5 * Perlin(dy * 0.08 + ox, dz * 0.08 + oy, disc ? 0.5 : x * 0.02);
            double t = disc ? 0.35 * cloud : 0.12 * cloud;
            cr = Lerp(Pal.E1[0], Pal.E0[0], t); cg = Lerp(Pal.E1[1], Pal.E0[1], t); cb = Lerp(Pal.E1[2], Pal.E0[2], t);
        }
        else
        {
            double wall = Math.Max(ro - ri, 1e-6), tc = Math.Clamp((r - ri) / wall, 0, 1);
            double cross = Math.Max(end, nodeW * 0.85);
            double angWarp = nodeW > 1e-3 ? nodeW * 0.55 * Perlin(x * 0.06, ang * 4 + ox, oy) : 0;
            var (sheath, lumen) = F.SampleBundle(ri, ro, r, ang, angWarp);
            lumen *= Math.Clamp(1.12 - (0.226 * tc + 0.710), 0, 1);
            double longKeep = 1 - cross, rho = Math.Pow(tc, 1.55), shade = Lerp(1.04, 0.90, rho);
            double baseT = 0.05 + 0.18 * (1 - tc);
            cr = Lerp(Pal.E0[0], Pal.E1[0], baseT) * shade; cg = Lerp(Pal.E0[1], Pal.E1[1], baseT) * shade; cb = Lerp(Pal.E0[2], Pal.E1[2], baseT) * shade;
            cr = Lerp(cr, F.Skin[0], 0.04); cg = Lerp(cg, F.Skin[1], 0.04); cb = Lerp(cb, F.Skin[2], 0.04);
            if (longKeep > 0.2)
            {
                double arc = (ang + angWarp) * 0.5 * (ri + ro);
                double brick = SmoothStep(0.18, 0.02, Math.Abs(Fract(x * 0.55 + ox) - 0.5) * Math.Abs(Fract(arc * 0.9 + oy) - 0.5));
                double softPar = (0.5 + 0.5 * Perlin(x * 0.03, dy * 1.2 + ox, dz * 1.2)) * 0.04 * longKeep * (1 - sheath);
                double dim = 1 - 0.045 * brick * longKeep - softPar;
                cr *= dim; cg *= dim; cb *= dim;
            }
            if (longKeep > 0.15)
            {
                double sx = dy, sy = dz;
                if (nodeW > 1e-3) { double tx = -Math.Sin(ang), ty = Math.Cos(ang); sx = Lerp(dy, tx * r * 0.02 + dy * (1 - nodeW), nodeW); sy = Lerp(dz, ty * r * 0.02 + dz * (1 - nodeW), nodeW); }
                // One axial octave (wood uses a similar Perlin budget); sheath boost keeps VB needles readable.
                double fibreStreak = (0.5 + 0.5 * Perlin(x * 0.008, sx * 12 + ox, sy * 12)) * (0.16 + 0.08 * sheath) * longKeep;
                cr *= 1 - fibreStreak; cg *= 1 - fibreStreak; cb *= 1 - fibreStreak;
            }
            double sheathMix = Math.Min(1, Math.Pow(sheath, 0.55) * (0.82 + 0.18 * cross + 0.35 * longKeep));
            cr = Lerp(cr, Pal.Late[0], sheathMix); cg = Lerp(cg, Pal.Late[1], sheathMix); cb = Lerp(cb, Pal.Late[2], sheathMix);
            if (cross > 0.12 && lumen > 0)
            {
                double lt = lumen * Math.Max(sheath, 0.35) * cross * 0.98;
                cr = Lerp(cr, Pal.E1[0], lt); cg = Lerp(cg, Pal.E1[1], lt); cb = Lerp(cb, Pal.E1[2], lt);
            }
            double cortex = SmoothStep(0.91, 0.97, tc);
            if (cortex > 0)
            {
                double ct = 0.40 * cortex * Math.Max(sheath, 0.45);
                cr = Lerp(cr, Pal.Late[0], ct); cg = Lerp(cg, Pal.Late[1], ct); cb = Lerp(cb, Pal.Late[2], ct);
            }
            double skin = r > ro ? 1 : SmoothStep(0.22, 0.0, ro - r);
            if (skin > 1e-3)
            {
                // Two Perlin hits for cured epidermis (was six); silica stays a cheap integer hash.
                double fAng = ang + nodeW * Perlin(x * 0.05 + ox, ang * 3.5, oy);
                double grain = 0.22 * Perlin(x * 0.0035, fAng * 90 + ox, oy) + 0.12 * Perlin(x * 0.012, fAng * 40 + oy, ox);
                double weather = 0.20 * Perlin(x * 0.025, ang * 2.2 + oy, ox * 0.7);
                double silica = SmoothStep(0.82, 0.99, FastHash01((int)Math.Floor(x * 4.0), (int)Math.Floor(ang * 64), ox));
                double lift = 1.0 + grain + weather + 0.10 * silica;
                cr = Lerp(cr, F.Skin[0] * lift, skin);
                cg = Lerp(cg, F.Skin[1] * (1.0 + grain * 0.92 + weather * 0.88 + 0.07 * silica), skin);
                cb = Lerp(cb, F.Skin[2] * (1.0 + grain * 0.85 + weather * 0.80 + 0.05 * silica), skin);
            }
            if (nodeHalo > 0)
            {
                double band = Math.Max(skin, 0.65 * longKeep + 0.45);
                double lipT = 0.42 * nodeHalo * (1 - nodeCore) * band, ringT = (0.80 * nodeCore + 0.34 * nodeHalo) * band;
                cr = Lerp(cr, Math.Min(255, F.Skin[0] * 1.12), lipT); cg = Lerp(cg, Math.Min(255, F.Skin[1] * 1.08), lipT); cb = Lerp(cb, Math.Min(255, F.Skin[2] * 1.05), lipT);
                cr = Lerp(cr, F.Skin[0] * 0.52, ringT); cg = Lerp(cg, F.Skin[1] * 0.45, ringT); cb = Lerp(cb, F.Skin[2] * 0.33, ringT);
            }
        }
        return (cr * tint, cg * tint, cb * tint);
    }

    static double Fract(double t) => t - Math.Floor(t);

    static double FastHash01(int x, int y, double salt)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263);
            n ^= (uint)(salt * 4294967.0);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 0xffffff) * (1.0 / 16777215.0);
        }
    }
}
