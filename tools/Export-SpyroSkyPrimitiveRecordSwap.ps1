param(
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [string[]]$RecordSwapSpecs = @(),
    [string]$OutImagePath = ".\Spyro the Dragon (USA)-stonehill-skyprimitive-recordswap-test.bin",
    [string]$OutCuePath = ".\Spyro the Dragon (USA)-stonehill-skyprimitive-recordswap-test.cue",
    [string]$OutPlanPath = "",
    [switch]$PreserveTargetCommandBytes,
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

function Convert-BytesToHex([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $end = [Math]::Min($Bytes.Length, $Offset + $Length)
    $parts = New-Object Collections.Generic.List[string]
    for ($i = $Offset; $i -lt $end; $i++) {
        [void]$parts.Add(("{0:X2}" -f $Bytes[$i]))
    }
    return ($parts -join " ")
}

function Test-PvdAt([IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
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

function Parse-RecordSwapSpec([string]$Spec) {
    $parts = $Spec.Split("|")
    if ($parts.Count -lt 7 -or $parts.Count -gt 8) {
        throw "Record swap spec '$Spec' must be '<targetEntry>|<targetSubfile>|<targetStart>|<length>|<donorEntry>|<donorSubfile>|<donorStart>|<label optional>'."
    }
    return [pscustomobject][ordered]@{
        targetEntry = [int]$parts[0]
        targetSubfile = [int]$parts[1]
        targetStart = [int64](Convert-HexToInt64 $parts[2])
        length = [int](Convert-HexToInt64 $parts[3])
        donorEntry = [int]$parts[4]
        donorSubfile = [int]$parts[5]
        donorStart = [int64](Convert-HexToInt64 $parts[6])
        label = if ($parts.Count -eq 8 -and -not [string]::IsNullOrWhiteSpace($parts[7])) { [string]$parts[7] } else { "record-swap" }
    }
}

function New-RecordPatch($Layout, $TargetSubfile, $DonorSubfile, $Spec, [byte[]]$BeforeBytes, [byte[]]$DonorBytes) {
    $targetWadOffset = [int64]$TargetSubfile.wadOffset + [int64]$Spec.targetStart
    $donorWadOffset = [int64]$DonorSubfile.wadOffset + [int64]$Spec.donorStart
    return [pscustomobject][ordered]@{
        label = [string]$Spec.label
        kind = "sky-primitive-record-swap"
        target = "entry-$($Spec.targetEntry)-subfile-$($Spec.targetSubfile)"
        donor = "entry-$($Spec.donorEntry)-subfile-$($Spec.donorSubfile)"
        targetSubfileOffset = ("0x{0:X}" -f [int64]$Spec.targetStart)
        donorSubfileOffset = ("0x{0:X}" -f [int64]$Spec.donorStart)
        byteLength = [int]$Spec.length
        targetWadOffset = ("0x{0:X}" -f $targetWadOffset)
        donorWadOffset = ("0x{0:X}" -f $donorWadOffset)
        targetImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $Layout $targetWadOffset))
        donorImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $Layout $donorWadOffset))
        beforeHexPreview = Convert-BytesToHex $BeforeBytes 0 ([Math]::Min(64, $BeforeBytes.Length))
        donorHexPreview = Convert-BytesToHex $DonorBytes 0 ([Math]::Min(64, $DonorBytes.Length))
    }
}

if ($RecordSwapSpecs.Count -eq 0) {
    $RecordSwapSpecs = @(
        "10|1|0x73638|0x10C|58|1|0x91598|portal-flyin-known-sky-record-from-crystalflight",
        "12|1|0x8FDFC|0x10C|58|1|0x91598|loaded-known-sky-record-from-crystalflight"
    )
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$wadAnalysisPath = Resolve-WorkspacePath $WadAnalysisPath
$outImage = Resolve-WorkspacePath $OutImagePath
$outCue = Resolve-WorkspacePath $OutCuePath
if ([string]::IsNullOrWhiteSpace($OutPlanPath)) {
    $OutPlanPath = "$OutImagePath.skyprimitive-record-swap-plan.json"
}
$outPlan = Resolve-WorkspacePath $OutPlanPath

foreach ($path in @($sourceImage, $wadAnalysisPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required file: $path" }
}

$layout = Detect-DiscLayout $sourceImage
$wadAnalysis = Get-Content -Raw -LiteralPath $wadAnalysisPath | ConvertFrom-Json
$specs = @($RecordSwapSpecs | ForEach-Object { Parse-RecordSwapSpec $_ })
$patches = New-Object Collections.Generic.List[object]
$payloads = New-Object Collections.Generic.List[object]

$stream = [IO.File]::OpenRead($sourceImage)
try {
    foreach ($spec in @($specs)) {
        $targetSubfile = Get-WadSubfile $wadAnalysis ([int]$spec.targetEntry) ([int]$spec.targetSubfile)
        $donorSubfile = Get-WadSubfile $wadAnalysis ([int]$spec.donorEntry) ([int]$spec.donorSubfile)
        if ([int64]$spec.targetStart -lt 0 -or ([int64]$spec.targetStart + [int64]$spec.length) -gt [int64]$targetSubfile.size) {
            throw "Target range for '$($spec.label)' is outside entry $($spec.targetEntry) subfile $($spec.targetSubfile)."
        }
        if ([int64]$spec.donorStart -lt 0 -or ([int64]$spec.donorStart + [int64]$spec.length) -gt [int64]$donorSubfile.size) {
            throw "Donor range for '$($spec.label)' is outside entry $($spec.donorEntry) subfile $($spec.donorSubfile)."
        }

        $targetWadOffset = [int64]$targetSubfile.wadOffset + [int64]$spec.targetStart
        $donorWadOffset = [int64]$donorSubfile.wadOffset + [int64]$spec.donorStart
        $beforeBytes = Read-WadBytes $stream $layout $targetWadOffset ([int]$spec.length)
        $donorBytes = Read-WadBytes $stream $layout $donorWadOffset ([int]$spec.length)
        if ($PreserveTargetCommandBytes) {
            for ($i = 3; $i -lt $donorBytes.Length; $i += 4) {
                $donorBytes[$i] = $beforeBytes[$i]
            }
        }
        [void]$patches.Add((New-RecordPatch $layout $targetSubfile $donorSubfile $spec $beforeBytes $donorBytes))
        [void]$payloads.Add([pscustomobject][ordered]@{
            wadOffset = $targetWadOffset
            bytes = $donorBytes
        })
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
    wadLba = $WadLba
    layout = $layout
    recordSwapSpecs = @($RecordSwapSpecs)
    preserveTargetCommandBytes = [bool]$PreserveTargetCommandBytes
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
Write-Host "Record swaps: $($patches.Count), bytes: $($plan.totalPatchedBytes)"
