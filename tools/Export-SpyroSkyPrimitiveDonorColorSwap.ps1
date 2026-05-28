param(
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [string[]]$TargetPatchSpecs = @(),
    [string[]]$TargetWindowSpecs = @(),
    [int]$DonorEntryIndex = 58,
    [int]$DonorSubfileIndex = 1,
    [string]$DonorStartOffset = "0x70000",
    [string]$DonorEndOffset = "0x93000",
    [ValidateSet("SkyBright", "AllPrimitiveWords")]
    [string]$DonorFilter = "SkyBright",
    [ValidateSet("Any", "SkyPastel")]
    [string]$TargetColorFilter = "Any",
    [string[]]$TargetIncludeColorHexes = @(),
    [string[]]$TargetExcludeColorHexes = @(),
    [string[]]$TargetCommandHexes = @(),
    [ValidateSet("Resample", "Sequential", "StablePaletteBrightness")]
    [string]$MappingMode = "StablePaletteBrightness",
    [string]$StablePaletteSeedPlanPath = "",
    [int]$MaxTargetPatches = 512,
    [int]$MaxDonorCandidates = 2048,
    [string]$OutImagePath = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-skyprimitive-colors.bin",
    [string]$OutCuePath = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-skyprimitive-colors.cue",
    [string]$OutPlanPath = "",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37

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

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
    }
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

function Test-DonorColorCandidate([byte]$R, [byte]$G, [byte]$B, [byte]$Command, [string]$Filter) {
    if (-not ($Command -in 0x20, 0x28, 0x30, 0x38)) { return $false }
    if ($Filter -eq "AllPrimitiveWords") { return $true }

    $brightness = [int]$R + [int]$G + [int]$B
    $blueDominant = [int]$B -ge [Math]::Max([int]$R, [int]$G)
    $pastelSky = ([int]$B -ge 0xB0 -and $brightness -ge 0x220)
    $blueSky = ([int]$B -ge 0x90 -and $brightness -ge 0x1D0 -and $blueDominant)
    return ($pastelSky -or $blueSky)
}

function Test-TargetColorCandidate([byte]$R, [byte]$G, [byte]$B, [string]$Filter) {
    if ($Filter -eq "Any") { return $true }

    $brightness = [int]$R + [int]$G + [int]$B
    $max = [Math]::Max([int]$R, [Math]::Max([int]$G, [int]$B))
    $min = [Math]::Min([int]$R, [Math]::Min([int]$G, [int]$B))
    $range = $max - $min
    $greenDominant = [int]$G -gt [int]$R -and [int]$G -gt [int]$B

    if ($brightness -lt 500) { return $false }
    if ($greenDominant -and ([int]$G - [Math]::Max([int]$R, [int]$B)) -gt 20) { return $false }

    $softPastel = $range -le 155
    $brightBlueOrPink = ([int]$B -ge 0xD0 -and $brightness -ge 540) -or ([int]$R -ge 0xD0 -and [int]$B -ge 0xB0)
    $warmCloud = ([int]$R -ge 0xD0 -and [int]$G -ge 0xC0 -and [int]$B -ge 0xA0)
    return ($softPastel -or $brightBlueOrPink -or $warmCloud)
}

function Get-ColorMetric([byte]$R, [byte]$G, [byte]$B) {
    return [pscustomobject][ordered]@{
        brightness = ([int]$R + [int]$G + [int]$B)
        blueBias = ([int]$B - [Math]::Max([int]$R, [int]$G))
        redBias = ([int]$R - [Math]::Max([int]$G, [int]$B))
    }
}

function Get-RgbFromColorKey([string]$ColorKey) {
    $match = [regex]::Match($ColorKey, "#(?<r>[0-9A-Fa-f]{2})(?<g>[0-9A-Fa-f]{2})(?<b>[0-9A-Fa-f]{2})")
    if (-not $match.Success) { throw "Could not parse RGB from source color key '$ColorKey'." }
    return [pscustomobject][ordered]@{
        r = [byte][Convert]::ToInt32($match.Groups["r"].Value, 16)
        g = [byte][Convert]::ToInt32($match.Groups["g"].Value, 16)
        b = [byte][Convert]::ToInt32($match.Groups["b"].Value, 16)
    }
}

