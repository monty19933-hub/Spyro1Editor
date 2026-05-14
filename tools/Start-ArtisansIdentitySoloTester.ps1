param(
    [string]$BatchPlanPath = ".\artisans-identity-test-batches.json",
    [string]$CatalogPath = ".\artisans-moby-catalog.json",
    [string]$OriginalsPath = ".\artisans-live-moby-originals.json",
    [string]$ObservationCsvPath = ".\artisans-solo-identity-observations.csv",
    [int[]]$RecordIndexes = @(),
    [double]$X = 5194,
    [double]$Y = 2880,
    [double]$Z = 560,
    [int]$HoldSeconds = 600,
    [int]$ReadyTimeoutSeconds = 45,
    [int]$StartAt = 0,
    [switch]$SkipObserved,
    [switch]$NoRevertAllBeforeStart,
    [switch]$DryRun
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$MoveScript = Join-Path $PSScriptRoot "Move-DuckStationMoby.ps1"

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return @() }
    return @($prop.Value)
}

function Convert-DoubleToRaw([double]$Value) {
    return [int][Math]::Round($Value * 16.0)
}

function Get-Queue {
    if ($RecordIndexes.Count -gt 0) {
        return @($RecordIndexes | ForEach-Object { [int]$_ })
    }

    $resolvedPlan = Resolve-WorkspacePath $BatchPlanPath
    if (-not (Test-Path -LiteralPath $resolvedPlan)) { throw "Missing Artisans identity batch plan: $resolvedPlan" }
    $plan = Get-Content -LiteralPath $resolvedPlan -Raw | ConvertFrom-Json
    $queue = New-Object System.Collections.ArrayList
    foreach ($batch in @(Get-ArrayField $plan "batches")) {
        foreach ($placement in @(Get-ArrayField $batch "placements")) {
            $id = [string]$placement.id
            if ($id -match "^T(\d+)$") { [void]$queue.Add([int]$Matches[1]) }
        }
    }
    return @($queue.ToArray())
}

function Get-LabelMap {
    $map = @{}
    $resolvedCatalog = Resolve-WorkspacePath $CatalogPath
    if (-not (Test-Path -LiteralPath $resolvedCatalog)) { return $map }
    $catalog = Get-Content -LiteralPath $resolvedCatalog -Raw | ConvertFrom-Json
    foreach ($moby in @(Get-ArrayField $catalog "mobys")) {
        $index = if ($null -ne $moby.PSObject.Properties["trueIndex"]) { [int]$moby.trueIndex } else { [int]$moby.index }
        $label = [string]$moby.displayTargetLabel
        $kind = [string]$moby.candidateKind
        $map[$index] = [pscustomobject]@{ label = $label; kind = $kind }
    }
    return $map
}

