param(
    [string]$SourceLinksPath = ".\stonehill-terrain-source-links.json",
    [string]$OutJsonPath = ".\stonehill-terrain-axis-probe.json",
    [string]$OutMarkdownPath = ".\stonehill-terrain-axis-probe.md",
    [double[]]$DistanceThresholds = @(4, 8, 16, 32, 64)
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return @($value) }
    return @($value)
}

function Convert-SignedField([double]$Value, [int]$Bits) {
    $half = [Math]::Pow(2, $Bits - 1)
    $full = [Math]::Pow(2, $Bits)
    if ($Value -ge $half) { return [double]($Value - $full) }
    return [double]$Value
}

function Get-FeatureValue($Link, [string]$Feature) {
    $source = Get-Field $Link "source" $null
    $localX = [double](Get-Field $source "localX" 0)
    $localY = [double](Get-Field $source "localY" 0)
    $localZ = [double](Get-Field $source "localZ" 0)
    $x = [double](Get-Field $source "x" 0)
    $y = [double](Get-Field $source "y" 0)
    $z = [double](Get-Field $source "z" 0)
    $globalX = $x - $localX
    $globalY = $y - $localY
    $globalZ = $z - $localZ

    switch ($Feature) {
        "localX" { return $localX }
        "localY" { return $localY }
        "localZ" { return $localZ }
        "signedLocalX" { return (Convert-SignedField $localX 11) }
        "signedLocalY" { return (Convert-SignedField $localY 11) }
        "signedLocalZ" { return (Convert-SignedField $localZ 10) }
        "globalX" { return $globalX }
        "globalY" { return $globalY }
        "globalZ" { return $globalZ }
        "xUnsigned" { return $x }
        "yUnsigned" { return $y }
        "zUnsigned" { return $z }
        "xSigned" { return ($globalX + (Convert-SignedField $localX 11)) }
        "ySigned" { return ($globalY + (Convert-SignedField $localY 11)) }
        "zSigned" { return ($globalZ + (Convert-SignedField $localZ 10)) }
    }
    return $null
}

function Select-BestRuntimeLinks($Links, [double]$MaxDistance) {
    $bestByRuntime = @{}
    foreach ($link in @($Links)) {
        $distance = [double](Get-Field $link "distance" 999999)
        if ($distance -gt $MaxDistance) { continue }
        $runtime = Get-Field $link "runtime" $null
        $key = [string](Get-Field $runtime "key" "")
        if ([string]::IsNullOrWhiteSpace($key)) { continue }
        if (-not $bestByRuntime.ContainsKey($key) -or $distance -lt [double](Get-Field $bestByRuntime[$key] "distance" 999999)) {
            $bestByRuntime[$key] = $link
        }
    }
    return @($bestByRuntime.Values)
}

function New-LinearFit($Links, [string]$Feature) {
    $pairs = New-Object System.Collections.ArrayList
    foreach ($link in @($Links)) {
        $x = Get-FeatureValue $link $Feature
        $runtime = Get-Field $link "runtime" $null
        $y = [double](Get-Field $runtime "z" ([double]::NaN))
        if ($null -eq $x -or [double]::IsNaN([double]$x) -or [double]::IsNaN($y)) { continue }
        [void]$pairs.Add([pscustomobject]@{ x = [double]$x; y = $y })
    }
    if ($pairs.Count -lt 3) { return $null }

    $sumX = 0.0; $sumY = 0.0
    foreach ($p in @($pairs)) {
        $sumX += [double]$p.x
        $sumY += [double]$p.y
    }
    $meanX = $sumX / [double]$pairs.Count
    $meanY = $sumY / [double]$pairs.Count
    $num = 0.0; $den = 0.0; $varY = 0.0
    foreach ($p in @($pairs)) {
        $dx = [double]$p.x - $meanX
        $dy = [double]$p.y - $meanY
        $num += $dx * $dy
        $den += $dx * $dx
        $varY += $dy * $dy
    }
    $slope = if ([Math]::Abs($den) -gt 0.000001) { $num / $den } else { 0.0 }
    $intercept = $meanY - ($slope * $meanX)
    $corr = if ($den -gt 0.0 -and $varY -gt 0.0) { $num / [Math]::Sqrt($den * $varY) } else { 0.0 }

    $errors = New-Object System.Collections.ArrayList
    $sumSquared = 0.0
    foreach ($p in @($pairs)) {
        $error = (($slope * [double]$p.x) + $intercept) - [double]$p.y
        $sumSquared += $error * $error
        [void]$errors.Add([Math]::Abs($error))
    }
    $sortedErrors = @($errors.ToArray() | Sort-Object)
    return [pscustomobject][ordered]@{
        feature = $Feature
        sampleCount = $pairs.Count
        slope = [Math]::Round($slope, 8)
        intercept = [Math]::Round($intercept, 4)
        correlation = [Math]::Round($corr, 4)
        absCorrelation = [Math]::Round([Math]::Abs($corr), 4)
        medianAbsError = [Math]::Round([double]$sortedErrors[[int][Math]::Floor($sortedErrors.Count / 2)], 2)
        p90AbsError = [Math]::Round([double]$sortedErrors[[int][Math]::Floor($sortedErrors.Count * 0.9)], 2)
        rmse = [Math]::Round([Math]::Sqrt($sumSquared / [double]$pairs.Count), 2)
    }
}

