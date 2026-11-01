# Woodie

Procedural timber preview for Rhino 8.20+ / .NET 8 and Grasshopper.

Install and update through Rhino **PackageManager**, searching for **woodie**.
Components are under **Display → Woodie**: connect Timber Material → Settings to Timber Preview → Settings.

Open the single examples/Timber-preview.ghx definition for all examples. Numbered Scribble headings and groups organize cutting, knot transitions, custom buildup, one eight-product library, a same-log tower and a bamboo culm section.
Demonstrations are visible initially. The library Show toggle starts off; Material panels start compact.
Timber Layer and Timber Buildup define physical stacks and a section diagram. Exact thicknesses are checked; Fit to part scales proportions explicitly. Material Stock, Log Stock and Bamboo Stock build Stock Models. Older Natural Stock nodes reopen as Log Stock. Connect a Stock Model, or Timber Cut's uncut Stock output, so cutting preserves grain. Cut outputs are in document units.
The gallery declares Geometry Units = Millimetres, so it works in millimetre, metre and inch documents.
For your own geometry, Geometry Units defaults to Document; choose the source units explicitly when the coordinate values came from another unit system.
Input geometry is never transformed in place.

Full [documentation and component settings](https://github.com/lasaths/Woodie#readme),
[material limitations](https://github.com/lasaths/Woodie/blob/main/docs/material-realism-audit.md),
and source code are on GitHub. Materials are visual approximations, not structural specifications.
Native Mac display and interaction have not been verified.

Appearance inspired by Maria Larsson et al., [Procedural Texturing of Solid Wood with Knots](https://doi.org/10.1145/3528223.3530081),
ACM TOG 41(4), Article 45, SIGGRAPH 2022; independent C# procedural adaptation.

MIT licence and third-party credits are included alongside this file.

Right-click engineered **Timber Preview** for Textured, Gradient or Force flow. Bake stays textured. Natural Log and Bamboo stay Textured.
Bake by right-clicking **Timber Preview → Bake**. This creates native textured Rhino geometry; enable **Save textures** when saving the model. Shape changes require rebaking.
