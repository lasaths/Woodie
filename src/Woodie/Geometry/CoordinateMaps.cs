using Rhino.Geometry;

namespace Woodie.Geometry;

internal interface ICoordinateMap
{
    Point3d Map(Point3d p);
    string Description { get; }
}

internal sealed class StraightMap(Plane frame) : ICoordinateMap
{
    public string Description => "Straight";
    public Point3d Map(Point3d p) { frame.RemapToPlaneSpace(p, out Point3d local); return local; }
}

internal sealed class GuideMap : ICoordinateMap
{
    readonly Point3d[] points;
    readonly Vector3d[] normals;
    readonly double[] lengths;
    readonly double tolerance;
    readonly Curve curve;
    public string Description => "Guide curve";

    public GuideMap(Curve guide, Plane initial, double tolerance)
    {
        this.tolerance = tolerance;
        curve = guide;
        if (!guide.IsValid || guide.IsClosed) throw new ArgumentException("Guide must be a valid open curve; closed guides are not supported in v1.");
        double length = guide.GetLength();
        if (!double.IsFinite(length) || length <= tolerance) throw new ArgumentException("Guide curve is too short.");
        var intersections = Rhino.Geometry.Intersect.Intersection.CurveSelf(guide, tolerance);
        if (intersections != null && intersections.Count > 0) throw new ArgumentException("Guide mapping is ambiguous: the guide intersects itself.");
        double next;
        if (guide.GetNextDiscontinuity(Continuity.C1_continuous, guide.Domain.Min, guide.Domain.Max, out next))
            throw new ArgumentException("Guide has a kink or cusp. Use a smooth guide curve.");
        // Uniform arc-length stations keep orientation independent of the guide's parameter domain.
        int count = 128;
        double[] parameters;
        while (true)
        {
            parameters = guide.DivideByCount(count, true);
            if (parameters == null) throw new ArgumentException("Cannot sample guide curve.");
            bool coarse = false;
            for (int i = 1; i < parameters.Length; i++)
                if (Vector3d.VectorAngle(guide.TangentAt(parameters[i - 1]), guide.TangentAt(parameters[i])) > Math.PI / 60) coarse = true;
            if (!coarse) break;
            if (count >= 2048) throw new ArgumentException("Guide bends too sharply. Simplify the guide or split the timber part.");
            count *= 2;
        }
        points = parameters.Select(guide.PointAt).ToArray();
        lengths = Enumerable.Range(0, points.Length).Select(i => length * i / (points.Length - 1)).ToArray();
        normals = new Vector3d[points.Length];
        Vector3d tangent = guide.TangentAt(parameters[0]); tangent.Unitize();
        Vector3d z = initial.ZAxis - (initial.ZAxis * tangent) * tangent;
        if (!z.Unitize()) { z = initial.YAxis - (initial.YAxis * tangent) * tangent; z.Unitize(); }
        normals[0] = z;
        for (int i = 1; i < points.Length; i++)
        {
            Vector3d t = guide.TangentAt(parameters[i]); t.Unitize();
            if (t * tangent < -0.95) throw new ArgumentException("Guide contains a cusp or an unresolved sharp bend.");
            var rotation = Transform.Rotation(tangent, t, Point3d.Origin);
            z.Transform(rotation);
            z -= (z * t) * t; z.Unitize();
            normals[i] = z;
            tangent = t;
        }
    }

    public Point3d Map(Point3d p)
    {
        int best = -1; double fraction = 0, distance = double.MaxValue;
        var distances = new double[points.Length - 1];
        for (int i = 0; i < distances.Length; i++)
        {
            Vector3d d = points[i + 1] - points[i];
            double f = Math.Clamp(((p - points[i]) * d) / d.SquareLength, 0, 1);
            double dist = p.DistanceToSquared(points[i] + f * d);
            distances[i] = dist;
            if (dist < distance) { best = i; fraction = f; distance = dist; }
        }
        // Distinct equally close stations make the inverse bending map ambiguous.
        for (int i = 0; i < distances.Length; i++)
            if (Math.Abs(i - best) > 3 && distances[i] <= distance + tolerance * tolerance &&
                (i == 0 || distances[i] <= distances[i - 1]) && (i == distances.Length - 1 || distances[i] <= distances[i + 1]))
                throw new ArgumentException("Guide mapping is ambiguous near this part. Use a non-overlapping guide or split the geometry.");
        if (!curve.ClosestPoint(p, out double parameter)) throw new ArgumentException("Cannot map geometry onto the guide curve.");
        double arc = parameter <= curve.Domain.Min ? 0 : curve.GetLength(new Interval(curve.Domain.Min, parameter));
        double station = arc / lengths[^1] * (points.Length - 1);
        best = Math.Min(points.Length - 2, (int)station); fraction = Math.Clamp(station - best, 0, 1);
        Vector3d x = curve.TangentAt(parameter); x.Unitize();
        Vector3d z = (1 - fraction) * normals[best] + fraction * normals[best + 1];
        z -= (z * x) * x; z.Unitize();
        Vector3d y = Vector3d.CrossProduct(z, x);
        Vector3d delta = p - curve.PointAt(parameter);
        return new Point3d(arc, delta * y, delta * z);
    }
}

internal sealed class SurfaceMap : ICoordinateMap
{
    readonly Surface surface;
    readonly bool alongU, reverseX, reverseY;
    public string Description => "Bent surface";

    public SurfaceMap(Surface surface, Plane reference, bool preferLength = false)
    {
        this.surface = surface;
        double u = surface.Domain(0).Mid, v = surface.Domain(1).Mid;
        if (!surface.FrameAt(u, v, out Plane frame)) throw new ArgumentException("Cannot evaluate curved surface frame.");
        // FrameAt's axes follow surface U and its perpendicular in the tangent plane.
        using Curve uCurve = surface.IsoCurve(0, v);
        using Curve vCurve = surface.IsoCurve(1, u);
        Vector3d tu = uCurve.TangentAt(u), tv = vCurve.TangentAt(v);
        alongU = preferLength ? uCurve.GetLength() >= vCurve.GetLength() : Math.Abs(tu * reference.XAxis) >= Math.Abs(tv * reference.XAxis);
        Vector3d tx = alongU ? tu : tv, ty = alongU ? tv : tu;
        reverseX = tx * reference.XAxis < 0;
        if (reverseX) tx = -tx;
        reverseY = Vector3d.CrossProduct(tx, ty) * reference.ZAxis < 0;
    }

    public Point3d Map(Point3d p)
    {
        if (!surface.ClosestPoint(p, out double u, out double v)) throw new ArgumentException("Cannot map a vertex onto the curved surface.");
        using Curve xCurve = surface.IsoCurve(alongU ? 0 : 1, alongU ? v : u);
        using Curve yCurve = surface.IsoCurve(alongU ? 1 : 0, alongU ? u : v);
        double x = ArcLength(xCurve, alongU ? u : v, reverseX);
        double y = ArcLength(yCurve, alongU ? v : u, reverseY);
        return new Point3d(x, y, 0);
    }

    static double ArcLength(Curve c, double parameter, bool reverse)
    {
        double a = reverse ? parameter : c.Domain.Min, b = reverse ? c.Domain.Max : parameter;
        return b - a <= 1e-12 ? 0 : c.GetLength(new Interval(a, b));
    }
}
