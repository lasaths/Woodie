# Implementation references

The implementation was checked against McNeel's official documentation:

- [Simple Component](https://developer.rhino3d.com/guides/grasshopper/simple-component/): public classes, stable GUIDs, parameters and solving.
- [Custom Attributes](https://developer.rhino3d.com/guides/grasshopper/custom-attributes/): canvas layout, drawing and mouse hit regions.
- [Simple Data Types](https://developer.rhino3d.com/en/guides/grasshopper/simple-data-types/): GH_Goo, duplication and GH_IO serialization.
- [Custom Component Options](https://developer.rhino3d.com/guides/grasshopper/custom-component-options/): Grasshopper archives.
- [Grasshopper Icons](https://developer.rhino3d.com/guides/grasshopper/grasshopper-icons/): 24 × 24 component icons.
- [Moving to .NET Core](https://developer.rhino3d.com/en/guides/rhinocommon/moving-to-dotnet-core/): cross-platform modern .NET and host-provided dependencies.
- [Yak package anatomy](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/) and [CLI reference](https://developer.rhino3d.com/en/guides/yak/yak-cli-reference/): layout and local build.

Installed SDK XML documentation supplied preview callbacks, parameter access, geometry bounds, isocurve directions, mesh creation, and document lifecycle contracts.

## Modules

`Appearance` contains the procedural wood function, palettes and layups. `Geometry` prepares geometry, bounds, and coordinate maps. `Rendering` packs triangle charts, writes PNGs, and guards refinement generations. `Grasshopper` owns parameters, archives, materials and lifecycle. `Baking` owns solved snapshots, native UV preparation, final texture rasterization and atomic Rhino document commits. See the [native mapping investigation](development/native-baking-feasibility.md) and [bake usage](baking.md).

Rhino geometry operations run during solution preparation on the host thread. Background jobs only use copied triangle coordinates and managed appearance/pixel data. Atlas charts duplicate vertices for independent UV islands while retaining source winding and normals. Surface and guide coordinates are evaluated at vertices and interpolated over the render mesh.

Cache keys include engine version, appearance settings, mapped dimensions and vertices, geometry vertices, connectivity, unit scale and atlas density. Increment the engine version whenever pixels or packing change. Files use unique temporary paths and atomic moves.

The appearance engine uses calibrated palettes and product layups. Thin ThreeS inputs use positive layer thicknesses; calibrated thicker layups are unchanged. Fibre alignment constructs X/Y directly, correcting the reference's antiparallel signed-angle case.

The original research inspiration is Larsson et al., [Procedural Texturing of Solid Wood with Knots](https://doi.org/10.1145/3528223.3530081), ACM TOG 41(4), 2022. Noise follows Perlin, [Improving Noise](https://doi.org/10.1145/566654.566636), ACM TOG 21(3), 2002. Full authorship and the distinction between the research implementation and this C# approximation are documented near the top of the repository README and in the component/package descriptions.

## Icons

Phosphor Icons 2.1.1 supplies `tree-evergreen`, `stack`, and `sliders-horizontal`, bold, `#875D3A`. Component PNGs are 24 px; the Yak icon is 64 px. Regenerate with:

```powershell
node tools/export-icons.mjs <phosphor-icons-checkout>
```

The helper calls the CLI package's styling and PNG export functions with its local assets. Generated resources are committed, so normal builds need neither Node nor the icon repository.

## Material controls and archives

MaterialAttributes draws in canvas coordinates using GH_FontServer fonts. Dropdown lists are rendered inside the component bounds; no Forms dialog, Oasys or Arup assembly is needed. Its only reflection reads the host mouse-button enum, avoiding a Windows Forms target dependency. Interaction records undo and schedules recomputation through GH_Document. Product overrides are resolved through standard item access.

SettingsComponent stores its selections and input expansion before base serialization. Read recreates the correct port count before restoring IDs, sources and persistent values. It intentionally uses the normal component archive format, matching legacy definitions; a variable-parameter archive would lose the old fixed-port data. Legacy ports are retained and the veneer input is appended. SettingsGoo writes version 2 and accepts version 1 as Natural.

The canvas layout pattern is informed by [Arup custom GH attributes](https://github.com/arup-archive/Custom-Grasshopper-UI-Components). Woodie implements its controls independently.

The full material panel has an independent PanelExpanded archive flag (component version 3). Its disclosure triangle stays within the attributes bounds; hidden controls clear their rectangles and dropdown options. SetPanelExpanded records an undo event and marks the document modified while leaving computed material outputs and preview jobs intact. Advanced parameter visibility is independent, so hiding the panel never removes wired ports.
