namespace Woodie.Appearance;

public enum LayerConstruction { Solid, Boards, Veneer }
public enum LayerJoints { None, Butt, Finger }

// Physical layer recipes are immutable; resolved z positions belong to a particular stock.
public sealed record LayerDefinition(double Thickness = 30, bool AlongX = true,
    LayerConstruction Construction = LayerConstruction.Boards, double BoardWidth = 150,
    LayerJoints Joints = LayerJoints.None, double SegmentMin = 1500, double SegmentMax = 4000,
    bool ContinuousSheet = true, bool NearPith = false)
{
    public void Validate()
    {
        if (!double.IsFinite(Thickness) || Thickness <= 0) throw new ArgumentException("Layer thickness must be finite and greater than zero (mm).");
        if (!Enum.IsDefined(Construction) || !Enum.IsDefined(Joints)) throw new ArgumentException("Unknown layer construction or joint type.");
        if (Construction == LayerConstruction.Boards && !double.IsNaN(BoardWidth) && (!double.IsFinite(BoardWidth) || BoardWidth < 1)) throw new ArgumentException("Board width must be at least 1 mm, or unset for full width.");
        if (!(SegmentMin > 0) || !double.IsFinite(SegmentMin + SegmentMax) || SegmentMax < SegmentMin) throw new ArgumentException("Layer segment lengths are invalid.");
        if (Construction != LayerConstruction.Boards && Joints != LayerJoints.None) throw new ArgumentException("End joints apply only to board layers.");
    }
}
