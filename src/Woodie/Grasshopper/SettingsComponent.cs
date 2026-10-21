using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Woodie.Appearance;

namespace Woodie.Grasshopper;

public sealed class SettingsComponent : GH_Component
{
    public SettingsComponent() : base("Timber Material", "TimberMaterial", "Click the arrow beneath the icon to show or hide material controls. Choose a product and appearance; expand Advanced inputs for overrides. Resolution is in mm/pixel. " + ResearchCredit.Timber, "Display", "Woodie") { }
    public override Guid ComponentGuid => new("7a74fc91-6ed6-447b-adb6-2e2fcd425383");
    protected override Bitmap Icon => Icons.Sliders;
    public TimberProduct SelectedProduct { get; private set; } = TimberProduct.CLT;
    public TimberLook SelectedLook { get; private set; } = TimberLook.Natural;
    public bool PanelExpanded { get; private set; } = true;
    public bool Advanced => Params.Input.Count > 3;
    public string AdvancedNotice { get; private set; }
    public bool ProductControlled => Params.Input[0].SourceCount > 0 || ((Param_String)Params.Input[0]).PersistentDataCount > 0;
    public string ProductSource => Params.Input[0].SourceCount > 0 ? "Product: wired input" : ProductControlled ? "Product: stored input" : null;
    public override void CreateAttributes() => m_attributes = new MaterialAttributes(this);

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        // Names and indices 0..12 remain the legacy contract. Product has no stored default in new instances.
        p.AddTextParameter("Product", "P", "Optional product override (text or product list); controls the Product dropdown when supplied", GH_ParamAccess.item);
        p.AddIntegerParameter("Seed", "S", "Random seed", GH_ParamAccess.item, 0);
        p.AddNumberParameter("Resolution", "R", "Large-face resolution in mm/pixel (smaller = finer)", GH_ParamAccess.item, 2);
        for (int i = 0; i < p.ParamCount; i++) p[i].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Woodie timber material settings", GH_ParamAccess.item);
        p.AddTextParameter("Product", "P", "Canonical product identifier", GH_ParamAccess.item);
    }
    static IGH_Param AdvancedParameter(int index) => index switch {
        3 => Number("Edge Resolution", "ER", "Edge resolution in mm/pixel; product default 0.5, KertoQ 0.25"),
        4 => Number("Knot Density", "KD", "Knots/metre (0–100); overrides the selected appearance"),
        5 => Number("Ring Spacing", "RS", "Annual ring spacing in mm"),
        6 => Number("Lamella Width", "LW", "CLT / Three-layer panel board width in mm"),
        7 => Number("Brightness", "B", "Brightness multiplier; default 1"),
        8 => Colour("Earlywood A", "E0"), 9 => Colour("Earlywood B", "E1"),
        10 => Colour("Latewood", "LC"), 11 => Colour("Knot", "KC"), 12 => Colour("Glue", "GC"),
        13 => Number("Veneer Thickness", "VT", "Nominal veneer thickness in mm for Plywood (1.5) or parallel LVL (3); fitted to total depth"),
        _ => null };
    static Param_Number Number(string name, string nick, string description) => new() { Name = name, NickName = nick, Description = description, Access = GH_ParamAccess.item, Optional = true };
    static Param_Colour Colour(string name, string nick) => new() { Name = name, NickName = nick, Description = "Optional palette override", Access = GH_ParamAccess.item, Optional = true };

    void Change(string title, Action action)
    {
        var doc = OnPingDocument();
        void Apply() { RecordUndoEvent(title); action(); Attributes?.ExpireLayout(); ExpireSolution(false); }
        if (doc?.SolutionDepth > 0) doc.ScheduleSolution(1, _ => Apply());
        else { Apply(); doc?.ScheduleSolution(1, _ => ExpireSolution(false)); }
    }
    public void SetPanelExpanded(bool expanded)
    {
        if (PanelExpanded == expanded) return;
        var doc = OnPingDocument();
        void Apply() {
            RecordUndoEvent(expanded ? "Show material controls" : "Hide material controls");
            PanelExpanded = expanded;
            doc?.Modified();
            Attributes?.ExpireLayout();
            global::Grasshopper.Instances.RedrawCanvas();
        }
        // This is a layout change only: keep solved outputs and texture refinement intact.
        if (doc?.SolutionDepth > 0) doc.ScheduleSolution(1, _ => Apply()); else Apply();
    }
    public void SelectProduct(TimberProduct product)
    {
        if (!Enum.IsDefined(typeof(TimberProduct), product) || ProductControlled || product == SelectedProduct) return;
        Change("Timber product", () => SelectedProduct = product);
    }
    public void SelectLook(TimberLook look)
    {
        if (!Enum.IsDefined(typeof(TimberLook), look) || look == SelectedLook) return;
        Change("Timber appearance", () => SelectedLook = look);
    }
    static bool HasOverride(IGH_Param p, int index)
    {
        if (p.SourceCount > 0) return true;
        if (p is Param_Number number) {
            var stored = number.PersistentData.AllData(true).ToArray();
            // The old Brightness port carried 1 as its default; removing that default is lossless.
            return stored.Length > 0 && !(index == 7 && stored.Length == 1 && stored[0] is GH_Number n && n.Value == 1);
        }
        return p is Param_Colour colour && colour.PersistentDataCount > 0;
    }
    public bool SetAdvanced(bool expanded)
    {
        if (expanded == Advanced) return true;
        if (!expanded && Params.Input.Skip(3).Select((p, i) => HasOverride(p, i + 3)).Any(v => v)) {
            AdvancedNotice = "Advanced inputs retained: disconnect wires and clear stored overrides before collapsing.";
            Attributes?.ExpireLayout(); return false;
        }
        Change("Timber advanced inputs", () => {
            AdvancedNotice = null;
            if (expanded) EnsureAdvanced();
            else { for (int i = Params.Input.Count - 1; i >= 3; i--) Params.UnregisterInputParameter(Params.Input[i], true); Params.OnParametersChanged(); }
        });
        return true;
    }
    void EnsureAdvanced()
    {
        if (Attributes == null) CreateAttributes();
        for (int i = Params.Input.Count; i < 14; i++) Params.RegisterInputParam(AdvancedParameter(i));
        Params.OnParametersChanged();
    }
    protected override void BeforeSolveInstance()
    {
        if (AdvancedNotice != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, AdvancedNotice);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        string name = null;
        var product = SelectedProduct;
        if (da.GetData(0, ref name) && !TimberProducts.TryParse(name, out product)) {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown product. Choose SolidTimber, KVH, GL, CLT, ThreeS, Plywood, ParallelLVL or KertoQ."); return;
        }
        var s = new TimberSettings { Product = product, Look = SelectedLook };
        int seed = 0; if (da.GetData(1, ref seed)) s.Seed = seed;
        double n = 0; if (da.GetData(2, ref n)) s.Resolution = n;
        if (Advanced) {
            if (da.GetData(3, ref n)) s.EdgeResolution = n;
            if (da.GetData(4, ref n)) s.KnotDensity = n;
            if (da.GetData(5, ref n)) s.RingSpacing = n;
            if (da.GetData(6, ref n)) s.LamellaWidth = n;
            if (da.GetData(7, ref n)) s.Brightness = n;
            Color color = Color.Empty;
            if (da.GetData(8, ref color)) s.EarlywoodA = color;
            if (da.GetData(9, ref color)) s.EarlywoodB = color;
            if (da.GetData(10, ref color)) s.Latewood = color;
            if (da.GetData(11, ref color)) s.Knot = color;
            if (da.GetData(12, ref color)) s.Glue = color;
            if (Params.Input.Count > 13 && da.GetData(13, ref n)) s.VeneerThickness = n;
        }
        try { s.Validate(); da.SetData(0, new SettingsGoo(s)); da.SetData(1, product.ToString()); }
        catch (ArgumentException ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message); }
    }
    public override bool Write(GH_IWriter writer)
    {
        writer.SetInt32("WoodieMaterialVersion", 3); writer.SetBoolean("PanelExpanded", PanelExpanded); writer.SetInt32("SelectedProduct", (int)SelectedProduct);
        writer.SetInt32("SelectedLook", (int)SelectedLook); writer.SetBoolean("Advanced", Advanced);
        return base.Write(writer);
    }
    public override bool Read(GH_IReader reader)
    {
        bool legacy = !reader.ItemExists("WoodieMaterialVersion");
        PanelExpanded = !reader.ItemExists("PanelExpanded") || reader.GetBoolean("PanelExpanded");
        SelectedProduct = reader.ItemExists("SelectedProduct") ? (TimberProduct)reader.GetInt32("SelectedProduct") : TimberProduct.CLT;
        SelectedLook = reader.ItemExists("SelectedLook") ? (TimberLook)reader.GetInt32("SelectedLook") : TimberLook.Natural;
        if (!Enum.IsDefined(typeof(TimberProduct), SelectedProduct)) SelectedProduct = TimberProduct.CLT;
        if (!Enum.IsDefined(typeof(TimberLook), SelectedLook)) SelectedLook = TimberLook.Natural;
        if (legacy || reader.GetBoolean("Advanced")) EnsureAdvanced();
        else for (int i = Params.Input.Count - 1; i >= 3; i--) Params.UnregisterInputParameter(Params.Input[i], true);
        bool ok = base.Read(reader);
        Name = "Timber Material"; if (NickName == "TimberDisplay") NickName = "TimberMaterial";
        // Legacy archives restore all original ports, including IDs, wires and persistent data.
        if (legacy) { EnsureAdvanced(); AdvancedNotice = "Legacy advanced inputs retained to preserve wires and stored settings."; }
        if (Attributes == null) CreateAttributes();
        if (Params.Output.Count == 1) Params.RegisterOutputParam(new Param_String { Name = "Product", NickName = "P", Description = "Canonical product identifier", Access = GH_ParamAccess.item });
        Params.OnParametersChanged(); Attributes?.ExpireLayout(); return ok;
    }
}
