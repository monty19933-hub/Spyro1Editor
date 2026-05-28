param(
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [string]$RecordReportPath = ".\_skybox_probe\sky-primitive-record-candidates-darkhollow.json",
    [ValidateSet("DarkHollowMixed", "DarkHollowDark", "DarkHollowMid", "DarkHollowPlus19Dark", "StoneHillNight", "StoneHillBestNight", "StoneHillNightKeeper", "StoneHillMoonAtmosphere", "StoneHillMoonStarProbe", "StoneHillFlyInPeriA", "StoneHillFlyInPeriB", "StoneHillFlyInPeriC", "StoneHillFlyInWaterA", "StoneHillFlyInWaterB", "StoneHillFlyInWaterC", "StoneHillFlyInWaterAll", "StoneHillFlyInLowerA", "StoneHillFlyInLowerB", "StoneHillFlyInLowerC", "StoneHillFlyInLowerD", "StoneHillFlyInLowerAll", "StoneHillBestWaterA", "StoneHillBestWaterB", "StoneHillBestWaterC", "StoneHillBestWaterABC", "StoneHillBestWaterABCFlyInPeriA", "StoneHillWaterOnlyABC", "StoneHillWaterOnlyABCDeep", "StoneHillWaterOnlyABCMidnight", "StoneHillWaterOnlyABCDeepFlyInPeriA", "StoneHillStableNight", "StoneHillDeepNight", "StoneHillFullNight", "StoneHillSmoothNight", "StoneHillFlatNight", "StoneHillBaseSkyLoaded", "StoneHillBaseSkyPortal", "StoneHillBaseSkyCombined", "StoneHillLoadedPeriA", "StoneHillLoadedPeriB", "StoneHillLoadedPeriC", "StoneHillGpuCleanNight", "StoneHillBandCleanA", "StoneHillBandCleanB", "StoneHillBandCleanC", "Custom")]
    [string]$Preset = "DarkHollowMixed",
    [string]$PaletteHex = "",
    [string]$OutputPrefix = "",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Convert-HexToInt64([string]$Value) {
    $clean = $Value.Trim()
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [Convert]::ToInt64($clean, 10)
}

function Test-PvdAt([IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    return [ordered]@{ sectorSize = $SectorSize; userOffset = $UserOffset; userSize = 2048 }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-PvdAt $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    throw "Could not detect disc layout for $Path."
}

function Convert-WadOffsetToImageOffset($Layout, [int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + [int64]$sectorOffset
}

function Read-WadBytes([IO.FileStream]$Stream, $Layout, [int64]$WadOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $WadOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = Convert-WadOffsetToImageOffset $Layout $absolute
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return ,$result
}

function Write-WadBytes([IO.FileStream]$Stream, $Layout, [int64]$WadOffset, [byte[]]$Bytes) {
    $remaining = $Bytes.Length
    $readOffset = 0
    $absolute = $WadOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $toWrite = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = Convert-WadOffsetToImageOffset $Layout $absolute
        $Stream.Write($Bytes, $readOffset, $toWrite)
        $readOffset += $toWrite
        $remaining -= $toWrite
        $absolute += $toWrite
    }
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

function Get-WadEntry($WadAnalysis, [int]$EntryIndex) {
    foreach ($entry in @($WadAnalysis.entries)) {
        if ([int]$entry.index -eq $EntryIndex) { return $entry }
    }
    throw "WAD entry $EntryIndex was not found."
}

function Get-WadSubfile($WadAnalysis, [int]$EntryIndex, [int]$SubfileIndex) {
    $entry = Get-WadEntry $WadAnalysis $EntryIndex
    if ($null -eq $entry.level -or $null -eq $entry.level.subfiles) {
        throw "WAD entry $EntryIndex has no parsed subfiles."
    }
    foreach ($subfile in @($entry.level.subfiles)) {
        if ([int]$subfile.index -eq $SubfileIndex) {
            return [pscustomobject][ordered]@{
                entryIndex = $EntryIndex
                subfileIndex = $SubfileIndex
                wadOffset = [int64]$entry.offset + [int64]$subfile.offset
                size = [int]$subfile.size
            }
        }
    }
    throw "WAD entry $EntryIndex subfile $SubfileIndex was not found."
}

function Convert-BytesToHex([byte[]]$Bytes, [int]$Length) {
    $parts = New-Object Collections.Generic.List[string]
    $end = [Math]::Min($Length, $Bytes.Length)
    for ($i = 0; $i -lt $end; $i++) {
        [void]$parts.Add(("{0:X2}" -f $Bytes[$i]))
    }
    return ($parts -join " ")
}

function Get-ColorBrightness([string]$Color) {
    if ([string]::IsNullOrWhiteSpace($Color) -or -not $Color.StartsWith("#")) { return 999 }
    $r = [Convert]::ToInt32($Color.Substring(1, 2), 16)
    $g = [Convert]::ToInt32($Color.Substring(3, 2), 16)
    $b = [Convert]::ToInt32($Color.Substring(5, 2), 16)
    return $r + $g + $b
}

function Convert-HexColorToRgbBytes([string]$Color) {
    $text = $Color.Trim()
    if ($text.StartsWith("#")) { $text = $text.Substring(1) }
    if ($text.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $text = $text.Substring(2) }
    if ($text.Length -ne 6) { throw "Bad color '$Color'. Use #RRGGBB." }
    return ,([byte[]]@(
        [Convert]::ToByte($text.Substring(0, 2), 16),
        [Convert]::ToByte($text.Substring(2, 2), 16),
        [Convert]::ToByte($text.Substring(4, 2), 16)
    ))
}

function Parse-PaletteHex([string]$Value) {
    $tokens = @($Value -split '[,;|\s]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($tokens.Count -eq 0) { throw "Custom palette is empty. Use colors like #0B1038 #243A80 #AAB6D8." }
    $items = New-Object Collections.Generic.List[object]
    foreach ($token in $tokens) {
        $rgb = Convert-HexColorToRgbBytes $token
        $clean = $token.Trim()
        if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
        if (-not $clean.StartsWith("#")) { $clean = "#" + $clean }
        [void]$items.Add([pscustomobject][ordered]@{
            kind = "literal"
            color = $clean
            rgb = $rgb
        })
    }
    return @($items.ToArray())
}

function New-NightGradePalette() {
    return @([pscustomobject][ordered]@{
        kind = "night-grade"
        color = "#0A1538..#2A5F9C"
    })
}

function New-SmoothNightPalette() {
    return @([pscustomobject][ordered]@{
        kind = "smooth-night-grade"
        color = "#101F54..#173B78"
    })
}

function New-FlatNightPalette() {
    return @([pscustomobject][ordered]@{
        kind = "flat-night"
        color = "#143166"
    })
}

function Convert-RgbToNightBytes([byte[]]$BeforeBytes) {
    $r = [double]$BeforeBytes[0]
    $g = [double]$BeforeBytes[1]
    $b = [double]$BeforeBytes[2]
    $t = ((0.299 * $r) + (0.587 * $g) + (0.114 * $b)) / 255.0
    if ($t -lt 0.0) { $t = 0.0 }
    if ($t -gt 1.0) { $t = 1.0 }
    $outR = [byte][Math]::Round(7 + (31 * $t))
    $outG = [byte][Math]::Round(14 + (72 * $t))
    $outB = [byte][Math]::Round(48 + (126 * $t))
    return ,([byte[]]@($outR, $outG, $outB))
}

function Convert-RgbToSmoothNightBytes([byte[]]$BeforeBytes) {
    $r = [double]$BeforeBytes[0]
    $g = [double]$BeforeBytes[1]
    $b = [double]$BeforeBytes[2]
    $t = ((0.299 * $r) + (0.587 * $g) + (0.114 * $b)) / 255.0
    if ($t -lt 0.0) { $t = 0.0 }
    if ($t -gt 1.0) { $t = 1.0 }
    $outR = [byte][Math]::Round(16 + (7 * $t))
    $outG = [byte][Math]::Round(31 + (28 * $t))
    $outB = [byte][Math]::Round(84 + (36 * $t))
    return ,([byte[]]@($outR, $outG, $outB))
}

function Convert-RgbToWaterNightBytes([byte[]]$BeforeBytes) {
    $r = [double]$BeforeBytes[0]
    $g = [double]$BeforeBytes[1]
    $b = [double]$BeforeBytes[2]
    $t = ((0.299 * $r) + (0.587 * $g) + (0.114 * $b)) / 255.0
    if ($t -lt 0.0) { $t = 0.0 }
    if ($t -gt 1.0) { $t = 1.0 }
    $outR = [byte][Math]::Round(4 + (18 * $t))
    $outG = [byte][Math]::Round(10 + (42 * $t))
    $outB = [byte][Math]::Round(38 + (82 * $t))
    return ,([byte[]]@($outR, $outG, $outB))
}

function New-DonorWordPalette([string]$Which, [string]$ReportPath) {
    $resolvedReport = Resolve-WorkspacePath $ReportPath
    if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Missing Dark Hollow record report: $resolvedReport" }
    $report = Get-Content -Raw -LiteralPath $resolvedReport | ConvertFrom-Json
    $donorRecords = @($report.records | Where-Object {
        [int]$_.entryIndex -eq 14 -and
        [int]$_.runWordCount -ge 7 -and
        [double]$_.avgBrightness -ge 180 -and
        [double]$_.avgBrightness -le 460
    } | Sort-Object @{ Expression = { [int]$_.runWordCount }; Descending = $true }, @{ Expression = { [double]$_.avgBrightness } })
    if ($donorRecords.Count -eq 0) { throw "No Dark Hollow donor records were found in $resolvedReport." }

    $words = New-Object Collections.Generic.List[object]
    foreach ($record in @($donorRecords)) {
        $runStart = [int64](Convert-HexToInt64 ([string]$record.runStartOffset))
        $colors = @($record.previewColors)
        for ($i = 0; $i -lt [int]$record.runWordCount; $i++) {
            $color = if ($i -lt $colors.Count) { [string]$colors[$i] } else { "" }
            [void]$words.Add([pscustomobject][ordered]@{
                kind = "donor"
                donorEntry = 14
                donorSubfile = 1
                donorStart = $runStart + ([int64]$i * 4)
                color = $color
                brightness = if (-not [string]::IsNullOrWhiteSpace($color)) { Get-ColorBrightness $color } else { [int][double]$record.avgBrightness }
            })
        }
    }

    $array = @($words.ToArray())
    $dark = @($array | Where-Object { [int]$_.brightness -le 260 } | Sort-Object brightness)
    $mid = @($array | Where-Object { [int]$_.brightness -gt 260 -and [int]$_.brightness -le 390 } | Sort-Object brightness)
    $bright = @($array | Where-Object { [int]$_.brightness -gt 390 } | Sort-Object brightness)
    if ($dark.Count -eq 0) { $dark = @($array | Sort-Object brightness | Select-Object -First 24) }
    if ($mid.Count -eq 0) { $mid = @($array | Sort-Object brightness | Select-Object -First 24) }
    if ($bright.Count -eq 0) { $bright = @($array | Sort-Object brightness -Descending | Select-Object -First 24) }

    if ($Which -eq "Dark") { return @($dark) }
    if ($Which -eq "Mid") { return @($mid) }
    return @($dark + $mid + $bright)
}

function New-PortalSingleWordTargets([string]$Prefix, [string[]]$Offsets) {
    $items = New-Object Collections.Generic.List[object]
    foreach ($offset in $Offsets) {
        $clean = $offset.Trim()
        $labelOffset = $clean.ToLowerInvariant().Replace("0x", "")
        [void]$items.Add([pscustomobject]@{
            Label = "$Prefix-$labelOffset"
            WordCount = 1
            Portal = $clean
        })
    }
    return @($items.ToArray())
}

function Add-TargetRgbOverride([object[]]$Records, [string]$RgbHex) {
    $items = New-Object Collections.Generic.List[object]
    foreach ($record in @($Records)) {
        $copy = [ordered]@{
            Label = [string]$record.Label
            WordCount = [int]$record.WordCount
            RgbHex = $RgbHex
        }
        if ($record.PSObject.Properties.Name -contains "Portal" -and -not [string]::IsNullOrWhiteSpace([string]$record.Portal)) {
            $copy.Portal = [string]$record.Portal
        }
        if ($record.PSObject.Properties.Name -contains "Loaded" -and -not [string]::IsNullOrWhiteSpace([string]$record.Loaded)) {
            $copy.Loaded = [string]$record.Loaded
        }
        [void]$items.Add([pscustomobject]$copy)
    }
    return @($items.ToArray())
}

$targetRecords = @(
    [pscustomobject]@{ Label = "best22-a"; WordCount = 22; Portal = "0x74B8C"; Loaded = "0x91350" },
    [pscustomobject]@{ Label = "best22-b"; WordCount = 22; Portal = "0x78670"; Loaded = "0x94E34" },
    [pscustomobject]@{ Label = "best22-c"; WordCount = 22; Portal = "0x79AA8"; Loaded = "0x9626C" }
)

$targetExtras = @(
    [pscustomobject]@{ Label = "anchor19-a"; WordCount = 19; Portal = "0x73638"; Loaded = "0x8FDFC" },
    [pscustomobject]@{ Label = "anchor19-b"; WordCount = 19; Portal = "0x74378"; Loaded = "0x90B3C" },
    [pscustomobject]@{ Label = "anchor19-c"; WordCount = 19; Portal = "0x76DD8"; Loaded = "0x9359C" }
)

$targetDeepNight = @(
    $targetExtras +
    $targetRecords +
    @(
        [pscustomobject]@{ Label = "upper13"; WordCount = 7; Portal = "0x78338"; Loaded = "0x94AFC" },
        [pscustomobject]@{ Label = "upper15"; WordCount = 7; Portal = "0x78DE0"; Loaded = "0x955A4" }
    )
)

$targetFlyInSlabs = @(
    [pscustomobject]@{ Label = "flyin-wedge-6a19c"; WordCount = 2; Portal = "0x6A19C" },
    [pscustomobject]@{ Label = "flyin-wedge-6adc8"; WordCount = 2; Portal = "0x6ADC8" },
    [pscustomobject]@{ Label = "flyin-wedge-6b3fc"; WordCount = 3; Portal = "0x6B3FC" },
    [pscustomobject]@{ Label = "flyin-slab-5de90"; WordCount = 1; Portal = "0x5DE90" },
    [pscustomobject]@{ Label = "flyin-slab-5de98"; WordCount = 3; Portal = "0x5DE98" },
    [pscustomobject]@{ Label = "flyin-slab-5df0c"; WordCount = 5; Portal = "0x5DF0C" },
    [pscustomobject]@{ Label = "flyin-slab-5f120"; WordCount = 2; Portal = "0x5F120" },
    [pscustomobject]@{ Label = "flyin-slab-5f12c"; WordCount = 1; Portal = "0x5F12C" },
    [pscustomobject]@{ Label = "flyin-triangle-5f13c"; WordCount = 1; Portal = "0x5F13C" },
    [pscustomobject]@{ Label = "flyin-slab-5f248"; WordCount = 1; Portal = "0x5F248" },
    [pscustomobject]@{ Label = "flyin-slab-5f250"; WordCount = 3; Portal = "0x5F250" },
    [pscustomobject]@{ Label = "flyin-slab-5f2c4"; WordCount = 6; Portal = "0x5F2C4" },
    [pscustomobject]@{ Label = "flyin-triangle-5f2dc"; WordCount = 1; Portal = "0x5F2DC" },
    [pscustomobject]@{ Label = "flyin-slab-5f38c"; WordCount = 5; Portal = "0x5F38C" },
    [pscustomobject]@{ Label = "flyin-triangle-5f3a8"; WordCount = 1; Portal = "0x5F3A8" },
    [pscustomobject]@{ Label = "flyin-slab-5f3ac"; WordCount = 1; Portal = "0x5F3AC" },
    [pscustomobject]@{ Label = "flyin-slab-5f66c"; WordCount = 1; Portal = "0x5F66C" },
    [pscustomobject]@{ Label = "flyin-slab-5f674"; WordCount = 1; Portal = "0x5F674" },
    [pscustomobject]@{ Label = "flyin-slab-5fa30"; WordCount = 1; Portal = "0x5FA30" },
    [pscustomobject]@{ Label = "flyin-slab-5fa38"; WordCount = 1; Portal = "0x5FA38" },
    [pscustomobject]@{ Label = "flyin-slab-5faa4"; WordCount = 1; Portal = "0x5FAA4" },
    [pscustomobject]@{ Label = "flyin-slab-5faac"; WordCount = 5; Portal = "0x5FAAC" },
    [pscustomobject]@{ Label = "flyin-slab-5fdd0"; WordCount = 1; Portal = "0x5FDD0" },
    [pscustomobject]@{ Label = "flyin-slab-5fdd8"; WordCount = 4; Portal = "0x5FDD8" }
)

$targetFlyInTriangles = @(
    [pscustomobject]@{ Label = "flyin-triangle-58ab8"; WordCount = 1; Portal = "0x58AB8" },
    [pscustomobject]@{ Label = "flyin-triangle-591c8"; WordCount = 1; Portal = "0x591C8" },
    [pscustomobject]@{ Label = "flyin-triangle-6a720"; WordCount = 1; Portal = "0x6A720" },
    [pscustomobject]@{ Label = "flyin-triangle-6ba28"; WordCount = 1; Portal = "0x6BA28" },
    [pscustomobject]@{ Label = "flyin-triangle-6c0d4"; WordCount = 1; Portal = "0x6C0D4" },
    [pscustomobject]@{ Label = "flyin-triangle-6c5e4"; WordCount = 1; Portal = "0x6C5E4" },
    [pscustomobject]@{ Label = "flyin-triangle-6ced4"; WordCount = 1; Portal = "0x6CED4" },
    [pscustomobject]@{ Label = "flyin-triangle-6d524"; WordCount = 1; Portal = "0x6D524" },
    [pscustomobject]@{ Label = "flyin-triangle-6da0c"; WordCount = 1; Portal = "0x6DA0C" },
    [pscustomobject]@{ Label = "flyin-triangle-6de6c"; WordCount = 1; Portal = "0x6DE6C" },
    [pscustomobject]@{ Label = "flyin-triangle-6e25c"; WordCount = 1; Portal = "0x6E25C" },
    [pscustomobject]@{ Label = "flyin-triangle-6e9e0"; WordCount = 1; Portal = "0x6E9E0" },
    [pscustomobject]@{ Label = "flyin-triangle-6f070"; WordCount = 1; Portal = "0x6F070" },
    [pscustomobject]@{ Label = "flyin-triangle-6f630"; WordCount = 1; Portal = "0x6F630" },
    [pscustomobject]@{ Label = "flyin-triangle-6fcfc"; WordCount = 1; Portal = "0x6FCFC" },
    [pscustomobject]@{ Label = "flyin-triangle-70370"; WordCount = 1; Portal = "0x70370" },
    [pscustomobject]@{ Label = "flyin-triangle-70d4c"; WordCount = 1; Portal = "0x70D4C" },
    [pscustomobject]@{ Label = "flyin-triangle-716fc"; WordCount = 1; Portal = "0x716FC" }
)

$targetSpotCleanup = @(
    [pscustomobject]@{ Label = "flyin-spot-5f128"; WordCount = 1; Portal = "0x5F128" },
    [pscustomobject]@{ Label = "flyin-spot-5f130"; WordCount = 2; Portal = "0x5F130" },
    [pscustomobject]@{ Label = "flyin-spot-5f670"; WordCount = 1; Portal = "0x5F670" },
    [pscustomobject]@{ Label = "flyin-spot-5f678"; WordCount = 2; Portal = "0x5F678" },
    [pscustomobject]@{ Label = "flyin-spot-6a18c"; WordCount = 1; Portal = "0x6A18C" },
    [pscustomobject]@{ Label = "flyin-spot-6a724"; WordCount = 1; Portal = "0x6A724" },
    [pscustomobject]@{ Label = "flyin-spot-6a72c"; WordCount = 1; Portal = "0x6A72C" },
    [pscustomobject]@{ Label = "flyin-spot-6de5c"; WordCount = 2; Portal = "0x6DE5C" },
    [pscustomobject]@{ Label = "loaded-spot-7b9bc"; WordCount = 1; Loaded = "0x7B9BC" },
    [pscustomobject]@{ Label = "loaded-spot-7d294"; WordCount = 1; Loaded = "0x7D294" },
    [pscustomobject]@{ Label = "loaded-spot-81c50"; WordCount = 1; Loaded = "0x81C50" },
    [pscustomobject]@{ Label = "loaded-spot-8bd40"; WordCount = 1; Loaded = "0x8BD40" },
    [pscustomobject]@{ Label = "loaded-spot-8c418"; WordCount = 1; Loaded = "0x8C418" },
    [pscustomobject]@{ Label = "loaded-spot-8c574"; WordCount = 1; Loaded = "0x8C574" },
    [pscustomobject]@{ Label = "loaded-spot-8c6a4"; WordCount = 2; Loaded = "0x8C6A4" },
    [pscustomobject]@{ Label = "loaded-spot-8cbd0"; WordCount = 1; Loaded = "0x8CBD0" },
    [pscustomobject]@{ Label = "loaded-spot-93540"; WordCount = 1; Loaded = "0x93540" },
    [pscustomobject]@{ Label = "loaded-spot-95e40"; WordCount = 1; Loaded = "0x95E40" },
    [pscustomobject]@{ Label = "loaded-spot-97854"; WordCount = 1; Loaded = "0x97854" }
)

$targetRemainingTriangles = @(
    [pscustomobject]@{ Label = "flyin-final-58c40"; WordCount = 1; Portal = "0x58C40" },
    [pscustomobject]@{ Label = "flyin-final-59c3c"; WordCount = 1; Portal = "0x59C3C" },
    [pscustomobject]@{ Label = "flyin-final-5abe4"; WordCount = 2; Portal = "0x5ABE4" },
    [pscustomobject]@{ Label = "flyin-final-5c3c4"; WordCount = 2; Portal = "0x5C3C4" },
    [pscustomobject]@{ Label = "flyin-final-5df24"; WordCount = 1; Portal = "0x5DF24" },
    [pscustomobject]@{ Label = "flyin-final-5e93c"; WordCount = 2; Portal = "0x5E93C" },
    [pscustomobject]@{ Label = "loaded-final-72e30"; WordCount = 1; Loaded = "0x72E30" },
    [pscustomobject]@{ Label = "loaded-final-825c4"; WordCount = 1; Loaded = "0x825C4" },
    [pscustomobject]@{ Label = "loaded-final-8573c"; WordCount = 1; Loaded = "0x8573C" },
    [pscustomobject]@{ Label = "loaded-final-8a2e4"; WordCount = 1; Loaded = "0x8A2E4" },
    [pscustomobject]@{ Label = "loaded-final-91af8"; WordCount = 1; Loaded = "0x91AF8" },
    [pscustomobject]@{ Label = "loaded-final-92548"; WordCount = 1; Loaded = "0x92548" },
    [pscustomobject]@{ Label = "loaded-final-92f00"; WordCount = 1; Loaded = "0x92F00" },
    [pscustomobject]@{ Label = "loaded-final-92f24"; WordCount = 1; Loaded = "0x92F24" },
    [pscustomobject]@{ Label = "loaded-final-934d8"; WordCount = 1; Loaded = "0x934D8" },
    [pscustomobject]@{ Label = "loaded-final-93ad4"; WordCount = 1; Loaded = "0x93AD4" },
    [pscustomobject]@{ Label = "loaded-final-93e38"; WordCount = 1; Loaded = "0x93E38" },
    [pscustomobject]@{ Label = "loaded-final-93ef4"; WordCount = 1; Loaded = "0x93EF4" },
    [pscustomobject]@{ Label = "loaded-final-9440c"; WordCount = 1; Loaded = "0x9440C" },
    [pscustomobject]@{ Label = "loaded-final-962f4"; WordCount = 1; Loaded = "0x962F4" }
)

$targetGpuSkyCleanup = @(
    [pscustomobject]@{ Label = "gpu-sky-8fce8"; WordCount = 1; Portal = "0x73524"; Loaded = "0x8FCE8" },
    [pscustomobject]@{ Label = "gpu-sky-8fcf8"; WordCount = 1; Portal = "0x73534"; Loaded = "0x8FCF8" },
    [pscustomobject]@{ Label = "gpu-sky-8fd24"; WordCount = 1; Portal = "0x73560"; Loaded = "0x8FD24" },
    [pscustomobject]@{ Label = "gpu-sky-8fde0"; WordCount = 1; Portal = "0x7361C"; Loaded = "0x8FDE0" },
    [pscustomobject]@{ Label = "gpu-sky-909a8"; WordCount = 1; Portal = "0x741E4"; Loaded = "0x909A8" },
    [pscustomobject]@{ Label = "gpu-sky-90adc"; WordCount = 1; Portal = "0x74318"; Loaded = "0x90ADC" },
    [pscustomobject]@{ Label = "gpu-sky-911b0"; WordCount = 1; Portal = "0x749EC"; Loaded = "0x911B0" },
    [pscustomobject]@{ Label = "gpu-sky-91268"; WordCount = 1; Portal = "0x74AA4"; Loaded = "0x91268" },
    [pscustomobject]@{ Label = "gpu-sky-91324"; WordCount = 1; Portal = "0x74B60"; Loaded = "0x91324" },
    [pscustomobject]@{ Label = "gpu-sky-924d8"; WordCount = 1; Portal = "0x75D14"; Loaded = "0x924D8" },
    [pscustomobject]@{ Label = "gpu-sky-92528"; WordCount = 1; Portal = "0x75D64"; Loaded = "0x92528" },
    [pscustomobject]@{ Label = "gpu-sky-92534"; WordCount = 1; Portal = "0x75D70"; Loaded = "0x92534" },
    [pscustomobject]@{ Label = "gpu-sky-925a4"; WordCount = 1; Portal = "0x75DE0"; Loaded = "0x925A4" },
    [pscustomobject]@{ Label = "gpu-sky-92df8"; WordCount = 1; Portal = "0x76634"; Loaded = "0x92DF8" },
    [pscustomobject]@{ Label = "gpu-sky-92dfc"; WordCount = 1; Portal = "0x76638"; Loaded = "0x92DFC" },
    [pscustomobject]@{ Label = "gpu-sky-92e70"; WordCount = 1; Portal = "0x766AC"; Loaded = "0x92E70" },
    [pscustomobject]@{ Label = "gpu-sky-92f38"; WordCount = 1; Portal = "0x76774"; Loaded = "0x92F38" },
    [pscustomobject]@{ Label = "gpu-sky-934e0"; WordCount = 1; Portal = "0x76D1C"; Loaded = "0x934E0" },
    [pscustomobject]@{ Label = "gpu-sky-93ae8"; WordCount = 1; Portal = "0x77324"; Loaded = "0x93AE8" },
    [pscustomobject]@{ Label = "gpu-sky-93aec"; WordCount = 1; Portal = "0x77328"; Loaded = "0x93AEC" },
    [pscustomobject]@{ Label = "gpu-sky-93af8"; WordCount = 1; Portal = "0x77334"; Loaded = "0x93AF8" },
    [pscustomobject]@{ Label = "gpu-sky-93dec"; WordCount = 1; Portal = "0x77628"; Loaded = "0x93DEC" },
    [pscustomobject]@{ Label = "gpu-sky-93e0c"; WordCount = 1; Portal = "0x77648"; Loaded = "0x93E0C" },
    [pscustomobject]@{ Label = "gpu-sky-93e10"; WordCount = 1; Portal = "0x7764C"; Loaded = "0x93E10" },
    [pscustomobject]@{ Label = "gpu-sky-93e74"; WordCount = 1; Portal = "0x776B0"; Loaded = "0x93E74" },
    [pscustomobject]@{ Label = "gpu-sky-94adc"; WordCount = 1; Portal = "0x78318"; Loaded = "0x94ADC" },
    [pscustomobject]@{ Label = "gpu-sky-94cb8"; WordCount = 1; Portal = "0x784F4"; Loaded = "0x94CB8" },
    [pscustomobject]@{ Label = "gpu-sky-94d70"; WordCount = 1; Portal = "0x785AC"; Loaded = "0x94D70" },
    [pscustomobject]@{ Label = "gpu-sky-94d9c"; WordCount = 1; Portal = "0x785D8"; Loaded = "0x94D9C" },
    [pscustomobject]@{ Label = "gpu-sky-95d00"; WordCount = 1; Portal = "0x7953C"; Loaded = "0x95D00" },
    [pscustomobject]@{ Label = "gpu-sky-95e0c"; WordCount = 1; Portal = "0x79648"; Loaded = "0x95E0C" },
    [pscustomobject]@{ Label = "gpu-sky-95e14"; WordCount = 1; Portal = "0x79650"; Loaded = "0x95E14" },
    [pscustomobject]@{ Label = "gpu-sky-95e28"; WordCount = 1; Portal = "0x79664"; Loaded = "0x95E28" },
    [pscustomobject]@{ Label = "gpu-sky-96234"; WordCount = 1; Portal = "0x79A70"; Loaded = "0x96234" }
)

$targetBaseSkyLoaded = @(
    [pscustomobject]@{ Label = "base-loaded-0f18"; WordCount = 56; Loaded = "0x0F18" },
    [pscustomobject]@{ Label = "base-loaded-1260"; WordCount = 33; Loaded = "0x1260" },
    [pscustomobject]@{ Label = "base-loaded-1500"; WordCount = 20; Loaded = "0x1500" }
)

$targetBaseSkyPortal = @(
    [pscustomobject]@{ Label = "base-portal-0c30"; WordCount = 20; Portal = "0x0C30" },
    [pscustomobject]@{ Label = "base-portal-1170"; WordCount = 20; Portal = "0x1170" },
    [pscustomobject]@{ Label = "base-portal-2130"; WordCount = 20; Portal = "0x2130" },
    [pscustomobject]@{ Label = "base-portal-23d0"; WordCount = 20; Portal = "0x23D0" },
    [pscustomobject]@{ Label = "base-portal-2670"; WordCount = 20; Portal = "0x2670" },
    [pscustomobject]@{ Label = "base-portal-2910"; WordCount = 20; Portal = "0x2910" },
    [pscustomobject]@{ Label = "base-portal-2bb0"; WordCount = 20; Portal = "0x2BB0" }
)

$targetLoadedPeriwinkleA = @(
    [pscustomobject]@{ Label = "loaded-peri-a-8fb98"; WordCount = 1; Loaded = "0x8FB98" }
)

$targetLoadedPeriwinkleB = @(
    [pscustomobject]@{ Label = "loaded-peri-b-90b28"; WordCount = 2; Loaded = "0x90B28" },
    [pscustomobject]@{ Label = "loaded-peri-b-93ab8"; WordCount = 1; Loaded = "0x93AB8" }
)

$targetLoadedPeriwinkleC = @(
    [pscustomobject]@{ Label = "loaded-peri-c-94e08"; WordCount = 1; Loaded = "0x94E08" },
    [pscustomobject]@{ Label = "loaded-peri-c-9767c"; WordCount = 1; Loaded = "0x9767C" },
    [pscustomobject]@{ Label = "loaded-peri-c-97c44"; WordCount = 1; Loaded = "0x97C44" }
)

$targetFlyInPeriwinkleA = @(
    [pscustomobject]@{ Label = "flyin-peri-a-739d4"; WordCount = 1; Portal = "0x739D4" }
)

$targetFlyInPeriwinkleB = @(
    [pscustomobject]@{ Label = "flyin-peri-b-74364"; WordCount = 2; Portal = "0x74364" },
    [pscustomobject]@{ Label = "flyin-peri-b-772f4"; WordCount = 1; Portal = "0x772F4" }
)

$targetFlyInPeriwinkleC = @(
    [pscustomobject]@{ Label = "flyin-peri-c-78644"; WordCount = 1; Portal = "0x78644" },
    [pscustomobject]@{ Label = "flyin-peri-c-7aeb8"; WordCount = 1; Portal = "0x7AEB8" },
    [pscustomobject]@{ Label = "flyin-peri-c-7b480"; WordCount = 1; Portal = "0x7B480" }
)

$targetFlyInWaterA = @(
    [pscustomobject]@{ Label = "flyin-water-a-84010"; WordCount = 3; Portal = "0x84010" },
    [pscustomobject]@{ Label = "flyin-water-a-84020"; WordCount = 7; Portal = "0x84020" },
    [pscustomobject]@{ Label = "flyin-water-a-84720"; WordCount = 7; Portal = "0x84720" },
    [pscustomobject]@{ Label = "flyin-water-a-84774"; WordCount = 6; Portal = "0x84774" },
    [pscustomobject]@{ Label = "flyin-water-a-84790"; WordCount = 3; Portal = "0x84790" },
    [pscustomobject]@{ Label = "flyin-water-a-847a0"; WordCount = 4; Portal = "0x847A0" }
)

$targetFlyInWaterB = @(
    [pscustomobject]@{ Label = "flyin-water-b-855b8"; WordCount = 14; Portal = "0x855B8" },
    [pscustomobject]@{ Label = "flyin-water-b-855f4"; WordCount = 3; Portal = "0x855F4" },
    [pscustomobject]@{ Label = "flyin-water-b-85604"; WordCount = 8; Portal = "0x85604" },
    [pscustomobject]@{ Label = "flyin-water-b-85628"; WordCount = 7; Portal = "0x85628" },
    [pscustomobject]@{ Label = "flyin-water-b-85664"; WordCount = 3; Portal = "0x85664" },
    [pscustomobject]@{ Label = "flyin-water-b-86418"; WordCount = 5; Portal = "0x86418" },
    [pscustomobject]@{ Label = "flyin-water-b-8644c"; WordCount = 5; Portal = "0x8644C" },
    [pscustomobject]@{ Label = "flyin-water-b-8648c"; WordCount = 2; Portal = "0x8648C" },
    [pscustomobject]@{ Label = "flyin-water-b-87290"; WordCount = 21; Portal = "0x87290" },
    [pscustomobject]@{ Label = "flyin-water-b-872d8"; WordCount = 3; Portal = "0x872D8" }
)

$targetFlyInWaterC = @(
    [pscustomobject]@{ Label = "flyin-water-c-87c44"; WordCount = 4; Portal = "0x87C44" },
    [pscustomobject]@{ Label = "flyin-water-c-87c58"; WordCount = 5; Portal = "0x87C58" },
    [pscustomobject]@{ Label = "flyin-water-c-8a440"; WordCount = 5; Portal = "0x8A440" },
    [pscustomobject]@{ Label = "flyin-water-c-8a458"; WordCount = 7; Portal = "0x8A458" },
    [pscustomobject]@{ Label = "flyin-water-c-8a47c"; WordCount = 5; Portal = "0x8A47C" },
    [pscustomobject]@{ Label = "flyin-water-c-8abf0"; WordCount = 15; Portal = "0x8ABF0" },
    [pscustomobject]@{ Label = "flyin-water-c-8ac30"; WordCount = 4; Portal = "0x8AC30" },
    [pscustomobject]@{ Label = "flyin-water-c-8c0e8"; WordCount = 29; Portal = "0x8C0E8" }
)

$targetFlyInWaterAll = @($targetFlyInWaterA + $targetFlyInWaterB + $targetFlyInWaterC)

$targetFlyInLowerA = @(
    New-PortalSingleWordTargets "flyin-lower-a-58aa" @("0x58AA4", "0x58AA8", "0x58AAC", "0x58AB0", "0x58AB4", "0x58ABC", "0x58AC0", "0x58AC4", "0x58AC8", "0x58ACC", "0x58AD0")
    New-PortalSingleWordTargets "flyin-lower-a-58be" @("0x58BE4", "0x58C2C", "0x58C30", "0x58C34", "0x58C38", "0x58C3C", "0x58C44", "0x58C48", "0x58C4C", "0x58C50", "0x58C54", "0x58C58")
    New-PortalSingleWordTargets "flyin-lower-a-58d6" @("0x58D6C", "0x58D70", "0x58D74", "0x58D78", "0x58D7C", "0x58D80", "0x58D84", "0x58D88", "0x58D8C", "0x58D90", "0x58D94", "0x58D98", "0x58D9C", "0x58DA0", "0x58DA4")
    New-PortalSingleWordTargets "flyin-lower-a-58ed" @("0x58ED0", "0x58ED4", "0x58ED8", "0x58EDC", "0x58EE0", "0x58EE4", "0x58EE8", "0x58EEC", "0x58EF0", "0x58F00", "0x58F04", "0x58F08", "0x58F0C")
    New-PortalSingleWordTargets "flyin-lower-a-5905" @("0x59050", "0x59054", "0x59058", "0x5905C", "0x59068", "0x5906C")
    New-PortalSingleWordTargets "flyin-lower-a-591a" @("0x591AC", "0x591B0", "0x591B4", "0x591B8", "0x591BC", "0x591C0", "0x591C4", "0x591CC")
    New-PortalSingleWordTargets "flyin-lower-a-592d" @("0x592D0", "0x592D4", "0x592D8", "0x592DC", "0x592E0", "0x592E4", "0x592E8", "0x592EC", "0x592F0", "0x592F4", "0x592F8", "0x592FC")
    New-PortalSingleWordTargets "flyin-lower-a-5957" @("0x59578", "0x5957C", "0x59580", "0x59588", "0x5958C", "0x59590", "0x5959C", "0x595A4", "0x595A8", "0x595AC", "0x595B4", "0x595BC", "0x595C0", "0x595C8", "0x595D0")
)

$targetFlyInLowerB = @(
    New-PortalSingleWordTargets "flyin-lower-b-59c4" @("0x59C4C", "0x59C88", "0x59CD4", "0x59CD8", "0x59CDC", "0x59CE0", "0x59CE4", "0x59CE8", "0x59CEC", "0x59CF0", "0x59CF8", "0x59CFC", "0x59D00", "0x59D04", "0x59D08", "0x59D0C", "0x59D10", "0x59D14", "0x59D20", "0x59D24", "0x59D28")
    New-PortalSingleWordTargets "flyin-lower-b-5a4e" @("0x5A4E4", "0x5A4F0", "0x5A504", "0x5A508", "0x5A510", "0x5A514", "0x5A518", "0x5A51C", "0x5A520", "0x5A524", "0x5A528", "0x5A52C", "0x5A530")
    New-PortalSingleWordTargets "flyin-lower-b-5ac7" @("0x5AC74", "0x5ACC0", "0x5ACE0", "0x5AD0C", "0x5AD10", "0x5AD18", "0x5AD1C", "0x5AD20", "0x5AD24", "0x5AD28", "0x5AD2C", "0x5AD34", "0x5AD38", "0x5AD3C")
    New-PortalSingleWordTargets "flyin-lower-b-5b44" @("0x5B44C", "0x5B450", "0x5B454", "0x5B458", "0x5B45C", "0x5B460", "0x5B464", "0x5B468", "0x5B46C", "0x5B470", "0x5B474", "0x5B478", "0x5B47C", "0x5B480", "0x5B484", "0x5B48C", "0x5B490", "0x5B494", "0x5B498", "0x5B49C")
)

$targetFlyInLowerC = @(
    New-PortalSingleWordTargets "flyin-lower-c-5bd9" @("0x5BD94", "0x5BD98", "0x5BD9C", "0x5BDA0", "0x5BDA4", "0x5BDA8", "0x5BDAC", "0x5BDB0", "0x5BDB4", "0x5BDB8", "0x5BDBC", "0x5BDC0", "0x5BDC4", "0x5BDC8", "0x5BDCC", "0x5BDD4", "0x5BDD8", "0x5BDDC")
    New-PortalSingleWordTargets "flyin-lower-c-5c4c" @("0x5C4C8", "0x5C4CC", "0x5C4D0", "0x5C4D4", "0x5C4D8", "0x5C4DC", "0x5C4E0", "0x5C4E4", "0x5C4E8", "0x5C4EC", "0x5C4F0", "0x5C4F4", "0x5C4F8", "0x5C4FC", "0x5C500")
    New-PortalSingleWordTargets "flyin-lower-c-5cc9" @("0x5CC9C", "0x5CCA4", "0x5CCA8", "0x5CCB4", "0x5CCB8", "0x5CCBC", "0x5CCC0", "0x5CCC4", "0x5CCC8", "0x5CCCC", "0x5CCD0", "0x5CCD4", "0x5CCD8", "0x5CCDC", "0x5CCE0", "0x5CCE4", "0x5CCE8", "0x5CCEC", "0x5CCF4", "0x5CCF8", "0x5CD00", "0x5CD04", "0x5CD08", "0x5CD0C", "0x5CD10")
)

$targetFlyInLowerD = @(
    New-PortalSingleWordTargets "flyin-lower-d-5d79" @("0x5D790", "0x5D798", "0x5D79C", "0x5D7A0", "0x5D7A4", "0x5D7A8", "0x5D7AC", "0x5D7B4", "0x5D7B8", "0x5D7BC", "0x5D7C0", "0x5D7C4", "0x5D7D0")
    New-PortalSingleWordTargets "flyin-lower-d-5e1a" @("0x5E1A4", "0x5E1AC", "0x5E1BC", "0x5E1C0", "0x5E1C4", "0x5E1CC", "0x5E1D0", "0x5E1D4", "0x5E1D8", "0x5E1DC", "0x5E1E0", "0x5E1E4", "0x5E1E8", "0x5E1EC", "0x5E1F0")
    New-PortalSingleWordTargets "flyin-lower-d-5ea1" @("0x5EA1C", "0x5EA30", "0x5EA34", "0x5EA38", "0x5EA50", "0x5EA54", "0x5EA58", "0x5EA5C", "0x5EA60", "0x5EA64", "0x5EA68", "0x5EA6C", "0x5EA70", "0x5EA74")
)

$targetFlyInLowerAll = @($targetFlyInLowerA + $targetFlyInLowerB + $targetFlyInLowerC + $targetFlyInLowerD)

$targetSkyBandCleanupA = @(
    [pscustomobject]@{ Label = "skyband-a-93048"; WordCount = 59; Portal = "0x76884"; Loaded = "0x93048" },
    [pscustomobject]@{ Label = "skyband-a-93fec"; WordCount = 15; Portal = "0x77828"; Loaded = "0x93FEC" },
    [pscustomobject]@{ Label = "skyband-a-9184c"; WordCount = 12; Portal = "0x75088"; Loaded = "0x9184C" },
    [pscustomobject]@{ Label = "skyband-a-92a9c"; WordCount = 11; Portal = "0x762D8"; Loaded = "0x92A9C" },
    [pscustomobject]@{ Label = "skyband-a-963b4"; WordCount = 11; Portal = "0x79BF0"; Loaded = "0x963B4" }
)

$targetSkyBandCleanupB = @(
    [pscustomobject]@{ Label = "skyband-b-915dc"; WordCount = 10; Portal = "0x74E18"; Loaded = "0x915DC" },
    [pscustomobject]@{ Label = "skyband-b-901fc"; WordCount = 10; Portal = "0x73A38"; Loaded = "0x901FC" },
    [pscustomobject]@{ Label = "skyband-b-94ffc"; WordCount = 10; Portal = "0x78838"; Loaded = "0x94FFC" },
    [pscustomobject]@{ Label = "skyband-b-917bc"; WordCount = 9; Portal = "0x74FF8"; Loaded = "0x917BC" },
    [pscustomobject]@{ Label = "skyband-b-92b58"; WordCount = 9; Portal = "0x76394"; Loaded = "0x92B58" }
)

$targetSkyBandCleanupC = @(
    [pscustomobject]@{ Label = "skyband-c-94340"; WordCount = 9; Portal = "0x77B7C"; Loaded = "0x94340" },
    [pscustomobject]@{ Label = "skyband-c-959e0"; WordCount = 9; Portal = "0x7921C"; Loaded = "0x959E0" },
    [pscustomobject]@{ Label = "skyband-c-9011c"; WordCount = 9; Portal = "0x73958"; Loaded = "0x9011C" },
    [pscustomobject]@{ Label = "skyband-c-92948"; WordCount = 8; Portal = "0x76184"; Loaded = "0x92948" },
    [pscustomobject]@{ Label = "skyband-c-91754"; WordCount = 8; Portal = "0x74F90"; Loaded = "0x91754" }
)

$targetWaterBands = @(
    [pscustomobject]@{ Label = "waterband-0ae80"; WordCount = 2; Loaded = "0xAE80" },
    [pscustomobject]@{ Label = "waterband-0aeb0"; WordCount = 2; Loaded = "0xAEB0" },
    [pscustomobject]@{ Label = "waterband-0b8b0"; WordCount = 2; Loaded = "0xB8B0" },
    [pscustomobject]@{ Label = "waterband-21328"; WordCount = 3; Loaded = "0x21328" },
    [pscustomobject]@{ Label = "waterband-215dc"; WordCount = 2; Loaded = "0x215DC" },
    [pscustomobject]@{ Label = "waterband-269c4"; WordCount = 2; Loaded = "0x269C4" },
    [pscustomobject]@{ Label = "waterband-2ec9c"; WordCount = 2; Loaded = "0x2EC9C" },
    [pscustomobject]@{ Label = "waterband-3c1b4"; WordCount = 2; Loaded = "0x3C1B4" },
    [pscustomobject]@{ Label = "waterband-409d4"; WordCount = 2; Loaded = "0x409D4" },
    [pscustomobject]@{ Label = "waterband-8c6a4"; WordCount = 2; Loaded = "0x8C6A4" },
    [pscustomobject]@{ Label = "flyin-water-84010"; WordCount = 3; Portal = "0x84010" },
    [pscustomobject]@{ Label = "flyin-water-84020"; WordCount = 7; Portal = "0x84020" },
    [pscustomobject]@{ Label = "flyin-water-84720"; WordCount = 7; Portal = "0x84720" },
    [pscustomobject]@{ Label = "flyin-water-84774"; WordCount = 6; Portal = "0x84774" },
    [pscustomobject]@{ Label = "flyin-water-84790"; WordCount = 3; Portal = "0x84790" },
    [pscustomobject]@{ Label = "flyin-water-847a0"; WordCount = 4; Portal = "0x847A0" },
    [pscustomobject]@{ Label = "flyin-water-855b8"; WordCount = 14; Portal = "0x855B8" },
    [pscustomobject]@{ Label = "flyin-water-855f4"; WordCount = 3; Portal = "0x855F4" },
    [pscustomobject]@{ Label = "flyin-water-85604"; WordCount = 8; Portal = "0x85604" },
    [pscustomobject]@{ Label = "flyin-water-85628"; WordCount = 7; Portal = "0x85628" },
    [pscustomobject]@{ Label = "flyin-water-85664"; WordCount = 3; Portal = "0x85664" },
    [pscustomobject]@{ Label = "flyin-water-86418"; WordCount = 5; Portal = "0x86418" },
    [pscustomobject]@{ Label = "flyin-water-8644c"; WordCount = 5; Portal = "0x8644C" },
    [pscustomobject]@{ Label = "flyin-water-8648c"; WordCount = 2; Portal = "0x8648C" },
    [pscustomobject]@{ Label = "flyin-water-87290"; WordCount = 21; Portal = "0x87290" },
    [pscustomobject]@{ Label = "flyin-water-872d8"; WordCount = 3; Portal = "0x872D8" },
    [pscustomobject]@{ Label = "flyin-water-87c44"; WordCount = 4; Portal = "0x87C44" },
    [pscustomobject]@{ Label = "flyin-water-87c58"; WordCount = 5; Portal = "0x87C58" },
    [pscustomobject]@{ Label = "flyin-water-8a440"; WordCount = 5; Portal = "0x8A440" },
    [pscustomobject]@{ Label = "flyin-water-8a458"; WordCount = 7; Portal = "0x8A458" },
    [pscustomobject]@{ Label = "flyin-water-8a47c"; WordCount = 5; Portal = "0x8A47C" },
    [pscustomobject]@{ Label = "flyin-water-8abf0"; WordCount = 15; Portal = "0x8ABF0" },
    [pscustomobject]@{ Label = "flyin-water-8ac30"; WordCount = 4; Portal = "0x8AC30" },
    [pscustomobject]@{ Label = "flyin-water-8c0e8"; WordCount = 29; Portal = "0x8C0E8" }
)

$targetBestWaterA = @(
    [pscustomobject]@{ Label = "best-water-a-0ae80"; WordCount = 2; Loaded = "0xAE80" },
    [pscustomobject]@{ Label = "best-water-a-0aeb0"; WordCount = 2; Loaded = "0xAEB0" },
    [pscustomobject]@{ Label = "best-water-a-0b8b0"; WordCount = 2; Loaded = "0xB8B0" }
)

$targetBestWaterB = @(
    [pscustomobject]@{ Label = "best-water-b-21328"; WordCount = 3; Loaded = "0x21328" },
    [pscustomobject]@{ Label = "best-water-b-215dc"; WordCount = 2; Loaded = "0x215DC" },
    [pscustomobject]@{ Label = "best-water-b-269c4"; WordCount = 2; Loaded = "0x269C4" }
)

$targetBestWaterC = @(
    [pscustomobject]@{ Label = "best-water-c-2ec9c"; WordCount = 2; Loaded = "0x2EC9C" },
    [pscustomobject]@{ Label = "best-water-c-3c1b4"; WordCount = 2; Loaded = "0x3C1B4" },
    [pscustomobject]@{ Label = "best-water-c-409d4"; WordCount = 2; Loaded = "0x409D4" }
)

$targetBestWaterABC = @($targetBestWaterA + $targetBestWaterB + $targetBestWaterC)
$targetBestWaterABCDeep = Add-TargetRgbOverride $targetBestWaterABC "#061B3D"
$targetFlyInPeriwinkleADeep = Add-TargetRgbOverride $targetFlyInPeriwinkleA "#061B3D"

$targetMoonDiscWord03 = @(
    [pscustomobject]@{ Label = "moon-upper13-word03"; WordCount = 1; Portal = "0x78344"; Loaded = "0x94B08"; RgbHex = "#D8DDFF" }
)

$targetAtmosphereClouds = @(
    [pscustomobject]@{ Label = "atmo-cloud-73d6c"; WordCount = 17; Portal = "0x73D6C"; Loaded = "0x90530"; RgbHex = "#293B98" },
    [pscustomobject]@{ Label = "atmo-cloud-75350"; WordCount = 21; Portal = "0x75350"; Loaded = "0x91B14"; RgbHex = "#344AC3" },
    [pscustomobject]@{ Label = "atmo-cloud-75890"; WordCount = 17; Portal = "0x75890"; Loaded = "0x92054"; RgbHex = "#253985" },
    [pscustomobject]@{ Label = "atmo-cloud-75eec"; WordCount = 25; Portal = "0x75EEC"; Loaded = "0x926B0"; RgbHex = "#4158D6" },
    [pscustomobject]@{ Label = "atmo-cloud-76798"; WordCount = 20; Portal = "0x76798"; Loaded = "0x92F5C"; RgbHex = "#233780" },
    [pscustomobject]@{ Label = "atmo-cloud-77368"; WordCount = 18; Portal = "0x77368"; Loaded = "0x93B2C"; RgbHex = "#3046AE" },
    [pscustomobject]@{ Label = "atmo-cloud-77788"; WordCount = 20; Portal = "0x77788"; Loaded = "0x93F4C"; RgbHex = "#263B91" },
    [pscustomobject]@{ Label = "atmo-cloud-77e60"; WordCount = 16; Portal = "0x77E60"; Loaded = "0x94624"; RgbHex = "#3A51C8" },
    [pscustomobject]@{ Label = "atmo-cloud-79090"; WordCount = 18; Portal = "0x79090"; Loaded = "0x95854"; RgbHex = "#2B419A" },
    [pscustomobject]@{ Label = "atmo-cloud-7a86c"; WordCount = 16; Portal = "0x7A86C"; Loaded = "0x97030"; RgbHex = "#354DBB" }
)

$targetStarProbe = @(
    [pscustomobject]@{ Label = "star-probe-loaded-7b9bc"; WordCount = 1; Loaded = "0x7B9BC"; RgbHex = "#CBD7FF" },
    [pscustomobject]@{ Label = "star-probe-loaded-81c50"; WordCount = 1; Loaded = "0x81C50"; RgbHex = "#E3E8FF" },
    [pscustomobject]@{ Label = "star-probe-loaded-8bd40"; WordCount = 1; Loaded = "0x8BD40"; RgbHex = "#CBD7FF" },
    [pscustomobject]@{ Label = "star-probe-loaded-8c418"; WordCount = 1; Loaded = "0x8C418"; RgbHex = "#B9C7FF" },
    [pscustomobject]@{ Label = "star-probe-loaded-93540"; WordCount = 1; Loaded = "0x93540"; RgbHex = "#DDE4FF" },
    [pscustomobject]@{ Label = "star-probe-loaded-95e40"; WordCount = 1; Loaded = "0x95E40"; RgbHex = "#C7D3FF" }
)

$targetFullNight = @(
    @(
        [pscustomobject]@{ Label = "portal-bg-00"; WordCount = 7; Portal = "0x72060" },
        [pscustomobject]@{ Label = "portal-bg-01"; WordCount = 4; Portal = "0x722C4" },
        [pscustomobject]@{ Label = "portal-bg-02"; WordCount = 4; Portal = "0x724D0" },
        [pscustomobject]@{ Label = "portal-bg-03"; WordCount = 4; Portal = "0x72674" },
        [pscustomobject]@{ Label = "portal-bg-04"; WordCount = 5; Portal = "0x72860" },
        [pscustomobject]@{ Label = "portal-bg-05"; WordCount = 6; Portal = "0x72B38" },
        [pscustomobject]@{ Label = "portal-bg-06"; WordCount = 7; Portal = "0x72CE8" },
        [pscustomobject]@{ Label = "portal-bg-07"; WordCount = 5; Portal = "0x72E94" },
        [pscustomobject]@{ Label = "portal-bg-08"; WordCount = 6; Portal = "0x73168" },
        [pscustomobject]@{ Label = "cloud-73d6c"; WordCount = 17; Portal = "0x73D6C"; Loaded = "0x90530" },
        [pscustomobject]@{ Label = "cloud-75350"; WordCount = 21; Portal = "0x75350"; Loaded = "0x91B14" },
        [pscustomobject]@{ Label = "cloud-75890"; WordCount = 17; Portal = "0x75890"; Loaded = "0x92054" },
        [pscustomobject]@{ Label = "cloud-75eec"; WordCount = 25; Portal = "0x75EEC"; Loaded = "0x926B0" },
        [pscustomobject]@{ Label = "cloud-76798"; WordCount = 20; Portal = "0x76798"; Loaded = "0x92F5C" },
        [pscustomobject]@{ Label = "cloud-77368"; WordCount = 18; Portal = "0x77368"; Loaded = "0x93B2C" },
        [pscustomobject]@{ Label = "cloud-77788"; WordCount = 20; Portal = "0x77788"; Loaded = "0x93F4C" },
        [pscustomobject]@{ Label = "blue-77c64"; WordCount = 6; Portal = "0x77C64"; Loaded = "0x94428" },
        [pscustomobject]@{ Label = "cloud-77e60"; WordCount = 16; Portal = "0x77E60"; Loaded = "0x94624" },
        [pscustomobject]@{ Label = "blue-78ed8"; WordCount = 5; Portal = "0x78ED8"; Loaded = "0x9569C" },
        [pscustomobject]@{ Label = "cloud-79090"; WordCount = 18; Portal = "0x79090"; Loaded = "0x95854" },
        [pscustomobject]@{ Label = "blue-79418"; WordCount = 6; Portal = "0x79418"; Loaded = "0x95BDC" },
        [pscustomobject]@{ Label = "blue-79550"; WordCount = 10; Portal = "0x79550"; Loaded = "0x95D14" },
        [pscustomobject]@{ Label = "backdrop-79694"; WordCount = 7; Portal = "0x79694"; Loaded = "0x95E58" },
        [pscustomobject]@{ Label = "wedge-797f4"; WordCount = 2; Portal = "0x797F4"; Loaded = "0x95FB8" },
        [pscustomobject]@{ Label = "bright-7a284"; WordCount = 5; Portal = "0x7A284"; Loaded = "0x96A48" },
        [pscustomobject]@{ Label = "blue-7a518"; WordCount = 4; Portal = "0x7A518"; Loaded = "0x96CDC" },
        [pscustomobject]@{ Label = "wedge-7a5f4"; WordCount = 2; Portal = "0x7A5F4"; Loaded = "0x96DB8" },
        [pscustomobject]@{ Label = "cloud-7a86c"; WordCount = 16; Portal = "0x7A86C"; Loaded = "0x97030" },
        [pscustomobject]@{ Label = "blue-7aed0"; WordCount = 7; Portal = "0x7AED0"; Loaded = "0x97694" },
        [pscustomobject]@{ Label = "wedge-7af68"; WordCount = 2; Portal = "0x7AF68"; Loaded = "0x9772C" },
        [pscustomobject]@{ Label = "wedge-7afe0"; WordCount = 2; Portal = "0x7AFE0"; Loaded = "0x977A4" },
        [pscustomobject]@{ Label = "blue-7b168"; WordCount = 9; Portal = "0x7B168"; Loaded = "0x9792C" },
        [pscustomobject]@{ Label = "blue-7b2e8"; WordCount = 8; Portal = "0x7B2E8"; Loaded = "0x97AAC" },
        [pscustomobject]@{ Label = "bright-7b4b4"; WordCount = 5; Portal = "0x7B4B4"; Loaded = "0x97C78" },
        [pscustomobject]@{ Label = "blue-7b708"; WordCount = 5; Portal = "0x7B708"; Loaded = "0x97ECC" }
    ) +
    $targetFlyInSlabs +
    $targetFlyInTriangles +
    $targetSpotCleanup +
    $targetRemainingTriangles +
    $targetDeepNight
)

function Get-RecordTargetPairs($Record) {
    $pairs = New-Object Collections.Generic.List[object]
    if ($Record.PSObject.Properties.Name -contains "Portal" -and -not [string]::IsNullOrWhiteSpace([string]$Record.Portal)) {
        [void]$pairs.Add(@{ Entry = 10; Subfile = 1; Start = [int64](Convert-HexToInt64 ([string]$Record.Portal)); Scope = "portal" })
    }
    if ($Record.PSObject.Properties.Name -contains "Loaded" -and -not [string]::IsNullOrWhiteSpace([string]$Record.Loaded)) {
        [void]$pairs.Add(@{ Entry = 12; Subfile = 1; Start = [int64](Convert-HexToInt64 ([string]$Record.Loaded)); Scope = "loaded" })
    }
    return @($pairs.ToArray())
}

$presetSlug = $Preset.ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($OutputPrefix)) {
    $OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-skycolors-$presetSlug"
}
$outImage = Resolve-WorkspacePath ("{0}.bin" -f $OutputPrefix)
$outCue = Resolve-WorkspacePath ("{0}.cue" -f $OutputPrefix)
$outPlan = Resolve-WorkspacePath ("{0}.skyprimitive-color-palette-plan.json" -f $OutputPrefix)

if ($Preset -eq "Custom") {
    $palette = Parse-PaletteHex $PaletteHex
    $targets = $targetRecords
}
elseif ($Preset -eq "StoneHillNight") {
    $palette = Parse-PaletteHex "#081132 #111D4D #1F316F #314A8C #667CA8 #9DAED0 #CDD5EA #2B235F"
    $targets = $targetRecords
}
elseif ($Preset -eq "StoneHillBestNight") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA)
}
elseif ($Preset -eq "StoneHillNightKeeper") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterABCDeep + $targetFlyInPeriwinkleADeep)
}
elseif ($Preset -eq "StoneHillMoonAtmosphere") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterABCDeep + $targetFlyInPeriwinkleADeep + $targetMoonDiscWord03 + $targetAtmosphereClouds)
}
elseif ($Preset -eq "StoneHillMoonStarProbe") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterABCDeep + $targetFlyInPeriwinkleADeep + $targetMoonDiscWord03 + $targetAtmosphereClouds + $targetStarProbe)
}
elseif ($Preset -eq "StoneHillFlyInPeriA") {
    $palette = New-FlatNightPalette
    $targets = $targetFlyInPeriwinkleA
}
elseif ($Preset -eq "StoneHillFlyInPeriB") {
    $palette = New-FlatNightPalette
    $targets = $targetFlyInPeriwinkleB
}
elseif ($Preset -eq "StoneHillFlyInPeriC") {
    $palette = New-FlatNightPalette
    $targets = $targetFlyInPeriwinkleC
}
elseif ($Preset -eq "StoneHillFlyInWaterA") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInWaterA
}
elseif ($Preset -eq "StoneHillFlyInWaterB") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInWaterB
}
elseif ($Preset -eq "StoneHillFlyInWaterC") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInWaterC
}
elseif ($Preset -eq "StoneHillFlyInWaterAll") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInWaterAll
}
elseif ($Preset -eq "StoneHillFlyInLowerA") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInLowerA
}
elseif ($Preset -eq "StoneHillFlyInLowerB") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInLowerB
}
elseif ($Preset -eq "StoneHillFlyInLowerC") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInLowerC
}
elseif ($Preset -eq "StoneHillFlyInLowerD") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInLowerD
}
elseif ($Preset -eq "StoneHillFlyInLowerAll") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetFlyInLowerAll
}
elseif ($Preset -eq "StoneHillBestWaterA") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterA)
}
elseif ($Preset -eq "StoneHillBestWaterB") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterB)
}
elseif ($Preset -eq "StoneHillBestWaterC") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterC)
}
elseif ($Preset -eq "StoneHillBestWaterABC") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterABC)
}
elseif ($Preset -eq "StoneHillBestWaterABCFlyInPeriA") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA + $targetBestWaterABC + $targetFlyInPeriwinkleA)
}
elseif ($Preset -eq "StoneHillWaterOnlyABC") {
    $palette = New-FlatNightPalette
    $targets = $targetBestWaterABC
}
elseif ($Preset -eq "StoneHillWaterOnlyABCDeep") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = $targetBestWaterABC
}
elseif ($Preset -eq "StoneHillWaterOnlyABCMidnight") {
    $palette = Parse-PaletteHex "#020A1E"
    $targets = $targetBestWaterABC
}
elseif ($Preset -eq "StoneHillWaterOnlyABCDeepFlyInPeriA") {
    $palette = Parse-PaletteHex "#061B3D"
    $targets = @($targetBestWaterABC + $targetFlyInPeriwinkleA)
}
elseif ($Preset -eq "StoneHillStableNight") {
    $palette = New-SmoothNightPalette
    $targets = $targetDeepNight
}
elseif ($Preset -eq "StoneHillDeepNight") {
    $palette = New-NightGradePalette
    $targets = $targetDeepNight
}
elseif ($Preset -eq "StoneHillFullNight") {
    $palette = New-NightGradePalette
    $targets = $targetFullNight
}
elseif ($Preset -eq "StoneHillSmoothNight") {
    $palette = New-SmoothNightPalette
    $targets = $targetFullNight
}
elseif ($Preset -eq "StoneHillFlatNight") {
    $palette = New-FlatNightPalette
    $targets = $targetFullNight
}
elseif ($Preset -eq "StoneHillBaseSkyLoaded") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded)
}
elseif ($Preset -eq "StoneHillBaseSkyPortal") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyPortal)
}
elseif ($Preset -eq "StoneHillBaseSkyCombined") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal)
}
elseif ($Preset -eq "StoneHillLoadedPeriA") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleA)
}
elseif ($Preset -eq "StoneHillLoadedPeriB") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleB)
}
elseif ($Preset -eq "StoneHillLoadedPeriC") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetBaseSkyLoaded + $targetBaseSkyPortal + $targetLoadedPeriwinkleC)
}
elseif ($Preset -eq "StoneHillGpuCleanNight") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetGpuSkyCleanup)
}
elseif ($Preset -eq "StoneHillBandCleanA") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetSkyBandCleanupA)
}
elseif ($Preset -eq "StoneHillBandCleanB") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetSkyBandCleanupB)
}
elseif ($Preset -eq "StoneHillBandCleanC") {
    $palette = New-FlatNightPalette
    $targets = @($targetFullNight + $targetSkyBandCleanupC)
}
elseif ($Preset -eq "DarkHollowDark") {
    $palette = New-DonorWordPalette "Dark" $RecordReportPath
    $targets = $targetRecords
}
elseif ($Preset -eq "DarkHollowMid") {
    $palette = New-DonorWordPalette "Mid" $RecordReportPath
    $targets = $targetRecords
}
elseif ($Preset -eq "DarkHollowPlus19Dark") {
    $palette = New-DonorWordPalette "Dark" $RecordReportPath
    $targets = @($targetRecords + $targetExtras)
}
else {
    $palette = New-DonorWordPalette "Mixed" $RecordReportPath
    $targets = $targetRecords
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$wadAnalysisPath = Resolve-WorkspacePath $WadAnalysisPath
foreach ($path in @($sourceImage, $wadAnalysisPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $path" }
}

$layout = Detect-DiscLayout $sourceImage
$wadAnalysis = Get-Content -Raw -LiteralPath $wadAnalysisPath | ConvertFrom-Json
$targetSubfiles = @{
    "10|1" = Get-WadSubfile $wadAnalysis 10 1
    "12|1" = Get-WadSubfile $wadAnalysis 12 1
}
$donorSubfiles = @{}

$patches = New-Object Collections.Generic.List[object]
$payloads = New-Object Collections.Generic.List[object]
$paletteItems = @($palette)
$cursor = 0

$stream = [IO.File]::OpenRead($sourceImage)
try {
    foreach ($record in @($targets)) {
        for ($word = 0; $word -lt [int]$record.WordCount; $word++) {
            $paletteItem = $paletteItems[$cursor % $paletteItems.Count]
            foreach ($targetPair in @(Get-RecordTargetPairs $record)) {
                $targetSubfile = $targetSubfiles["$($targetPair.Entry)|$($targetPair.Subfile)"]
                $targetStart = [int64]$targetPair.Start + ([int64]$word * 4)
                if ($targetStart -lt 0 -or ($targetStart + 4) -gt [int64]$targetSubfile.size) {
                    throw "Target range $($targetPair.Entry)|$($targetPair.Subfile)|0x$($targetStart.ToString('X')) is outside the subfile."
                }
                $targetWadOffset = [int64]$targetSubfile.wadOffset + $targetStart
                $beforeBytes = Read-WadBytes $stream $layout $targetWadOffset 4
                $afterBytes = New-Object byte[] 4
                $paletteKindForPlan = [string]$paletteItem.kind
                $paletteColorForPlan = [string]$paletteItem.color
                if ($record.PSObject.Properties.Name -contains "RgbHex" -and -not [string]::IsNullOrWhiteSpace([string]$record.RgbHex)) {
                    [byte[]]$rgb = Convert-HexColorToRgbBytes ([string]$record.RgbHex)
                    $afterBytes[0] = $rgb[0]
                    $afterBytes[1] = $rgb[1]
                    $afterBytes[2] = $rgb[2]
                    $paletteKindForPlan = "target-literal"
                    $paletteColorForPlan = [string]$record.RgbHex
                }
                elseif ([string]$paletteItem.kind -eq "donor") {
                    $donorKey = "$($paletteItem.donorEntry)|$($paletteItem.donorSubfile)"
                    if (-not $donorSubfiles.ContainsKey($donorKey)) {
                        $donorSubfiles[$donorKey] = Get-WadSubfile $wadAnalysis ([int]$paletteItem.donorEntry) ([int]$paletteItem.donorSubfile)
                    }
                    $donorSubfile = $donorSubfiles[$donorKey]
                    $donorWadOffset = [int64]$donorSubfile.wadOffset + [int64]$paletteItem.donorStart
                    $donorBytes = Read-WadBytes $stream $layout $donorWadOffset 4
                    $afterBytes[0] = $donorBytes[0]
                    $afterBytes[1] = $donorBytes[1]
                    $afterBytes[2] = $donorBytes[2]
                }
                elseif ([string]$paletteItem.kind -eq "night-grade") {
                    [byte[]]$rgb = if ([string]$record.Label -like "*water*") {
                        Convert-RgbToWaterNightBytes $beforeBytes
                    }
                    else {
                        Convert-RgbToNightBytes $beforeBytes
                    }
                    $afterBytes[0] = $rgb[0]
                    $afterBytes[1] = $rgb[1]
                    $afterBytes[2] = $rgb[2]
                }
                elseif ([string]$paletteItem.kind -eq "smooth-night-grade") {
                    [byte[]]$rgb = Convert-RgbToSmoothNightBytes $beforeBytes
                    $afterBytes[0] = $rgb[0]
                    $afterBytes[1] = $rgb[1]
                    $afterBytes[2] = $rgb[2]
                }
                elseif ([string]$paletteItem.kind -eq "flat-night") {
                    $afterBytes[0] = 0x14
                    $afterBytes[1] = 0x31
                    $afterBytes[2] = 0x66
                }
                else {
                    [byte[]]$rgb = $paletteItem.rgb
                    $afterBytes[0] = $rgb[0]
                    $afterBytes[1] = $rgb[1]
                    $afterBytes[2] = $rgb[2]
                }
                $afterBytes[3] = $beforeBytes[3]

                [void]$patches.Add([pscustomobject][ordered]@{
                    label = "$($targetPair.Scope)-$Preset-$($record.Label)-w$('{0:00}' -f $word)"
                    kind = "sky-primitive-color-palette"
                    target = "entry-$($targetPair.Entry)-subfile-$($targetPair.Subfile)"
                    targetSubfileOffset = ("0x{0:X}" -f $targetStart)
                    byteLength = 4
                    targetWadOffset = ("0x{0:X}" -f $targetWadOffset)
                    targetImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $layout $targetWadOffset))
                    paletteKind = $paletteKindForPlan
                    paletteColor = $paletteColorForPlan
                    beforeHexPreview = Convert-BytesToHex $beforeBytes 4
                    afterHexPreview = Convert-BytesToHex $afterBytes 4
                })
                [void]$payloads.Add([pscustomobject][ordered]@{
                    wadOffset = $targetWadOffset
                    bytes = $afterBytes
                })
            }
            $cursor++
        }
    }
}
finally {
    $stream.Dispose()
}

