# Woodie

Procedural timber preview for Rhino 8.20+ / .NET 8 and Grasshopper.

Install and update through Rhino **PackageManager**, searching for **woodie**.
Components are under **Display → Woodie**: connect Timber Material → Settings to Timber Preview → Settings.

Open the single examples/Timber-preview.ghx definition for all examples. Numbered Scribble headings and groups organize cutting, knot transitions, custom buildup and the eight-product library.
The three demonstrations are visible initially. Product library Show toggles start off; Material panels start compact.
Timber Layer and Timber Buildup define physical stacks and a section diagram. Exact thicknesses are checked; Fit to part scales proportions explicitly. Connect Timber Cut's uncut Stock output to Preview's Stock Geometry to preserve grain while cutting. Cut outputs are in document units.
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
