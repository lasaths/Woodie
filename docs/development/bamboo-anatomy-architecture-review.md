# Bamboo anatomy — Phase 0 architecture review

Date: 2026-11-02. Status: **Phase 0 baseline**; persistent volumetric bundles shipped afterward (`woodie-19`, see concern 1 resolution).

Workspace: `Woodie-stock-bamboo` on branch `main` (`91a3d41`, package version still **0.1.5**). Tag `v0.1.5` is the published start; two post-tag appearance commits already sit on `main` (`e162a86`, `91a3d41`). The sibling `Woodie` checkout on `master` is still timber-only 0.1.3 and must not be used for this work.

Related notes: [`stock-architecture.md`](stock-architecture.md), [`natural-materials-research.md`](natural-materials-research.md), [`bamboo-culm-segment-distribution.md`](bamboo-culm-segment-distribution.md), [`bamboo-appearance-research.md`](bamboo-appearance-research.md).

## Baseline tests

| Suite | Result |
| --- | --- |
| Managed Release checks | **14 passed**, 0 failed |
| `--rhino` | **37 passed**, 0 failed (includes natural solids/volume/cuts) |
| `--rhino --integration` | **48 passed**, 0 failed (includes Bamboo Stock component, archive, preview atlas, combined bamboo demos) |
| `--bamboo-probe` (diagnostic) | See concern 1 evidence below |

Build order used: `dotnet build tests/Woodie.Checks -c Release`, then `dotnet run … -- --rhino --integration`.

## Current construction (what exists)

```
Line / settings → NaturalFeatures.Resolve (once)
                 ├─ NaturalGeometry.Create  → revolved Brep (Profile)
                 └─ BambooMaterialModel     → point Sample in stock mm
Stock / engineered strips → MaterialPiece.ToSource → same BambooMaterialModel (+ StripModel glue)
```

| Module | Role today |
| --- | --- |
| `NaturalFeatures` | Immutable specimen: taper, ovality, RIN–RCIL nodes, `Outer`/`WallAt`/`Inner`/`Ridge`/`Solid`/`Profile`, appearance offsets, species skin, Grosser free-fibre count |
| `NaturalGeometry` | Revolves `Profile()` runs into a joined solid (void shells for cavity) |
| `BambooMaterialModel` | Procedural shade: wall `t`, cellular `BundleField`, epidermis, Peng-style node colour/reorient |
| `BambooSpecies` | Moso / Guadua / Asper presets (Ø range, wall, spacing, taper, skin, `CulmInternodes`, `PeakRin`, free-fibre type) |
| `BambooConstruction` | Engineered lumber/panel: one `MaterialPiece` per strip, rigid map into generated Moso culms, planed radial window |
| `StockDefinition` | `FromNatural` / `FromBamboo` / timber wrap; sampling stock → piece → source → model |
| GH | `BambooStockComponent` (+ attributes), shared `NaturalStockSolve`, `StockGoo`, Preview/Cut/Bake unchanged paths |

Larsson principle already holds at the **stock** level: cuts sample a fixed definition. It does **not** yet hold for **vascular-bundle identity** inside the culm wall (concern 1).

## Reuse / modify / replace

### Reuse as-is (keep contracts)

- Stock pipeline: `WoodieStock` → `StockDefinition` → `Construction` → `MaterialPiece` → `IMaterialModel`
- GH GUIDs, port order, `StockGoo` archive, Preview/Cut/Bake, Geometry Units, atlas/cache versioning rules
- Species library shape and cured-skin RGB as appearance presets
- RIN–RCIL node placement (`PlaceBambooNodes` / `Rcil`) — already non-equal; Variation 0 basal window matches structural-pole practice
- Engineered strip **grid locate**, glue bands, plain/side/panel axis conventions (covered by `StockChecks.EngineeredBamboo`)
- Separate geometry vs appearance RNG streams in `NaturalFeatures`
- `NaturalGeometry` revolve + void-join pattern

### Modify (keep type, change internals)

