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
    public double KnotInfluence(double x, double y, double z) => 0;

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
/// Bamboo culm: hollow internodes, discrete vascular bundles (dense small outer → sparse large inner), thin epidermis, node ridges, diaphragms.
/// Bundle field is cellular in wall-polar space (Dixon/Xu/Shang/Tsuyama-guided); not annual rings. Nodes from <see cref="NaturalFeatures"/>.
/// </summary>
public sealed class BambooMaterialModel : NaturalModel
{
    public BambooMaterialModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm) : base(features, palette, widthMm, depthMm) { }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end)
    {
        double v = F.Variation, ox = OffsetX, oy = OffsetY, ri = F.Inner(x), ro = F.Outer(x);
        F.NearNode(x, out double nd);
        double half = Math.Max(1.2, F.RidgeHalfWidth);
        double ang0 = Math.Atan2(dz, dy);
        // Peng μCT node stack ~12.9 mm axial: reorient + darken across that band.
        double nodeHalf = Math.Max(half, 6.5);
        double ndAdj = double.IsInfinity(nd) ? double.PositiveInfinity
            : Math.Abs(nd + 0.9 * Perlin(ang0 * 2.4 + ox, oy * 0.35, 1.7));
        double nodeCore = double.IsInfinity(ndAdj) ? 0 : Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 0.22, 0.8), 2));
        double nodeHalo = double.IsInfinity(ndAdj) ? 0 : Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 0.55, 2.0), 2));
        double nodeW = Math.Max(nodeCore, 0.7 * nodeHalo);
        double[] c;
        if (r < ri - 1e-3)
        {
            bool disc = nd <= F.Diaphragm / 2 + 1e-3;
            double cloud = 0.5 + 0.5 * Perlin(dy * 0.08 + ox, dz * 0.08 + oy, disc ? 0.5 : x * 0.02);
            c = Lerp3(Pal.E1, Pal.E0, disc ? 0.35 * cloud : 0.12 * cloud);
        }
        else
        {
            double wall = Math.Max(ro - ri, 1e-6), tc = Math.Clamp((r - ri) / wall, 0, 1);
            double cross = Math.Max(end, nodeW * 0.85);
            double ang = ang0;
            // Peng: rotate packing toward tangential near the node.
            double arc = (ang + nodeW * 0.55 * Perlin(x * 0.06, ang * 4 + ox, oy)) * 0.5 * (ri + ro);
            // Shang area 0.194→0.061 mm²; Speichinger density rises outer then soft drop past t≈0.8.
            double areaMm2 = Lerp(0.194, 0.061, Math.Pow(tc, 0.9));
            double spacing = Math.Max(0.34, Math.Sqrt(areaMm2) * Lerp(2.6, 0.55, Math.Pow(tc, 1.25)));
            spacing *= 1 + 0.04 * v * Perlin(x * 0.01, ox, oy);
            double survive = SmoothStep(0.08, 0.28, tc) * (0.18 + 0.82 * Math.Pow(Math.Max(0, (Math.Exp(2.8 * tc) - 1) / (Math.Exp(2.8) - 1)), 0.95));
            // Speichinger empty pith (~5.6%) and skin (~1.0%) — no VBs.
            survive *= SmoothStep(0.056, 0.10, tc) * (1.0 - SmoothStep(0.97, 0.995, tc));
            if (tc > 0.8) survive *= Lerp(1.0, 0.72, (tc - 0.8) / 0.2);
            double aspect = Lerp(1.55, 0.48, Math.Pow(tc, 1.0));
            double clover = Math.Pow(1 - tc, 1.15);
            var (sheath, lumen) = BundleField(arc / spacing, (r - ri) / spacing,
                Lerp(x / (spacing * 36), x / spacing, cross), ox, oy, aspect, survive, clover, ox);
            // Dixon Eq. 3.6: S_f rises outward — fade pale vessel lumens.
            double sf = 0.226 * tc + 0.710;
            lumen *= Math.Clamp(1.12 - sf, 0, 1);
            double longKeep = 1 - cross;
            // Dixon-ish density albedo: pale foam inner → denser outer parenchyma.
            double rho = Math.Pow(tc, 1.55);
            c = Lerp3(Pal.E0, Pal.E1, 0.05 + 0.18 * (1 - tc));
            for (int k = 0; k < 3; k++) c[k] *= Lerp(1.04, 0.90, rho);
            c = Lerp3(c, F.Skin, 0.04);
            // Ahvenainen: soft parenchyma mottling (high MFA) vs sharp sheath needles (low MFA).
            if (longKeep > 0.2) {
                double brick = Math.Abs(Fract(x * 0.55 + ox) - 0.5) * Math.Abs(Fract(arc * 0.9 + oy) - 0.5);
                brick = SmoothStep(0.18, 0.02, brick);
                double softPar = (0.5 + 0.5 * Perlin(x * 0.03, dy * 1.2 + ox, dz * 1.2)) * 0.04 * longKeep * (1 - sheath);
                for (int k = 0; k < 3; k++) c[k] *= 1 - 0.045 * brick * longKeep - softPar;
            }
            // Tangential blend of streak axes near node (Peng reorientation).
            double tx = -Math.Sin(ang), ty = Math.Cos(ang);
            double sx = Lerp(dy, tx * r * 0.02 + dy * (1 - nodeW), nodeW);
            double sy = Lerp(dz, ty * r * 0.02 + dz * (1 - nodeW), nodeW);
            double fibreStreak =
                (0.5 + 0.5 * Perlin(x * 0.004, sx * 4.2 + ox, sy * 4.2)) * 0.10 * longKeep +
                (0.5 + 0.5 * Perlin(x * 0.012, sx * 11 + ox, sy * 11)) * 0.07 * longKeep +
                (0.5 + 0.5 * Perlin(x * 0.035, sx * 22 + ox, sy * 22)) * 0.05 * longKeep * Math.Max(sheath, 0.25);
            for (int k = 0; k < 3; k++) c[k] *= 1 - fibreStreak;
            c = Lerp3(c, Pal.Late, Math.Min(1, Math.Pow(sheath, 0.85) * (0.62 + 0.38 * cross + 0.35 * longKeep)));
            if (cross > 0.12 && lumen > 0)
                c = Lerp3(c, Pal.E1, lumen * cross * 0.95);
            double cortex = SmoothStep(0.91, 0.97, tc);   // stop before VB-free skin band
            if (cortex > 0) c = Lerp3(c, Pal.Late, 0.40 * cortex * Math.Max(sheath, 0.45));
            double skin = SmoothStep(0.22, 0.0, ro - r);
            double twist = nodeW * (1.0 + 0.55 * Perlin(ang * 3.1 + ox, oy, 2.2));
            double fAng = ang + twist * Perlin(x * 0.05 + ox, ang * 3.5, oy);
            double fibre = 0.5 + 0.5 * Perlin(x * 0.0008, fAng * 78 + ox, oy);
            double fibre2 = 0.5 + 0.5 * Perlin(x * 0.00045 + 2.7, fAng * 28 + oy, ox);
            double needle = 0.5 + 0.5 * Perlin(x * 0.0014, fAng * 140 + ox * 0.5, oy);
            double mottling = 0.5 + 0.5 * Perlin(x * 0.0018, ang * 1.9 + ox * 0.3, oy);
            double silica = SmoothStep(0.92, 0.995, Hash01((int)Math.Floor(x * 2.5), (int)Math.Floor(ang * 40), 3, ox));
            var sk = new[] {
                F.Skin[0] * (0.72 + 0.14 * fibre + 0.07 * fibre2 + 0.07 * needle + 0.04 * mottling + 0.06 * silica),
                F.Skin[1] * (0.73 + 0.13 * fibre + 0.06 * fibre2 + 0.06 * needle + 0.04 * mottling + 0.05 * silica),
                F.Skin[2] * (0.70 + 0.13 * fibre + 0.06 * fibre2 + 0.06 * needle + 0.04 * mottling + 0.04 * silica) };
            c = Lerp3(c, sk, skin);
            if (nodeHalo > 0) {
                var ring = new[] { F.Skin[0] * 0.52, F.Skin[1] * 0.45, F.Skin[2] * 0.33 };
                var lip = new[] { Math.Min(255, F.Skin[0] * 1.12), Math.Min(255, F.Skin[1] * 1.08), Math.Min(255, F.Skin[2] * 1.05) };
                double onSkin = Math.Max(skin, 0.55);
                c = Lerp3(c, lip, 0.42 * nodeHalo * (1 - nodeCore) * onSkin);
                c = Lerp3(c, ring, (0.80 * nodeCore + 0.34 * nodeHalo) * onSkin);
            }
        }
        return (c[0] * F.Tint, c[1] * F.Tint, c[2] * F.Tint);
    }

    // Flower-shaped VB: fibre caps + two metaxylem vessel lumens (Grosser Type I / Dixon clover→skull).
    // Cells with Hash > survive are skipped. clover=1 → four caps; clover=0 → two outer caps.
    static (double Sheath, double Lumen) BundleField(double u, double v, double w, double ox, double oy, double aspect, double survive, double clover, double salt)
    {
        int iu = (int)Math.Floor(u), iv = (int)Math.Floor(v), iw = (int)Math.Floor(w);
        double su = Math.Max(0.55, Math.Sqrt(aspect)), sv = Math.Max(0.55, 1 / Math.Sqrt(aspect)), sw = 0.18;
        double best = 9, blu = 0, blv = 0, blw = 0;
        for (int dw = -1; dw <= 1; dw++)
        for (int dv = -1; dv <= 1; dv++)
        for (int du = -1; du <= 1; du++)
        {
            int cx = iu + du, cy = iv + dv, cz = iw + dw;
            if (Hash01(cx, cy + 41, cz, salt) > survive) continue;
            double jx = Hash01(cx, cy, cz, ox), jy = Hash01(cx + 19, cy - 7, cz, oy), jz = Hash01(cx - 3, cy + 11, cz, ox + oy);
            double lu = u - (cx + jx), lv = v - (cy + jy), lw = w - (cz + jz);
            double d = Math.Sqrt(Math.Pow(lu * su, 2) + Math.Pow(lv * sv, 2) + Math.Pow(lw * sw, 2));
            if (d < best) { best = d; blu = lu; blv = lv; blw = lw; }
        }
        if (best > 8) return (0, 0);
        // Central fibre core + sclerenchyma lobes (Max keeps gaps between bundles).
        double fibre = SoftCap(blu, blv, blw, 0.0, 0.0, su, sv, sw) * 0.75;
        fibre = Math.Max(fibre, Math.Max(SoftCap(blu, blv, blw, 0.0, 0.22, su, sv, sw), SoftCap(blu, blv, blw, 0.0, -0.22, su, sv, sw)));
        if (clover > 0.05)
            fibre = Math.Max(fibre, clover * Math.Max(SoftCap(blu, blv, blw, 0.22, 0.0, su, sv, sw), SoftCap(blu, blv, blw, -0.22, 0.0, su, sv, sw)));
        // Two metaxylem vessels (pale); larger when clover high (inner wall).
        double vs = Lerp(0.09, 0.15, clover);
        double lumen = Math.Max(
            SoftCap(blu, blv, blw, vs, 0.0, su, sv, sw * 1.1),
            SoftCap(blu, blv, blw, -vs, 0.0, su, sv, sw * 1.1));
        double sheath = Math.Min(1, fibre);
        return (sheath, lumen * SmoothStep(0.50, 0.12, best));
    }

    static double SoftCap(double lu, double lv, double lw, double cu, double cv, double su, double sv, double sw)
    {
        double d = Math.Sqrt(Math.Pow((lu - cu) * su, 2) + Math.Pow((lv - cv) * sv, 2) + Math.Pow(lw * sw, 2));
        return SmoothStep(0.20, 0.05, d);
    }

    static double Fract(double t) => t - Math.Floor(t);

    static double Hash01(int x, int y, int z, double salt) =>
        Perlin(x * 0.173 + salt, y * 0.311 - salt * 0.2, z * 0.271 + salt * 0.07) * 0.5 + 0.5;
}
