# Bamboo preview at scaffolding length

Date: 2026-11-04. Revised the same day after a review of this note against `PreparedPart`, `NaturalFeatures`, and `NaturalStockSolve`. Status: implementation plan. This note changes no product code.

## Goal

Preview a bamboo scaffolding tower of many poles, about 6 m to 8 m long, inside the existing 200,000 triangle cap. Keep bake on the native solid. Add no package. Keep component GUIDs and port order.

## Problem

Timber Preview meshes each object in `PreparedPart.Create`. Natural Log and Bamboo use a 5° grid angle. The natural maximum edge is `max(10 / unitToMm, tolerance * 20)` document units. `PreparedPart.Create` throws above 200,000 triangles. The message is "Preview is limited to 200,000 triangles per object. Simplify the mesh."

`NaturalFeatures.Profile` builds the revolved profile. When Variation is above 0, each run adds 63 interior stations (`i / 64`). `NaturalGeometry.Create` revolves one NURBS face per run. Default Variation is 0.8, so a default Moso pole includes those stations. Each internode cavity is its own run. A longer culm adds runs and stations.

`NaturalChecks.Component` asserts a 1.5 m Moso preview mesh. The face count must be above 8,000 and at most 200,000. That test does not build a 6 m or 8 m pole.

A reported Grasshopper tower used document metres and Moso defaults (diameter 75 mm, node spacing 270 mm). Posts of 6 m failed preview. Ledgers of 2 m produced Bamboo meshes. This session did not re-solve that file. The station counts below match that split.

The same report cited about 4 GB of Rhino memory and a Cassis timeout on 24 full culms. This session did not measure those figures. Each preview owns a Brep, a mesh, and atlas pages. `TextureAtlas.Prepare` stores three vertices per triangle. No cache shares one pole across components.

`docs/usage.md` caps atlas pages and triangles. It also says there is no maximum physical extent. The triangle throw is a length limit for this mesher. `TextureAtlas.Prepare` lowers density and retries (factor 0.6, at most 12 attempts). The mesh path throws on the first count.

A line of length 6000 in a metre document is 6000 m. `NaturalFeatures` then throws "Taper leaves less than 20% of the base diameter at the tip." That throw happens before any mesh. A real 6 m or 8 m Moso pole stays under the guard. With diameter 75 mm and taper 5 mm per metre, the guard trips only after `800 * Diameter / Taper` millimetres, which is 12 m. Keep the guard. Set Geometry Units when the line was drawn in millimetres. The mesh plan assumes the line length is the real culm length.

## Constraints

`PreviewComponent.SolveInstance` returns the meshes Rhino draws. The solve does not read the camera. A camera move does not rebuild the mesh.

Bake reads the stock Brep. `NativeBakePlan` calls `NativeUvGeometry.Create` with refine on, the natural grid angle, and a 30 mm maximum edge. Bake throws above 200,000 triangles with its own message. The preview mesh stays off that path.

The solid keeps hollow internodes, end rims, and diaphragms. Cuts use that Brep.

Add no dependency and no Grasshopper port. `BambooStockComponent.ComponentGuid` stays `d65cfaa6-137e-4547-91b5-33e5960ea8ca`. Log Stock keeps `5d7f3c1e-8a42-4b6f-9e0d-1c3a7b9f2e64`.

`BambooMaterialModel.Shade` already paints a node core and halo from `NearNode`. The solid still carries a geometric ridge.

## Findings

### Who spends the triangles

