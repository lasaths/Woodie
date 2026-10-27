# Release validation

## 0.1.4 — shared stock, natural materials and engineered bamboo — 2026-10-26

Branch `codex/shared-stock-bamboo` merged to `main` via PR #2.

- Release builds of `Woodie.Checks` then `Woodie.gha`: PASS, zero warnings/errors.
- Native `--rhino` with `--examples`: PASS, **37** checks including stock archive, natural geometry/cuts, culm preview mesh under the triangle cap, vertical bamboo half-cut stock mapping, component GUIDs, and regenerate/reopen of Timber-preview.ghx.
- Combined example now includes sections **05 Rotated tower** and **06 Natural bamboo**; temporary live Cut/Preview probes were discarded and the ghx regenerated from `TimberExamples`.
- Mac native checks: not run.
- Limitations recorded in development notes: bark ovality, constant bamboo wall with wobble, Lamella/Veneer reused for strip size, no scrimber/flattened culms/user manufacturing sequences, age/reuse deferred. Strip machining windows track Outer(x)/WallAt(x) along each culm.
- Publication (2026-10-27): verified GHA SHA-256 `0C7AC71D85D7E48EEC8BF2C24C6F628977CB3F41DA5597CB93579E3FF5A7A2D6`; Yak `woodie-0.1.4-rh8_20-any.yak` SHA-256 `e22007ae31e492b539221f62b7400279274988ba7c073d3334d7c1e6eae6cc15`. Test-server push to `https://test.yak.rhino3d.com/` succeeded; production push of `woodie` / `0.1.4` succeeded. GitHub release: https://github.com/lasaths/Woodie/releases/tag/v0.1.4.

## Native textured baking — 2026-10-25

