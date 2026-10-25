# Contributing

Use the build instructions in [README](README.md) and the architecture notes in [development](docs/development.md). Repository-specific agent guidance is in [AGENTS.md](AGENTS.md). Keep changes focused and explain the behaviour and validation in a pull request.

For bug reports, include the Woodie/Rhino versions, operating system, document units, input geometry type and a small shareable definition. Remove private geometry, paths and credentials before attaching files. A screenshot of the affected face and edge is useful for appearance issues.

Preserve component GUIDs, product archive identifiers and old settings/definition compatibility. Keep geometry preparation on the host thread; background rendering must use managed copied data. Change the texture cache namespace whenever pixels or atlas packing change.

Run the managed checks for code changes. For geometry, preview or UI changes, also run the native checks on a licensed Rhino 8 installation as described in [validation](docs/validation.md). Record platform checks that were not run. Never regenerate appearance fixtures merely to make a failing test pass; review the visual change and record its reason.

Do not commit build outputs, caches, local logs, downloaded research images or credentials. Preserve the MIT notices and research attribution. Release preparation is local; package publication is a separate maintainer action.