function Get-RgbFromHexColor([string]$ColorHex) {
    $clean = $ColorHex.Trim()
    if ($clean.StartsWith("#")) { $clean = $clean.Substring(1) }
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    if ($clean.Length -ne 6) { throw "Could not parse RGB from color '$ColorHex'." }
    return [pscustomobject][ordered]@{
        r = [byte][Convert]::ToInt32($clean.Substring(0, 2), 16)
        g = [byte][Convert]::ToInt32($clean.Substring(2, 2), 16)
        b = [byte][Convert]::ToInt32($clean.Substring(4, 2), 16)
    }
}

function Get-NormalizedColorSet([string[]]$ColorHexes) {
    $set = @{}
    foreach ($colorText in @($ColorHexes)) {
        if ([string]::IsNullOrWhiteSpace($colorText)) { continue }
        $clean = $colorText.Trim()
        if ($clean.StartsWith("#")) { $clean = $clean.Substring(1) }
        if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
        if ($clean.Length -ne 6) { throw "Color '$colorText' must be #RRGGBB, RRGGBB, or 0xRRGGBB." }
        $set[("#" + $clean.ToUpperInvariant())] = $true
    }
    return $set
}

function Get-ColorHex([byte]$R, [byte]$G, [byte]$B) {
    return ("#{0:X2}{1:X2}{2:X2}" -f $R, $G, $B)
}

function Test-TargetColorWithOverrides([byte]$R, [byte]$G, [byte]$B, [string]$Filter, [hashtable]$IncludeColors, [hashtable]$ExcludeColors) {
    $color = Get-ColorHex $R $G $B
    if ($ExcludeColors.ContainsKey($color)) { return $false }
    if ($IncludeColors.ContainsKey($color)) { return $true }
    return (Test-TargetColorCandidate $R $G $B $Filter)
}

function Get-DonorCandidates([byte[]]$Bytes, [int]$StartOffset, [int]$EndOffset, [string]$Filter, [int]$Limit) {
    $candidates = New-Object Collections.Generic.List[object]
    $start = [Math]::Max(0, $StartOffset)
    $end = [Math]::Min($Bytes.Length - 4, $EndOffset)
    for ($offset = $start; $offset -le $end; $offset += 4) {
        $r = [byte]$Bytes[$offset]
        $g = [byte]$Bytes[$offset + 1]
        $b = [byte]$Bytes[$offset + 2]
        $command = [byte]$Bytes[$offset + 3]
        if (-not (Test-DonorColorCandidate $r $g $b $command $Filter)) { continue }
        [void]$candidates.Add([pscustomobject][ordered]@{
            donorSubfileOffset = ("0x{0:X}" -f $offset)
            donorColor = ("#{0:X2}{1:X2}{2:X2}" -f $r, $g, $b)
            donorCommand = ("0x{0:X2}" -f $command)
            brightness = ([int]$r + [int]$g + [int]$b)
            blueBias = ([int]$b - [Math]::Max([int]$r, [int]$g))
            redBias = ([int]$r - [Math]::Max([int]$g, [int]$b))
            r = $r
            g = $g
            b = $b
        })
        if ($candidates.Count -ge $Limit) { break }
    }
    return @($candidates.ToArray())
}

function Get-NormalizedCommandSet([string[]]$CommandHexes) {
    $set = @{}
    foreach ($commandText in @($CommandHexes)) {
        if ([string]::IsNullOrWhiteSpace($commandText)) { continue }
        $value = [byte](Convert-HexToInt64 $commandText)
        $set[("0x{0:X2}" -f $value)] = $true
    }
    return $set
}

