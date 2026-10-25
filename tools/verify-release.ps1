[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '..'))
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)

function Get-PublicFiles([string]$Directory) {
    foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -notin @('.git', 'artifacts', 'bin', 'obj', '.vs', '.vscode')) {
                Get-PublicFiles $item.FullName
            }
        } else { $item }
    }
}

$files = @(Get-PublicFiles $Root)
$examples = @($files | Where-Object { $_.Extension -in @('.gh', '.ghx') })
if ($examples.Count -ne 1 -or $examples[0].Name -ne 'Timber-preview.ghx') {
    throw 'Public source must contain only the combined Timber-preview.ghx example.'
}
foreach ($required in @('README.md', 'AGENTS.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'examples/Timber-preview.ghx')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $required) -PathType Leaf)) { throw "Missing $required" }
}
# Patterns are assembled to avoid flagging this scanner's own source.
$privatePath = '[A-Za-z]:[\\/]' + '(?:Users|Documents and Settings)[\\/]|' + '/Us' + 'ers/[^/\s]+/|' + '/ho' + 'me/[^/\s]+/'
$internalName = '(?i)' + ('AB' + 'xM|Plate' + 'Structures|Element' + 'Base')
$secret = '(?:gh[pousr]_' + '[A-Za-z0-9]{30,}|github_pat_' + '[A-Za-z0-9_]{30,}|AKIA' + '[A-Z0-9]{16}|-----BEGIN ' + '(?:RSA |EC |OPENSSH )?PRIVATE KEY-----)'
$textExtensions = @('.md', '.cs', '.csproj', '.sln', '.ps1', '.mjs', '.json', '.config', '.txt', '.yml', '.yaml', '.svg', '.ghx')
foreach ($file in $files) {
    if ($file.Extension -in @('.log', '.tmp', '.pdb', '.dll', '.gha', '.yak', '.zip') -or $file.Name -like '.env*') {
        throw "Unexpected public source artifact: $($file.FullName)"
    }
    if ($file.Extension -notin $textExtensions -and $file.Name -notin @('LICENSE', '.gitignore', '.gitattributes', '.editorconfig')) { continue }
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match $privatePath) { throw "Private machine path in $($file.FullName)" }
    if ($text -match $internalName) { throw "Internal project reference in $($file.FullName)" }
    if ($text -match $secret) { throw "Possible credential in $($file.FullName)" }
    if ($file.Extension -eq '.md') {
        foreach ($match in [regex]::Matches($text, '\[[^\]]*\]\(([^)]+)\)')) {
            $link = $match.Groups[1].Value.Trim('<', '>')
            if ($link -match '^(?:[a-zA-Z][a-zA-Z0-9+.-]*:|#)') { continue }
            $link = [Uri]::UnescapeDataString(($link -split '#')[0])
            if ($link -and -not (Test-Path -LiteralPath (Join-Path $file.DirectoryName $link))) {
                throw "Broken local Markdown link in $($file.FullName): $link"
            }
        }
    }
}
foreach ($name in @('README.md', 'packaging/manifest.yml', 'src/Woodie/Grasshopper/ResearchCredit.cs')) {
    if (-not [IO.File]::ReadAllText((Join-Path $Root $name)).Contains('10.1145/3528223.3530081')) {
        throw "Original research DOI missing from $name"
    }
}
$notice = [IO.File]::ReadAllText((Join-Path $Root 'THIRD-PARTY-NOTICES.md'))
if (-not $notice.Contains('University of Stuttgart') -or -not $notice.Contains('Phosphor Icons')) { throw 'Required copyright notices missing.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $Root 'src/Woodie/Woodie.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if (-not $version -or -not [IO.File]::ReadAllText((Join-Path $Root 'CHANGELOG.md')).Contains($version)) { throw 'Project version has no changelog entry.' }
Write-Output "Public content checks passed: $($files.Count) files; one combined example; version $version."
