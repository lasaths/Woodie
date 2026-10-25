# Native timber baking

Available in **0.1.3**. Headless bake checks and Cassis document proofs pass; a fresh interactive Bake-menu session and Mac checks remain outstanding.

## Bake an object

1. Solve Timber Preview with **Show = true** and the intended final Resolution/Edge Resolution.
2. Right-click the **Timber Preview component**, choose Grasshopper's standard **Bake** action and select the target layer.
3. Wait for final-quality preparation. Escape cancels. If inputs change before preparation finishes, the batch is discarded.
4. Inspect the native objects in Rhino's textured display mode. When saving, enable **Save textures** for a portable `.3dm`.

Viewport preview may be hidden while baking. Show off, cleared data and failed inputs are ineligible. Each solved, valid item creates one native copy, independent of preview atlas-page count. With mixed valid/invalid items, only successfully solved items are eligible. The supplied layer and object attributes are honoured; the timber material is assigned explicitly.

Breps remain joined, trims remain intact and native extrusions remain extrusions. Surface geometry is added using Rhino's normal native surface representation. Mesh vertex positions and face indices are preserved; only texture coordinates are assigned on the duplicate. No preview mesh replaces a native Brep or extrusion.

## Fixed material coordinates

Bake uses an owned snapshot of Geometry, settings, source units, orientation, guide and optional original Stock from the solved item. Native UVs sample the same procedural field as the preview, including knots, custom layers and bent coordinates. Stock fixes the material field but is never baked instead of Geometry.

Baked objects are **independent snapshots**. They need neither Woodie nor the Grasshopper definition for display. Editing their shape, moving a cut or changing buildup settings requires rebaking. Normal object placement transforms are separate from changing the material's shape.

## Quality and safe limits

Baking uses final settings, never quick-preview textures. Unlike interactive preview's bounded triangle pages, each native object uses one bake atlas with gutters. Native Breps/surfaces use Rhino's persistent surface UVs; extrusions use native box mapping; meshes receive charts along existing vertex-index seams. The [feasibility record](development/native-baking-feasibility.md) explains why projecting the preview's triangle atlas was rejected.

The atlas is limited to **8192 × 8192 pixels**. If the requested physical density cannot fit, baking reports the required size and stops rather than reducing quality. Increase Resolution and Edge Resolution (larger mm/pixel values), or simplify the input. Native parameterization can waste texture space, so a part that previews successfully may exceed the bake limit.

A welded closed mesh may not have the independent UV seams required to represent every face continuously. Collapsed or overlapping layouts are rejected. Supply non-overlapping seams upstream if needed; Woodie does not unweld or alter the topology during baking. Native surface/extrusion layouts with ambiguous overlaps are likewise rejected.

All texture preparation completes before document objects are added. A batch uses one undo operation; cancellation or a failed addition removes its new objects, render materials and legacy display-material rows. Content hashes reuse identical PNGs and registered render materials. Different settings or constructions have distinct cache identities.

## Texture files and portability

For a saved model, PNGs are stored in **`<model>_WoodieTextures`** alongside its `.3dm`. An unsaved model uses **`Woodie/BakedTextures`** under the user's local application-data directory. These are durable files, separate from the disposable viewport cache. Saving an initially unsaved model later does not automatically relocate existing textures.

Woodie registers PNGs in Rhino's bitmap table. Enable **Save textures** in Rhino's save options to embed them, following [McNeel's documentation](https://www.wiki.mcneel.com/rhino/save_textures). Woodie does not save the model or change global save preferences. Without embedding, keep the texture folder with the model. Do not delete durable textures while unembedded models reference them.

Windows checks and remaining live/Mac checks are recorded in [validation](validation.md).