function Get-TargetHits([string[]]$Specs, [int]$MaxPerSpec, [hashtable]$AllowedCommands, [string]$ColorFilter, [hashtable]$IncludeColors, [hashtable]$ExcludeColors) {
    $result = New-Object Collections.Generic.List[object]
    foreach ($specText in $Specs) {
        $parts = $specText.Split("|")
        if ($parts.Count -ne 3) { throw "Target patch spec '$specText' must be '<hits-json>|<entry>|<subfile>'." }
        $hitsPath = Resolve-WorkspacePath $parts[0]
        $entryIndex = [int]$parts[1]
        $subfileIndex = [int]$parts[2]
        if (-not (Test-Path -LiteralPath $hitsPath)) { throw "Missing target hits file: $hitsPath" }
        $hitsJson = Get-Content -Raw -LiteralPath $hitsPath | ConvertFrom-Json
        $targetRegion = "entry-$entryIndex-subfile-$subfileIndex"
        $hits = @($hitsJson.hits | Where-Object {
            $isTarget = [string]$_.region -eq $targetRegion -and [string]$_.kind -eq "rgb-command-word"
            if (-not $isTarget) { return $false }
            $patternBytes = Convert-HexPatternToBytes ([string]$_.patternHex)
            if ($patternBytes.Length -ne 4) { return $false }
            $commandKey = ("0x{0:X2}" -f $patternBytes[3])
            if ($AllowedCommands.Count -gt 0 -and -not $AllowedCommands.ContainsKey($commandKey)) { return $false }
            return (Test-TargetColorWithOverrides ([byte]$patternBytes[0]) ([byte]$patternBytes[1]) ([byte]$patternBytes[2]) $ColorFilter $IncludeColors $ExcludeColors)
        } | Sort-Object imageOffset, key -Unique | Select-Object -First $MaxPerSpec)
        if ($hits.Count -eq 0) { throw "No $targetRegion rgb-command-word hits found in $hitsPath." }
        foreach ($hit in $hits) {
            [void]$result.Add([pscustomobject][ordered]@{
                targetRegion = $targetRegion
                sourceHitsPath = $hitsPath
                hit = $hit
            })
        }
    }
    return @($result.ToArray() | Sort-Object { Convert-HexToInt64 ([string]$_.hit.imageOffset) })
}

function New-RgbCommandHit($Layout, $Subfile, [int]$Offset, [byte]$R, [byte]$G, [byte]$B, [byte]$Command) {
    $wadOffset = [int64]$Subfile.wadOffset + [int64]$Offset
    $commandHex = "0x{0:X2}" -f $Command
    return [pscustomobject][ordered]@{
        region = "entry-$($Subfile.entryIndex)-subfile-$($Subfile.subfileIndex)"
        entryIndex = [int]$Subfile.entryIndex
        subfileIndex = [int]$Subfile.subfileIndex
        regionRelativeOffset = ("0x{0:X}" -f $Offset)
        wadRelativeOffset = ("0x{0:X}" -f $wadOffset)
        imageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $Layout $wadOffset))
        kind = "rgb-command-word"
        key = ("#{0:X2}{1:X2}{2:X2}/{3}" -f $R, $G, $B, $commandHex)
        patternHex = ("{0:X2} {1:X2} {2:X2} {3:X2}" -f $R, $G, $B, $Command)
        source = "target-window"
    }
}

