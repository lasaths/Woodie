namespace Woodie.Stock;

/// <summary>Piece arrangement inside a stock definition. Joints/adhesive live in the material models for now.</summary>
public sealed class Construction
{
    readonly MaterialPiece[] pieces;

    public Construction(IEnumerable<MaterialPiece> pieces)
    {
        this.pieces = (pieces ?? throw new ArgumentNullException(nameof(pieces))).ToArray();
        if (this.pieces.Length == 0) throw new ArgumentException("Construction needs at least one material piece.");
        if (this.pieces.Any(p => p == null)) throw new ArgumentException("Construction contains an empty piece.");
    }

    public IReadOnlyList<MaterialPiece> Pieces => pieces;

    /// <summary>Resolve the piece covering a stock-space point. Falls back to the nearest piece centre.</summary>
    public MaterialPiece PieceAt(double x, double y, double z)
    {
        for (int i = pieces.Length - 1; i >= 0; i--)
            if (pieces[i].Contains(x, y, z)) return pieces[i];
        MaterialPiece best = pieces[0];
        double bestD = Distance2(best, x, y, z);
        for (int i = 1; i < pieces.Length; i++)
        {
            double d = Distance2(pieces[i], x, y, z);
            if (d < bestD) { best = pieces[i]; bestD = d; }
        }
        return best;
    }

    static double Distance2(MaterialPiece p, double x, double y, double z)
    {
        double cx = 0.5 * (p.MinX + p.MaxX), cy = 0.5 * (p.MinY + p.MaxY), cz = 0.5 * (p.MinZ + p.MaxZ);
        double dx = x - cx, dy = y - cy, dz = z - cz;
        return dx * dx + dy * dy + dz * dz;
    }
}
