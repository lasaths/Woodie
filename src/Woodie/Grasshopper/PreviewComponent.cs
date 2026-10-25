using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Input.Custom;
using Woodie.Baking;
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
    readonly List<BakeSnapshot> bakeItems=new();
    CancellationTokenSource activeBake;
    long solutionVersion;

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
        p.AddGeometryParameter("Stock Geometry", "Stock", "Optional original uncut geometry: fixes material coordinates, layers and knots as the displayed geometry is cut. Uses Geometry Units.", GH_ParamAccess.item);
        p[7].Optional = true;
        p.AddParameter(new StockParam(), "Stock Model", "Model", "Optional Woodie stock model; supplies geometry, settings and fixed material coordinates", GH_ParamAccess.item);
        p[8].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddMeshParameter("Meshes", "M", "UV-mapped meshes; Woodie supplies viewport textures", GH_ParamAccess.list);
        p.AddTextParameter("Products", "P", "Timber product per mesh", GH_ParamAccess.list);
        p.AddBoxParameter("Aligned Box", "B", "Full geometry bounds in the fibre-aligned frame", GH_ParamAccess.item);
        p.AddPlaneParameter("Frame", "F", "Material frame anchored at the original stock minimum when Stock Geometry is supplied; otherwise at the displayed box minimum", GH_ParamAccess.item);
    }

    protected override void BeforeSolveInstance()
    {
        InvalidateBake();
        refinement.Cancel();
        ReleasePages();
        bounds = BoundingBox.Unset;
        Message = null;
        base.BeforeSolveInstance();
    }

    public override void ExpireSolution(bool recompute)
    {
        InvalidateBake();
        refinement.Cancel();
        base.ExpireSolution(recompute);
    }

    public override void ClearData()
    {
        InvalidateBake();
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
        StockGoo stockModel = null;
        bool hasModel = Params.Input.Count > 8 && da.GetData(8, ref stockModel) && stockModel?.Value != null;
        IGH_GeometricGoo goo = null;
        bool hasGeometry = da.GetData(0, ref goo);
        SettingsGoo settingsGoo = null;
        bool hasSettings = da.GetData(1, ref settingsGoo) && settingsGoo?.Value != null;
        Vector3d f = Vector3d.Unset; Plane p = Plane.Unset; Curve guide = null;
        bool hasFibre = da.GetData(2, ref f), hasPlane = da.GetData(3, ref p), hasGuide = da.GetData(4, ref guide);
        var doc = RhinoDoc.ActiveDoc;
        var documentUnits = doc?.ModelUnitSystem ?? UnitSystem.Millimeters;
        string sourceUnits = "Document"; if (Params.Input.Count > 6) da.GetData(6, ref sourceUnits);
        IGH_GeometricGoo stockGoo = null;
        if (Params.Input.Count > 7) da.GetData(7, ref stockGoo);
        var legacyStock = stockGoo?.ScriptVariable() as GeometryBase;
        if (stockGoo != null && legacyStock == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Stock Geometry must be Mesh, Brep, Surface or Extrusion."); return; }
        if (hasModel && (hasGeometry || hasSettings || hasFibre || hasPlane || hasGuide || stockGoo != null))
        { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Stock Model mode rejects Geometry, Settings, Fibre, Base Plane, Guide Curve and Stock Geometry overrides. Disconnect them or clear Stock Model."); return; }
        GeometryBase geometry;
        TimberSettings settings;
        GeometryBase stock;
        if (hasModel) {
            geometry = stockModel.Value.Geometry;
            settings = stockModel.Value.Settings.Duplicate();
            stock = stockModel.Value.OriginalGeometry;
            if (stockModel.Value.Fibre.HasValue) { f = stockModel.Value.Fibre.Value; hasFibre = true; }
            if (stockModel.Value.BasePlane.HasValue) { p = stockModel.Value.BasePlane.Value; hasPlane = true; }
            guide = stockModel.Value.Guide; hasGuide = guide != null;
            sourceUnits = "Document";
        } else {
            if (!hasGeometry) return;
            geometry = goo?.ScriptVariable() as GeometryBase;
            if (geometry == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Supply a Mesh, Brep, Surface or Extrusion."); return; }
            settings = hasSettings ? settingsGoo.Value.Duplicate() : new TimberSettings();
            stock = legacyStock;
        }
        double tolerance = doc?.ModelAbsoluteTolerance ?? 0.001;
        var finals = new List<AtlasPage>();
        var quick = new List<AtlasPage>();
        try
        {
            using var input = new GeometryUnits(geometry, guide, hasPlane ? p : null, sourceUnits, documentUnits, stock);
            using var part = PreparedPart.Create(input.Geometry, settings, input.ToMillimetres, tolerance, input.BasePlane, hasFibre ? f : null, input.Guide, input.Stock);
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
            da.SetDataList(1, displayed.Select(m => settings.Layers == null ? m.Product.ToString() : "Custom buildup (" + m.Product + " palette)"));
            Message = part.Mapping + (pending.Count > 0 ? " · refining" : "");
            bakeItems.Add(new BakeSnapshot(input.Geometry,settings,documentUnits,input.BasePlane,hasFibre?f:null,input.Guide,input.Stock));
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
        InvalidateBake();
        refinement.Cancel(); ReleasePages();
        base.RemovedFromDocument(document);
    }
    public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
    {
        activeBake?.Cancel();
        refinement.Cancel();
        base.DocumentContextChanged(document, context);
    }
    sealed class BakeProgress(Action<double> report) : IProgress<double> { public void Report(double value)=>report(value); }
    void InvalidateBake()
    {
        solutionVersion++;activeBake?.Cancel();
        foreach(var item in bakeItems)item.Dispose();bakeItems.Clear();
    }
    public override bool IsBakeCapable=>bakeItems.Count>0 && activeBake==null;
    public override void BakeGeometry(RhinoDoc doc,List<Guid> objIds)=>BakeGeometry(doc,null,objIds);
    public override void BakeGeometry(RhinoDoc doc,ObjectAttributes attributes,List<Guid> objIds)
    {
        if(!IsBakeCapable||doc==null)return;
        refinement.Cancel();
        long revision=solutionVersion;var units=doc.ModelUnitSystem;uint serial=doc.RuntimeSerialNumber;
        var copies=new List<BakeSnapshot>();var plans=new List<NativeBakePlan>();
        using var source=new CancellationTokenSource();activeBake=source;
        EventHandler escape=(_,_)=>source.Cancel();RhinoApp.EscapeKeyPressed+=escape;
        try {
            foreach(var item in bakeItems)copies.Add(item.CopyFor(item.Units));
            using var cancel=new GetCancel { ProgressReporting=true,ProgressMessage="Preparing full-quality native timber textures" };
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(source.Token,cancel.Token);
            foreach(var item in copies) {
                linked.Token.ThrowIfCancellationRequested();plans.Add(new NativeBakePlan(item,doc,linked.Token));
                RhinoApp.Wait();linked.Token.ThrowIfCancellationRequested();
            }
            var task=Task.Run(()=> {
                for(int i=0;i<plans.Count;i++) {
                    int at=i;
                    var progress=new BakeProgress(p=>cancel.Progress.Report((at+p)/plans.Count));
                    plans[i].Render(linked.Token,progress);
                }
            },linked.Token);
            cancel.Wait(task,doc);
            // Cancellation must finish the worker before its immutable jobs are disposed.
            if(!task.IsCompleted)linked.Cancel();
            task.GetAwaiter().GetResult();linked.Token.ThrowIfCancellationRequested();
            if(revision!=solutionVersion||RhinoDoc.FromRuntimeSerialNumber(serial)==null||doc.ModelUnitSystem!=units)throw new OperationCanceledException("The solution or bake document changed.");
            var ids=TimberBaker.Commit(doc,plans,attributes,linked.Token);foreach(var id in ids)objIds.Add(id);
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,$"Baked {ids.Count} native timber object(s). Use Save textures when saving the 3dm for portability. Later shape edits require rebaking.");
        }
        catch(OperationCanceledException){AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,"Timber bake cancelled; no new objects were added.");}
        catch(Exception ex){AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Timber bake failed: "+ex.Message);}
        finally {
            RhinoApp.EscapeKeyPressed-=escape;activeBake=null;
            foreach(var plan in plans)plan.Dispose();foreach(var item in copies)item.Dispose();
            if(revision==solutionVersion&&OnPingDocument()!=null&&!Hidden&&pending.Count>0)ExpireSolution(true);
        }
    }
    public override void DrawViewportMeshes(IGH_PreviewArgs args)
    {
        if (Hidden) return;
        foreach (var page in shown)
            if (materials.TryGetValue(page.TexturePath, out var material)) args.Display.DrawMeshShaded(page.Mesh, material);
    }
    public override void DrawViewportWires(IGH_PreviewArgs args) { }
}
