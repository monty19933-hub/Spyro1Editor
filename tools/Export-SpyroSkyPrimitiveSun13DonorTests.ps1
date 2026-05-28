param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-sun13"
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
    WordCount = 7
    Portal = "0x78338"
    Loaded = "0x94AFC"
}

$donorVariants = @(
    [pscustomobject]@{ Name = "warm-d5ac"; Donor = "0x8D5AC"; Note = "mostly orange/yellow, no purple in first seven" },
    [pscustomobject]@{ Name = "warm-d124"; Donor = "0x8D124"; Note = "orange/yellow ramp, no purple in first seven" },
    [pscustomobject]@{ Name = "warm-e618"; Donor = "0x8E618"; Note = "warm longer run, first seven only" },
    [pscustomobject]@{ Name = "soft-81814"; Donor = "0x81814"; Note = "same soft pink donor as best22, first seven only" },
    [pscustomobject]@{ Name = "soft-85eec"; Donor = "0x85EEC"; Note = "pink/peach alternate seven-word run" }
)

function Add-RecordSpecs($List, $Record, [string]$Tag, [string]$DonorOverride = "") {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    $donor = if ([string]::IsNullOrWhiteSpace($DonorOverride)) { [string]$Record.Donor } else { $DonorOverride }
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $donor, $Tag))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($variant in $donorVariants) {
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-RecordSpecs $specs $record ([string]$record.Label)
    }
    Add-RecordSpecs $specs $sunRecord ("sun13-$($variant.Name)") ([string]$variant.Donor)

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $variant.Name
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $variant.Name
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        name = [string]$variant.Name
        donorOffset = [string]$variant.Donor
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
