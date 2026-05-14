param(
    [string]$CatalogPath = ".\artisans-moby-catalog.json",
    [string]$OutJsonPath = ".\artisans-focused-identity-retest.json",
    [string]$OutMarkdownPath = ".\artisans-focused-identity-retest.md",
    [int[]]$RecordIndexes = @(89, 90, 91),
    [double]$X = 4544,
    [double]$Y = 2880,
    [double]$Z = 560
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

function Convert-DoubleToRaw([double]$Value) {
    return [int][Math]::Round($Value * 16.0)
}

function New-Vector([double]$VX, [double]$VY, [double]$VZ) {
    return [ordered]@{ x = [Math]::Round($VX, 4); y = [Math]::Round($VY, 4); z = [Math]::Round($VZ, 4) }
}

function New-RawVector([double]$VX, [double]$VY, [double]$VZ) {
    return [ordered]@{ x = Convert-DoubleToRaw $VX; y = Convert-DoubleToRaw $VY; z = Convert-DoubleToRaw $VZ }
}

function New-TestEdit($Moby, [double]$VX, [double]$VY, [double]$VZ) {
    $trueIndex = [int](Get-Field $Moby "trueIndex" (Get-Field $Moby "index" -1))
    $originalX = [double](Get-Field $Moby "x" 0)
    $originalY = [double](Get-Field $Moby "y" 0)
    $originalZ = [double](Get-Field $Moby "z" 0)
    $displayLabel = [string](Get-Field $Moby "displayTargetLabel" "")
    return [ordered]@{
        index = $trueIndex
        trueIndex = $trueIndex
        label = "Artisans focused identity retest: T$trueIndex $displayLabel"
        typeHex = [string](Get-Field $Moby "typeHex" "")
        stateHex = [string](Get-Field $Moby "stateHex" "")
        runtimeAddress = ""
        specialDataPointer = [string](Get-Field $Moby "specialDataPointer" "")
        flag4AHex = [string](Get-Field $Moby "flag52Hex" "")
        flag4BHex = [string](Get-Field $Moby "flag53Hex" "")
        patchStatus = "runtime identity retest"
        patchLead = "Live RAM-only Artisans focused identity retest. Does not create a permanent BIN patch."
        behaviorNote = "Temporary one-object Artisans identity-test placement. Revert after observing."
        original = New-Vector $originalX $originalY $originalZ
        edited = New-Vector $VX $VY $VZ
        rawOriginal = New-RawVector $originalX $originalY $originalZ
        rawEdited = New-RawVector $VX $VY $VZ
        rawDelta = [ordered]@{
            x = (Convert-DoubleToRaw $VX) - (Convert-DoubleToRaw $originalX)
            y = (Convert-DoubleToRaw $VY) - (Convert-DoubleToRaw $originalY)
            z = (Convert-DoubleToRaw $VZ) - (Convert-DoubleToRaw $originalZ)
        }
        identityBatch = [ordered]@{
            level = "Artisans"
            batchNumber = 0
            slot = 1
            category = "focused-retest"
            originalLabel = [string](Get-Field $Moby "displayTargetLabel" "")
            confidence = [string](Get-Field $Moby "confidence" "")
        }
    }
}

function Write-BatchFile([string]$Path, [string]$EditsPath, [int]$Index, [switch]$Revert) {
    $command = if ($Revert) {
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `".\tools\Move-DuckStationMoby.ps1`" -MobyIndexes $Index -OriginalsPath `".\artisans-live-moby-originals.json`" -ExpectedLevelId 10 -CatalogPath `".\artisans-moby-catalog.json`" -Revert"
    }
    else {
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `".\tools\Move-DuckStationMoby.ps1`" -MobyIndexes $Index -FromNativeEdit -NativeEditsPath `"$EditsPath`" -OriginalsPath `".\artisans-live-moby-originals.json`" -ExpectedLevelId 10 -CatalogPath `".\artisans-moby-catalog.json`" -Apply -HoldSeconds 5"
    }

    @(
        "@echo off",
        "setlocal",
        "cd /d ""%~dp0""",
        $command,
        "pause"
    ) | Set-Content -LiteralPath $Path -Encoding ASCII
}

$resolvedCatalog = Resolve-WorkspacePath $CatalogPath
if (-not (Test-Path -LiteralPath $resolvedCatalog)) { throw "Missing Artisans catalog: $resolvedCatalog" }

$catalog = Get-Content -LiteralPath $resolvedCatalog -Raw | ConvertFrom-Json
$byIndex = @{}
foreach ($moby in @($catalog.mobys)) {
    $trueIndex = [int](Get-Field $moby "trueIndex" (Get-Field $moby "index" -1))
    if ($trueIndex -ge 0) { $byIndex[$trueIndex] = $moby }
}

$summaries = New-Object System.Collections.ArrayList
foreach ($index in $RecordIndexes) {
    if (-not $byIndex.ContainsKey([int]$index)) { throw "Catalog does not contain Artisans moby T$index" }
    $moby = $byIndex[[int]$index]
    $editFileName = "artisans-focused-t$index-edits.json"
    $applyFileName = "Apply Artisans Focus T$index Live.bat"
    $revertFileName = "Revert Artisans Focus T$index Live.bat"
    $editPath = Resolve-WorkspacePath ".\$editFileName"
    $applyPath = Resolve-WorkspacePath ".\$applyFileName"
    $revertPath = Resolve-WorkspacePath ".\$revertFileName"

    $editRoot = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        editor = "NativeSpyroEditor"
        levelName = "Artisans"
        note = "Temporary one-object Artisans focused identity retest."
        editCount = 1
        edits = @((New-TestEdit $moby $X $Y $Z))
    }
    $editRoot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $editPath -Encoding UTF8
    Write-BatchFile $applyPath ".\$editFileName" ([int]$index)
    Write-BatchFile $revertPath ".\$editFileName" ([int]$index) -Revert

    [void]$summaries.Add([ordered]@{
        id = "T$index"
        typeHex = [string](Get-Field $moby "typeHex" "")
        label = [string](Get-Field $moby "displayTargetLabel" "")
        kind = [string](Get-Field $moby "candidateKind" "")
        confidence = [string](Get-Field $moby "confidence" "")
        x = $X
        y = $Y
        z = $Z
        applyBatch = $applyPath
        revertBatch = $revertPath
        editsPath = $editPath
    })
}

