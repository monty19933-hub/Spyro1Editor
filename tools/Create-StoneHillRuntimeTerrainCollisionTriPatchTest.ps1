param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$CollisionReportPath = ".\stonehill-collision-bridge-report.json",
    [string]$CollisionTriangleWadBaseOffset = "0xCE4338",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrain-colltritest.bin",
    [string]$CuePath = "",
    [switch]$RebuildCollisionIndex,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

$findTerrainScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$terrainScript = Join-Path $PSScriptRoot "Export-StoneHillRuntimeTerrainPatchTest.ps1"
$collisionReportScript = Join-Path $PSScriptRoot "New-StoneHillCollisionBridgeReport.ps1"
$collisionPatchScript = Join-Path $PSScriptRoot "Apply-StoneHillCollisionTrianglePatch.ps1"
foreach ($script in @($findTerrainScript, $terrainScript, $collisionReportScript, $collisionPatchScript)) {
    if (-not (Test-Path -LiteralPath $script)) { throw "Missing required script: $script" }
}

$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedCollisionReport = Resolve-WorkspacePath $CollisionReportPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }

Write-Host "Searching WAD for exact runtime terrain sectors..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $findTerrainScript `
    -ImagePath (Resolve-WorkspacePath $ImagePath) `
    -RamPath (Resolve-WorkspacePath $RamPath) `
    -TerrainEditsPath (Resolve-WorkspacePath $TerrainEditsPath) `
    -QuickSectorOnly `
    -OutJsonPath $resolvedSourceSearch `
    -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.md")
if ($LASTEXITCODE -ne 0) { throw "Runtime terrain source search failed." }

Write-Host "Writing visual terrain patch image..."
$terrainArgs = @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", $terrainScript,
    "-ImagePath", (Resolve-WorkspacePath $ImagePath),
    "-RamPath", (Resolve-WorkspacePath $RamPath),
    "-TerrainEditsPath", (Resolve-WorkspacePath $TerrainEditsPath),
    "-SourceSearchPath", $resolvedSourceSearch,
    "-OutPath", $resolvedOut,
    "-CuePath", $resolvedCue,
    "-PlanPath", "$resolvedOut.terrainpatchplan.json",
    "-MarkdownPath", "$resolvedOut.terrainpatchplan.md"
)
if ($PlanOnly) { $terrainArgs += "-PlanOnly" } else { $terrainArgs += "-AllowExperimentalWrite" }
& powershell.exe @terrainArgs
if ($LASTEXITCODE -ne 0) { throw "Runtime terrain patch export failed." }

Write-Host "Mapping terrain face to packed collision triangles..."
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $collisionReportScript `
    -ImagePath (Resolve-WorkspacePath $ImagePath) `
    -RamPath (Resolve-WorkspacePath $RamPath) `
    -TerrainEditsPath (Resolve-WorkspacePath $TerrainEditsPath) `
    -OutJsonPath $resolvedCollisionReport `
    -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-collision-bridge-report.md") `
    -CollisionTriangleWadBaseOffset $CollisionTriangleWadBaseOffset `
    -SearchWad
if ($LASTEXITCODE -ne 0) { throw "Collision bridge report failed." }

Write-Host "Applying packed collision triangle patch..."
$collisionArgs = @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", $collisionPatchScript,
    "-ImagePath", $(if ($PlanOnly) { Resolve-WorkspacePath $ImagePath } else { $resolvedOut }),
    "-CollisionReportPath", $resolvedCollisionReport,
    "-OutPath", $resolvedOut,
    "-CuePath", $resolvedCue,
    "-RamPath", (Resolve-WorkspacePath $RamPath),
    "-PlanPath", "$resolvedOut.collisionpatchplan.json",
    "-MarkdownPath", "$resolvedOut.collisionpatchplan.md"
)
if ($RebuildCollisionIndex) { $collisionArgs += "-RebuildCollisionIndex" }
if ($PlanOnly) { $collisionArgs += "-PlanOnly" } else { $collisionArgs += "-AllowExperimentalWrite" }
& powershell.exe @collisionArgs
if ($LASTEXITCODE -ne 0) { throw "Collision triangle patch failed." }

Write-Host "Runtime terrain + collision triangle patch workflow complete."
if (-not $PlanOnly) {
    Write-Host "Fresh-load CUE: $resolvedCue"
}
