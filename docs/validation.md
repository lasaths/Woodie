# Release validation

Target: **0.1.0**, Rhino 8 modern .NET. Windows is the native test platform; Mac native display and interaction remain unverified.

Raw local outputs belong under ignored `artifacts/`, rather than in the public source tree.

## Results — 21 October 2026

| Release gate | Result | Scope |
| --- | --- | --- |
| Release GHA and checks-runner builds | PASS | Zero warnings and errors on this Windows host |
| Managed checks | PASS | 9 checks, zero failures |
| Full isolated Rhino suite | PASS | 28 checks, zero failures; includes 88 product/geometry cases |
| Focused material UI check | PASS | Dropdowns, precedence, undo, collapse, compatibility and archives |
| Shipped example | PASS | Reopened from disk and solved before regeneration; valid embedded geometry, guide, meshes and UVs |
| Source ZIP independent build | PASS | 0.1.0 source archive extracted and both projects built independently |
| Extracted source native suite | PASS | Uniform-colour source archive independently built; 26 checks, zero failures |
| Public-source scan | PASS | One definition; no detected private paths, common credential patterns, internal project names or broken local Markdown links |
| Binary path scan | PASS | No detected private user-directory paths in ASCII or UTF-16 strings in the release GHA |
| Dependency advisory query | PASS | NuGet reported no known vulnerable packages for the plugin and its transitive dependencies at audit time; Rhino supplies runtime SDK assemblies |
| Yak CLI and archive inspection | PASS | Rhino 8 / any tag, matching version/GHA, one example, credits and licences; no host binaries or development outputs |
| Checksums | PASS | SHA-256 file generated for the Yak and source ZIP |
| GitHub hosted workflow | PASS | [Uniform-colour Windows/Linux build and public-content checks](https://github.com/lasaths/Woodie/actions/runs/39652836057); Windows managed checks passed |
| Fresh live uniform-colour viewport acceptance | PASS | Cassis verified the installed GHA hash and clean, uniform ThreeS/Natural display on flat/bowed boards and the captured affected panel; face grain and edge layers remain visible |
| Mac native load/display/UI | NOT RUN | Native acceptance remains pending |

Release GHA SHA-256:

```text
8c4f699683daeb699bf1c4d9b40f9b6c7258ea6c91e33ca89efb35bc0e4bd40c
```

Independent source validation is recorded in the table above. Binary identity across checkouts is not asserted; compiler/debug metadata can differ. Archive checksums are distributed beside the artifacts, rather than embedded in this report.

An earlier 0.1.0 build was uploaded only to the Yak test server. That immutable test upload is not the corrected release candidate. The corrected build was uploaded under the temporary test-server name `woodie-releasecheck-20261021` at version `0.1.0`. Its downloaded archive matched the upload SHA-256, and every non-manifest entry matched the canonical candidate. This retains `woodie` / `0.1.0` for production. Fresh installed Windows viewport acceptance passed afterward for the exterior-glue build. The later uniform-colour build passed 28 native checks, independent source validation and fresh installed viewport acceptance. Its final release payload must also pass a test-server upload/download before production publication. Validation does not restart an interactive Rhino session. Raw build/native outputs, former examples and development screenshots remain local under ignored `artifacts/`.

## Uniform-colour appearance

Both looks now reduce board tint, the dark tint tail and warm shadow amplification. Reviewed face/end/oblique swatches cover all eight products. The knot-free face p90–p10 range across 48 product/look/seed combinations is at most 14/25/28 RGB levels; the ThreeS/Natural seed-17 blue range fell from 67 to 18. Updated fixtures and a 32-level per-channel contrast limit cover this intentional change. Layups, grain, knots and joints are unchanged.

## Exterior glue regression

A nearly planar face could switch from RGB 213/188/168 to 170/150/134 after a 0.00001 mm inward displacement. Glue shading incorrectly included the outside layer boundary; float mesh precision therefore drew large polygon patches. Version 0.1.0 measures only internal layer and board joints and uses cache namespace `woodie-7`. The minimal probe failed before the change and passed after it.

Permanent checks cover all eight products, both looks, two seeds, exterior depth offsets, retained internal bond lines and a rotated metre-scale atlas with four-micrometre vertex perturbations. The native atlas check exercises 96 broad-face triangles, including 85 samples below the nominal outside plane. Captured user geometry and diagnostic scripts remain private local artifacts and are not included in the repository or packages.

## Commands

Build the checks runner before the GHA because its project reference uses a DLL target:

```powershell
dotnet build tests/Woodie.Checks -c Release
dotnet build src/Woodie/Woodie.csproj -c Release
dotnet tests/Woodie.Checks/bin/Release/net8.0-windows/Woodie.Checks.dll
dotnet tests/Woodie.Checks/bin/Release/net8.0-windows/Woodie.Checks.dll --rhino --integration --geometry-gallery --examples
dotnet tests/Woodie.Checks/bin/Release/net8.0-windows/Woodie.Checks.dll --material-ui
pwsh ./tools/verify-release.ps1
pwsh ./tools/package.ps1
```

Native checks require an installed, licensed Rhino 8 and access to its runtime services. The runner seeds an isolated Grasshopper component server before starting Rhino in safe mode, then loads only built-in components and Woodie. The installed Grasshopper library folders are not used as test dependencies.

## Coverage and limits

The native geometry gallery covers all eight products over eleven geometry cases: solid Brep, extrusion, flat surface, closed mesh, open mesh, trimmed face, automatic bowed surface, automatic bowed single-face Brep, and guide-driven bowed solid Brep, extrusion and mesh. It checks native geometry preparation, atlas generation, output meshes and UVs, physical units, input preservation and mapping.

Other checks cover deterministic appearance fixtures, cut orientation, palette bounds, settings precedence, cancellation, preview lifecycle, item matching, save/reopen, legacy archives, UI selection, advanced inputs, undo/redo and panel collapse. The release example is written and reopened from disk; both embedded solids, its guide, centreline length, volume and solved outputs are checked.

Pixel fixtures establish consistency with reviewed procedural baselines. They do not prove photorealism or product-specific manufacture; [material realism](material-realism-audit.md) records the remaining differences.

Earlier live Windows checks confirmed the dropdown handlers, collapse/expand, save/reopen and textured preview for all eight products. Those observations predate this release build and must not be presented as a fresh live check of 0.1.0. Native release checks and the live installed session are reported separately.

Mac loading, viewport materials, mouse interaction and OS-level DPI changes must still be checked on Mac hardware. No stable cross-platform acceptance claim is made.
