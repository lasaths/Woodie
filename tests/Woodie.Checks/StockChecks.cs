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
