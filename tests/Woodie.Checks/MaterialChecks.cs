using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;
using Woodie.Grasshopper;
using Woodie.Rendering;
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
    static SettingsGoo Result(SettingsComponent c) => c.Params.Output[0].VolatileData.AllData(true).OfType<SettingsGoo>().Single();
    public static void Interface()
    {
        IsolatedHost.LoadWoodie(Path.GetFullPath(Path.Combine(Output, "../../src/Woodie/bin/Release/net7.0/Woodie.gha")));
        GH_Document.EnableSolutions = true;
        using var doc = new GH_Document { Enabled = true };
        var c = new SettingsComponent(); c.CreateAttributes(); doc.AddObject(c, false);
        Assert(c.Params.Input.Count == 3 && c.Params.Output.Count == 2 && c.Name == "Timber Material", "New compact interface");
        var ui = (MaterialAttributes)c.Attributes; c.Attributes.PerformLayout();
        static PointF Centre(RectangleF r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        Assert(ui.Click(Centre(ui.ProductBounds)), "Dropdown hit failed"); ui.PerformLayout(); Assert(ui.OptionBounds.Count == 8, "Missing product choices");
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
