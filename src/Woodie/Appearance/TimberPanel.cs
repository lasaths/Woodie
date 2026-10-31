using System;
using System.Collections.Generic;
using System.Linq;
using Woodie.Stock;

namespace Woodie.Appearance
{
// Independent C# implementation inspired by Larsson et al. 2022, "Procedural Texturing of Solid Wood with Knots" (ACM TOG 41(4)); noise after Perlin 2002, "Improving Noise". See README.md and docs/appearance.md.
// Cut-aware detail is an independent
// adaptation informed by Larsson et al. 2024 and Mokume 2025. See docs/appearance.md.
public class TimberPanel : IMaterialModel {
  // Colours and growth samples are values: no allocation for every texel.
  struct Rgb {
    double r, g, b;
    public double this[int index] {
      [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
      get => index == 0 ? r : index == 1 ? g : b;
      [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
      set { if (index == 0) r = value; else if (index == 1) g = value; else b = value; }
    }
  }
#pragma warning disable CS0649   // tv, tw, yel stay 0 (see NOTE below)
  class Seg { public double u0, u1, pv, pw, tint, tv, tw, yel, R0, detail; public int seed; public double[] ku, ka, ks; }
#pragma warning restore CS0649
  class Lam { public double c0, c1; public List<Seg> segs = new List<Seg>(); }
  public TimberProduct Prod; public TimberPalette Pal; double L, W, H, RS = 3.5; List<TimberLayer> layers; List<Lam>[] lams;

  public TimberPanel(TimberProduct prod, double L, double W, double H, int seed, double KD = 3, TimberPalette pal = null, double boardWidth = double.NaN, double veneerThickness = double.NaN, IReadOnlyList<TimberLayer> resolvedLayers = null) {
    Prod = prod; this.L = L; this.W = W; this.H = H; Pal = pal ?? TimberPalette.For(prod); RS = Pal.RS;
    layers = resolvedLayers?.ToList() ?? TimberLayups.Make(prod, H, W, boardWidth, veneerThickness); lams = new List<Lam>[layers.Count]; var rnd0 = new Random(seed);
    for (int i = 0; i < layers.Count; i++) {
      var ly = layers[i]; double T = ly.z1 - ly.z0, across = ly.alongX ? W : L, len = ly.alongX ? L : W; lams[i] = new List<Lam>();
      int j = 0;
      for (double c = 0; c < across; ) {
        var rnd = new Random(unchecked(seed * 7919 + i * 1009 + j++ * 131));   // v2.3: per-board seed -> boards stable when W/L change
        double bw = ly.continuous ? across : ly.veneer ? ly.bw * (0.8 + 0.4 * rnd.NextDouble()) : ly.bw;
        var lm = new Lam { c0 = c, c1 = Math.Min(across, c + bw) }; c = lm.c1;
        bool unbroken = ly.continuous || ly.continuousLength;
        double segmentMin = unbroken ? len + 2 : ly.segMin, segmentMax = unbroken ? len + 2 : ly.segMax;
        for (double u = unbroken ? -1 : -rnd.NextDouble() * ly.segMax; u < len; ) {
          var s = new Seg { u0 = u, u1 = u + segmentMin + rnd.NextDouble() * (segmentMax - segmentMin), seed = rnd.Next(1 << 20) }; var r = new Random(s.seed);
          // Independent integer hash: no draws from the calibrated board/knot sequence.
          uint detail = unchecked((uint)s.seed * 747796405u + 2891336453u);
          detail = ((detail >> (int)((detail >> 28) + 4)) ^ detail) * 277803737u;
          s.detail = ((detail >> 22) ^ detail) / (double)uint.MaxValue * 2 - 1;
          double side = ly.veneer ? 30 : (ly.bw > 300 ? Math.Min(ly.bw, T) : ly.bw);
          s.pv = (r.NextDouble() - 0.5) * side * 1.2;
          s.pw = (r.NextDouble() < 0.5 ? -1 : 1) * (T * 0.5 + 40 + r.NextDouble() * 200);
          if (ly.solidPith) { s.pv = (r.NextDouble() - 0.5) * W * 0.3; s.pw = (r.NextDouble() < 0.5 ? -1 : 1) * (T * 0.5 + 5 + r.NextDouble() * 35); }  // pith just outside the lamella
          s.R0 = 80 + r.NextDouble() * 120;                      // veneer: peel radius
          double tr = r.NextDouble(); s.tint = 1 - Pal.TintSpread / 2 + tr * Pal.TintSpread - (tr < 0.25 ? Pal.DarkTail * (0.25 - tr) / 0.25 : 0);   // v2.2: widen lower side only
          // NOTE: the tuned v2 script never assigned the pith drift (tv, tw) or per-board yellow shift (yel) (the code sat behind a comment), so they stay 0 to keep the calibrated look and random sequence identical.
          if (ly.veneer) s.tint = 1 + (s.tint - 1) * 0.5;   // v2.4: veneer sheets differ less (halved tint spread between face sheets)
          double kd = (Prod == TimberProduct.ThreeS ? KD * 0.6 : KD) * Pal.KdK;   // graded face
          int nk = (int)Math.Round(kd * (s.u1 - s.u0) / 1000.0 * (0.5 + r.NextDouble()) * (ly.veneer ? 3 : 1));
          s.ku = new double[nk]; s.ka = new double[nk]; s.ks = new double[nk];
          for (int k = 0; k < nk; k++) { s.ku[k] = s.u0 + r.NextDouble() * (s.u1 - s.u0);
            s.ka[k] = ly.veneer ? r.NextDouble() * 2 * Math.PI : Math.Atan2(-s.pw, -s.pv) + (r.NextDouble() - 0.5) * 0.5; s.ks[k] = Pal.KsMin + r.NextDouble() * r.NextDouble() * 1.6; }
          lm.segs.Add(s); u = s.u1;
        }
        lams[i].Add(lm);
      }
    }
  }

  // Layup query: point -> board/segment, local fibre coords (u along fibre, v across in-plane, w across thickness), glue flag
  public int Col(double x, double y, double z) => Colour(x, y, z, -1, -1);
  public int Sample(double x, double y, double z) => Col(x, y, z);
  public int Sample(double x, double y, double z, double nx, double ny, double nz) => Col(x, y, z, nx, ny, nz);

  /// <summary>Soft volumetric knot proximity in 0..1 (halo around knots; not the hard body mask).</summary>
  public double KnotInfluence(double x, double y, double z) => SampleGrowth(x, y, z)?.Influence ?? 0;

  /// <summary>Unit fibre direction in material millimetres (stem along the board, diverted around knots).</summary>
  public bool TryFibreDirection(double x, double y, double z, out double dx, out double dy, out double dz)
  {
    dx = dy = dz = 0;
    if (!Locate(x, y, z, out var ly, out var lm, out var s, out double u, out double v, out double w, out bool ax)) return false;
    if (ly.veneer) { if (ax) { dx = 1; dy = 0; } else { dx = 0; dy = 1; } dz = 0; return true; }
    double dv = v - s.pv - s.tv * (u - s.u0), dw = w - s.pw - s.tw * (u - s.u0);
    double r = Math.Sqrt(dv * dv + dw * dw), ang = Math.Atan2(dw, dv);
    double fu = 1, fv = 0, fw = 0;
    for (int k = 0; k < s.ku.Length; k++) {
      double du = u - (s.ku[k] + 0.35 * r);
      double dt = Math.IEEERemainder(ang - s.ka[k], Math.PI * 2) * r;
      double kr = s.ks[k] * Math.Min(14, 2 + 0.03 * r), d = Math.Sqrt(du * du + dt * dt);
      if (d > 12 * kr) continue;
      // Soft wrap around the knot strand: divert in the cross-section, stronger beside the knot body.
      double soft = Math.Exp(-d / Math.Max(kr, 1e-6));
      double beside = dt / Math.Max(d, 1e-6);
      double ca = Math.Cos(s.ka[k]), sa = Math.Sin(s.ka[k]);
      fv += soft * beside * (-sa) * 2.2;
      fw += soft * beside * ca * 2.2;
      fu -= soft * 0.25;
    }
    double len = Math.Sqrt(fu * fu + fv * fv + fw * fw); if (len < 1e-9) return false;
    fu /= len; fv /= len; fw /= len;
    // (u,v,w) → material (x,y,z); v is across the lamella in the board plane.
    if (ax) { dx = fu; dy = fv; dz = fw; }
    else { dx = fv; dy = fu; dz = fw; }
    return true;
  }

  /// <summary>Fibre streamlines in material millimetres, clipped to an axis-aligned box (cut geometry, not full stock).</summary>
  public List<List<(double X, double Y, double Z)>> TraceFibreFlow(double x0, double x1, double y0, double y1, double z0, double z1, int across = 10, int through = 8, double stepMm = 8)
  {
    var flow = new List<List<(double X, double Y, double Z)>>();
    if (layers == null || layers.Count == 0 || !(stepMm > 0)) return flow;
    if (x1 - x0 < stepMm || y1 - y0 < 1 || z1 - z0 < 1) return flow;
    across = Math.Max(2, across); through = Math.Max(2, through);
    int maxSteps = Math.Max(8, (int)((x1 - x0) / stepMm) + 4);
    for (int iy = 0; iy < across; iy++) for (int iz = 0; iz < through; iz++) {
      double y = y0 + (iy + 0.5) * (y1 - y0) / across, z = z0 + (iz + 0.5) * (z1 - z0) / through;
      foreach (double sign in new[] { 1.0, -1.0 }) {
        var poly = new List<(double X, double Y, double Z)>();
        double x = sign > 0 ? x0 + stepMm * 0.5 : x1 - stepMm * 0.5;
        for (int n = 0; n < maxSteps; n++) {
          if (x < x0 || x > x1 || y < y0 || y > y1 || z < z0 || z > z1) break;
          poly.Add((x, y, z));
          if (!TryFibreDirection(x, y, z, out double dx, out double dy, out double dz)) break;
          x += sign * dx * stepMm; y += sign * dy * stepMm; z += sign * dz * stepMm;
        }
        if (poly.Count >= 3) flow.Add(poly);
      }
    }
    return flow;
  }

  /// <summary>Stem/knot skeleton plus board-domain fibre seeds (legacy helpers / checks).</summary>
  public (List<List<(double X, double Y, double Z)>> Skeleton, List<List<(double X, double Y, double Z)>> Flow) TraceFibreStrands(int across = 7, int through = 5, double stepMm = 12)
  {
    var skeleton = new List<List<(double X, double Y, double Z)>>();
    var flow = TraceFibreFlow(0, L, 0, W, 0, H, across, through, stepMm);
    if (layers == null || layers.Count == 0 || !(L > stepMm && W > 1 && H > 1)) return (skeleton, flow);
    for (int li = 0; li < layers.Count; li++) {
      var ly = layers[li]; bool ax = ly.alongX; double midW = 0.5 * (ly.z0 + ly.z1);
      foreach (var lm in lams[li]) {
        double midC = 0.5 * (lm.c0 + lm.c1);
        foreach (var s in lm.segs) {
          var pith = new List<(double X, double Y, double Z)>();
          for (double u = s.u0; u <= s.u1; u += stepMm) pith.Add(FromLocal(ax, u, midC + s.pv, midW + s.pw));
          pith.Add(FromLocal(ax, s.u1, midC + s.pv, midW + s.pw));
          if (pith.Count >= 2) skeleton.Add(pith);
          for (int k = 0; k < s.ku.Length; k++) {
            var knot = new List<(double X, double Y, double Z)>();
            double ca = Math.Cos(s.ka[k]), sa = Math.Sin(s.ka[k]);
            double reach = Math.Min(180, 40 + 80 * s.ks[k]);
            for (double rad = 0; rad <= reach; rad += stepMm * 0.75) {
              double u = s.ku[k] + 0.35 * rad;
              knot.Add(FromLocal(ax, u, midC + s.pv + rad * ca, midW + s.pw + rad * sa));
            }
            if (knot.Count >= 2) skeleton.Add(knot);
          }
        }
      }
    }
    return (skeleton, flow);
  }

  static (double X, double Y, double Z) FromLocal(bool alongX, double u, double c, double w) => alongX ? (u, c, w) : (c, u, w);

  bool Locate(double x, double y, double z, out TimberLayer ly, out Lam lm, out Seg s, out double u, out double v, out double w, out bool ax)
  {
    ly = default; lm = null; s = null; u = v = w = 0; ax = true;
    if (layers == null || layers.Count == 0) return false;
    int li = 0; while (li < layers.Count - 1 && z >= layers[li].z1) li++;
    ly = layers[li]; ax = ly.alongX; double c = ax ? y : x; u = ax ? x : y;
    var lay = lams[li]; if (lay.Count == 0) return false;
    int j = 0; while (j < lay.Count - 1 && c >= lay[j].c1) j++; lm = lay[j];
    v = c - 0.5 * (lm.c0 + lm.c1); w = z - 0.5 * (ly.z0 + ly.z1);
    double fc = ly.fingersThroughDepth ? z : c;
    double tri = ly.fingers ? Math.Abs(((fc / 6.0) % 1.0 + 1) % 1.0 - 0.5) * 2 - 0.5 : 0, uj = u - tri * 20;
    if (ly.veneer) uj = u - (w / 1.5) * 12;
    if (lm.segs.Count == 0) return false;
    int si = 0; while (si < lm.segs.Count - 1 && uj >= lm.segs[si].u1) si++; s = lm.segs[si];
    return true;
  }

  GrowthSample? SampleGrowth(double x, double y, double z)
  {
    if (!Locate(x, y, z, out var ly, out var lm, out var s, out double u, out double v, out double w, out _)) return null;
    if (ly.veneer) {
      double ox = s.seed % 97;
      double r = s.R0 + w + 2 * RS * Perlin(u * 0.0002, v * 0.01, ox) + 1.0 * RS * Perlin(u * 0.0005, v * 0.04, ox + 5) + u * 0.0015;
      return Growth(s, u, r, v / s.R0, v, w);
    }
    double dv = v - s.pv - s.tv * (u - s.u0), dw = w - s.pw - s.tw * (u - s.u0);
    return Growth(s, u, Math.Sqrt(dv * dv + dw * dw), Math.Atan2(dw, dv), v, w);
  }

  /// <summary>Sample using a geometric cut normal in panel material coordinates (not a lighting normal).</summary>
  /// <remarks>Normal length and winding do not affect colour. Invalid/zero normals use the position-only fallback.</remarks>
  public int Col(double x, double y, double z, double nx, double ny, double nz) {
    double scale = Math.Max(Math.Abs(nx), Math.Max(Math.Abs(ny), Math.Abs(nz)));
    if (!(scale > 0) || !double.IsFinite(scale)) return Col(x, y, z);
    nx /= scale; ny /= scale; nz /= scale;
    double length2 = nx * nx + ny * ny + nz * nz;
    return Colour(x, y, z, nx * nx / length2, ny * ny / length2);
  }

  int Colour(double x, double y, double z, double nx2, double ny2) {
    int li = 0; while (li < layers.Count - 1 && z >= layers[li].z1) li++;
    var ly = layers[li]; bool ax = ly.alongX; double c = ax ? y : x, u = ax ? x : y;
    var lay = lams[li]; int j = 0; while (j < lay.Count - 1 && c >= lay[j].c1) j++; var lm = lay[j];
    double v = c - 0.5 * (lm.c0 + lm.c1), w = z - 0.5 * (ly.z0 + ly.z1);
    double fc = ly.fingersThroughDepth ? z : c;   // GL: fingers through the lamella height -> zig-zag on side faces, straight line on top
    double tri = ly.fingers ? Math.Abs(((fc / 6.0) % 1.0 + 1) % 1.0 - 0.5) * 2 - 0.5 : 0, uj = u - tri * 20;   // finger joints 20 mm / 6 mm pitch
    if (ly.veneer) uj = u - (w / 1.5) * 12;                                                                    // scarf joint ~1:8 slope through the veneer
    int si = 0; while (si < lm.segs.Count - 1 && uj >= lm.segs[si].u1) si++; var s = lm.segs[si];
    double jd = Math.Min(Math.Abs(uj - s.u0), Math.Abs(uj - s.u1));
    double e = 1e-6, gap = 1e9;
    // Glue exists only between boards/layers, never on an exterior boundary.
    // Mesh precision must not turn a nearly planar outside face into glue polygons.
    if (j > 0) gap = Math.Min(gap, Math.Abs(c - lm.c0));
    if (j < lay.Count - 1) gap = Math.Min(gap, Math.Abs(lm.c1 - c));
    if (li > 0) gap = Math.Min(gap, Math.Abs(z - ly.z0));
    if (li < layers.Count - 1) gap = Math.Min(gap, Math.Abs(ly.z1 - z));
    double endg = nx2 < 0 ? ((ax ? (x < e || x > L - e) : (y < e || y > W - e)) ? 1 : 0) : (ax ? nx2 : ny2);
    Rgb col;
    if (ly.veneer) {
      col = Veneer(s, u, v, w, endg);
      // scarf joint ~1:8 through a 3 mm veneer (~24 mm): blend into the neighbouring sheet within 12 mm of the joint
      int sn = uj - s.u0 < s.u1 - uj ? si - 1 : si + 1;
      if (!ly.continuous && jd < 12 && sn >= 0 && sn < lm.segs.Count) {
        double t = 0.5 * (1 - SmoothStep(0, 12, jd));
        var cn = Veneer(lm.segs[sn], u, v, w, endg);
        for (int k = 0; k < 3; k++) col[k] = Lerp(col[k], cn[k], t);
      }
    } else {
      double dv = v - s.pv - s.tv * (u - s.u0), dw = w - s.pw - s.tw * (u - s.u0);
      col = Wood(s, u, Math.Sqrt(dv * dv + dw * dw), Math.Atan2(dw, dv), v, w, endg);
    }
    double dark = Lerp(1, 0.94, endg), gk = gap < Pal.GlueW ? Pal.GlueK : 0;
    if (!ly.continuous && !ly.continuousLength && jd < 0.5) gk = Math.Max(gk, ly.veneer ? (z > H - 3.01 ? 0.15 : Pal.GlueK) : Pal.JointK);  // face scarfs light glue (front side), inner dark
    for (int k = 0; k < 3; k++) col[k] = (col[k] * (1 - gk) + Pal.Glue[k] * gk) * dark;
    double g = Pal.Gain; return (255 << 24) | (Cl(col[0] * g * Pal.GR) << 16) | (Cl(col[1] * g * Pal.GG * Lerp(1, Pal.EndG, endg)) << 8) | Cl(col[2] * g * Pal.GB * Lerp(1, Pal.EndB, endg));
  }

  // rotary-peeled veneer: unroll a log of radius R0; depth w and slow taper/ovality move through rings -> wide wavy bands
  Rgb Veneer(Seg s, double u, double v, double w, double endg) {
    double ox = s.seed % 97;
    double r = s.R0 + w + 2 * RS * Perlin(u * 0.0002, v * 0.01, ox) + 1.0 * RS * Perlin(u * 0.0005, v * 0.04, ox + 5) + u * 0.0015;  // v2.1: calmer, longer figure  // fibre-elongated figure
    return Wood(s, u, r, v / s.R0, v, w, endg);
  }

  // Growth is volumetric and independent of the cut. Colour synthesis consumes the
  // same phase on every face, with orientation affecting contrast/detail only.
  readonly struct GrowthSample {
    public readonly double Phase, Earlywood, Fibre, Knot, KnotRing, Influence;
    public GrowthSample(double phase, double earlywood, double fibre, double knot, double knotRing, double influence) {
      Phase = phase; Earlywood = earlywood; Fibre = fibre; Knot = knot; KnotRing = knotRing; Influence = influence;
    }
  }

  GrowthSample Growth(Seg s, double u, double r, double ang, double v, double w) {
    double ox = s.seed % 101, ca = Math.Cos(ang), sa = Math.Sin(ang);
    double n = (Perlin(u * 0.004, ca * 1.2 + ox, sa * 1.2) * 4 + Perlin(u * 0.01, ca * 4 + ox, sa * 4 + r * 0.02) * 1.5) * Pal.NoiseK;
    double kn = 0, kring = 0, influence = 0;
    for (int k = 0; k < s.ku.Length; k++) {
      double du = u - (s.ku[k] + 0.35 * r);
      if (Math.Abs(du) > 168 * s.ks[k]) continue;   // speed: d > 12 kr (kr <= 14 ks) -> knot influence < 1.5 kr e^-11, invisible
      double dt = Math.Abs(Math.IEEERemainder(ang - s.ka[k], Math.PI * 2)) * r;
      double kr = s.ks[k] * Math.Min(14, 2 + 0.03 * r), d = Math.Sqrt(du * du + dt * dt);
      // Soft influence area: Exp falloff around each knot centre (wider than the hard body).
      influence = Math.Max(influence, Math.Exp(-d / Math.Max(3.5 * kr, 1e-6)));
      if (d < kr) { kn = Math.Max(kn, 1 - d / kr); kring = Math.Max(kring, SmoothStep(0.75, 1.0, d / kr)); }
      else n -= 1.5 * kr * Math.Exp(-(d - kr) / kr);
    }
    double rr = (r + n) / (RS * (1 + 0.25 * Perlin(r * 0.03, ox * 0.37, 0.5))), f = rr - Math.Floor(rr);

    double fib = (Perlin(u * 0.08, v * 2, w * 2) * 0.5 + 0.5) * 0.08, ew = Perlin((r + n) / 14.0, u * 0.002, ox) * 0.5 + 0.5;
    return new GrowthSample(f, ew, fib, kn, kring, influence);
  }

  Rgb Wood(Seg s, double u, double r, double ang, double v, double w, double endg) =>
    Shade(s, Growth(s, u, r, ang, v, w), u, v, w, endg);

  Rgb Shade(Seg s, GrowthSample growth, double u, double v, double w, double endg) {
    double f = growth.Phase, ew = growth.Earlywood;
    // Slowly varying transition widths, without moving the underlying ring phase.
    double variation = (2 * ew - 1) * .75 + s.detail * .25;
    double lateShape = SmoothStep(.6 + .035 * variation, .9 + .015 * variation, f)
      * (1 - SmoothStep(.96 + .008 * variation, 1, f));
    double late = Lerp(Pal.LateK, Pal.EndLateK >= 0 ? Pal.EndLateK : Pal.LateK, endg) * lateShape;
    double fib = growth.Fibre;
    // Longitudinal fibres become cross-sectional stipple on end cuts. This stays
    // in material space, so adjacent charts sample the same detail at their edge.
    if (endg > 0) {
      double cross = (.5 + .5 * Perlin(u * .6, v * .8, w * .8 + s.detail * 13)) * .06;
      fib = Lerp(fib, cross, endg);
    }
    var P = Pal; Rgb c = default;
    for (int k = 0; k < 3; k++) c[k] = Lerp(Lerp(P.E0[k], P.E1[k], ew), P.Late[k], late);
    c[2] *= 1 + P.YelK * (0.08 * (1 - s.yel) - 0.07); c[1] *= 1 + P.YelK * (0.03 * s.yel - 0.02);  // per-board yellow/peach shift
    double t = s.tint * (1 - fib); c[0] *= t; c[1] *= Math.Pow(t, P.TG); c[2] *= Math.Pow(t, P.TB);
    c[1] = Math.Min(c[1], c[0] - P.HueG); c[2] = Math.Min(c[2], Math.Min(c[0] - P.HueB, c[1] - (P.HueB - P.HueG)));             // v2.1: keep hue warm (R > G > B), no pink/white boards
    double lum = (c[0] + c[1] + c[2]) / 3; if (lum < P.WarmL) { c[1] -= P.WarmG * (P.WarmL - lum); c[2] -= P.WarmB * (P.WarmL - lum); }   // v2.2: darker tones more saturated/honey (colref p10)
    double mx = c[0]; if (mx > P.Cap) for (int k = 0; k < 3; k++) c[k] *= P.Cap / mx;   // cap brightness -> brightest boards stay cream
    if (growth.Knot > 0) { double kk = 0.40 + 0.10 * (Math.Sin(growth.Knot * 40) * 0.5 + 0.5) - 0.22 * growth.KnotRing; for (int k = 0; k < 3; k++) c[k] = Lerp(c[k], P.Knot[k] * kk / 0.5, 0.95); }
    return c;
  }

  static readonly int[] Pm = MakePerm();
  static int[] MakePerm() { var r = new Random(1337); var p = Enumerable.Range(0, 256).OrderBy(i => r.Next()).ToArray(); return p.Concat(p).ToArray(); }
  static double Fade(double t) { return t * t * t * (t * (t * 6 - 15) + 10); }
  static double Grad(int h, double x, double y, double z) { h &= 15; double u = h < 8 ? x : y, v = h < 4 ? y : (h == 12 || h == 14 ? x : z); return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v); }
  internal static double Perlin(double x, double y, double z) {
    if (double.IsNaN(x + y + z) || Math.Abs(x) + Math.Abs(y) + Math.Abs(z) > 1e8) return 0;
    int X = (int)((long)Math.Floor(x) & 255), Y = (int)((long)Math.Floor(y) & 255), Z = (int)((long)Math.Floor(z) & 255);
    x -= Math.Floor(x); y -= Math.Floor(y); z -= Math.Floor(z);
    double u = Fade(x), v = Fade(y), w = Fade(z);
    int A = Pm[X] + Y, AA = Pm[A] + Z, AB = Pm[A + 1] + Z, B = Pm[X + 1] + Y, BA = Pm[B] + Z, BB = Pm[B + 1] + Z;
    return Lerp(Lerp(Lerp(Grad(Pm[AA], x, y, z), Grad(Pm[BA], x - 1, y, z), u), Lerp(Grad(Pm[AB], x, y - 1, z), Grad(Pm[BB], x - 1, y - 1, z), u), v),
                Lerp(Lerp(Grad(Pm[AA + 1], x, y, z - 1), Grad(Pm[BA + 1], x - 1, y, z - 1), u), Lerp(Grad(Pm[AB + 1], x, y - 1, z - 1), Grad(Pm[BB + 1], x - 1, y - 1, z - 1), u), v), w);
  }
  internal static double SmoothStep(double a, double b, double x) { double t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
  internal static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
  static int Cl(double v) { return double.IsNaN(v) ? 255 : (int)Math.Max(0, Math.Min(255, v)); }
}
}
