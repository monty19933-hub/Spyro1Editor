param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.json",
    [string]$MarkdownPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.md",
    [switch]$IncludeCollisionLp,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

$findScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$exportScript = Join-Path $PSScriptRoot "Export-StoneHillRuntimeTerrainPatchTest.ps1"
if (-not (Test-Path -LiteralPath $findScript)) { throw "Missing source finder: $findScript" }
if (-not (Test-Path -LiteralPath $exportScript)) { throw "Missing terrain exporter: $exportScript" }

$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath

Write-Host "Searching WAD for exact runtime terrain sectors..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $findScript `
    -ImagePath (Resolve-WorkspacePath $ImagePath) `
    -RamPath (Resolve-WorkspacePath $RamPath) `
    -TerrainEditsPath (Resolve-WorkspacePath $TerrainEditsPath) `
    -QuickSectorOnly `
    -OutJsonPath $resolvedSourceSearch `
    -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.md")
if ($LASTEXITCODE -ne 0) { throw "Runtime terrain source search failed." }

Write-Host "Exporting runtime terrain patch plan..."
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $exportScript,
    "-ImagePath", (Resolve-WorkspacePath $ImagePath),
    "-RamPath", (Resolve-WorkspacePath $RamPath),
    "-TerrainEditsPath", (Resolve-WorkspacePath $TerrainEditsPath),
    "-SourceSearchPath", $resolvedSourceSearch,
    "-OutPath", (Resolve-WorkspacePath $OutPath),
    "-PlanPath", (Resolve-WorkspacePath $PlanPath),
    "-MarkdownPath", (Resolve-WorkspacePath $MarkdownPath)
)
if (-not [string]::IsNullOrWhiteSpace($CuePath)) {
    $args += "-CuePath"
    $args += (Resolve-WorkspacePath $CuePath)
}
if ($PlanOnly) {
    $args += "-PlanOnly"
}
else {
    $args += "-AllowExperimentalWrite"
}
if ($IncludeCollisionLp) {
    $args += "-IncludeCollisionLp"
}

& powershell.exe @args
if ($LASTEXITCODE -ne 0) { throw "Runtime terrain patch export failed." }

Write-Host "Runtime terrain patch workflow complete."
