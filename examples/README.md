# Timber preview example

Open **Timber-preview.ghx** in Grasshopper with a **millimetre Rhino document** and Woodie installed.

The definition contains all geometry internally and demonstrates **Timber Material → Timber Preview** with a flat board and an upward-bowed board. Both have the same centreline length and a 500 × 27 mm section. The bow has a 1800 mm chord and 220 mm rise. Three-layer panel / Clean/Pale is saved as the initial appearance.

Change the material dropdowns to explore the eight visual products. The geometry stays 27 mm thick; this does not turn it into a manufacturer's standard section for every product. For instance, the Kerto-Q visual recipe is based on its 45 mm layup. See [material limitations](../docs/material-realism-audit.md).

Input previews are hidden so the textures remain visible. The guide controls grain mapping; it does not predict physical bending. Nothing is baked into Rhino and no external file or script is required.
