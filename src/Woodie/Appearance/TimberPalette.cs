using System;

namespace Woodie.Appearance
{
// Softwood-oriented procedural palettes; see docs/appearance.md and docs/materials.md.
// Colours are visual approximations, not species- or finish-calibrated measurements.
public class TimberPalette {
  // Shared softwood palette with restrained board tint and warm shadow response.
  // Product layups, grain, knots and glue remain independent of this colour treatment.
  public double[] E0 = { 228, 226, 232 }, E1 = { 230, 214, 206 }, Late = { 182, 134, 70 }, Knot = { 120, 80, 50 }, Glue = { 0, 0, 0 };
  public double Gain = 1, GR = 1, GG = 1, GB = 1, TintSpread = 0.04, DarkTail = 0.015, GlueW = 0.6, GlueK = 0.2, LateK = 0.14, YelK = 1;
  public double WarmL = 205, WarmG = 0.2, WarmB = 0.5, HueG = 9, HueB = 18, Cap = 236, EndLateK = 0.5, TG = 1.05, TB = 1.15, EndG = 0.96, EndB = 0.85, KsMin = 0.35, JointK = 0.3, RS = 3.5, KdK = 1, NoiseK = 1;
  public static TimberPalette For(TimberProduct p) {
    var c = new TimberPalette();
    switch (p) {
      case TimberProduct.CLT:                                                       // offset 0 (reference); calmer board-to-board tint (photo spread), warm gain keeps mean b* ~12
        c.TintSpread = 0.02; c.DarkTail = 0.005; c.TG = 1.0; c.TB = 1.05; c.GG = 0.99; c.GB = 0.94; break;
      case TimberProduct.ThreeS:                                                         // colour: 0 (measured already ~+4 lighter: graded face has fewer dark features); structure: butt-jointed core, light melamine glue
        c.TintSpread = 0.035; c.GlueW = 0.3; c.Glue = new double[] { 215, 205, 185 }; break;
      case TimberProduct.GL:                                                             // colour: 0; lower board variation (sorted visible grade); light glue (~3 % dip)
        c.TintSpread = 0.025; c.DarkTail = 0.01; c.RS = 2.8; c.KsMin = 0.3; c.KdK = 0.5; c.NoiseK = 0.3;
        c.GlueW = 0.2; c.GlueK = 0.15; c.JointK = 0.18; break;
      case TimberProduct.SolidTimber:
      case TimberProduct.KVH:                                                      // C24 solid timber, finger-jointed; visible grade like GL
        c.TintSpread = 0.03; c.DarkTail = 0.012; c.RS = 3.0; c.NoiseK = 0.6; c.JointK = 0.25; break;
      case TimberProduct.Plywood:
      case TimberProduct.ParallelLVL:
      case TimberProduct.KertoQ:                                                         // colour: half the Metsa-ref vs CLT-target p50 ratio (1.00, 0.976, 0.85) -> slightly yellower
        c.GG = 0.99; c.GB = 0.93; c.Cap = 234; c.LateK = 0.25;                     // structure: peeled-veneer figure needs a bit more latewood
        c.Glue = new double[] { 95, 62, 38 }; c.GlueW = 0.35; c.GlueK = 0.75; break;   // phenolic PF glue lines are physically dark brown
      case TimberProduct.Log: c.RS = 4; c.TintSpread = 0.06; break;                       // whole softwood stem: wider rings, per-stem tint
      case TimberProduct.Bamboo:                                                         // Moso culm: E0 parenchyma, E1 lining/diaphragm, Late vascular bundles, Knot node band
        c.E0 = new double[] { 226, 205, 140 }; c.E1 = new double[] { 236, 222, 172 }; c.Late = new double[] { 150, 112, 60 }; c.Knot = new double[] { 176, 156, 96 }; break;
    }
    return c;
  }
}
}
