using Woodie.Appearance;
using Woodie.Stock;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class StockChecks
{
    public static void TimberParity()
    {
        foreach (var product in Enum.GetValues<TimberProduct>())
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
}
