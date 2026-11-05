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
        else { nx = ny = nz = 0; }
        var c = Shade(Math.Clamp(x, 0, F.Length), r, dy, dz, end, nx, ny, nz);
        double g = Pal.Gain, mx = Math.Max(c.R, Math.Max(c.G, c.B));
        if (mx > Pal.Cap) { c.R *= Pal.Cap / mx; c.G *= Pal.Cap / mx; c.B *= Pal.Cap / mx; }
        return (255 << 24) | (Channel(c.R * g * Pal.GR) << 16) | (Channel(c.G * g * Pal.GG) << 8) | Channel(c.B * g * Pal.GB);
    }

    static int Channel(double v) => double.IsNaN(v) ? 255 : (int)Math.Max(0, Math.Min(255, v));
    protected double[] Lerp3(double[] a, double[] b, double t) => new[] { Lerp(a[0], b[0], t), Lerp(a[1], b[1], t), Lerp(a[2], b[2], t) };

    /// <param name="nx">Oriented surface normal (unit-ish); zeros when Sample was unoriented.</param>
    protected abstract (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end, double nx, double ny, double nz);
}

/// <summary>Softwood stem: growth rings parallel to the surface, bounded noise, knots and an outer bark region.</summary>
public sealed class LogMaterialModel : NaturalModel
{
    public LogMaterialModel(NaturalFeatures features, TimberPalette palette, double widthMm, double depthMm) : base(features, palette, widthMm, depthMm) { }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end, double nx, double ny, double nz)
    {
        _ = (nx, ny, nz);
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
        dx = 1 - 0.70 * nodeW;
        dy = tx * 0.95 * nodeW;
        dz = ty * 0.95 * nodeW;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-9) { dx = dy = dz = 0; return false; }
        dx /= len; dy /= len; dz /= len;
        return r <= F.Outer(x) + F.Ridge(x) + 1e-3;
    }

    protected override (double R, double G, double B) Shade(double x, double r, double dy, double dz, double end, double nx, double ny, double nz)
    {
        // Scalar RGB; atlas raster hits this millions of times. Side streaks = same SampleBundle as end grain.
        double ox = OffsetX, oy = OffsetY, ri = F.Inner(x), ro = F.Outer(x), tint = F.Tint;
        int nearIx = F.NearNode(x, out double nd);
        double ndSigned = nearIx >= 0 ? x - F.Nodes[nearIx] : 0;
        double ang = Math.Atan2(dz, dy);
        double nodeHalf = Math.Max(Math.Max(1.2, F.RidgeHalfWidth), 6.5);
        double nodeW = 0, nodeCore = 0, nodeHalo = 0;
        if (!double.IsInfinity(nd) && nd < nodeHalf * 3.5)
        {
            double ndAdj = Math.Abs(nd + 1.35 * Perlin(ang * 3.1 + ox, oy * 0.4, 1.7) + 0.55 * Perlin(ang * 7.2 + oy, ox * 0.5, 2.3));
            // Wider halo: photo pigment fills the ends of each internode, not only the ridge crease.
            nodeCore = Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 0.42, 1.6), 2));
            nodeHalo = Math.Exp(-0.5 * Math.Pow(ndAdj / Math.Max(nodeHalf * 1.45, 6.0), 2));
            nodeW = Math.Max(nodeCore, 0.75 * nodeHalo);
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
            // Peng: θ-warp near nodes only — keep SampleBundle θ aligned with vol-compare / end grain.
            double angWarp = nodeW > 1e-3 ? nodeW * 1.25 * Perlin(x * 0.05, ang * 5 + ox, oy) : 0;
            // Query cortex just inside Speichinger skin cull so outer surface still sees VBs as streaks.
            double rBundle = Math.Min(r, Math.Max(ri + 0.06 * wall, ro - 0.22));
            var (sheath, lumen) = F.SampleBundle(ri, ro, rBundle, ang, angWarp);
            double tcBundle = Math.Clamp((rBundle - ri) / wall, 0, 1);
            lumen *= Math.Clamp(1.12 - (0.226 * tcBundle + 0.710), 0, 1);
            double longKeep = 1 - cross, rho = Math.Pow(tcBundle, 1.55), shade = Lerp(1.02, 0.95, rho);
            double baseT = 0.05 + 0.18 * (1 - tcBundle);
            cr = Lerp(Pal.E0[0], Pal.E1[0], baseT) * shade; cg = Lerp(Pal.E0[1], Pal.E1[1], baseT) * shade; cb = Lerp(Pal.E0[2], Pal.E1[2], baseT) * shade;
            cr = Lerp(cr, F.Skin[0], 0.04); cg = Lerp(cg, F.Skin[1], 0.04); cb = Lerp(cb, F.Skin[2], 0.04);
            // Low-freq + per-internode honey↔umber (cured pole variance, not grit).
            if (longKeep > 0.15)
            {
                double seg = Math.Floor(x / Math.Max(F.Spacing * 0.85, 80.0));
                double mottle = (0.14 * Perlin(x * 0.0018, ang * 0.45 + oy, ox * 0.4)
                    + 0.10 * Perlin(seg * 0.71 + ox, oy * 0.3, 4.2)) * longKeep;
                cr = Lerp(cr, Pal.Late[0] * 0.86, Math.Max(0, mottle));
                cg = Lerp(cg, Pal.Late[1] * 0.88, Math.Max(0, mottle));
                cb = Lerp(cb, Pal.Late[2] * 0.92, Math.Max(0, mottle));
                cr = Lerp(cr, Math.Min(255, F.Skin[0] * 1.09), Math.Max(0, -mottle));
                cg = Lerp(cg, Math.Min(255, F.Skin[1] * 1.07), Math.Max(0, -mottle));
                cb = Lerp(cb, Math.Min(255, F.Skin[2] * 1.05), Math.Max(0, -mottle));
            }
            // Ahvenainen: soft parenchyma mottling; sharp sheath streaks.
            if (longKeep > 0.2)
            {
                double softPar = (0.5 + 0.5 * Perlin(x * 0.025, dy + ox, dz)) * 0.36 * longKeep * (1 - sheath);
                cr = Lerp(cr, Pal.E0[0] * 0.93, softPar); cg = Lerp(cg, Pal.E0[1] * 0.94, softPar); cb = Lerp(cb, Pal.E0[2] * 0.95, softPar);
            }
            if (longKeep > 0.12)
            {
                double sx = dy, sy = dz;
                if (nodeW > 1e-3)
                {
                    double tx = -Math.Sin(ang), ty = Math.Cos(ang);
                    sx = Lerp(dy, tx * r * 0.06 + dy * (1 - nodeW), nodeW);
                    sy = Lerp(dz, ty * r * 0.06 + dz * (1 - nodeW), nodeW);
                }
                double fibreT = (0.5 + 0.5 * Perlin(x * 0.0035, sx * 5 + ox, sy * 5)) * 0.30 * longKeep * Math.Max(sheath, 0.12);
                cr = Lerp(cr, Pal.Late[0] * 1.08, fibreT); cg = Lerp(cg, Pal.Late[1] * 1.06, fibreT); cb = Lerp(cb, Pal.Late[2] * 1.04, fibreT);
            }
            // Sheath → Late amber (end grain flowers + long-grain identity).
            double sheathMix = Math.Min(1, Math.Pow(sheath, 0.50) * (0.42 + 0.34 * cross + 0.24 * longKeep));
            cr = Lerp(cr, Pal.Late[0], sheathMix); cg = Lerp(cg, Pal.Late[1], sheathMix); cb = Lerp(cb, Pal.Late[2], sheathMix);
            if (cross > 0.12 && lumen > 0)
            {
                double lt = lumen * Math.Max(sheath, 0.35) * cross * 0.50;
                cr = Lerp(cr, Pal.E1[0], lt); cg = Lerp(cg, Pal.E1[1], lt); cb = Lerp(cb, Pal.E1[2], lt);
            }
            // Thin wax/silica epidermis (~0.4 mm) + volumetric sheath streaks (no Fract hatch).
            double skin = r > ro ? 1 : SmoothStep(0.45, 0.0, ro - r);
            if (skin > 1e-3)
            {
                // Near-base Skin mottling (low amp — sheath carries most structure).
                double blotch = 0.08 * Perlin(x * 0.0035, ang * 1.3 + oy, ox * 0.5);
                double skinT = Math.Clamp(0.5 + blotch, 0, 1);
                double sr = Lerp(F.Skin[0] * 0.94, F.Skin[0] * 1.06, skinT);
                double sg = Lerp(F.Skin[1] * 0.95, F.Skin[1] * 1.04, skinT);
                double sb = Lerp(F.Skin[2] * 0.96, F.Skin[2] * 1.03, skinT);
                // Side: limb wax (θ-smooth) so sheath↔darkening stays measurable; end cuts may use oriented N.
                double limbN = Math.Max(0, 1.0 - Math.Pow(Math.Abs(dy) / Math.Max(ro, 1e-6), 2));
                double ndot = limbN;
                if (longKeep < 0.45 && nx * nx + ny * ny + nz * nz > 1e-6)
                {
                    const double lx = 0.35, ly = 0.55, lz = 0.76;
                    ndot = Math.Max(ndot, nx * lx + ny * ly + nz * lz);
                }
                double wax = 1.0 + 0.13 * ndot;
                sr *= wax; sg *= wax; sb *= wax;
                cr = Lerp(cr, sr, skin); cg = Lerp(cg, sg, skin); cb = Lerp(cb, sb, skin);
                // Sheath-driven amp (continuity). Mid still darker than Skin; nodes toast deeper (photo wick).
                double irreg = 0.85 + 0.15 * Perlin(x * 0.009, ang * 1.7 + ox, oy);
                double wick = 0.75 + 1.25 * nodeHalo + 0.55 * nodeCore;
                double strandT = Math.Pow(Math.Max(sheath, 0), 0.58) * wick * irreg * Math.Max(skin, 0.45 * longKeep);
                double toast = Math.Clamp(0.20 + 0.80 * nodeHalo + 0.35 * nodeCore, 0, 1);
                double dr = Lerp(F.Skin[0] * 0.78, F.Skin[0] * 0.48, toast);
                double dg = Lerp(F.Skin[1] * 0.76, F.Skin[1] * 0.45, toast);
                double db = Lerp(F.Skin[2] * 0.72, F.Skin[2] * 0.40, toast);
                dr = Lerp(dr, Pal.Late[0], 0.30); dg = Lerp(dg, Pal.Late[1], 0.30); db = Lerp(db, Pal.Late[2], 0.30);
                cr = Lerp(cr, dr, strandT); cg = Lerp(cg, dg, strandT); cb = Lerp(cb, db, strandT);
                // Satin limb on epidermis (θ-smooth); photo center highlight.
                double sheen = 1.0 + 0.08 * ndot * skin;
                cr *= sheen; cg *= sheen; cb *= sheen;
            }
            else if (longKeep > 0.2 && sheath > 1e-3)
            {
                double irreg = 0.80 + 0.20 * Perlin(x * 0.009, ang * 1.7 + ox, oy);
                double strandT = Math.Pow(sheath, 0.62) * 0.58 * longKeep * irreg * (0.55 + 1.4 * nodeHalo);
                cr = Lerp(cr, Pal.Late[0], strandT * 0.72); cg = Lerp(cg, Pal.Late[1], strandT * 0.72); cb = Lerp(cb, Pal.Late[2], strandT * 0.72);
            }
            if (nodeHalo > 0)
            {
                double band = Math.Max(skin, 0.65 * longKeep + 0.45);
                // Photo: whole internode ends toast toward umber (θ-smooth — does not fight sheath↔side correlation).
                double axialToast = 0.68 * nodeHalo * band;
                cr = Lerp(cr, F.Skin[0] * 0.50, axialToast); cg = Lerp(cg, F.Skin[1] * 0.48, axialToast); cb = Lerp(cb, F.Skin[2] * 0.42, axialToast);
                // Soft Skin lip + darker ring; basal whiskers stand in for sheath-scar fringe (shader-only).
                double lipT = 0.38 * nodeHalo * (1 - nodeCore) * band, ringT = (0.55 * nodeCore + 0.28 * nodeHalo) * band;
                cr = Lerp(cr, Math.Min(255, F.Skin[0] * 1.06), lipT); cg = Lerp(cg, Math.Min(255, F.Skin[1] * 1.04), lipT); cb = Lerp(cb, Math.Min(255, F.Skin[2] * 1.03), lipT);
                cr = Lerp(cr, F.Skin[0] * 0.66, ringT); cg = Lerp(cg, F.Skin[1] * 0.64, ringT); cb = Lerp(cb, F.Skin[2] * 0.58, ringT);
                if (skin > 0.05 && !double.IsInfinity(nd))
                {
                    // Match FringeBump density: dark basal brush on the face + ridge lip.
                    double onRidge = r > ro ? SmoothStep(0.0, Math.Max(F.Ridge(x), 0.4), r - ro) : 0;
                    double bump = F.FringeBump(x, ang);
                    double fringe = SmoothStep(0.15, 1.2, bump) * Math.Max(skin, 0.55) * (0.65 + 0.35 * onRidge);
                    // Extra face comb when bump is weak (inside silhouette) but still near the scar.
                    double nAng = ang * 96 + ox;
                    double facePick = SmoothStep(0.18, 0.42, 0.5 + 0.5 * Perlin(nAng, Math.Floor(nAng), oy));
                    double whisk = SmoothStep(18.0, 0.3, Math.Abs(nd));
                    double basal = SmoothStep(2.0, -16.0, ndSigned);
                    fringe = Math.Max(fringe, facePick * whisk * (0.15 + 0.85 * basal) * 0.9 * skin);
                    cr = Lerp(cr, F.Skin[0] * 0.26, fringe); cg = Lerp(cg, F.Skin[1] * 0.24, fringe); cb = Lerp(cb, F.Skin[2] * 0.18, fringe);
                }
            }
        }
        return (cr * tint, cg * tint, cb * tint);
    }
}
