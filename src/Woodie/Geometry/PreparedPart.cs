using Rhino.Geometry;
using Woodie.Appearance;
using Woodie.Stock;

namespace Woodie.Geometry;

public sealed class PreparedPart : IDisposable
{
    readonly ICoordinateMap coordinateMap;
    readonly Point3d coordinateMinimum;
    readonly bool usesVirtualDepth;
    PreparedPart(ICoordinateMap map,Point3d minimum,bool virtualDepth) { coordinateMap=map;coordinateMinimum=minimum;usesVirtualDepth=virtualDepth; }
    internal Point3d MaterialPoint(Point3d point) { var p=coordinateMap.Map(point);return new Point3d((p.X-coordinateMinimum.X)*UnitToMm,(p.Y-coordinateMinimum.Y)*UnitToMm,usesVirtualDepth?DepthMm:(p.Z-coordinateMinimum.Z)*UnitToMm); }
    public Mesh Mesh { get; private init; }
    public Point3d[] Coordinates { get; private init; }
    public AlignmentResult Alignment { get; private init; }
    public double LengthMm { get; private init; }
    public double WidthMm { get; private init; }
    public double DepthMm { get; private init; }
    public string Mapping { get; private init; }
    public double UnitToMm { get; private init; }
    public double ToleranceMm { get; private init; }
    public IReadOnlyList<string> Remarks { get; private init; }

