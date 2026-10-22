using System.Security.Cryptography;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;

namespace Woodie.Rendering;

public sealed class AtlasPage : IDisposable
{
    public Mesh Mesh { get; internal init; }
    public string TexturePath { get; internal init; }
    public TimberProduct Product { get; internal init; }
    internal int Width, Height;
    internal List<TriangleTile> Tiles;
    internal Lazy<TimberPanel> Panel;
    internal double L, W, H;
    public void Dispose() => Mesh?.Dispose();
}

internal sealed class TriangleTile
{
    public int Face, Rotation, X, Y, Width, Height;
    public double Bx, Cx, Cy;
    public Point3d A, B, C;
    // Coordinates already include straight/surface/guide mapping. Never use viewport normals here.
    public Vector3d CutNormal => Vector3d.CrossProduct(B - A, C - A);
}

public sealed class AppearanceBudgetException : ArgumentException
{
    public AppearanceBudgetException(string message) : base(message) { }
}

public static class TextureAtlas
{
    public const string EngineVersion = "woodie-8";
    internal const int Side = 2048, Gutter = 3, MaxPages = 16;
    public static string CacheDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Woodie", "Textures", EngineVersion);

    public static List<AtlasPage> Prepare(PreparedPart part, TimberSettings settings, double quality = 1, string cacheDirectory = null)
    {
        settings.Validate();
        ValidateAppearanceBudget(part, settings);
        if (!(quality > 0 && quality <= 1)) throw new ArgumentOutOfRangeException(nameof(quality));
        List<List<TriangleTile>> packed = null;
        double density = quality;
        // Bound memory even on heavily tessellated input. Reduce density rather than silently dropping faces.
        for (int attempt = 0; attempt < 12; attempt++, density *= 0.6)
        {
            packed = Pack(MakeTiles(part, settings, density));
            if (packed.Count <= MaxPages) break;
        }
        if (packed.Count > MaxPages) throw new ArgumentException("Mesh needs too many texture charts. Simplify the mesh.");
        var panel = new Lazy<TimberPanel>(() => new TimberPanel(settings.Product, part.LengthMm, part.WidthMm, part.DepthMm,
            settings.Seed, settings.EffectiveKnotDensity, settings.Palette(), settings.LamellaWidth, settings.VeneerThickness));
        string key = Key(part, settings, density);
        var result = new List<AtlasPage>();
        try
        {
            for (int i = 0; i < packed.Count; i++)
            {
                var tiles = packed[i];
                int width = tiles.Max(t => t.X + t.Width), height = tiles.Max(t => t.Y + t.Height);
                var mesh = new Mesh();
                foreach (var t in tiles)
                {
                    var f = part.Mesh.Faces[t.Face];
                    int start = mesh.Vertices.Count;
                    var ids = new[] { f.A, f.B, f.C };
                    foreach (int v in new[] { ids[t.Rotation], ids[(t.Rotation + 1) % 3], ids[(t.Rotation + 2) % 3] })
                    {
                        mesh.Vertices.Add(part.Mesh.Vertices.Point3dAt(v));
                        mesh.Normals.Add(part.Mesh.Normals[v]);
                    }
                    mesh.Faces.AddFace(start, start + 1, start + 2);
                    // Pixel centres, PNG top-down rows, Rhino bottom-up V coordinates.
                    AddUv(mesh, t.X + Gutter + 0.5, t.Y + Gutter + 0.5, width, height);
                    AddUv(mesh, t.X + Gutter + 0.5 + t.Bx, t.Y + Gutter + 0.5, width, height);
                    AddUv(mesh, t.X + Gutter + 0.5 + t.Cx, t.Y + Gutter + 0.5 + t.Cy, width, height);
                }
                result.Add(new AtlasPage { Mesh = mesh, Product = settings.Product, Width = width, Height = height, Tiles = tiles,
                    Panel = panel, L = part.LengthMm, W = part.WidthMm, H = part.DepthMm,
                    TexturePath = Path.Combine(cacheDirectory ?? CacheDirectory, key + "_" + i + ".png") });
            }
            return result;
        }
        catch { foreach (var p in result) p.Dispose(); throw; }
    }

    static void AddUv(Mesh mesh, double x, double y, int w, int h) => mesh.TextureCoordinates.Add(x / w, 1 - y / h);