| Step | Function | Cost |
| --- | --- | --- |
| Stock solid | `NaturalStockSolve.Run` | Line length times the document scale is the culm length in millimetres. `NaturalGeometry.Create` revolves every `Profile()` run and joins one solid. |
| Profile | `NaturalFeatures.Profile` | Variation above 0 inserts 63 stations on every run. The outer wall is one run, plus nine ridge stations per node. Each internode cavity is another run. |
| Ridge | `PlaceBambooNodes`, `Ridge` | Ridge half-width is `clamp(0.03 * Spacing, 2, 15)` mm. Moso spacing 270 mm gives 8.1 mm. Peak ridge height is about 0.07 times local radius (about 2.6 mm at radius 37.5 mm). |
| Preview mesh | `PreparedPart.MakeMesh` | `RefineGrid` is false when the natural edge cap is positive. `GridAngle` is 5°. `MakeMesh` assigns `MaximumEdgeLength`. `MinimumEdgeLength` stays 0. |
| Display | `PreviewComponent.SolveInstance` | Builds the full mesh, then prepares textures at quality 1. On a cache miss it prepares again at 0.15 and writes those pixels on the solve thread. Full pages refine in the background. |
| Atlas | `TextureAtlas` | Page side is 2048. The page cap is 16. Density drops when packing needs more pages. |
| Bake mesh | `NativeUvGeometry.Create` | `RefineGrid` is true. Grid angle comes from settings (5° for natural). Maximum edge is 30 mm in document units. The triangle cap is 200,000. |

Moso defaults come from `BambooSpecies` and `ResolveDiameter`. The diameter midpoint of 70 mm and 80 mm is 75 mm. Wall is 7 mm. Spacing is 270 mm. Taper is 5 mm per metre. `TimberSettings.Variation` defaults to 0.8.

Segment count is `max(2, round(lengthMm / Spacing))`. Node attempts are one less than the segment count. The constructor rejects more than 100 nodes. At 270 mm spacing that is about 6 segments at 1.5 m, 7 at 2 m, 22 at 6 m, and 30 at 8 m.

Radial wobble amplitude is `Variation * 0.05 * radius`. At Variation 0.8 and radius 37.5 mm the amplitude is about 1.5 mm. The shortest sway wavelength is `0.15 * length` (`b2` in `NaturalFeatures`).

Variation 0 skips the 63-station loop. The live default is 0.8, so the loop runs.

### Mesh rules Rhino already documents

The Rhino 8 Mesh command defines the controls Woodie sets (`docs.mcneel.com`, Mesh):

- Maximum angle, on the initial grid, is the approximate angle between surface normals at the two ends of an edge. Refinement subdivides until the angle is smaller. The control is scale-independent. The suggested range is 5° to 90°. Zero disables it.
- Maximum edge length, on the initial grid, is the approximate maximum quad edge. Refinement splits edges until all are shorter. The control is scale-dependent. Zero disables it.
- Maximum distance, edge to surface, measures an edge midpoint against the surface. Zero disables it. Preview leaves the default at 0.
- Refine mesh subdivides until angle, minimum edge, maximum edge, and edge-to-surface are met. With refine off, the second step does nothing. Untrimmed regions then use evenly sized quads.
- Jagged seams mesh each face alone and skip the stitch. The help text says the result cracks between joined faces.

The first meshing step builds a rectangular grid. Rhino estimates the spacing so the grid meets those controls. The help page never states "one row per knot."

### Count estimate

These counts were not meshed in Rhino. They assume one ring per profile vertex and 72 stations around the culm (360 / 5). One such ring on one face is about 144 triangles.

| Culm | Inner runs × 64 × 144 | Reading |
| --- | --- | --- |
| 1.5 m (about 6 runs) | about 55,000 | Inside the current assert, before the outer wall. |
| 2 m (about 7 runs) | about 65,000 | Consistent with reported ledgers that produced meshes. |
| 6 m (about 22 runs) | about 203,000 | Over the cap before the outer wall. |
| 8 m (about 30 runs) | about 276,000 | Over the cap. |

A smooth tube at 5° and a 20 mm edge, two walls, is about 86,000 triangles at 6 m and about 115,000 at 8 m. Formula: `288 * lengthMm / edgeMm`. In a metre document with tolerance 0.001, the edge cap is already about 20 mm (`max(0.01, 0.001 * 20)`). That smooth count leaves room under 200,000. If the baseline 6 m mesh is over the cap, the extra profile stations are the cause this table predicts. The baseline count decides.

