param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-best22-plus"
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

$expansionRecords = @(
    [pscustomobject]@{ Index = 1; WordCount = 17; Portal = "0x73D6C"; Loaded = "0x90530"; Donor = "0x7DE80"; Note = "near-first-cloud-17" },
    [pscustomobject]@{ Index = 2; WordCount = 19; Portal = "0x74378"; Loaded = "0x90B3C"; Donor = "0x863C8"; Note = "near-first-cloud-19" },
    [pscustomobject]@{ Index = 13; WordCount = 7; Portal = "0x78338"; Loaded = "0x94AFC"; Donor = "0x87080"; Note = "near-middle-cloud-7a" },
    [pscustomobject]@{ Index = 15; WordCount = 7; Portal = "0x78DE0"; Loaded = "0x955A4"; Donor = "0x85EEC"; Note = "near-middle-cloud-7b" },
    [pscustomobject]@{ Index = 20; WordCount = 7; Portal = "0x79694"; Loaded = "0x95E58"; Donor = "0x8FB18"; Note = "near-last-cloud-7" },
    [pscustomobject]@{ Index = 22; WordCount = 5; Portal = "0x7A284"; Loaded = "0x96A48"; Donor = "0x7E34C"; Note = "near-last-cloud-5" }
)

function Add-RecordSpecs($List, $Record, [string]$Tag) {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $Record.Donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $Record.Donor, $Tag))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($candidate in $expansionRecords) {
    $indexText = "{0:D2}" -f [int]$candidate.Index
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-RecordSpecs $specs $record ([string]$record.Label)
    }
    Add-RecordSpecs $specs $candidate ("candidate-$indexText-$($candidate.Note)")

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $indexText
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $indexText
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        index = [int]$candidate.Index
        wordCount = [int]$candidate.WordCount
        note = [string]$candidate.Note
        portalOffset = [string]$candidate.Portal
        loadedOffset = [string]$candidate.Loaded
        donorOffset = [string]$candidate.Donor
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    baseRecords = @($baseRecords)
    expansionRecords = @($created.ToArray())
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
