using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;
using Woodie.Grasshopper;
using Woodie.Rendering;
using Woodie.Stock;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class MaterialChecks
{
    public static void Constructions()
    {
        var keys = new HashSet<string>();
        foreach (var product in TimberProducts.MenuOrder) {
            var settings = new TimberSettings { Product = product, Seed = 17 };
            double depth = settings.VirtualDepthMm;
            var layers = TimberLayups.Make(product, depth, 500);
            Near(layers.First().z0, 0); Near(layers.Last().z1, depth);
            Assert(layers.All(l => l.z1 > l.z0), "Empty layer: " + product);
            Assert(TimberProducts.TryParse(product.Label(), out var parsed) && parsed == product, "Product label parsing");
            Assert(keys.Add(settings.Key), "Products share a cache key");
            var pale = settings.Duplicate(); pale.Look = TimberLook.CleanPale;
            Near(pale.EffectiveKnotDensity, 0.25); Assert(keys.Add(pale.Key), "Look omitted from cache key");
            Assert(double.IsNaN(pale.LamellaWidth), "Appearance changed lamella width");
            pale.KnotDensity = 2; pale.EarlywoodA = Color.Red; Near(pale.EffectiveKnotDensity, 2); Near(pale.Palette().E0[0], 255); Near(pale.Palette().E0[1], 0);
            var panel = new TimberPanel(product, 2400, 500, depth, 17, settings.EffectiveKnotDensity, settings.Palette());
            Assert(Enumerable.Range(0, 30).Select(i => panel.Col(i * 80, 200, depth)).Distinct().Count() > 3, "Missing face grain: " + product);
            Assert(Enumerable.Range(0, 30).Select(i => panel.Col(0, i * 16, depth / 2)).Distinct().Count() > 3, "Missing end grain: " + product);
            WriteSwatch(settings);
        }
        var solid = TimberLayups.Make(TimberProduct.SolidTimber, 120, 500);
        Assert(solid.Count == 1 && solid[0].continuous && !solid[0].fingers && !solid[0].veneer, "Solid timber has manufactured joints");
        Assert(TimberLayups.Make(TimberProduct.KVH, 120, 500).Single().fingers, "KVH lost finger joints");
        var plywood = TimberLayups.Make(TimberProduct.Plywood, 18, 500);
        Assert(plywood.Count % 2 == 1 && plywood.Count >= 3 && plywood.All(l => l.veneer && l.continuous && !l.fingers), "Plywood is not continuous odd veneer layup");
        Assert(plywood.Select((l, i) => l.alongX == (i % 2 == 0)).All(x => x), "Plywood veneers must alternate");
        var parallel = TimberLayups.Make(TimberProduct.ParallelLVL, 45, 500);
        Assert(parallel.Count == 15 && parallel.All(l => l.alongX && l.veneer && l.continuous), "Parallel LVL layup");
        Assert(TimberLayups.Make(TimberProduct.KertoQ, 45, 500).Any(l => !l.alongX), "Kerto-Q lost cross veneers");
        Assert(TimberLayups.Make(TimberProduct.Plywood, 18, 500, veneerThickness: 2).Count == 9, "Plywood veneer override ignored");
        Assert(TimberLayups.Make(TimberProduct.ParallelLVL, 45, 500, veneerThickness: 5).Count == 9, "LVL veneer override ignored");
        Reject(() => TimberLayups.Make(TimberProduct.Plywood, 200, 500, veneerThickness: 0.001), "too many");
        Reject(() => new TimberSettings { VeneerThickness = 0 }.Validate(), "Veneer");
        Assert(TimberProducts.TryParse("2", out var numeric) && numeric == TimberProduct.GL, "Legacy numeric product override");
        // A joint-strength change must have no effect anywhere on continuous solid wood.
        var a = TimberPalette.For(TimberProduct.SolidTimber); var b = TimberPalette.For(TimberProduct.SolidTimber); b.JointK = 1;
        var first = new TimberPanel(TimberProduct.SolidTimber, 6000, 500, 120, 17, pal: a);
        var second = new TimberPanel(TimberProduct.SolidTimber, 6000, 500, 120, 17, pal: b);
        for (int i = 0; i <= 6000; i++) Assert(first.Col(i, 250, 120) == second.Col(i, 250, 120), "Solid timber has a butt/finger seam");
        ContactSheet();
    }
    static void ContactSheet()
    {
        using var bitmap = new Bitmap(1280, 1300); using var g = Graphics.FromImage(bitmap); g.Clear(Color.FromArgb(248,246,239));
        using var title = new Font(FontFamily.GenericSansSerif, 20); using var label = new Font(FontFamily.GenericSansSerif, 12);
        g.DrawString("Woodie · Eight timber constructions · Natural", title, Brushes.Black, 24, 18);
        g.DrawString("Each swatch: broad face / longitudinal edge / end grain. Seed 17; physical mm. Edge heights enlarged for inspection.", label, Brushes.DimGray, 24, 56);
        for (int i = 0; i < TimberProducts.MenuOrder.Length; i++) {
            var product = TimberProducts.MenuOrder[i]; int x = 24 + i % 2 * 630, y = 96 + i / 2 * 296;
            g.DrawString(product.Label(), label, Brushes.Black, x, y);
            using var swatch = Image.FromFile(Path.Combine(Output, "material-" + product + ".png")); g.DrawImageUnscaled(swatch, x, y + 28);
        }
        bitmap.Save(Path.Combine(Output, "material-gallery.png"));
    }
    static void WriteSwatch(TimberSettings settings)
    {
        const int width = 600, height = 240;
        double depth = settings.VirtualDepthMm;
        var panel = new TimberPanel(settings.Product, 1800, 500, depth, 17, settings.EffectiveKnotDensity, settings.Palette());
        var pixels = new int[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            double u = x * 1800d / (width - 1);
            pixels[y * width + x] = y < 150 ? panel.Col(u, y * 500d / 149, depth) :
                y < 195 ? panel.Col(u, 0, (y - 150) * depth / 44) : panel.Col(0, x * 500d / (width - 1), (y - 195) * depth / 44);
        }
        PngEncoder.WriteAtomic(Path.Combine(Output, "material-" + settings.Product + ".png"), width, height, pixels, CancellationToken.None);
    }

    /// <summary>Large side (skin) + top (end ring) renders for each bamboo species. Writes bamboo-*-side/top.png and bamboo-side-top.png.</summary>
    public static void BambooFullRenders()
    {
        const int sideW = 480, sideH = 1400, topS = 720, gap = 24, bg = unchecked((int)0xFFF0EEE6);
        var paths = new List<(string Label, string Side, string Top)>();
        foreach (var species in BambooSpecies.All) {
            var settings = new TimberSettings {
                Product = TimberProduct.Bamboo, Seed = 17, Species = species.Id,
                Diameter = species.Diameter, WallThickness = species.Wall, NodeSpacing = species.Spacing,
                Taper = species.Taper, Variation = .5 };
            settings.Validate();
            const double length = 3000;
            double diameter = settings.VirtualDepthMm;
            var features = NaturalFeatures.Resolve(settings, length, diameter, diameter);
            double width = 2 * features.Outer(0) * (1 + Math.Abs(features.Ovality)) + 4;
            var stock = StockDefinition.FromNatural(settings, length, width, width);
            double cy = width / 2, cz = width / 2;

            // Side elevation: silhouette uses Outer+Ridge so node rings bulge against the pad.
            const int sideBg = unchecked((int)0xFFF0EEE6);
            double maxR = 0;
            for (int i = 0; i < 64; i++) {
                double xu = 1 + i * (length - 2) / 63;
                maxR = Math.Max(maxR, features.Outer(xu) + features.Ridge(xu) + 4.5);
            }
            double padR = maxR * 1.08;
            var side = new int[sideW * sideH];
            for (int y = 0; y < sideH; y++) for (int x = 0; x < sideW; x++) {
                double u = y * (length - 2) / (sideH - 1) + 1;
                double worldY = (x / (sideW - 1d) - .5) * 2 * padR;
                double roBody = features.Outer(u) + features.Ridge(u);
                double ang = Math.Asin(Math.Clamp(worldY / Math.Max(roBody, 1e-6), -1, 1));
                double ro = roBody + features.FringeBump(u, ang);
                if (Math.Abs(worldY) > ro) { side[y * sideW + x] = sideBg; continue; }
                double sy = cy + ro * Math.Cos(ang), sz = cz + ro * Math.Sin(ang);
                int rgb = stock.Sample(u, sy, sz, 0, Math.Cos(ang), Math.Sin(ang));
                double limb = 1 - 0.28 * Math.Pow(worldY / Math.Max(ro, 1e-6), 2);
                int r8 = (int)Math.Clamp(((rgb >> 16) & 255) * limb, 0, 255);
                int g8 = (int)Math.Clamp(((rgb >> 8) & 255) * limb, 0, 255);
                int b8 = (int)Math.Clamp((rgb & 255) * limb, 0, 255);
                side[y * sideW + x] = (255 << 24) | (r8 << 16) | (g8 << 8) | b8;
            }
            string sidePath = Path.Combine(Output, "bamboo-" + species.Name + "-side.png");
            PngEncoder.WriteAtomic(sidePath, sideW, sideH, side, CancellationToken.None);

            // Top: full end-grain ring at mid-internode (fresh-cut normal).
            double xCut = length / 2;
            if (features.Nodes.Count > 0)
                xCut = (features.Nodes[0] + (features.Nodes.Count > 1 ? features.Nodes[1] : length)) / 2;
            double pad = features.Outer(xCut) + features.Ridge(xCut) + 6;
            var top = new int[topS * topS];
            for (int i = 0; i < top.Length; i++) top[i] = bg;
            for (int y = 0; y < topS; y++) for (int x = 0; x < topS; x++) {
                double sy = cy + (2 * (x + .5) / topS - 1) * pad, sz = cz + (2 * (y + .5) / topS - 1) * pad;
                double dy = (sy - cy) / (1 + features.Ovality), dz = (sz - cz) / (1 - features.Ovality);
                double r = Math.Sqrt(dy * dy + dz * dz);
                if (r > features.Outer(xCut) + features.Ridge(xCut) + .5 || !features.Solid(xCut, r)) continue;
                top[y * topS + x] = stock.Sample(xCut, sy, sz, 1, 0, 0);
            }
            string topPath = Path.Combine(Output, "bamboo-" + species.Name + "-top.png");
            PngEncoder.WriteAtomic(topPath, topS, topS, top, CancellationToken.None);
            paths.Add((species.Name + " · Ø" + species.DiameterMin.ToString("0") + "–" + species.DiameterMax.ToString("0"), sidePath, topPath));
            Console.WriteLine("Wrote " + sidePath);
            Console.WriteLine("Wrote " + topPath);
        }

        // Combined sheet: side | top per species.
        int sheetW = 40 + sideW + gap + topS + 40;
        int rowH = Math.Max(sideH, topS) + 48;
        using var sheet = new Bitmap(sheetW, 70 + paths.Count * rowH);
        using var g = Graphics.FromImage(sheet); g.Clear(Color.FromArgb(248, 246, 239));
        using var title = new Font(FontFamily.GenericSansSerif, 20);
        using var label = new Font(FontFamily.GenericSansSerif, 13);
        g.DrawString("Woodie · bamboo side (skin) + top (end cut) · seed 17", title, Brushes.Black, 24, 18);
        for (int i = 0; i < paths.Count; i++) {
            int y = 64 + i * rowH;
            g.DrawString(paths[i].Label, label, Brushes.Black, 24, y);
            using var sideImg = Image.FromFile(paths[i].Side);
            using var topImg = Image.FromFile(paths[i].Top);
            g.DrawImage(sideImg, 24, y + 22, sideW, sideH);
            g.DrawImage(topImg, 24 + sideW + gap, y + 22 + (sideH - topS) / 2, topS, topS);
        }
        string combined = Path.Combine(Output, "bamboo-side-top.png");
        sheet.Save(combined);
        Console.WriteLine("Bamboo side+top sheet: " + combined);
    }

    /// <summary>
    /// Side A/B after volumetric skin: plugin Sample vs bare SampleBundle proxy + end cut.
    /// Prints sheath↔side-darkening Pearson r (want &gt; 0.3).
    /// </summary>
    public static void BambooVolumetricCompare()
    {
        const int sideW = 360, sideH = 1100, topS = 480, gap = 20, bg = unchecked((int)0xFFF0EEE6);
        var species = BambooSpecies.Default;
        var settings = new TimberSettings {
            Product = TimberProduct.Bamboo, Seed = 17, Species = species.Id,
            Diameter = species.Diameter, WallThickness = species.Wall, NodeSpacing = species.Spacing,
            Taper = species.Taper, Variation = .5 };
        settings.Validate();
        const double length = 3000;
        double diameter = settings.VirtualDepthMm;
        var features = NaturalFeatures.Resolve(settings, length, diameter, diameter);
        double width = 2 * features.Outer(0) * (1 + Math.Abs(features.Ovality)) + 4;
        var stock = StockDefinition.FromNatural(settings, length, width, width);
        double cy = width / 2, cz = width / 2;
        var pal = settings.Palette();
        double maxR = 0;
        for (int i = 0; i < 64; i++) {
            double xu = 1 + i * (length - 2) / 63;
            maxR = Math.Max(maxR, features.Outer(xu) + features.Ridge(xu) + 4.5);
        }
        double padR = maxR * 1.08;

        var current = new int[sideW * sideH];
        var vol = new int[sideW * sideH];
        for (int y = 0; y < sideH; y++) for (int x = 0; x < sideW; x++) {
            double u = y * (length - 2) / (sideH - 1) + 1;
            double worldY = (x / (sideW - 1d) - .5) * 2 * padR;
            double roBody = features.Outer(u) + features.Ridge(u);
            double ang = Math.Asin(Math.Clamp(worldY / Math.Max(roBody, 1e-6), -1, 1));
            double roSil = roBody + features.FringeBump(u, ang);
            if (Math.Abs(worldY) > roSil) { current[y * sideW + x] = bg; vol[y * sideW + x] = bg; continue; }
            double ca = Math.Cos(ang), sa = Math.Sin(ang);
            double sy = cy + roSil * ca, sz = cz + roSil * sa;
            int rgb = stock.Sample(u, sy, sz, 0, ca, sa);
            double limb = 1 - 0.28 * Math.Pow(worldY / Math.Max(roSil, 1e-6), 2);
            current[y * sideW + x] = (255 << 24)
                | ((int)Math.Clamp(((rgb >> 16) & 255) * limb, 0, 255) << 16)
                | ((int)Math.Clamp(((rgb >> 8) & 255) * limb, 0, 255) << 8)
                | (int)Math.Clamp((rgb & 255) * limb, 0, 255);

            double ri = features.Inner(u), ro = features.Outer(u);
            var (sheath, _) = features.SampleBundle(ri, ro, ro - 0.22, ang, 0);
            double cr = TimberPanel.Lerp(pal.E0[0], features.Skin[0], 0.82);
            double cg = TimberPanel.Lerp(pal.E0[1], features.Skin[1], 0.82);
            double cb = TimberPanel.Lerp(pal.E0[2], features.Skin[2], 0.82);
            cr = TimberPanel.Lerp(cr, pal.Late[0], sheath * 0.55);
            cg = TimberPanel.Lerp(cg, pal.Late[1], sheath * 0.55);
            cb = TimberPanel.Lerp(cb, pal.Late[2], sheath * 0.55);
            vol[y * sideW + x] = (255 << 24)
                | ((int)Math.Clamp(cr * limb, 0, 255) << 16)
                | ((int)Math.Clamp(cg * limb, 0, 255) << 8)
                | (int)Math.Clamp(cb * limb, 0, 255);
        }
        // Correlation on a mid-internode θ-arc (raw Sample). Match Shade's ovality-corrected ang.
        double xMid = length / 2;
        if (features.Nodes.Count > 0)
            xMid = (features.Nodes[0] + (features.Nodes.Count > 1 ? features.Nodes[1] : length)) / 2;
        double riM = features.Inner(xMid), roM = features.Outer(xMid);
        double syO = 1 + features.Ovality, szO = 1 - features.Ovality;
        double sumL = 0; var Ls = new double[96]; var Ss = new double[96];
        for (int i = 0; i < 96; i++) {
            double ang = i * (2 * Math.PI / 96), ca = Math.Cos(ang), sa = Math.Sin(ang);
            double y = cy + (roM - .05) * ca, z = cz + (roM - .05) * sa;
            double dy = (y - cy) / syO, dz = (z - cz) / szO;
            double angShade = Math.Atan2(dz, dy);
            Ss[i] = features.SampleBundle(riM, roM, roM - 0.22, angShade, 0).Sheath;
            int rgb = stock.Sample(xMid, y, z, 0, ca, sa);
            Ls[i] = 0.3 * ((rgb >> 16) & 255) + 0.59 * ((rgb >> 8) & 255) + 0.11 * (rgb & 255);
            sumL += Ls[i];
        }
        double meanL = sumL / 96, sumA = 0, sumB = 0, sumA2 = 0, sumB2 = 0, sumAB = 0;
        for (int i = 0; i < 96; i++) {
            double dark = meanL - Ls[i];
            sumA += Ss[i]; sumB += dark; sumA2 += Ss[i] * Ss[i]; sumB2 += dark * dark; sumAB += Ss[i] * dark;
        }
        double meanA = sumA / 96, meanB = sumB / 96;
        double cov = sumAB / 96 - meanA * meanB;
        double varA = sumA2 / 96 - meanA * meanA, varB = sumB2 / 96 - meanB * meanB;
        double corr = (varA > 1e-12 && varB > 1e-12) ? cov / Math.Sqrt(varA * varB) : 0;
        Console.WriteLine($"Sheath↔side-darkening Pearson r = {corr:0.000} (n=96; want > 0.3)");
        Assert(corr > 0.3, $"Volumetric skin should track SampleBundle sheath (r={corr:0.00})");

        double xCut = length / 2;
        if (features.Nodes.Count > 0)
            xCut = (features.Nodes[0] + (features.Nodes.Count > 1 ? features.Nodes[1] : length)) / 2;
        double pad = features.Outer(xCut) + features.Ridge(xCut) + 6;
        var top = new int[topS * topS];
        for (int i = 0; i < top.Length; i++) top[i] = bg;
        for (int y = 0; y < topS; y++) for (int x = 0; x < topS; x++) {
            double sy = cy + (2 * (x + .5) / topS - 1) * pad, sz = cz + (2 * (y + .5) / topS - 1) * pad;
            double dy = (sy - cy) / (1 + features.Ovality), dz = (sz - cz) / (1 - features.Ovality);
            double r = Math.Sqrt(dy * dy + dz * dz);
            if (r > features.Outer(xCut) + features.Ridge(xCut) + .5 || !features.Solid(xCut, r)) continue;
            top[y * topS + x] = stock.Sample(xCut, sy, sz, 1, 0, 0);
        }

        string curPath = Path.Combine(Output, "bamboo-compare-current-side.png");
        string volPath = Path.Combine(Output, "bamboo-compare-vol-side.png");
        string topPath = Path.Combine(Output, "bamboo-compare-end.png");
        PngEncoder.WriteAtomic(curPath, sideW, sideH, current, CancellationToken.None);
        PngEncoder.WriteAtomic(volPath, sideW, sideH, vol, CancellationToken.None);
        PngEncoder.WriteAtomic(topPath, topS, topS, top, CancellationToken.None);

        int sheetW = 32 + sideW + gap + sideW + gap + topS + 32;
        int sheetH = 90 + sideH;
        using var sheet = new Bitmap(sheetW, sheetH);
        using var g = Graphics.FromImage(sheet); g.Clear(Color.FromArgb(248, 246, 239));
        using var title = new Font(FontFamily.GenericSansSerif, 16);
        using var label = new Font(FontFamily.GenericSansSerif, 12);
        g.DrawString($"Woodie · volumetric skin · Moso seed 17 · sheath↔darkening r={corr:0.00}", title, Brushes.Black, 20, 14);
        g.DrawString("PLUGIN side (SampleBundle streaks)", label, Brushes.Black, 20, 48);
        g.DrawString("PROXY side (sheath→Late only)", label, Brushes.Black, 20 + sideW + gap, 48);
        g.DrawString("End cut (volumetric field)", label, Brushes.Black, 20 + 2 * (sideW + gap), 48);
        using (var a = Image.FromFile(curPath)) g.DrawImage(a, 20, 72, sideW, sideH);
        using (var b = Image.FromFile(volPath)) g.DrawImage(b, 20 + sideW + gap, 72, sideW, sideH);
        using (var c = Image.FromFile(topPath)) g.DrawImage(c, 20 + 2 * (sideW + gap), 72 + (sideH - topS) / 2, topS, topS);
        string combined = Path.Combine(Output, "bamboo-vol-vs-current.png");
        sheet.Save(combined);
        Console.WriteLine("Wrote " + combined);
    }

    /// <summary>
    /// Speichinger KIT wall-stripe A/B: reference mesostructure (outer→inner vertical) beside Woodie Moso wall sample.
    /// Writes artifacts/checks/bamboo-speichinger-ab.png. Needs speichinger/page04_img8.png from the paper PDF extract.
    /// </summary>
    public static void SpeichingerAbSheet()
    {
        string refPath = Path.Combine(Output, "speichinger", "page04_img8.png");
        Assert(File.Exists(refPath), "Missing Speichinger wall stripe at " + refPath + " (extract from KIT PDF)");
        const int wallW = 220, wallH = 640, gap = 28;
        var settings = new TimberSettings {
            Product = TimberProduct.Bamboo, Seed = 17, Species = BambooSpeciesId.Moso,
            Diameter = BambooSpecies.Default.Diameter, WallThickness = BambooSpecies.Default.Wall,
            NodeSpacing = BambooSpecies.Default.Spacing, Taper = BambooSpecies.Default.Taper, Variation = .5 };
        settings.Validate();
        const double length = 2400;
        double diameter = settings.VirtualDepthMm;
        var features = NaturalFeatures.Resolve(settings, length, diameter, diameter);
        double width = 2 * features.Outer(0) * (1 + Math.Abs(features.Ovality)) + 4;
        var stock = StockDefinition.FromNatural(settings, length, width, width);
        double cy = width / 2, cz = width / 2;
        double xCut = length / 2;
        if (features.Nodes.Count > 0)
            xCut = (features.Nodes[0] + (features.Nodes.Count > 1 ? features.Nodes[1] : length)) / 2;
        double ri = features.Inner(xCut), ro = features.Outer(xCut), wall = Math.Max(ro - ri, 1);
        double arcSpan = Math.Max(6, 1.5 * wall);
        var ours = new int[wallW * wallH];
        // Match Speichinger crop: top = outer epidermis, bottom = inner lining.
        for (int y = 0; y < wallH; y++) for (int x = 0; x < wallW; x++) {
            double t = 1.0 - (y + .5) / wallH;
            double arc = ((x + .5) / wallW - .5) * arcSpan;
            double rr = ri + t * wall, ang = arc / Math.Max(rr, 1e-3);
            ours[y * wallW + x] = stock.Sample(xCut, cy + rr * Math.Cos(ang), cz + rr * Math.Sin(ang), 1, 0, 0);
        }
        string oursPath = Path.Combine(Output, "bamboo-wall-moso-outer-top.png");
        PngEncoder.WriteAtomic(oursPath, wallW, wallH, ours, CancellationToken.None);

        using var sheet = new Bitmap(48 + wallW * 2 + gap, 80 + wallH);
        using var g = Graphics.FromImage(sheet); g.Clear(Color.FromArgb(248, 246, 239));
        using var title = new Font(FontFamily.GenericSansSerif, 14);
        using var label = new Font(FontFamily.GenericSansSerif, 11);
        g.DrawString("Speichinger KIT wall stripe (left) · Woodie Moso (right) · outer↑ · seed 17", title, Brushes.Black, 20, 14);
        g.DrawString("CC-BY Speichinger et al. 2026 · DOI 10.1016/j.conbuildmat.2026.146522", label, Brushes.DimGray, 20, 40);
        using var refImg = Image.FromFile(refPath);
        g.DrawImage(refImg, 24, 70, wallW, wallH);
        using var ourImg = Image.FromFile(oursPath);
        g.DrawImage(ourImg, 24 + wallW + gap, 70, wallW, wallH);
        string path = Path.Combine(Output, "bamboo-speichinger-ab.png");
        sheet.Save(path);
        Console.WriteLine("Speichinger A/B: " + path);
    }

    // One inspection sheet: every product plus a square radial/end section. Writes artifacts/checks/material-radial-contact.png.
    public static void RadialInspectionSheet()
    {
        var entries = new List<(string Key, string Label, TimberSettings Settings)>();
        foreach (var product in TimberProducts.MenuOrder)
            entries.Add((product.ToString(), product.Label(), new TimberSettings { Product = product, Seed = 17 }));
        entries.Add(("Log", "Log", new TimberSettings { Product = TimberProduct.Log, Seed = 17, Diameter = 200, Taper = 10, BarkThickness = 5, Variation = .5 }));
        foreach (var species in BambooSpecies.All)
            entries.Add(("Bamboo-" + species.Name, "Bamboo · " + species.Name,
                new TimberSettings { Product = TimberProduct.Bamboo, Seed = 17, Species = species.Id,
                    Diameter = species.Diameter, WallThickness = species.Wall, NodeSpacing = species.Spacing, Taper = species.Taper, Variation = .5 }));
        foreach (var product in TimberProducts.BambooOrder)
            entries.Add((product.ToString(), product.Label(), new TimberSettings { Product = product, Seed = 17, Variation = 0 }));

        foreach (var entry in entries) WriteRadialPair(entry.Key, entry.Settings);

        const int faceW = 420, radial = 280, gap = 12, rowH = 328, cols = 2;
        int cellW = faceW + gap + radial, rows = (entries.Count + cols - 1) / cols;
        using var bitmap = new Bitmap(24 * 2 + cols * cellW + (cols - 1) * 24, 96 + rows * rowH);
        using var g = Graphics.FromImage(bitmap); g.Clear(Color.FromArgb(248, 246, 239));
        using var title = new Font(FontFamily.GenericSansSerif, 18);
        using var label = new Font(FontFamily.GenericSansSerif, 11);
        g.DrawString("Woodie · materials + radial sections · seed 17", title, Brushes.Black, 24, 16);
        g.DrawString("Left: longitudinal / broad face. Right: radial/end. Bamboo right = magnified wall (inner→outer), not the whole ring.", label, Brushes.DimGray, 24, 52);
        for (int i = 0; i < entries.Count; i++) {
            int x = 24 + i % cols * (cellW + 24), y = 88 + i / cols * rowH;
            g.DrawString(entries[i].Label, label, Brushes.Black, x, y);
            using var swatch = Image.FromFile(Path.Combine(Output, "radial-" + entries[i].Key + ".png"));
            g.DrawImageUnscaled(swatch, x, y + 22);
        }
        string path = Path.Combine(Output, "material-radial-contact.png");
        bitmap.Save(path);
        Console.WriteLine("Radial contact sheet: " + path);
    }

    static void WriteRadialPair(string key, TimberSettings settings)
    {
        const int faceW = 420, h = 280, radial = 280, gap = 12, bg = unchecked((int)0xFFF0EEE6);
        int width = faceW + gap + radial;
        var pixels = new int[width * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
        settings.Validate();
        if (settings.IsNatural) FillNaturalPair(pixels, width, faceW, h, radial, gap, settings, bg);
        else FillStockPair(pixels, width, faceW, h, radial, gap, settings, bg);
        PngEncoder.WriteAtomic(Path.Combine(Output, "radial-" + key + ".png"), width, h, pixels, CancellationToken.None);
    }

    static void FillStockPair(int[] pixels, int stride, int faceW, int h, int radial, int gap, TimberSettings settings, int bg)
    {
        double length = 1800, breadth = 500, depth = settings.VirtualDepthMm;
        if (settings.IsEngineeredBamboo) { breadth = 100; depth = 40; if (settings.Product == TimberProduct.BambooPanel) depth = 18; }
        var stock = StockDefinition.FromTimber(settings, length, breadth, depth);
        for (int y = 0; y < h; y++) for (int x = 0; x < faceW; x++) {
            double u = x * (length - 2) / (faceW - 1) + 1, v = y * (breadth - 2) / (h - 1) + 1;
            pixels[y * stride + x] = settings.Product == TimberProduct.BambooLumberSide
                ? stock.Sample(u, breadth / 2, Math.Min(depth - .1, v * depth / breadth), 0, 1, 0)
                : stock.Sample(u, v, depth, 0, 0, 1);
        }
        // Plain-pressed radial = Z: fibre×radial (X×Z). Side-pressed radial = Y: fibre×radial (X×Y). Else end grain.
        for (int y = 0; y < h; y++) for (int x = 0; x < radial; x++) {
            double a = (x + .5) / radial, b = (y + .5) / h;
            pixels[y * stride + faceW + gap + x] = settings.Product switch {
                TimberProduct.BambooLumber => stock.Sample(length * (.2 + .6 * a), breadth / 2, depth * b, 0, 1, 0),
                TimberProduct.BambooLumberSide => stock.Sample(length * (.2 + .6 * a), breadth * b, depth / 2, 0, 0, 1),
                TimberProduct.BambooPanel => stock.Sample(0, breadth * a, depth * b, 1, 0, 0),
                _ => stock.Sample(0, breadth * a, depth * b, 1, 0, 0)
            };
        }
    }

    static void FillNaturalPair(int[] pixels, int stride, int faceW, int h, int radial, int gap, TimberSettings settings, int bg)
    {
        const double length = 2400;
        double diameter = settings.VirtualDepthMm;
        var features = NaturalFeatures.Resolve(settings, length, diameter, diameter);
        double width = 2 * features.Outer(0) * (1 + Math.Abs(features.Ovality)) + 4;
        var stock = StockDefinition.FromNatural(settings, length, width, width);
        double cy = width / 2, cz = width / 2;
        // Longitudinal: bamboo length down the swatch; silhouette uses Outer+Ridge for node bulges.
        double bambooMaxR = 0;
        if (features.Bamboo)
            for (int i = 0; i < 48; i++) {
                double xu = 1 + i * (length - 2) / 47;
                bambooMaxR = Math.Max(bambooMaxR, features.Outer(xu) + features.Ridge(xu));
            }
        double bambooPad = bambooMaxR * 1.08;
        for (int y = 0; y < h; y++) for (int x = 0; x < faceW; x++) {
            if (features.Bamboo) {
                double u = y * (length - 2) / (h - 1) + 1;
                double worldY = (x / (faceW - 1d) - .5) * 2 * bambooPad;
                double ro = features.Outer(u) + features.Ridge(u);
                if (Math.Abs(worldY) > ro) { pixels[y * stride + x] = bg; continue; }
                double ang = Math.Asin(Math.Clamp(worldY / Math.Max(ro, 1e-6), -1, 1));
                double sy = cy + ro * Math.Cos(ang), sz = cz + ro * Math.Sin(ang);
                pixels[y * stride + x] = stock.Sample(u, sy, sz, 0, Math.Cos(ang), Math.Sin(ang));
            } else {
                double u = x * (length - 2) / (faceW - 1) + 1;
                double ro = features.Outer(u);
                double t = .55 + .4 * y / (h - 1);
                pixels[y * stride + x] = stock.Sample(u, cy + ro * t, cz, 0, 1, 0);
            }
        }
        double xCut = length / 2;
        if (features.Bamboo && features.Nodes.Count > 0)
            xCut = (features.Nodes[0] + (features.Nodes.Count > 1 ? features.Nodes[1] : length)) / 2;
        if (features.Bamboo) {
            // Magnified wall sector so discrete bundles resolve (full-ring tiles only had ~20 px across the wall).
            double ri = features.Inner(xCut), ro = features.Outer(xCut), wall = Math.Max(ro - ri, 1);
            double arcSpan = Math.Max(8, 1.8 * wall);
            for (int y = 0; y < h; y++) for (int x = 0; x < radial; x++) {
                double t = (x + .5) / radial, arc = ((y + .5) / h - .5) * arcSpan;
                double rr = ri + t * wall, ang = arc / Math.Max(rr, 1e-3);
                double sy = cy + rr * Math.Cos(ang), sz = cz + rr * Math.Sin(ang);
                pixels[y * stride + faceW + gap + x] = stock.Sample(xCut, sy, sz, 1, 0, 0);
            }
        } else {
            double pad = features.Outer(xCut) + 4;
            for (int y = 0; y < h; y++) for (int x = 0; x < radial; x++) {
                double sy = cy + (2 * (x + .5) / radial - 1) * pad, sz = cz + (2 * (y + .5) / h - 1) * pad;
                double dy = (sy - cy) / (1 + features.Ovality), dz = (sz - cz) / (1 - features.Ovality);
                double r = Math.Sqrt(dy * dy + dz * dz);
                if (r > features.Outer(xCut) + .5) pixels[y * stride + faceW + gap + x] = bg;
                else pixels[y * stride + faceW + gap + x] = stock.Sample(xCut, sy, sz, 1, 0, 0);
            }
        }
    }
    static SettingsGoo Result(SettingsComponent c) => c.Params.Output[0].VolatileData.AllData(true).OfType<SettingsGoo>().Single();
    public static void Interface()
    {
        IsolatedHost.LoadWoodie(Path.GetFullPath(Path.Combine(Output, "../../src/Woodie/bin/Release/net8.0/Woodie.gha")));
        GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled = true };
        var c = new SettingsComponent(); c.CreateAttributes(); doc.AddObject(c, false);
        Assert(c.Params.Input.Count == 3 && c.Params.Output.Count == 2 && c.Name == "Timber Material", "New compact interface");
        var ui = (MaterialAttributes)c.Attributes; c.Attributes.PerformLayout();
        static PointF Centre(RectangleF r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        Assert(ui.Click(Centre(ui.ProductBounds)), "Dropdown hit failed"); ui.PerformLayout(); Assert(ui.OptionBounds.Count == 8, "Missing product choices");
        Assert(ui.IsPickRegion(Centre(ui.OptionBounds[5])), "Open product list is outside the pick region");
        ui.Click(Centre(ui.OptionBounds[5])); ui.PerformLayout(); Assert(c.SelectedProduct == TimberProduct.Plywood, "Product dropdown selection failed");
        ui.Click(Centre(ui.LookBounds)); ui.PerformLayout(); ui.Click(Centre(ui.OptionBounds[1])); ui.PerformLayout();
        doc.NewSolution(false); Assert(Result(c).Value.Product == TimberProduct.Plywood && Result(c).Value.Look == TimberLook.CleanPale, "Dropdown solve");
        Assert(doc.Undo(), "No look undo record");
        c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(c.SelectedLook == TimberLook.Natural, "Look undo failed");
        Assert(doc.Redo(), "No look redo record"); c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(c.SelectedLook == TimberLook.CleanPale, "Look redo failed");
        c.SetAdvanced(true); Assert(c.Params.Input.Count == 14, "Advanced expansion");
        Assert(doc.Undo(), "No expansion undo"); c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(!c.Advanced, "Expansion undo failed");
        Assert(doc.Redo(), "No expansion redo"); c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(c.Advanced, "Expansion redo failed");
        ((Param_Number)c.Params.Input[4]).SetPersistentData(new GH_Number(1.75));
        Assert(!c.SetAdvanced(false) && c.Advanced && c.AdvancedNotice != null, "Collapse discarded stored data");
        c.ExpireSolution(false); doc.NewSolution(false); Near(Result(c).Value.EffectiveKnotDensity, 1.75);
        var products = new Param_String(); products.CreateAttributes(); products.SetPersistentData((IEnumerable<GH_String>)new[] { new GH_String("Solid timber"), new GH_String("KertoQ") }); doc.AddObject(products, false);
        c.Params.Input[0].AddSource(products); c.ExpireSolution(false); doc.NewSolution(false);
        Assert(c.ProductControlled && c.ProductSource.Contains("wired"), "Wired dropdown must disable");
        var outputs = c.Params.Output[0].VolatileData.AllData(true).OfType<SettingsGoo>().Select(g => g.Value.Product).ToArray();
        Assert(outputs.SequenceEqual(new[] { TimberProduct.SolidTimber, TimberProduct.KertoQ }), "Per-object product matching");
        c.SelectProduct(TimberProduct.GL); Assert(c.SelectedProduct == TimberProduct.Plywood, "Disabled product selector changed");
        Assert(!c.SetAdvanced(false), "Collapse discarded wires/values");
        var archive = new GH_Archive(); Assert(archive.AppendObject(c, "component"), "Material archive write");
        var duplicate = new SettingsComponent(); duplicate.CreateAttributes(); Assert(archive.ExtractObject(duplicate, "component"), "Material duplication/reopen");
        Assert(duplicate.Advanced && duplicate.SelectedLook == c.SelectedLook && duplicate.SelectedProduct == c.SelectedProduct, "UI state lost in archive");
        Near(((Param_Number)duplicate.Params.Input[4]).PersistentData.AllData(true).OfType<GH_Number>().Single().Value, 1.75);
        c.Params.Input[0].RemoveAllSources(); ((Param_String)c.Params.Input[0]).SetPersistentData(new GH_String("KVH"));
        Assert(c.ProductControlled && c.ProductSource.Contains("stored"), "Stored product did not disable selector");
        c.ExpireSolution(false); doc.NewSolution(false); Assert(Result(c).Value.Product == TimberProduct.KVH, "Stored product precedence");
        ((Param_Number)c.Params.Input[4]).PersistentData.Clear();
        var knots = new Param_Number(); knots.CreateAttributes(); doc.AddObject(knots, false); c.Params.Input[4].AddSource(knots);
        Assert(!c.SetAdvanced(false), "Collapse discarded advanced wire"); c.Params.Input[4].RemoveAllSources();
        Assert(c.SetAdvanced(false) && c.Params.Input.Count == 3, "Safe collapse failed");
        var sample = new SettingsComponent(); sample.CreateAttributes(); sample.Attributes.Pivot = new PointF(150, 100);
        Render(sample, "material-ui-compact.png");
        RenderScaleComparison(sample); sample.SetAdvanced(true); Render(sample, "material-ui-advanced.png");
        sample.SetAdvanced(false); var dropdown = (MaterialAttributes)sample.Attributes; dropdown.PerformLayout();
        dropdown.Click(Centre(dropdown.ProductBounds)); Render(sample, "material-ui-products.png");
        Legacy();
        SettingsArchives();
        CollapsiblePanel();
    }
    static void Render(SettingsComponent c, string name)
    {
        c.Attributes.PerformLayout(); var bounds = c.Attributes.Bounds;
        using var bitmap = new Bitmap((int)Math.Ceiling(bounds.Width) + 80, (int)Math.Ceiling(bounds.Height) + 80);
        using var g = Graphics.FromImage(bitmap); g.Clear(Color.White); g.TranslateTransform(40 - bounds.Left, 40 - bounds.Top);
        using var canvas = new global::Grasshopper.GUI.Canvas.GH_Canvas();
        typeof(MaterialAttributes).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(c.Attributes, new object[] { canvas, g, global::Grasshopper.GUI.Canvas.GH_CanvasChannel.Objects });
        bitmap.Save(Path.Combine(Output, name));
    }
    static void RenderScaleComparison(SettingsComponent material)
    {
        var preview = new PreviewComponent(); preview.CreateAttributes();
        material.Attributes.Pivot = new PointF(130, 100); material.Attributes.PerformLayout();
        preview.Attributes.Pivot = new PointF(material.Attributes.Bounds.Right + 100, 100); preview.Attributes.PerformLayout();
        var bounds = RectangleF.Union(material.Attributes.Bounds, preview.Attributes.Bounds);
        foreach (int dpi in new[] { 96, 144, 192 }) foreach (float zoom in new[] { 0.5f, 1f, 2f }) {
            float scale = dpi / 96f * zoom;
            using var bitmap = new Bitmap((int)Math.Ceiling((bounds.Width + 80) * scale), (int)Math.Ceiling((bounds.Height + 80) * scale));
            // Scale canvas coordinates, as the host does; keep font rendering in logical 96-DPI units.
            using var g = Graphics.FromImage(bitmap); g.Clear(Color.White); g.ScaleTransform(scale, scale); g.TranslateTransform(40 - bounds.Left, 40 - bounds.Top);
            using var canvas = new global::Grasshopper.GUI.Canvas.GH_Canvas();
            foreach (var component in new GH_Component[] { material, preview }) {
                component.Attributes.GetType().GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(component.Attributes, new object[] { canvas, g, global::Grasshopper.GUI.Canvas.GH_CanvasChannel.Objects });
            }
            bitmap.Save(Path.Combine(Output, $"material-scale-{dpi}-{zoom:0.0}.png"));
        }
    }
    static void CollapsiblePanel()
    {
        using var doc = new GH_Document { Enabled = true };
        var source = new Param_String(); source.CreateAttributes(); source.SetPersistentData(new GH_String("Plywood")); doc.AddObject(source, false);
        var c = new SettingsComponent(); c.CreateAttributes(); doc.AddObject(c, false);
        c.SelectLook(TimberLook.CleanPale); c.SetAdvanced(true);
        c.Params.Input[0].AddSource(source);
        ((Param_Number)c.Params.Input[4]).SetPersistentData(new GH_Number(1.75));
        doc.NewSolution(false);
        string key = Result(c).Value.Key;
        var ids = c.Params.Input.Select(p => p.InstanceGuid).ToArray();
        var ui = (MaterialAttributes)c.Attributes; ui.PerformLayout(); var expanded = ui.Bounds;
        var previousField = ui.ProductBounds;
        static PointF Centre(RectangleF r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        doc.IsModified = false;
        Assert(ui.Click(Centre(ui.DisclosureBounds)), "Collapse arrow did not handle click"); ui.PerformLayout();
        Assert(!c.PanelExpanded && ui.Bounds.Width < expanded.Width && ui.Bounds.Height < expanded.Height, "Whole material panel did not shrink");
        Assert(ui.ProductBounds.IsEmpty && ui.LookBounds.IsEmpty && ui.AdvancedBounds.IsEmpty && ui.OptionBounds.Count == 0, "Hidden controls retain hit regions");
        Assert(!ui.Click(Centre(previousField)), "Hidden product field still handles clicks");
        Assert(c.Phase == GH_SolutionPhase.Computed && Result(c).Value.Key == key, "UI collapse expired or changed material outputs");
        Assert(doc.IsModified, "Collapse did not mark definition for saving");
        Assert(c.Params.Input.Select(p => p.InstanceGuid).SequenceEqual(ids) && c.Params.Input[0].SourceCount == 1, "Collapse removed input identities or wires");
        Near(((Param_Number)c.Params.Input[4]).PersistentData.AllData(true).OfType<GH_Number>().Single().Value, 1.75);
        Render(c, "material-ui-collapsed.png");
        Assert(doc.Undo(), "Missing panel collapse undo"); c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(c.PanelExpanded, "Collapse undo failed");
        Assert(doc.Redo(), "Missing panel collapse redo"); c = doc.Objects.OfType<SettingsComponent>().Single(); Assert(!c.PanelExpanded, "Collapse redo failed");
        var archive = new GH_Archive(); archive.AppendObject(doc, "Definition");
        var read = new GH_Archive(); Assert(read.Deserialize_Xml(archive.Serialize_Xml()), "Collapsed definition XML failed");
        using var restored = new GH_Document { Enabled = true }; Assert(read.ExtractObject(restored, "Definition"), "Collapsed definition reopen failed");
        var copy = restored.Objects.OfType<SettingsComponent>().Single(); Assert(!copy.PanelExpanded && copy.Advanced && copy.Params.Input[0].SourceCount == 1, "Collapsed state or advanced wire did not survive reopen");
        restored.NewSolution(false); Assert(Result(copy).Value.Key == key, "Collapsed material changed after reopening");
        copy.Attributes.PerformLayout(); var reopenedUI = (MaterialAttributes)copy.Attributes;
        Assert(reopenedUI.Click(Centre(reopenedUI.DisclosureBounds)) && copy.PanelExpanded, "Expand arrow did not restore controls");
        reopenedUI.PerformLayout(); Assert(!reopenedUI.ProductBounds.IsEmpty, "Expanded product field missing");
        var sample = new SettingsComponent(); sample.CreateAttributes(); sample.SelectProduct(TimberProduct.ThreeS);
        Render(sample, "material-ui-expanded-beta6.png");
        sample.SetPanelExpanded(false); Render(sample, "material-ui-compact-beta6.png");
        sample.SetAdvanced(true); sample.SetPanelExpanded(true); sample.Attributes.PerformLayout();
        // Missing collapse state in beta.4/5/legacy archives keeps their visible controls.
        Legacy();
    }
    static void SettingsArchives()
    {
        foreach (var product in TimberProducts.MenuOrder) {
            var original = new SettingsGoo(new TimberSettings { Product = product, Look = TimberLook.CleanPale, VeneerThickness = 2.5, Seed = 17 });
            var archive = new GH_Archive(); archive.AppendObject(original, "settings"); var restored = new SettingsGoo();
            Assert(archive.ExtractObject(restored, "settings") && restored.Value.Key == original.Value.Key, "V2 settings archive: " + product);
        }
        var chunk = new GH_LooseChunk("settings"); chunk.SetBoolean("HasValue", true); chunk.SetInt32("Version", 1); chunk.SetInt32("Product", 3); chunk.SetInt32("Seed", 9);
        chunk.SetDouble("KnotDensity", 0.6); var legacy = new SettingsGoo();
        Assert(legacy.Read(chunk) && legacy.Value.Look == TimberLook.Natural && legacy.Value.Product == TimberProduct.KertoQ, "V1 settings archive rejected");
        Near(legacy.Value.KnotDensity, 0.6); Assert(double.IsNaN(legacy.Value.VeneerThickness), "Legacy veneer default changed");
    }
    static void Legacy()
    {
        var old = new LegacySettings(); old.CreateAttributes();
        ((Param_String)old.Params.Input[0]).PersistentData.Clear();
        ((Param_String)old.Params.Input[0]).SetPersistentData(new GH_String("GL"));
        ((Param_Number)old.Params.Input[4]).SetPersistentData(new GH_Number(0.7));
        var id = old.Params.Input[4].InstanceGuid;
        var archive = new GH_Archive(); archive.AppendObject(old, "legacy");
        var restored = new SettingsComponent(); restored.CreateAttributes(); Assert(archive.ExtractObject(restored, "legacy"), "Legacy component read");
        Assert(restored.Advanced && restored.Params.Input.Count == 14 && restored.Params.Input[4].InstanceGuid == id, "Legacy parameter identity lost");
        Near(((Param_Number)restored.Params.Input[4]).PersistentData.AllData(true).OfType<GH_Number>().Single().Value, 0.7);
        Assert(restored.Params.Output.Count == 2 && restored.Params.Output[0] is SettingsParam && restored.Name == "Timber Material" && restored.PanelExpanded, "Legacy output/name migration");
        using var doc = new GH_Document { Enabled = true };
        var source = new Param_Number(); source.CreateAttributes(); source.SetPersistentData(new GH_Number(0.7));
        doc.AddObject(source, false); doc.AddObject(old, false); old.Params.Input[4].AddSource(source);
        var definition = new GH_Archive(); definition.AppendObject(doc, "Definition");
        using var reopened = new GH_Document { Enabled = true }; Assert(definition.ExtractObject(reopened, "Definition"), "Legacy definition read");
        var migrated = reopened.Objects.OfType<SettingsComponent>().Single();
        Assert(migrated.Params.Input[4].SourceCount == 1 && migrated.Params.Input[4].InstanceGuid == id, "Legacy wire lost");
        reopened.NewSolution(false); Near(Result(migrated).Value.EffectiveKnotDensity, 0.7);
        Assert(Result(migrated).Value.Product == TimberProduct.GL, "Legacy product override lost");
    }
    sealed class LegacySettings : GH_Component
    {
        public LegacySettings() : base("Timber Display Settings", "TimberDisplay", "Legacy fixture", "Display", "Woodie") { }
        public override Guid ComponentGuid => new("7a74fc91-6ed6-447b-adb6-2e2fcd425383");
        protected override void RegisterInputParams(GH_InputParamManager p) {
            p.AddTextParameter("Product", "P", "Product", GH_ParamAccess.item, "CLT"); p.AddIntegerParameter("Seed", "S", "Seed", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Resolution", "R", "Resolution", GH_ParamAccess.item, 2);
            foreach (var name in new[] { "Edge Resolution", "Knot Density", "Ring Spacing", "Lamella Width", "Brightness" }) p.AddNumberParameter(name, name, name, GH_ParamAccess.item);
            foreach (var name in new[] { "Earlywood A", "Earlywood B", "Latewood", "Knot", "Glue" }) p.AddColourParameter(name, name, name, GH_ParamAccess.item);
            for (int i = 0; i < p.ParamCount; i++) p[i].Optional = true;
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddParameter(new SettingsParam(), "Settings", "TDS", "Settings", GH_ParamAccess.item);
        protected override void SolveInstance(IGH_DataAccess da) { }
    }
}
