using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.IO.Compression;
using Woodie.Appearance;
using Woodie.Rendering;

namespace Woodie.Checks;

internal static class Program
{
    static int failures, passed;
    internal static string Output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/checks"));

    [STAThread]
    static int Main(string[] args)
    {
        Directory.CreateDirectory(Output);
        if (args.Contains("--capture-appearance")) { AppearanceChecks.Capture(args[Array.IndexOf(args, "--capture-appearance") + 1]); return 0; }
        if (args.Contains("--bake-checks")||args.Contains("--bake-matrix")) { RhinoInside.Resolver.Initialize(); RunBakeChecks(args.Contains("--bake-matrix")); return failures==0?0:1; }
        if (args.Contains("--native-packed-probe")) { RhinoInside.Resolver.Initialize(); RunPackedBakeProbe(); return failures==0?0:1; }
        if (args.Contains("--native-surface-probe")) { RhinoInside.Resolver.Initialize(); RunSurfaceBakeProbe(); return failures==0?0:1; }
        if ((args.Contains("--native-bake-probe") || args.Contains("--native-chart-probe") || args.Contains("--native-chart-ray-probe"))) { RhinoInside.Resolver.Initialize(); RunNativeBakeProbe(args.Contains("--native-chart-probe") || args.Contains("--native-chart-ray-probe"),args.Contains("--native-chart-ray-probe")); return failures==0?0:1; }
        if (args.Contains("--material-ui")) {
            RhinoInside.Resolver.Initialize(); RunMaterialUI(); return failures == 0 ? 0 : 1;
        }
        if (args.Contains("--benchmark-atlas"))
        {
            RhinoInside.Resolver.Initialize();
            Check("Atlas triangle coverage and gutters", AtlasCoverage);
            Check("Cold texture atlas throughput", BenchmarkAtlas);
            return failures == 0 ? 0 : 1;
        }
        if (args.Contains("--benchmark-pixels"))
        {
            BenchmarkPixels(args.Contains("--check-pixel-allocations"));
            return failures == 0 ? 0 : 1;
        }
        Check("Settings validation", Settings);
        Check("Stock sampling matches TimberPanel for all eight products", StockChecks.TimberParity);
        Check("Natural Log and Bamboo features: seed identity, variation bounds, hollow anatomy", NaturalChecks.Features);
        Check("Natural Log and Bamboo appearance: bark, timber, nodes, variation zero", NaturalChecks.Appearance);
        Check("Managed PNG round trip", Png);
        Check("Appearance deterministic for all eight products", Appearance);
        Check("Product constructions and appearance precedence", MaterialChecks.Constructions);
        Check("Cut orientation, cross layers, veneer and shared material samples", AppearanceChecks.Orientation);
        Check("Pale longitudinal colour retained", AppearanceChecks.PalePalette);
        Check("Exterior glue continuity and retained internal joints", ExteriorGlueChecks.Sampling);
        Check("Three-layer face seams remain subtle", ExteriorGlueChecks.LightBoardSeams);
        Check("Refinement suppresses stale queued callbacks", StaleCallback);
        Check("Refinement cancellation and failures", Cancellation);
        if (args.Contains("--rhino"))
        {
            try { RhinoInside.Resolver.Initialize(); RunRhino(args.Contains("--examples"), args.Contains("--integration"), args.Contains("--geometry-gallery")); }
            catch (Exception ex) { failures++; Console.WriteLine("FAIL Rhino host: " + ex); }
        }
        Console.WriteLine($"{passed} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunBakeChecks(bool matrix)
    {
        IsolatedHost.Prepare();
        using var core=new Rhino.Runtime.InProcess.RhinoCore(new[]{"/safemode","/nosplash","/notemplate"},Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        using var testDoc=Rhino.RhinoDoc.Create(null);testDoc.ModelUnitSystem=Rhino.UnitSystem.Millimeters;IsolatedHost.LoadBuiltIns();
        if(matrix)Check("Native bake eight-product matrix",NativeBakingChecks.MaterialMatrix);
        Check("Native bake core",NativeBakingChecks.Core);Check("Native bake geometry",NativeBakingChecks.Geometry);Check("Native bake units/stock",NativeBakingChecks.UnitsAndStock);Check("Native bake component",NativeBakingChecks.Component);Check("Native knot section",NativeBakingChecks.KnotSection);
        Console.WriteLine($"{passed} passed, {failures} failed");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunPackedBakeProbe()
    {
        IsolatedHost.Prepare();
        using var core=new Rhino.Runtime.InProcess.RhinoCore(new[]{"/safemode","/nosplash","/notemplate"},Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        Check("Native packed surface UV persistence across remeshing",PackedBakeProbe.Run);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunSurfaceBakeProbe()
    {
        IsolatedHost.Prepare();
        using var core=new Rhino.Runtime.InProcess.RhinoCore(new[]{"/safemode","/nosplash","/notemplate"},Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        Check("Native joined Brep surface materials survive reopening and remesh",SurfaceBakeProbe.Run);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunNativeBakeProbe(bool continuousCharts,bool rayProjection)
    {
        IsolatedHost.Prepare();
        using var core = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/safemode", "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        Check("Native geometry mapping survives reopen and remeshing",()=>NativeBakeProbe.Run(continuousCharts,rayProjection));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunMaterialUI()
    {
        IsolatedHost.Prepare();
        using var core = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/safemode", "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        IsolatedHost.LoadBuiltIns(); Check("Material UI and compatibility", MaterialChecks.Interface);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunRhino(bool examples, bool integration, bool gallery)
    {
        // Seed an empty GH server BEFORE native startup, preventing Instances' default extension scan.
        // Native safe mode additionally prevents unrelated Rhino plugins from loading.
        IsolatedHost.Prepare();
        using var core = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/safemode", "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow);
        using var testRhinoDocument = Rhino.RhinoDoc.Create(null);
        testRhinoDocument.ModelUnitSystem = Rhino.UnitSystem.Millimeters;
        IsolatedHost.LoadBuiltIns();
        RhinoChecks.Run(examples, integration);
        if (gallery) Check("All geometry types and upward-bowed thin timber", GeometryGallery.Run);
    }

    internal static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Near(double actual, double expected, double tolerance = 1e-5) => Assert(Math.Abs(actual - expected) <= tolerance, $"Expected {expected}, got {actual}");
    internal static void Reject(Action action, string contains)
    {
        try { action(); } catch (ArgumentException ex) { Assert(ex.Message.Contains(contains, StringComparison.OrdinalIgnoreCase), ex.Message); return; }
        throw new Exception("Expected rejection containing " + contains);
    }

    static void BenchmarkPixels(bool enforceBudget)
    {
        Check("Procedural texture pixel throughput", () =>
        {
            var settings = new TimberSettings();
            var panel = new TimberPanel(settings.Product, 2400, 600, 200, 17, settings.EffectiveKnotDensity, settings.Palette());
            for (int i = 0; i < 262144; i++) panel.Col(i % 2400, i % 600, 200);
            for (int run = 0; run < 5; run++)
            {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int checksum = 0;
            for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
                checksum = unchecked(checksum * 31 + panel.Col(x * 2400.0 / 511, y * 600.0 / 511, 200));
            clock.Stop();
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Console.WriteLine($"  262144 pixels: {clock.Elapsed.TotalMilliseconds:0.0} ms; {allocated} allocated bytes; checksum {checksum}");
            if (enforceBudget) Assert(allocated < 128 * 1024, "Per-pixel allocations exceed the 128 KiB budget for a 512 x 512 texture");
            Assert(checksum == 639478963, "Pixel benchmark appearance changed");
            }
        });
    }

    static int[] BaselineRasterize(AtlasPage page)
    {
        var pixels = new int[page.Width * page.Height]; var panel = page.Panel.Value;
        Parallel.ForEach(page.Tiles, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(8, Environment.ProcessorCount) }, tile => {
            var normal = tile.CutNormal;
            for (int y = 0; y < tile.Height; y++) for (int x = 0; x < tile.Width; x++) {
                double c = (y - TextureAtlas.Gutter) / tile.Cy, b = (x - TextureAtlas.Gutter - c * tile.Cx) / tile.Bx, a = 1 - b - c;
                a = Math.Max(0, a); b = Math.Max(0, b); c = Math.Max(0, c);
                double sum = a + b + c; a /= sum; b /= sum; c /= sum;
                double Snap(double v, double max) => v < 1e-3 ? 0 : v > max - 1e-3 ? max : v;
                pixels[(tile.Y + y) * page.Width + tile.X + x] = panel.Sample(
                    Snap(a * tile.A.X + b * tile.B.X + c * tile.C.X, page.L),
                    Snap(a * tile.A.Y + b * tile.B.Y + c * tile.C.Y, page.W),
                    Snap(a * tile.A.Z + b * tile.B.Z + c * tile.C.Z, page.H), normal.X, normal.Y, normal.Z);
            }
        });
        return pixels;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void AtlasCoverage()
    {
        foreach (int cut in new[] { 0, 1, 2, 3 })
        foreach (var shape in new[] { (0d, 48d), (24d, 48d), (48d, 48d), (24d, 1d) })
        {
            var tile = new TriangleTile { X = 2, Y = 3, Width = 55, Height = (int)shape.Item2 + 7,
                Bx = 48, Cx = shape.Item1, Cy = shape.Item2, A = new(0, 0, 200), B = new(2400, 0, 200), C = new(1200, 600, 200) };
            if (cut > 0) {
                tile.A = new(900, 0, 0); tile.B = new(900, 600, 0); tile.C = new(cut == 1 ? 900 : 700, 300, 200);
                if (cut == 3) (tile.B, tile.C) = (tile.C, tile.B);
            }
            using var page = new AtlasPage { Width = 59, Height = tile.Height + 6, L = 2400, W = 600, H = 200,
                Panel = new Lazy<Woodie.Stock.IMaterialModel>(() => new TimberPanel(TimberProduct.CLT, 2400, 600, 200, 17)), Tiles = new() { tile } };
            int[] expected = BaselineRasterize(page), actual = TextureAtlas.Rasterize(page, CancellationToken.None);
            for (int y = 0; y < tile.Height; y++) for (int x = 0; x < tile.Width; x++) {
                double px = x - TextureAtlas.Gutter, py = y - TextureAtlas.Gutter;
                double b = (px - py / tile.Cy * tile.Cx) / tile.Bx, c = py / tile.Cy;
                bool inside = b >= 0 && c >= 0 && b + c <= 1;
                double Distance(double ax, double ay, double bx, double by) {
                    double dx = bx - ax, dy = by - ay, t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1);
                    return Math.Sqrt(Math.Pow(px - ax - t * dx, 2) + Math.Pow(py - ay - t * dy, 2));
                }
                bool gutter = Math.Min(Distance(0, 0, 48, 0), Math.Min(Distance(48, 0, tile.Cx, tile.Cy), Distance(tile.Cx, tile.Cy, 0, 0))) <= TextureAtlas.Gutter;
                int index = (tile.Y + y) * page.Width + tile.X + x;
                if (inside || gutter) Assert(actual[index] != 0, "Triangle or three-pixel gutter was skipped");
                if (inside || gutter) Assert(actual[index] == expected[index], "Visible texel differs from the full rectangle reference");
                Assert(actual[index] != 0, "Unfilled chart pixels can bleed dark triangle seams through texture filtering");
            }

        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void BenchmarkAtlas()
    {
        // Exercise the complete cold bake (parallel shading plus managed PNG), without loading Rhino.
        // Two large triangular charts model opposite halves of a planar timber face.
        for (int run = 0; run < 3; run++)
        {
            string path = Path.Combine(Output, "benchmark-" + Guid.NewGuid().ToString("N") + ".png");
            using var page = new AtlasPage { Width = 2034, Height = 1017, L = 2400, W = 600, H = 200,
                TexturePath = path, Panel = new Lazy<Woodie.Stock.IMaterialModel>(() => new TimberPanel(TimberProduct.CLT, 2400, 600, 200, 17)),
                Tiles = new List<TriangleTile> {
                    new() { X = 0, Y = 0, Width = 1017, Height = 1017, Bx = 1010, Cx = 0, Cy = 1010,
                        A = new(0, 0, 200), B = new(2400, 0, 200), C = new(0, 600, 200) },
                    new() { X = 1017, Y = 0, Width = 1017, Height = 1017, Bx = 1010, Cx = 1010, Cy = 1010,
                        A = new(0, 600, 200), B = new(2400, 600, 200), C = new(2400, 0, 200) } } };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try {
                int[] baseline = BaselineRasterize(page);
                PngEncoder.WriteAtomic(path, page.Width, page.Height, baseline, CancellationToken.None);
                clock.Stop();
                Console.WriteLine($"  Full rectangle baseline: {clock.Elapsed.TotalMilliseconds:0.0} ms");
                File.Delete(path); clock.Restart();
                TextureAtlas.BakeMissing(new[] { page }, CancellationToken.None);
                clock.Stop();
                Console.WriteLine($"  Cold atlas: {clock.Elapsed.TotalMilliseconds:0.0} ms; {new FileInfo(path).Length} PNG bytes");
                clock.Restart(); TextureAtlas.BakeMissing(new[] { page }, CancellationToken.None); clock.Stop();
                Console.WriteLine($"  Cached atlas: {clock.Elapsed.TotalMilliseconds:0.00} ms");
            } finally { File.Delete(path); }
        }
    }

    static void Settings()
    {
        new TimberSettings().Validate();
        Reject(() => new TimberSettings { Resolution = double.NaN }.Validate(), "Resolution");
        Reject(() => new TimberSettings { Brightness = double.PositiveInfinity }.Validate(), "Brightness");
        Reject(() => new TimberSettings { KnotDensity = -1 }.Validate(), "Knot density");
        Reject(() => new TimberSettings { LamellaWidth = 0 }.Validate(), "Lamella width");
        Reject(() => new TimberSettings { RingSpacing = 0 }.Validate(), "Ring spacing");
        Reject(() => new TimberSettings { EdgeResolution = 0 }.Validate(), "Edge resolution");
        var s = new TimberSettings(); var copy = s.Duplicate(); copy.Seed = 44;
        Assert(s.Seed == 0 && s.Key != copy.Key, "Settings copies or cache keys are not independent");
    }

    static void Png()
    {
        int[] colors = { unchecked((int)0xff112233), unchecked((int)0x7f445566), unchecked((int)0xffabcdef), 0 };
        string path = Path.Combine(Output, "png-roundtrip.png");
        PngEncoder.WriteAtomic(path, 2, 2, colors, CancellationToken.None);
        byte[] bytes = File.ReadAllBytes(path);
        Assert(bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "PNG signature");
        int offset = 8; using var idat = new MemoryStream();
        while (offset < bytes.Length)
        {
            int size = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            string type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (type == "IDAT") idat.Write(bytes, offset + 8, size);
            offset += size + 12;
        }
        idat.Position = 0; using var z = new ZLibStream(idat, CompressionMode.Decompress); using var raw = new MemoryStream(); z.CopyTo(raw);
        Assert(raw.ToArray().SequenceEqual(new byte[] { 0, 0x11, 0x22, 0x33, 0xff, 0x44, 0x55, 0x66, 0x7f, 0, 0xab, 0xcd, 0xef, 0xff, 0, 0, 0, 0 }), "PNG pixel order");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { PngEncoder.WriteAtomic(path + ".cancelled", 2, 2, colors, cancelled.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { }
        Assert(!Directory.GetFiles(Output, "*.tmp").Any(), "Temporary PNG leaked");
    }

    internal static string AppearanceHash(TimberProduct product)
    {
        var settings = new TimberSettings { Product = product, Seed = 17 };
        var panel = new TimberPanel(product, 2400, 600, 200, 17, settings.EffectiveKnotDensity, settings.Palette());
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        foreach (double z in new[] { 0d, 3d, 9d, 30d, 95d, 170d, 200d })
            for (int y = 0; y <= 12; y++) for (int x = 0; x <= 32; x++) writer.Write(panel.Col(x * 75, y * 50, z));
        return Convert.ToHexString(SHA256.HashData(data.ToArray()));
    }
    static void Appearance()
    {
        string golden = Path.Combine(AppContext.BaseDirectory, "appearance-golden.txt");
        Assert(File.Exists(golden), "Missing reference appearance fixture");
        var expected = File.ReadAllLines(golden).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Split(' ')).ToDictionary(p => p[0], p => p[1]);
        foreach (var product in TimberProducts.MenuOrder)   // natural Log/Bamboo have no TimberPanel golden
        {
            string hash = AppearanceHash(product);
            Console.WriteLine($"  {product} {hash}");
            Assert(hash == AppearanceHash(product), "Appearance is nondeterministic");
            Assert(hash == expected[product.ToString()], "Reference appearance differs for " + product);
            var thin = new TimberPanel(product, 50, 40, 1, 0); thin.Col(10, 10, 0.5);
        }
    }

    static void StaleCallback()
    {
        var gate = new RefinementGate(); var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>(); int published = 0;
        gate.Start(_ => { }, queued.Enqueue, () => published++, _ => throw new Exception("Unexpected failure"));
        gate.Work.GetAwaiter().GetResult();
        gate.Cancel();
        while (queued.TryDequeue(out var action)) action();
        Assert(published == 0, "An old solution published after cancellation");
        gate.Start(_ => { }, queued.Enqueue, () => published++, _ => { }); gate.Work.GetAwaiter().GetResult();
        while (queued.TryDequeue(out var action)) action();
        Assert(published == 1, "Current solution did not publish");
    }

    static void Cancellation()
    {
        var gate = new RefinementGate(); int completed = 0, failed = 0;
        using var started = new ManualResetEventSlim();
        gate.Start(token => { started.Set(); token.WaitHandle.WaitOne(); token.ThrowIfCancellationRequested(); }, a => a(), () => completed++, _ => failed++);
        started.Wait(); gate.Cancel(); gate.Work.GetAwaiter().GetResult();
        Assert(completed == 0 && failed == 0, "Cancellation published a result or failure");
        gate.Start(_ => throw new IOException("test"), a => a(), () => completed++, _ => failed++); gate.Work.GetAwaiter().GetResult();
        Assert(failed == 1, "Worker failure was not reported");
        for (int i = 0; i < 100; i++) { gate.Start(_ => { }, a => a(), () => { }, _ => { }); gate.Cancel(); }
        gate.Work.GetAwaiter().GetResult();
    }
}