    static void ValidateAppearanceBudget(PreparedPart part, TimberSettings settings)
    {
        double segments = 0, knots = 0;
        foreach (var layer in TimberLayups.Make(settings.Product, part.DepthMm, part.WidthMm, settings.LamellaWidth, settings.VeneerThickness))
        {
            double across = layer.alongX ? part.WidthMm : part.LengthMm;
            double length = layer.alongX ? part.LengthMm : part.WidthMm;
            double boards = layer.continuous ? 1 : Math.Ceiling(across / (layer.bw * (layer.veneer ? 0.8 : 1)));
            double count = layer.continuous ? 1 : boards * (Math.Ceiling(length / layer.segMin) + 2);
            segments += count;
            knots += count * (Math.Ceiling(settings.EffectiveKnotDensity * settings.Palette().KdK * (layer.continuous ? length + 2 : layer.segMax) / 1000 * 1.5 * (layer.veneer ? 3 : 1)) + 1);
        }
        if (segments > 100000 || knots > 2000000)
            throw new AppearanceBudgetException(FormattableString.Invariant(
                $"Appearance needs too many boards or knots for a mapped {part.LengthMm:0.###} × {part.WidthMm:0.###} × {part.DepthMm:0.###} mm part ({segments:0} board segments; {knots:0} estimated knots)."));
    }

    static List<TriangleTile> MakeTiles(PreparedPart part, TimberSettings settings, double quality)
    {
        var result = new List<TriangleTile>();
        for (int i = 0; i < part.Mesh.Faces.Count; i++)
        {
            var face = part.Mesh.Faces[i];
            Point3d a = part.Mesh.Vertices.Point3dAt(face.A), b = part.Mesh.Vertices.Point3dAt(face.B), c = part.Mesh.Vertices.Point3dAt(face.C);
            Vector3d e = b - a; double len = e.Length; e.Unitize();
            double cx = (c - a) * e, cy = Math.Sqrt(Math.Max(0, (c - a).SquareLength - cx * cx));
            if (len <= 0 || cy <= 0) continue;
            double minX = Math.Min(0, cx), span = Math.Max(len, cx) - minX;
            Point3d ca = part.Coordinates[face.A], cb = part.Coordinates[face.B], cc = part.Coordinates[face.C];
            Vector3d n = Vector3d.CrossProduct(cb - ca, cc - ca); n.Unitize();
            double res = Math.Abs(n.Z) > 0.9 ? settings.Resolution : Math.Min(settings.Resolution, settings.EffectiveEdgeResolution);
            double scale = part.UnitToMm * quality / res;
            scale = Math.Min(scale, (Side - 2 * Gutter - 2) / Math.Max(span, cy));
            // Rotate the triangle chart if C projects outside AB, keeping A at the chart's left edge.
            // Choosing the longest triangle edge guarantees 0 <= Cx <= Bx.
            if (cx < 0 || cx > len)
            {
                int[] ids = { face.A, face.B, face.C };
                int longest = Enumerable.Range(0, 3).OrderByDescending(j => part.Mesh.Vertices.Point3dAt(ids[j]).DistanceToSquared(part.Mesh.Vertices.Point3dAt(ids[(j + 1) % 3]))).First();
                // Store a cyclic corner order; it preserves winding and removes negative chart coordinates.
                int ia = ids[longest], ib = ids[(longest + 1) % 3], ic = ids[(longest + 2) % 3];
                a = part.Mesh.Vertices.Point3dAt(ia); b = part.Mesh.Vertices.Point3dAt(ib); c = part.Mesh.Vertices.Point3dAt(ic);
                e = b - a; len = e.Length; e.Unitize(); cx = (c - a) * e; cy = Math.Sqrt(Math.Max(0, (c - a).SquareLength - cx * cx));
                ca = part.Coordinates[ia]; cb = part.Coordinates[ib]; cc = part.Coordinates[ic];
                // Mesh output uses this same cyclic order (recorded separately below).
                scale = Math.Min(part.UnitToMm * quality / res, (Side - 2 * Gutter - 2) / Math.Max(len, cy));
                result.Add(Tile(i, longest, ca, cb, cc, len, cx, cy, scale));
            }
            else result.Add(Tile(i, 0, ca, cb, cc, len, cx, cy, scale));
        }
        return result;
    }

    static TriangleTile Tile(int face, int rotation, Point3d a, Point3d b, Point3d c, double len, double cx, double cy, double scale)
    {
        scale = Math.Max(scale, 1e-12);
        double bx = Math.Max(1, len * scale), py = Math.Max(1, cy * scale);
        return new TriangleTile { Face = face, Rotation = rotation, A = a, B = b, C = c, Bx = bx, Cx = Math.Clamp(cx / len, 0, 1) * bx, Cy = py,
            Width = (int)Math.Ceiling(bx) + 2 * Gutter + 1, Height = (int)Math.Ceiling(py) + 2 * Gutter + 1 };
    }

    static List<List<TriangleTile>> Pack(List<TriangleTile> tiles)
    {
        var pages = new List<List<TriangleTile>>();
        int x = 0, y = 0, row = 0;
        foreach (var t in tiles.OrderByDescending(t => t.Height).ThenBy(t => t.Face))
        {
            if (pages.Count == 0) pages.Add(new List<TriangleTile>());
            if (x + t.Width > Side) { x = 0; y += row; row = 0; }
            if (y + t.Height > Side) { pages.Add(new List<TriangleTile>()); x = y = row = 0; }
            t.X = x; t.Y = y; pages[^1].Add(t); x += t.Width; row = Math.Max(row, t.Height);
        }
        return pages;
    }

