param(
    [string]$WorkspaceRoot = "",
    [string]$ArtifactRoot = "",
    [switch]$DryRun
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Split-Path -Parent $PSScriptRoot
}

$workspace = (Resolve-Path -LiteralPath $WorkspaceRoot).Path
if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $workspace ("_local\cleanup-" + (Get-Date).ToString("yyyyMMdd-HHmmss"))
}
elseif (-not [System.IO.Path]::IsPathRooted($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $workspace $ArtifactRoot
}

$artifact = $ArtifactRoot
$workspaceFull = [System.IO.Path]::GetFullPath($workspace)
$artifactFull = [System.IO.Path]::GetFullPath($artifact)
if (-not $artifactFull.StartsWith($workspaceFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Cleanup target must stay inside the workspace. Target was: $artifactFull"
}

$keepNames = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    ".gitattributes",
    ".gitignore",
    "README.md",
    "RELEASE.md",
    "spyro-level-catalog.json",
    "spyro-object-templates.json",
    "sample-artisans.splevel.json"
)) {
    [void]$keepNames.Add($name)
}

$keepRegexes = @(
    "^[a-z0-9]+-moby-user-overrides\.json$",
    "^[a-z0-9]+-native-edits\.json$",
    "^[a-z0-9]+-terrain-edits\.json$",
    "^[a-z0-9]+-terrain-material-overrides\.json$",
    "^[a-z0-9]+-live-validation-overrides\.json$"
)

$gameAndCaptureExtensions = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
foreach ($ext in @(
    ".bin", ".cue", ".iso", ".img", ".chd", ".ecm", ".m3u", ".ccd", ".sub", ".toc",
    ".sav", ".srm", ".mcr", ".mcd", ".dmp", ".dump", ".state",
    ".png", ".bmp", ".jpg", ".jpeg", ".tga", ".webp", ".csv"
)) {
    [void]$gameAndCaptureExtensions.Add($ext)
}

$generatedRegexes = @(
    "\.patchplan\.json$",
    "\.planonly\.json$",
    "patchplan.*\.json$",
    "loaderpatch.*\.json$",
    "nativepatch.*\.json$",
    "^current-level-",
    "^object-add-lab-",
    "-runtime-.*\.json$",
    "runtime-scene.*\.json$",
    "runtime-moby.*\.json$",
    "source-donor-map\.(json|md)$",
    "capture-summary\.(json|md)$",
    "dependency-trace\.(json|md)$",
    "object-bundle-audit\.(json|md)$",
    "actor-package-ram-hits.*\.json$",
    "color-(now|probe)-.*\.(json|md)$",
    "^Spyro the Dragon \(USA\)-",
    "^Apply .* Live\.bat$",
    "^Revert .* Live\.bat$",
    "\.bat$"
)

$generatedDirs = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    "actor-texture-atlases",
    "color-research-snapshots",
    "unsafe-tests"
)) {
    [void]$generatedDirs.Add($name)
}

