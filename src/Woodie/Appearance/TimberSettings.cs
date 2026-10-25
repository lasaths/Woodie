using System.Drawing;
using System.Globalization;

namespace Woodie.Appearance;

public sealed class TimberSettings
{
    public TimberProduct Product { get; set; } = TimberProduct.CLT;
    public TimberLook Look { get; set; } = TimberLook.Natural;
    public double VeneerThickness { get; set; } = double.NaN;
    public int Seed { get; set; }
    public double Resolution { get; set; } = 2;
    public double EdgeResolution { get; set; } = double.NaN;
    public double KnotDensity { get; set; } = double.NaN;
    public double RingSpacing { get; set; } = double.NaN;
    public double LamellaWidth { get; set; } = double.NaN;
    public double Brightness { get; set; } = 1;
    public Color? EarlywoodA { get; set; }
    public Color? EarlywoodB { get; set; }
    public Color? Latewood { get; set; }
    public Color? Knot { get; set; }
    public Color? Glue { get; set; }
    IReadOnlyList<LayerDefinition> layers;
    public IReadOnlyList<LayerDefinition> Layers { get => layers; set => layers = value == null ? null : Array.AsReadOnly(value.ToArray()); }
    public bool FitToPart { get; set; }
    // Natural Log/Bamboo anatomy (mm; NaN = unset). Unset Diameter wraps the stock's cross-section bounds.
    public double Diameter { get; set; } = double.NaN;
    public double Taper { get; set; } = double.NaN;          // mm of diameter lost per metre
    public double BarkThickness { get; set; } = double.NaN;  // Log; 0 = debarked
    public double WallThickness { get; set; } = double.NaN;  // Bamboo
    public double NodeSpacing { get; set; } = double.NaN;    // Bamboo
    public double Variation { get; set; } = 0.5;             // Natural Variation 0..1

    public TimberSettings Duplicate() { var copy = (TimberSettings)MemberwiseClone(); copy.Layers = Layers; return copy; }
    public double EffectiveEdgeResolution => double.IsNaN(EdgeResolution) ? (Product == TimberProduct.KertoQ ? 0.25 : 0.5) : EdgeResolution;
    public double EffectiveKnotDensity => double.IsNaN(KnotDensity) ? (Look == TimberLook.CleanPale ? 0.25 : Product == TimberProduct.CLT ? 8 : 3) : KnotDensity;
    // Revolved natural stock needs far fewer facets than the 1° grid used for flat timber (200k triangle budget).
    internal double GridAngle => IsNatural ? 5 * Math.PI / 180 : Math.PI / 180;
    public bool IsNatural => Product is TimberProduct.Log or TimberProduct.Bamboo;
    public bool IsEngineeredBamboo => Product is TimberProduct.BambooLumber or TimberProduct.BambooLumberSide or TimberProduct.BambooPanel;
    public double VirtualDepthMm => Layers != null ? Layers.Sum(l => l.Thickness) : Product switch
    {
        TimberProduct.BambooLumber or TimberProduct.BambooLumberSide => 40, TimberProduct.BambooPanel => 18,
        TimberProduct.Log or TimberProduct.Bamboo => double.IsNaN(Diameter) ? (Product == TimberProduct.Log ? 200 : 80) : Diameter,
        TimberProduct.CLT => 200, TimberProduct.ThreeS => 27,
        TimberProduct.KertoQ or TimberProduct.ParallelLVL => 45, TimberProduct.Plywood => 18, _ => 120
    };

