param(
    [string]$LevelKey = "Artisans",
    [string]$NativeEditsPath = "",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = ".\_local\experiments\editor-artisans-springpair-ingame.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [int]$ControllerIndex = 174,
    [int]$ShellIndex = 175,
    [int]$SpawnGemIndex = 172,
    [int]$RewardGemIdByte = 0x55,
    [int]$ControllerActorId = -1,
    [uint32]$LevelPointer = ([Convert]::ToUInt32("8016D3E8", 16))
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Quote-Display([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    return '"' + $Path + '"'
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
}

function Set-JsonProperty($Object, [string]$Name, $Value) {
    if ($null -eq $Object) { return }
    if ($Object.PSObject.Properties.Name -contains $Name) {
        $Object.$Name = $Value
    }
    else {
        Add-Member -InputObject $Object -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function New-StoneHillHiddenSpawnEdit($ShellEdit, [int]$TrueIndex) {
    return [ordered]@{
        index = $TrueIndex
        trueIndex = $TrueIndex
        label = "Spring Chest reward spawn row (Stone Hill export helper)"
        typeHex = "0x20"
        stateHex = "0x00"
        runtimeAddress = "0x00000000"
        patchStatus = "append-patchable"
        patchLead = "append source record T$TrueIndex from Stone Hill donor T79"
        behaviorNote = "Hidden runtime row reserved for the Stone Hill spring chest helper to turn into the collectible reward gem."
        original = [ordered]@{ x = 0.0; y = 0.0; z = 0.0 }
        edited = [ordered]@{ x = 0.0; y = 0.0; z = -4096.0 }
        rawOriginal = [ordered]@{ x = 0; y = 0; z = 0 }
        rawEdited = [ordered]@{ x = 0; y = 0; z = -65536 }
        rawDelta = [ordered]@{ x = 0; y = 0; z = -65536 }
        recordMutation = [ordered]@{
            mode = "appendFromSource"
            sourceIndex = -1
            sourceTrueIndex = 79
            sourceLabel = "Stone Hill local gem donor"
            sourceLevelKey = "StoneHill"
            sourceLevelName = "Stone Hill"
            sourceFamily = "gem"
            runtimeIdentityPolicy = "local-gem-spawn-row-for-spring-helper"
            targetTrueIndex = $TrueIndex
            note = "True add: append a local Stone Hill source row that the in-game helper repurposes as the collectible reward gem."
        }
    }
}

function New-DarkHollowHiddenSpawnEdit($ShellEdit, [int]$TrueIndex) {
    return [ordered]@{
        index = $TrueIndex
        trueIndex = $TrueIndex
        label = "Spring Chest reward spawn row (Dark Hollow export helper)"
        typeHex = "0x18"
        stateHex = "0x00"
        runtimeAddress = "0x00000000"
        patchStatus = "append-patchable"
        patchLead = "append source record T$TrueIndex from Dark Hollow donor T56"
        behaviorNote = "Hidden runtime row reserved for the Dark Hollow spring chest helper to turn into the collectible reward gem."
        original = [ordered]@{ x = 0.0; y = 0.0; z = 0.0 }
        edited = [ordered]@{ x = 0.0; y = 0.0; z = -4096.0 }
        rawOriginal = [ordered]@{ x = 0; y = 0; z = 0 }
        rawEdited = [ordered]@{ x = 0; y = 0; z = -65536 }
        rawDelta = [ordered]@{ x = 0; y = 0; z = -65536 }
        recordMutation = [ordered]@{
            mode = "appendFromSource"
            sourceIndex = 56
            sourceTrueIndex = 56
            sourceLabel = "Dark Hollow local loose gem donor"
            sourceLevelKey = "DarkHollow"
            sourceLevelName = "Dark Hollow"
            sourceFamily = "gem"
            runtimeIdentityPolicy = "local-gem-spawn-row-for-spring-helper"
            targetTrueIndex = $TrueIndex
            note = "True add: append a local Dark Hollow source row that the in-game helper repurposes as the collectible reward gem."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($NativeEditsPath)) {
    throw "-NativeEditsPath is required for the spring chest pair exporter."
}
if ($RewardGemIdByte -lt 0x53 -or $RewardGemIdByte -gt 0x57) {
    throw "-RewardGemIdByte must be 0x53..0x57."
}

$exportScript = Join-Path $PSScriptRoot "Export-SpyroLevelMobyPatchTest.ps1"
$patchScript = Join-Path $PSScriptRoot "Add-SpyroSpringChestInGamePatch.ps1"
if (-not (Test-Path -LiteralPath $exportScript)) { throw "Missing base pair exporter: $exportScript" }
if (-not (Test-Path -LiteralPath $patchScript)) { throw "Missing in-game patch injector: $patchScript" }

$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) { $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue") }
if ([string]::IsNullOrWhiteSpace($PlanPath)) { $PlanPath = "$resolvedOutPath.patchplan.json" }
$resolvedCuePath = Resolve-WorkspacePath $CuePath
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath
$outDir = Split-Path -Parent $resolvedOutPath
if (-not [string]::IsNullOrWhiteSpace($outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$stem = [System.IO.Path]::GetFileNameWithoutExtension($resolvedOutPath)
$pairOutPath = Join-Path $outDir ($stem + "-sourcepair.bin")
$pairCuePath = Join-Path $outDir ($stem + "-sourcepair.cue")
$pairPlanPath = Join-Path $outDir ($stem + "-sourcepair.patchplan.json")
$exportNativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
$normalizedLevelKey = Normalize-LevelKey $LevelKey

if ($normalizedLevelKey -eq "stonehill") {
    if ($ControllerIndex -eq 174) { $ControllerIndex = 195 }
    if ($ShellIndex -eq 175) { $ShellIndex = 196 }
    if ($SpawnGemIndex -eq 172) { $SpawnGemIndex = 197 }
    if ($ControllerActorId -lt 0) { $ControllerActorId = 0x00C2 }
    if ($LevelPointer -eq ([Convert]::ToUInt32("8016D3E8", 16))) { $LevelPointer = [Convert]::ToUInt32("80173658", 16) }

    $root = Get-Content -LiteralPath $exportNativeEditsPath -Raw | ConvertFrom-Json
    $edits = @($root.edits)
    if ($edits.Count -lt 2) { throw "Stone Hill spring chest export expected controller/shell edits." }
    $controllerEdit = $edits | Where-Object { [int]$_.trueIndex -eq $ControllerIndex } | Select-Object -First 1
    $shellEdit = $edits | Where-Object { [int]$_.trueIndex -eq $ShellIndex } | Select-Object -First 1
    if ($null -eq $controllerEdit) { $controllerEdit = $edits[0] }
    if ($null -eq $shellEdit) { $shellEdit = $edits[1] }

    $controllerEdit.sourceByte36Hex = "0xC2"
    $controllerEdit.sourceByte37Hex = "0x00"
    $controllerEdit.typeHex = "0x20"
    $controllerEdit.stateHex = "0x00"
    $controllerEdit.sourceByte4FHex = "0x00"
    $controllerEdit.flag4AHex = "0x10"
    $controllerEdit.flag4BHex = "0x53"
    $shellEdit.typeHex = "0x20"
    $shellEdit.stateHex = "0x00"
    $shellEdit.flag4AHex = "0x10"
    if ($null -ne $controllerEdit.recordMutation) {
        Set-JsonProperty $controllerEdit.recordMutation "packageImportProfile" "Local00C2Shell0149Over000E"
        Set-JsonProperty $controllerEdit.recordMutation "runtimeIdentityPolicy" "stonehill-local-00c2-controller-with-imported-0149-shell"
    }
    if ($null -ne $shellEdit.recordMutation) {
        Set-JsonProperty $shellEdit.recordMutation "packageImportProfile" "Local00C2Shell0149Over000E"
        Set-JsonProperty $shellEdit.recordMutation "runtimeIdentityPolicy" "stonehill-local-00c2-controller-with-imported-0149-shell"
    }

    $spawnEdit = New-StoneHillHiddenSpawnEdit $shellEdit $SpawnGemIndex
    $root.edits = @($controllerEdit, $shellEdit, $spawnEdit)
    $root.editCount = 3
    $stoneHillEditsPath = Join-Path $outDir ($stem + "-stonehill-export-edits.json")
    $root | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $stoneHillEditsPath -Encoding UTF8
    $exportNativeEditsPath = $stoneHillEditsPath
}
elseif ($normalizedLevelKey -eq "darkhollow") {
    throw "Dark Hollow spring chest export is deferred: current probes create a collectible sparkle but the visible reward gem mesh remains invisible. Artisans and Stone Hill are the official supported levels for this exporter."
}
elseif ($ControllerActorId -lt 0) {
    $ControllerActorId = 0x01FE
}

Write-Host "Creating source spring chest pair BIN..."
& $exportScript `
    -LevelKey $LevelKey `
    -NativeEditsPath $exportNativeEditsPath `
    -ImagePath $ImagePath `
    -AppendPolicy All `
    -SkipTreasureTotalPatch `
    -ExperimentalImportExternalChestPackages `
    -ExperimentalExternalChestPackageMode ReplaceUnusedRoot `
    -AllowExperimentalPackageRootRegistration `
    -OutPath $pairOutPath `
    -CuePath $pairCuePath `
    -PlanPath $pairPlanPath

if (-not (Test-Path -LiteralPath $pairOutPath)) {
    throw "Base spring chest pair export did not create $(Quote-Display $pairOutPath)."
}

Write-Host "Injecting proven small-cave in-game spring chest pop/collect patch..."
& $patchScript `
    -ImagePath $pairOutPath `
    -OutPath $resolvedOutPath `
    -CuePath $resolvedCuePath `
    -PlanPath $resolvedPlanPath `
    -LevelPointer $LevelPointer `
    -ControllerIndex $ControllerIndex `
    -ShellIndex $ShellIndex `
    -SpawnGemIndex $SpawnGemIndex `
    -ControllerActorId $ControllerActorId `
    -RewardGemIdByte $RewardGemIdByte `
    -PayloadAddress ([Convert]::ToUInt32("8007314C", 16)) `
    -PayloadReserveBytes 0x400 `
    -PayloadStateOffset 0x300 `
    -ScratchSaveAddress ([Convert]::ToUInt32("800740C0", 16)) `
    -SmallCaveMinimalPop

Write-Host "Wrote spring chest in-game test BIN to $resolvedOutPath"
Write-Host "Wrote CUE to $resolvedCuePath"
Write-Host "Wrote in-game patch plan to $resolvedPlanPath"
