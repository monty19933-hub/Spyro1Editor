param(
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceLinksPath = ".\stonehill-terrain-source-links.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutputPrefix = "Spyro the Dragon (USA)-terrain-axis",
    [string]$OutJsonPath = ".\stonehill-terrain-axis-test-batches.json",
    [string]$OutMarkdownPath = ".\stonehill-terrain-axis-test-batches.md",
    [double]$SourceUnitsPerZ = 1.0,
    [double]$MaxLinkDistance = 32.0,
    [int]$MaxSourceAliasesPerRuntime = 4,
    [switch]$WriteBins
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$Axes = @("localY", "localZ", "localX")

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Read-JsonIfPresent([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
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

function Invoke-TerrainExporter([string]$Axis, [bool]$WriteBin) {
    $safeAxis = $Axis.ToLowerInvariant()
    $binPath = Resolve-WorkspacePath (".\{0}-{1}.bin" -f $OutputPrefix, $safeAxis)
    $planPath = "$binPath.terrainpatchplan.json"
    $mdPath = "$binPath.terrainpatchplan.md"
    $exporter = Join-Path $PSScriptRoot "Export-StoneHillTerrainPatchPlan.ps1"

    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $exporter,
        "-TerrainEditsPath", (Resolve-WorkspacePath $TerrainEditsPath),
        "-SourceLinksPath", (Resolve-WorkspacePath $SourceLinksPath),
        "-ImagePath", (Resolve-WorkspacePath $ImagePath),
        "-OutPath", $binPath,
        "-PlanPath", $planPath,
        "-MarkdownPath", $mdPath,
        "-SourceAxis", $Axis,
        "-SourceUnitsPerZ", ([string]$SourceUnitsPerZ),
        "-MaxLinkDistance", ([string]$MaxLinkDistance),
        "-MaxSourceAliasesPerRuntime", ([string]$MaxSourceAliasesPerRuntime)
    )
    if ($WriteBin) {
        $args += "-AllowExperimentalWrite"
    }
    else {
        $args += "-PlanOnly"
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & powershell.exe @args 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        return [ordered]@{
            axis = $Axis
            status = "export-failed"
            patchCount = 0
            linkedRuntimeVertexCount = 0
            terrainEditCount = 0
            binPath = $binPath
            cuePath = [System.IO.Path]::ChangeExtension($binPath, ".cue")
            planPath = $planPath
            markdownPath = $mdPath
            wroteBin = $false
            error = ($output -join "`n")
            exporterOutput = @($output)
        }
    }

    $plan = Read-JsonIfPresent $planPath
    if ($null -eq $plan) { throw "Terrain exporter did not write plan: $planPath" }
    return [ordered]@{
        axis = $Axis
        status = [string](Get-Field $plan "status" "")
        patchCount = [int](Get-Field $plan "patchCount" 0)
        linkedRuntimeVertexCount = [int](Get-Field $plan "linkedRuntimeVertexCount" 0)
        terrainEditCount = [int](Get-Field $plan "terrainEditCount" 0)
        binPath = $binPath
        cuePath = [System.IO.Path]::ChangeExtension($binPath, ".cue")
        planPath = $planPath
        markdownPath = $mdPath
        wroteBin = [bool]($WriteBin -and (Test-Path -LiteralPath $binPath))
        exporterOutput = @($output)
    }
}

function Write-Markdown($Report, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Axis Test Batches")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Report.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("Mode: $($Report.mode)")
    [void]$lines.Add("")
    [void]$lines.Add("- Terrain edits: $($Report.terrainEditCount)")
    [void]$lines.Add("- Source units per Z: $($Report.parameters.sourceUnitsPerZ)")
    [void]$lines.Add("- Max link distance: $($Report.parameters.maxLinkDistance)")
    [void]$lines.Add("- Max source aliases per runtime vertex: $($Report.parameters.maxSourceAliasesPerRuntime)")
    [void]$lines.Add("")
    [void]$lines.Add("| Axis | Status | Patches | Linked vertices | BIN written | Plan |")
    [void]$lines.Add("|---|---|---:|---:|---|---|")
    foreach ($batch in @($Report.batches)) {
        $planName = if ([string]::IsNullOrWhiteSpace([string]$batch.planPath)) { "-" } else { [System.IO.Path]::GetFileName([string]$batch.planPath) }
        $binText = if ([bool]$batch.wroteBin) { "yes" } else { "no" }
        [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} |" -f $batch.axis, $batch.status, $batch.patchCount, $batch.linkedRuntimeVertexCount, $binText, $planName))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## How To Use")
    [void]$lines.Add("")
    if ([string]$Report.mode -eq "plans-only") {
        [void]$lines.Add("This run created patch plans only. After saving one small terrain edit, rerun with `-WriteBins` to create three CUE/BIN test builds.")
    }
    else {
        [void]$lines.Add("Fresh-load each generated CUE in DuckStation. The axis whose build visibly raises/lowers the intended terrain patch is the packed height field to use for permanent terrain export.")
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedTerrainEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSourceLinks = Resolve-WorkspacePath $SourceLinksPath
$resolvedImage = Resolve-WorkspacePath $ImagePath
if (-not (Test-Path -LiteralPath $resolvedSourceLinks)) { throw "Missing source links: $resolvedSourceLinks" }
if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing source image: $resolvedImage" }

$terrainEditsRoot = Read-JsonIfPresent $resolvedTerrainEdits
$terrainEditCount = if ($null -ne $terrainEditsRoot) { @(Get-ArrayField $terrainEditsRoot "edits").Count } else { 0 }

if ($WriteBins -and $terrainEditCount -le 0) {
    throw "Refusing to write axis-test BINs because no terrain edits were found at $resolvedTerrainEdits. Save one tiny terrain edit first."
}

$batches = New-Object System.Collections.ArrayList
foreach ($axis in $Axes) {
    if ($terrainEditCount -le 0) {
        [void]$batches.Add([ordered]@{
            axis = $axis
            status = "no-terrain-edits"
            patchCount = 0
            linkedRuntimeVertexCount = 0
            terrainEditCount = 0
            binPath = ""
            cuePath = ""
            planPath = ""
            markdownPath = ""
            wroteBin = $false
            exporterOutput = @()
        })
    }
    else {
        [void]$batches.Add((Invoke-TerrainExporter $axis ([bool]$WriteBins)))
    }
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    mode = if ($WriteBins) { "experimental-bin-axis-test" } else { "plans-only" }
    terrainEditsPath = if (Test-Path -LiteralPath $resolvedTerrainEdits) { (Resolve-Path -LiteralPath $resolvedTerrainEdits).Path } else { $resolvedTerrainEdits }
    sourceLinksPath = (Resolve-Path -LiteralPath $resolvedSourceLinks).Path
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    terrainEditCount = $terrainEditCount
    parameters = [ordered]@{
        sourceUnitsPerZ = $SourceUnitsPerZ
        maxLinkDistance = $MaxLinkDistance
        maxSourceAliasesPerRuntime = $MaxSourceAliasesPerRuntime
    }
    batches = @($batches.ToArray())
}

$resolvedOutJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedOutMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8
Write-Markdown ([pscustomobject]$report) $resolvedOutMd

Write-Host "Wrote terrain axis batch manifest to $resolvedOutJson"
Write-Host "Wrote terrain axis batch summary to $resolvedOutMd"
Write-Host ("Mode: {0}; terrain edits: {1}; batches: {2}" -f $report.mode, $terrainEditCount, $Axes.Count)
