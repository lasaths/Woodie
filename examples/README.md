# Complete timber gallery — Woodie 0.1.2+

Open **Timber-preview.ghx** with Woodie 0.1.2 or later. It contains **88 cases: eight timber products × eleven geometry types**.
No external geometry or scripts are required.

## Units

Every preview is wired to **Geometry Units = Millimetres**. The plugin converts copies of the embedded geometry, guide and plane origins into the current Rhino document units.
The same file therefore works in millimetre, metre and inch documents. Do not change Rhino units or manually scale the embedded geometry to use this gallery.

For ordinary user geometry, Geometry Units defaults to **Document**. If the coordinate values were modelled in millimetres but Rhino uses metres, choose **Millimetres** explicitly.
Woodie cannot infer the intended physical units from the shape alone and does not guess or rescale ordinary inputs automatically.

## Layout

Each coloured canvas group contains one product's Timber Material component and eleven previews. The viewport uses one row per product.
Flat and guide-bowed solid pairs are visible initially and have equal centreline lengths. The remaining cases are included with preview hidden to keep initial refinement manageable; enable those preview components or the group's preview to explore them.

All eight products are present: Solid timber, KVH, Glulam, CLT, Three-layer panel (3S), Plywood, parallel LVL and Kerto-Q.
Each uses its product's representative depth rather than forcing all products into a 27 mm board.
Clean/Pale and 8 mm/pixel are saved for a restrained, lighter initial gallery; adjust each material's dropdowns and resolution to compare appearances.

| Case | Geometry/mapping |
| --- | --- |
| 01 | Flat solid Brep, matching the bowed centreline length |
| 02 | Straight solid extrusion |
| 03 | Flat open surface |
| 04 | Closed mesh |
| 05 | Open planar mesh |
| 06 | Trimmed planar face with a hole |
| 07 | Automatically mapped bowed surface |
| 08 | Automatically mapped bowed single-face Brep |
| 09 | Guide-driven bowed solid Brep |
| 10 | Guide-driven bowed extrusion |
| 11 | Guide-driven bowed mesh |

The guide defines grain coordinates; it does not predict physical bending. Open surfaces and meshes stay open. Nothing is baked into Rhino.
Changing a preset does not turn geometry into a manufactured product; see [material limitations](https://github.com/lasaths/Woodie/blob/main/docs/material-realism-audit.md).