function Get-TargetWindowHits([string[]]$Specs, $WadAnalysis, $Layout, [string]$ImagePath, [hashtable]$AllowedCommands, [string]$ColorFilter, [hashtable]$IncludeColors, [hashtable]$ExcludeColors, [int]$MaxPerSpec) {
    $result = New-Object Collections.Generic.List[object]
    $stream = [IO.File]::OpenRead($ImagePath)
    try {
        foreach ($specText in $Specs) {
            $parts = $specText.Split("|")
            if ($parts.Count -ne 4) { throw "Target window spec '$specText' must be '<entry>|<subfile>|<start>|<end>'." }
            $entryIndex = [int]$parts[0]
            $subfileIndex = [int]$parts[1]
            $start = [int](Convert-HexToInt64 $parts[2])
            $end = [int](Convert-HexToInt64 $parts[3])
            if ($end -lt $start) { throw "Target window spec '$specText' has end before start." }

            $subfile = Get-WadSubfile $WadAnalysis $entryIndex $subfileIndex
            $bytes = Read-WadBytes $stream $Layout ([int64]$subfile.wadOffset) ([int]$subfile.size)
            $targetRegion = "entry-$entryIndex-subfile-$subfileIndex"
            $added = 0
            $limit = [Math]::Min($end, $bytes.Length - 4)
            for ($offset = [Math]::Max(0, $start); $offset -le $limit; $offset += 4) {
                $command = [byte]$bytes[$offset + 3]
                if (-not ($command -in 0x20, 0x28, 0x30, 0x38)) { continue }
                $commandKey = ("0x{0:X2}" -f $command)
                if ($AllowedCommands.Count -gt 0 -and -not $AllowedCommands.ContainsKey($commandKey)) { continue }
                if (-not (Test-TargetColorWithOverrides ([byte]$bytes[$offset]) ([byte]$bytes[$offset + 1]) ([byte]$bytes[$offset + 2]) $ColorFilter $IncludeColors $ExcludeColors)) { continue }
                $hit = New-RgbCommandHit $Layout $subfile $offset ([byte]$bytes[$offset]) ([byte]$bytes[$offset + 1]) ([byte]$bytes[$offset + 2]) $command
                [void]$result.Add([pscustomobject][ordered]@{
                    targetRegion = $targetRegion
                    sourceHitsPath = "window:$specText"
                    hit = $hit
                })
                $added++
                if ($added -ge $MaxPerSpec) { break }
            }
            if ($added -eq 0) { throw "No rgb-command-word hits found in target window spec '$specText'." }
        }
    }
    finally {
        $stream.Dispose()
    }
    return @($result.ToArray() | Sort-Object { Convert-HexToInt64 ([string]$_.hit.imageOffset) })
}

function Select-DonorForIndex($Candidates, [int]$Index, [int]$Total, [string]$Mode) {
    if ($Candidates.Count -eq 0) { throw "No donor candidates are available." }
    if ($Mode -eq "Sequential") {
        return $Candidates[$Index % $Candidates.Count]
    }
    if ($Total -le 1) { return $Candidates[0] }
    $donorIndex = [int][Math]::Round(([double]$Index * [double]($Candidates.Count - 1)) / [double]($Total - 1))
    return $Candidates[$donorIndex]
}

function Get-StablePaletteSeedMap([string]$SeedPlanPath) {
    $seedMap = @{}
    if ([string]::IsNullOrWhiteSpace($SeedPlanPath)) { return $seedMap }
    $resolvedSeed = Resolve-WorkspacePath $SeedPlanPath
    if (-not (Test-Path -LiteralPath $resolvedSeed)) { throw "Missing stable palette seed plan: $resolvedSeed" }
    $seedPlan = Get-Content -Raw -LiteralPath $resolvedSeed | ConvertFrom-Json
    if ($null -eq $seedPlan.stablePalette -or $null -eq $seedPlan.stablePalette.rows) {
        throw "Seed plan does not contain stablePalette.rows: $resolvedSeed"
    }
    foreach ($row in @($seedPlan.stablePalette.rows)) {
        $sourceColor = [string]$row.sourceColor
        $donorColor = [string]$row.donorColor
        if ([string]::IsNullOrWhiteSpace($sourceColor) -or [string]::IsNullOrWhiteSpace($donorColor)) { continue }
        $rgb = Get-RgbFromHexColor $donorColor
        $metric = Get-ColorMetric $rgb.r $rgb.g $rgb.b
        $seedMap[$sourceColor] = [pscustomobject][ordered]@{
            donorSubfileOffset = [string]$row.donorSubfileOffset
            donorColor = (Get-ColorHex $rgb.r $rgb.g $rgb.b)
            donorCommand = "seed"
            brightness = [int]$metric.brightness
            blueBias = [int]$metric.blueBias
            redBias = [int]$metric.redBias
            r = $rgb.r
            g = $rgb.g
            b = $rgb.b
            seeded = $true
        }
    }
    return $seedMap
}

