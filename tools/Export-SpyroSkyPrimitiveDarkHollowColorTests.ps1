param(
    [string]$RecordReportPath = ".\_skybox_probe\sky-primitive-record-candidates-darkhollow.json",
    [string]$OutputPrefix = ".\Spyro the Dragon (USA)-stonehill-from-darkhollow-recordcolors"
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

function Get-ColorBrightness([string]$Color) {
    $r = [Convert]::ToInt32($Color.Substring(1, 2), 16)
    $g = [Convert]::ToInt32($Color.Substring(3, 2), 16)
    $b = [Convert]::ToInt32($Color.Substring(5, 2), 16)
    return $r + $g + $b
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

$resolvedReport = Resolve-WorkspacePath $RecordReportPath
if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Missing record report: $resolvedReport" }
$report = Get-Content -Raw -LiteralPath $resolvedReport | ConvertFrom-Json

$donorRecords = @($report.records | Where-Object {
    [int]$_.entryIndex -eq 14 -and
    [int]$_.runWordCount -ge 7 -and
    [double]$_.avgBrightness -ge 180 -and
    [double]$_.avgBrightness -le 460
} | Sort-Object @{ Expression = { [int]$_.runWordCount }; Descending = $true }, @{ Expression = { [double]$_.avgBrightness } })

if ($donorRecords.Count -eq 0) { throw "No Dark Hollow donor records were found in $resolvedReport." }

$donorWords = New-Object Collections.Generic.List[object]
foreach ($record in @($donorRecords)) {
    $runStart = [int64](Convert-HexToInt64 ([string]$record.runStartOffset))
    $colors = @($record.previewColors)
    for ($i = 0; $i -lt [int]$record.runWordCount; $i++) {
        $color = if ($i -lt $colors.Count) { [string]$colors[$i] } else { "" }
        [void]$donorWords.Add([pscustomobject][ordered]@{
            sourceRecord = [string]$record.runStartOffset
            wordIndex = [int]$i
            offset = ("0x{0:X}" -f ($runStart + ([int64]$i * 4)))
            color = $color
            brightness = if (-not [string]::IsNullOrWhiteSpace($color)) { Get-ColorBrightness $color } else { [int][double]$record.avgBrightness }
        })
    }
}

$darkWords = @($donorWords.ToArray() | Where-Object { [int]$_.brightness -le 260 } | Sort-Object brightness)
$midWords = @($donorWords.ToArray() | Where-Object { [int]$_.brightness -gt 260 -and [int]$_.brightness -le 390 } | Sort-Object brightness)
$brightWords = @($donorWords.ToArray() | Where-Object { [int]$_.brightness -gt 390 } | Sort-Object brightness)
if ($darkWords.Count -eq 0) { $darkWords = @($donorWords.ToArray() | Sort-Object brightness | Select-Object -First 24) }
if ($midWords.Count -eq 0) { $midWords = @($donorWords.ToArray() | Sort-Object brightness | Select-Object -First 24) }
if ($brightWords.Count -eq 0) { $brightWords = @($donorWords.ToArray() | Sort-Object brightness -Descending | Select-Object -First 24) }

function Add-WordSpecs($List, $Records, [string]$VariantName, $PaletteWords) {
    $cursor = 0
    foreach ($record in @($Records)) {
        for ($word = 0; $word -lt [int]$record.WordCount; $word++) {
            $donor = $PaletteWords[$cursor % $PaletteWords.Count]
            $portal = [int64](Convert-HexToInt64 ([string]$record.Portal)) + ([int64]$word * 4)
            $loaded = [int64](Convert-HexToInt64 ([string]$record.Loaded)) + ([int64]$word * 4)
            [void]$List.Add(("10|1|0x{0:X}|0x4|14|1|{1}|portal-{2}-{3}-w{4:00}" -f $portal, $donor.offset, $VariantName, $record.Label, $word))
            [void]$List.Add(("12|1|0x{0:X}|0x4|14|1|{1}|loaded-{2}-{3}-w{4:00}" -f $loaded, $donor.offset, $VariantName, $record.Label, $word))
            $cursor++
        }
    }
}

$variants = @(
    [pscustomobject]@{ Name = "best22-dark"; Targets = $targetRecords; Palette = $darkWords; Note = "safe best22 target records using the darkest Dark Hollow donor words" },
    [pscustomobject]@{ Name = "best22-mid"; Targets = $targetRecords; Palette = $midWords; Note = "safe best22 target records using mid-brightness Dark Hollow donor words" },
    [pscustomobject]@{ Name = "best22-mixed"; Targets = $targetRecords; Palette = @($darkWords + $midWords + $brightWords); Note = "safe best22 target records cycling the full selected Dark Hollow palette" },
    [pscustomobject]@{ Name = "best22-plus19-dark"; Targets = @($targetRecords + $targetExtras); Palette = $darkWords; Note = "broader but still color-only pass: best22 plus the three 19-word anchor records" }
)

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
$created = New-Object Collections.Generic.List[object]
foreach ($variant in $variants) {
    $specs = New-Object Collections.Generic.List[string]
    Add-WordSpecs $specs $variant.Targets ([string]$variant.Name) $variant.Palette

    $outImage = "{0}-{1}-test.bin" -f $OutputPrefix, $variant.Name
    $outCue = "{0}-{1}-test.cue" -f $OutputPrefix, $variant.Name
    & $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $outImage -OutCuePath $outCue

    [void]$created.Add([pscustomobject][ordered]@{
        name = [string]$variant.Name
        note = [string]$variant.Note
        targetRecordCount = @($variant.Targets).Count
        paletteWordCount = @($variant.Palette).Count
        cuePath = Resolve-WorkspacePath $outCue
    })
}

$planPath = Resolve-WorkspacePath ("{0}-plan.json" -f $OutputPrefix)
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    recordReportPath = $resolvedReport
    donorRecords = @($donorRecords | Select-Object runStartOffset, runWordCount, avgBrightness, previewColors)
    donorWordCount = $donorWords.Count
    darkWordCount = $darkWords.Count
    midWordCount = $midWords.Count
    brightWordCount = $brightWords.Count
    variants = @($created.ToArray())
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
