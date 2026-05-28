param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-coolsoft-record"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

$rows = @(
    [pscustomobject]@{ Index = 3; WordCount = 22; Portal = "0x74B8C"; Loaded = "0x91350"; Donor = "0x81814" },
    [pscustomobject]@{ Index = 11; WordCount = 6; Portal = "0x77C64"; Loaded = "0x94428"; Donor = "0x893B8" },
    [pscustomobject]@{ Index = 14; WordCount = 22; Portal = "0x78670"; Loaded = "0x94E34"; Donor = "0x81814" },
    [pscustomobject]@{ Index = 18; WordCount = 6; Portal = "0x79418"; Loaded = "0x95BDC"; Donor = "0x893B8" },
    [pscustomobject]@{ Index = 21; WordCount = 22; Portal = "0x79AA8"; Loaded = "0x9626C"; Donor = "0x81814" }
)

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($row in $rows) {
    $length = "0x{0:X}" -f ([int]$row.WordCount * 4)
    $indexText = "{0:D2}" -f [int]$row.Index
    $specs = @(
        ("10|1|{0}|{1}|58|1|{2}|portal-coolsoft-record-{3}" -f $row.Portal, $length, $row.Donor, $indexText),
        ("12|1|{0}|{1}|58|1|{2}|loaded-coolsoft-record-{3}" -f $row.Loaded, $length, $row.Donor, $indexText)
    )

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $indexText
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $indexText
    & $exporter -RecordSwapSpecs $specs -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        index = [int]$row.Index
        wordCount = [int]$row.WordCount
        portalOffset = [string]$row.Portal
        loadedOffset = [string]$row.Loaded
        donorOffset = [string]$row.Donor
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-singles-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    records = @($created.ToArray())
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