$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-ArtisansFocusedIdentityRetest.ps1"
    levelName = "Artisans"
    expectedLevelId = "0x0A"
    note = "Run one Apply file at a time, observe the single moved object, then run its matching Revert file."
    testPoint = [ordered]@{ x = $X; y = $Y; z = $Z }
    records = @($summaries.ToArray())
}

$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMd = Resolve-WorkspacePath $OutMarkdownPath
$root | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Artisans Focused Identity Retest")
[void]$lines.Add("")
[void]$lines.Add("Run one Apply file at a time, observe the single moved object at X $X, Y $Y, Z $Z, then run its matching Revert file.")
[void]$lines.Add("")
[void]$lines.Add("| ID | Type | Current label | Kind | Confidence | Apply | Revert |")
[void]$lines.Add("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($summary in @($summaries.ToArray())) {
    [void]$lines.Add("| $($summary.id) | $($summary.typeHex) | $($summary.label) | $($summary.kind) | $($summary.confidence) | $([IO.Path]::GetFileName([string]$summary.applyBatch)) | $([IO.Path]::GetFileName([string]$summary.revertBatch)) |")
}
$lines | Set-Content -LiteralPath $resolvedOutMd -Encoding UTF8

Write-Host "Wrote Artisans focused retest plan to $resolvedOutJson"
Write-Host "Wrote Artisans focused retest report to $resolvedOutMd"
Write-Host "Generated $($summaries.Count) focused apply/revert pair(s)."