function New-StablePaletteMap($TargetHits, $DonorCandidates, [string]$SeedPlanPath) {
    $targetByKey = @{}
    foreach ($targetHit in @($TargetHits)) {
        $key = [string]$targetHit.hit.key
        if ($targetByKey.ContainsKey($key)) { continue }
        $rgb = Get-RgbFromColorKey $key
        $metric = Get-ColorMetric $rgb.r $rgb.g $rgb.b
        $targetByKey[$key] = [pscustomobject][ordered]@{
            sourceColor = $key
            r = $rgb.r
            g = $rgb.g
            b = $rgb.b
            brightness = [int]$metric.brightness
            blueBias = [int]$metric.blueBias
            redBias = [int]$metric.redBias
        }
    }

    $uniqueDonors = @($DonorCandidates |
        Group-Object donorColor |
        ForEach-Object { $_.Group[0] } |
        Sort-Object brightness, blueBias, redBias, donorColor)
    if ($uniqueDonors.Count -eq 0) { throw "No unique donor colors are available for stable palette mapping." }

    $targetPalette = @($targetByKey.Values | Sort-Object brightness, blueBias, redBias, sourceColor)
    $seedMap = Get-StablePaletteSeedMap $SeedPlanPath
    $map = @{}
    $rows = New-Object Collections.Generic.List[object]
    for ($i = 0; $i -lt $targetPalette.Count; $i++) {
        $sourceColor = [string]$targetPalette[$i].sourceColor
        $isSeeded = $false
        if ($seedMap.ContainsKey($sourceColor)) {
            $donor = $seedMap[$sourceColor]
            $isSeeded = $true
        }
        else {
            $donorIndex = 0
            if ($targetPalette.Count -gt 1) {
                $donorIndex = [int][Math]::Round(([double]$i * [double]($uniqueDonors.Count - 1)) / [double]($targetPalette.Count - 1))
            }
            $donor = $uniqueDonors[$donorIndex]
        }
        $map[$sourceColor] = $donor
        [void]$rows.Add([pscustomobject][ordered]@{
            sourceColor = $sourceColor
            sourceBrightness = [int]$targetPalette[$i].brightness
            donorColor = [string]$donor.donorColor
            donorSubfileOffset = [string]$donor.donorSubfileOffset
            donorBrightness = [int]$donor.brightness
            seeded = $isSeeded
        })
    }

    return [pscustomobject][ordered]@{
        map = $map
        rows = @($rows.ToArray())
        targetUniqueColorCount = $targetPalette.Count
        donorUniqueColorCount = $uniqueDonors.Count
        seededColorCount = @($rows.ToArray() | Where-Object { $_.seeded }).Count
    }
}

