using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rhino;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Rendering;
using Woodie.Stock;
namespace Woodie.Baking;

// A native object plus an immutable managed pixel job. No Rhino document is touched by Render.
internal sealed class NativeBakePlan : IDisposable
{
    internal const string Version = "native-uv-4";
    const int Gutter = 3;
    readonly NativeUvGeometry native;
    readonly Triangle[] triangles;
    readonly Lazy<IMaterialModel> panel;
    readonly double length, width, depth, tolerance;
    readonly string directory, recipe;
    internal GeometryBase Geometry => native.Geometry;
    internal MeshingParameters Meshing => native.Meshing;
    internal Rhino.Render.TextureMapping Mapping => native.Mapping;
    internal int Side { get; }
    internal string TexturePath { get; private set; }
    internal string ImageHash { get; private set; }
    internal TimberProduct Product { get; }
    internal string Label { get; }
    internal Transform TextureTransform { get; }
    sealed class Triangle
    {
        internal Point2d A, B, C;
        internal Point3d P, Q, R;
        internal Vector3d Normal => Vector3d.CrossProduct(Q - P, R - P);
        internal double Area => (B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X);
        internal Point3d Point(Point2d uv)
        {
            double b = ((uv.X - A.X) * (C.Y - A.Y) - (uv.Y - A.Y) * (C.X - A.X)) / Area;
            double c = ((B.X - A.X) * (uv.Y - A.Y) - (B.Y - A.Y) * (uv.X - A.X)) / Area;
            return new Point3d(P.X + b * (Q.X - P.X) + c * (R.X - P.X), P.Y + b * (Q.Y - P.Y) + c * (R.Y - P.Y), P.Z + b * (Q.Z - P.Z) + c * (R.Z - P.Z));
        }
        internal BoundingBox Bounds => new(new Point3d(Math.Min(A.X, Math.Min(B.X, C.X)), Math.Min(A.Y, Math.Min(B.Y, C.Y)), -1e-10), new Point3d(Math.Max(A.X, Math.Max(B.X, C.X)), Math.Max(A.Y, Math.Max(B.Y, C.Y)), 1e-10));
    }
    internal NativeBakePlan(BakeSnapshot snapshot, RhinoDoc target, CancellationToken ct)
    {
        using var input = snapshot.CopyFor(target.ModelUnitSystem);
        using var part = input.Prepare(target.ModelAbsoluteTolerance);
        var settings = input.Settings.Duplicate();
        var layers = TimberLayups.Resolve(settings, part.DepthMm, part.WidthMm, part.ToleranceMm);
        TextureAtlas.ValidateAppearanceBudget(part, settings, layers);
        native = NativeUvGeometry.Create(input.Geometry, input.Units, 8 * Math.Max(settings.Resolution, settings.EffectiveEdgeResolution), ct);
        try
        {
            Product = settings.Product; Label = settings.Layers == null ? Product.ToString() : "Custom buildup (" + Product + " palette)"; length = part.LengthMm; width = part.WidthMm; depth = part.DepthMm; tolerance = Math.Max(.01, 2 * part.ToleranceMm);
            panel = new Lazy<IMaterialModel>(() => StockDefinition.FromTimber(settings, length, width, depth, part.ToleranceMm, layers));
            var mesh = native.Samples;
            if (mesh.TextureCoordinates.Count != mesh.Vertices.Count) throw new ArgumentException("Rhino did not supply native UV coordinates for this geometry.");
            var coordinates = mesh.Vertices.ToPoint3dArray().Select(part.MaterialPoint).ToArray();
            var list = new List<Triangle>(); double required = 0;
            foreach (var face in mesh.Faces)
            {
                ct.ThrowIfCancellationRequested();
                Point2d UV(int i) { var uv = mesh.TextureCoordinates[i]; if (!double.IsFinite(uv.X + uv.Y) || uv.X < -.00001 || uv.Y < -.00001 || uv.X > 1.00001 || uv.Y > 1.00001) throw new ArgumentException("Native texture coordinates extend outside the bake atlas. Add valid UV seams upstream."); return new Point2d(uv.X, uv.Y); }
                var triangle = new Triangle { A = UV(face.A), B = UV(face.B), C = UV(face.C), P = coordinates[face.A], Q = coordinates[face.B], R = coordinates[face.C] };
                var worldA = mesh.Vertices.Point3dAt(face.A); var e = mesh.Vertices.Point3dAt(face.B) - worldA; var f = mesh.Vertices.Point3dAt(face.C) - worldA;
                if (Vector3d.CrossProduct(e, f).SquareLength < 1e-24) continue;
                if (Math.Abs(triangle.Area) < 1e-15) throw new ArgumentException("A mesh UV chart is collapsed. Add UV seams upstream; Woodie will not change the input topology.");
                var n = triangle.Normal; n.Unitize(); double resolution = Math.Abs(n.Z) > .9 ? settings.Resolution : Math.Min(settings.Resolution, settings.EffectiveEdgeResolution);
                double du = triangle.B.X - triangle.A.X, dv = triangle.B.Y - triangle.A.Y, eu = triangle.C.X - triangle.A.X, ev = triangle.C.Y - triangle.A.Y;
                var pu = (e * ev - f * dv) * (part.UnitToMm / triangle.Area); var pv = (f * du - e * eu) * (part.UnitToMm / triangle.Area);
                double a = pu.SquareLength, b = pu * pv, c = pv.SquareLength;
                required = Math.Max(required, Math.Sqrt((a + c + Math.Sqrt((a - c) * (a - c) + 4 * b * b)) / 2) / resolution);
                list.Add(triangle);
            }
            if (list.Count == 0) throw new ArgumentException("No usable native texture triangles.");
            if (!double.IsFinite(required) || required + 2 * Gutter > 8192) throw new ArgumentException(FormattableString.Invariant($"Native bake needs an atlas at least {Math.Ceiling(required + 2 * Gutter):0} pixels wide to retain the requested resolution; the limit is 8192. Increase Resolution and Edge Resolution (mm/pixel), or provide a more compact mesh UV layout. Mapped stock: {length:0.###} × {width:0.###} × {depth:0.###} mm."));
            Side = Math.Max(native.Meshing == null ? 64 : 2048, ((int)Math.Ceiling(required) + 2 * Gutter + 15) / 16 * 16);
            triangles = list.ToArray(); ValidateOverlaps(ct);
            double scale = (Side - 2d * Gutter) / Side; TextureTransform = Transform.Translation(Gutter / (double)Side, Gutter / (double)Side, 0) * Transform.Scale(Point3d.Origin, scale);
            native.Mapping.UvwTransform = TextureTransform;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(Version + TextureAtlas.Key(part, settings, 1) + native.Geometry.GetType().FullName + Side.ToString(CultureInfo.InvariantCulture)));
            using (var data = new MemoryStream()) { using (var writer = new BinaryWriter(data, Encoding.UTF8, true)) foreach (var t in triangles) foreach (var p in new[] { t.A, t.B, t.C }) { writer.Write(p.X); writer.Write(p.Y); } hash.AppendData(data.GetBuffer().AsSpan(0, (int)data.Length)); }
            recipe = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            directory = string.IsNullOrWhiteSpace(target.Path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Woodie", "BakedTextures") :
                Path.Combine(Path.GetDirectoryName(target.Path), Path.GetFileNameWithoutExtension(target.Path) + "_WoodieTextures");
        }
        catch { native.Dispose(); throw; }
    }
    internal void Render(CancellationToken ct, IProgress<double> progress = null)
    {
        ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(directory);
        string index = Path.Combine(directory, Version + "-" + recipe + ".txt");
        if (File.Exists(index))
        {
            string candidate = File.ReadAllText(index).Trim();
            if (candidate.Length == 64 && candidate.All(Uri.IsHexDigit))
            {
                string file = Path.Combine(directory, candidate + ".png");
                if (File.Exists(file) && FileHash(file) == candidate) { ImageHash = candidate; TexturePath = file; progress?.Report(1); return; }
            }
        }
        var pixels = new int[checked(Side * Side)]; int completed = 0;
        var bands = triangles.SelectMany(t =>
        {
            int lo = Math.Max(0, (int)Math.Floor(Gutter + (1 - Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y))) * (Side - 2 * Gutter)));
            int hi = Math.Min(Side - 1, (int)Math.Ceiling(Gutter + (1 - Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y))) * (Side - 2 * Gutter)));
            return Enumerable.Range(0, (hi - lo + 32) / 32).Select(i => (Triangle: t, First: lo + i * 32, Last: Math.Min(hi + 1, lo + (i + 1) * 32)));
        }).ToArray();
        var field = panel.Value;
        Parallel.ForEach(bands, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, band =>
        {
            var t = band.Triangle; var n = t.Normal; double span = Side - 2 * Gutter;
            int left = Math.Max(0, (int)Math.Floor(Gutter + Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)) * span));
            int right = Math.Min(Side - 1, (int)Math.Ceiling(Gutter + Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)) * span));
            for (int y = band.First; y < band.Last; y++)
            {
                ct.ThrowIfCancellationRequested(); double v = 1 - (y + .5 - Gutter) / span;
                for (int x = left; x <= right; x++)
                {
                    double u = (x + .5 - Gutter) / span;
                    double b = ((u - t.A.X) * (t.C.Y - t.A.Y) - (v - t.A.Y) * (t.C.X - t.A.X)) / t.Area;
                    double c = ((t.B.X - t.A.X) * (v - t.A.Y) - (t.B.Y - t.A.Y) * (u - t.A.X)) / t.Area;
                    if (b < -.0000001 || c < -.0000001 || b + c > 1.0000001) continue;
                    var p = t.Point(new Point2d(u, v));
                    pixels[y * Side + x] = field.Sample(TextureAtlas.Snap(p.X, length), TextureAtlas.Snap(p.Y, width), TextureAtlas.Snap(p.Z, depth), n.X, n.Y, n.Z);
                }
            }
            int done = Interlocked.Increment(ref completed); if (done % 32 == 0) progress?.Report(.8 * done / bands.Length);
        });
        var previous = new int[pixels.Length];
        for (int pass = 0; pass < Gutter; pass++)
        {
            Array.Copy(pixels, previous, pixels.Length);
            for (int y = 1; y < Side - 1; y++)
            {
                ct.ThrowIfCancellationRequested();
                for (int x = 1; x < Side - 1; x++)
                {
                    int i = y * Side + x; if (previous[i] != 0) continue;
                    if (previous[i - 1] != 0) pixels[i] = previous[i - 1]; else if (previous[i + 1] != 0) pixels[i] = previous[i + 1]; else if (previous[i - Side] != 0) pixels[i] = previous[i - Side]; else if (previous[i + Side] != 0) pixels[i] = previous[i + Side];
                }
            }
        }
        string temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            PngEncoder.WriteAtomic(temporary, Side, Side, pixels, ct); ImageHash = FileHash(temporary); TexturePath = Path.Combine(directory, ImageHash + ".png");
            ct.ThrowIfCancellationRequested(); File.Move(temporary, TexturePath, true);
            string pending = index + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(pending, ImageHash); ct.ThrowIfCancellationRequested(); File.Move(pending, index, true); } finally { if (File.Exists(pending)) File.Delete(pending); }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        progress?.Report(1);
    }
    static string FileHash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    void ValidateOverlaps(CancellationToken ct)
    {
        using var tree = new RTree();
        for (int i = 0; i < triangles.Length; i++)
        {
            ct.ThrowIfCancellationRequested(); var t = triangles[i]; var candidates = new List<int>(); tree.Search(t.Bounds, (_, e) => candidates.Add(e.Id));
            foreach (int j in candidates)
            {
                var other = triangles[j]; var polygon = new List<Point2d> { t.A, t.B, t.C }; var clip = new[] { other.A, other.B, other.C }; double sign = Math.Sign(other.Area);
                for (int edge = 0; edge < 3 && polygon.Count > 0; edge++)
                {
                    var a = clip[edge]; var b = clip[(edge + 1) % 3]; double Distance(Point2d p) => sign * ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));
                    var output = new List<Point2d>(); var start = polygon[^1]; double ds = Distance(start);
                    foreach (var end in polygon) { double de = Distance(end); if ((ds >= 0) != (de >= 0)) { double f = ds / (ds - de); output.Add(new Point2d(start.X + f * (end.X - start.X), start.Y + f * (end.Y - start.Y))); } if (de >= 0) output.Add(end); start = end; ds = de; }
                    polygon = output;
                }
                if (polygon.Count < 3) continue; double area = 0; for (int k = 0; k < polygon.Count; k++) { var a = polygon[k]; var b = polygon[(k + 1) % polygon.Count]; area += a.X * b.Y - a.Y * b.X; }
                if (Math.Abs(area) < 1e-12) continue;
                var centre = new Point2d(polygon.Average(p => p.X), polygon.Average(p => p.Y));
                if (t.Point(centre).DistanceTo(other.Point(centre)) > tolerance) throw new ArgumentException("The native UV layout overlaps different material points. For meshes, supply non-overlapping UV seams upstream. For native surfaces or extrusions, use a simpler part or report this shape; Woodie will not replace it with a mesh.");
            }
            tree.Insert(t.Bounds, i);
        }
    }
    public void Dispose() => native.Dispose();
}