Bake is integrated. Projection-based mapping failed the remesh gate; production uses Rhino packed surface UVs for Breps/surfaces, enclosing-cube box mapping for solid extrusions, and topology-preserving mesh charts. Headless suite: **40** integration checks and the full **88**-case bake matrix. Cassis inspected native Brep/extrusion/mesh proofs and the stepped knot section. Failed projection probes remain opt-in under `tests/Woodie.Checks/Diagnostics/`. See [feasibility](development/native-baking-feasibility.md), [bake usage](baking.md) and the [bake results](#native-textured-baking-2026-10-25) below. **0.1.3** is published with Mac and a fresh interactive Bake-menu session still outstanding, matching the 0.1.2 publication pattern.

## 0.1.3 — one clean example and unique icons

- Release builds: PASS; zero warnings/errors.
- Native suite: PASS, 36 checks including regeneration, archive reopening, controls and appearance regressions. All 88 library cases solve in millimetre/metre documents; cutting, knot and custom-buildup demonstrations also solve in inch documents.
- One self-contained Timber-preview.ghx now contains four numbered Scribble sections and rectangular groups. The three demonstrations are visible; eight product Show toggles start off. Material panels start compact, shared wires appear on selection, and only the live schedule uses a Panel.
- Canvas layout: PASS; Consolas is used by every Scribble and the schedule Panel, and bounds retain at least 8 px separation. All 36 checks pass after the font change. The installed Consolas opening view, schedule and distinct Layer/Buildup icons were subsequently inspected through Cassis. Percentage/degree sliders display their actual labelled units; native components perform the conversions.
- Icons: PASS; all five ribbon components, the legacy Presets component and the two settings/layer parameters have distinct 24 px Phosphor pixel fingerprints. Timber Layer uses one rectangle; Timber Buildup uses a stack.
- Existing Natural appearance hashes and 1 mm/pixel library density are unchanged. Custom layer construction, exact/fit thickness handling, immutable copies, old/new settings archives and cache separation pass.
- Capped Brep, extrusion and mesh cuts preserve the original stock field, including guide-mapped bent stock. Earlier live Windows checks passed for moved/angled/reversed cuts, the stepped knot transition and the 30 / 60 cross / 30 mm buildup and diagram.
- Updated Yak/source inspection: PASS; exactly one combined definition, required credits and no developer docs in the runtime Yak. The independently extracted source builds with zero warnings/errors and passes all 35 integration checks.
- Refreshed GHA installation: PASS; SHA-256 matches the tested binary, `43eb4a5cb573b5e295d9d3b94a2ba1fb4b9f1dbbc7f52226b9280962e6076b3c`. The sole installed definition is Timber-preview.ghx; the final section-slider archive matches the repository SHA-256 `5afbef7bced2aedddca137d484903bbb35cb0a71900717a52fe92afdb7b26590`. The two superseded installed definitions were backed up before removal.
- Installed Windows canvas check: PASS; Consolas annotations, compact materials and distinct Layer/Buildup icons were inspected. The refreshed 414-object example was subsequently loaded from the repository and solved without reported errors; the ribbon popup was not separately captured.
- Stepped-section example: PASS, all 36 checks after adding Knot section (%). Slider positions reopen and solve in millimetre, metre and inch documents with fixed material frames. The stored default plane normal was explicitly cleared after live inspection revealed a second overlapping cut; regression coverage requires exactly one cut frame and verifies its normal, physical retained volume and stable frame in all three unit systems. The strengthened suite passes all 35 integration checks (example regeneration was not repeated).
- Live Arctic recording (2026-10-25): PASS through Cassis on Windows. One cutting plane, one closed capped Brep and one outline, with no errors/warnings. A fixed low camera faces the stepped profile. Captured 94 distinct slider positions from 95% down to 15%; the end face reveals a knot. Smoothstep easing and continuous Gaussian weighting add finer steps around four observed knot crossings (83.5%, 66%, 49.5% and 17%). The README embeds a 186-frame, 12.24-second looping GIF with a position indicator. Encoding checks confirm distinct captures, monotonic outbound positions, frame count, loop flag and total duration; encoded knot detail was visually inspected. The camera helper was temporary and removed; the shipped example contains no scripts. Mac native checks remain unperformed.

## Example resolution correction — 2026-10-24

- Reproduced coarse final surface textures at 8 mm/pixel for all eight shipped trimmed-surface examples; the new density regression failed before the fix.
- Corrected saved gallery face resolution to 1 mm/pixel; atlas measurements confirm that density is retained for all eight materials.
- Native integration suite: PASS, 31 checks. The regenerated 88-case gallery also reopens and solves in millimetre and metre documents.
- Cassis on installed 0.1.2: first verified the intermediate 2 mm/pixel gallery for all eight materials and inspected a close view of the trimmed Three-layer panel. The final 1 mm/pixel gallery was then reopened and its resolved material setting checked. The previous modified definition remains open.
- No rendering engine or appearance fixture changes. This example correction is not yet published in Yak; Mac display was not rechecked.

## 0.1.2 — slim package and explicit source units

- Release builds: PASS, zero warnings/errors.
- Full native suite: PASS, 31 checks including existing 88 product/geometry cases.
- Expanded shipped gallery: all 88 cases reopen and solve in millimetre and metre documents; physical output dimensions agree.
- Explicit source-unit inputs: tested in millimetre, metre and inch documents; original geometry remains unchanged.
- Guide lengths and plane origins scale with geometry; invalid source/document units and old six-input archives are covered.
- The original metre-document reproduction failed with an unhelpful boards/knots error before the fix. It now reports physical dimensions, source/document units and the Geometry Units remedy.
- Appearance golden fixtures remain unchanged.
- Slim Yak inspection: PASS; developer docs are excluded and the expanded example is included.
- Independent extracted-source build: PASS; both projects build and all 30 native integration checks pass.
- Hosted checks: PASS; [Windows/Linux builds and Windows managed checks](https://github.com/lasaths/Woodie/actions/runs/39811473441).
- Fresh installed 0.1.2 viewport and Mac native checks: not yet run. Published with these checks outstanding.



## 0.1.1 — .NET 8

Target: Rhino 8.20+ running .NET 8. The release uses the exact locally installed and viewport-tested GHA recorded below.

| Check | Result |
| --- | --- |
| Plugin and runner Release builds | PASS; zero warnings/errors |
| Managed checks | PASS; 10 checks |
| Full Windows native suite | PASS; 28 checks, including 88 material/geometry cases |
| Appearance fixtures | PASS; no changes to grain, colours or seams from 0.1.0 |
| Yak layout and source/content checks | PASS; net8.0 GHA, Rhino 8.20/any distribution, one example |
| Extracted source build | PASS; both projects built independently and 27 native checks passed |
| Hosted workflow for this update | PASS; [Windows/Linux builds and Windows managed checks](https://github.com/lasaths/Woodie/actions/runs/39667884704) |
| Fresh installed viewport check | PASS; local Yak 0.1.1 loaded on Rhino 8.35 / .NET 8.0.31, with clean flat and guide-bowed ThreeS/Clean-Pale previews |
| Mac native display and interaction | Not run |

The local Yak GHA SHA-256 is `02c94fbab1716e5e516afb2bf89dd99d7dab74547e68fdc867368769ab116213`. No previous Woodie installation or loaded assembly was present; the local candidate was installed through Yak and registered into the running Grasshopper session. Fresh-start automatic discovery remains to be checked. A separate example copy was scaled from millimetres into the current metre document; both parts solved without errors or warnings, and the bowed output contained 2,516 triangles. Rhino document units and source example geometry were preserved.

The RhinoCommon/Grasshopper API reference remains the 8.0 SDK. The host runtime requirement is stricter: Rhino 8.20 supplies .NET 8 by default. Packaging marks the distribution rh8_20 to enforce that baseline.

## 0.1.0 published release


Target: **0.1.0**, Rhino 8 modern .NET. Windows is the native test platform; Mac native display and interaction remain unverified.

Raw local outputs belong under ignored `artifacts/`, rather than in the public source tree.

## Results — 21 October 2026

| Release gate | Result | Scope |
| --- | --- | --- |
| Release GHA and checks-runner builds | PASS | Zero warnings and errors on this Windows host |
| Managed checks | PASS | 10 checks, zero failures |
| Full isolated Rhino suite | PASS | 28 checks, zero failures; includes 88 product/geometry cases |
| Focused material UI check | PASS | Dropdowns, precedence, undo, collapse, compatibility and archives |
| Shipped example | PASS | Reopened from disk and solved before regeneration; valid embedded geometry, guide, meshes and UVs |
| Source ZIP independent build | PASS | 0.1.0 source archive extracted and both projects built independently |
| Extracted source native suite | PASS | Light-seam source archive independently built; 27 checks, zero failures |
| Public-source scan | PASS | One definition; no detected private paths, common credential patterns, internal project names or broken local Markdown links |
| Binary path scan | PASS | No detected private user-directory paths in ASCII or UTF-16 strings in the release GHA |
| Dependency advisory query | PASS | NuGet reported no known vulnerable packages for the plugin and its transitive dependencies at audit time; Rhino supplies runtime SDK assemblies |
| Yak CLI and archive inspection | PASS | Rhino 8 / any tag, matching version/GHA, one example, credits and licences; no host binaries or development outputs |
| Checksums | PASS | SHA-256 file generated for the Yak and source ZIP |
| GitHub hosted workflow | PASS | [Light-seam Windows/Linux build and public-content checks](https://github.com/lasaths/Woodie/actions/runs/39659155148); Windows managed checks passed |
| Fresh live final viewport acceptance | PASS | Cassis verified the installed GHA hash and clean flat/bowed ThreeS/Clean-Pale display using the shipped material settings, with no glue-colour override |
| Mac native load/display/UI | NOT RUN | Native acceptance remains pending |

Release GHA SHA-256:

```text
642787455fe53092c1ed646e9d32ce40f69eed3442ecf2639835f46adc681a71
```

Independent source validation is recorded in the table above. Binary identity across checkouts is not asserted; compiler/debug metadata can differ. The distributed GHA is promoted by its recorded SHA-256 from the exact artifact tested in Rhino; documentation-only packaging does not replace it with a rebuild. A comparison against the final source build found all 358 method bodies/signatures, custom attributes and embedded resources identical, with different module/debug identities. Archive checksums are distributed beside the artifacts, rather than embedded in this report.

Before production publication, the final archive must pass a Yak test-server upload/download with SHA-256 equality and byte comparisons of every entry except the temporary manifest name. Test uploads use unique temporary package names because versions are immutable; production retains `woodie` / `0.1.0`. Validation does not restart an interactive Rhino session. Raw build/native outputs, former examples and development screenshots remain local under ignored `artifacts/`.

## Uniform-colour appearance

Both looks now reduce board tint, the dark tint tail and warm shadow amplification. Reviewed face/end/oblique swatches cover all eight products. The knot-free face p90–p10 range across 48 product/look/seed combinations is at most 14/25/28 RGB levels; the ThreeS/Natural seed-17 blue range fell from 67 to 18. Updated fixtures and a 32-level per-channel contrast limit cover this intentional change. Layups, grain, knots and joints are unchanged.

## Light Three-layer seams

The user identified residual dark longitudinal streaks on both example boards after the broad-polygon and colour fixes. A sampler-only reproduction found 7,334 dark samples, concentrated at the 120 mm board boundaries. Disabling glue shading removed 7,305 of these samples; disabling knots or segment joints did not remove the boundary streaks. The ThreeS preset described light melamine glue but used black. Its default adhesive colour is now light cream (215/205/185), retaining the seam construction without dark tracks. The same probe has 29 dark samples from small knots; disabling knots leaves zero. Veneer adhesive colours are unchanged, and explicit glue overrides remain supported. The new seam regression failed before this fix and passes for both looks and three seeds afterward.

## Exterior glue regression

A nearly planar face could switch from RGB 213/188/168 to 170/150/134 after a 0.00001 mm inward displacement. Glue shading incorrectly included the outside layer boundary; float mesh precision therefore drew large polygon patches. Version 0.1.0 measures only internal layer and board joints and uses cache namespace `woodie-8`. The minimal probe failed before the change and passed after it.

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

## Native textured baking (2026-10-25)

The Windows native integration suite passes **40 checks**. A separate bake matrix generates final PNGs for **8 products × 11 geometry cases**, using 16 mm/pixel faces and 8 mm/pixel edges with product-appropriate depths. The native geometry/persistence checks use 8/4 mm/pixel. These are declared test settings, not changes to component defaults. The unpublished 0.1.3 Yak GHA SHA-256 is `a3bf4009c696b88b61958e8aa18d0047bb31c5762f4c466910a5db2fc9ad67cc` (matches the local Release build used for those checks).

All eleven geometry cases retain native types and mesh topology, save without render meshes, reopen and regenerate different meshes. No sampled triangle centre loses its native UV chart; largest measured physical interpolation difference is 0.049 mm. Unit/Stock checks cover millimetre, metre and inch documents, capped custom-stack cuts and unchanged sources. Repeated bakes share the registered Rhino render material and content-addressed PNG; per-object legacy simulation rows are a Rhino detail. A failure on item two removes item one's object and new materials. Pre-cancelled batches add nothing. Embedded bitmap lookup succeeds after the external PNG is temporarily made unavailable.

Cassis has displayed native textured Brep, bowed extrusion and mesh proofs. The installed plugin's Bake menu/action, interactive cancellation, stale-solution cancellation, Undo/Redo, and portable reopened viewport still require live checks after PackageManager install. Mac remains untested. See the [native mapping investigation](development/native-baking-feasibility.md) for failed approaches and [bake instructions](baking.md) for limitations. Published with these live/Mac checks outstanding.

Publication (2026-10-25): verified GHA SHA-256 `a3bf4009c696b88b61958e8aa18d0047bb31c5762f4c466910a5db2fc9ad67cc`; Yak `woodie-0.1.3-rh8_20-any.yak` SHA-256 `e3b062bbf0e56496d3175d9226fc7534a2d427825ad6ba49b0e36b042f37036a`. Temporary test-server package `woodie-releasecheck-20261025171805` upload/download matched byte-for-byte except the temporary manifest name. Production push of `woodie` / `0.1.3` succeeded. GitHub release: https://github.com/lasaths/Woodie/releases/tag/v0.1.3.

The full-resolution stepped knot section was inspected through Cassis in Rendered mode. Moving the inset into native mapping eliminated white face-boundary lines; its 2144-pixel atlas retains 1 mm/pixel face and 0.5 mm/pixel edge settings. The baked object remains a joined capped Brep. See the [close-view evidence](development/images/native-bake-knot-section.png).