    public static PreparedPart Create(GeometryBase geometry, TimberSettings settings, double unitToMm,
        double tolerance, Plane? basePlane = null, Vector3d? fibre = null, Curve guide = null, GeometryBase stock = null,
        PreviewLod previewLod = PreviewLod.Full)
    {
        settings.Validate();
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Geometry is null or invalid.");
        if (stock != null && !stock.IsValid) throw new ArgumentException("Stock Geometry is invalid.");
        if (!double.IsFinite(unitToMm) || unitToMm <= 0) throw new ArgumentException("Document units must have a valid physical scale.");
        var remarks = new List<string>();
        // Natural revolved solids (esp. hollow bamboo) explode under the flat-timber 1° grid; cap edge length in document units.
        double maxEdge = settings.IsNatural ? Math.Max(10 / unitToMm, tolerance * 20) : 0;
        double gridAngle = settings.GridAngle;
        // Draft: coarser MakeMesh for non-tube paths. Engineered: raise angle and skip refine (no tight max-edge — that subdivides long boards).
        if (previewLod == PreviewLod.Draft)
        {
            gridAngle = Math.Max(gridAngle, 15 * Math.PI / 180);
            if (maxEdge > 0) maxEdge = Math.Max(maxEdge, 30 / unitToMm);
        }
        bool refineWhenNoEdge = previewLod != PreviewLod.Draft;
        // Uncut bamboo only: no stock, or stock that matches the culm extent. Cuts keep MakeMesh.
        bool bambooTube = settings.Product == TimberProduct.Bamboo && (stock == null || SameExtent(geometry, stock, tolerance));
        Mesh mesh = null;
        Mesh stockMesh = null;
        try
        {
            if (bambooTube && TryBambooPreviewMesh(geometry, settings, unitToMm, tolerance, basePlane, fibre, previewLod, out mesh, out var tubeFrame, out var lengthMm))
            {
                remarks.Add(previewLod == PreviewLod.Draft
                    ? "Bamboo preview uses a draft tube mesh; cut and bake keep the revolved solid."
                    : "Bamboo preview uses a coarse tube mesh; cut and bake keep the revolved solid.");
                stockMesh = stock == null ? null : mesh.DuplicateMesh();
                mesh.Faces.ConvertQuadsToTriangles();
                mesh.Faces.CullDegenerateFaces();
                mesh.Compact();
                if (mesh.Faces.Count == 0) throw new ArgumentException("Geometry has no usable mesh faces.");
                if (mesh.Faces.Count > 200000) throw new ArgumentException("Preview is limited to 200,000 triangles per object. Simplify the mesh.");
                var alignment = new AlignmentResult(tubeFrame, new Box(tubeFrame, geometry.GetBoundingBox(tubeFrame)));
                if (stock != null) alignment = new AlignmentResult(alignment.Frame, new Box(alignment.Frame, geometry.GetBoundingBox(alignment.Frame)));
                ICoordinateMap mapper = new StraightMap(alignment.Frame);
                Point3d[] coords = mesh.Vertices.ToPoint3dArray().Select(mapper.Map).ToArray();
                var bounds = new BoundingBox(coords);
                double l = lengthMm, w = (bounds.Max.Y - bounds.Min.Y) * unitToMm, h = (bounds.Max.Z - bounds.Min.Z) * unitToMm;
                if (l < 0.01 || w < 0.01 || !double.IsFinite(l + w + h)) throw new ArgumentException("Mapped geometry is degenerate. Change the base plane or guide direction.");
                for (int i = 0; i < coords.Length; i++)
                    coords[i] = new Point3d((coords[i].X - bounds.Min.X) * unitToMm, (coords[i].Y - bounds.Min.Y) * unitToMm, (coords[i].Z - bounds.Min.Z) * unitToMm);
                mesh.Normals.ComputeNormals();
                return new PreparedPart(mapper, bounds.Min, false) { Mesh = mesh, Coordinates = coords, Alignment = alignment, LengthMm = l, WidthMm = w,
                    DepthMm = h, Mapping = mapper.Description + (stock == null ? "" : " · fixed stock"), UnitToMm = unitToMm, ToleranceMm = tolerance * unitToMm, Remarks = remarks };
            }

            mesh = MakeMesh(geometry, gridAngle, maxEdge, refineWhenNoEdge);
            stockMesh = stock == null ? null : MakeMesh(stock, gridAngle, maxEdge, refineWhenNoEdge);
            if (previewLod == PreviewLod.Draft)
                remarks.Add("Draft preview uses a coarser mesh; bake keeps the source geometry.");
            mesh.Faces.ConvertQuadsToTriangles();
            mesh.Faces.CullDegenerateFaces();
            mesh.Compact();
            if (mesh.Faces.Count == 0) throw new ArgumentException("Geometry has no usable mesh faces.");
            if (mesh.Faces.Count > 200000) throw new ArgumentException("Preview is limited to 200,000 triangles per object. Simplify the mesh.");
            var reference = stock ?? geometry;
            var alignmentBrep = TimberAlignment.Resolve(reference, stockMesh ?? mesh, basePlane, fibre, tolerance);
            if (stock != null) alignmentBrep = new AlignmentResult(alignmentBrep.Frame, new Box(alignmentBrep.Frame, geometry.GetBoundingBox(alignmentBrep.Frame)));
            ICoordinateMap mapperBrep = new StraightMap(alignmentBrep.Frame);
            Surface singleSurface = reference is Extrusion ? null : reference as Surface;
            if (reference is Brep b && b.Faces.Count == 1) singleSurface = b.Faces[0];
            if (guide != null) mapperBrep = new GuideMap(guide, alignmentBrep.Frame, tolerance);
            else if (singleSurface != null && !singleSurface.IsPlanar(tolerance)) mapperBrep = new SurfaceMap(singleSurface, alignmentBrep.Frame, !fibre.HasValue && !basePlane.HasValue);
            else if (singleSurface == null && (reference is Mesh || (reference is Brep multi && multi.Faces.Any(f => !f.IsPlanar(tolerance)))))
                remarks.Add("Using straight timber mapping. Supply a Guide Curve for bent grain on meshes and multi-face Breps.");

            Point3d[] coordsBrep = mesh.Vertices.ToPoint3dArray().Select(mapperBrep.Map).ToArray();
            var boundsBrep = new BoundingBox(stockMesh == null ? coordsBrep : stockMesh.Vertices.ToPoint3dArray().Select(mapperBrep.Map));
            if (stock != null) {
                // Meshed stock can sit inside the true solid; cut faces may land outside that mesh AABB.
                var domain = boundsBrep;
                domain.Inflate(Math.Max(tolerance, maxEdge > 0 ? maxEdge : tolerance));
                if (coordsBrep.Any(c => !domain.Contains(c))) throw new ArgumentException("Geometry extends outside the mapped Stock Geometry. Supply the original uncut stock and matching guide/plane.");
            }
            double lb = (boundsBrep.Max.X - boundsBrep.Min.X) * unitToMm, wb = (boundsBrep.Max.Y - boundsBrep.Min.Y) * unitToMm;
            double hb = (boundsBrep.Max.Z - boundsBrep.Min.Z) * unitToMm;
            bool virtualDepth = mapperBrep is SurfaceMap || hb <= tolerance * unitToMm;
            if (virtualDepth) { hb = settings.VirtualDepthMm; remarks.Add($"Appearance only: using virtual timber depth {hb:0.###} mm."); }
            if (lb < 0.01 || wb < 0.01 || !double.IsFinite(lb + wb + hb)) throw new ArgumentException("Mapped geometry is degenerate. Change the base plane or guide direction.");
            for (int i = 0; i < coordsBrep.Length; i++)
                coordsBrep[i] = new Point3d((coordsBrep[i].X - boundsBrep.Min.X) * unitToMm, (coordsBrep[i].Y - boundsBrep.Min.Y) * unitToMm,
                    virtualDepth ? hb : (coordsBrep[i].Z - boundsBrep.Min.Z) * unitToMm);
            mesh.Normals.ComputeNormals();
            return new PreparedPart(mapperBrep,boundsBrep.Min,virtualDepth) { Mesh = mesh, Coordinates = coordsBrep, Alignment = alignmentBrep, LengthMm = lb, WidthMm = wb,
                DepthMm = hb, Mapping = mapperBrep.Description + (stock == null ? "" : " · fixed stock"), UnitToMm = unitToMm, ToleranceMm = tolerance * unitToMm, Remarks = remarks };
        }
        catch { mesh?.Dispose(); throw; }
        finally { stockMesh?.Dispose(); }
    }

