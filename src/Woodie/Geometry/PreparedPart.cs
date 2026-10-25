using Rhino.Geometry;
using Woodie.Appearance;

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
    public IReadOnlyList<TimberLayer> Layers { get; private init; }

    public static PreparedPart Create(GeometryBase geometry, TimberSettings settings, double unitToMm,
        double tolerance, Plane? basePlane = null, Vector3d? fibre = null, Curve guide = null, GeometryBase stock = null)
    {
        settings.Validate();
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Geometry is null or invalid.");
        if (stock != null && !stock.IsValid) throw new ArgumentException("Stock Geometry is invalid.");
        if (!double.IsFinite(unitToMm) || unitToMm <= 0) throw new ArgumentException("Document units must have a valid physical scale.");
        var remarks = new List<string>();
        Mesh mesh = MakeMesh(geometry);
        try
        {
            using var stockMesh = stock == null ? null : MakeMesh(stock);
            mesh.Faces.ConvertQuadsToTriangles();
            mesh.Faces.CullDegenerateFaces();
            mesh.Compact();
            if (mesh.Faces.Count == 0) throw new ArgumentException("Geometry has no usable mesh faces.");
            if (mesh.Faces.Count > 200000) throw new ArgumentException("Preview is limited to 200,000 triangles per object. Simplify the mesh.");
            var reference = stock ?? geometry;
            var alignment = TimberAlignment.Resolve(reference, stockMesh ?? mesh, basePlane, fibre, tolerance);
            if (stock != null) alignment = new AlignmentResult(alignment.Frame, new Box(alignment.Frame, geometry.GetBoundingBox(alignment.Frame)));
            ICoordinateMap mapper = new StraightMap(alignment.Frame);
            Surface singleSurface = reference is Extrusion ? null : reference as Surface;
            if (reference is Brep b && b.Faces.Count == 1) singleSurface = b.Faces[0];
            if (guide != null) mapper = new GuideMap(guide, alignment.Frame, tolerance);
            else if (singleSurface != null && !singleSurface.IsPlanar(tolerance)) mapper = new SurfaceMap(singleSurface, alignment.Frame, !fibre.HasValue && !basePlane.HasValue);
            else if (singleSurface == null && (reference is Mesh || (reference is Brep multi && multi.Faces.Any(f => !f.IsPlanar(tolerance)))))
                remarks.Add("Using straight timber mapping. Supply a Guide Curve for bent grain on meshes and multi-face Breps.");

            Point3d[] coords = mesh.Vertices.ToPoint3dArray().Select(mapper.Map).ToArray();
            var bounds = new BoundingBox(stockMesh == null ? coords : stockMesh.Vertices.ToPoint3dArray().Select(mapper.Map));
            if (stock != null) {
                var domain = bounds; domain.Inflate(tolerance);
                if (coords.Any(c => !domain.Contains(c))) throw new ArgumentException("Geometry extends outside the mapped Stock Geometry. Supply the original uncut stock and matching guide/plane.");
            }
            double l = (bounds.Max.X - bounds.Min.X) * unitToMm, w = (bounds.Max.Y - bounds.Min.Y) * unitToMm;
            double h = (bounds.Max.Z - bounds.Min.Z) * unitToMm;
            bool virtualDepth = mapper is SurfaceMap || h <= tolerance * unitToMm;
            if (virtualDepth) { h = settings.VirtualDepthMm; remarks.Add($"Appearance only: using virtual timber depth {h:0.###} mm."); }
            if (l < 0.01 || w < 0.01 || !double.IsFinite(l + w + h)) throw new ArgumentException("Mapped geometry is degenerate. Change the base plane or guide direction.");
            var layers = TimberLayups.Resolve(settings, h, w, tolerance * unitToMm);
            for (int i = 0; i < coords.Length; i++)
                coords[i] = new Point3d((coords[i].X - bounds.Min.X) * unitToMm, (coords[i].Y - bounds.Min.Y) * unitToMm,
                    virtualDepth ? h : (coords[i].Z - bounds.Min.Z) * unitToMm);
            mesh.Normals.ComputeNormals();
            return new PreparedPart(mapper,bounds.Min,virtualDepth) { Mesh = mesh, Coordinates = coords, Alignment = alignment, LengthMm = l, WidthMm = w,
                DepthMm = h, Mapping = mapper.Description + (stock == null ? "" : " · fixed stock"), UnitToMm = unitToMm, ToleranceMm = tolerance * unitToMm, Remarks = remarks, Layers = layers };
        }
        catch { mesh.Dispose(); throw; }
    }

    static Mesh MakeMesh(GeometryBase geometry)
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
            Mesh[] pieces = Mesh.CreateFromBrep(brep, new MeshingParameters(MeshingParameters.Default)
            { RefineGrid = true, GridAngle = Math.PI / 180, MinimumEdgeLength = 0 });
            if (pieces == null || pieces.Length == 0) throw new ArgumentException("Rhino could not mesh the geometry.");
            var result = new Mesh();
            foreach (var piece in pieces) { result.Append(piece); piece.Dispose(); }
            return result;
        }
        finally { if (owns) brep?.Dispose(); }
    }

    public void Dispose() => Mesh?.Dispose();
}
