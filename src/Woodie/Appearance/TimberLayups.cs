using System;
using System.Collections.Generic;
using System.Linq;

namespace Woodie.Appearance
{
// Representative product layups; see docs/materials.md and docs/material-realism-audit.md.
// These visual presets do not reproduce every manufacturer thickness or construction.
public class TimberLayer { public double z0, z1, bw; public bool alongX, veneer, fingers, continuous, continuousLength, solidPith, fingersThroughDepth; public double segMin = 1500, segMax = 4000; }

public static class TimberLayups {
  public static List<TimberLayer> Resolve(TimberSettings settings, double height, double width, double toleranceMm = .01) {
    if (settings.Layers == null) return Make(settings.Product, height, width, settings.LamellaWidth, settings.VeneerThickness);
    settings.Validate();
    double total = settings.Layers.Sum(l => l.Thickness);
    if (!settings.FitToPart && Math.Abs(total - height) > Math.Max(.01, toleranceMm))
      throw new ArgumentException(FormattableString.Invariant($"Buildup is {total:0.###} mm thick; stock is {height:0.###} mm. Match the layer thicknesses or enable Fit to part on Timber Buildup."));
    double scale = settings.FitToPart ? height / total : 1, z = 0;
    var result = new List<TimberLayer>();
    foreach (var layer in settings.Layers) {
      bool full = layer.Construction != LayerConstruction.Boards;
      var resolved = new TimberLayer { z0 = z, z1 = z + layer.Thickness * scale, alongX = layer.AlongX,
        bw = double.IsNaN(layer.BoardWidth) ? Math.Max(width, 1) : layer.BoardWidth,
        veneer = layer.Construction == LayerConstruction.Veneer,
        fingers = layer.Joints == LayerJoints.Finger, continuous = full && (layer.Construction == LayerConstruction.Solid || layer.ContinuousSheet),
        continuousLength = layer.Joints == LayerJoints.None && layer.Construction == LayerConstruction.Boards,
        solidPith = layer.Construction == LayerConstruction.Solid || layer.NearPith,
        fingersThroughDepth = layer.Joints == LayerJoints.Finger && double.IsNaN(layer.BoardWidth),
        segMin = layer.SegmentMin, segMax = layer.SegmentMax };
      z = resolved.z1; result.Add(resolved);
    }
    return result;
  }

