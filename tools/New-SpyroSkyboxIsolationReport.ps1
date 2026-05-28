param(
    [string]$TargetLevelKey = "StoneHill",
    [string]$DonorLevelKey = "CrystalFlight",
    [string]$CatalogPath = ".\spyro-skybox-catalog.json",
    [string]$OutJsonPath = "",
    [string]$OutMarkdownPath = ""
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
}

function Get-JsonProperty($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Find-Level($Catalog, [string]$Key) {
    $wanted = Normalize-LevelKey $Key
    foreach ($level in @($Catalog.levels)) {
        $matches = @(
            (Normalize-LevelKey ([string](Get-JsonProperty $level "key"))),
            (Normalize-LevelKey ([string](Get-JsonProperty $level "scriptKey"))),
            (Normalize-LevelKey ([string](Get-JsonProperty $level "displayName")))
        )
        if ($matches -contains $wanted) { return $level }
    }
    throw "No cataloged skybox level matches '$Key'."
}

function Add-MarkdownRow($Lines, $Columns) {
    [void]$Lines.Add("| " + (($Columns | ForEach-Object { ([string]$_).Replace("|", "\|") }) -join " | ") + " |")
}

function New-Segment($Key, $Description, [int]$Start, [int]$Length, [string]$Risk) {
    [ordered]@{
        key = $Key
        description = $Description
        start = $Start
        length = $Length
        endExclusive = $Start + $Length
        risk = $Risk
    }
}

$resolvedCatalogPath = Resolve-WorkspacePath $CatalogPath
if (-not (Test-Path -LiteralPath $resolvedCatalogPath)) {
    throw "Missing skybox catalog: $resolvedCatalogPath. Run tools\New-SpyroSkyboxCatalog.ps1 first."
}

$catalog = Get-Content -LiteralPath $resolvedCatalogPath -Raw | ConvertFrom-Json
$target = Find-Level $catalog $TargetLevelKey
$donor = Find-Level $catalog $DonorLevelKey
$targetName = [string](Get-JsonProperty $target "displayName")
$donorName = [string](Get-JsonProperty $donor "displayName")
$targetScriptKey = [string](Get-JsonProperty $target "scriptKey")
$donorScriptKey = [string](Get-JsonProperty $donor "scriptKey")
$targetSize = [int](Get-JsonProperty $target "skySubfileSize")
$donorSize = [int](Get-JsonProperty $donor "skySubfileSize")
$maxProbeLength = [Math]::Min($targetSize, $donorSize)

if ([string]::IsNullOrWhiteSpace($OutJsonPath)) {
    $OutJsonPath = ".\_skybox_probe\skybox-isolation-$((Normalize-LevelKey $targetName))-from-$((Normalize-LevelKey $donorName)).json"
}
if ([string]::IsNullOrWhiteSpace($OutMarkdownPath)) {
    $OutMarkdownPath = ".\_skybox_probe\skybox-isolation-$((Normalize-LevelKey $targetName))-from-$((Normalize-LevelKey $donorName)).md"
}
$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
$outDir = Split-Path -Parent $resolvedOutJsonPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    [void][IO.Directory]::CreateDirectory($outDir)
}

$segments = New-Object System.Collections.ArrayList
if ($maxProbeLength -ge 0x80) {
    [void]$segments.Add((New-Segment "header-bounds" "Small leading header/bounds/control candidate." 0 0x80 "medium"))
}
if ($maxProbeLength -gt 0x80) {
    $end = [Math]::Min(0x255C, $maxProbeLength)
    [void]$segments.Add((New-Segment "early-control" "Early control/table region from the previous packed-line split." 0x80 ($end - 0x80) "medium-high"))
}
if ($maxProbeLength -gt 0x255C) {
    $end = [Math]::Min(0xB800, $maxProbeLength)
    [void]$segments.Add((New-Segment "middle-packed-records" "Middle packed-record region; likely terrain/material risk." 0x255C ($end - 0x255C) "high"))
}
if ($maxProbeLength -gt 0xB800) {
    [void]$segments.Add((New-Segment "tail-records" "Tail region after the known terrain-probe split; a useful first sky candidate if earlier ranges break terrain." 0xB800 ($maxProbeLength - 0xB800) "medium-high"))
}

$chunkSize = 0x4000
for ($start = 0; $start -lt $maxProbeLength; $start += $chunkSize) {
    $length = [Math]::Min($chunkSize, $maxProbeLength - $start)
    [void]$segments.Add((New-Segment ("chunk-0x{0:X}" -f $start) "Uniform 0x4000-byte chunk probe for binary search." $start $length "unknown"))
}