if ($TargetPatchSpecs.Count -eq 0 -and $TargetWindowSpecs.Count -eq 0) {
    $TargetPatchSpecs = @(
        ".\_skybox_probe\live-current-screen-sky-wad-source-hits-focused.json|10|1",
        ".\_skybox_probe\live-current-screen-sky-wad-entry12-source-hits.json|12|1"
    )
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$wadAnalysisPath = Resolve-WorkspacePath $WadAnalysisPath
$outImage = Resolve-WorkspacePath $OutImagePath
$outCue = Resolve-WorkspacePath $OutCuePath
if ([string]::IsNullOrWhiteSpace($OutPlanPath)) {
    $OutPlanPath = "$OutImagePath.skyprimitive-donor-swap-plan.json"
}
$outPlan = Resolve-WorkspacePath $OutPlanPath

foreach ($path in @($sourceImage, $wadAnalysisPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $path" }
}
if ($MaxTargetPatches -lt 1) { throw "-MaxTargetPatches must be at least 1." }
if ($MaxDonorCandidates -lt 1) { throw "-MaxDonorCandidates must be at least 1." }

$layout = Detect-DiscLayout $sourceImage
$wadAnalysis = Get-Content -Raw -LiteralPath $wadAnalysisPath | ConvertFrom-Json
$donorSubfile = Get-WadSubfile $wadAnalysis $DonorEntryIndex $DonorSubfileIndex
$donorImageOffset = Convert-WadOffsetToImageOffset $layout ([int64]$donorSubfile.wadOffset)
$donorStream = [IO.File]::OpenRead($sourceImage)
try {
    $donorBytes = Read-WadBytes $donorStream $layout ([int64]$donorSubfile.wadOffset) ([int]$donorSubfile.size)
}
finally {
    $donorStream.Dispose()
}
$donorStart = [int](Convert-HexToInt64 $DonorStartOffset)
$donorEnd = [int](Convert-HexToInt64 $DonorEndOffset)
if ($donorEnd -lt $donorStart) { throw "-DonorEndOffset must be greater than or equal to -DonorStartOffset." }
$donorCandidates = @(Get-DonorCandidates $donorBytes $donorStart $donorEnd $DonorFilter $MaxDonorCandidates)
if ($donorCandidates.Count -eq 0) {
    throw "No donor primitive color candidates found in entry $DonorEntryIndex subfile $DonorSubfileIndex window $DonorStartOffset-$DonorEndOffset using filter $DonorFilter."
}

$allowedTargetCommands = Get-NormalizedCommandSet $TargetCommandHexes
$includeTargetColors = Get-NormalizedColorSet $TargetIncludeColorHexes
$excludeTargetColors = Get-NormalizedColorSet $TargetExcludeColorHexes
$targetHits = @()
if ($TargetPatchSpecs.Count -gt 0) {
    $targetHits += @(Get-TargetHits $TargetPatchSpecs $MaxTargetPatches $allowedTargetCommands $TargetColorFilter $includeTargetColors $excludeTargetColors)
}
if ($TargetWindowSpecs.Count -gt 0) {
    $targetHits += @(Get-TargetWindowHits $TargetWindowSpecs $wadAnalysis $layout $sourceImage $allowedTargetCommands $TargetColorFilter $includeTargetColors $excludeTargetColors $MaxTargetPatches)
}
$targetHits = @($targetHits | Sort-Object { Convert-HexToInt64 ([string]$_.hit.imageOffset) })
$stablePalette = $null
if ($MappingMode -eq "StablePaletteBrightness") {
    $stablePalette = New-StablePaletteMap $targetHits $donorCandidates $StablePaletteSeedPlanPath
}
$patches = New-Object Collections.Generic.List[object]
$seenOffsets = @{}
$stream = [IO.File]::OpenRead($sourceImage)
try {
    for ($i = 0; $i -lt $targetHits.Count; $i++) {
        $targetHit = $targetHits[$i]
        $hit = $targetHit.hit
        $imageOffset = Convert-HexToInt64 ([string]$hit.imageOffset)
        $offsetKey = ("0x{0:X}" -f $imageOffset)
        if ($seenOffsets.ContainsKey($offsetKey)) { continue }
        $seenOffsets[$offsetKey] = $true

        $original = New-Object byte[] 4
        $stream.Position = $imageOffset
        [void]$stream.Read($original, 0, 4)

        $patternBytes = Convert-HexPatternToBytes ([string]$hit.patternHex)
        if ($patternBytes.Length -ne 4) { continue }
        for ($b = 0; $b -lt 4; $b++) {
            if ($original[$b] -ne $patternBytes[$b]) {
                throw ("Source mismatch at image offset 0x{0:X}: expected {1}, found {2}" -f $imageOffset, [string]$hit.patternHex, (Convert-BytesToHex $original))
            }
        }

        if ($MappingMode -eq "StablePaletteBrightness") {
            $sourceKey = [string]$hit.key
            if (-not $stablePalette.map.ContainsKey($sourceKey)) { throw "Stable palette did not contain source color '$sourceKey'." }
            $donor = $stablePalette.map[$sourceKey]
        }
        else {
            $donor = Select-DonorForIndex $donorCandidates $i $targetHits.Count $MappingMode
        }
        $patched = [byte[]]@([byte]$donor.r, [byte]$donor.g, [byte]$donor.b, $original[3])
        [void]$patches.Add([pscustomobject][ordered]@{
            imageOffset = $offsetKey
            targetRegion = [string]$targetHit.targetRegion
            sourceHitsPath = [string]$targetHit.sourceHitsPath
            wadRelativeOffset = [string]$hit.wadRelativeOffset
            subfileOffset = [string]$hit.regionRelativeOffset
            sourceColor = [string]$hit.key
            originalBytes = Convert-BytesToHex $original
            patchBytes = Convert-BytesToHex $patched
            donorSubfileOffset = [string]$donor.donorSubfileOffset
            donorColor = [string]$donor.donorColor
            donorCommand = [string]$donor.donorCommand
            mappingMode = $MappingMode
        })
    }
}
finally {
    $stream.Dispose()
}

$plan = [pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroSkyPrimitiveDonorColorSwap.ps1"
    sourceImagePath = (Resolve-Path -LiteralPath $sourceImage).Path
    outImagePath = $outImage
    outCuePath = $outCue
    targetPatchSpecs = @($TargetPatchSpecs)
    targetWindowSpecs = @($TargetWindowSpecs)
    targetColorFilter = $TargetColorFilter
    targetIncludeColorHexes = @($includeTargetColors.Keys | Sort-Object)
    targetExcludeColorHexes = @($excludeTargetColors.Keys | Sort-Object)
    targetCommandHexes = @($allowedTargetCommands.Keys | Sort-Object)
    targetRegions = @($targetHits | Group-Object targetRegion | ForEach-Object { $_.Name })
    donor = [ordered]@{
        entryIndex = $DonorEntryIndex
        subfileIndex = $DonorSubfileIndex
        wadOffset = ("0x{0:X}" -f [int64]$donorSubfile.wadOffset)
        imageOffset = ("0x{0:X}" -f [int64]$donorImageOffset)
        startOffset = ("0x{0:X}" -f $donorStart)
        endOffset = ("0x{0:X}" -f $donorEnd)
        filter = $DonorFilter
        candidateCount = $donorCandidates.Count
        sampleCandidates = @($donorCandidates | Select-Object -First 40 donorSubfileOffset, donorColor, donorCommand)
    }
    mappingMode = $MappingMode
    stablePaletteSeedPlanPath = $(if ([string]::IsNullOrWhiteSpace($StablePaletteSeedPlanPath)) { $null } else { (Resolve-WorkspacePath $StablePaletteSeedPlanPath) })
    stablePalette = $(if ($null -ne $stablePalette) {
        [ordered]@{
            targetUniqueColorCount = $stablePalette.targetUniqueColorCount
            donorUniqueColorCount = $stablePalette.donorUniqueColorCount
            seededColorCount = $stablePalette.seededColorCount
            rows = @($stablePalette.rows)
        }
    } else { $null })
    patchCount = $patches.Count
    scope = "target sky rgb-command-word hits patched with donor primitive RGB colors while preserving target command bytes"
    patches = @($patches.ToArray())
}

$outPlanDir = Split-Path -Parent $outPlan
if (-not [string]::IsNullOrWhiteSpace($outPlanDir)) { [IO.Directory]::CreateDirectory($outPlanDir) | Out-Null }
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

Write-Host ("Sky primitive donor-color swap plan: {0}" -f $outPlan)
Write-Host ("Patch count: {0}; donor candidates: {1}; donor entry/subfile: {2}/{3}; filter: {4}; mapping: {5}" -f $patches.Count, $donorCandidates.Count, $DonorEntryIndex, $DonorSubfileIndex, $DonorFilter, $MappingMode)
if ($PlanOnly) {
    Write-Host "Plan only; no BIN/CUE was written."
}
else {
    Write-Host ("Wrote patched BIN: {0}" -f $outImage)
    Write-Host ("Wrote patched CUE: {0}" -f $outCue)
}
