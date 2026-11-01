using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Stock;

namespace Woodie.Grasshopper;

/// <summary>Construction-ready bamboo culm from a base→tip Line. Species presets set cured skin and starting dimensions.</summary>
public sealed class BambooStockComponent : GH_Component
{
    public BambooStockComponent() : base("Bamboo Stock", "BambooStock",
        "Build a hollow bamboo culm Stock Model on a Line. Open the panel arrow to choose species. " + ResearchCredit.Timber,
        "Display", "Woodie") { }
    public override Guid ComponentGuid => new("d65cfaa6-137e-4547-91b5-33e5960ea8ca");
    protected override Bitmap Icon => Icons.Plant;
    public BambooSpeciesId SelectedSpecies { get; private set; } = BambooSpeciesId.Moso;
    public bool PanelExpanded { get; private set; } = true;
    // Compact: Line, Species, Settings, Diameter. Wall/Nodes/Taper/Variation/Units sit under Advanced.
    public bool Advanced => Params.Input.Count > 4;
    public string AdvancedNotice { get; private set; }
    /// <summary>True when a Species wire drives the list. Stored Species text does not lock the panel.</summary>
    public bool SpeciesControlled => Params.Input[1].SourceCount > 0;
    public string SpeciesSource => SpeciesControlled ? "Species: wired input" : null;
    public override void CreateAttributes() => m_attributes = new BambooStockAttributes(this);

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddLineParameter("Line", "L", "Axis from base to tip, in Geometry Units", GH_ParamAccess.item);
        p.AddTextParameter("Species", "S", "Optional species name. A wire locks the Species list. " + BambooSpecies.KindHelp, GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional seed, colours and resolution. Product becomes Bamboo. Diameter comes from the Diameter input, not Settings.", GH_ParamAccess.item);
        p.AddIntervalParameter("Diameter", "D", "Base diameter range in mm. Default is the species trade range. A single number is a zero-width range.", GH_ParamAccess.item);
        p[1].Optional = true;
        p[2].Optional = true;
        p[3].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Stock Model for Cut and Preview", GH_ParamAccess.item);
        p.AddGeometryParameter("Geometry", "G", "Closed Brep in document units", GH_ParamAccess.item);
    }