  public static LayerDefinition[] Recipes(TimberSettings settings, double height) => Resolve(settings, height, 1000).Select(l =>
    new LayerDefinition(l.z1 - l.z0, l.alongX, l.veneer ? LayerConstruction.Veneer : l.continuous ? LayerConstruction.Solid : LayerConstruction.Boards,
      l.solidPith || l.fingersThroughDepth ? double.NaN : l.bw, l.veneer || l.continuous ? LayerJoints.None : l.fingers ? LayerJoints.Finger : l.continuousLength ? LayerJoints.None : LayerJoints.Butt,
      l.segMin, l.segMax, l.continuous, l.solidPith)).ToArray();
  // thicknesses with orientation: true = along x (bold in drawing), false = cross
  public static List<TimberLayer> Make(TimberProduct p, double H, double W, double boardWidth = double.NaN, double veneerThickness = double.NaN) {
    if (!(H > 0) || !(W > 0)) throw new ArgumentException("Timber dimensions must be positive.");
    var t = new List<Tuple<double, bool>>(); double bw = 150; bool ven = false, fj = true;
    switch (p) {
      case TimberProduct.SolidTimber:
        t.Add(Tuple.Create(H, true)); bw = Math.Max(W, 1); fj = false; break;
      case TimberProduct.Plywood:
      case TimberProduct.ParallelLVL: {
        double nominal = double.IsNaN(veneerThickness) ? (p == TimberProduct.Plywood ? 1.5 : 3) : veneerThickness;
        if (!(nominal > 0) || !double.IsFinite(nominal)) throw new ArgumentException("Veneer thickness must be positive.");
        double wanted = H / nominal;
        if (wanted > 4096) throw new ArgumentException("Veneer thickness creates too many layers; increase Veneer Thickness.");
        int n = p == TimberProduct.Plywood ? Math.Max(3, 2 * (int)Math.Round((wanted - 1) / 2) + 1) : Math.Max(1, (int)Math.Round(wanted));
        for (int i = 0; i < n; i++) t.Add(Tuple.Create(H / n, p == TimberProduct.ParallelLVL || i % 2 == 0));
        ven = true; fj = false; break; }

      case TimberProduct.CLT:    // A1 Hasslacher BSP 200 7ss: 30-30-30-20-30-30-30, bold = L -> L L C L C L L
        foreach (var e in new[] { 30, 30, 30, 20, 30, 30, 30 }.Zip(new[] { 1, 1, 0, 1, 0, 1, 1 }, (a, b) => Tuple.Create((double)a, b == 1))) t.Add(e); break;
      case TimberProduct.ThreeS: // Novatop SWP/3 SD 27 (9-9-9) or 50 (9-32-9); face lamellas continuous 93-143 mm
        double face = Math.Min(9, H / 3); // retain positive layers on thin appearance-only inputs
        t.Add(Tuple.Create(face, true)); t.Add(Tuple.Create(H - 2 * face, false)); t.Add(Tuple.Create(face, true)); bw = 120; fj = false; break;
      case TimberProduct.GL: {   // GL24h: lamellas <= 45 mm (EN 14080, NKL 1/2; usual 40), one board across width (<= 280 mm); 128 -> 3 x 42.7, 105 -> 3 x 35
        int n = Math.Max(2, (int)Math.Ceiling(H / 45.0 - 1e-9)); for (int i = 0; i < n; i++) t.Add(Tuple.Create(H / n, true)); bw = Math.Max(W, 1); break; }
      case TimberProduct.KVH:    // KVH C24: one solid finger-jointed board over the full section
        t.Add(Tuple.Create(H, true)); bw = Math.Max(W, 1); break;
      case TimberProduct.KertoQ: // Kerto-Q 45: 15 x 3 mm, "II-IIII-IIII-II" ('-' = cross veneer)
        foreach (char ch in "II-IIII-IIII-II") t.Add(Tuple.Create(3.0, ch == 'I')); ven = true; fj = false; break;
    }
    if (t.Count == 0) throw new ArgumentException("Unknown timber product.");
    if (boardWidth > 0 && (p == TimberProduct.CLT || p == TimberProduct.ThreeS)) bw = boardWidth;   // display setting: lamella width
    double s = H / t.Sum(e => e.Item1), z = 0; var L = new List<TimberLayer>();
    foreach (var e in t) { var ly = new TimberLayer { z0 = z, z1 = z + e.Item1 * s, alongX = e.Item2, bw = bw, veneer = ven, fingers = fj }; z = ly.z1;
      if (p == TimberProduct.ThreeS) { ly.segMin = 1e5; ly.segMax = 1e5; if (!ly.alongX) { ly.segMin = 800; ly.segMax = 2500; } } // butt-jointed core
      if (ven) { ly.bw = 1200; ly.segMin = 1800; ly.segMax = 2000; }   // veneer sheet ~1.2 m clip x ~1.9 m log length, scarfed
      if (p == TimberProduct.SolidTimber || p == TimberProduct.Plywood || p == TimberProduct.ParallelLVL) {
        ly.continuous = true; ly.bw = Math.Max(W, H) + 1; // no clip seams within a generic veneer sheet
      }
      ly.solidPith = p == TimberProduct.GL || p == TimberProduct.KVH || p == TimberProduct.SolidTimber;
      ly.fingersThroughDepth = p == TimberProduct.GL || p == TimberProduct.KVH;
      L.Add(ly); }
    return L;
  }
}
}
