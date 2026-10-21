using System.Drawing;
using System.Reflection;
using Grasshopper.Kernel;

namespace Woodie.Grasshopper;

public sealed class WoodieInfo : GH_AssemblyInfo
{
    public override string Name => "Woodie";
    public override string Version => typeof(WoodieInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? typeof(WoodieInfo).Assembly.GetName().Version?.ToString() ?? "unknown";
    public override Bitmap Icon => Icons.Tree;
    public override string Description => "Standalone procedural timber preview for Rhino geometry. " + ResearchCredit.Timber;
    public override Guid Id => new("8b9e1ab6-d335-4c07-a976-3fe7d685170b");
    public override string AuthorName => "Lasath Siriwardena";
    public override string AuthorContact => "";
}
