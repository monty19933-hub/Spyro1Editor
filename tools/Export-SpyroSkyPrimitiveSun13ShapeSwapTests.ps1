param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-sun13-shape"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

$baseRecords = @(
    [pscustomobject]@{ Label = "best22-a"; WordCount = 22; Portal = "0x74B8C"; Loaded = "0x91350"; Donor = "0x81814" },
    [pscustomobject]@{ Label = "best22-b"; WordCount = 22; Portal = "0x78670"; Loaded = "0x94E34"; Donor = "0x81814" },
    [pscustomobject]@{ Label = "best22-c"; WordCount = 22; Portal = "0x79AA8"; Loaded = "0x9626C"; Donor = "0x81814" }
)

$sunRecord = [pscustomobject]@{
    Label = "sun13"
    Portal = "0x78338"
    Loaded = "0x94AFC"
    ColorBytes = "0x1C"
    TotalBytes = "0xDC"
}

$variants = @(
    [pscustomobject]@{
        Name = "tailonly-87080"
        Donor = "0x87080"
        StartDelta = "0x1C"
        Length = "0xC0"
        PreserveTargetCommandBytes = $false
        Note = "keep Stone Hill record-13 colors, copy Crystal Flight same-size record tail/shape only"
    },
    [pscustomobject]@{
        Name = "full-87080"
        Donor = "0x87080"
        StartDelta = "0x0"
        Length = "0xDC"
        PreserveTargetCommandBytes = $false
        Note = "copy Crystal Flight same-size record 13 colors plus tail/shape"
    },
    [pscustomobject]@{
        Name = "full-8928c"
        Donor = "0x8928C"
        StartDelta = "0x0"
        Length = "0xDC"
        PreserveTargetCommandBytes = $false
        Note = "bright soft-pink same-size donor record colors plus tail/shape"
    },
    [pscustomobject]@{
        Name = "full-d5ac"
        Donor = "0x8D5AC"
        StartDelta = "0x0"
        Length = "0xDC"
        PreserveTargetCommandBytes = $false
        Note = "warm same-size donor record colors plus tail/shape"
    }
)

function Convert-HexToInt64([string]$Value) {
    $clean = $Value.Trim()
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [Convert]::ToInt64($clean, 10)
}

function Add-BaseRecordSpecs($List, $Record, [string]$Tag) {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $Record.Donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $Record.Donor, $Tag))
}

function Add-SunRecordSpecs($List, $Variant) {
    $delta = [int64](Convert-HexToInt64 ([string]$Variant.StartDelta))
    $portalStart = [int64](Convert-HexToInt64 $sunRecord.Portal) + $delta
    $loadedStart = [int64](Convert-HexToInt64 $sunRecord.Loaded) + $delta
    $donorStart = [int64](Convert-HexToInt64 ([string]$Variant.Donor)) + $delta
    [void]$List.Add(("10|1|0x{0:X}|{1}|58|1|0x{2:X}|portal-{3}" -f $portalStart, $Variant.Length, $donorStart, $Variant.Name))
    [void]$List.Add(("12|1|0x{0:X}|{1}|58|1|0x{2:X}|loaded-{3}" -f $loadedStart, $Variant.Length, $donorStart, $Variant.Name))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($variant in $variants) {
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-BaseRecordSpecs $specs $record ([string]$record.Label)
    }
    Add-SunRecordSpecs $specs $variant

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $variant.Name
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $variant.Name
    $args = @{
        RecordSwapSpecs = $specs.ToArray()
        OutImagePath = $outImage
        OutCuePath = $outCue
    }
    if ([bool]$variant.PreserveTargetCommandBytes) {
        & $exporter @args -PreserveTargetCommandBytes
    }
    else {
        & $exporter @args
    }

    [void]$created.Add([pscustomobject][ordered]@{
        name = [string]$variant.Name
        donor = [string]$variant.Donor
        startDelta = [string]$variant.StartDelta
        length = [string]$variant.Length
        preserveTargetCommandBytes = [bool]$variant.PreserveTargetCommandBytes
        note = [string]$variant.Note
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    baseRecords = @($baseRecords)
    sunRecord = $sunRecord
    variants = @($created.ToArray())
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