$trackedFiles = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
try {
    $gitOutput = & git -C $workspaceFull ls-files
    foreach ($line in @($gitOutput)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        [void]$trackedFiles.Add(($line -replace "/", "\"))
    }
}
catch {
    Write-Warning "Could not query git tracked files; cleanup will continue with name-based exclusions only."
}

function Get-WorkspaceRelativePath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($workspaceFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $full
    }
    return $full.Substring($workspaceFull.Length).TrimStart("\", "/")
}

function Test-IsTrackedPath([string]$Path) {
    $relative = Get-WorkspaceRelativePath $Path
    return $trackedFiles.Contains($relative)
}

function Test-IsGitIgnoredPath([string]$Path) {
    $relative = Get-WorkspaceRelativePath $Path
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative -eq $Path) { return $false }
    & git -C $workspaceFull check-ignore --quiet -- $relative
    return $LASTEXITCODE -eq 0
}

function Test-IsKeptFile([System.IO.FileInfo]$File) {
    if ($keepNames.Contains($File.Name)) { return $true }
    if (Test-IsTrackedPath $File.FullName) { return $true }
    foreach ($regex in $keepRegexes) {
        if ($File.Name -match $regex) { return $true }
    }
    return $false
}

function Test-IsGeneratedFile([System.IO.FileInfo]$File) {
    if (Test-IsKeptFile $File) { return $false }
    if (Test-IsGitIgnoredPath $File.FullName) { return $true }
    if ($gameAndCaptureExtensions.Contains($File.Extension)) { return $true }
    foreach ($regex in $generatedRegexes) {
        if ($File.Name -match $regex) { return $true }
    }
    return $false
}

function Get-UniqueDestination([string]$DestinationPath) {
    if (-not (Test-Path -LiteralPath $DestinationPath)) { return $DestinationPath }
    $dir = Split-Path -Parent $DestinationPath
    $leaf = Split-Path -Leaf $DestinationPath
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($leaf)
    $ext = [System.IO.Path]::GetExtension($leaf)
    for ($i = 1; $i -lt 10000; $i++) {
        $candidate = Join-Path $dir ("{0}-{1}{2}" -f $stem, $i, $ext)
        if (-not (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    throw "Could not find a unique destination for $DestinationPath"
}

function Move-ToArtifact([string]$SourcePath, [string]$Bucket) {
    $sourceFull = [System.IO.Path]::GetFullPath($SourcePath)
    if (-not $sourceFull.StartsWith($workspaceFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to move a path outside the workspace: $sourceFull"
    }
    if ($sourceFull.StartsWith($artifactFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    $targetDir = Join-Path $artifactFull $Bucket
    $targetPath = Get-UniqueDestination (Join-Path $targetDir (Split-Path -Leaf $SourcePath))
    if (-not $DryRun) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
        Move-Item -LiteralPath $SourcePath -Destination $targetPath
    }
    return [pscustomobject]@{
        source = $SourcePath
        destination = $targetPath
        bucket = $Bucket
    }
}

$moves = New-Object System.Collections.ArrayList
foreach ($file in Get-ChildItem -LiteralPath $workspaceFull -Force -File) {
    if (Test-IsGeneratedFile $file) {
        $bucket = if ($gameAndCaptureExtensions.Contains($file.Extension)) { "game-and-capture-artifacts" } else { "generated-research" }
        $move = Move-ToArtifact $file.FullName $bucket
        if ($null -ne $move) { [void]$moves.Add($move) }
    }
}

foreach ($dir in Get-ChildItem -LiteralPath $workspaceFull -Force -Directory) {
    if ($dir.Name -eq ".git" -or $dir.Name -eq "_local" -or $dir.Name -eq "native" -or $dir.Name -eq "tools") {
        continue
    }
    if (Test-IsTrackedPath $dir.FullName) {
        continue
    }
    if ($generatedDirs.Contains($dir.Name) -or (Test-IsGitIgnoredPath $dir.FullName)) {
        $move = Move-ToArtifact $dir.FullName "generated-research"
        if ($null -ne $move) { [void]$moves.Add($move) }
    }
}

if (-not $DryRun -and $moves.Count -gt 0) {
    $manifestPath = Join-Path $artifactFull "cleanup-manifest.json"
    [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        workspace = $workspaceFull
        artifactRoot = $artifactFull
        movedCount = $moves.Count
        moved = @($moves.ToArray())
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

$totalBytes = 0L
foreach ($move in @($moves.ToArray())) {
    if (Test-Path -LiteralPath $move.destination -PathType Leaf) {
        $totalBytes += (Get-Item -LiteralPath $move.destination).Length
    }
}

[pscustomobject]@{
    dryRun = [bool]$DryRun
    workspace = $workspaceFull
    artifactRoot = $artifactFull
    movedCount = $moves.Count
    movedMB = [Math]::Round($totalBytes / 1MB, 1)
}