    static IGH_Param AdvancedParameter(int index) => index switch
    {
        4 => new Param_Number { Name = "Wall", NickName = "W", Description = "Wall thickness in mm. Overrides the species default when set.", Access = GH_ParamAccess.item, Optional = true },
        5 => new Param_Number { Name = "Node Spacing", NickName = "N", Description = "Node spacing in mm. Overrides the species default when set.", Access = GH_ParamAccess.item, Optional = true },
        6 => new Param_Number { Name = "Taper", NickName = "T", Description = "Diameter loss per metre, in mm. Overrides the species default when set.", Access = GH_ParamAccess.item, Optional = true },
        7 => new Param_Number { Name = "Variation", NickName = "V", Description = "Specimen spread from 0 to 1", Access = GH_ParamAccess.item, Optional = true },
        _ => new Param_String { Name = "Geometry Units", NickName = "U", Description = "Source units of the Line. Default is Document.", Access = GH_ParamAccess.item, Optional = true },
    };

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
            RecordUndoEvent(expanded ? "Show bamboo species" : "Hide bamboo species");
            PanelExpanded = expanded;
            doc?.Modified();
            Attributes?.ExpireLayout();
            global::Grasshopper.Instances.RedrawCanvas();
        }
        if (doc?.SolutionDepth > 0) doc.ScheduleSolution(1, _ => Apply()); else Apply();
    }
    public void SelectSpecies(BambooSpeciesId id)
    {
        if (!Enum.IsDefined(typeof(BambooSpeciesId), id) || SpeciesControlled) return;
        if (id == SelectedSpecies && ((Param_String)Params.Input[1]).PersistentDataCount == 0) return;
        Change("Bamboo species", () => {
            SelectedSpecies = id;
            // Clear stored Species text so SolveInstance does not re-apply a stale panel value.
            var species = (Param_String)Params.Input[1];
            species.PersistentData.Clear();
            species.ClearData();
            // Drop a previous Diameter override so the new species trade range applies.
            if (Params.Input[3] is Param_Interval diameter) { diameter.PersistentData.Clear(); diameter.ClearData(); }
        });
    }

    static bool HasOverride(IGH_Param p) =>
        p.SourceCount > 0 || p is Param_Number n && n.PersistentDataCount > 0 || p is Param_String s && s.PersistentDataCount > 0;

    public bool SetAdvanced(bool expanded)
    {
        if (expanded == Advanced) return true;
        if (!expanded && Params.Input.Skip(4).Any(HasOverride)) {
            AdvancedNotice = "Advanced inputs retained: disconnect wires and clear stored overrides before collapsing.";
            Attributes?.ExpireLayout(); return false;
        }
        Change("Bamboo advanced inputs", () => {
            AdvancedNotice = null;
            if (expanded) EnsureAdvanced();
            else { for (int i = Params.Input.Count - 1; i >= 4; i--) Params.UnregisterInputParameter(Params.Input[i], true); Params.OnParametersChanged(); }
        });
        return true;
    }
    void EnsureAdvanced()
    {
        if (Attributes == null) CreateAttributes();
        for (int i = Params.Input.Count; i < 9; i++) Params.RegisterInputParam(AdvancedParameter(i));
        Params.OnParametersChanged();
    }

    protected override void BeforeSolveInstance()
    {
        if (AdvancedNotice != null) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, AdvancedNotice);
    }
    protected override void SolveInstance(IGH_DataAccess da)
    {
        var species = BambooSpecies.For(SelectedSpecies);
        string speciesText = null;
        if (da.GetData(1, ref speciesText)) {
            if (!BambooSpecies.TryParse(speciesText, out species)) {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, BambooSpecies.KindHelp); return;
            }
            SelectedSpecies = species.Id;
        }
        var settings = NaturalStockSolve.BaseSettings(da, 2, TimberProduct.Bamboo);
        settings.Species = species.Id;
        // Diameter is a component Domain, not a Settings material property.
        Interval diameter = Interval.Unset;
        Interval? domain = da.GetData(3, ref diameter) && diameter.IsValid ? diameter : null;
        settings.Diameter = BambooSpecies.ResolveDiameter(species, domain);
        double value = 0;
        string units = "Document";
        if (Advanced) {
            settings.WallThickness = da.GetData(4, ref value) ? value : double.IsNaN(settings.WallThickness) ? species.Wall : settings.WallThickness;
            settings.NodeSpacing = da.GetData(5, ref value) ? value : double.IsNaN(settings.NodeSpacing) ? species.Spacing : settings.NodeSpacing;
            settings.Taper = da.GetData(6, ref value) ? value : double.IsNaN(settings.Taper) ? species.Taper : settings.Taper;
            if (da.GetData(7, ref value)) settings.Variation = value;
            da.GetData(8, ref units);
        } else {
            settings.WallThickness = double.IsNaN(settings.WallThickness) ? species.Wall : settings.WallThickness;
            settings.NodeSpacing = double.IsNaN(settings.NodeSpacing) ? species.Spacing : settings.NodeSpacing;
            settings.Taper = double.IsNaN(settings.Taper) ? species.Taper : settings.Taper;
        }
        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, BambooSpecies.Summary(species));
        NaturalStockSolve.Run(this, da, settings, units);
    }

    public override bool Write(GH_IWriter writer)
    {
        writer.SetInt32("WoodieBambooVersion", 3);
        writer.SetBoolean("PanelExpanded", PanelExpanded);
        writer.SetInt32("SelectedSpecies", (int)SelectedSpecies);
        writer.SetBoolean("Advanced", Advanced);
        return base.Write(writer);
    }
    public override bool Read(GH_IReader reader)
    {
        int version = reader.ItemExists("WoodieBambooVersion") ? reader.GetInt32("WoodieBambooVersion") : 0;
        PanelExpanded = !reader.ItemExists("PanelExpanded") || reader.GetBoolean("PanelExpanded");
        SelectedSpecies = reader.ItemExists("SelectedSpecies") ? (BambooSpeciesId)reader.GetInt32("SelectedSpecies") : BambooSpeciesId.Moso;
        if (!Enum.IsDefined(typeof(BambooSpeciesId), SelectedSpecies)) SelectedSpecies = BambooSpeciesId.Moso;
        // v0/v1 always shipped nine ports; v2 hid size overrides. v3 keeps Diameter visible and moves Wall+ under Advanced.
        bool advanced = version < 2 || (reader.ItemExists("Advanced") && reader.GetBoolean("Advanced"));
        // Drop a pre-v3 Number Diameter so the Interval port can take index 3.
        while (Params.Input.Count > 3) Params.UnregisterInputParameter(Params.Input[Params.Input.Count - 1], true);
        if (Params.Input.Count < 4)
            Params.RegisterInputParam(new Param_Interval {
                Name = "Diameter", NickName = "D",
                Description = "Base diameter range in mm. Default is the species trade range. A single number is a zero-width range.",
                Access = GH_ParamAccess.item, Optional = true,
            });
        if (advanced) EnsureAdvanced();
        bool ok = base.Read(reader);
        if (version < 3) AdvancedNotice = "Bamboo Diameter is now a Domain input; species trade ranges apply when it is empty.";
        if (Attributes == null) CreateAttributes();
        Params.OnParametersChanged();
        Attributes?.ExpireLayout();
        return ok;
    }
}