    static bool SameExtent(GeometryBase a, GeometryBase b, double tolerance)
    {
        var ba = a.GetBoundingBox(true); var bb = b.GetBoundingBox(true);
        if (!ba.IsValid || !bb.IsValid) return false;
        double scale = Math.Max(ba.Diagonal.Length, bb.Diagonal.Length);
        double tol = Math.Max(tolerance, 1e-6 * scale);
        // Per-axis extents catch mid-cuts; diagonal+center alone can match a notched solid.
        if (Math.Abs(ba.Min.X - bb.Min.X) > tol || Math.Abs(ba.Max.X - bb.Max.X) > tol
            || Math.Abs(ba.Min.Y - bb.Min.Y) > tol || Math.Abs(ba.Max.Y - bb.Max.Y) > tol
            || Math.Abs(ba.Min.Z - bb.Min.Z) > tol || Math.Abs(ba.Max.Z - bb.Max.Z) > tol)
            return false;
        if (a is Brep ab && b is Brep bbB && ab.IsSolid && bbB.IsSolid)
        {
            double va = ab.GetVolume(), vb = bbB.GetVolume();
            if (!(va > 0) || !(vb > 0)) return false;
            // shortcut: relative volume guard for holes/notches that keep the outer box; tighten if thin shells need it.
            if (Math.Abs(va - vb) > 0.02 * Math.Max(va, vb)) return false;
        }
        return true;
    }