$probePlans = New-Object System.Collections.ArrayList
foreach ($segment in @($segments.ToArray())) {
    $planPath = ".\_skybox_probe\segment-$((Normalize-LevelKey $targetName))-from-$((Normalize-LevelKey $donorName))-$($segment.key).patchplan.json"
    $binPath = ".\_local\skybox-segment-$((Normalize-LevelKey $targetName))-from-$((Normalize-LevelKey $donorName))-$($segment.key).bin"
    [void]$probePlans.Add([ordered]@{
        segment = $segment
        planOnlyCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroSkyboxPatchTest.ps1 -TargetLevelKey $targetScriptKey -DonorLevelKey $donorScriptKey -SegmentStart 0x$($segment.start.ToString('X')) -SegmentLength 0x$($segment.length.ToString('X')) -PlanOnly -PlanPath `"$planPath`""
        crashRiskCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-SpyroSkyboxPatchTest.ps1 -TargetLevelKey $targetScriptKey -DonorLevelKey $donorScriptKey -SegmentStart 0x$($segment.start.ToString('X')) -SegmentLength 0x$($segment.length.ToString('X')) -AllowExperimentalSegmentSwap -AllowStartupCriticalPatch -OutPath `"$binPath`""
        expectedObservation = "Do not boot segment BINs casually. WAD 10 segment tests can crash before Artisans loads; use plan output first and only create BINs when intentionally testing a crash-risk range."
    })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-SpyroSkyboxIsolationReport.ps1"
    target = [ordered]@{
        key = $targetScriptKey
        name = $targetName
        assetWadEntry = Get-JsonProperty $target "assetWadEntry"
        subfileSize = $targetSize
        sha1 = Get-JsonProperty $target "skySha1"
    }
    donor = [ordered]@{
        key = $donorScriptKey
        name = $donorName
        assetWadEntry = Get-JsonProperty $donor "assetWadEntry"
        subfileSize = $donorSize
        sha1 = Get-JsonProperty $donor "skySha1"
    }
    finding = "Whole subfile 3 swaps are not skybox-only. In-game tests changed Artisans terrain/material textures even when the archive size matched, and a tail segment probe crashed before Artisans loaded."
    policy = "WAD 10 BIN/CUE output is blocked by default. Use plan-only segment output for analysis; only generate BINs with explicit crash-risk flags."
    segments = @($segments.ToArray())
    probePlans = @($probePlans.ToArray())
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJsonPath -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Spyro Skybox Isolation Report")
[void]$lines.Add("")
[void]$lines.Add(('Target: {0} (`0x{1:X}` bytes)' -f $targetName, $targetSize))
[void]$lines.Add(('Donor: {0} (`0x{1:X}` bytes)' -f $donorName, $donorSize))
[void]$lines.Add("")
[void]$lines.Add("## Finding")
[void]$lines.Add("")
[void]$lines.Add("Whole subfile 3 swaps are not skybox-only. They can change Artisans terrain/material textures, and the tail segment probe crashed before Artisans loaded. WAD 10 BIN/CUE output is blocked by default.")
[void]$lines.Add("")
[void]$lines.Add("## Segment Probes")
[void]$lines.Add("")
Add-MarkdownRow $lines @("Segment", "Range", "Length", "Risk", "Purpose")
Add-MarkdownRow $lines @("---", "---", "---", "---", "---")
foreach ($segment in @($segments.ToArray())) {
    Add-MarkdownRow $lines @(
        $segment.key,
        ("0x{0:X}..0x{1:X}" -f $segment.start, $segment.endExclusive),
        ("0x{0:X}" -f $segment.length),
        $segment.risk,
        $segment.description
    )
}
[void]$lines.Add("")
[void]$lines.Add("## Commands")
[void]$lines.Add("")
foreach ($probe in @($probePlans.ToArray())) {
    [void]$lines.Add(("### {0}" -f $probe.segment.key))
    [void]$lines.Add("")
    [void]$lines.Add(('Plan only: `{0}`' -f $probe.planOnlyCommand))
    [void]$lines.Add("")
    [void]$lines.Add(('Crash-risk BIN/CUE, not recommended unless intentionally probing: `{0}`' -f $probe.crashRiskCommand))
    [void]$lines.Add("")
}

$lines | Set-Content -LiteralPath $resolvedOutMarkdownPath -Encoding UTF8

Write-Host "Wrote skybox isolation JSON to $resolvedOutJsonPath"
Write-Host "Wrote skybox isolation report to $resolvedOutMarkdownPath"
Write-Host ("Segments: {0}; target {1} <- donor {2}" -f $segments.Count, $targetName, $donorName)
