param(
    [string]$RecordReportPath = ".\_skybox_probe\sky-primitive-record-candidates.json",
    [string]$OutImagePath = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-skycluster-test.bin",
    [string]$OutCuePath = ".\Spyro the Dragon (USA)-stonehill-from-crystalflight-recordcolors-skycluster-test.cue",
    [int]$PortalStart = 0x73638,
    [int]$PortalEnd = 0x7B2E8,
    [int]$LoadedStart = 0x8FDFC,
    [int]$LoadedEnd = 0x97AAC,
    [double]$PreferredDonorMinBrightness = 350.0,
    [double]$PreferredDonorMaxBrightness = 700.0,
    [double]$MaxDonorAvgColorRange = 999.0,
    [double]$MaxDonorYellowFraction = 1.0,
    [double]$MaxDonorDarkFraction = 1.0,
    [switch]$SkipUnmatchedWordCounts
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Convert-HexToInt([string]$Value) {
    $clean = $Value.Trim()
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt32($clean.Substring(2), 16)
    }
    return [Convert]::ToInt32($clean, 10)
}

function Get-ColorStats($Record) {
    $colors = @($Record.uniqueColors)
    if ($colors.Count -eq 0) {
        $colors = @($Record.previewColors)
    }
    $rangeTotal = 0.0
    $yellowCount = 0
    $darkCount = 0
    foreach ($color in @($colors)) {
        $r = [Convert]::ToInt32(([string]$color).Substring(1, 2), 16)
        $g = [Convert]::ToInt32(([string]$color).Substring(3, 2), 16)
        $b = [Convert]::ToInt32(([string]$color).Substring(5, 2), 16)
        $max = [Math]::Max($r, [Math]::Max($g, $b))
        $min = [Math]::Min($r, [Math]::Min($g, $b))
        $rangeTotal += ($max - $min)
        if ($r -ge 200 -and $g -ge 150 -and $b -lt 150) { $yellowCount++ }
        if (($r + $g + $b) -lt 250) { $darkCount++ }
    }
    return [pscustomobject][ordered]@{
        colorCount = $colors.Count
        avgRange = if ($colors.Count -gt 0) { [Math]::Round($rangeTotal / $colors.Count, 2) } else { 999.0 }
        yellowFraction = if ($colors.Count -gt 0) { [Math]::Round($yellowCount / $colors.Count, 4) } else { 1.0 }
        darkFraction = if ($colors.Count -gt 0) { [Math]::Round($darkCount / $colors.Count, 4) } else { 1.0 }
    }
}

$resolvedReport = Resolve-WorkspacePath $RecordReportPath
if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Missing record report: $resolvedReport" }
$report = Get-Content -Raw -LiteralPath $resolvedReport | ConvertFrom-Json

$portalRecords = @($report.records | Where-Object {
    [int]$_.entryIndex -eq 10 -and
    (Convert-HexToInt ([string]$_.runStartOffset)) -ge $PortalStart -and
    (Convert-HexToInt ([string]$_.runStartOffset)) -le $PortalEnd
} | Sort-Object { Convert-HexToInt ([string]$_.runStartOffset) })

$loadedRecords = @($report.records | Where-Object {
    [int]$_.entryIndex -eq 12 -and
    (Convert-HexToInt ([string]$_.runStartOffset)) -ge $LoadedStart -and
    (Convert-HexToInt ([string]$_.runStartOffset)) -le $LoadedEnd
} | Sort-Object { Convert-HexToInt ([string]$_.runStartOffset) })

if ($portalRecords.Count -ne $loadedRecords.Count) {
    throw "Portal record count $($portalRecords.Count) does not match loaded record count $($loadedRecords.Count)."
}

$donorsByCount = @{}
$preferredDonors = @($report.records | Where-Object {
    if ([int]$_.entryIndex -ne 58) { return $false }
    if ([double]$_.avgBrightness -lt $PreferredDonorMinBrightness -or [double]$_.avgBrightness -gt $PreferredDonorMaxBrightness) { return $false }
    $stats = Get-ColorStats $_
    if ([double]$stats.avgRange -gt $MaxDonorAvgColorRange) { return $false }
    if ([double]$stats.yellowFraction -gt $MaxDonorYellowFraction) { return $false }
    if ([double]$stats.darkFraction -gt $MaxDonorDarkFraction) { return $false }
    return $true
} | Sort-Object runWordCount, @{ Expression = { [double]$_.avgBrightness }; Descending = $true })

