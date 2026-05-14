param(
    [string]$LevelName = "Artisans",
    [int]$ExpectedLevelId = 0x0A,
    [string]$OutPrefix = "artisans",
    [int]$MaxCandidates = 32,
    [int]$MaxSectors = 512,
    [int]$MaxSceneCandidates = 1,
    [int]$MaxOverlayCandidates = 6
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Write-CaptureSummary($Summary, [string]$JsonPath, [string]$MarkdownPath) {
    $Summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $JsonPath -Encoding UTF8

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# $($Summary.levelName) Workbench Capture")
    [void]$lines.Add("")
    [void]$lines.Add("- Expected level ID: $($Summary.expectedLevelId)")
    [void]$lines.Add("- Matched: $($Summary.matched)")
    if ($Summary.matched) {
        [void]$lines.Add("- Selected candidate: $($Summary.selected.index) at $($Summary.selected.path)")
        [void]$lines.Add("- RAM dump: $($Summary.outputs.ram)")
        [void]$lines.Add("- Moby table: $($Summary.outputs.mobys)")
        [void]$lines.Add("- Scene overlay: $($Summary.outputs.overlay)")
    }
    else {
        [void]$lines.Add("- No matching DuckStation RAM candidate was found. Stand in the target level and run the capture again.")
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Candidates")
    [void]$lines.Add("")
    [void]$lines.Add("| Index | Level | Mobys | Pointer | File |")
    [void]$lines.Add("| ---: | --- | ---: | --- | --- |")
    foreach ($candidate in @($Summary.candidates)) {
        [void]$lines.Add("| $($candidate.index) | $($candidate.levelId) | $($candidate.mobys) | $($candidate.pointer) | $([IO.Path]::GetFileName([string]$candidate.path)) |")
    }
    $lines | Set-Content -LiteralPath $MarkdownPath -Encoding UTF8
}

$dumpScript = Join-Path $PSScriptRoot "Dump-DuckStationRam.ps1"
$mobyScript = Join-Path $PSScriptRoot "Analyze-SpyroRuntimeMobyTables.ps1"
$overlayScript = Join-Path $PSScriptRoot "Export-SpyroRuntimeSceneOverlay.ps1"
foreach ($script in @($dumpScript, $mobyScript, $overlayScript)) {
    if (-not (Test-Path -LiteralPath $script)) {
        throw "Missing required script: $script"
    }
}

$capturePrefix = Join-Path $WorkspaceRoot "$OutPrefix-live-capture"
$summaryJson = Join-Path $WorkspaceRoot "$OutPrefix-capture-summary.json"
$summaryMd = Join-Path $WorkspaceRoot "$OutPrefix-capture-summary.md"
$ramOut = Join-Path $WorkspaceRoot "$OutPrefix-before-clean.bin"
$mobyOut = Join-Path $WorkspaceRoot "$OutPrefix-runtime-moby-tables.json"
$overlayOut = Join-Path $WorkspaceRoot "$OutPrefix-runtime-scene-editor-overlay.json"

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $dumpScript -DumpAllCandidates -MaxCandidates $MaxCandidates -OutPrefix $capturePrefix

$manifestPath = "$capturePrefix-candidates.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "DuckStation RAM dump did not produce a candidate manifest: $manifestPath"
}

$parsedManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest = @()
foreach ($item in @($parsedManifest)) {
    if ($item -is [System.Array]) {
        foreach ($subItem in $item) { $manifest += $subItem }
    }
    else {
        $manifest += $item
    }
}
$candidateReports = @()
foreach ($candidate in $manifest) {
    $path = [string]$candidate.path
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $bytes = [IO.File]::ReadAllBytes($path)
    $levelId = Get-UInt32LE $bytes 0x758B4
    $pointer = Get-UInt32LE $bytes 0x75828
    $candidateReports += [pscustomobject][ordered]@{
        index = [int]$candidate.index
        path = $path
        levelId = ("0x{0:X2}" -f [uint32]$levelId)
        levelIdValue = [uint32]$levelId
        pointer = ("0x{0:X8}" -f [uint32]$pointer)
        mobys = [int]$candidate.mobys
        nonZero = [int]$candidate.nonZero
        checksum = [string]$candidate.checksum
    }
}

if ($candidateReports.Count -eq 0) {
    throw "DuckStation RAM candidate manifest was present, but no candidate files could be inspected: $manifestPath"
}

$matches = @($candidateReports | Where-Object { [uint32]$_.levelIdValue -eq [uint32]$ExpectedLevelId } | Sort-Object -Property @{ Expression = { $_.mobys }; Descending = $true }, index)
$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    levelName = $LevelName
    outPrefix = $OutPrefix
    expectedLevelId = ("0x{0:X2}" -f [uint32]$ExpectedLevelId)
    matched = ($matches.Count -gt 0)
    selected = $null
    candidates = @($candidateReports)
    outputs = [ordered]@{
        ram = $ramOut
        mobys = $mobyOut
        overlay = $overlayOut
    }
}

if ($matches.Count -eq 0) {
    Write-CaptureSummary $summary $summaryJson $summaryMd
    throw "No $LevelName RAM candidate had level ID $('0x{0:X2}' -f [uint32]$ExpectedLevelId). Current DuckStation level is probably not $LevelName."
}

$selected = $matches[0]
$summary.selected = [ordered]@{
    index = [int]$selected.index
    path = [string]$selected.path
    levelId = [string]$selected.levelId
    pointer = [string]$selected.pointer
    mobys = [int]$selected.mobys
}

Copy-Item -LiteralPath ([string]$selected.path) -Destination $ramOut -Force

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $mobyScript -RamPath $ramOut -OutPath $mobyOut
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $overlayScript -RamPath $ramOut -OutPath $overlayOut -MaxSceneCandidates $MaxSceneCandidates -MaxSectors $MaxSectors -MaxOverlayCandidates $MaxOverlayCandidates -MaxPolygonsPerCandidate 2500 -MaxEdgesPerCandidate 8000 -MaxPointsPerCandidate 8000 -Projections xy

Write-CaptureSummary $summary $summaryJson $summaryMd
Write-Host "Captured $LevelName workbench from candidate $($selected.index)."
Write-Host "Summary: $summaryMd"
