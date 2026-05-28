param(
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-sun13-wordprobe",
    [string]$DonorOffset = "0x8D5AC"
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
    WordCount = 7
    Portal = "0x78338"
    Loaded = "0x94AFC"
}

function Add-RecordSpecs($List, $Record, [string]$Tag) {
    $length = "0x{0:X}" -f ([int]$Record.WordCount * 4)
    [void]$List.Add(("10|1|{0}|{1}|58|1|{2}|portal-{3}" -f $Record.Portal, $length, $Record.Donor, $Tag))
    [void]$List.Add(("12|1|{0}|{1}|58|1|{2}|loaded-{3}" -f $Record.Loaded, $length, $Record.Donor, $Tag))
}

function Add-SunWordSpecs($List, [int]$WordIndex) {
    $portalStart = [int64](Convert-HexToInt64 $sunRecord.Portal) + ([int64]$WordIndex * 4)
    $loadedStart = [int64](Convert-HexToInt64 $sunRecord.Loaded) + ([int64]$WordIndex * 4)
    $donorStart = [int64](Convert-HexToInt64 $DonorOffset) + ([int64]$WordIndex * 4)
    [void]$List.Add(("10|1|0x{0:X}|0x4|58|1|0x{1:X}|portal-sun13-word{2:00}" -f $portalStart, $donorStart, $WordIndex))
    [void]$List.Add(("12|1|0x{0:X}|0x4|58|1|0x{1:X}|loaded-sun13-word{2:00}" -f $loadedStart, $donorStart, $WordIndex))
}

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
for ($wordIndex = 0; $wordIndex -lt [int]$sunRecord.WordCount; $wordIndex++) {
    $specs = New-Object Collections.Generic.List[string]
    foreach ($record in $baseRecords) {
        Add-RecordSpecs $specs $record ([string]$record.Label)
    }
    Add-SunWordSpecs $specs $wordIndex

    $outImage = "{0}-word{1:00}-test.bin" -f $OutputPrefix, $wordIndex
    $outCue = "{0}-word{1:00}-test.cue" -f $OutputPrefix, $wordIndex
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        name = ("word{0:00}" -f $wordIndex)
        donorOffset = ("0x{0:X}" -f (([int64](Convert-HexToInt64 $DonorOffset)) + ([int64]$wordIndex * 4)))
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    donorOffset = $DonorOffset
    baseRecords = @($baseRecords)
    sunRecord = $sunRecord
    variants = @($created.ToArray())
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