foreach ($donor in @($preferredDonors)) {
    $key = [int]$donor.runWordCount
    if (-not $donorsByCount.ContainsKey($key)) {
        $donorsByCount[$key] = New-Object Collections.Generic.List[object]
    }
    [void]$donorsByCount[$key].Add($donor)
}

if (-not $SkipUnmatchedWordCounts) {
    foreach ($donor in @($report.records | Where-Object { [int]$_.entryIndex -eq 58 } | Sort-Object runWordCount, @{ Expression = { [double]$_.avgBrightness }; Descending = $true })) {
        $key = [int]$donor.runWordCount
        if (-not $donorsByCount.ContainsKey($key)) {
            $donorsByCount[$key] = New-Object Collections.Generic.List[object]
        }
        if ($donorsByCount[$key].Count -eq 0) {
            [void]$donorsByCount[$key].Add($donor)
        }
    }
}

$specs = New-Object Collections.Generic.List[string]
$rows = New-Object Collections.Generic.List[object]
for ($i = 0; $i -lt $portalRecords.Count; $i++) {
    $portal = $portalRecords[$i]
    $loaded = $loadedRecords[$i]
    $wordCount = [int]$portal.runWordCount
    if ($wordCount -ne [int]$loaded.runWordCount) {
        throw "Portal/loaded record word-count mismatch at index $i."
    }
    if (-not $donorsByCount.ContainsKey($wordCount) -or $donorsByCount[$wordCount].Count -eq 0) {
        continue
    }

    $donorList = $donorsByCount[$wordCount]
    $donor = $donorList[$i % $donorList.Count]
    $byteLength = $wordCount * 4
    $lengthHex = "0x{0:X}" -f $byteLength

    [void]$specs.Add(("10|1|{0}|{1}|58|1|{2}|portal-cluster-color-{3:D2}" -f $portal.runStartOffset, $lengthHex, $donor.runStartOffset, $i))
    [void]$specs.Add(("12|1|{0}|{1}|58|1|{2}|loaded-cluster-color-{3:D2}" -f $loaded.runStartOffset, $lengthHex, $donor.runStartOffset, $i))
    [void]$rows.Add([pscustomobject][ordered]@{
        index = $i
        wordCount = $wordCount
        byteLength = $byteLength
        portalOffset = [string]$portal.runStartOffset
        loadedOffset = [string]$loaded.runStartOffset
        donorOffset = [string]$donor.runStartOffset
        donorAvgBrightness = [double]$donor.avgBrightness
        donorStats = Get-ColorStats $donor
    })
}

if ($specs.Count -eq 0) { throw "No compatible cluster color specs were generated." }

$exporter = Join-Path $PSScriptRoot "Export-SpyroSkyPrimitiveRecordSwap.ps1"
& $exporter -RecordSwapSpecs $specs.ToArray() -PreserveTargetCommandBytes -OutImagePath $OutImagePath -OutCuePath $OutCuePath

$planPath = Resolve-WorkspacePath "$OutImagePath.skyprimitive-record-color-cluster-source-plan.json"
[pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    recordReportPath = $resolvedReport
    portalRange = ("0x{0:X}-0x{1:X}" -f $PortalStart, $PortalEnd)
    loadedRange = ("0x{0:X}-0x{1:X}" -f $LoadedStart, $LoadedEnd)
    preferredDonorMinBrightness = $PreferredDonorMinBrightness
    preferredDonorMaxBrightness = $PreferredDonorMaxBrightness
    maxDonorAvgColorRange = $MaxDonorAvgColorRange
    maxDonorYellowFraction = $MaxDonorYellowFraction
    maxDonorDarkFraction = $MaxDonorDarkFraction
    skipUnmatchedWordCounts = [bool]$SkipUnmatchedWordCounts
    specCount = $specs.Count
    rows = @($rows.ToArray())
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Wrote $planPath"