function Write-Markdown($Report, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Axis Probe")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Report.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("Status: ``$($Report.status)``")
    [void]$lines.Add("")
    [void]$lines.Add("This compares source-link candidate fields against runtime terrain Z. It is an offline hint only; Stone Hill slopes can make map axes correlate with height, so live patched-disc validation is still required.")
    [void]$lines.Add("")
    foreach ($bucket in @($Report.thresholds)) {
        [void]$lines.Add("## Distance <= $($bucket.maxDistance)")
        [void]$lines.Add("")
        [void]$lines.Add("Unique runtime vertices: $($bucket.uniqueRuntimeVertexCount)")
        [void]$lines.Add("")
        [void]$lines.Add("| Rank | Feature | Corr | RMSE | Median Abs | P90 Abs | Slope |")
        [void]$lines.Add("|---:|---|---:|---:|---:|---:|---:|")
        $rank = 1
        foreach ($fit in @($bucket.topFits | Select-Object -First 8)) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |" -f $rank, $fit.feature, $fit.correlation, $fit.rmse, $fit.medianAbsError, $fit.p90AbsError, $fit.slope))
            $rank++
        }
        [void]$lines.Add("")
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedSourceLinks = (Resolve-Path -LiteralPath (Resolve-WorkspacePath $SourceLinksPath) -ErrorAction Stop).Path
$linksRoot = Get-Content -LiteralPath $resolvedSourceLinks -Raw | ConvertFrom-Json
$links = @(Get-ArrayField (Get-Field $linksRoot "links" $null) "links")
$features = @("localX", "localY", "localZ", "signedLocalX", "signedLocalY", "signedLocalZ", "globalX", "globalY", "globalZ", "xUnsigned", "yUnsigned", "zUnsigned", "xSigned", "ySigned", "zSigned")

$thresholdReports = New-Object System.Collections.ArrayList
foreach ($threshold in $DistanceThresholds) {
    $selected = @(Select-BestRuntimeLinks $links $threshold)
    $fits = New-Object System.Collections.ArrayList
    foreach ($feature in $features) {
        $fit = New-LinearFit $selected $feature
        if ($null -ne $fit) { [void]$fits.Add($fit) }
    }
    $ranked = @($fits.ToArray() | Sort-Object -Property @{ Expression = { [double]$_.absCorrelation }; Descending = $true }, @{ Expression = { [double]$_.rmse }; Descending = $false })
    [void]$thresholdReports.Add([ordered]@{
        maxDistance = [double]$threshold
        uniqueRuntimeVertexCount = $selected.Count
        topFits = @($ranked)
    })
}

$localFits = @()
foreach ($bucket in @($thresholdReports)) {
    foreach ($fit in @($bucket.topFits | Where-Object { [string]$_.feature -in @("localX", "localY", "localZ", "signedLocalX", "signedLocalY", "signedLocalZ") })) {
        $localFits += $fit
    }
}
$bestLocal = @($localFits | Sort-Object -Property @{ Expression = { [double]$_.absCorrelation }; Descending = $true } | Select-Object -First 1)
$status = if ($bestLocal.Count -eq 0 -or [double]$bestLocal[0].absCorrelation -lt 0.25) { "needs-live-axis-test" } else { "offline-axis-hint-found" }

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceLinksPath = $resolvedSourceLinks
    status = $status
    note = "Offline correlation can be confounded by Stone Hill's map slope. Use this to choose a test matrix, not as final proof."
    thresholds = @($thresholdReports.ToArray())
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-Markdown ([pscustomobject]$report) $resolvedMarkdown
Write-Host "Wrote terrain axis probe to $resolvedJson"
Write-Host "Wrote terrain axis summary to $resolvedMarkdown"
Write-Host "Status: $status"