- `BambooMaterialModel.Shade` / `BundleField` — replace cellular resampling with sampling of a persistent bundle field (Phase 2)
- `NaturalFeatures.WallAt` / taper coupling — add anatomy-driven wall thickness along height (Phase 1)
- `NaturalFeatures.Profile` diaphragms/ridges — keep shared node stations; deepen geometry only where anatomy requires it
- `StockDefinition.TryFibreDirection` — when bamboo reports fibre, transform source vectors back through the piece frame (see concern 7)
- `BambooConstruction.RadialWindow` / strip frame — keep strip pieces; optionally improve chord/wall window once source anatomy is richer
- Checks: keep appearance contrast asserts; add axial-identity / fibre-direction asserts when Phase 2 lands

### Replace (or extract behind a new immutable specimen)

- Inline **generation** of bundles at every `Sample` call (`BundleField` Worley cells) — this is the primary research rewrite target
- Conflation of “node appearance” (Gaussian bands + coordinate warp) with true 3D vessel paths — keep colour as a derived layer; do not treat it as anatomy
- Absence of a single **bamboo source specimen** object that both `Profile` and `Sample` must read for wall radii, nodes, and bundle layout

Do **not** wholesale replace the stock architecture, GH components, or engineered strip piece model in Phase 1.

---

## Known concerns — verified vs potential

Legend: **Verified** = shown in code and/or probe numbers. **Potential** = research mismatch or modelling gap without a failing test yet.

### 1. `BundleField` and vascular-bundle identity along the culm — **Verified problem**

`BambooMaterialModel` builds sheath/lumen from a 3D cellular hash in coordinates `(arc/spacing, (r−ri)/spacing, w)`, with

- `spacing` a function of wall depth `t` **and** a weak `Perlin(x, …)` term
- `w = Lerp(x/(spacing·36), x/spacing, cross)` so end-grain (`cross≈1`) advances one cell per ~spacing millimetres of axis

Nearest-neighbour cell centres are **not** persistent tubes. End cuts a few millimetres apart therefore show unrelated angular patterns.

**Probe** (`--bamboo-probe`, seed 3, Variation 0, mid-internode, `t≈0.72`):

| Axial separation | End-grain angular luma correlation |
| --- | --- |
| 1 mm | −0.044 |
| 5 mm | 0.053 |
| 20 mm | −0.068 |
| 50 mm | 0.364 |

Persistent axial bundles would stay near **1.0** at small Δx. Cross-section still shows discrete jumps (14 jumps / 96 samples), so end-grain *looks* bundled while failing Larsson-style continuity under arbitrary cuts.

**Implication for Phase 2:** generate a fixed cross-sectional (or sparse 3D) bundle set once per specimen; transport it along the local material frame; sample distance-to-bundle at query points. Cosmetic noise may remain; identity must not.

### 2. Node appearance vs 3D vascular paths — **Verified cosmetic; Potential anatomy gap**

Code combines:

- Gaussian `nodeCore` / `nodeHalo` colour bands (~Peng 12.9 mm axial scale)
- Angular/Perlin distortion of packing and epidermis grain near the node
- Geometry: external ridge + flat diaphragm disks from `Profile()`

Docs already mark node colour as an **appearance adaptation**. There is **no** explicit vessel bending, interconnect, or diaphragm fibre field in the material model—only reoriented sampling coordinates and darker/lighter lerps. Geometry diaphragms are planar plates, not measured nodal vascular networks (Peng / Palombini).

**Keep:** shared node *stations* between geometry and appearance. **Upgrade later:** separate node transport field from paint; optional richer diaphragm mesh.

### 3. `WallAt` sinusoidal thickness — **Verified simplification; Potential anatomy upgrade**

```csharp
WallAt(x) => Wall * (1 + 0.15 * Variation * Sin(…))
```

Variation 0 ⇒ constant wall (probe: 7 / 7 / 7 mm). Variation 1 ⇒ sinusoid (probe: 8.04 / 7.13 / 5.96 mm) with **no** required base→tip thinning. Outer diameter tapers via `Outer`; wall does not follow published *D(z)*, *t(z)* couples (Ghavami, Chaowana, Dixon).

Appropriate as Variation jitter. Insufficient as the sole anatomical wall model for Phase 1.

### 4. `Profile` ridges and diaphragms — **Verified geometry; Potential fidelity**

