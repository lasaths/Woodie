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
    readonly double cy, cz, sy, sz, ox, oy;

    protected NaturalModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm)
    {
        F = features; Pal = palette; cy = widthMm / 2; cz = depthMm / 2; sy = 1 + F.Ovality; sz = 1 - F.Ovality;
        ox = F.OffsetX * F.Variation; oy = F.OffsetY * F.Variation;   // variation 0: seed-independent noise phase
    }

    protected double OffsetX => ox;
    protected double OffsetY => oy;

    public int Sample(double x, double y, double z) => Sample(x, y, z, 0, 0, 0, false);
    public int Sample(double x, double y, double z, double nx, double ny, double nz) => Sample(x, y, z, nx, ny, nz, true);

    int Sample(double x, double y, double z, double nx, double ny, double nz, bool oriented)
    {
        double dy = (y - cy) / sy, dz = (z - cz) / sz, r = Math.Sqrt(dy * dy + dz * dz);
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
/// Moso culm: hollow internodes, a graded vascular-bundle wall, external node ridges and solid diaphragms. Node positions
/// come from <see cref="NaturalFeatures"/>, shared with the geometry. Node banding and colour are appearance adaptations.
/// </summary>
public sealed class BambooMaterialModel : NaturalModel
{
    static readonly double[] Skin = { 112, 128, 74 };

    public BambooMaterialModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm) : base(features, palette, widthMm, depthMm) { }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end)
    {
        double v = F.Variation, ox = OffsetX, oy = OffsetY, ri = F.Inner(x), ro = F.Outer(x);
        F.NearNode(x, out double nd);
        double nodeW = double.IsInfinity(nd) ? 0 : 1 - SmoothStep(0, 2 * F.RidgeHalfWidth, nd);
        double[] c;
        if (r < ri - 1e-3)
        {
            // Open hollow (only inner surfaces are ever sampled) or a solid diaphragm across it.
            bool disc = nd <= F.Diaphragm / 2 + 1e-3;
            double cloud = 0.5 + 0.5 * Perlin(dy * 0.08 + ox, dz * 0.08 + oy, disc ? 0.5 : x * 0.02);
            c = Lerp3(Pal.E1, Pal.E0, disc ? 0.35 * cloud : 0.2 * cloud);
        }
        else
        {
            double t = Math.Max(0, (r - ri) / Math.Max(ro - ri, 1e-6)), tc = Math.Min(t, 1);
            double ef = Math.Max(end, nodeW);   // nodes cross-link the fibres, so they read like an end grain
            double p = Perlin(dy * 0.35 + ox, dz * 0.35 + oy, x * Lerp(0.01, 0.35, ef)) * 0.5 + 0.5;
            double thr = 0.72 - 0.45 * tc - 0.12 * nodeW;
            double bundle = SmoothStep(thr, thr + 0.2, p) * (0.25 + 0.75 * tc);
            c = Lerp3(Lerp3(Pal.E1, Pal.E0, SmoothStep(0, 0.4, t)), Pal.Late, bundle);
            if (nodeW > 0) c = Lerp3(c, Pal.Knot, 0.5 * nodeW);
            double skin = SmoothStep(0.86, 0.97, t);
            if (skin > 0)
            {
                double streak = 1 + v * 0.1 * Perlin(dy * 0.05 + ox, dz * 0.05, x * 0.002);
                var sk = Lerp3(Skin, Pal.Knot, 0.6 * nodeW);
                c = Lerp3(c, new[] { sk[0] * streak, sk[1] * streak, sk[2] * streak }, skin);
            }
        }
        return (c[0] * F.Tint, c[1] * F.Tint, c[2] * F.Tint);
    }
}
