# Preparing a release

Woodie follows McNeel's [package anatomy](https://developer.rhino3d.com/en/guides/yak/the-anatomy-of-a-package/), [manifest](https://developer.rhino3d.com/en/guides/yak/the-package-manifest/) and [CLI reference](https://developer.rhino3d.com/en/guides/yak/yak-cli-reference/).

## Build locally

Use PowerShell 7 with the .NET 8 SDK or newer. Rhino supplies the Yak CLI:

```powershell
pwsh ./tools/package.ps1
# Explicit CLI locations:
pwsh ./tools/package.ps1 -YakPath 'C:/Program Files/Rhino 8/System/Yak.exe'
pwsh ./tools/package.ps1 -YakPath '/Applications/Rhino 8.app/Contents/Resources/bin/yak'
```

The script builds Release, stages an explicit set of distributable files and invokes `yak build --platform any`. It validates the archive before copying it to `artifacts/release/`. Each invocation uses a fresh staging directory; old packages are not used as input.

The package contains `net7.0/Woodie.gha`, manifest, icon, public documentation, licences and exactly one definition: `examples/Timber-preview.ghx`. Host SDKs, runtime dependencies supplied by Rhino, source, debug symbols, logs, caches and development galleries are excluded.

A separate `woodie-<version>-source.zip` contains the curated GitHub source tree, tests and that same example, without local artifacts or Git metadata. SHA-256 checksums accompany both archives. The version comes from `src/Woodie/Woodie.csproj`; the Yak manifest's `$version` placeholder is resolved by Yak.

## Verify

Run the [validation commands](validation.md) before packaging. From an extracted source ZIP, build the checks runner and GHA again to confirm that the source distribution is independent of the original checkout.

`tools/verify-release.ps1` checks private paths, common secret formats, internal project references, local Markdown links, example count and research attribution. It is an additional content check, not a guarantee that every possible sensitive string is detected. The package script also rejects any binary other than Woodie's GHA and verifies that its hash matches the Release build.

Native Mac acceptance and documented material refinements remain outstanding; release numbering does not change that testing scope. The `any` platform tag expresses intended compatibility, not proof of Mac testing.

## Publication

Review the generated archives, checksum file, [changelog](../CHANGELOG.md), [material limitations](material-realism-audit.md) and [validation report](validation.md). The public repository is [lasaths/Woodie](https://github.com/lasaths/Woodie).

For GitHub, push the curated repository to the chosen remote and create a release using the version tag and changelog. Attach the Yak, source ZIP and checksums from `artifacts/release/`. Add the confirmed public repository URL to the Yak manifest before publishing it if available.

For Yak, authenticate with the intended account and follow McNeel's explicit push workflow. The scripts do not log in, create repositories, install components, push commits or publish packages.