Outer path uses `Outer+Ridge`; cavity walls reverse between diaphragms; each node adds two radial faces to the axis. Solids, volumes and hollow cuts pass Rhino checks. Diaphragms are zero-thickness face pairs at ±`Diaphragm/2`; ridges are cosine bumps (~7% of radius). Fine for stock solids; not a substitute for nodal µCT structure. Treat geometry and material upgrades separately.

### 5. `BambooConstruction` rectangular strip windows — **Verified approximation (accepted for now)**

Strips are rectangular stock cells rigidly mapped into a cylindrical source at a constant planed radius `rho` (min chord / max inner along the culm). Skin and lining stay outside the window. Tests enforce corners inside the machined wall. Comments already mark flat-chord / no end-joint shortcuts.

**Fit / continuity:** source sampling is continuous within one culm specimen; stagger and 180° turns are seeded. **Machining realism:** chord ≠ true cylindrical face; wall window is constant radius, not a developable strip surface. Improve after persistent culm anatomy exists; do not block Phase 1–2 on curved strip geometry.

### 6. `TryFibreDirection` on bamboo — **Verified missing**

`IMaterialModel` defaults to `false`. `BambooMaterialModel` and `LogMaterialModel` do not override it. `StripModel` forwards to the inner model (still false). Probe: `TryFibreDirection mid-wall: False (0,0,0)`.

`StockDefinition.TraceFibreFlow` only works for a single `TimberPanel` piece, so Preview “Force flow” draws nothing useful for Log/Bamboo/engineered bamboo. Softwood `TimberPanel` already implements fibre diversion around knots.

### 7. Source↔stock direction transforms — **Verified gap for rotated strips**

`MaterialPiece.ToSource` / `ToSourceNormal` apply the piece rotation correctly for positions and cut normals.

`StockDefinition.TryFibreDirection` returns the **source-space** vector unchanged, with a comment that piece frames match stock axes for solid timber. Engineered bamboo pieces use non-identity frames (radial/tangential at `theta`, optional 180° flip). Once bamboo implements fibre direction, stock-space flow **must** apply the inverse rotation (transpose of the orthogonal piece matrix). Section normals for colour already go through `ToSourceNormal`; fibre output is the incomplete path.

Natural bamboo identity pieces (`M = I`) are unaffected until a non-axial fibre field exists near nodes.

---

## Anatomy vs colour (Phase 1 requirement)

Today `NaturalFeatures` mixes geometry radii/nodes with appearance offsets/tint/skin. `BambooMaterialModel` invents bundle structure at shade time. Phase 1 should introduce an immutable **culm specimen** (name TBD: extend `NaturalFeatures` or add a bamboo-specific record) that owns:

1. Centerline + local frame (base→tip X retained unless evidence forces change)
2. `r_o(x)`, `r_i(x)` and wall `t = (r−r_i)/(r_o−r_i)` in millimetres
3. Internode/node stations (already present)
4. Epidermis / lining / parenchyma / bundle **structure** handles (Phase 2 fills bundles)
5. Colour as a separate sampling layer over that structure

Geometry (`Profile`) and material (`Sample`) must read the same radii and nodes. Bundle generation must not be free to disagree with geometry solidity (`Solid`).

## Proposed change order (do not start with a rewrite)

1. **Phase 0 (this document)** — baseline locked; concerns classified.
2. **Phase 1** — unify specimen: wall functions + shared coordinates + testable anatomy helpers; keep current `BundleField` until Phase 2 so GH/bake stay green.
3. **Phase 2** — persistent vascular bundles (Speichinger-style radial stats + transport); implement `TryFibreDirection`; fix stock-space fibre transform for strips.
4. **Then** — node transport field, wall taper presets, strip machining fidelity, Speichinger contact-sheet A/B gates.

## Risk notes

- Appearance commits after `v0.1.5` already changed `BambooMaterialModel`; cache/engine versioning must bump when pixels change again.
- Do not alter component GUIDs or strip product ids (10–12).
- Mac native acceptance remains unverified (project-wide).
- Probe correlation helper lives in `NaturalChecks.ProbeBundles` behind `--bamboo-probe` only; it is not a release gate yet.