    static string Key(PreparedPart p, TimberSettings s, double density)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var buffer = new MemoryStream();
        using (var w = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true))
        {
            w.Write(EngineVersion); w.Write(s.Key); w.Write(density); w.Write(p.UnitToMm);
            w.Write(p.LengthMm); w.Write(p.WidthMm); w.Write(p.DepthMm);
            for (int i = 0; i < p.Coordinates.Length; i++)
            {
                var c = p.Coordinates[i]; w.Write(c.X); w.Write(c.Y); w.Write(c.Z);
                var v = p.Mesh.Vertices.Point3dAt(i); w.Write(v.X); w.Write(v.Y); w.Write(v.Z);
            }
            foreach (var f in p.Mesh.Faces) { w.Write(f.A); w.Write(f.B); w.Write(f.C); }
        }
        hash.AppendData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static void BakeMissing(IEnumerable<AtlasPage> pages, CancellationToken ct)
    {
        foreach (var page in pages.GroupBy(p => p.TexturePath).Select(g => g.First()))
        {
            ct.ThrowIfCancellationRequested();
            if (File.Exists(page.TexturePath)) continue;
            int[] pixels = Rasterize(page, ct);
            PngEncoder.WriteAtomic(page.TexturePath, page.Width, page.Height, pixels, ct);
        }
    }

    internal static int[] Rasterize(AtlasPage page, CancellationToken ct)
    {
        int[] pixels = new int[page.Width * page.Height];
        var panel = page.Panel.Value;
        // Split large charts into row bands so even a single-triangle page can use the worker budget.
        var bands = page.Tiles.SelectMany(tile => Enumerable.Range(0, (tile.Height + 31) / 32)
            .Select(row => (Tile: tile, First: row * 32, Last: Math.Min(tile.Height, (row + 1) * 32))));
        Parallel.ForEach(bands, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, band =>
        {
            var tile = band.Tile;
            var normal = tile.CutNormal;
            // Expand each sloping edge by a true pixel distance, preserving the gutter even on narrow triangles.
            double leftPad = Gutter * Math.Sqrt(tile.Cy * tile.Cy + tile.Cx * tile.Cx) / tile.Cy;
            double rightPad = Gutter * Math.Sqrt(tile.Cy * tile.Cy + (tile.Bx - tile.Cx) * (tile.Bx - tile.Cx)) / tile.Cy;
            for (int y = band.First; y < band.Last; y++)
            {
                ct.ThrowIfCancellationRequested();
                double row = (y - Gutter) / tile.Cy;
                int first = Math.Max(0, (int)Math.Floor(Gutter + row * tile.Cx - leftPad));
                int last = Math.Min(tile.Width - 1, (int)Math.Ceiling(Gutter + tile.Bx + row * (tile.Cx - tile.Bx) + rightPad));
                for (int x = first; x <= last; x++)
                {
                    double c = row, b = (x - Gutter - c * tile.Cx) / tile.Bx, a = 1 - b - c;
                    // Preserve the calibrated barycentric sampling for the triangle and its padded border.
                    a = Math.Max(0, a); b = Math.Max(0, b); c = Math.Max(0, c);
                    double sum = a + b + c; a /= sum; b /= sum; c /= sum;
                    Point3d p = new Point3d(a * tile.A.X + b * tile.B.X + c * tile.C.X,
                        a * tile.A.Y + b * tile.B.Y + c * tile.C.Y, a * tile.A.Z + b * tile.B.Z + c * tile.C.Z);
                    pixels[(tile.Y + y) * page.Width + tile.X + x] = panel.Col(Snap(p.X, page.L), Snap(p.Y, page.W), Snap(p.Z, page.H), normal.X, normal.Y, normal.Z);
                }
                // Texture filtering can reach beyond the three-pixel border. Extend the edge colour
                // across unused chart space without evaluating the expensive wood function again.
                int rowStart = (tile.Y + y) * page.Width + tile.X;
                if (first <= last)
                {
                    Array.Fill(pixels, pixels[rowStart + first], rowStart, first);
                    Array.Fill(pixels, pixels[rowStart + last], rowStart + last + 1, tile.Width - last - 1);
                }
                else
                {
                    int tip = panel.Col(Snap(tile.C.X, page.L), Snap(tile.C.Y, page.W), Snap(tile.C.Z, page.H), normal.X, normal.Y, normal.Z);
                    Array.Fill(pixels, tip, rowStart, tile.Width);
                }
            }
        });
        return pixels;
    }

    static double Snap(double v, double max) => v < 1e-3 ? 0 : v > max - 1e-3 ? max : v;
}
