using System.Drawing;

namespace Woodie.Grasshopper;

internal static class Icons
{
    static readonly Lazy<Bitmap> tree = new(() => Load("tree-evergreen-bold.png"));
    static readonly Lazy<Bitmap> sliders = new(() => Load("sliders-horizontal-bold.png"));
    static readonly Lazy<Bitmap> buildup = new(() => Load("stack-bold.png"));
    static readonly Lazy<Bitmap> layer = new(() => Load("rectangle-bold.png"));
    static readonly Lazy<Bitmap> presets = new(() => Load("swatches-bold.png"));
    static readonly Lazy<Bitmap> settingsParameter = new(() => Load("palette-bold.png"));
    static readonly Lazy<Bitmap> layerParameter = new(() => Load("list-dashes-bold.png"));
    static readonly Lazy<Bitmap> cut = new(() => Load("selection-slash-bold.png"));
    static readonly Lazy<Bitmap> eye = new(() => Load("eye-bold.png"));
    static readonly Lazy<Bitmap> package = new(() => Load("package-bold.png"));
    static readonly Lazy<Bitmap> cube = new(() => Load("cube-bold.png"));
    public static Bitmap Tree => tree.Value;
    public static Bitmap Sliders => sliders.Value;
    public static Bitmap Presets => presets.Value;
    public static Bitmap Buildup => buildup.Value;
    public static Bitmap Layer => layer.Value;
    public static Bitmap SettingsParameter => settingsParameter.Value;
    public static Bitmap LayerParameter => layerParameter.Value;
    public static Bitmap Cut => cut.Value;
    public static Bitmap Eye => eye.Value;
    public static Bitmap Package => package.Value;
    public static Bitmap Cube => cube.Value;

    static Bitmap Load(string name)
    {
        using var stream = typeof(Icons).Assembly.GetManifestResourceStream("Woodie.Resources." + name)
            ?? throw new InvalidOperationException("Missing embedded icon " + name);
        // GH's public icon contract is Bitmap. Rhino supplies its platform implementation.
        using var bitmap = new Bitmap(stream);
        return new Bitmap(bitmap);
    }
}