After the profile is coarse, a long span can still split down to the maximum edge. An 8 m tube at 5° and a 10 mm edge is about 230,000 triangles for two walls. A millimetre document uses a 10 mm floor today (`10 / unitToMm` with `unitToMm = 1`). Do not raise that floor for every natural part in the same edit. Phase 1 raises the edge only for a long axis, and only after the re-mesh is still above 150,000 faces. `domain.Inflate` keeps the 10 mm floor.

Diaphragms, end rims, and the outer wall sit on top of the inner-run estimate. Nine ridge stations per node add more outer rings. Those terms were not added into the table. A rough sum that includes them can sit near 200,000 triangles. Treat every number in the table as an unmeasured model.

`PreparedPart.Create` throws before it returns a mesh above 200,000 faces. A test that only calls `Create` cannot record that count. `MakeMesh` is private. The baseline check meshes the Brep with the same parameters `MakeMesh` uses (5°, `RefineGrid` false, `MaximumEdgeLength` set) and prints the face count.

## Options

Ranked by triangles removed per new line of Woodie code.

### 1. Coarser profile stations, then a longer edge only if the count is still high

- Domain: Rhino Mesh command (initial grid, maximum edge, refine off).
- Source: https://docs.mcneel.com/rhino/8/help/en-us/commands/mesh.htm
- Copy: Measure the current 6 m and 8 m meshes first. If they are over the cap, replace the fixed 63 interior stations with equal steps of `min(100 mm, b2 / 8)`. Leave refine off. Leave jagged seams off so joined culm faces still stitch.
- Skip: A global change of the natural edge floor from 10 mm to 20 mm. That floor also meshes logs, cuts, and legacy Breps, and `PreparedPart.Create` inflates the stock-containment box by `maxEdge`. A move to the 1° flat-timber angle. The comment in `PreparedPart.Create` already says that angle explodes natural solids. Keep 5°.
- Where: `NaturalFeatures.Profile` local `Run`. `PreparedPart.Create` only if the re-mesh is still over the margin.
- Risk: `Run` also builds Log Stock. A fixed 100 mm step aliases sway on a short log (`b2` can be `0.15 * length`). The step uses the specimen `b2`. The 2% volume check on a 2.4 m solid will not see a lost radial wobble. Add a radius check.

### 2. Parametric tube mesh for preview

- Domain: HOOPS cylinder LOD, paintstrokes, brepkit, hardware tube tessellation.
- Sources: HOOPS Visualize 3DF cylinders. Neulander and van de Panne, Graphics Interface 1998. brepkit pull request 1017. Nunes et al. 2012, abstract only.
- Copy: Pick sides around the culm and steps along it. Triangle count is then `2 * sides * steps` per wall. brepkit states the same law as `2 * n_u * n_v`. HOOPS default cylinder LODs are 24, 12, 6, 0, and -1 facets. HOOPS LOD changes that facet count only. The HOOPS page says cylinders are never broken lengthwise into pieces.
- Skip: A GPU compute shader, a hull shader, and a per-frame rebuild. Rhino display does not give Woodie those stages. Nunes et al. build the tube in the tessellator so the CPU uploads less. Woodie can upload a short mesh built once during solve. brepkit PR 1017 takes a caller-chosen `TessFactor`. The PR says screen-space LOD is later work on `TessFactor`. Paintstrokes ship no texture mapping in that paper. Woodie needs atlas UVs. Paintstrokes also note that hardware goes faster on a static mesh.
- Where: A helper called from the uncut-stock preview path only. Pass `NaturalFeatures` in. `PreparedPart.Create` still meshes cut pieces and user Breps with `MakeMesh`. `NaturalGeometry.Create` remains the cut and bake solid.
- Risk: `PreparedPart` does not hold `NaturalFeatures` today. A tube built for every `IsNatural` input would replace a cut with a full culm. The helper must emit the outer wall, the inner wall, the end rims, and the diaphragms. It must apply the same ovality scale as `NaturalGeometry.Create`.

