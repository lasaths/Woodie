using System.Text.Json;
using Woodie.Appearance;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class AppearanceChecks
{
    // Capture and statistics use the same cut-aware entry point as atlas shading.
    static int Sample(TimberPanel panel, double x, double y, double z, double nx, double ny, double nz) => panel.Col(x, y, z, nx, ny, nz);

    public static void Orientation()
    {
        foreach (var product in TimberProducts.MenuOrder)
        foreach (var look in Enum.GetValues<TimberLook>())
        {
            var settings = new TimberSettings { Product = product, Look = look, Seed = 17 };
            double depth = settings.VirtualDepthMm;
            var panel = new TimberPanel(product, 1800, 500, depth, 17, 0, settings.Palette());
            var repeat = new TimberPanel(product, 1800, 500, depth, 17, 0, settings.Palette());
            bool changed = false, obliqueChanged = false;
            foreach (var layer in TimberLayups.Make(product, depth, 500))
            {
                double z = (layer.z0 + layer.z1) / 2;
                for (int i = 0; i < 16; i++)
                {
                    double x = 700 + i * 13.37, y = 200 + i * 2.17;
                    double nx = layer.alongX ? 1 : 0, ny = layer.alongX ? 0 : 1;
                    int end = panel.Col(x, y, z, nx, ny, 0), side = panel.Col(x, y, z, 0, 0, 1);
                    int oblique = panel.Col(x, y, z, nx, ny, 1);
                    changed |= end != side; obliqueChanged |= oblique != end && oblique != side;
                    Assert(end == panel.Col(x, y, z, -nx, -ny, 0), "Winding changed cut colour: " + product);
                    Assert(end == panel.Col(x, y, z, nx * 1e200, ny * 1e200, 0), "Normal scale changed colour");
                    Assert(oblique == repeat.Col(x, y, z, nx, ny, 1), "Oriented sampling is nondeterministic");
                    Assert(side == panel.Col(x, y, z, ny, nx, 0), "Cross layer fibre direction ignored: " + product);
                    Assert(panel.Col(x, y, z) == side, "Interior fallback no longer selects longitudinal detail");
                    Assert(panel.Col(x, y, z, 0, 0, 0) == side && panel.Col(x, y, z, double.NaN, 0, 1) == side, "Invalid normal fallback changed");
                    // Endpoint overload retains the old *orientation choice*, while
                    // all overloads share the improved volumetric appearance.
                    double ex = layer.alongX ? 0 : x, ey = layer.alongX ? y : 0;
                    Assert(panel.Col(ex, ey, z) == panel.Col(ex, ey, z, nx, ny, 0), "Bounding-end fallback changed");
                    int previous = side;
                    for (int step = 1; step <= 100; step++)
                    {
                        double weight = step / 100d;
                        int c = panel.Col(x, y, z, nx * Math.Sqrt(weight), ny * Math.Sqrt(weight), Math.Sqrt(1 - weight));
                        for (int k = 0; k < 3; k++) Assert(Math.Abs(((c >> (8 * k)) & 255) - ((previous >> (8 * k)) & 255)) <= 5, "Cut response has a discontinuity");
                        previous = c;
                    }
                    // Two coplanar adjacent charts share this edge sample. Their
                    // different triangle areas/windings must not produce a seam.
                    Assert(oblique == panel.Col(x, y, z, -nx * 7, -ny * 7, -7), "Adjacent charts disagree at a common material point");
                }
            }
            Assert(changed && obliqueChanged, "Interior/oblique cuts received no end-grain treatment: " + product);
        }
    }

    static Dictionary<string, double[]> LongitudinalStatistics()
    {
        var result = new Dictionary<string, double[]>();
        foreach (var product in TimberProducts.MenuOrder)
        foreach (var look in Enum.GetValues<TimberLook>())
        foreach (int seed in new[] { 0, 17, 41 })
        {
            var settings = new TimberSettings { Product = product, Look = look, Seed = seed };
            var palette = settings.Palette();
            // Exclude knots and manufactured seams from the colour acceptance mask.
            palette.GlueK = palette.JointK = palette.GlueW = 0;
            double depth = settings.VirtualDepthMm;
            var panel = new TimberPanel(product, 1800, 500, depth, seed, 0, palette);
            var channels = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 256; x++)
            {
                int c = Sample(panel, 1 + x * 1798d / 255, 1 + y * 498d / 63, depth, 0, 0, 1);
                for (int k = 0; k < 3; k++) channels[k].Add((c >> (16 - 8 * k)) & 255);
            }
            foreach (var channel in channels) channel.Sort();
            result[$"{product}/{look}/{seed}"] = channels.SelectMany(c => new[] { c[(int)((c.Count - 1) * .1)], c[(c.Count - 1) / 2], c[(int)((c.Count - 1) * .9)] }).Select(v => (double)v).ToArray();
        }
        return result;
    }

    public static void PalePalette()
    {
        var baseline = JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appearance-longitudinal.json")));
        var actual = LongitudinalStatistics();
        Assert(baseline.Count == actual.Count, "Longitudinal baseline coverage changed");
        double maxMedian = 0, maxTail = 0;
        foreach (var pair in actual) for (int k = 0; k < 9; k++)
        {
            double difference = Math.Abs(pair.Value[k] - baseline[pair.Key][k]);
            if (k % 3 == 1) maxMedian = Math.Max(maxMedian, difference); else maxTail = Math.Max(maxTail, difference);
            Assert(difference <= (k % 3 == 1 ? 5 : 8), $"Pale palette drift: {pair.Key}, statistic {k}, difference {difference}");
        }
        Console.WriteLine($"  Longitudinal RGB drift: median <= {maxMedian}, tails <= {maxTail}; {actual.Count} product/look/seed combinations");
    }

    public static void Capture(string label)
    {
        Assert(label is "baseline" or "current", "Capture label must be baseline or current");
        string directory = Path.Combine(Output, "appearance-upgrade", label); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "longitudinal.json"), JsonSerializer.Serialize(LongitudinalStatistics(), new JsonSerializerOptions { WriteIndented = true }));
        const int side = 256, columns = 3;
        foreach (var product in TimberProducts.MenuOrder)
        foreach (var look in Enum.GetValues<TimberLook>())
        {
            var settings = new TimberSettings { Product = product, Look = look, Seed = 17 };
            double depth = settings.VirtualDepthMm;
            var panel = new TimberPanel(product, 1800, 500, depth, 17, settings.EffectiveKnotDensity, settings.Palette());
            var pixels = new int[side * side * columns];
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                double a = x / (side - 1d), b = y / (side - 1d), z = depth * (.05 + .9 * b);
                pixels[y * side * columns + x] = Sample(panel, 400 + 1000 * a, 160 + 180 * b, depth, 0, 0, 1);
                pixels[y * side * columns + side + x] = Sample(panel, 900, 160 + 180 * a, z, 1, 0, 0);
                pixels[y * side * columns + 2 * side + x] = Sample(panel, 900 + depth / 2 - z, 160 + 180 * a, z, 1, 0, 1);
            }
            PngEncoder.WriteAtomic(Path.Combine(directory, $"{product}-{look}.png"), side * columns, side, pixels, CancellationToken.None);
        }
        foreach (var product in TimberProducts.MenuOrder) Console.WriteLine($"GOLDEN {product} {AppearanceHash(product)}");
        Console.WriteLine("Appearance sections and colour statistics: " + directory);
    }
}
