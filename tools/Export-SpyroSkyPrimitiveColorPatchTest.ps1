param(
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$SkySourceHitsPath = ".\_skybox_probe\live-current-screen-sky-wad-source-hits-focused.json",
    [string]$OutImagePath = ".\Spyro the Dragon (USA)-skyprimitive-magenta-test.bin",
    [string]$OutCuePath = ".\Spyro the Dragon (USA)-skyprimitive-magenta-test.cue",
    [string]$OutPlanPath = "",
    [string]$TargetColorHex = "#FF00FF",
    [int]$TargetEntryIndex = 10,
    [int]$TargetSubfileIndex = 1,
    [string[]]$PatchSpecs = @(),
    [int]$MaxColorWordPatches = 512,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Convert-HexToInt64([string]$Value) {
    $clean = $Value.Trim()
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [Convert]::ToInt64($clean, 10)
}

function Convert-HexColorToRgb([string]$Hex) {
    $clean = $Hex.Trim()
    if ($clean.StartsWith("#")) { $clean = $clean.Substring(1) }
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    if ($clean.Length -ne 6) { throw "Target color must be #RRGGBB, RRGGBB, or 0xRRGGBB." }
    return [pscustomobject]@{
        r = [byte][Convert]::ToInt32($clean.Substring(0, 2), 16)
        g = [byte][Convert]::ToInt32($clean.Substring(2, 2), 16)
        b = [byte][Convert]::ToInt32($clean.Substring(4, 2), 16)
        text = ("#{0}" -f $clean.ToUpperInvariant())
    }
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { "{0:X2}" -f $_ }) -join " ")
}

function Convert-HexPatternToBytes([string]$PatternHex) {
    $parts = $PatternHex.Split(" ", [StringSplitOptions]::RemoveEmptyEntries)
    $bytes = New-Object byte[] $parts.Count
    for ($i = 0; $i -lt $parts.Count; $i++) {
        $bytes[$i] = [byte][Convert]::ToInt32($parts[$i], 16)
    }
    return ,$bytes
}

