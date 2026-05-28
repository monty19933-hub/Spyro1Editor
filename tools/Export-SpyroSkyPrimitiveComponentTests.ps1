param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-component"
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

$components = @{
    "02" = [pscustomobject]@{ Label = "plus02"; WordCount = 19; Portal = "0x74378"; Loaded = "0x90B3C"; Donor = "0x863C8" }
    "13" = [pscustomobject]@{ Label = "plus13"; WordCount = 7; Portal = "0x78338"; Loaded = "0x94AFC"; Donor = "0x87080" }
    "15" = [pscustomobject]@{ Label = "plus15"; WordCount = 7; Portal = "0x78DE0"; Loaded = "0x955A4"; Donor = "0x85EEC" }
    "22" = [pscustomobject]@{ Label = "plus22"; WordCount = 5; Portal = "0x7A284"; Loaded = "0x96A48"; Donor = "0x7E34C" }
}

$variants = @(
    [pscustomobject]@{ Name = "13only"; Keys = @("13"); Note = "best22 plus middle record 13 only" },
    [pscustomobject]@{ Name = "15only"; Keys = @("15"); Note = "best22 plus middle record 15 only" },
    [pscustomobject]@{ Name = "02only"; Keys = @("02"); Note = "best22 plus record 02 only" },
    [pscustomobject]@{ Name = "22only"; Keys = @("22"); Note = "best22 plus record 22 only" },
    [pscustomobject]@{ Name = "13-22"; Keys = @("13", "22"); Note = "best22 plus 13 and 22" },
    [pscustomobject]@{ Name = "15-22"; Keys = @("15", "22"); Note = "best22 plus 15 and 22" }
)

function Add-RecordSpecs($List, $Record, [string]$Tag) {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $Record.Donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $Record.Donor, $Tag))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($variant in $variants) {
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-RecordSpecs $specs $record ([string]$record.Label)
    }
    foreach ($key in @($variant.Keys)) {
        Add-RecordSpecs $specs $components[$key] ([string]$components[$key].Label)
    }

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $variant.Name
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $variant.Name
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        name = [string]$variant.Name
        note = [string]$variant.Note
        keys = @($variant.Keys)
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    baseRecords = @($baseRecords)
    variants = @($created.ToArray())
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