### 3. Texture-carried nodes

- Domain: Woodie node paint, and the HOOPS rule that lengthwise splits are a poor LOD knob.
- Source: `BambooMaterialModel.Shade` in `src/Woodie/Stock/NaturalMaterial.cs`. HOOPS cylinder limitations.
- Copy: Keep the preview tube smooth along the internode. Node colour already comes from `NearNode`, `nodeCore`, and `nodeHalo`.
- Skip: Removal of ridge stations from `Profile()`. Bake and a close cut still use the solid bulge (about 2.6 mm on a 75 mm Moso culm).
- Where: The preview builder from option 2. `Shade` stays.
- Risk: The preview silhouette loses that 2.6 mm bulge. Check a close viewport before treating the preview as finished.

### 4. Shared template for repeated poles

- Domain: SpeedTree GPU topics.
- Source: https://docs.speedtree.com/doku.php?id=gpu_topics
- Copy: The page says LOD settings dominate frame rate ("LOD is King"). It also says fewer unique models beat a crowd of unique ones. Effect LOD drops shader work on distant trees. Woodie's match is a coarse mesh first, then the existing 0.15 texture.
- Skip: Billboard atlases, billboard cutouts, and impostor cards. The same page describes those for far trees. A Woodie pole is cut and baked as a solid.
- Where: A cache in front of `PreparedPart.Create` and `TextureAtlas.Prepare`. Store the mesh in the stock frame. Each pole gets a transform. Key the cache with `TimberSettings.Key`, length, `unitToMm`, tolerance, and the axis frame. A cut pole keeps its own mesh.
- Risk: `PreparedPart.Mesh` vertices are in world coordinates today. A key that omits the frame and the units returns a mesh in the wrong place. Copying that world mesh per pole spends the memory again. Grasshopper still solves one component per pole. The cache does not instance the draw. Different seeds do not share.

### 5. Adaptive edge retry

- Domain: Woodie texture packing, and Rhino maximum edge.
- Source: `TextureAtlas.Prepare`. Rhino 8 Mesh command.
- Copy: When the face count is above 200,000, raise `MaximumEdgeLength` and mesh again. Keep 5° until the edge is long. Then step the angle through 10° and 15°, still inside the suggested 5° to 90° range.
- Skip: Maximum distance, edge to surface. That control adds triangles. Preview leaves it at 0.
- Where: `PreparedPart.Create`, after `MakeMesh`.
- Risk: The retry is idle while every profile station forces a ring shorter than the edge cap. Run it after option 1. In that same change, amend `docs/usage.md` so "no maximum physical extent" matches the retry.

### 6. Whole-pole screen-size choice

- Domain: Nanite LOD selection, and the HOOPS facet list.
- Source: Karis, Stubbe, and Wihlidal, SIGGRAPH 2021 Advances slides. HOOPS tessellation list `(24, 12, 6, 0, -1)`.
- Copy: Project geometric error to pixels. Draw the coarser mesh when that error is about one pixel. For a tube the error is the chord sagitta `R * (1 - cos(π / sides))`. At radius 37.5 mm and 24 sides the sagitta is about 0.32 mm.
- Skip: The cluster hierarchy, the visibility buffer, and the software rasterizer. The slides select LOD per cluster, rasterize into a visibility buffer, and software-rasterize clusters whose triangles are shorter than 32 pixels. They also say the pixel-error estimate was the hard part, because the material is unknown at simplify time. A culm chord is an analytic sagitta. Rhino draws the mesh Woodie returns.
- Where: A display conduit, if one exists later. `SolveInstance` has no view. Three stored meshes at 24, 12, and 6 sides match the HOOPS list.
- Risk: A conduit is a new display path. Wait until phase 1 is in the build and a tower is still slow when the camera moves.

## Recommended plan

### Phase 1. One 6–8 m pole under 200,000 triangles

Measure before any edit.

