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

    public TimberSettings Duplicate() { var copy = (TimberSettings)MemberwiseClone(); copy.Layers = Layers; return copy; }
    public double EffectiveEdgeResolution => double.IsNaN(EdgeResolution) ? (Product == TimberProduct.KertoQ ? 0.25 : 0.5) : EdgeResolution;
    public double EffectiveKnotDensity => double.IsNaN(KnotDensity) ? (Look == TimberLook.CleanPale ? 0.25 : Product == TimberProduct.CLT ? 8 : 3) : KnotDensity;
    public double VirtualDepthMm => Layers != null ? Layers.Sum(l => l.Thickness) : Product switch
    {
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
        if (Look == TimberLook.CleanPale) {
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
            string.Join(",", F(l.Thickness), l.AlongX, (int)l.Construction, F(l.BoardWidth), (int)l.Joints, F(l.SegmentMin), F(l.SegmentMax), l.ContinuousSheet, l.NearPith))));
    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    public override string ToString() => $"Woodie {(Layers == null ? Product.ToString() : "Custom buildup (" + Product + " palette)")}, seed {Seed}, {Resolution:0.##} mm/px";
}
