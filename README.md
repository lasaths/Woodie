# Woodie

Woodie previews native Rhino geometry as timber in **Rhino and Grasshopper**. Connect a mesh, Brep, surface or extrusion and choose a product to see its grain, knots, board joints and layered construction in the viewport.

Woodie resolves a fibre-aligned frame, measures the part in physical units and samples a procedural wood model over a render mesh. These material coordinates determine face grain, end grain, knots and cross-grain layers. The sampled colours become texture atlases displayed by Grasshopper. A smooth open guide curve maps grain along a bend using arc length; a coherent curved single surface can derive its own bent mapping. Input geometry is unchanged, and open surfaces stay open.

The grain and knot model is inspired by **Maria Larsson et al., [*Procedural Texturing of Solid Wood with Knots*](https://doi.org/10.1145/3528223.3530081), ACM Transactions on Graphics 41(4), Article 45, SIGGRAPH 2022**. Woodie is an independent C# procedural adaptation, not a reproduction of the research implementation or a species-calibrated material model. Full credits and further references are [below](#research-and-credits).

**Timber Material → optional Timber Buildup → Timber Preview** · **Display → Woodie**

**0.1.3** adds custom layer stacks, stock-aware cuts and native textured baking. Mac native display and a fresh interactive Bake-menu session remain unverified; see [validation](docs/validation.md).

## Install from Rhino PackageManager

1. Open Rhino and run the **PackageManager** command.
2. Search for **woodie**.
3. Select Woodie and click **Install**.
4. Restart Rhino if prompted, open Grasshopper and find **Display → Woodie**.

**Use Rhino PackageManager for normal installation and updates.** No GitHub checkout, SDK or command-line installation is needed. Remove a previously copied Woodie.gha from Grasshopper Libraries before installing through PackageManager to avoid duplicate components.

**0.1.3** targets **.NET 8** and requires **Rhino 8.20 or later** on Windows or Mac. Rhino 8.20 supplies .NET 8 by default; see [McNeel's runtime guidance](https://developer.rhino3d.com/en/guides/rhinocommon/moving-to-dotnet-core/). On Windows, use modern .NET rather than .NET Framework mode. Rhino 7 is unsupported. Native Mac display and interaction remain unverified; see [validation](docs/validation.md).

## Components

All five components are under **Display → Woodie**.

| Component | Purpose |
| --- | --- |
| **Timber Material** | Choose a product and Natural/Clean-Pale appearance. Seed controls repeatability; Resolution controls face detail in mm/pixel. Advanced inputs expose knots, rings, board/veneer dimensions and palette colours. |
| **Timber Layer** | Define one layer: thickness, longitudinal/cross grain, solid/boards/veneer construction and optional board joints. |
| **Timber Buildup** | Combine layers from bottom to top; output material settings, a schedule and section diagram. Exact thickness is checked; Fit to part scales proportions explicitly. |
| **Timber Cut** | Cut and cap closed stock with a plane. Outputs the retained geometry, original Stock and section outlines. |
| **Timber Preview** | Display textured meshes for native geometry. Fibre, Base Plane and Guide Curve control mapping; Geometry Units declares source units. Its Bake action creates native textured Rhino objects. |

**Timber Material → optional Timber Buildup → Timber Preview**. For cuts, connect Cut's Geometry and Stock outputs to Preview's Geometry and Stock Geometry inputs. The uncut stock keeps grain and knots fixed as the plane moves.

Material starts at **CLT / Natural**. Click the triangle beneath its icon to collapse the control panel without changing wires or settings. A wired Product value overrides the dropdown. Settings resolve as product defaults → appearance defaults → explicit inputs. [All inputs, outputs and defaults](docs/usage.md#complete-component-input-reference).

Choose Solid timber, KVH, Glulam, CLT, Three-layer panel (3S), Plywood, LVL — parallel or Kerto-Q. These represent different board/veneer constructions; they are visual approximations rather than manufacturing or structural specifications. See [material coverage](docs/materials.md) and [limitations](docs/material-realism-audit.md).

## Start with the example

Open the single **[Timber-preview.ghx](examples/Timber-preview.ghx)** definition with **Woodie 0.1.3**. It contains four numbered sections, with Scribble headings and groups:

1. **Cut through the stock:** one rectangular piece with position/angle sliders and a textured interior cap.
2. **Follow a knot:** a stepped cutaway with a **Knot section (%)** slider and capped exposed face, revealing the same automatic knot field through its depth.
3. **Build your own stack:** longitudinal/cross layers, a live schedule and a section diagram.
4. **Product library:** all eight products × eleven geometry cases, with a Show toggle on each product card.

The three demonstrations are visible initially; library previews start off to keep the viewport clean. Material controls start collapsed. All geometry is embedded and source units are declared, so the definition works in millimetre, metre and inch Rhino documents. Faces use **1 mm/pixel**. See the [example guide](examples/README.md). For the published 0.1.2 plugin, use its [matching gallery](https://raw.githubusercontent.com/lasaths/Woodie/v0.1.2/examples/Timber-preview.ghx).

![Moving section through the stepped timber, captured in Rhino Arctic mode](docs/images/knot-section-arctic.gif)

Move **Knot section (%)** in section 02 to reproduce this cut. The fixed camera faces the stepped profile; the moving cap samples the original material field, including the knot visible near the end of the sweep. The 186-frame loop runs from 95% to 15% and back, with eased starts/stops and slower travel through the knot crossings.

The product library uses product-appropriate thicknesses and matched flat/bowed centreline lengths. Use a product card's Show toggle to explore its geometry cases.

For older **0.1.1** installations, use its [matching two-board example](https://raw.githubusercontent.com/lasaths/Woodie/v0.1.1/examples/Timber-preview.ghx) in a **millimetre Rhino document**. The new Geometry Units input is available from 0.1.2.

Connect geometry to **Timber Preview → Geometry**, and **Timber Material → Settings** to the preview's **Settings** input. Supply Fibre or Base Plane for explicit orientation and Guide Curve for a controlled bend.

## Bake with textures

Right-click **Timber Preview → Bake**, then choose the Rhino layer normally. Woodie prepares final-quality textures and creates one native object per shown input: Breps stay joined, extrusions remain extrusions, and meshes keep their vertices and faces. Stock is a mapping reference and is not baked.

Baked objects are independent snapshots that display without Woodie. Save the `.3dm` with **Save textures** enabled for portability. Textures also live beside a saved model in `<model>_WoodieTextures`; unsaved models use Woodie's persistent application-data folder. Later shape edits or cuts require rebaking. [Bake workflow, resolution limits and persistence](docs/baking.md).

## Development

Use the .NET 8 SDK and PowerShell 7. Native checks need a licensed Rhino 8.20+ on Windows; the plugin itself targets cross-platform `net8.0`.

```powershell
dotnet build tests/Woodie.Checks -c Release
dotnet build src/Woodie/Woodie.csproj -c Release
dotnet tests/Woodie.Checks/bin/Release/net8.0-windows/Woodie.Checks.dll --rhino --integration
pwsh ./tools/package.ps1
```

Build the checks runner before the plugin because its project reference uses a DLL target. Packaging creates a slim Yak and a source ZIP under `artifacts/release/`; it does not install or publish them. See [development](docs/development.md), [packaging](docs/packaging.md), [validation](docs/validation.md), [contributing](CONTRIBUTING.md) and [changelog](CHANGELOG.md).

## Research and credits

The procedural grain and knots are inspired by **Maria Larsson, Takashi Ijiri, Hironori Yoshida, Johannes A. J. Huber, Magnus Fredriksson, Olof Broman and Takeo Igarashi**, [*Procedural Texturing of Solid Wood with Knots*](https://doi.org/10.1145/3528223.3530081), *ACM Transactions on Graphics* 41(4), Article 45, SIGGRAPH 2022. See the [paper](https://www.ma-la.com/procedural_knots/Procedural_Knots_2022.pdf) and [authors' implementation](https://github.com/marialarsson/procedural_knots).

Woodie is an independent C# procedural adaptation. Cut-aware detail also draws on Larsson et al. (2024) and *Mokume* (2025); [appearance documentation](docs/appearance.md) explains scope, references and evidence. Noise follows Ken Perlin's [*Improving Noise*](https://doi.org/10.1145/566654.566636), SIGGRAPH 2002.

Component controls follow [McNeel's custom attributes guidance](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/) and are informed by [Arup's custom Grasshopper UI examples](https://github.com/arup-archive/Custom-Grasshopper-UI-Components). Neither Arup/Oasys nor the research implementation is required at runtime. Icons are [Phosphor Icons](https://phosphoricons.com/).

Released under the [MIT licence](LICENSE). Upstream copyright and icon notices are retained in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
