param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-sun13-word03"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

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

$baseRecords = @(
    [pscustomobject]@{ Label = "best22-a"; WordCount = 22; Portal = "0x74B8C"; Loaded = "0x91350"; Donor = "0x81814" },
    [pscustomobject]@{ Label = "best22-b"; WordCount = 22; Portal = "0x78670"; Loaded = "0x94E34"; Donor = "0x81814" },
    [pscustomobject]@{ Label = "best22-c"; WordCount = 22; Portal = "0x79AA8"; Loaded = "0x9626C"; Donor = "0x81814" }
)

$sunRecord = [pscustomobject]@{
    Label = "sun13"
    WordIndex = 3
    Portal = "0x78338"
    Loaded = "0x94AFC"
}

$donorVariants = @(
    [pscustomobject]@{ Name = "soft-81814"; DonorBase = "0x81814"; Note = "same soft donor family as best22, word 03 only" },
    [pscustomobject]@{ Name = "soft-85eec"; DonorBase = "0x85EEC"; Note = "alternate soft pink/peach donor, word 03 only" },
    [pscustomobject]@{ Name = "warm-d5ac"; DonorBase = "0x8D5AC"; Note = "warm donor used for the first word probe, word 03 only" },
    [pscustomobject]@{ Name = "warm-d124"; DonorBase = "0x8D124"; Note = "alternate warm ramp, word 03 only" },
    [pscustomobject]@{ Name = "warm-e618"; DonorBase = "0x8E618"; Note = "longer warm run, word 03 only" }
)

function Add-RecordSpecs($List, $Record, [string]$Tag) {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $Record.Donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $Record.Donor, $Tag))
}

function Add-SunWord03Specs($List, [string]$DonorBase, [string]$Tag) {
    $portalStart = [int64](Convert-HexToInt64 $sunRecord.Portal) + ([int64]$sunRecord.WordIndex * 4)
    $loadedStart = [int64](Convert-HexToInt64 $sunRecord.Loaded) + ([int64]$sunRecord.WordIndex * 4)
    $donorStart = [int64](Convert-HexToInt64 $DonorBase) + ([int64]$sunRecord.WordIndex * 4)
    [void]$List.Add(("10|1|0x{0:X}|0x4|58|1|0x{1:X}|portal-{2}" -f $portalStart, $donorStart, $Tag))
    [void]$List.Add(("12|1|0x{0:X}|0x4|58|1|0x{1:X}|loaded-{2}" -f $loadedStart, $donorStart, $Tag))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($variant in $donorVariants) {
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-RecordSpecs $specs $record ([string]$record.Label)
    }
    Add-SunWord03Specs $specs ([string]$variant.DonorBase) ("sun13-word03-$($variant.Name)")

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $variant.Name
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $variant.Name
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        name = [string]$variant.Name
        donorBase = [string]$variant.DonorBase
        donorWordOffset = ("0x{0:X}" -f (([int64](Convert-HexToInt64 ([string]$variant.DonorBase))) + ([int64]$sunRecord.WordIndex * 4)))
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
