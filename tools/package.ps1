[CmdletBinding()]
param(
    [string]$YakPath,
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'verify-release.ps1') -Root $repo
if (-not $YakPath) {
    $candidates = @(
        (Join-Path $repo 'artifacts/tools/yak.exe'),
        (Join-Path $repo 'artifacts/tools/yak'),
        'C:/Program Files/Rhino 8/System/Yak.exe',
        '/Applications/Rhino 8.app/Contents/Resources/bin/yak'
    )
    $YakPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $YakPath) { throw 'Yak CLI not found. Pass -YakPath; see docs/packaging.md.' }
$YakPath = (Resolve-Path -LiteralPath $YakPath).Path
& $DotnetPath build (Join-Path $repo 'src/Woodie/Woodie.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $repo 'src/Woodie/Woodie.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$release = Join-Path $repo 'artifacts/release'
$stage = Join-Path $repo ('artifacts/staging/' + [Guid]::NewGuid().ToString('N'))
$yakStage = Join-Path $stage 'yak'
$sourceStage = Join-Path $stage 'source'
New-Item -ItemType Directory -Path $release, (Join-Path $yakStage 'net7.0'), $sourceStage -Force | Out-Null

foreach ($name in @('manifest.yml', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $repo "packaging/$name") -Destination $yakStage
}
$gha = Join-Path $repo 'src/Woodie/bin/Release/net7.0/Woodie.gha'
$binary = [IO.File]::ReadAllBytes($gha)
if ([Text.Encoding]::ASCII.GetString($binary) -match $privatePath -or [Text.Encoding]::Unicode.GetString($binary) -match $privatePath) { throw 'Private machine path embedded in the GHA.' }
Copy-Item -LiteralPath $gha -Destination (Join-Path $yakStage 'net7.0')
foreach ($name in @('README.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'docs', 'examples')) {
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $yakStage -Recurse
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $sourceStage -Recurse
}
foreach ($name in @('.gitignore', '.gitattributes', '.editorconfig', 'Woodie.sln', 'NuGet.Config', '.github', 'packaging', 'tools')) {
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $sourceStage -Recurse
}
# Enumerate only redistributable source; never recurse into caches or build outputs.
foreach ($folder in @('src', 'tests')) {
    foreach ($file in Get-PublicFiles (Join-Path $repo $folder)) {
        $relative = [IO.Path]::GetRelativePath($repo, $file.FullName)
        $destination = Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
& (Join-Path $PSScriptRoot 'verify-release.ps1') -Root $sourceStage
Add-Type -AssemblyName System.IO.Compression.FileSystem
Push-Location $yakStage
try {
    & $YakPath build --platform any
    if ($LASTEXITCODE -ne 0) { throw 'Yak validation/build failed.' }
    $packages = @(Get-ChildItem -LiteralPath $yakStage -Filter 'woodie-*.yak')
    if ($packages.Count -ne 1 -or $packages[0].Name -notmatch '^woodie-.+-rh8(?:_\d+)?-any\.yak$') {
        throw 'Expected exactly one Rhino 8 cross-platform package.'
    }
    $package = $packages[0]
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($required in @('manifest.yml', 'icon.png', 'net7.0/Woodie.gha', 'README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'examples/Timber-preview.ghx')) {
            if ($required -notin $names) { throw "Missing package content: $required" }
        }
        $binaries = @($names | Where-Object { $_ -match '(?i)\.(dll|gha|rhp|exe|pdb)$' })
        if ($binaries.Count -ne 1 -or $binaries[0] -ne 'net7.0/Woodie.gha') { throw 'Package may contain only the Woodie GHA binary.' }
        if (@($names | Where-Object { $_ -match '(?i)\.ghx?$' }).Count -ne 1) { throw 'Exactly one Grasshopper example is required.' }
        if (@($names | Where-Object { $_ -match '(?i)(^|/)(bin|obj|artifacts|\.git)/|\.(log|tmp|cs|csproj|ps1|mjs)$' }).Count) { throw 'Development files leaked into Yak.' }
        foreach ($credited in @('manifest.yml', 'README.md')) {
            $reader = [IO.StreamReader]::new($archive.GetEntry($credited).Open())
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if (-not $text.Contains('10.1145/3528223.3530081')) { throw "Original research credit missing from $credited" }
            if ($credited -eq 'manifest.yml' -and $text -notmatch ('(?m)^version:\s*' + [regex]::Escape($version) + '\s*$')) { throw 'Yak version does not match source.' }
        }
        $stream = $archive.GetEntry('net7.0/Woodie.gha').Open()
        try { $sha = [Security.Cryptography.SHA256]::Create(); $packedHash = [Convert]::ToHexString($sha.ComputeHash($stream)); $sha.Dispose() } finally { $stream.Dispose() }
        if ($packedHash -ne (Get-FileHash -LiteralPath $gha -Algorithm SHA256).Hash) { throw 'Packaged GHA differs from the Release build.' }
    } finally { $archive.Dispose() }
    $packageDestination = Join-Path $release $package.Name
    Copy-Item -LiteralPath $package.FullName -Destination $packageDestination -Force
} finally { Pop-Location }

$sourceName = "woodie-$version-source.zip"
$sourceTemp = Join-Path $stage $sourceName
[IO.Compression.ZipFile]::CreateFromDirectory($sourceStage, $sourceTemp, [IO.Compression.CompressionLevel]::Optimal, $false)
$sourceDestination = Join-Path $release $sourceName
Copy-Item -LiteralPath $sourceTemp -Destination $sourceDestination -Force
$lines = foreach ($path in @($packageDestination, $sourceDestination)) {
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($path))"
}
[IO.File]::WriteAllLines((Join-Path $release "woodie-$version-SHA256SUMS.txt"), $lines, [Text.UTF8Encoding]::new($false))
Write-Output "Validated local Yak: $packageDestination"
Write-Output "Curated source ZIP: $sourceDestination"
Write-Output "SHA-256 checksums: artifacts/release/woodie-$version-SHA256SUMS.txt"
