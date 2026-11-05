using Rhino.Geometry;
using Woodie.Stock;

namespace Woodie.Geometry;

/// <summary>
/// Coarse hollow-tube mesh for bamboo preview. Bake and cut keep the revolved Brep from <see cref="NaturalGeometry"/>.
/// </summary>
internal static class NaturalPreviewMesh
{
    // Full: 72×20 mm (~phase 2). Draft: 12×80 mm (HOOPS-style coarse facet count + longer axial step).
    public const int FullSides = 72;
    public const double FullAxialStepMm = 20;
    public const int DraftSides = 12;
    public const double DraftAxialStepMm = 80;

    public static (int Sides, double AxialStepMm) Tessellation(PreviewLod lod) =>
        lod == PreviewLod.Draft ? (DraftSides, DraftAxialStepMm) : (FullSides, FullAxialStepMm);

    public static Mesh Create(NaturalFeatures features, Plane frame, double unitToMm, PreviewLod lod = PreviewLod.Full)
    {
        var (sides, axialStepMm) = Tessellation(lod);
        return Create(features, frame, unitToMm, sides, axialStepMm);
    }

    public static Mesh Create(NaturalFeatures features, Plane frame, double unitToMm, int sides, double axialStepMm)
    {
        if (!(unitToMm > 0) || !double.IsFinite(unitToMm)) throw new ArgumentException("Document units must have a valid physical scale.");
        if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides));
        if (!(axialStepMm > 0) || !double.IsFinite(axialStepMm)) throw new ArgumentOutOfRangeException(nameof(axialStepMm));
        double toDoc = 1 / unitToMm;
        double oy = 1 + features.Ovality, oz = 1 - features.Ovality;
        var xs = Stations(features, axialStepMm);
        var mesh = new Mesh();
        var outer = new int[xs.Count][];
        var inner = new int[xs.Count][];
        for (int i = 0; i < xs.Count; i++)
        {
            double x = xs[i], ro = features.Outer(x) + features.Ridge(x), ri = features.Inner(x);
            outer[i] = Ring(mesh, frame, x, ro, oy, oz, toDoc, sides, features);
            inner[i] = Ring(mesh, frame, x, Math.Max(ri, 0), oy, oz, toDoc, sides);
        }
        for (int i = 0; i < xs.Count - 1; i++)
        {
            Strip(mesh, outer[i], outer[i + 1], outward: true, sides);
            if (features.Inner(xs[i]) > 0 || features.Inner(xs[i + 1]) > 0)
                Strip(mesh, inner[i], inner[i + 1], outward: false, sides);
        }
        CapAnnulus(mesh, outer[0], inner[0], outward: false, sides);
        CapAnnulus(mesh, outer[^1], inner[^1], outward: true, sides);
        double h = features.Diaphragm / 2;
        foreach (double n in features.Nodes)
        {
            // Base face looks −X; tip face looks +X so both point out of the diaphragm slab.
            Disk(mesh, frame, Math.Clamp(n - h, 0, features.Length), features.Inner(n - h), oy, oz, toDoc, sides, towardTip: false);
            Disk(mesh, frame, Math.Clamp(n + h, 0, features.Length), features.Inner(n + h), oy, oz, toDoc, sides, towardTip: true);
        }
        mesh.Normals.ComputeNormals();
        mesh.Compact();
        return mesh;
    }

    static List<double> Stations(NaturalFeatures f, double axialStepMm)
    {
        var xs = new SortedSet<double> { 0, f.Length };
        int n = Math.Max(1, (int)Math.Ceiling(f.Length / axialStepMm));
        for (int i = 1; i < n; i++) xs.Add(f.Length * i / (double)n);
        double h = f.Diaphragm / 2;
        foreach (double node in f.Nodes)
        {
            xs.Add(Math.Clamp(node - h, 0, f.Length));
            xs.Add(Math.Clamp(node + h, 0, f.Length));
        }
        return xs.ToList();
    }

    static int[] Ring(Mesh mesh, Plane frame, double xMm, double rMm, double oy, double oz, double toDoc, int sides, NaturalFeatures fringe = null)
    {
        var ids = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = 2 * Math.PI * i / sides;
            double r = rMm + (fringe != null ? fringe.FringeBump(xMm, a) : 0);
            var local = new Point3d(xMm * toDoc, r * Math.Cos(a) * oy * toDoc, r * Math.Sin(a) * oz * toDoc);
            ids[i] = mesh.Vertices.Add(frame.PointAt(local.X, local.Y, local.Z));
        }
        return ids;
    }

    static void Strip(Mesh mesh, int[] a, int[] b, bool outward, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            if (outward) { mesh.Faces.AddFace(a[i], a[j], b[j]); mesh.Faces.AddFace(a[i], b[j], b[i]); }
            else { mesh.Faces.AddFace(a[i], b[j], a[j]); mesh.Faces.AddFace(a[i], b[i], b[j]); }
        }
    }

    static void CapAnnulus(Mesh mesh, int[] outer, int[] inner, bool outward, int sides)
    {
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            if (outward) { mesh.Faces.AddFace(outer[i], outer[j], inner[j]); mesh.Faces.AddFace(outer[i], inner[j], inner[i]); }
            else { mesh.Faces.AddFace(outer[i], inner[j], outer[j]); mesh.Faces.AddFace(outer[i], inner[i], inner[j]); }
        }
    }

    static void Disk(Mesh mesh, Plane frame, double xMm, double rMm, double oy, double oz, double toDoc, int sides, bool towardTip)
    {
        if (!(rMm > 0)) return;
        int centre = mesh.Vertices.Add(frame.PointAt(xMm * toDoc, 0, 0));
        var ring = Ring(mesh, frame, xMm, rMm, oy, oz, toDoc, sides);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            if (towardTip) mesh.Faces.AddFace(centre, ring[i], ring[j]);
            else mesh.Faces.AddFace(centre, ring[j], ring[i]);
        }
    }
}