function Get-CueText([string]$SourceCue, [string]$OutBinName) {
    if (-not (Test-Path -LiteralPath $SourceCue)) {
        return @(
            ("FILE ""{0}"" BINARY" -f $OutBinName),
            "  TRACK 01 MODE2/2352",
            "    INDEX 01 00:00:00"
        )
    }

    $lines = Get-Content -LiteralPath $SourceCue
    $updated = New-Object Collections.Generic.List[string]
    $replaced = $false
    foreach ($line in $lines) {
        if (-not $replaced -and $line -match '^\s*FILE\s+".*"\s+BINARY\s*$') {
            [void]$updated.Add(("FILE ""{0}"" BINARY" -f $OutBinName))
            $replaced = $true
        }
        else {
            [void]$updated.Add($line)
        }
    }
    if (-not $replaced) {
        $updated.Insert(0, ("FILE ""{0}"" BINARY" -f $OutBinName))
    }
    return @($updated.ToArray())
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$hitsPath = Resolve-WorkspacePath $SkySourceHitsPath
$outImage = Resolve-WorkspacePath $OutImagePath
$outCue = Resolve-WorkspacePath $OutCuePath
if ([string]::IsNullOrWhiteSpace($OutPlanPath)) {
    $OutPlanPath = "$OutImagePath.skyprimitivepatchplan.json"
}
$outPlan = Resolve-WorkspacePath $OutPlanPath

foreach ($path in @($sourceImage, $hitsPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $path" }
}
if ($MaxColorWordPatches -lt 1) { throw "-MaxColorWordPatches must be at least 1." }

$target = Convert-HexColorToRgb $TargetColorHex
$specs = New-Object Collections.Generic.List[object]
if ($PatchSpecs.Count -gt 0) {
    foreach ($specText in $PatchSpecs) {
        $parts = $specText.Split("|")
        if ($parts.Count -ne 3) { throw "Patch spec '$specText' must be '<hits-json>|<entry>|<subfile>'." }
        [void]$specs.Add([pscustomobject][ordered]@{
            hitsPath = Resolve-WorkspacePath $parts[0]
            entryIndex = [int]$parts[1]
            subfileIndex = [int]$parts[2]
        })
    }
}
else {
    [void]$specs.Add([pscustomobject][ordered]@{
        hitsPath = $hitsPath
        entryIndex = $TargetEntryIndex
        subfileIndex = $TargetSubfileIndex
    })
}

$patches = New-Object Collections.Generic.List[object]
$seenOffsets = @{}
$stream = [IO.File]::OpenRead($sourceImage)
try {
    foreach ($spec in @($specs.ToArray())) {
        if (-not (Test-Path -LiteralPath ([string]$spec.hitsPath))) { throw "Missing source-hit file: $($spec.hitsPath)" }
        $specHitsJson = Get-Content -Raw -LiteralPath ([string]$spec.hitsPath) | ConvertFrom-Json
        $targetRegion = "entry-$($spec.entryIndex)-subfile-$($spec.subfileIndex)"
        $candidateHits = @($specHitsJson.hits | Where-Object {
            [string]$_.region -eq $targetRegion -and
            [string]$_.kind -eq "rgb-command-word"
        } | Sort-Object imageOffset, key -Unique | Select-Object -First $MaxColorWordPatches)

        if ($candidateHits.Count -eq 0) {
            throw "No $targetRegion rgb-command-word hits found in $($spec.hitsPath)."
        }

        foreach ($hit in $candidateHits) {
            $imageOffset = Convert-HexToInt64 ([string]$hit.imageOffset)
            $offsetKey = ("0x{0:X}" -f $imageOffset)
            if ($seenOffsets.ContainsKey($offsetKey)) { continue }
            $seenOffsets[$offsetKey] = $true

            $original = New-Object byte[] 4
            $stream.Position = $imageOffset
            [void]$stream.Read($original, 0, 4)

            $patternBytes = Convert-HexPatternToBytes ([string]$hit.patternHex)
            if ($patternBytes.Length -ne 4) { continue }
            $matches = $true
            for ($i = 0; $i -lt 4; $i++) {
                if ($original[$i] -ne $patternBytes[$i]) { $matches = $false; break }
            }
            if (-not $matches) {
                throw ("Source mismatch at image offset 0x{0:X}: expected {1}, found {2}" -f $imageOffset, [string]$hit.patternHex, (Convert-BytesToHex $original))
            }

            $patched = [byte[]]@($target.r, $target.g, $target.b, $original[3])
            [void]$patches.Add([pscustomobject][ordered]@{
                imageOffset = $offsetKey
                targetRegion = $targetRegion
                sourceHitsPath = [string]$spec.hitsPath
                wadRelativeOffset = [string]$hit.wadRelativeOffset
                subfileOffset = [string]$hit.regionRelativeOffset
                sourceColor = [string]$hit.key
                originalBytes = Convert-BytesToHex $original
                patchBytes = Convert-BytesToHex $patched
                source = [string]$hit.source
            })
        }
    }
}
finally {
    $stream.Dispose()
}

$plan = [pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroSkyPrimitiveColorPatchTest.ps1"
    sourceImagePath = (Resolve-Path -LiteralPath $sourceImage).Path
    skySourceHitsPath = (Resolve-Path -LiteralPath $hitsPath).Path
    outImagePath = $outImage
    outCuePath = $outCue
    targetColorHex = $target.text
    targetRegions = @($specs.ToArray() | ForEach-Object { "entry-$($_.entryIndex)-subfile-$($_.subfileIndex)" })
    patchSpecs = @($specs.ToArray())
    patchCount = $patches.Count
    scope = "rgb-command-word sky primitive source-color hits"
    patches = @($patches.ToArray())
}

$outDir = Split-Path -Parent $outPlan
if (-not [string]::IsNullOrWhiteSpace($outDir)) { [IO.Directory]::CreateDirectory($outDir) | Out-Null }
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPlan -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $sourceImage -Destination $outImage -Force
    $writeStream = [IO.File]::Open($outImage, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        foreach ($patch in @($patches.ToArray())) {
            $imageOffset = Convert-HexToInt64 ([string]$patch.imageOffset)
            $bytes = Convert-HexPatternToBytes ([string]$patch.patchBytes)
            $writeStream.Position = $imageOffset
            $writeStream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $writeStream.Dispose()
    }

    $cueDir = Split-Path -Parent $outCue
    if (-not [string]::IsNullOrWhiteSpace($cueDir)) { [IO.Directory]::CreateDirectory($cueDir) | Out-Null }
    $outBinName = Split-Path -Leaf $outImage
    Get-CueText $sourceCue $outBinName | Set-Content -LiteralPath $outCue -Encoding ASCII
}

Write-Host ("Sky primitive source color patch plan: {0}" -f $outPlan)
Write-Host ("Patch count: {0}; target color: {1}" -f $patches.Count, $target.text)
if ($PlanOnly) {
    Write-Host "Plan only; no BIN/CUE was written."
}
else {
    Write-Host ("Wrote patched BIN: {0}" -f $outImage)
    Write-Host ("Wrote patched CUE: {0}" -f $outCue)
}