$plan = [pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    sourceImagePath = $sourceImage
    outputImagePath = $outImage
    outputCuePath = $outCue
    wadAnalysisPath = $wadAnalysisPath
    preset = $Preset
    paletteInput = $PaletteHex
    targetRecordCount = @($targets).Count
    paletteWordCount = @($paletteItems).Count
    patchCount = $patches.Count
    totalPatchedBytes = [int](($patches | Measure-Object -Property byteLength -Sum).Sum)
    patches = @($patches.ToArray())
}

$planDir = Split-Path -Parent $outPlan
if (-not [string]::IsNullOrWhiteSpace($planDir)) {
    New-Item -ItemType Directory -Force -Path $planDir | Out-Null
}
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPlan -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $sourceImage -Destination $outImage -Force
    $outStream = [IO.File]::Open($outImage, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read)
    try {
        foreach ($payload in @($payloads.ToArray())) {
            Write-WadBytes $outStream $layout ([int64]$payload.wadOffset) ([byte[]]$payload.bytes)
        }
    }
    finally {
        $outStream.Dispose()
    }
    $cueText = Get-CueText $sourceCue ([IO.Path]::GetFileName($outImage))
    $cueText | Set-Content -LiteralPath $outCue -Encoding ASCII
}

Write-Host "Wrote $outPlan"
if (-not $PlanOnly) {
    Write-Host "Wrote $outImage"
    Write-Host "Wrote $outCue"
}
Write-Host "Sky color patches: $($patches.Count), bytes: $($plan.totalPatchedBytes)"
