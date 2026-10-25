using GH_IO.Serialization;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Grasshopper;
using Woodie.Stock;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class StockChecks
{
    public static void TimberParity()
    {
        foreach (var product in TimberProducts.MenuOrder)
        {
            var settings = new TimberSettings { Product = product, Seed = 17, KnotDensity = 0 };
            double h = settings.VirtualDepthMm;
            var panel = new TimberPanel(product, 1800, 500, h, 17, 0, settings.Palette());
            var stock = StockDefinition.FromTimber(settings, 1800, 500, h);
            Assert(stock.Construction.Pieces.Count == 1, "Timber stock should be one identity piece: " + product);
            Assert(stock.Construction.Pieces[0].Specimen.Seed == 17, "Specimen seed lost: " + product);
            for (int i = 0; i < 40; i++)
            {
                double x = 20 + i * 43, y = 10 + i * 11, z = h * ((i % 5) + .5) / 5;
                Assert(panel.Col(x, y, z) == stock.Sample(x, y, z), "Position sample drifted: " + product);
                Assert(panel.Col(x, y, z, 1, 0, 1) == stock.Sample(x, y, z, 1, 0, 1), "Oriented sample drifted: " + product);
            }
        }
    }

    static TimberSettings Bamboo(TimberProduct product, double variation = 0, int seed = 5) => new() { Product = product, Seed = seed, Variation = variation };
    static (double Y, double Z) Cross(MaterialPiece p, double x, double y, double z) { p.ToSource(x, y, z, out _, out var sy, out var sz); return (sy, sz); }
    // Distinct adhesive bands along a line: samples that change when only the glue colour changes.
    static int GlueBands(TimberProduct product, double y0, double y1, double z, double x = 300)
    {
        var plain = StockDefinition.FromTimber(Bamboo(product), 600, 100, 40);
        var marked = Bamboo(product); marked.Glue = System.Drawing.Color.FromArgb(0, 255, 0);
        var coloured = StockDefinition.FromTimber(marked, 600, 100, 40);
        int bands = 0; double last = -9;
        for (double y = y0; y <= y1; y += .05)
            if (plain.Sample(x, y, z) != coloured.Sample(x, y, z)) { if (y - last > .2) bands++; last = y; }
        return bands;
    }

    public static void EngineeredBamboo()
    {
        Assert((int)TimberProduct.BambooLumber == 10 && (int)TimberProduct.BambooLumberSide == 11 && (int)TimberProduct.BambooPanel == 12, "Engineered bamboo ids are appended");
        Assert(TimberProducts.MenuOrder.Length == 8 && TimberProducts.BambooOrder.All(p => !TimberProducts.MenuOrder.Contains(p)), "MenuOrder stays engineered timber only");
        Assert(TimberProducts.TryParse("BambooLumberSide", out var parsed) && parsed == TimberProduct.BambooLumberSide && TimberProducts.TryParse("Layered bamboo panel", out parsed) && parsed == TimberProduct.BambooPanel, "Bamboo products parse");
        Reject(() => new TimberSettings { Product = TimberProduct.BambooLumberSide, Layers = new[] { new LayerDefinition(40) } }.Validate(), "Side-pressed");

        // Plain-pressed: radial = stock Z, tangential = stock Y. Side-pressed: radial = stock Y, tangential = stock Z.
        var plain = StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumber), 2000, 100, 40);
        var side = StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumberSide), 2000, 100, 40);
        Assert(plain.Construction.Pieces.Count == 5 * 10 && side.Construction.Pieces.Count == 25 * 2, $"Strip counts {plain.Construction.Pieces.Count}/{side.Construction.Pieces.Count}");
        foreach (var (stock, radialAxis) in new[] { (plain, 'z'), (side, 'y') })
            foreach (int index in new[] { 3, 17, 29, 41 }) {
                var piece = stock.Construction.Pieces[index]; double d = .01;
                double cx = (piece.MinX + piece.MaxX) / 2, cy = (piece.MinY + piece.MaxY) / 2, cz = (piece.MinZ + piece.MaxZ) / 2, half = piece.Specimen.Features.Diameter / 2;
                var centre = Cross(piece, cx, cy, cz); double rx = centre.Y - half, rz = centre.Z - half, radius = Math.Sqrt(rx * rx + rz * rz);
                var dy = Cross(piece, cx, cy + d, cz); var dz = Cross(piece, cx, cy, cz + d);
                double alongY = ((dy.Y - centre.Y) * rx + (dy.Z - centre.Z) * rz) / (d * radius), alongZ = ((dz.Y - centre.Y) * rx + (dz.Z - centre.Z) * rz) / (d * radius);
                Near(Math.Abs(radialAxis == 'z' ? alongZ : alongY), 1, 1e-9);                 // radial axis follows the culm radius
                Near(Math.Abs(radialAxis == 'z' ? alongY : alongZ), 0, 1e-9);                 // the other stock axis is tangential
            }
        // Machining + split: with nominal anatomy every strip corner lies inside the wall, away from skin and lining; source x stays in the culm.
        foreach (var stock in new[] { plain, side })
            foreach (var piece in stock.Construction.Pieces) {
                var f = piece.Specimen.Features; double half = f.Diameter / 2;
                foreach (double x in new[] { piece.MinX, piece.MaxX }) foreach (double y in new[] { piece.MinY, piece.MaxY }) foreach (double z in new[] { piece.MinZ, piece.MaxZ }) {
                    piece.ToSource(x, y, z, out double sx, out double sy, out double sz); double r = Math.Sqrt((sy - half) * (sy - half) + (sz - half) * (sz - half));
                    Assert(sx >= 0 && sx <= f.Length && r > f.Inner(sx) + .4 && r < f.Outer(sx) - .9, $"Strip corner outside the machined wall: r {r:0.##} wall {f.Inner(sx):0.##}-{f.Outer(sx):0.##}");
                }
            }
        // No skin (green-olive) ever reaches stock samples; nodes remain visible; strips from one culm share a specimen, other culms differ.
        for (int i = 0; i < 400; i++) { int c = side.Sample(5 + i * 4.9, 1 + i % 98, 1 + i * 7 % 38); Assert((c >> 16 & 255) >= (c >> 8 & 255), "Skin colour in machined strip"); }
        var first = plain.Construction.Pieces[0]; var features = first.Specimen.Features;
        first.ToSource(0, 0, 0, out double sx0, out _, out _);
        double node = features.Nodes.First(n => n - sx0 > 20 && n - sx0 < 1900), at = node - sx0;
        double my = (first.MinY + first.MaxY) / 2, mz = (first.MinZ + first.MaxZ) / 2;
        Assert(plain.Sample(at, my, mz) != plain.Sample(at + 135, my, mz), "Node structure should survive the split and machining");
        var specimens = plain.Construction.Pieces.Select(p => p.Specimen).Distinct().Count();
        Assert(specimens > 1 && specimens < plain.Construction.Pieces.Count && plain.Construction.Pieces[0].Specimen != plain.Construction.Pieces[^1].Specimen, "Culm grouping");
        Assert(plain.Construction.Pieces.Select(p => { p.ToSource(0, 0, 0, out double sx, out _, out _); return sx; }).Distinct().Count() > 20, "Node stagger between strips");
        Assert(StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumber), 2000, 100, 40).Sample(500, 33, 21) == plain.Sample(500, 33, 21), "Bamboo lumber is deterministic");
        Assert(plain.Sample(500, 33, 21) != StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumber, 0, 6), 2000, 100, 40).Sample(500, 33, 21) ||
               plain.Sample(1500, 53, 11) != StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumber, 0, 6), 2000, 100, 40).Sample(1500, 53, 11), "Seed changes the strips");

        // Plain vs side: adhesive lines every 20 mm across the top vs every 4 mm; plain stacks 10 strips through the thickness, side only 2.
        Assert(GlueBands(TimberProduct.BambooLumber, 0, 100, 22) == 4 && GlueBands(TimberProduct.BambooLumberSide, 0, 100, 10) == 24, "Plain/side glue lines differ and never sit on the outside");

        // Layered panels: Buildup layers, 0°/90° fibre, and Fit to part / exact thickness rules.
        var panelSettings = Bamboo(TimberProduct.BambooPanel);
        var recipe = TimberLayups.Make(TimberProduct.BambooPanel, 18, 300); Assert(recipe.Count == 5 && recipe.Select(l => l.alongX).SequenceEqual(new[] { true, false, true, false, true }), "Panel alternates strip layers");
        var panel = StockDefinition.FromTimber(panelSettings, 800, 300, 18);
        Assert(panel.Construction.Pieces.Count > 0 && panel.Construction.Pieces.Max(p => p.MaxZ) == 18 && panel.Construction.Pieces.Min(p => p.MinZ) == 0, "Panel layers fill the exact thickness");
        var along = panel.Construction.PieceAt(400, 150, .5); var cross = panel.Construction.PieceAt(400, 150, 18 / 5.0 * 1.5);
        along.ToSource(10, 20, 0, out double a0, out _, out _); along.ToSource(11, 20, 0, out double a1, out _, out _);
        cross.ToSource(10, 20, 0, out double c0, out _, out _); cross.ToSource(10, 21, 0, out double c1, out _, out _); cross.ToSource(11, 20, 0, out double c2, out _, out _);
        Near(a1 - a0, 1, 1e-9); Near(c1 - c0, 1, 1e-9); Near(c2 - c0, 0, 1e-9);   // cross layer: fibre runs along stock Y
        var buildup = Bamboo(TimberProduct.BambooPanel); buildup.Layers = new[] { new LayerDefinition(5), new LayerDefinition(10, false), new LayerDefinition(5) };
        Reject(() => StockDefinition.FromTimber(buildup, 800, 300, 40), "Fit to part");
        buildup.FitToPart = true; var fitted = StockDefinition.FromTimber(buildup, 800, 300, 40);
        Near(fitted.Construction.Pieces.Max(p => p.MaxZ), 40, 1e-9); Assert(fitted.Layers.Count == 3 && fitted.Layers[1].z0 == 10 && !fitted.Layers[1].alongX, "Fit to part scales the layers");
        Reject(() => StockDefinition.FromTimber(Bamboo(TimberProduct.BambooLumber), 100000, 20000, 400), "Too many bamboo strips");
        Reject(() => StockDefinition.FromTimber(new TimberSettings { Product = TimberProduct.BambooLumber, VeneerThickness = 12 }, 600, 100, 40), "culm wall");
    }

    public static void ArchiveRoundTrip()
    {
        using var box = new Box(Plane.WorldXY, new Interval(0, 1200), new Interval(0, 300), new Interval(0, 120)).ToBrep();
        var settings = new TimberSettings { Product = TimberProduct.SolidTimber, Seed = 41 };
        var definition = StockDefinition.FromTimber(settings, 1200, 300, 120);
        using var stock = new WoodieStock(box, definition, box, Plane.WorldXY, Vector3d.XAxis);
        var goo = new StockGoo(stock);
        var archive = new GH_Archive();
        Assert(archive.AppendObject(goo, "Stock"), "Could not write stock goo");
        var copy = new StockGoo();
        Assert(archive.ExtractObject(copy, "Stock") && copy.Value != null, "Stock archive failed to reload");
        Assert(copy.Value.Definition.LengthMm == 1200 && copy.Value.Settings.Seed == 41, "Stock archive lost definition");
        Assert(copy.Value.BasePlane.HasValue && copy.Value.Fibre.HasValue, "Stock archive lost mapping");
        Assert(copy.Value.Definition.Sample(100, 50, 60) == stock.Definition.Sample(100, 50, 60), "Reload changed material samples");
    }
}
