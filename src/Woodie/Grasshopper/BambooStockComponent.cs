using System.Drawing;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
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
    public bool SpeciesControlled => Params.Input[1].SourceCount > 0 || ((Param_String)Params.Input[1]).PersistentDataCount > 0;
    public string SpeciesSource => Params.Input[1].SourceCount > 0 ? "Species: wired input" : SpeciesControlled ? "Species: stored input" : null;
    public override void CreateAttributes() => m_attributes = new BambooStockAttributes(this);

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddLineParameter("Line", "L", "Axis from base to tip, in Geometry Units", GH_ParamAccess.item);
        p.AddTextParameter("Species", "S", "Optional species name. Controls the Species list when set. " + BambooSpecies.KindHelp, GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional seed, colours and resolution. Product becomes Bamboo. Clear Diameter/Wall/Nodes/Taper on Settings to use the species defaults.", GH_ParamAccess.item);
        p.AddNumberParameter("Diameter", "D", "Base diameter in mm. Overrides the species default when set.", GH_ParamAccess.item);
        p.AddNumberParameter("Wall", "W", "Wall thickness in mm. Overrides the species default when set.", GH_ParamAccess.item);
        p.AddNumberParameter("Node Spacing", "N", "Node spacing in mm. Overrides the species default when set.", GH_ParamAccess.item);
        p.AddNumberParameter("Taper", "T", "Diameter loss per metre, in mm. Overrides the species default when set.", GH_ParamAccess.item);
        p.AddNumberParameter("Variation", "V", "Specimen spread from 0 to 1", GH_ParamAccess.item);
        p.AddTextParameter("Geometry Units", "U", "Source units of the Line. Default is Document.", GH_ParamAccess.item);
        for (int i = 1; i < p.ParamCount; i++) p[i].Optional = true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Stock Model for Cut and Preview", GH_ParamAccess.item);
        p.AddGeometryParameter("Geometry", "G", "Closed Brep in document units", GH_ParamAccess.item);
    }

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
        if (!Enum.IsDefined(typeof(BambooSpeciesId), id) || SpeciesControlled || id == SelectedSpecies) return;
        Change("Bamboo species", () => SelectedSpecies = id);
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
        double value = 0;
        settings.Diameter = da.GetData(3, ref value) ? value : double.IsNaN(settings.Diameter) ? species.Diameter : settings.Diameter;
        settings.WallThickness = da.GetData(4, ref value) ? value : double.IsNaN(settings.WallThickness) ? species.Wall : settings.WallThickness;
        settings.NodeSpacing = da.GetData(5, ref value) ? value : double.IsNaN(settings.NodeSpacing) ? species.Spacing : settings.NodeSpacing;
        settings.Taper = da.GetData(6, ref value) ? value : double.IsNaN(settings.Taper) ? species.Taper : settings.Taper;
        if (da.GetData(7, ref value)) settings.Variation = value;
        string units = "Document"; da.GetData(8, ref units);
        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, BambooSpecies.Summary(species));
        NaturalStockSolve.Run(this, da, settings, units);
    }

    public override bool Write(GH_IWriter writer)
    {
        writer.SetBoolean("PanelExpanded", PanelExpanded);
        writer.SetInt32("SelectedSpecies", (int)SelectedSpecies);
        return base.Write(writer);
    }
    public override bool Read(GH_IReader reader)
    {
        PanelExpanded = !reader.ItemExists("PanelExpanded") || reader.GetBoolean("PanelExpanded");
        SelectedSpecies = reader.ItemExists("SelectedSpecies") ? (BambooSpeciesId)reader.GetInt32("SelectedSpecies") : BambooSpeciesId.Moso;
        if (!Enum.IsDefined(typeof(BambooSpeciesId), SelectedSpecies)) SelectedSpecies = BambooSpeciesId.Moso;
        bool ok = base.Read(reader);
        if (Attributes == null) CreateAttributes();
        Attributes?.ExpireLayout();
        return ok;
    }
}
