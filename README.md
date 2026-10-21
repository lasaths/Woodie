# Woodie

Procedural timber preview for **Rhino 8 and Grasshopper**. Apply grain, knots and engineered-timber layers to meshes, Breps, surfaces and extrusions.

**Timber Material → Timber Preview** · **Display → Woodie**

![Timber Material controls](docs/images/material-controls.png)

## Release status

**0.1.0** is the first release for Rhino 8 using modern .NET. Windows native loading, geometry and component checks are covered by the [validation report](docs/validation.md). Mac compatibility is intended; native Mac display and interaction have not been verified. Rhino 7 and Windows .NET Framework mode are unsupported.

The materials are visual approximations. Product layups, colours and bending do not specify a manufactured or structural product. See [material coverage and known limitations](docs/materials.md).

## Install

Download the `.yak` release asset and install it with Rhino's Yak CLI:

```text
yak install <path-to-woodie-package.yak>
```

Restart Rhino, open Grasshopper, and find the components under **Display → Woodie**. The CLI ships with Rhino; [McNeel's instructions](https://developer.rhino3d.com/en/guides/yak/yak-cli-reference/) list its location on each platform. Remove any manually installed Woodie GHA before installing the Yak package to avoid duplicate components.

For manual installation, copy the release's `net7.0/Woodie.gha` into your Grasshopper Libraries folder. Install either the Yak package or a manual GHA copy.

## Start with the example

Open [examples/Timber-preview.ghx](examples/Timber-preview.ghx) in a **millimetre Rhino document**. It contains internal geometry, one Timber Material component and flat/upward-bowed previews with equal centreline lengths. No external geometry, scripts or data files are required. The saved Three-layer panel / Clean/Pale settings demonstrate a 27 mm section; use the dropdowns to explore the other visual presets.

1. Connect Rhino geometry to **Timber Preview → Geometry**.
2. Connect **Timber Material → Settings** to the preview's **Settings** input.
3. Choose Product and Appearance. **CLT / Natural** are the component defaults.
4. Supply a Fibre vector or Base Plane to control orientation; use an open Guide Curve for bent geometry.

Click the triangle below the material icon to hide or reveal the whole control panel ([collapsed view](docs/images/material-collapsed.png)). **Advanced inputs** exposes optional appearance controls. **Resolution** is measured in **mm/pixel**; smaller values add detail at greater cost.

Preview outputs include UV-mapped meshes, product labels, aligned boxes and frames. Woodie supplies the viewport materials; downstream mesh previews do not inherit them. Original input geometry is unchanged. Open surfaces remain open.

See the [usage guide](docs/usage.md) for input precedence, units, alignment, bending and cache behaviour.

## Materials

| Product | Construction represented |
| --- | --- |
| Solid timber | Continuous solid wood without manufactured joints |
| KVH | Finger-jointed solid timber |
| Glulam | Parallel timber laminations |
| CLT | Seven-layer cross-laminated board preset |
| Three-layer panel (3S) | Longitudinal faces and cross-grain core |
| Plywood | Alternating veneers with an odd layer count |
| LVL — parallel | Longitudinal veneers |
| Kerto-Q | Cross-banded LVL, based on the 45 mm layup |

Natural and Clean/Pale affect colour and knot appearance without changing the layup. Species, coatings, OSB/MDF and NLT/DLT are outside this release. Thickness-dependent layups and curved manufacture have [documented limitations](docs/material-realism-audit.md).

## Build and package

Use the .NET 8 SDK or newer and PowerShell 7. Restore supplies the .NET 7 targeting references used by Rhino 8.

```powershell
dotnet build tests/Woodie.Checks -c Release
dotnet build src/Woodie/Woodie.csproj -c Release
dotnet tests/Woodie.Checks/bin/Release/net8.0-windows/Woodie.Checks.dll
pwsh ./tools/package.ps1
```

The checks runner is Windows-only; native checks additionally require an installed, licensed Rhino 8. Build the runner before the plugin: its project reference uses a DLL target. Packaging creates a validated Yak and a source ZIP under `artifacts/release/`, including exactly one example, documentation and licences. It does not install or publish anything. See [packaging](docs/packaging.md), [development](docs/development.md), [contributing](CONTRIBUTING.md) and the [changelog](CHANGELOG.md).

## Research and credits

The procedural grain and knots are inspired by **Maria Larsson, Takashi Ijiri, Hironori Yoshida, Johannes A. J. Huber, Magnus Fredriksson, Olof Broman and Takeo Igarashi**, [*Procedural Texturing of Solid Wood with Knots*](https://doi.org/10.1145/3528223.3530081), *ACM Transactions on Graphics* 41(4), Article 45, SIGGRAPH 2022. See the [paper](https://www.ma-la.com/procedural_knots/Procedural_Knots_2022.pdf) and [authors' implementation](https://github.com/marialarsson/procedural_knots).

Woodie is an independent C# procedural adaptation. Cut-aware detail also draws on Larsson et al. (2024) and *Mokume* (2025); [appearance documentation](docs/appearance.md) explains scope, references and evidence. Noise follows Ken Perlin's [*Improving Noise*](https://doi.org/10.1145/566654.566636), SIGGRAPH 2002.

Component controls follow [McNeel's custom attributes guidance](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/) and are informed by [Arup's custom Grasshopper UI examples](https://github.com/arup-archive/Custom-Grasshopper-UI-Components). Neither Arup/Oasys nor the research implementation is required at runtime. Icons are [Phosphor Icons](https://phosphoricons.com/).

Released under the [MIT licence](LICENSE). Upstream copyright and icon notices are retained in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
