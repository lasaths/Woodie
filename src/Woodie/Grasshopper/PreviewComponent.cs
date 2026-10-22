using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Geometry;
using Woodie.Rendering;

namespace Woodie.Grasshopper;

public sealed class PreviewComponent : GH_Component
{
    readonly List<AtlasPage> shown = new();
    readonly List<AtlasPage> pending = new();
    readonly Dictionary<string, DisplayMaterial> materials = new();
    readonly RefinementGate refinement = new();
    BoundingBox bounds = BoundingBox.Unset;

    public PreviewComponent() : base("Timber Preview", "TimberPreview", "Preview Rhino geometry as straight or bent timber. " + ResearchCredit.Timber, "Display", "Woodie")
    {
        ObjectChanged += (_, e) =>
        {
            if (e.Type != GH_ObjectEventType.Preview) return;
            if (Hidden) refinement.Cancel();
            else ExpireSolution(true);
        };
    }
    public override Guid ComponentGuid => new("b3bcf727-0468-4fb8-8ee5-55fb8084ffb6");
    protected override Bitmap Icon => Icons.Tree;
    public override BoundingBox ClippingBox => bounds;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Mesh, Brep, Surface or Extrusion; input geometry is unchanged", GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional timber settings; default CLT", GH_ParamAccess.item);
        p.AddVectorParameter("Fibre", "F", "Fibre direction projected into the inferred or supplied base plane", GH_ParamAccess.item);
        p.AddPlaneParameter("Base Plane", "P", "Optional base plane: X is fibre, Z is thickness", GH_ParamAccess.item);
        p.AddCurveParameter("Guide Curve", "C", "Optional smooth open centreline for bent timber; curve direction sets grain X", GH_ParamAccess.item);
        p.AddBooleanParameter("Show", "S", "Enable preview and texture generation", GH_ParamAccess.item, true);
        p.AddTextParameter("Geometry Units", "U", "Source coordinate units: Document (default), Millimetres/mm, Centimetres/cm, Metres/m, Inches or Feet. Converts copies of geometry, guide and base-plane origin into Rhino document units.", GH_ParamAccess.item);
        for (int i = 1; i <= 4; i++) p[i].Optional = true;
        p[6].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddMeshParameter("Meshes", "M", "UV-mapped meshes; Woodie supplies viewport textures", GH_ParamAccess.list);
        p.AddTextParameter("Products", "P", "Timber product per mesh", GH_ParamAccess.list);
        p.AddBoxParameter("Aligned Box", "B", "Full geometry bounds in the fibre-aligned frame", GH_ParamAccess.item);
        p.AddPlaneParameter("Frame", "F", "Fibre-aligned frame anchored at box minimum", GH_ParamAccess.item);
    }

    protected override void BeforeSolveInstance()
    {
        refinement.Cancel();
        ReleasePages();
        bounds = BoundingBox.Unset;
        Message = null;
        base.BeforeSolveInstance();
    }

    public override void ExpireSolution(bool recompute)
    {
        refinement.Cancel();
        base.ExpireSolution(recompute);
    }

    public override void ClearData()
    {
        refinement.Cancel();
        ReleasePages();
        bounds = BoundingBox.Unset;
        Message = null;
        base.ClearData();
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        bool show = true; da.GetData(5, ref show);
        if (!show) return;
        IGH_GeometricGoo goo = null;
        if (!da.GetData(0, ref goo)) return;
        GeometryBase geometry = goo?.ScriptVariable() as GeometryBase;
        if (geometry == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Supply a Mesh, Brep, Surface or Extrusion."); return; }
        SettingsGoo settingsGoo = null;
        var settings = da.GetData(1, ref settingsGoo) && settingsGoo?.Value != null ? settingsGoo.Value.Duplicate() : new TimberSettings();
        Vector3d f = Vector3d.Unset; Plane p = Plane.Unset; Curve guide = null;
        bool hasFibre = da.GetData(2, ref f), hasPlane = da.GetData(3, ref p);
        da.GetData(4, ref guide);
        var doc = RhinoDoc.ActiveDoc;
        var documentUnits = doc?.ModelUnitSystem ?? UnitSystem.Millimeters;
        string sourceUnits = "Document"; if (Params.Input.Count > 6) da.GetData(6, ref sourceUnits);
        double tolerance = doc?.ModelAbsoluteTolerance ?? 0.001;
        var finals = new List<AtlasPage>();
        var quick = new List<AtlasPage>();
        try
        {
            using var input = new GeometryUnits(geometry, guide, hasPlane ? p : null, sourceUnits, documentUnits);
            using var part = PreparedPart.Create(input.Geometry, settings, input.ToMillimetres, tolerance, input.BasePlane, hasFibre ? f : null, input.Guide);
            if (Math.Abs(input.Conversion - 1) > 1e-12)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Converted input coordinates from {input.Source} to {input.Document}; source geometry is unchanged.");
            da.SetData(2, part.Alignment.Box); da.SetData(3, part.Alignment.Frame);
            foreach (string remark in part.Remarks) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, remark);
            finals = TextureAtlas.Prepare(part, settings);
            List<AtlasPage> displayed;
            if (finals.All(m => File.Exists(m.TexturePath))) { displayed = finals; shown.AddRange(finals); }
            else
            {
                quick = TextureAtlas.Prepare(part, settings, 0.15);
                TextureAtlas.BakeMissing(quick, CancellationToken.None);
                displayed = quick; shown.AddRange(quick); pending.AddRange(finals);
            }
            foreach (var page in displayed)
            {
                var box = page.Mesh.GetBoundingBox(false);
                if (!bounds.IsValid) bounds = box; else bounds.Union(box);
            }
            da.SetDataList(0, displayed.Select(m => m.Mesh));
            da.SetDataList(1, displayed.Select(m => m.Product.ToString()));
            Message = part.Mapping + (pending.Count > 0 ? " · refining" : "");
        }
        catch (Exception ex)
        {
            foreach (var page in finals.Concat(quick).Where(page => !shown.Contains(page) && !pending.Contains(page))) page.Dispose();
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex is AppearanceBudgetException ? GeometryUnits.BudgetMessage(ex, sourceUnits, documentUnits) : ex.Message);
        }
    }

    protected override void AfterSolveInstance()
    {
        UpdateMaterials();
        if (pending.Count > 0 && !Hidden)
        {
            var pages = pending.ToArray();
            refinement.Start(token => TextureAtlas.BakeMissing(pages, token),
                action => RhinoApp.InvokeOnUiThread(action),
                () => { if (OnPingDocument() != null && !Hidden) ExpireSolution(true); },
                ex => { if (OnPingDocument() != null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Texture refinement failed: " + ex.Message); Message = "Refinement failed"; RhinoDoc.ActiveDoc?.Views.Redraw(); } });
        }
        base.AfterSolveInstance();
    }

    void UpdateMaterials()
    {
        foreach (var material in materials.Values) material.Dispose();
        materials.Clear();
        foreach (string path in shown.Select(m => m.TexturePath).Distinct())
        {
            var material = new DisplayMaterial(Color.White) { Diffuse = Color.White, Specular = Color.Black, Shine = 0.05 };
            material.SetBitmapTexture(path, true); materials.Add(path, material);
        }
    }

    void ReleasePages()
    {
        foreach (var material in materials.Values) material.Dispose();
        materials.Clear();
        foreach (var page in shown.Concat(pending)) page.Dispose();
        shown.Clear(); pending.Clear();
    }

    public override void RemovedFromDocument(GH_Document document)
    {
        refinement.Cancel(); ReleasePages();
        base.RemovedFromDocument(document);
    }
    public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
    {
        refinement.Cancel();
        base.DocumentContextChanged(document, context);
    }
    public override void DrawViewportMeshes(IGH_PreviewArgs args)
    {
        if (Hidden) return;
        foreach (var page in shown)
            if (materials.TryGetValue(page.TexturePath, out var material)) args.Display.DrawMeshShaded(page.Mesh, material);
    }
    public override void DrawViewportWires(IGH_PreviewArgs args) { }
}
