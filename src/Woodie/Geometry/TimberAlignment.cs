using Rhino.Geometry;

namespace Woodie.Geometry;

public sealed record AlignmentResult(Plane Frame, Box Box);

public static class TimberAlignment
{
    public static AlignmentResult Resolve(GeometryBase geometry, Mesh mesh, Plane? basePlane, Vector3d? fibre, double tolerance)
    {
        var principal = PrincipalFrame(mesh);
        Plane plane = basePlane ?? LargestPlanarRegion(geometry, mesh, tolerance) ?? principal;
        if (!plane.IsValid) throw new ArgumentException("Base plane is invalid.");
        Vector3d x = fibre ?? (basePlane.HasValue ? plane.XAxis : LongestBoundary(geometry, mesh, plane, tolerance) ?? principal.XAxis);
        if (!x.IsValid || x.IsTiny()) throw new ArgumentException("Fibre vector must be valid and nonzero.");
        double originalLength = x.Length;
        x -= (x * plane.ZAxis) * plane.ZAxis;
        if (x.Length <= originalLength * 1e-8 || !x.Unitize())
        {
            if (fibre.HasValue) throw new ArgumentException("Fibre is perpendicular to the base plane. Supply an in-plane fibre vector or change Base Plane.");
            x = plane.XAxis;
        }
        // Direct construction preserves an explicitly reversed fibre (the reference's signed-angle method did not).
        var frame = new Plane(plane.Origin, x, Vector3d.CrossProduct(plane.ZAxis, x));
        BoundingBox bounds = geometry.GetBoundingBox(frame);
        if (!bounds.IsValid) throw new ArgumentException("Cannot calculate geometry bounds.");
        if (bounds.Max.X - bounds.Min.X <= tolerance || bounds.Max.Y - bounds.Min.Y <= tolerance)
            throw new ArgumentException("Geometry needs two nonzero dimensions to preview timber.");
        var box = new Box(frame, bounds);
        frame.Origin = frame.PointAt(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
        return new AlignmentResult(frame, box);
    }

    internal static Vector3d Canonical(Vector3d v)
    {
        v.Unitize();
        double[] a = { Math.Abs(v.X), Math.Abs(v.Y), Math.Abs(v.Z) };
        int i = Array.IndexOf(a, a.Max());
        if ((i == 0 ? v.X : i == 1 ? v.Y : v.Z) < 0) v = -v;
        return v;
    }

    static Plane? LargestPlanarRegion(GeometryBase geometry, Mesh mesh, double tolerance)
    {
        Plane? result = null;
        double best = 0;
        Brep brep = geometry as Brep;
        if (geometry is Surface surface) brep = surface.ToBrep();
        if (geometry is Extrusion extrusion) brep = extrusion.ToBrep();
        if (brep != null)
        {
            foreach (var face in brep.Faces)
                if (face.TryGetPlane(out Plane p, tolerance))
                {
                    using var area = AreaMassProperties.Compute(face);
                    if (area != null && area.Area > best) { best = area.Area; result = StablePlane(p.Origin, p.ZAxis); }
                }
            return result;
        }

        // Accumulate coplanar triangles rather than letting triangulation determine the base plane.
        var regions = new Dictionary<string, (double Area, Plane Plane)>();
        foreach (var face in mesh.Faces)
        {
            Point3d a = mesh.Vertices.Point3dAt(face.A), b = mesh.Vertices.Point3dAt(face.B), c = mesh.Vertices.Point3dAt(face.C);
            Vector3d cross = Vector3d.CrossProduct(b - a, c - a);
            double area = cross.Length / 2;
            if (area <= tolerance * tolerance) continue;
            Vector3d n = Canonical(cross);
            double d = n * (a - Point3d.Origin);
            string key = FormattableString.Invariant($"{Math.Round(n.X, 6)}|{Math.Round(n.Y, 6)}|{Math.Round(n.Z, 6)}|{Math.Round(d / Math.Max(tolerance, 1e-9))}");
            if (regions.TryGetValue(key, out var region)) regions[key] = (region.Area + area, region.Plane);
            else regions[key] = (area, StablePlane(a, n));
        }
        // A triangle on a curved mesh is not a meaningful planar region.
        double total = regions.Values.Sum(r => r.Area);
        foreach (var region in regions.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Value))
            if (region.Area > best && region.Area >= total * 0.1) { best = region.Area; result = region.Plane; }
        return result;
    }

