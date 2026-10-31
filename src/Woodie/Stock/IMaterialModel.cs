namespace Woodie.Stock;

/// <summary>Samples material anatomy in source coordinates. Cut normals are geometric, not lighting normals.</summary>
public interface IMaterialModel
{
    int Sample(double x, double y, double z);
    int Sample(double x, double y, double z, double nx, double ny, double nz);
    /// <summary>Soft knot proximity in 0..1. Zero when the model has no knot field.</summary>
    double KnotInfluence(double x, double y, double z);
    /// <summary>Alias of KnotInfluence for older callers.</summary>
    double KnotField(double x, double y, double z) => KnotInfluence(x, y, z);
    /// <summary>Unit fibre direction in material millimetres. False when the model has no fibre field.</summary>
    bool TryFibreDirection(double x, double y, double z, out double dx, out double dy, out double dz)
    { dx = dy = dz = 0; return false; }
}
