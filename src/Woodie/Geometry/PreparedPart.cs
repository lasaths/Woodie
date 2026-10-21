using Rhino.Geometry;
using Woodie.Appearance;

namespace Woodie.Geometry;

public sealed class PreparedPart : IDisposable
{
    public Mesh Mesh { get; private init; }
    public Point3d[] Coordinates { get; private init; }
    public AlignmentResult Alignment { get; private init; }
    public double LengthMm { get; private init; }
    public double WidthMm { get; private init; }
    public double DepthMm { get; private init; }
    public string Mapping { get; private init; }
    public double UnitToMm { get; private init; }
    public IReadOnlyList<string> Remarks { get; private init; }

    public static PreparedPart Create(GeometryBase geometry, TimberSettings settings, double unitToMm,
        double tolerance, Plane? basePlane = null, Vector3d? fibre = null, Curve guide = null)
    {
        settings.Validate();
        if (geometry == null || !geometry.IsValid) throw new ArgumentException("Geometry is null or invalid.");
        if (!double.IsFinite(unitToMm) || unitToMm <= 0) throw new ArgumentException("Document units must have a valid physical scale.");
        var remarks = new List<string>();
        Mesh mesh = MakeMesh(geometry);
        try
        {
            mesh.Faces.ConvertQuadsToTriangles();
            mesh.Faces.CullDegenerateFaces();
            mesh.Compact();
            if (mesh.Faces.Count == 0) throw new ArgumentException("Geometry has no usable mesh faces.");
            if (mesh.Faces.Count > 200000) throw new ArgumentException("Preview is limited to 200,000 triangles per object. Simplify the mesh.");
            var alignment = TimberAlignment.Resolve(geometry, mesh, basePlane, fibre, tolerance);
            ICoordinateMap mapper = new StraightMap(alignment.Frame);
            Surface singleSurface = geometry is Extrusion ? null : geometry as Surface;
            if (geometry is Brep b && b.Faces.Count == 1) singleSurface = b.Faces[0];
            if (guide != null) mapper = new GuideMap(guide, alignment.Frame, tolerance);
            else if (singleSurface != null && !singleSurface.IsPlanar(tolerance)) mapper = new SurfaceMap(singleSurface, alignment.Frame, !fibre.HasValue && !basePlane.HasValue);
            else if (singleSurface == null && (geometry is Mesh || (geometry is Brep multi && multi.Faces.Any(f => !f.IsPlanar(tolerance)))))
                remarks.Add("Using straight timber mapping. Supply a Guide Curve for bent grain on meshes and multi-face Breps.");

            Point3d[] coords = mesh.Vertices.ToPoint3dArray().Select(mapper.Map).ToArray();
            var bounds = new BoundingBox(coords);
            double l = (bounds.Max.X - bounds.Min.X) * unitToMm, w = (bounds.Max.Y - bounds.Min.Y) * unitToMm;
            double h = (bounds.Max.Z - bounds.Min.Z) * unitToMm;
            bool virtualDepth = mapper is SurfaceMap || h <= tolerance * unitToMm;
            if (virtualDepth) { h = settings.VirtualDepthMm; remarks.Add($"Appearance only: using virtual timber depth {h:0.###} mm."); }
            if (l < 0.01 || w < 0.01 || !double.IsFinite(l + w + h)) throw new ArgumentException("Mapped geometry is degenerate. Change the base plane or guide direction.");
            for (int i = 0; i < coords.Length; i++)
                coords[i] = new Point3d((coords[i].X - bounds.Min.X) * unitToMm, (coords[i].Y - bounds.Min.Y) * unitToMm,
                    virtualDepth ? h : (coords[i].Z - bounds.Min.Z) * unitToMm);
            mesh.Normals.ComputeNormals();
            return new PreparedPart { Mesh = mesh, Coordinates = coords, Alignment = alignment, LengthMm = l, WidthMm = w,
                DepthMm = h, Mapping = mapper.Description, UnitToMm = unitToMm, Remarks = remarks };
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
