using System.Drawing;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Rendering;
using static Woodie.Checks.Program;

namespace Woodie.Checks;

internal static class ExteriorGlueChecks
{
    public static void Sampling()
    {
        foreach (var product in TimberProducts.MenuOrder)
        foreach (var look in Enum.GetValues<TimberLook>())
        foreach (int seed in new[] { 0, 17 })
        {
            var settings = new TimberSettings { Product = product, Look = look, Seed = seed };
            double depth = settings.VirtualDepthMm;
            var panel = new TimberPanel(product, 2400, 600, depth, seed, 0, settings.Palette());
            foreach (double longitudinal in new[] { 131d, 523d, 1257d, 1871d })
            foreach (double across in new[] { 23d, 177d, 383d, 577d })
            foreach (bool top in new[] { false, true })
            {
                var exact = Color.FromArgb(panel.Col(longitudinal, across, top ? depth : 0, 0, 0, 1));
                foreach (double error in new[] { .00001, .0005, .001, .002, .005 })
                {
                    var nearby = Color.FromArgb(panel.Col(longitudinal, across, top ? depth - error : error, 0, 0, 1));
                    Assert(Difference(exact, nearby) <= 12, $"False exterior glue: {product}/{look}, seed {seed}, depth offset {error} mm");
                }
            }
            // Single-board sides must not be treated as glued board boundaries either.
            var outer = Color.FromArgb(panel.Col(523, 0, depth, 0, 0, 1));
            var inset = Color.FromArgb(panel.Col(523, .00001, depth, 0, 0, 1));
            Assert(Difference(outer, inset) <= 12, $"False perimeter glue: {product}/{look}");
        }

        var palette = TimberPalette.For(TimberProduct.ThreeS);
        var noGlue = TimberPalette.For(TimberProduct.ThreeS); noGlue.GlueK = 0;
        var joined = new TimberPanel(TimberProduct.ThreeS, 2400, 600, 27, 0, 0, palette);
        var clear = new TimberPanel(TimberProduct.ThreeS, 2400, 600, 27, 0, 0, noGlue);
        Assert(Difference(Color.FromArgb(joined.Col(500, 250, 9, 0, 0, 1)), Color.FromArgb(clear.Col(500, 250, 9, 0, 0, 1))) > 20,
            "Internal layer bond line was removed");
        Assert(Difference(Color.FromArgb(joined.Col(500, 120, 27, 0, 0, 1)), Color.FromArgb(clear.Col(500, 120, 27, 0, 0, 1))) > 20,
            "Internal board joint was removed");
    }

    static int Difference(Color first, Color second) => Math.Max(Math.Abs(first.R - second.R),
        Math.Max(Math.Abs(first.G - second.G), Math.Abs(first.B - second.B)));

    public static void Atlas()
    {
        // Metre geometry far from the origin reproduces float-mesh depth error on
        // a thin, rotated planar panel. One board per layer isolates external glue.
        using var mesh = Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 3.2, 1.7, .027), 6, 4, 1);
        for (int vi = 0; vi < mesh.Vertices.Count; vi += 2)
        {
            var point = mesh.Vertices.Point3dAt(vi);
            point.Z += point.Z > .013 ? -.000004 : .000004;
            mesh.Vertices.SetVertex(vi, point);
        }
        var frame = Plane.WorldXY;
        var rotation = Transform.Rotation(.61, new Vector3d(1, 2, 3), Point3d.Origin);
        var move = Transform.Translation(35, 22, 13);
        mesh.Transform(rotation); mesh.Transform(move);
        frame.Transform(rotation); frame.Transform(move);
        var settings = new TimberSettings { Product = TimberProduct.ThreeS, KnotDensity = 0, LamellaWidth = 5000, Resolution = 12, EdgeResolution = 12 };
        using var part = PreparedPart.Create(mesh, settings, 1000, .001, frame);
        var pages = TextureAtlas.Prepare(part, settings);
        int compared = 0, insetSamples = 0;
        try
        {
            foreach (var page in pages)
            {
                var actual = TextureAtlas.Rasterize(page, CancellationToken.None);
                var palette = settings.Palette(); palette.GlueK = 0;
                page.Panel = new Lazy<TimberPanel>(() => new TimberPanel(settings.Product, part.LengthMm, part.WidthMm,
                    part.DepthMm, settings.Seed, 0, palette, settings.LamellaWidth));
                var expected = TextureAtlas.Rasterize(page, CancellationToken.None);
                foreach (var tile in page.Tiles)
                {
                    bool top = tile.A.Z > part.DepthMm - .05 && tile.B.Z > part.DepthMm - .05 && tile.C.Z > part.DepthMm - .05;
                    bool bottom = tile.A.Z < .05 && tile.B.Z < .05 && tile.C.Z < .05;
                    if (!top && !bottom) continue;
                    int row = Math.Clamp(TextureAtlas.Gutter + (int)(.2 * tile.Cy), 0, tile.Height - 1);
                    int col = Math.Clamp(TextureAtlas.Gutter + (int)(.3 * tile.Bx + .2 * tile.Cx), 0, tile.Width - 1);
                    int at = (tile.Y + row) * page.Width + tile.X + col;
                    Assert(actual[at] == expected[at], "Atlas has a false glue polygon on a planar outside face");
                    compared++;
                    double z = .5 * tile.A.Z + .3 * tile.B.Z + .2 * tile.C.Z;
                    if (top ? part.DepthMm - z > .001 : z > .001) insetSamples++;
                }
            }
            Console.WriteLine($"  Exterior atlas: {compared} triangles checked; {insetSamples} inset samples");
            Assert(compared >= 20 && insetSamples > 0, "Rotated atlas fixture did not exercise float-mesh exterior depth error");
        }
        finally { foreach (var page in pages) page.Dispose(); }
    }
}