    public void Validate()
    {
        if (Layers != null) {
            if (Layers.Count == 0 || Layers.Count > 4096) throw new ArgumentException("Buildup needs 1–4096 layers.");
            foreach (var layer in Layers) { if (layer == null) throw new ArgumentException("Buildup contains an empty layer."); layer.Validate(); }
            if (!double.IsFinite(Layers.Sum(l => l.Thickness))) throw new ArgumentException("Total buildup thickness is invalid.");
        }
        if (!Enum.IsDefined(typeof(TimberProduct), Product)) throw new ArgumentException("Unknown timber product.");
        if (IsNatural && Layers != null) throw new ArgumentException("Natural Log and Bamboo stock does not use buildup layers.");
        if (Product == TimberProduct.BambooLumberSide && Layers != null) throw new ArgumentException("Side-pressed bamboo lumber does not use buildup layers; use Layered bamboo panel.");
        if (IsNatural || IsEngineeredBamboo) {   // engineered bamboo strips come from internally generated Moso culms
            if (!double.IsFinite(Variation) || Variation < 0 || Variation > 1) throw new ArgumentException("Natural variation must be between 0 and 1.");
            OptionalPositive(Diameter, "Diameter"); OptionalPositive(WallThickness, "Wall thickness");
            if (!double.IsNaN(NodeSpacing) && (!double.IsFinite(NodeSpacing) || NodeSpacing < 20)) throw new ArgumentException("Node spacing must be at least 20 mm.");
            if (!double.IsNaN(Taper) && !double.IsFinite(Taper)) throw new ArgumentException("Taper must be finite.");
            if (!double.IsNaN(BarkThickness) && (!double.IsFinite(BarkThickness) || BarkThickness < 0)) throw new ArgumentException("Bark thickness must be zero or greater.");
        }
        if (!Enum.IsDefined(typeof(TimberLook), Look)) throw new ArgumentException("Unknown appearance preset.");
        OptionalPositive(VeneerThickness, "Veneer thickness");
        Positive(Resolution, "Resolution");
        Positive(Brightness, "Brightness");
        OptionalPositive(EdgeResolution, "Edge resolution");
        OptionalPositive(RingSpacing, "Ring spacing");
        OptionalPositive(LamellaWidth, "Lamella width");
        if (!double.IsNaN(KnotDensity) && (!double.IsFinite(KnotDensity) || KnotDensity < 0 || KnotDensity > 100))
            throw new ArgumentException("Knot density must be between 0 and 100 knots/metre.");
        if (Resolution < 0.05 || EffectiveEdgeResolution < 0.05)
            throw new ArgumentException("Resolution must be at least 0.05 mm/pixel.");
        if (!double.IsNaN(LamellaWidth) && LamellaWidth < 1)
            throw new ArgumentException("Lamella width must be at least 1 mm.");
    }

    static void Positive(double v, string name)
    {
        if (!double.IsFinite(v) || v <= 0) throw new ArgumentException(name + " must be finite and greater than zero.");
    }
    static void OptionalPositive(double v, string name) { if (!double.IsNaN(v)) Positive(v, name); }

    public TimberPalette Palette()
    {
        var p = TimberPalette.For(Product);
        if (Look == TimberLook.CleanPale && Product != TimberProduct.Bamboo && !IsEngineeredBamboo) {
            p.E0 = new double[] { 223, 213, 181 }; p.E1 = new double[] { 234, 224, 198 };
            p.Late = new double[] { 194, 181, 151 }; p.Knot = new double[] { 130, 110, 80 };
        }
        p.Gain = Brightness;
        if (!double.IsNaN(RingSpacing)) p.RS = RingSpacing;
        if (EarlywoodA.HasValue) p.E0 = Rgb(EarlywoodA.Value);
        if (EarlywoodB.HasValue) p.E1 = Rgb(EarlywoodB.Value);
        if (Latewood.HasValue) p.Late = Rgb(Latewood.Value);
        if (Knot.HasValue) p.Knot = Rgb(Knot.Value);
        if (Glue.HasValue) p.Glue = Rgb(Glue.Value);
        return p;
    }

    static double[] Rgb(Color c) => new double[] { c.R, c.G, c.B };
    internal string Key => string.Join("|", (int)Product, (int)Look, F(VeneerThickness), Seed, F(Resolution), F(EdgeResolution), F(KnotDensity),
        F(RingSpacing), F(LamellaWidth), F(Brightness), EarlywoodA?.ToArgb(), EarlywoodB?.ToArgb(),
        Latewood?.ToArgb(), Knot?.ToArgb(), Glue?.ToArgb()) + (Layers == null ? "" : "|custom|" + FitToPart + "|" + string.Join(";", Layers.Select(l =>
            string.Join(",", F(l.Thickness), l.AlongX, (int)l.Construction, F(l.BoardWidth), (int)l.Joints, F(l.SegmentMin), F(l.SegmentMax), l.ContinuousSheet, l.NearPith))))
        + (IsNatural || IsEngineeredBamboo ? "|natural|" + string.Join(",", F(Diameter), F(Taper), F(BarkThickness), F(WallThickness), F(NodeSpacing), F(Variation)) : "");
    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    public override string ToString() => $"Woodie {(Layers == null ? Product.ToString() : "Custom buildup (" + Product + " palette)")}, seed {Seed}, {Resolution:0.##} mm/px";
}