    static bool TryBambooPreviewMesh(GeometryBase geometry, TimberSettings settings, double unitToMm, double tolerance,
        Plane? basePlane, Vector3d? fibre, PreviewLod previewLod, out Mesh mesh, out Plane frame, out double lengthMm)
    {
        mesh = null; frame = Plane.Unset; lengthMm = 0;
        try
        {
            if (basePlane.HasValue) frame = basePlane.Value;
            else if (fibre.HasValue && fibre.Value.IsValid && fibre.Value.Length > tolerance)
            {
                var dir = fibre.Value; dir.Unitize();
                var box = geometry.GetBoundingBox(true);
                var axis = new Line(box.Center - .5 * box.Diagonal.Length * dir, box.Center + .5 * box.Diagonal.Length * dir);
                if (!(axis.Length > tolerance)) return false;
                frame = NaturalGeometry.Frame(axis);
            }
            else
            {
                // Geometry-only: longest world AABB edge as culm axis (not the short world-X box edge).
                var box = geometry.GetBoundingBox(true);
                if (!box.IsValid) return false;
                double sx = box.Max.X - box.Min.X, sy = box.Max.Y - box.Min.Y, sz = box.Max.Z - box.Min.Z;
                Vector3d dir = sx >= sy && sx >= sz ? Vector3d.XAxis : sy >= sz ? Vector3d.YAxis : Vector3d.ZAxis;
                double half = .5 * box.Diagonal.Length;
                var axis = new Line(box.Center - half * dir, box.Center + half * dir);
                if (!(axis.Length > tolerance)) return false;
                frame = NaturalGeometry.Frame(axis);
            }
            var aligned = geometry.GetBoundingBox(frame);
            lengthMm = (aligned.Max.X - aligned.Min.X) * unitToMm;
            double spanY = (aligned.Max.Y - aligned.Min.Y) * unitToMm, spanZ = (aligned.Max.Z - aligned.Min.Z) * unitToMm;
            // Frame X must be the long culm axis, not a short cross-section.
            if (!(lengthMm > 1) || lengthMm < Math.Max(spanY, spanZ)) return false;
            // Frame origin at the stock base (aligned box minimum on X).
            frame = new Plane(frame.PointAt(aligned.Min.X, 0, 0), frame.XAxis, frame.YAxis);
            var features = NaturalFeatures.Resolve(settings, lengthMm, settings.Diameter, settings.Diameter);
            mesh = NaturalPreviewMesh.Create(features, frame, unitToMm, previewLod);
            return mesh != null && mesh.Faces.Count > 0;
        }
        catch (ArgumentException) { mesh?.Dispose(); mesh = null; return false; }
    }

    static Mesh MakeMesh(GeometryBase geometry, double gridAngle, double maxEdgeDocument = 0, bool refineWhenNoEdge = true)
    {
        if (geometry is Mesh mesh) return mesh.DuplicateMesh();
        Brep brep;
        bool owns;
        switch (geometry)
        {
            case Brep b: brep = b; owns = false; break;
            case Extrusion e: brep = e.ToBrep(); owns = true; break;
            case Surface s: brep = s.ToBrep(); owns = true; break;
            default: throw new ArgumentException("Supported inputs are Mesh, Brep, Surface, and Extrusion.");
        }
        try
        {
            var parameters = new MeshingParameters(MeshingParameters.Default)
            { RefineGrid = maxEdgeDocument <= 0 && refineWhenNoEdge, GridAngle = gridAngle, MinimumEdgeLength = 0 };
            if (maxEdgeDocument > 0) parameters.MaximumEdgeLength = maxEdgeDocument;
            Mesh[] pieces = Mesh.CreateFromBrep(brep, parameters);
            if (pieces == null || pieces.Length == 0) throw new ArgumentException("Rhino could not mesh the geometry.");
            var result = new Mesh();
            foreach (var piece in pieces) { result.Append(piece); piece.Dispose(); }
            return result;
        }
        finally { if (owns) brep?.Dispose(); }
    }

    public void Dispose() => Mesh?.Dispose();
}
