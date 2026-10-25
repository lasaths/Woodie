namespace Woodie.Stock;

/// <summary>One placed region of stock, mapped into a source specimen's material space.</summary>
public sealed class MaterialPiece
{
    readonly double ox, oy, oz;
    readonly double xx, xy, xz, yx, yy, yz, zx, zy, zz;

    public MaterialPiece(SourceSpecimen specimen, IMaterialModel model,
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ,
        double ox = 0, double oy = 0, double oz = 0,
        double xx = 1, double xy = 0, double xz = 0,
        double yx = 0, double yy = 1, double yz = 0,
        double zx = 0, double zy = 0, double zz = 1)
    {
        Specimen = specimen ?? throw new ArgumentNullException(nameof(specimen));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        MinX = minX; MinY = minY; MinZ = minZ; MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        this.ox = ox; this.oy = oy; this.oz = oz;
        this.xx = xx; this.xy = xy; this.xz = xz;
        this.yx = yx; this.yy = yy; this.yz = yz;
        this.zx = zx; this.zy = zy; this.zz = zz;
    }

    public SourceSpecimen Specimen { get; }
    public IMaterialModel Model { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MinZ { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double MaxZ { get; }

    public bool Contains(double x, double y, double z) =>
        x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;

    public void ToSource(double x, double y, double z, out double sx, out double sy, out double sz)
    {
        double dx = x - ox, dy = y - oy, dz = z - oz;
        sx = xx * dx + xy * dy + xz * dz;
        sy = yx * dx + yy * dy + yz * dz;
        sz = zx * dx + zy * dy + zz * dz;
    }

    public void ToSourceNormal(double nx, double ny, double nz, out double snx, out double sny, out double snz)
    {
        snx = xx * nx + xy * ny + xz * nz;
        sny = yx * nx + yy * ny + yz * nz;
        snz = zx * nx + zy * ny + zz * nz;
    }
}