    static Plane StablePlane(Point3d origin, Vector3d normal)
    {
        Vector3d z = Canonical(normal);
        Vector3d x = new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis }.OrderBy(a => Math.Abs(a * z)).First();
        x -= (x * z) * z;
        x.Unitize();
        return new Plane(origin, x, Vector3d.CrossProduct(z, x));
    }

    static Vector3d? LongestBoundary(GeometryBase geometry, Mesh mesh, Plane plane, double tolerance)
    {
        var lines = new List<Line>();
        Brep brep = geometry as Brep;
        if (geometry is Surface surface) brep = surface.ToBrep();
        if (geometry is Extrusion extrusion) brep = extrusion.ToBrep();
        if (brep != null)
        {
            foreach (var edge in brep.Edges)
                if (edge.IsLinear(tolerance)) lines.Add(new Line(edge.PointAtStart, edge.PointAtEnd));
        }
        else
        {
            mesh.FaceNormals.ComputeFaceNormals();
            for (int i = 0; i < mesh.TopologyEdges.Count; i++)
            {
                int[] faces = mesh.TopologyEdges.GetConnectedFaces(i);
                if (faces.Length != 2 || Math.Abs(mesh.FaceNormals[faces[0]] * mesh.FaceNormals[faces[1]]) < 0.9999)
                    lines.Add(mesh.TopologyEdges.EdgeLine(i));
            }
        }
        var candidates = lines.Select(l => l.Direction - (l.Direction * plane.ZAxis) * plane.ZAxis)
            .Where(v => v.Length > tolerance).Select(v => (Length: v.Length, Axis: Canonical(v))).ToList();
        if (candidates.Count == 0) return null;
        double longest = candidates.Max(c => c.Length);
        return candidates.Where(c => c.Length >= longest - tolerance).OrderByDescending(c => c.Axis.X)
            .ThenByDescending(c => c.Axis.Y).ThenByDescending(c => c.Axis.Z).First().Axis;
    }

    internal static Plane PrincipalFrame(Mesh mesh)
    {
        var triangles = mesh.Faces.Select(f => new[] { mesh.Vertices.Point3dAt(f.A), mesh.Vertices.Point3dAt(f.B), mesh.Vertices.Point3dAt(f.C) }).ToArray();
        double weight = 0; Vector3d mean = Vector3d.Zero;
        foreach (var t in triangles)
        {
            double a = Vector3d.CrossProduct(t[1] - t[0], t[2] - t[0]).Length / 2;
            mean += a * ((Vector3d)t[0] + (Vector3d)t[1] + (Vector3d)t[2]) / 3;
            weight += a;
        }
        if (weight <= 0) throw new ArgumentException("Geometry has no non-degenerate faces.");
        mean /= weight;
        double[,] cov = new double[3, 3];
        foreach (var t in triangles)
        {
            double a = Vector3d.CrossProduct(t[1] - t[0], t[2] - t[0]).Length / 2;
            var v = t.Select(p => p - (Point3d)mean).ToArray();
            var sum = v[0] + v[1] + v[2];
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
                cov[r, c] += a / 12 * (sum[r] * sum[c] + v.Sum(p => p[r] * p[c]));
        }
        double[,] axes = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int step = 0; step < 40; step++)
        {
            int p = 0, q = 1;
            for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++)
                if (Math.Abs(cov[i, j]) > Math.Abs(cov[p, q])) { p = i; q = j; }
            if (Math.Abs(cov[p, q]) <= 1e-12 * Math.Max(1, cov[0, 0] + cov[1, 1] + cov[2, 2])) break;
            double theta = 0.5 * Math.Atan2(2 * cov[p, q], cov[q, q] - cov[p, p]);
            double cos = Math.Cos(theta), sin = Math.Sin(theta);
            for (int k = 0; k < 3; k++) { double a = cov[k, p], b = cov[k, q]; cov[k, p] = cos * a - sin * b; cov[k, q] = sin * a + cos * b; }
            for (int k = 0; k < 3; k++) { double a = cov[p, k], b = cov[q, k]; cov[p, k] = cos * a - sin * b; cov[q, k] = sin * a + cos * b; }
            for (int k = 0; k < 3; k++) { double a = axes[k, p], b = axes[k, q]; axes[k, p] = cos * a - sin * b; axes[k, q] = sin * a + cos * b; }
        }
        int[] order = Enumerable.Range(0, 3).OrderBy(i => cov[i, i]).ThenBy(i => i).ToArray();
        Vector3d z = Canonical(new Vector3d(axes[0, order[0]], axes[1, order[0]], axes[2, order[0]]));
        Vector3d x = Canonical(new Vector3d(axes[0, order[2]], axes[1, order[2]], axes[2, order[2]]));
        return new Plane((Point3d)mean, x, Vector3d.CrossProduct(z, x));
    }
}
