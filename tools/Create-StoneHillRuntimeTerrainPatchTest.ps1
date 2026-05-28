param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$CustomTexturesPath = "",
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

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
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

function Read-ArrayCount([string]$Path, [string]$Name) {
    $resolved = Resolve-WorkspacePath $Path
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $resolved)) { return 0 }
    $root = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    return @(Get-ArrayField $root $Name).Count
}

$findScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$exportScript = Join-Path $PSScriptRoot "Export-StoneHillRuntimeTerrainPatchTest.ps1"
if (-not (Test-Path -LiteralPath $findScript)) { throw "Missing source finder: $findScript" }
if (-not (Test-Path -LiteralPath $exportScript)) { throw "Missing terrain exporter: $exportScript" }

$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedCustomTextures = if ([string]::IsNullOrWhiteSpace($CustomTexturesPath)) { "" } else { Resolve-WorkspacePath $CustomTexturesPath }
$terrainEditCount = Read-ArrayCount $TerrainEditsPath "edits"
$customTextureCount = if ([string]::IsNullOrWhiteSpace($resolvedCustomTextures)) { 0 } else { Read-ArrayCount $resolvedCustomTextures "textures" }

if (($terrainEditCount + $customTextureCount) -le 0) {
    throw "No saved terrain edits or custom texture imports were found."
}

if ($terrainEditCount -gt 0) {
    Write-Host "Searching WAD for exact runtime terrain sectors..."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $findScript `
        -ImagePath (Resolve-WorkspacePath $ImagePath) `
        -RamPath (Resolve-WorkspacePath $RamPath) `
        -TerrainEditsPath (Resolve-WorkspacePath $TerrainEditsPath) `
        -QuickSectorOnly `
        -OutJsonPath $resolvedSourceSearch `
        -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.md")
    if ($LASTEXITCODE -ne 0) { throw "Runtime terrain source search failed." }
}

Write-Host "Exporting runtime terrain patch plan..."
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $exportScript,
    "-ImagePath", (Resolve-WorkspacePath $ImagePath),
    "-RamPath", (Resolve-WorkspacePath $RamPath),
    "-TerrainEditsPath", (Resolve-WorkspacePath $TerrainEditsPath),
    "-OutPath", (Resolve-WorkspacePath $OutPath),
    "-PlanPath", (Resolve-WorkspacePath $PlanPath),
    "-MarkdownPath", (Resolve-WorkspacePath $MarkdownPath)
)
if ($terrainEditCount -gt 0) {
    $args += "-SourceSearchPath"
    $args += $resolvedSourceSearch
}
if ($customTextureCount -gt 0) {
    $args += "-CustomTexturesPath"
    $args += $resolvedCustomTextures
}
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
