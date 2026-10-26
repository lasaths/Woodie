# Timber playground — Woodie 0.1.4

Open **Timber-preview.ghx** from this `examples/` folder (not an older Yak copy). Everything is in this one self-contained definition: cutting, knot transitions, a custom buildup, the eight-product library, a rotated tower, and a Natural Stock bamboo culm. No external geometry or scripts are required.

## Find your way around

Numbered **Scribble** headings and subtle rectangular groups divide the canvas. Demonstrations sit across the top; the product library is below. Material panels start collapsed: click the small arrow under a Timber Material icon to open its controls. The only large Panel shows the live layer schedule.

Scribbles and the schedule use **Consolas**. If it is unavailable on another machine, Grasshopper may substitute a font; check text spacing there.

The opening view focuses on cutting. Pan right for the knot, buildup, tower and bamboo demonstrations, or down for the product library.

The viewport initially shows only the open demonstrations. Library **Show** toggles start off; enable one product at a time to inspect its eleven cases. Shared Settings, Show and Units wires appear when their inputs are selected. Input geometry stays hidden so it does not overlay the timber textures.

## 01 / Cut through the stock

One 1200 × 300 × 120 mm piece. Move **Position (%)** and **Angle (degrees)**; **Keep Positive** reverses the retained side. Timber Cut caps the exposed interior face. Its original Stock output fixes the material field in Preview, so moving the cut reveals existing grain and knots rather than regenerating them. End positions can retain the whole stock or leave an empty result.

## 02 / Follow a knot

One connected piece with terraces at 120, 80 and 40 mm. Move **Knot section (%)** to cut along its length and expose a capped cross-section, with a section outline. Inspect a knot across the cut face, top terraces and vertical risers. The uncut stepped stock is wired to Preview Stock Geometry; downstream units stay Document. Automatic seed **3** was reviewed for this transition; no individual knots are placed. A hidden original stock anchors the same volumetric field through every depth. This is procedural texturing, not biological growth simulation.

The [Arctic section animation](../docs/images/knot-section-arctic.gif) uses a fixed, low camera facing the stepped end and sweeps the section from 95% to 15% and back. Its 186 frames ease into motion and slow through the knot crossings.

## 03 / Build your own stack

Three Timber Layer components define **30 mm longitudinal / 60 mm cross / 30 mm longitudinal** from bottom to top. They feed Timber Buildup, then Preview. Exact thicknesses are checked by default; **Fit to part** scales proportions explicitly. The live schedule reports layer directions and heights. The Section Diagram output is hidden initially; isolate its preview to inspect the schematic.

## 04 / Product library

Eight product cards each contain a compact Material, a **Show** toggle, source units and eleven labelled geometry cases. Products are Solid timber, KVH, Glulam, CLT, Three-layer panel (3S), Plywood, parallel LVL and Kerto-Q. Each uses its own representative depth with Clean/Pale at **1 mm/pixel**.

| Case | Geometry/mapping |
| --- | --- |
| 01 | Flat solid Brep, matched to the bowed centreline length |
| 02 | Solid extrusion |
| 03 | Open planar surface |
| 04 | Closed mesh |
| 05 | Open mesh |
| 06 | Trimmed face with a hole |
| 07 | Bowed surface, automatic mapping |
| 08 | Bowed single-face Brep, automatic mapping |
| 09 | Bowed solid Brep with guide |
| 10 | Bowed extrusion with guide |
| 11 | Bowed mesh with guide |

## 05 / Rotated tower

Sixteen solid-timber slats between two rings. The top ring is twisted so the generators form a hyperboloid waist. Each slat is item-matched through Preview; fibre follows the long axis.

## 06 / Natural bamboo

**Natural Stock** with Kind=Bamboo builds a hollow Moso culm on a Line from base to tip (Ø 80 · wall 7 · 1500 mm). Natural Stock alone shows the culm solid. Wire Stock Model to Timber Preview Stock when you want texture.

## Units and appearance

The definition works in millimetre, metre and inch Rhino documents. All embedded coordinates are authored in millimetres. Timber Cut declares **Millimetres** and outputs document units, so its downstream Preview uses **Document**. The other previews declare **Millimetres** directly. Do not scale the geometry or change document units to use the example.

Face resolution is **1 mm/pixel** in the library; denser demos may use coarser settings to stay under the preview triangle cap. Smaller values provide finer close views at greater texture cost; allow background refinement to finish. Open surfaces remain open. The guide sets grain coordinates and does not predict physical bending. Custom stacks and presets are visual approximations, not structural specifications; see the [material limitations](https://github.com/lasaths/Woodie/blob/main/docs/material-realism-audit.md).

The appearance is inspired by Maria Larsson et al., [Procedural Texturing of Solid Wood with Knots](https://doi.org/10.1145/3528223.3530081), SIGGRAPH 2022. Credits and MIT notices accompany Woodie.

Bake by right-clicking **Timber Preview → Bake**. This creates native textured Rhino geometry; enable **Save textures** when saving the model. Shape changes require rebaking.
