# Woodie contributor guidance

Woodie is a Rhino 8.20+ / .NET 8 Grasshopper plugin. Keep host APIs in geometry preparation and Grasshopper/baking integration; background rendering must use copied managed data.

- Preserve component/parameter GUIDs, input ordering and older GH_IO archives. New optional inputs append to existing ports.
- Material dimensions are millimetres; geometry, guides and plane origins convert together through Geometry Units. Cutting and baking retain original Stock coordinates.
- Bake native geometry, never substitute preview meshes. Mesh UV preparation must retain vertex positions and face indices. Reject unsafe layouts or oversized bake atlases rather than silently lowering quality.
- Keep the Larsson research credit and MIT/Phosphor notices in public descriptions and packages.

## Checks and delivery

Build `tests/Woodie.Checks` in Release before `src/Woodie/Woodie.csproj`: the checks project references the plugin as a DLL. Verify build exit codes before running the runner. Run `--rhino --integration` for native changes and `--bake-matrix` for baking or material changes. Tests need a licensed Windows Rhino; report Mac checks separately.

See [development](docs/development.md) for module/API notes and [packaging](docs/packaging.md) for Yak validation. Keep investigation notes in `docs/development/`; generated logs, captures, scratch definitions and models belong in ignored `artifacts/`. Runtime Yak packages contain no full documentation or agent instructions and ship only the single combined example.

Before replacing an installed GHA, wait for the user to close Rhino; wait for their confirmation before resuming live tests. Publish only when requested and after the installed build is verified. Test the Yak package against the test server before production publication.