On the current code, build default Moso culms of 6 m and 8 m. Mesh each Brep with the preview parameters: grid angle 5°, `RefineGrid` false, `MaximumEdgeLength` equal to `max(10 / unitToMm, tolerance * 20)`. Print the face count. Do this at `unitToMm` 1 and on the metre-document solid from the test section. `PreparedPart.Create` throws above 200,000 faces, so this count comes from that direct mesh, not from `Create`.

Change `NaturalFeatures.Profile` local `Run` only when the 6 m count is above 200,000.

When Variation is above 0, remove the loop that writes `i / 64`. Keep stations the caller already passed in. The ridge list is that extra set. Set `step = min(100, b2 / 8)` in millimetres. `b2` is the specimen sway length (`length * (0.15 + 0.15 * random)`, so the shortest `b2` is `0.15 * length`). Split the run into `ceil(span / step)` equal intervals. `SortedSet` still drops exact duplicates.

That step is dense on a short log and coarse on a long cavity. A fixed 100 mm step aliases a 1 m log (`b2` can be 150 mm) and adds stations on an 8 m outer wall (about 79 versus today's 63). The extra triangles today come from 63 stations on every short cavity run.

Leave the 10 mm edge floor in place for this edit. Re-mesh the 6 m and 8 m culms. If the 8 m count at `unitToMm` 1 is still above 150,000, raise the preview edge for that part only. Use `max(10 mm, 288 * lengthMm / 150000)` and cap the raise at 20 mm. Apply it when the axis length is above 4 m. Keep `domain.Inflate` on the unscaled floor `max(10 / unitToMm, tolerance * 20)`. A longer edge must not loosen the "Geometry extends outside the mapped Stock Geometry" guard. Keep `RefineGrid` false. Keep the 5° grid angle.

Leave `NativeUvGeometry` on the Brep. Refine stays on. The bake edge stays 30 mm. Print the 8 m bake face count from `NativeUvGeometry.Create`. Do not quote an unmeasured bake total.

Leave GUIDs, ports, and packages unchanged.

Stop when the Phase 1 tests pass. If the 8 m preview count stays above 200,000 after the profile change and the length-scaled edge, move to phase 2. Keep the ridge stations on the solid.

### Phase 2. Parametric tube mesh

Build the preview mesh from `Outer`, `Inner`, `Ridge`, and `Nodes`. Use an explicit side count and axial step. Call it only when the input geometry is the uncut stock Brep and the caller passes `NaturalFeatures`. Cut pieces and user Breps stay on `MakeMesh`. Keep `NaturalGeometry.Create` as the cut and bake solid.

Start at 72 sides and a 20 mm axial step so the picture matches phase 1. The count is then chosen in code.

Emit the inner wall, the end rims, and the diaphragm disks. Apply the ovality scale from `NaturalGeometry.Create`.

### Phase 3. Texture-carried nodes

On the preview tube, omit the nine ridge loops. Leave ridge stations on the Brep. `Shade` already darkens the node band.

### Phase 4. Shared template

Cache one local-frame mesh and one atlas for identical uncut culms. Place each pole with a transform. Key the cache with `TimberSettings.Key`, length, `unitToMm`, tolerance, and the axis frame. Cut geometry stays per object.

### Phase 5. Adaptive edge retry

Mirror the `TextureAtlas.Prepare` density loop inside `PreparedPart.Create`. Raise the edge, then the grid angle (5°, then 10°, then 15°), until the face count is at most 200,000. Stop early if the angle is 15° and the edge equals the radius. If the count is still high, throw the existing preview message.

In that same change, amend `docs/usage.md`. State that a long natural preview may use a coarser edge so the triangle cap holds.

### Leave these unbuilt

Leave Nanite unbuilt. That system needs a visibility buffer, a cluster tree, and a software rasterizer. Woodie draws the mesh `PreviewComponent` returns.

Leave impostor atlases unbuilt. SpeedTree uses them as the far tree LOD. A scaffold pole has to survive cut and native bake.

Leave jagged seams off. The Rhino 8 Mesh help says unstitched faces crack at joins.

Leave per-frame paintstroke tessellation unbuilt until a display conduit exists. The 1998 method tiles the screen projection. The authors note that hardware prefers a static mesh. Their implementation also has no texture maps.

## Test

Record the baseline face counts before the `Profile` edit. Mesh 6 m and 8 m default Moso Breps with the current preview parameters. Print the counts. Change `Run` only when the 6 m count is above 200,000.

Extend `NaturalChecks.Component`. That method runs under `Program.Main --rhino`. Keep the 1.5 m assert (`Faces.Count > 8000` and `Faces.Count <= 200000`).

Millimetre document. Add an 8 m default Moso pole on `BambooStockComponent`, by the same pattern as the 1.5 m pole. Use a line from `(0, 0, 0)` to `(0, 0, 8000)`. The checks document is millimetres, so that line is 8 m. Call `PreparedPart.Create` with `unitToMm` 1 and tolerance 0.001. Print the face count. Assert `Faces.Count <= 150000`. Repeat for seeds 0, 1, and 2, and for Moso, Guadua, and Asper on seed 0. `Create` already throws above 200,000, so an assert of `<= 200000` does not show margin.

Metre document. Do not pass a line of length 8 through `BambooStockComponent` in the millimetre harness. That line is 8 mm, and `PlaceBambooNodes` throws "no room for nodes". Resolve features at length 8,000 mm with the Moso defaults the component would set (`Diameter` 75, `Taper` 5, `WallThickness` 7, `NodeSpacing` 270, `Variation` 0.8). Build the solid with `NaturalGeometry.Create(features, axis from (0,0,0) to (0,0,8), mmToDocument: 0.001, tolerance: 0.001)`. Call `PreparedPart.Create` on that Brep with `unitToMm` 1000 and tolerance 0.001. Print the face count. Assert `Faces.Count <= 150000`.

Radius. On a 1 m Log and a 1.5 m Moso culm, sample the Brep radius between profile stations. Compare it with `Outer(x) + Ridge(x)` within 1 mm. The 2% volume check in `NaturalChecks.Geometry` can pass while that wobble is aliased. `Profile()` feeds both Log Stock and Bamboo Stock.

Taper, in `NaturalChecks.Features`, with no Rhino. `NaturalFeatures` maps a missing taper to 0, so a bare `TimberSettings { Product = Bamboo }` never hits the taper guard. Set `Diameter = 75`, `Taper = 5`, `WallThickness = 7`, `NodeSpacing = 270`, and `Species = Moso`. `Resolve` at 8,000 mm succeeds. `Resolve` at 6,000,000 mm throws `ArgumentException` and the message contains "Taper leaves less than 20%". A call that leaves taper unset throws "more than 100 nodes" instead.

Bake. Call `NativeUvGeometry.Create` on the 8 m Moso solid. Assert the face count is at most 200,000 and print it.

Exit for the code change is `Program.Main --rhino --integration --bake-matrix`. Phase 1 changes the Brep that bake reads.

## Open risks

The triangle tables are estimates. The baseline mesh is the measurement. The Rhino 8 Mesh page does not say that each profile station becomes one ring.

`Run` changes the stock Brep for Log Stock and Bamboo Stock. Ridge stations stay. The radius check is the guard for sway. If that check fails, shorten the step (`b2 / 8` already does this on short stock). Do not fall back to a fixed 50 mm step. A fixed step still aliases the shortest logs.

The length-scaled edge, if it runs, stays at or under 20 mm and does not enter `domain.Inflate`. Node ridges stay on the solid. The bake mesher still refines them. A sentence that says a 20 mm edge skips a 16 mm ridge assumes the mesher ignores stations. The baseline count decides that. Do not keep both claims.

Phase 1 does not share meshes across 24 poles. Memory still grows once per object. The reported 4 GB figure and the Cassis timeout stay unverified.

`PreviewComponent` still builds the preview mesh before the 0.15 texture. Phase 1 makes that mesh smaller. It adds no second geometry pass.

A later tube mesh that omits the cavity looks solid when cut. Use the tube only for the uncut stock. Keep the inner wall and the diaphragms.

Ovality is a Y/Z scale after the revolve. A tube built on world XY must use that same scale. A cache that stores world-space vertices cannot serve poles in different places.

## Sources

Woodie:

- `src/Woodie/Geometry/PreparedPart.cs` — `Create`, `MakeMesh`, the 200,000 triangle throw, the natural edge cap.
- `src/Woodie/Appearance/TimberSettings.cs` — `GridAngle` is 5° for natural stock. `Variation` defaults to 0.8.
- `src/Woodie/Stock/NaturalFeatures.cs` — `Profile`, `Run`, `PlaceBambooNodes`, `Outer`, the taper guard, the 100-node guard.
- `src/Woodie/Geometry/NaturalGeometry.cs` — `Create` revolves one face per run.
- `src/Woodie/Grasshopper/NaturalStockSolve.cs` — line to Brep.
- `src/Woodie/Grasshopper/PreviewComponent.cs` — `SolveInstance`, quality 0.15, background refine.
- `src/Woodie/Rendering/TextureAtlas.cs` — side 2048, 16 pages, density retry, three vertices per triangle.
- `src/Woodie/Stock/NaturalMaterial.cs` — `BambooMaterialModel.Shade` node bands.
- `src/Woodie/Stock/BambooSpecies.cs` — Moso range 70–80 mm, wall 7 mm, spacing 270 mm, taper 5. `ResolveDiameter` returns the midpoint, 75 mm.
- `src/Woodie/Baking/NativeUvGeometry.cs` — bake refine, 30 mm edge, 200,000 triangle throw.
- `src/Woodie/Baking/NativeBakePlan.cs` — bake uses the stock geometry and `settings.GridAngle`.
- `src/Woodie/Geometry/GeometryUnits.cs` — document scale. Declared source units convert into the document.
- `tests/Woodie.Checks/NaturalChecks.cs` — `Component` holds the 1.5 m mesh assert. `Features` is the place for the taper assert.
- `docs/usage.md` — 200,000 triangles, and the sentence "there is no maximum physical extent."
- `docs/development/stock-architecture.md` — natural meshes use a 5° grid angle to stay inside triangle budgets.
- `docs/development/bamboo-appearance-research.md` — node colour is an appearance model on the same stations as the solid.

Primary:

- Rhino 8, Mesh command: https://docs.mcneel.com/rhino/8/help/en-us/commands/mesh.htm
- Karis, Stubbe, and Wihlidal, "Nanite: A Deep Dive," SIGGRAPH 2021 Advances in Real-Time Rendering: https://advances.realtimerendering.com/s2021/Karis_Nanite_SIGGRAPH_Advances_2021_final.pdf
- SpeedTree SDK, GPU Topics: https://docs.speedtree.com/doku.php?id=gpu_topics
- HOOPS Visualize 3DF, Cylinders: https://docs.techsoft3d.com/hoops/visualize-3df/prog_guide/3dgs/02_16_geometry_cylinders.html
- Neulander and van de Panne, "Rendering Generalized Cylinders with Paintstrokes," Graphics Interface 1998: https://www.dgp.toronto.edu/~ivan/research/gi98.pdf and https://graphicsinterface.org/proceedings/gi1998/gi1998-29/
- Nunes, Valdetaro, Raposo, Feijó, and de Toledo, "Rendering Tubes from Discrete Curves Using Hardware Tessellation," Journal of Graphics Tools 16(3), 2012. Abstract and bibliographic entry only: https://doi.org/10.1080/2165347X.2012.659610
- brepkit pull request 1017 (caller-chosen cylinder LOD, merged 2026-06-26): https://github.com/andymai/brepkit/pull/1017

`docs/development/texture-performance-options.md` is absent from the tree. The quick-then-refine path comes from `PreviewComponent` and `TextureAtlas`.
