param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$GeometryPath = ".\stonehill-wad-ground-model-candidate4-oriented-xz-overlay.json",
    [int]$GeometryCandidateIndex = 0,
    [string]$DiffPath = ".\stonehill-ram-gem-clean-diff.json",
    [switch]$DeepSearch,
    [switch]$ForceFocusedPairs
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Resolve-WorkspacePath([string]$Path) {
    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Test-WorkspaceFile([string]$Path) {
    return (Test-Path -LiteralPath (Resolve-WorkspacePath $Path) -PathType Leaf)
}

function Invoke-WorkspaceTool([string]$Label, [string]$ToolPath, [string[]]$Arguments) {
    $resolvedTool = Resolve-WorkspacePath $ToolPath
    if (-not (Test-Path -LiteralPath $resolvedTool -PathType Leaf)) {
        throw "Missing tool for ${Label}: $resolvedTool"
    }

    Write-Host "== $Label"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $resolvedTool @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

function Invoke-OptionalWorkspaceTool([string]$Label, [string]$ToolPath, [string[]]$Arguments, [string[]]$RequiredInputs) {
    foreach ($inputPath in $RequiredInputs) {
        if (-not (Test-WorkspaceFile $inputPath)) {
            Write-Host "== $Label (skipped; missing $inputPath)"
            return
        }
    }
    Invoke-WorkspaceTool $Label $ToolPath $Arguments
}

$summary = [ordered]@{
    startedAt = (Get-Date).ToString("s")
    imagePath = Resolve-WorkspacePath $ImagePath
    ramPath = Resolve-WorkspacePath $RamPath
    geometryPath = Resolve-WorkspacePath $GeometryPath
    deepSearch = [bool]$DeepSearch
    steps = New-Object System.Collections.Generic.List[string]
}

if ((Test-WorkspaceFile ".\stonehill-before-gem-clean.bin") -and (Test-WorkspaceFile ".\stonehill-after-gem-clean.bin")) {
    Invoke-WorkspaceTool "Compare gem-clean RAM pair" ".\tools\Compare-SpyroRamMobys.ps1" @(
        "-BeforePath", ".\stonehill-before-gem-clean.bin",
        "-AfterPath", ".\stonehill-after-gem-clean.bin",
        "-OutPath", $DiffPath
    )
    [void]$summary.steps.Add("Compared gem-clean RAM pair")
}
else {
    Write-Host "== Compare gem-clean RAM pair (skipped; before/after dumps missing)"
}

$focusedArgs = @(
    "-ManifestPath", ".\stonehill-focused-ram-pairs.json",
    "-OutJsonPath", ".\stonehill-focused-ram-pair-report.json",
    "-OutMarkdownPath", ".\stonehill-focused-ram-pair-report.md",
    "-CompareToolPath", ".\tools\Compare-SpyroRamMobys.ps1"
)
if ($ForceFocusedPairs) { $focusedArgs += "-Force" }
Invoke-WorkspaceTool "Update focused RAM pair report" ".\tools\Update-StoneHillFocusedRamPairReport.ps1" $focusedArgs
[void]$summary.steps.Add("Updated focused RAM pair report")

Invoke-OptionalWorkspaceTool "Refresh base moby map and catalog" ".\tools\Render-StoneHillMobyMap.ps1" @(
    "-RamPath", $RamPath,
    "-GeometryPath", $GeometryPath,
    "-GeometryCandidateIndex", ([string]$GeometryCandidateIndex),
    "-DiffPath", $DiffPath,
    "-ReplicaPlanPath", ".\stonehill-replica-map-plan.json",
    "-OutImagePath", ".\stonehill-moby-map.png",
    "-OutCatalogPath", ".\stonehill-moby-catalog.json"
) @($RamPath, $GeometryPath, $DiffPath)
[void]$summary.steps.Add("Refreshed base moby map and catalog")

Invoke-OptionalWorkspaceTool "Analyze Stone Hill special-data pointers" ".\tools\Analyze-StoneHillMobySpecialData.ps1" @(
    "-RamPath", $RamPath,
    "-CatalogPath", ".\stonehill-moby-catalog.json",
    "-ImagePath", $ImagePath,
    "-OutJsonPath", ".\stonehill-moby-special-data.json",
    "-OutMarkdownPath", ".\stonehill-moby-special-data.md"
) @($RamPath, ".\stonehill-moby-catalog.json", $ImagePath)
[void]$summary.steps.Add("Refreshed special-data report when inputs were present")

if ($DeepSearch) {
    Invoke-OptionalWorkspaceTool "Find Stone Hill moby source links" ".\tools\Find-StoneHillMobySourceLinks.ps1" @(
        "-ImagePath", $ImagePath,
        "-CatalogPath", ".\stonehill-moby-catalog.json",
        "-OutPath", ".\stonehill-moby-source-links.json"
    ) @($ImagePath, ".\stonehill-moby-catalog.json")
    [void]$summary.steps.Add("Ran deep WAD source-link search")
}
else {
    Write-Host "== Find Stone Hill moby source links (skipped; pass -DeepSearch to rescan WAD windows)"
}

Invoke-OptionalWorkspaceTool "Rank source-window layouts" ".\tools\Find-StoneHillSourceWindowLayouts.ps1" @(
    "-SourceLinksPath", ".\stonehill-moby-source-links.json",
    "-OutPath", ".\stonehill-source-window-layouts.json"
) @(".\stonehill-moby-source-links.json")
[void]$summary.steps.Add("Ranked source-window layouts")

Invoke-OptionalWorkspaceTool "Build replica map plan" ".\tools\New-StoneHillReplicaMapPlan.ps1" @(
    "-CatalogPath", ".\stonehill-moby-catalog.json",
    "-SourceLinksPath", ".\stonehill-moby-source-links.json",
    "-SourceLayoutsPath", ".\stonehill-source-window-layouts.json",
    "-OutPath", ".\stonehill-replica-map-plan.json"
) @(".\stonehill-moby-catalog.json", ".\stonehill-moby-source-links.json", ".\stonehill-source-window-layouts.json")
[void]$summary.steps.Add("Rebuilt replica map plan")

Invoke-OptionalWorkspaceTool "Render labelled moby map and catalog" ".\tools\Render-StoneHillMobyMap.ps1" @(
    "-RamPath", $RamPath,
    "-GeometryPath", $GeometryPath,
    "-GeometryCandidateIndex", ([string]$GeometryCandidateIndex),
    "-DiffPath", $DiffPath,
    "-ReplicaPlanPath", ".\stonehill-replica-map-plan.json",
    "-OutImagePath", ".\stonehill-moby-map.png",
    "-OutCatalogPath", ".\stonehill-moby-catalog.json"
) @($RamPath, $GeometryPath, $DiffPath)
[void]$summary.steps.Add("Rendered labelled moby map and catalog")

Invoke-OptionalWorkspaceTool "Build target board" ".\tools\New-StoneHillReplicaTargetBoard.ps1" @(
    "-ReplicaPlanPath", ".\stonehill-replica-map-plan.json",
    "-OutJsonPath", ".\stonehill-replica-target-board.json",
    "-OutMarkdownPath", ".\stonehill-replica-target-board.md"
) @(".\stonehill-replica-map-plan.json")
[void]$summary.steps.Add("Rebuilt target board")

Invoke-OptionalWorkspaceTool "Build patch-test matrix" ".\tools\New-StoneHillPatchTestMatrix.ps1" @(
    "-CatalogPath", ".\stonehill-moby-catalog.json",
    "-SourceLinksPath", ".\stonehill-moby-source-links.json",
    "-SourceLayoutsPath", ".\stonehill-source-window-layouts.json",
    "-TargetBoardPath", ".\stonehill-replica-target-board.json",
    "-OutJsonPath", ".\stonehill-patch-test-matrix.json",
    "-OutMarkdownPath", ".\stonehill-patch-test-matrix.md"
) @(".\stonehill-moby-catalog.json", ".\stonehill-moby-source-links.json", ".\stonehill-source-window-layouts.json", ".\stonehill-replica-target-board.json")
[void]$summary.steps.Add("Rebuilt patch-test matrix")

Invoke-OptionalWorkspaceTool "Build readiness report" ".\tools\New-StoneHillReplicaReadinessReport.ps1" @(
    "-CatalogPath", ".\stonehill-moby-catalog.json",
    "-SourceLinksPath", ".\stonehill-moby-source-links.json",
    "-ReplicaPlanPath", ".\stonehill-replica-map-plan.json",
    "-TargetBoardPath", ".\stonehill-replica-target-board.json",
    "-FocusedPairReportPath", ".\stonehill-focused-ram-pair-report.json",
    "-OutJsonPath", ".\stonehill-replica-readiness.json",
    "-OutMarkdownPath", ".\stonehill-replica-readiness.md"
) @(".\stonehill-replica-target-board.json", ".\stonehill-focused-ram-pair-report.json", ".\stonehill-moby-source-links.json")
[void]$summary.steps.Add("Rebuilt readiness report")

Invoke-OptionalWorkspaceTool "Build identity matrix" ".\tools\New-StoneHillIdentityMatrix.ps1" @(
    "-CatalogPath", ".\stonehill-moby-catalog.json",
    "-ReplicaPlanPath", ".\stonehill-replica-map-plan.json",
    "-TargetBoardPath", ".\stonehill-replica-target-board.json",
    "-SpecialDataPath", ".\stonehill-moby-special-data.json",
    "-ReadinessPath", ".\stonehill-replica-readiness.json",
    "-OutJsonPath", ".\stonehill-identity-matrix.json",
    "-OutMarkdownPath", ".\stonehill-identity-matrix.md"
) @(".\stonehill-moby-catalog.json", ".\stonehill-replica-map-plan.json", ".\stonehill-replica-target-board.json", ".\stonehill-replica-readiness.json")
[void]$summary.steps.Add("Rebuilt identity matrix")

Invoke-OptionalWorkspaceTool "Build validation playbook" ".\tools\New-StoneHillValidationPlaybook.ps1" @(
    "-TargetBoardPath", ".\stonehill-replica-target-board.json",
    "-PatchTestMatrixPath", ".\stonehill-patch-test-matrix.json",
    "-FocusedPairsPath", ".\stonehill-focused-ram-pairs.json",
    "-ReadinessPath", ".\stonehill-replica-readiness.json",
    "-OutJsonPath", ".\stonehill-validation-playbook.json",
    "-OutMarkdownPath", ".\stonehill-validation-playbook.md"
) @(".\stonehill-replica-target-board.json", ".\stonehill-patch-test-matrix.json", ".\stonehill-focused-ram-pairs.json", ".\stonehill-replica-readiness.json")
[void]$summary.steps.Add("Rebuilt validation playbook")

Invoke-OptionalWorkspaceTool "Build replica dossier" ".\tools\New-StoneHillReplicaDossier.ps1" @(
    "-IdentityPath", ".\stonehill-identity-matrix.json",
    "-ReadinessPath", ".\stonehill-replica-readiness.json",
    "-ValidationPath", ".\stonehill-validation-playbook.json",
    "-FocusedPairReportPath", ".\stonehill-focused-ram-pair-report.json",
    "-SourceLayoutsPath", ".\stonehill-source-window-layouts.json",
    "-OutJsonPath", ".\stonehill-replica-dossier.json",
    "-OutMarkdownPath", ".\stonehill-replica-dossier.md"
) @(".\stonehill-identity-matrix.json", ".\stonehill-replica-readiness.json", ".\stonehill-validation-playbook.json")
[void]$summary.steps.Add("Rebuilt replica dossier")

$summary.completedAt = (Get-Date).ToString("s")
$summaryPath = Resolve-WorkspacePath ".\stonehill-replica-workbench-refresh.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "Stone Hill replica workbench refresh complete."
Write-Host "Summary: $summaryPath"
