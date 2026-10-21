# Cut-aware procedural appearance

Woodie uses a C# volumetric grain model and softwood-oriented palettes. The initial 0.1.0 release reduces board-to-board tint and warm shadow amplification in both looks following visual review. This release adds cut-aware colour/detail and small seeded changes to earlywood/latewood transition widths. It is an independent procedural adaptation informed by [Larsson et al. (2024)](https://doi.org/10.1111/cgf.15074) and [Mokume (2025)](https://doi.org/10.1145/3730874), not a reproduction of their inference pipelines or a fitted species model.

## Material sampling

Each atlas triangle supplies the cross product of its edges in mapped material coordinates. The squared normal component along the current layer's fibre axis, divided by squared normal length, gives a continuous end-grain weight. Reversing winding or changing lighting normals cannot change it. CLT cross layers and alternating veneers choose their own fibre axis. Straight, surface and guide mappings use the same atlas route.

Growth/ring sampling is independent of cut orientation. Colour synthesis uses that same phase, blending longitudinal and end-grain contrast, darkening and colour adjustments. A separate integer hash of each existing segment seed varies transition widths without consuming the board/knot random sequence. Longitudinal fine fibres blend into volumetric cross-section stipple as the cut approaches perpendicular. Pith position, ring spacing, knot placement, veneer peeling, glue and layups retain their existing construction.

The six-coordinate/normal-component `TimberPanel.Col(x, y, z, nx, ny, nz)` overload accepts non-unit normals. Zero or non-finite normals fall back to the original position-based orientation choice. The three-coordinate overload retains that choice while sharing the improved appearance. Grasshopper settings and serialization are unchanged. The current cache namespace is `woodie-7`, which invalidates earlier colour textures; `woodie-6` fixed the exterior glue-shading defect. The initial cut-aware appearance used `woodie-5`.

## Visual references and provenance

The qualitative softwood reference is Mokume sample **H11** (Hinoki), using its A, B, C and F colour photographs from the [authors' public sample folder](https://github.com/marialarsson/mokumeproject/tree/main/Samples/H11). The paired longitudinal figure, curved rings and knot-adjacent ring compression informed the restrained transition/detail treatment. Colours and dimensional parameters were not fitted to H11; Woodie's existing product palettes remain authoritative. H11 is not a spruce or engineered-panel calibration sample. The species code follows Table 1 of the [authors’ paper](https://rgl.s3.eu-central-1.amazonaws.com/media/papers/Larsson2025Mokume.pdf).

Retrieved 21 October 2026. Source file SHA-256 values:

- A_col.png: `aa91967e499479022392c3126346a7031c1e26038ae7f9e7da78794d59e2d600`
- B_col.png: `f139a90854e07b91c0d3468dbf1aaf9d75bff14c2867ef36025fb73a4e52fafb`
- C_col.png: `f649df15c049ffa9cab24032c4f396192cc0f4e15c489ba9b5b0102bad9c65d2`
- F_col.png: `0be7aa83a3de14b4846cd9abfe81552449e00e6eae0a72f43d02bf4f5325e53d`

The source dataset is [Mokume on Zenodo](https://zenodo.org/records/15588748); [project and authors](https://mokumeproject.github.io/) provide the paper, code and data links. Reference photographs are kept only in local validation artifacts, not shipped with the plugin. No research implementation, trained weights or dataset are required at runtime.

## Acceptance evidence

Before modification, fixed-seed sections were captured for all eight products in Natural and Clean/Pale. Each swatch contains longitudinal, interior perpendicular and 45-degree cuts, with identical material coordinates and unlit colour sampling before/after. In the Natural and Clean/Pale comparison sheets, baseline is on the left and the upgrade on the right. Rows are SolidTimber, KVH, GL, CLT, ThreeS, Plywood, ParallelLVL and KertoQ. Baseline source snapshots, images and reference provenance are under `artifacts/checks/appearance-upgrade/`.

The checked-in longitudinal fixture covers eight products × two looks × seeds 0, 17 and 41. Knots and glue/joint darkening are disabled for this colour comparison. RGB medians must stay within 5 levels and tenth/ninetieth percentiles within 8 of the reviewed uniform-colour baseline. The sampled p90–p10 range must also stay at or below 32 levels in each channel, preventing the previous excessive colour contrast from returning. Across the reviewed 48 combinations, the maximum ranges are 14 (red), 25 (green) and 28 (blue). For ThreeS/Natural at seed 17, the blue range changed from 67 to 18 levels. Grain, knots, seeded placement, layups, glue and joint construction are unchanged. Original appearance fixtures remain available in Git history; the uniform-colour fixtures intentionally supersede them.

Independent orientation tests cover winding, normal magnitude, fallback, interior/oblique cuts, cross layers, veneer and shared material points; atlas checks cover full triangles and padded borders on multiple cut planes.

The original five-run 512×512 pixel benchmark had median **98.8 ms**; the upgraded median is **109.0 ms** (+10.3%, target at most 15%). Both allocate **40 bytes**, below the 128 KiB budget. The three-run cold atlas median is **211.1 ms**, versus **207.3 ms** before modification (+1.8%). Measurements are local to this Windows host and include normal machine variance.

Run the checks with:

```powershell
dotnet build tests/Woodie.Checks -c Release
dotnet build src/Woodie/Woodie.csproj -c Release
dotnet run --no-build --project tests/Woodie.Checks -c Release
dotnet run --no-build --project tests/Woodie.Checks -c Release -- --benchmark-pixels --check-pixel-allocations
dotnet run --no-build --project tests/Woodie.Checks -c Release -- --benchmark-atlas
dotnet run --no-build --project tests/Woodie.Checks -c Release -- --rhino --integration --geometry-gallery
dotnet run --no-build --project tests/Woodie.Checks -c Release -- --capture-appearance current
```

Build the checks runner before the GHA: their project-reference build uses a DLL target and can remove the GHA output. Then use `--no-build` for native integration.

The baseline fixture and appearance hashes were captured/reviewed separately; ordinary checks never regenerate them. The complete neural/photo-fitting workflows, BRDF/lighting changes, and a structural wood model are outside this implementation. Mac native validation remains separate from the Windows checks.
