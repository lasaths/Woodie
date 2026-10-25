using Rhino.Geometry;
using Woodie.Stock;

namespace Woodie.Geometry;

/// <summary>Native Brep for natural Log/Bamboo stock: one revolved face per profile run, joined into a solid.</summary>
internal static class NaturalGeometry
{
    /// <summary>Stock frame: X along the axis (base → tip), Y/Z across it. Material coordinates map through this plane.</summary>
    public static Plane Frame(Line axis)
    {
        var x = axis.Direction; x.Unitize();
        var y = Math.Abs(x.Z) > 0.999 ? Vector3d.XAxis : Vector3d.CrossProduct(Vector3d.ZAxis, x);
        return new Plane(axis.From, x, y);
    }

    /// <summary>Join faces into closed solids. Join orients every shell outward, so a shell strictly inside a larger one is a void: flip it and merge it into its host.</summary>
    internal static List<Brep> JoinSolids(IEnumerable<Brep> parts, double tolerance)
    {
        var groups = (Brep.JoinBreps(parts, tolerance) ?? Array.Empty<Brep>()).OrderByDescending(g => g.IsSolid ? Math.Abs(g.GetVolume()) : 0).ToList();
        var hosts = new List<(Brep Host, List<Brep> Voids)>(); var open = new List<Brep>();
        foreach (var group in groups) {
            if (!group.IsSolid) { open.Add(group); continue; }
            var host = hosts.FirstOrDefault(h => h.Host.IsPointInside(group.Vertices[0].Location, tolerance, true));
            if (host.Host == null) { hosts.Add((group, new List<Brep>())); continue; }
            if (group.SolidOrientation == BrepSolidOrientation.Outward) group.Flip();
            host.Voids.Add(group);
        }
        var result = new List<Brep>(open);
        foreach (var (host, voids) in hosts) {
            result.Add(voids.Count == 0 ? host : Brep.MergeBreps(voids.Prepend(host), tolerance));
            if (voids.Count > 0) { host.Dispose(); foreach (var v in voids) v.Dispose(); }
        }
        return result;
    }

    /// <param name="mmToDocument">Document units per millimetre.</param>
    public static Brep Create(NaturalFeatures features, Line axis, double mmToDocument, double tolerance)
    {
        if (!axis.IsValid || !(axis.Length > tolerance)) throw new ArgumentException("Supply a Line with nonzero length.");
        var faces = new List<Brep>();
        try
        {
            var local = new Line(Point3d.Origin, new Point3d(1, 0, 0));
            var place = Transform.PlaneToPlane(Plane.WorldXY, Frame(axis)) * Transform.Scale(Plane.WorldXY, 1, 1 + features.Ovality, 1 - features.Ovality);
            foreach (var run in features.Profile())
            {
                using var curve = new PolylineCurve(run.Select(p => new Point3d(p.X * mmToDocument, p.R * mmToDocument, 0)));
                using var revolved = RevSurface.Create(curve, local, 0, 2 * Math.PI);
                using var nurbs = revolved?.ToNurbsSurface();   // non-uniform scaling needs NURBS; a RevSurface loses its trims
                var face = nurbs == null ? null : Brep.CreateFromSurface(nurbs);
                if (face != null && !face.Transform(place)) { face.Dispose(); face = null; }
                if (face == null) throw new ArgumentException("Rhino could not revolve the natural stock profile.");
                faces.Add(face);
            }
            var solids = JoinSolids(faces, tolerance);
            var brep = solids.Count == 1 ? solids[0] : null;
            if (brep == null) foreach (var solid in solids) solid.Dispose();
            if (brep == null || !brep.IsValid) throw new ArgumentException("Natural stock did not form a valid solid. Check dimensions and document tolerance.");
            if (brep.SolidOrientation == BrepSolidOrientation.Inward) brep.Flip();
            return brep;
        }
        finally { foreach (var face in faces) face.Dispose(); }
    }
}
