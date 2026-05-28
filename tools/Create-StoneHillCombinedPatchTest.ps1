param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$CustomTexturesPath = ".\stonehill-custom-terrain-textures.json",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$OutPath = ".\Spyro the Dragon (USA)-combinedpatchtest.bin",
    [string]$CuePath = "",
    [string]$OutJsonPath = ".\stonehill-combined-patchtest.json",
    [string]$OutMarkdownPath = ".\stonehill-combined-patchtest.md",
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

function Read-EditCount([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if (-not (Test-Path -LiteralPath $resolved)) { return 0 }
    $root = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    return @(Get-ArrayField $root "edits").Count
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [System.IO.Path]::GetFileName($BinPath)
    @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $CueOutPath -Encoding ASCII
}

$loaderScript = Join-Path $PSScriptRoot "Export-StoneHillLoaderTablePatchTest.ps1"
$findTerrainScript = Join-Path $PSScriptRoot "Find-StoneHillRuntimeTerrainSource.ps1"
$terrainScript = Join-Path $PSScriptRoot "Export-StoneHillRuntimeTerrainPatchTest.ps1"
if (-not (Test-Path -LiteralPath $loaderScript)) { throw "Missing loader exporter: $loaderScript" }
if (-not (Test-Path -LiteralPath $findTerrainScript)) { throw "Missing terrain source finder: $findTerrainScript" }
if (-not (Test-Path -LiteralPath $terrainScript)) { throw "Missing terrain exporter: $terrainScript" }

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }
$resolvedJson = Resolve-WorkspacePath $OutJsonPath
$resolvedMd = Resolve-WorkspacePath $OutMarkdownPath
$loaderPlan = "$resolvedOut.loaderpatchplan.json"
$terrainSearch = Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.json"
$terrainPlan = "$resolvedOut.terrainpatchplan.json"
$terrainPlanMd = "$resolvedOut.terrainpatchplan.md"
$resolvedCustomTextures = Resolve-WorkspacePath $CustomTexturesPath

if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing source image: $resolvedImage" }

$mobyEditCount = Read-EditCount $NativeEditsPath
$terrainEditCount = Read-EditCount $TerrainEditsPath
$customTextureCount = if (Test-Path -LiteralPath $resolvedCustomTextures) {
    $customRoot = Get-Content -LiteralPath $resolvedCustomTextures -Raw | ConvertFrom-Json
    @(Get-ArrayField $customRoot "textures").Count
} else { 0 }
if (($mobyEditCount + $terrainEditCount + $customTextureCount) -le 0) {
    throw "No saved moby edits, terrain edits, or custom texture imports were found. Save edits in the editor before creating a combined BIN."
}

if ($mobyEditCount -gt 0) {
    Write-Host "Creating base image with $mobyEditCount moby edit(s)..."
    $loaderArgs = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $loaderScript,
        "-NativeEditsPath", (Resolve-WorkspacePath $NativeEditsPath),
        "-ImagePath", $resolvedImage,
        "-OutPath", $resolvedOut,
        "-CuePath", $resolvedCue,
        "-PlanPath", $loaderPlan
    )
    if ($PlanOnly) { $loaderArgs += "-PlanOnly" }
    & powershell.exe @loaderArgs
    if ($LASTEXITCODE -ne 0) { throw "Moby loader-table export failed." }
}
elseif (-not $PlanOnly) {
    Write-Host "No moby edits found; copying base image before terrain patches..."
    Copy-Item -LiteralPath $resolvedImage -Destination $resolvedOut -Force
    Write-Cue $resolvedOut $resolvedCue
}

if ($terrainEditCount -gt 0 -or $customTextureCount -gt 0) {
    if ($terrainEditCount -gt 0) {
        Write-Host "Searching WAD for exact runtime terrain sectors..."
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $findTerrainScript `
            -ImagePath $resolvedImage `
            -RamPath (Resolve-WorkspacePath $RamPath) `
            -TerrainEditsPath (Resolve-WorkspacePath $TerrainEditsPath) `
            -QuickSectorOnly `
            -OutJsonPath $terrainSearch `
            -OutMarkdownPath (Resolve-WorkspacePath ".\stonehill-runtime-terrain-source-search.md")
        if ($LASTEXITCODE -ne 0) { throw "Runtime terrain source search failed." }
    }

    Write-Host "Applying $terrainEditCount terrain edit(s) and $customTextureCount custom texture import(s) to combined image..."
    $terrainArgs = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $terrainScript,
        "-ImagePath", $(if ($PlanOnly) { $resolvedImage } else { $resolvedOut }),
        "-RamPath", (Resolve-WorkspacePath $RamPath),
        "-TerrainEditsPath", (Resolve-WorkspacePath $TerrainEditsPath),
        "-SourceSearchPath", $terrainSearch,
        "-OutPath", $resolvedOut,
        "-CuePath", $resolvedCue,
        "-PlanPath", $terrainPlan,
        "-MarkdownPath", $terrainPlanMd
    )
    if ($terrainEditCount -gt 0) {
        $terrainArgs += "-SourceSearchPath"
        $terrainArgs += $terrainSearch
    }
    if ($customTextureCount -gt 0) {
        $terrainArgs += "-CustomTexturesPath"
        $terrainArgs += $resolvedCustomTextures
    }
    if ($PlanOnly) { $terrainArgs += "-PlanOnly" } else { $terrainArgs += "-AllowExperimentalWrite" }
    if ($IncludeCollisionLp) { $terrainArgs += "-IncludeCollisionLp" }
    & powershell.exe @terrainArgs
    if ($LASTEXITCODE -ne 0) { throw "Runtime terrain patch export failed." }
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($PlanOnly) { "plan-ready" } else { "combined-bin-written" }
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    outPath = $resolvedOut
    cuePath = $resolvedCue
    mobyEditCount = $mobyEditCount
    terrainEditCount = $terrainEditCount
    customTextureCount = $customTextureCount
    loaderPlanPath = $loaderPlan
    terrainSourceSearchPath = $terrainSearch
    terrainPlanPath = $terrainPlan
    includeCollisionLp = [bool]$IncludeCollisionLp
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Combined Patch Test")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($summary.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("- Status: $($summary.status)")
[void]$lines.Add("- Moby edits: $mobyEditCount")
[void]$lines.Add("- Terrain edits: $terrainEditCount")
[void]$lines.Add("- Custom texture imports: $customTextureCount")
[void]$lines.Add("- Collision LP companion terrain patching: $IncludeCollisionLp")
[void]$lines.Add("- BIN: $resolvedOut")
[void]$lines.Add("- CUE: $resolvedCue")
[void]$lines.Add("")
[void]$lines.Add("Fresh-load the CUE in DuckStation to validate moby and terrain edits together.")
[System.IO.File]::WriteAllLines($resolvedMd, $lines.ToArray(), [System.Text.Encoding]::UTF8)

Write-Host "Combined patch workflow complete."
Write-Host "Wrote summary to $resolvedJson"
Write-Host "Wrote summary markdown to $resolvedMd"
if (-not $PlanOnly) {
    Write-Host "Fresh-load CUE: $resolvedCue"
}