function Get-ObservedIndexSet {
    $set = @{}
    $resolvedCsv = Resolve-WorkspacePath $ObservationCsvPath
    if (-not (Test-Path -LiteralPath $resolvedCsv)) { return $set }

    foreach ($row in @(Import-Csv -LiteralPath $resolvedCsv)) {
        if ($null -eq $row.index -or [string]::IsNullOrWhiteSpace([string]$row.index)) { continue }
        $observation = [string]$row.observation
        if ($observation.Trim().Equals("Skipped", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }
        $set[[int]$row.index] = $true
    }
    return $set
}

function Write-SoloEditFile([int]$Index, [double]$EditX, [double]$EditY, [double]$EditZ) {
    $path = Resolve-WorkspacePath ".\artisans-solo-current-edits.json"
    $root = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        editor = "NativeSpyroEditor"
        levelName = "Artisans"
        note = "Temporary one-object guided Artisans identity test."
        editCount = 1
        edits = @(
            [ordered]@{
                index = $Index
                trueIndex = $Index
                label = "Artisans guided solo identity test: T$Index"
                rawEdited = [ordered]@{
                    x = Convert-DoubleToRaw $EditX
                    y = Convert-DoubleToRaw $EditY
                    z = Convert-DoubleToRaw $EditZ
                }
            }
        )
    }
    $root | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-RevertAll {
    & $MoveScript `
        -OriginalsPath $OriginalsPath `
        -ExpectedLevelId 10 `
        -CatalogPath $CatalogPath `
        -RevertAll
}

function Invoke-RevertOne([int]$Index) {
    & $MoveScript `
        -MobyIndexes $Index `
        -OriginalsPath $OriginalsPath `
        -ExpectedLevelId 10 `
        -CatalogPath $CatalogPath `
        -Revert
}

function Start-HoldApply([int]$Index, [string]$EditPath) {
    $stdout = Resolve-WorkspacePath ".\artisans-solo-current-apply.out.txt"
    $stderr = Resolve-WorkspacePath ".\artisans-solo-current-apply.err.txt"
    Remove-Item -LiteralPath $stdout -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderr -Force -ErrorAction SilentlyContinue

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $MoveScript,
        "-MobyIndexes", $Index,
        "-FromNativeEdit",
        "-NativeEditsPath", $EditPath,
        "-OriginalsPath", $OriginalsPath,
        "-ExpectedLevelId", "10",
        "-CatalogPath", $CatalogPath,
        "-Apply",
        "-HoldSeconds", $HoldSeconds,
        "-IntervalMs", "100"
    )
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $ReadyTimeoutSeconds) {
        Start-Sleep -Milliseconds 250
        $proc.Refresh()
        $out = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { "" }
        $err = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { "" }
        if ($out -match "Target raw XYZ") {
            return $proc
        }
        if ($proc.HasExited) {
            if ($proc.ExitCode -ne 0) {
                throw "Failed to apply T$Index.`n$out`n$err"
            }
            throw "Move script exited before T$Index reached the target write step.`n$out`n$err"
        }
    }

    $currentOut = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { "" }
    $currentErr = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { "" }
    throw "Timed out waiting for T$Index to reach the target write step.`n$currentOut`n$currentErr"
}

function Stop-HoldApply($Process) {
    if ($null -ne $Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 250
    }
}

function Add-Observation([int]$Index, [string]$BeforeLabel, [string]$BeforeKind, [string]$Observation) {
    $resolvedCsv = Resolve-WorkspacePath $ObservationCsvPath
    $row = [pscustomobject]@{
        observedAt = (Get-Date).ToString("s")
        levelName = "Artisans"
        index = $Index
        previousLabel = $BeforeLabel
        previousKind = $BeforeKind
        observation = $Observation
        x = $X
        y = $Y
        z = $Z
    }
    if (Test-Path -LiteralPath $resolvedCsv) {
        $row | Export-Csv -LiteralPath $resolvedCsv -NoTypeInformation -Append -Encoding UTF8
    }
    else {
        $row | Export-Csv -LiteralPath $resolvedCsv -NoTypeInformation -Encoding UTF8
    }
}

$queue = @(Get-Queue)
if ($StartAt -gt 0) {
    $queue = @($queue | Where-Object { $_ -ge $StartAt })
}
if ($SkipObserved) {
    $observed = Get-ObservedIndexSet
    $beforeCount = $queue.Count
    $queue = @($queue | Where-Object { -not $observed.ContainsKey([int]$_) })
    Write-Host "Skipping $($beforeCount - $queue.Count) already-observed record(s) from $ObservationCsvPath."
}
if ($queue.Count -eq 0) { throw "No Artisans solo identity candidates to test." }

$labels = Get-LabelMap

Write-Host "Artisans solo identity tester"
Write-Host "Testing $($queue.Count) record(s) at X $X, Y $Y, Z $Z."
Write-Host "For each object: observe DuckStation, type what you see, then press Enter."
Write-Host "Use blank for invisible/no visible change, s to skip, q to quit."
Write-Host ""

if ($DryRun) {
    foreach ($index in $queue) {
        $known = if ($labels.ContainsKey($index)) { $labels[$index].label } else { "" }
        Write-Host ("T{0}: {1}" -f $index, $known)
    }
    return
}

if (-not $NoRevertAllBeforeStart) {
    Write-Host "Reverting saved Artisans live test positions before starting..."
    Invoke-RevertAll
}

foreach ($index in $queue) {
    $known = if ($labels.ContainsKey($index)) { $labels[$index] } else { [pscustomobject]@{ label = ""; kind = "" } }
    Write-Host ""
    Write-Host ("Testing T{0}: {1}" -f $index, $known.label)
    $editPath = Write-SoloEditFile $index $X $Y $Z
    $proc = $null
    try {
        Write-Host "Waiting for T$index to be written into DuckStation RAM..."
        $proc = Start-HoldApply $index $editPath
        Write-Host "T$index is now being held at the solo test point."
        $observation = Read-Host "Observation for T$index"
        if ([string]::IsNullOrWhiteSpace($observation)) { $observation = "Invisible / no visible object" }
        if ($observation.Trim().ToLowerInvariant() -eq "q") {
            Write-Host "Quitting after reverting T$index."
            break
        }
        if ($observation.Trim().ToLowerInvariant() -eq "s") {
            $observation = "Skipped"
        }
        Add-Observation $index $known.label $known.kind $observation
        Write-Host "Recorded: T$index = $observation"
    }
    finally {
        Stop-HoldApply $proc
        Invoke-RevertOne $index
    }
}

Write-Host ""
Write-Host "Observation log: $(Resolve-WorkspacePath $ObservationCsvPath)"
