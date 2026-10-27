namespace Woodie.Stock;

/// <summary>Samples material anatomy in source coordinates. Cut normals are geometric, not lighting normals.</summary>
public interface IMaterialModel
{
    int Sample(double x, double y, double z);
    int Sample(double x, double y, double z, double nx, double ny, double nz);
}
