using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Woodie.Geometry;

public sealed class TimberCutResult : IDisposable
{
    public List<GeometryBase> Geometry { get; } = new();
    public List<Curve> Outlines { get; } = new();
    public string Remark { get; internal set; }
    public void Dispose() { foreach (var g in Geometry) g.Dispose(); foreach (var c in Outlines) c.Dispose(); }
}

public static class TimberCut
{
    public static TimberCutResult Create(GeometryBase stock, Plane plane, bool positive, double tolerance)
    {
        if (stock == null || !stock.IsValid) throw new ArgumentException("Supply valid closed Brep, solid Extrusion or closed Mesh stock.");
        if (!plane.IsValid) throw new ArgumentException("Cutting Plane is invalid.");
        if (!(tolerance > 0) || !double.IsFinite(tolerance)) throw new ArgumentException("Cut tolerance must be positive.");
        if (positive) plane.Flip();
        using var converted = stock is Extrusion extrusion ? extrusion.ToBrep() : null;
        var source = converted ?? stock;
        if (source is Brep brep && !brep.IsSolid || source is Mesh open && !open.IsClosed)
            throw new ArgumentException("Timber Cut requires closed solid stock. Close/cap the input first; open surfaces are supported by Timber Preview only.");
        if (source is not Brep && source is not Mesh) throw new ArgumentException("Timber Cut supports closed Breps, solid extrusions and closed meshes.");
        var result = new TimberCutResult();
        try {
            var bounds = source.GetBoundingBox(plane);
            if (bounds.Max.Z <= tolerance) { result.Geometry.Add(source.Duplicate()); result.Remark = "Cut is outside or on the boundary: whole stock retained."; return result; }
            if (bounds.Min.Z >= -tolerance) { result.Remark = "Cut is outside or on the boundary: retained side is empty."; return result; }
            if (source is Brep b) {
                Intersection.BrepPlane(b, plane, tolerance, out Curve[] curves, out _);
                if (curves != null) result.Outlines.AddRange(curves);
                var pieces = b.Trim(plane, tolerance);
                if (pieces == null || pieces.Length == 0) throw new ArgumentException("Rhino could not split the solid at this plane. Move the plane away from a tangent or tiny feature.");
                try {
                    foreach (var piece in pieces) {
                        var capped = piece.IsSolid ? piece.DuplicateBrep() : piece.CapPlanarHoles(tolerance);
                        if (capped == null || !capped.IsValid || !capped.IsSolid) { capped?.Dispose(); throw new ArgumentException("The cut could not be capped into a valid solid. Check stock validity and document tolerance."); }
                        if (capped.SolidOrientation == BrepSolidOrientation.Inward) capped.Flip();
                        result.Geometry.Add(capped);
                    }
                } finally { foreach (var piece in pieces) piece.Dispose(); }
            } else if (source is Mesh mesh) {
                var pieces = mesh.Split(plane);
                if (pieces == null || pieces.Length == 0) throw new ArgumentException("Rhino could not split the mesh at this plane.");
                try {
                    foreach (var piece in pieces) {
                        var d = piece.Vertices.ToPoint3dArray().Select(plane.DistanceTo).ToArray();
                        if (d.Min() >= -tolerance || d.Max() > tolerance) continue;
                        var retained = piece.DuplicateMesh();
                        try {
                            if (retained.IsClosed) { result.Geometry.Add(retained); retained = null; continue; }
                            var boundaries = retained.GetNakedEdges();
                            if (boundaries == null || boundaries.Any(p => !p.IsClosed)) throw new ArgumentException("Cut mesh has an open section boundary and cannot be capped.");
                            var curves = boundaries.Select(p => p.ToNurbsCurve()).ToArray();
                            try {
                                if (curves.Any(c => !c.IsPlanar(tolerance))) throw new ArgumentException("Cut mesh section is not planar within tolerance.");
                                var caps = Brep.CreatePlanarBreps(curves, tolerance);
                                if (caps == null || caps.Length == 0) throw new ArgumentException("Could not construct the mesh section cap.");
                                try { foreach (var cap in caps) foreach (var capMesh in Mesh.CreateFromBrep(cap, MeshingParameters.Default)) { retained.Append(capMesh); capMesh.Dispose(); } }
                                finally { foreach (var cap in caps) cap.Dispose(); }
                                retained.Vertices.CombineIdentical(true, true); retained.Weld(Math.PI); retained.UnifyNormals(); retained.Normals.ComputeNormals();
                                if (!retained.IsValid || !retained.IsClosed) throw new ArgumentException("The cut mesh cap did not produce a valid closed mesh. Check mesh topology and tolerance.");
                                if (retained.SolidOrientation() < 0) retained.Flip(true, true, true);
                                result.Geometry.Add(retained); retained = null;
                                result.Outlines.AddRange(curves.Select(c => c.DuplicateCurve()));
                            } finally { foreach (var curve in curves) curve.Dispose(); }
                        } finally { retained?.Dispose(); }
                    }
                } finally { foreach (var piece in pieces) piece.Dispose(); }
                if (result.Geometry.Count == 0) throw new ArgumentException("No valid retained mesh was produced. Check the cut plane and mesh topology.");
            }
            return result;
        } catch { result.Dispose(); throw; }
    }
}
