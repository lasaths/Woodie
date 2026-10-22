# Using Woodie


Connect **Timber Material → Settings** to **Timber Preview → Settings**. Select **Product** and **Appearance** from the two on-canvas dropdowns. Defaults are **CLT / Natural**. The Product input is an optional override: a wire or stored input value takes control and disables the Product dropdown; its controlling source is shown on the component.

**Hide the whole panel:** click the small triangle beneath the component icon. Click it again to restore Product, Appearance and Advanced controls. This preserves all ports, wires, values and solved outputs, and saves with the definition.

The three basic inputs are **Product**, **Seed**, and **Resolution** (mm/pixel). Click **Advanced inputs** for edge resolution, knots, ring spacing, lamella width, brightness, palette colours, and nominal veneer thickness. Effective settings follow **product defaults → appearance defaults → input overrides**. Product and settings lists use normal item matching. **Settings** remains output 0; output 1 gives the canonical product identifier (e.g. GL, ThreeS, ParallelLVL).

Removing the advanced input ports is blocked while they are connected or hold overrides; hiding the whole panel with the triangle is always allowed. Clear those values and wires first. Legacy definitions keep their original input names, indices, GUIDs, wires and stored settings; the old Presets component still loads but is hidden from the ribbon. Dropdowns and expansion state support undo/redo, duplication and save/reopen. The canvas controls follow the custom attributes pattern documented by [McNeel](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/) and demonstrated by [Arup](https://github.com/arup-archive/Custom-Grasshopper-UI-Components), with no Arup/Oasys runtime dependency.

Connect a Mesh, Brep, Surface, or Extrusion to **Geometry**. Settings are optional; the default is CLT. All inputs use normal Grasshopper item matching, so one settings value broadcasts over multiple objects, or each item can receive its own settings, direction, plane, and guide.

| Input | Meaning |
| --- | --- |
| Settings | Product, seed, palette, knots, rings, lamellas and texture resolution |
| Fibre | World-space vector projected into the base plane; reversing it reverses grain X |
| Base Plane | Reference X direction and layer-stacking Z; inferred when omitted |
| Guide Curve | Smooth open centreline for bent timber; guide direction sets grain X |
| Show | Enables preview and texture generation |
| Geometry Units (0.1.2+) | Source coordinate units; defaults to Document. mm/cm/m/inches/feet are accepted, including full unit names |

Outputs are UV-mapped meshes, matching product labels, and one aligned bounding box and frame per input item. The component supplies viewport materials; connecting the mesh output to another preview or baking it does not transfer Woodie's texture materials.

### Source geometry units (0.1.2+)

Rhino geometry carries coordinates, not a reliable independent unit declaration. By default, Woodie interprets them in the active Rhino document units. An imported or embedded millimetre model opened in a metre document can therefore appear 1,000 times too large.

Set **Geometry Units = Millimetres** when the coordinate values are millimetres, even if Rhino uses metres. Woodie scales copies of the geometry, guide curve and Base Plane origin into document units. Fibre vectors retain their direction. The source geometry, guide and Rhino document units remain unchanged. Outputs and bounds are in document coordinates; texture and construction settings remain in millimetres. Do not scale the input manually as well as selecting the source units.

Use Document for normal geometry modelled in the current Rhino document. Explicit values use ordinary item matching and can broadcast or vary per object. Invalid unit names or unset/nonphysical document units produce actionable errors. Woodie does not automatically guess source units or impose a maximum physical extent.

If the appearance budget is exceeded, the message reports mapped dimensions in millimetres, estimated board segments/knots and the source/document units. Check units first; for a correctly sized object, increase Lamella Width, reduce Knot Density or split the part. The allocation limits remain in place.

### Alignment and bending

For straight parts, Woodie finds the largest planar face/mesh region, or an area-weighted principal frame, then aligns to the longest straight outline direction. A Base Plane overrides the inferred plane; Fibre rotates X within it. Bounds enclose the full geometry in the fibre-aligned frame. The output frame begins at the box minimum.

Guide curves take precedence and use arc length with parallel-transport frames. Set Base Plane to control the initial layer orientation; its Z axis is projected perpendicular to the guide's starting tangent. If these are parallel, the plane's Y axis supplies the initial normal. Curves with kinks, cusps, self-intersections, closed ends, or ambiguous inverse mappings are rejected.

A curved single surface can infer bent grain from its U/V directions. Without overrides, the longer centre isocurve is the longitudinal direction. With overrides, the tangent closest to the resolved fibre is used. Lengths are measured along isocurves in physical units rather than raw UV parameters. This is an appearance mapping, not a fabrication unroll; doubly curved or sheared surfaces may distort. Use a guide for consistent bending across multiple faces or arbitrary meshes. Those inputs otherwise use straight mapping and report a remark.

Thin surfaces and planar open meshes remain thin. They use virtual appearance depths: CLT 200 mm, ThreeS 27 mm, Glulam/KVH/Solid timber 120 mm, Kerto-Q/parallel LVL 45 mm, Plywood 18 mm. No solid thickness is generated. Products are visual presets, not structural specifications.

### Products and quality

| Product | Identifier | Construction |
| --- | --- | --- |
| Solid timber | SolidTimber | Continuous solid wood, no manufactured joints |
| KVH | KVH | Finger-jointed solid structural timber |
| Glulam | GL | Parallel timber lamellas with finger joints |
| CLT | CLT | Cross-laminated boards, representative seven-layer appearance |
| Three-layer panel | ThreeS | Longitudinal faces, cross-grain core |
| Plywood | Plywood | Continuous alternating veneers, odd layer count, nominal 1.5 mm |
| LVL — parallel | ParallelLVL | Continuous longitudinal veneers, nominal 3 mm |
| Kerto-Q | KertoQ | Representative cross-banded LVL with scarf joints |

**Natural** uses softwood-oriented colours with restrained board tint and cut-aware grain detail. **Clean/Pale** uses a restrained palette and 0.25 knots/metre; it preserves layup and lamella width. Generic plywood and parallel LVL fit their veneer count to total physical depth. Veneer Thickness overrides the nominal thickness for those two products only. Plywood always has at least three layers and an odd layer count. These are generic visual constructions, not manufacturer-certified specifications. See [material coverage](materials.md) for references and verification.

Species, coatings, OSB/MDF and NLT/DLT are outside this release.

Resolution values are in **mm/pixel**, independent of document units. Smaller values add detail. Atlas pages are capped at 2048 pixels per side and 16 pages per object; density is reduced when needed. Each object is limited to 200,000 triangles; there is no maximum physical extent.

The renderer pads unused triangle-tile space with edge colours to prevent filtered texture seams. Curved surfaces use finer angular tessellation for a smoother silhouette. The first display uses quick textures and refines in the background. Input changes, hiding, disabling Show, clearing, and removal invalidate old work. Textures are cached in the user's local application-data directory under `Woodie/Textures/woodie-8`. Cache files can be deleted while Rhino is closed; they are recreated as needed. On meshes, curved coordinates are evaluated at vertices and interpolated across triangles; refine coarse input meshes for smooth bending.




See [material limitations](material-realism-audit.md) before using a preset as a reference for a particular manufactured product.
