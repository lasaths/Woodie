using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
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
using Woodie.Stock;

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
    // Viewport-only display: Textured | Gradient | ForceFlow (mutually exclusive).
    enum FieldMode : byte { Textured = 0, Gradient = 1, ForceFlow = 2 }
    FieldMode fieldMode;
    // Natural Log/Bamboo have no knot-influence field; keep Textured only.
    bool fieldModesEnabled = true;
    /// <summary>Example/authoring: Gradient mode (legacy Show knot influence).</summary>
    internal bool ShowKnotInfluence
    {
        get => fieldMode == FieldMode.Gradient;
        set => fieldMode = value ? FieldMode.Gradient : FieldMode.Textured;
    }
    internal byte PreviewDisplayModeByte => (byte)fieldMode;
    internal void SetPreviewDisplayModeForChecks(byte mode)
    {
        if (mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
        fieldMode = (FieldMode)mode;
    }
    readonly List<Mesh> influenceMeshes = new();
    readonly List<Line> influenceOutlines = new();
    readonly List<(Point3d A, Point3d B, Color Color)> flowSegments = new();
    readonly List<(Plane Frame, double UnitToMm, IMaterialModel Panel, BoundingBox Box)> fibreContexts = new();
    DisplayMaterial influenceFill;

    public PreviewComponent() : base("Timber Preview", "TimberPreview",
        "Preview timber on geometry or a Stock Model. Right-click for Textured, Gradient or Force flow on engineered timber. " + ResearchCredit.Timber, "Display", "Woodie")
    {
        ObjectChanged += (_, e) =>
        {
            if (e.Type != GH_ObjectEventType.Preview) return;
            if (Hidden) refinement.Cancel();
            else ExpireSolution(true);
        };
    }
    public override Guid ComponentGuid => new("b3bcf727-0468-4fb8-8ee5-55fb8084ffb6");
    protected override Bitmap Icon => Icons.Eye;
    public override BoundingBox ClippingBox => bounds;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
        p.AddGeometryParameter("Geometry", "G", "Mesh, Brep, Surface or Extrusion", GH_ParamAccess.item);
        p.AddParameter(new SettingsParam(), "Settings", "TDS", "Optional timber settings. Default is CLT.", GH_ParamAccess.item);
        p.AddVectorParameter("Fibre", "F", "Fibre direction in the base plane", GH_ParamAccess.item);
        p.AddPlaneParameter("Base Plane", "P", "Optional base plane. X is fibre. Z is thickness.", GH_ParamAccess.item);
        p.AddCurveParameter("Guide Curve", "C", "Optional open centreline for bent timber", GH_ParamAccess.item);
        p.AddBooleanParameter("Show", "S", "Turn preview on or off", GH_ParamAccess.item, true);
        p.AddTextParameter("Geometry Units", "U", "Source units. Default is Document.", GH_ParamAccess.item);
        p[0].Optional = true; // Stock Model can supply geometry alone
        for (int i = 1; i <= 4; i++) p[i].Optional = true;
        p[6].Optional = true;
        p.AddGeometryParameter("Stock Geometry", "SG", "Optional uncut stock. Locks material coordinates for cuts.", GH_ParamAccess.item);
        p[7].Optional = true;
        p.AddParameter(new StockParam(), "Stock Model", "Stock", "Optional Stock Model. Prefer this alone. Do not also wire Geometry or Settings.", GH_ParamAccess.item);
        p[8].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
        p.AddMeshParameter("Meshes", "M", "Preview meshes with viewport textures", GH_ParamAccess.list);
        p.AddTextParameter("Products", "P", "Product name for each mesh", GH_ParamAccess.list);
        p.AddBoxParameter("Aligned Box", "B", "Bounds in the fibre-aligned frame", GH_ParamAccess.item);
        p.AddPlaneParameter("Frame", "F", "Material frame at the stock or display minimum corner", GH_ParamAccess.item);
    }

    protected override void BeforeSolveInstance()
    {
        InvalidateBake();
        refinement.Cancel();
        ReleasePages();
        fibreContexts.Clear();
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
        GeometryBase geometry;
        TimberSettings settings;
        GeometryBase stock;
        if (hasModel) {
            // Stock Model is authoritative; ignore override ports (common when G and Stock are both wired from Natural/Material Stock).
            if (hasGeometry || hasSettings || hasFibre || hasPlane || hasGuide || stockGoo != null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Stock Model sets geometry and settings. The component ignores override inputs.");
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
        fieldModesEnabled = !settings.IsNatural;
        if (!fieldModesEnabled && fieldMode != FieldMode.Textured)
        {
            fieldMode = FieldMode.Textured;
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Gradient and Force flow need engineered timber. Natural stock stays Textured.");
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
            // One strand set per solved part; material bbox unions every atlas page.
            if (displayed.Count > 0 && displayed[0].Panel != null)
            {
                var box = BoundingBox.Empty;
                foreach (var page in displayed)
                    if (page.Tiles != null)
                        foreach (var tile in page.Tiles) { box.Union(tile.A); box.Union(tile.B); box.Union(tile.C); }
                if (box.IsValid) fibreContexts.Add((part.Alignment.Frame, part.UnitToMm, displayed[0].Panel.Value, box));
            }
            da.SetDataList(0, displayed.Select(m => m.Mesh));
            da.SetDataList(1, displayed.Select(m => settings.Layers == null ? m.Product.ToString() : "Custom buildup (" + m.Product + " palette)"));
            Message = part.Mapping + (pending.Count > 0 && fieldMode == FieldMode.Textured ? " · refining" : "") + FieldModeSuffix();
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
        ApplyFieldDisplay();
        // Bake full atlases even in Gradient/Force flow so returning to Textured is not stuck on 0.15 quality.
        if (pending.Count > 0 && !Hidden)
        {
            var pages = pending.ToArray();
            refinement.Start(token => TextureAtlas.BakeMissing(pages, token),
                action => RhinoApp.InvokeOnUiThread(action),
                () => ScheduleRefinementExpire(),
                ex => { if (OnPingDocument() != null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Texture refinement failed: " + ex.Message); Message = "Refinement failed"; RhinoDoc.ActiveDoc?.Views.Redraw(); } });
        }
        base.AfterSolveInstance();
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);
        Menu_AppendSeparator(menu);
        Menu_AppendItem(menu, "Textured", (_, _) => SetFieldMode(FieldMode.Textured), true, fieldMode == FieldMode.Textured);
        if (!fieldModesEnabled) return;
        Menu_AppendItem(menu, "Gradient", (_, _) => SetFieldMode(FieldMode.Gradient), true, fieldMode == FieldMode.Gradient);
        Menu_AppendItem(menu, "Force flow", (_, _) => SetFieldMode(FieldMode.ForceFlow), true, fieldMode == FieldMode.ForceFlow);
    }

    string FieldModeSuffix() => fieldMode switch
    {
        FieldMode.Gradient => " · gradient",
        FieldMode.ForceFlow => " · force flow",
        _ => ""
    };

    void StripFieldModeSuffix()
    {
        foreach (var tag in new[] { " · force flow", " · gradient", " · knot influence", " · fibre strands", " · refining" })
            if (Message != null && Message.EndsWith(tag, StringComparison.Ordinal))
                Message = Message[..^tag.Length];
    }

    void ScheduleRefinementExpire()
    {
        var doc = OnPingDocument();
        if (doc == null || Hidden) return;
        // Always schedule: ExpireSolution(false) while idle clears the preview and never recomputes.
        doc.ScheduleSolution(5, _ =>
        {
            if (Hidden || OnPingDocument() != doc) return;
            ExpireSolution(false);
        });
    }

    void SetFieldMode(FieldMode mode)
    {
        if (!fieldModesEnabled && mode != FieldMode.Textured) return;
        if (fieldMode == mode) return;
        RecordUndoEvent("Preview display mode");
        fieldMode = mode;
        // Keep texture refinement running; match AfterSolveInstance order for materials then field draw.
        UpdateMaterials();
        ApplyFieldDisplay();
        StripFieldModeSuffix();
        Message = (Message ?? "Preview") + FieldModeSuffix();
        if (fieldMode == FieldMode.Textured && pending.Count > 0 && !Hidden) ExpireSolution(true);
        else RhinoDoc.ActiveDoc?.Views.Redraw();
    }

    void ApplyFieldDisplay()
    {
        foreach (var mesh in influenceMeshes) mesh.Dispose();
        influenceMeshes.Clear();
        influenceOutlines.Clear();
        flowSegments.Clear();
        foreach (var page in shown) page.Mesh.VertexColors.Clear();
        if (fieldMode == FieldMode.Textured || !fieldModesEnabled) return;

        foreach (var page in shown)
            AddCreaseOutlines(page.Mesh, influenceOutlines);

        // Gradient and Force flow share the influence false-colour shell.
        if (fieldMode == FieldMode.Gradient || fieldMode == FieldMode.ForceFlow)
            BuildInfluenceShells();

        if (fieldMode != FieldMode.ForceFlow) return;

        // Force flow: streamlines clipped to the cut's material bbox, coloured with the same heat LUT.
        foreach (var (frame, unitToMm, panel, box) in fibreContexts)
        {
            if (unitToMm <= 0 || panel == null || !box.IsValid) continue;
            var clip = box; clip.Inflate(0.5);
            var lines = panel switch
            {
                TimberPanel timber => timber.TraceFibreFlow(clip.Min.X, clip.Max.X, clip.Min.Y, clip.Max.Y, clip.Min.Z, clip.Max.Z),
                StockDefinition stock => stock.TraceFibreFlow(clip.Min.X, clip.Max.X, clip.Min.Y, clip.Max.Y, clip.Min.Z, clip.Max.Z),
                _ => null
            };
            if (lines == null || lines.Count == 0) continue;
            double s = unitToMm;
            foreach (var line in lines)
            {
                for (int i = 1; i < line.Count; i++)
                {
                    var a = line[i - 1]; var b = line[i];
                    var mid = ((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5, (a.Z + b.Z) * 0.5);
                    var color = InfluenceHeat(panel.KnotInfluence(mid.Item1, mid.Item2, mid.Item3));
                    flowSegments.Add((
                        frame.PointAt(a.X / s, a.Y / s, a.Z / s),
                        frame.PointAt(b.X / s, b.Y / s, b.Z / s),
                        color));
                }
            }
        }
        if (fibreContexts.Count > 0 && flowSegments.Count == 0)
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Force flow needs a single solid-timber stock piece; layered or multi-strip stock shows influence colour only.");
    }

    void BuildInfluenceShells()
    {
        int faces = 0;
        foreach (var page in shown) faces += page.Mesh?.Faces.Count ?? 0;
        // Cap depth on multi-slat demos so a mode switch does not freeze the UI thread.
        int maxDepth = faces > 4000 ? 1 : faces > 1500 ? 2 : 4;
        foreach (var page in shown)
        {
            if (page.Tiles == null || page.Tiles.Count == 0 || page.Panel == null) continue;
            var panel = page.Panel.Value;
            try
            {
                var dense = new Mesh();
                for (int i = 0; i < page.Tiles.Count; i++)
                {
                    var tile = page.Tiles[i];
                    int v = 3 * i;
                    SubdivideInfluence(dense, panel,
                        page.Mesh.Vertices[v], page.Mesh.Vertices[v + 1], page.Mesh.Vertices[v + 2],
                        tile.A, tile.B, tile.C, 0, maxDepth);
                }
                dense.Normals.ComputeNormals();
                influenceMeshes.Add(dense);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Influence colour shell failed; using coarse vertex colours. " + ex.Message);
                foreach (var tile in page.Tiles)
                {
                    page.Mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(tile.A.X, tile.A.Y, tile.A.Z)));
                    page.Mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(tile.B.X, tile.B.Y, tile.B.Z)));
                    page.Mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(tile.C.X, tile.C.Y, tile.C.Z)));
                }
            }
        }
    }

    // Hard edges only (~35°), so blanks/slats read as solids without a triangulation wireframe.
    static void AddCreaseOutlines(Mesh mesh, List<Line> lines, double creaseDegrees = 35)
    {
        if (mesh == null || mesh.Faces.Count == 0) return;
        mesh.FaceNormals.ComputeFaceNormals();
        double cosLimit = Math.Cos(creaseDegrees * Math.PI / 180);
        var edges = mesh.TopologyEdges;
        for (int i = 0; i < edges.Count; i++)
        {
            var faces = edges.GetConnectedFaces(i);
            if (faces.Length == 1) { lines.Add(edges.EdgeLine(i)); continue; }
            if (faces.Length != 2) continue;
            if (mesh.FaceNormals[faces[0]] * mesh.FaceNormals[faces[1]] < cosLimit)
                lines.Add(edges.EdgeLine(i));
        }
    }

    // Midpoint split until edges ~8 mm (material) or maxDepth — denser than the old 20 mm / depth-2 wash.
    static void SubdivideInfluence(Mesh mesh, IMaterialModel panel, Point3d wa, Point3d wb, Point3d wc, Point3d ma, Point3d mb, Point3d mc, int depth, int maxDepth)
    {
        double maxMat2 = Math.Max(ma.DistanceToSquared(mb), Math.Max(mb.DistanceToSquared(mc), mc.DistanceToSquared(ma)));
        if (depth >= maxDepth || maxMat2 <= 64)
        {
            int i = mesh.Vertices.Count;
            mesh.Vertices.Add(wa); mesh.Vertices.Add(wb); mesh.Vertices.Add(wc);
            mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(ma.X, ma.Y, ma.Z)));
            mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(mb.X, mb.Y, mb.Z)));
            mesh.VertexColors.Add(InfluenceHeat(panel.KnotInfluence(mc.X, mc.Y, mc.Z)));
            mesh.Faces.AddFace(i, i + 1, i + 2);
            return;
        }
        Point3d wab = Mid(wa, wb), wbc = Mid(wb, wc), wca = Mid(wc, wa);
        Point3d mab = Mid(ma, mb), mbc = Mid(mb, mc), mca = Mid(mc, ma);
        SubdivideInfluence(mesh, panel, wa, wab, wca, ma, mab, mca, depth + 1, maxDepth);
        SubdivideInfluence(mesh, panel, wab, wb, wbc, mab, mb, mbc, depth + 1, maxDepth);
        SubdivideInfluence(mesh, panel, wca, wbc, wc, mca, mbc, mc, depth + 1, maxDepth);
        SubdivideInfluence(mesh, panel, wab, wbc, wca, mab, mbc, mca, depth + 1, maxDepth);
    }

    static Point3d Mid(Point3d a, Point3d b) => new(0.5 * (a.X + b.X), 0.5 * (a.Y + b.Y), 0.5 * (a.Z + b.Z));

    // Soft influence area: 0 far (cool) → 1 at knot (hot). Gamma lifts the halo so gradients read.
    internal static Color InfluenceHeat(double soft)
    {
        double t = Math.Min(1, Math.Pow(Math.Max(0, soft), 0.4));
        if (t < 1.0 / 3) { double u = t * 3; return Color.FromArgb((int)(15 + u * 30), (int)(30 + u * 160), (int)(140 + u * 80)); }
        if (t < 2.0 / 3) { double u = (t - 1.0 / 3) * 3; return Color.FromArgb((int)(45 + u * 200), (int)(190 - u * 20), (int)(220 - u * 170)); }
        double v = (t - 2.0 / 3) * 3; return Color.FromArgb((int)(245 - v * 15), (int)(170 - v * 130), (int)(50 - v * 35));
    }

    void UpdateMaterials()
    {
        foreach (var material in materials.Values) material.Dispose();
        materials.Clear();
        influenceFill?.Dispose();
        influenceFill = null;
        if (fieldMode == FieldMode.Gradient || fieldMode == FieldMode.ForceFlow)
        {
            // Dark underlay so InfluenceHeat false-colour reads; Force flow keeps it more open for streamlines.
            double glass = fieldMode == FieldMode.ForceFlow ? 0.45 : 0.2;
            influenceFill = new DisplayMaterial(Color.FromArgb(42, 56, 68))
            {
                Diffuse = Color.FromArgb(42, 56, 68),
                Specular = Color.FromArgb(20, 24, 28),
                Shine = 0.08,
                Emission = Color.FromArgb(8, 12, 16),
                Transparency = glass
            };
            return;
        }
        foreach (string path in shown.Select(m => m.TexturePath).Distinct())
        {
            var material = new DisplayMaterial(Color.White) { Diffuse = Color.White, Specular = Color.Black, Shine = 0.05 };
            material.SetBitmapTexture(path, true); materials.Add(path, material);
        }
    }

    public override bool Write(GH_IWriter writer)
    {
        writer.SetBoolean("ShowKnotInfluence", fieldMode == FieldMode.Gradient);
        writer.SetByte("PreviewDisplayMode", (byte)fieldMode);
        return base.Write(writer);
    }

    public override bool Read(GH_IReader reader)
    {
        if (reader.ItemExists("PreviewDisplayMode"))
        {
            byte mode = reader.GetByte("PreviewDisplayMode");
            fieldMode = mode <= 2 ? (FieldMode)mode : FieldMode.Textured;
        }
        else
            fieldMode = reader.ItemExists("ShowKnotInfluence") && reader.GetBoolean("ShowKnotInfluence")
                ? FieldMode.Gradient : FieldMode.Textured;
        return base.Read(reader);
    }

    void ReleasePages()
    {
        foreach (var material in materials.Values) material.Dispose();
        materials.Clear();
        foreach (var mesh in influenceMeshes) mesh.Dispose();
        influenceMeshes.Clear();
        influenceOutlines.Clear();
        flowSegments.Clear();
        fibreContexts.Clear();
        influenceFill?.Dispose();
        influenceFill = null;
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
            if(revision==solutionVersion&&OnPingDocument()!=null&&!Hidden&&pending.Count>0)ScheduleRefinementExpire();
        }
    }
    public override void DrawViewportMeshes(IGH_PreviewArgs args)
    {
        if (Hidden) return;
        if (fieldMode == FieldMode.Gradient || fieldMode == FieldMode.ForceFlow)
        {
            if (influenceFill != null)
                foreach (var page in shown) args.Display.DrawMeshShaded(page.Mesh, influenceFill);
            // Dense shells and per-page vertex-colour fallbacks are independent (mixed success across atlas pages).
            foreach (var mesh in influenceMeshes)
                if (mesh.VertexColors.Count == mesh.Vertices.Count) args.Display.DrawMeshFalseColors(mesh);
            foreach (var page in shown)
                if (page.Mesh.VertexColors.Count == page.Mesh.Vertices.Count)
                    args.Display.DrawMeshFalseColors(page.Mesh);
            return;
        }
        foreach (var page in shown)
            if (materials.TryGetValue(page.TexturePath, out var material)) args.Display.DrawMeshShaded(page.Mesh, material);
    }
    public override void DrawViewportWires(IGH_PreviewArgs args)
    {
        if (Hidden || fieldMode == FieldMode.Textured) return;
        var outline = fieldMode == FieldMode.ForceFlow
            ? Color.FromArgb(255, 90, 96, 104)
            : Color.FromArgb(255, 245, 248, 252);
        foreach (var line in influenceOutlines) args.Display.DrawLine(line, outline, 2);
        if (fieldMode != FieldMode.ForceFlow) return;
        foreach (var (a, b, color) in flowSegments)
            args.Display.DrawLine(a, b, color, 2);
    }
}
