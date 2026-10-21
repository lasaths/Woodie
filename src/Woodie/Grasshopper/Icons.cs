using System.Drawing;

namespace Woodie.Grasshopper;

internal static class Icons
{
    static readonly Lazy<Bitmap> tree = new(() => Load("tree-evergreen-bold.png"));
    static readonly Lazy<Bitmap> sliders = new(() => Load("sliders-horizontal-bold.png"));
    static readonly Lazy<Bitmap> presets = new(() => Load("stack-bold.png"));
    public static Bitmap Tree => tree.Value;
    public static Bitmap Sliders => sliders.Value;
    public static Bitmap Presets => presets.Value;

    static Bitmap Load(string name)
    {
        using var stream = typeof(Icons).Assembly.GetManifestResourceStream("Woodie.Resources." + name)
            ?? throw new InvalidOperationException("Missing embedded icon " + name);
        // GH's public icon contract is Bitmap. Rhino supplies its platform implementation.
        using var bitmap = new Bitmap(stream);
        return new Bitmap(bitmap);
    }
}
