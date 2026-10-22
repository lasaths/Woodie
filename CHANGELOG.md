# Changelog

## 0.1.2 — 2026-10-22

- Slim runtime Yak: retain the GHA, manifest/icon, short README, licences and complete gallery; keep full documentation in GitHub/source ZIP.
- Expand the example to eight products × eleven geometry cases, with matched flat/bowed pairs and product-appropriate depths.
- Add optional Geometry Units source-unit conversion for geometry, guides and plane origins; ordinary inputs retain Document units.
- Make the gallery unit-aware so it solves in millimetre and metre Rhino documents.
- Report physical dimensions and source/document units in appearance-budget errors.
- Preserve older six-input Preview archives and existing component GUIDs.


## 0.1.1 — 2026-10-22

- Target .NET 8 and require Rhino 8.20+; Yak distributions use the Rhino 8.20 compatibility tag.
- Recommend installation through Rhino PackageManager.
- Explain procedural mapping and cite Larsson et al. in the README introduction.
- Document both components, their inputs, outputs, dropdowns and advanced settings.

## 0.1.0 — 2026-10-21

Initial release.

- Standalone Rhino 8 timber preview for meshes, Breps, surfaces and extrusions.
- Light Three-layer panel adhesive colour avoids black longitudinal seam streaks on face grain.
- Eight product presets with Natural and Clean/Pale appearances, restrained board tint and reduced orange/brown shadow amplification.
- Combined Timber Material dropdown controls, optional advanced inputs and a collapsible panel.
- Fibre-aligned bounds, automatic single-surface mapping and guide-driven bent grain.
- Cut-aware face and end-grain detail, managed PNG textures, bounded atlases and cancellable refinement.
- Stable component GUIDs and migration of earlier settings archives and definitions.
- One self-contained, labelled flat/bowed Grasshopper example.
- Curated Yak/source archives, licence notices, release validation and material limitations.
- Correct exterior glue shading on nearly planar and transformed faces; real internal joints are retained.
- All-product exterior-continuity and rotated metre-scale atlas regression checks; corrected textures use cache namespace `woodie-8`.

Windows checks are documented in [validation](docs/validation.md). Mac native acceptance remains pending. The presets are visual approximations; see [material limitations](docs/material-realism-audit.md).
