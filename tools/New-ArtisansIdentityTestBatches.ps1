param(
    [string]$CatalogPath = ".\artisans-moby-catalog.json",
    [string]$OutJsonPath = ".\artisans-identity-test-batches.json",
    [string]$OutMarkdownPath = ".\artisans-identity-test-batches.md",
    [double]$StartX = 5194,
    [double]$StartY = 2880,
    [double]$StartZ = 560,
    [double]$SpecialZ = 620,
    [double]$SpacingX = 900,
    [double]$SpacingY = 520,
    [int]$MaxPerBatch = 3,
    [int]$MaxBatches = 18,
    [int[]]$RecordIndexes = @(),
    [switch]$IncludeControls
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

function New-Vector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = [Math]::Round($X, 4); y = [Math]::Round($Y, 4); z = [Math]::Round($Z, 4) }
}

function New-RawVector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = Convert-DoubleToRaw $X; y = Convert-DoubleToRaw $Y; z = Convert-DoubleToRaw $Z }
}

function Get-Category($Moby) {
    $type = [string](Get-Field $Moby "typeHex" "")
    $kind = ([string](Get-Field $Moby "candidateKind" "")).ToLowerInvariant()
    $label = ([string](Get-Field $Moby "displayTargetLabel" "")).ToLowerInvariant()
    $confidence = ([string](Get-Field $Moby "confidence" "")).ToLowerInvariant()

    if ($type -eq "0x30" -or $type -eq "0x10" -or $type -eq "0x3C" -or $kind -match "dragon|transport|portal") { return "special" }
    if ($kind -match "reward" -or $confidence -eq "byte-inferred") { return "rewards" }
    if ($type -eq "0x20" -and ($label -match "candidate" -or $confidence -eq "unknown")) { return "visible-unknowns" }
    if ($type -eq "0x00" -or $kind -match "control|helper") { return "controls" }
    return "other"
}

function Get-Priority([string]$Category) {
    switch ($Category) {
        "special" { return 10 }
        "rewards" { return 20 }
        "visible-unknowns" { return 30 }
        "other" { return 60 }
        "controls" { return 90 }
        default { return 99 }
    }
}

function Get-Goal([string]$Category) {
    switch ($Category) {
        "special" { return "Confirm dragons, balloonist/transport NPC, and portal-like records first." }
        "rewards" { return "Confirm whether reward-colored 0x20 records are visible loose gems, container rewards, or linked treasure controls." }
        "visible-unknowns" { return "Confirm unknown visible 0x20 records, likely treasure, scenery, or one-off home-world objects." }
        "controls" { return "Control/link records are often invisible; only test after nearby visible families are understood." }
        default { return "Confirm unresolved Artisans records by moving them to the test pad." }
    }
}

function Test-NeedsIdentity($Moby) {
    $type = [string](Get-Field $Moby "typeHex" "")
    $confidence = ([string](Get-Field $Moby "confidence" "")).ToLowerInvariant()
    if ($confidence -eq "live-unavailable") { return $false }
    if ($confidence.StartsWith("live-observed")) { return $false }
    if (($type -eq "0x30" -or $type -eq "0x10" -or $type -eq "0x3C") -and -not $confidence.StartsWith("live-observed")) { return $true }

    $kind = ([string](Get-Field $Moby "candidateKind" "")).ToLowerInvariant()
    $label = ([string](Get-Field $Moby "displayTargetLabel" "")).ToLowerInvariant()
    if ($confidence -eq "signature-inferred") { return $false }
    return ($confidence -eq "unknown" -or $confidence -eq "weak" -or $confidence -match "inferred" -or $kind -match "candidate|control|helper" -or $label -match "candidate|pending")
}

function Clear-OldGeneratedBatchFiles {
    $patterns = @(
        "Apply Artisans Identity Batch * Live.bat",
        "Revert Artisans Identity Batch * Live.bat",
        "artisans-identity-batch-*-edits.json"
    )
    foreach ($pattern in $patterns) {
        Get-ChildItem -LiteralPath $WorkspaceRoot -Filter $pattern -File |
            Remove-Item -Force
    }
}

function New-TestEdit($Moby, [int]$BatchNumber, [int]$Slot, [double]$X, [double]$Y, [double]$Z, [string]$Category) {
    $trueIndex = [int](Get-Field $Moby "trueIndex" (Get-Field $Moby "index" -1))
    $originalX = [double](Get-Field $Moby "x" 0)
    $originalY = [double](Get-Field $Moby "y" 0)
    $originalZ = [double](Get-Field $Moby "z" 0)
    $displayLabel = [string](Get-Field $Moby "displayTargetLabel" "")
    $label = "Artisans identity batch $BatchNumber slot ${Slot}: T$trueIndex $displayLabel"
    return [ordered]@{
        index = $trueIndex
        trueIndex = $trueIndex
        label = $label
        typeHex = [string](Get-Field $Moby "typeHex" "")
        stateHex = [string](Get-Field $Moby "stateHex" "")
        runtimeAddress = ""
        specialDataPointer = [string](Get-Field $Moby "specialDataPointer" "")
        flag4AHex = [string](Get-Field $Moby "flag52Hex" "")
        flag4BHex = [string](Get-Field $Moby "flag53Hex" "")
        patchStatus = "runtime identity test"
        patchLead = "Live RAM-only Artisans identity batch. Does not create a permanent BIN patch."
        behaviorNote = "Temporary Artisans identity-test placement. Revert after observing."
        original = New-Vector $originalX $originalY $originalZ
        edited = New-Vector $X $Y $Z
        rawOriginal = New-RawVector $originalX $originalY $originalZ
        rawEdited = New-RawVector $X $Y $Z
        rawDelta = [ordered]@{
            x = (Convert-DoubleToRaw $X) - (Convert-DoubleToRaw $originalX)
            y = (Convert-DoubleToRaw $Y) - (Convert-DoubleToRaw $originalY)
            z = (Convert-DoubleToRaw $Z) - (Convert-DoubleToRaw $originalZ)
        }
        identityBatch = [ordered]@{
            level = "Artisans"
            batchNumber = $BatchNumber
            slot = $Slot
            category = $Category
            originalLabel = [string](Get-Field $Moby "displayTargetLabel" "")
            confidence = [string](Get-Field $Moby "confidence" "")
        }
    }
}

function Write-BatchFile([string]$Path, [string]$EditsPath, [int[]]$Indexes, [switch]$Revert) {
    $indexText = ($Indexes | ForEach-Object { $_.ToString() }) -join " "
    $command = if ($Revert) {
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `".\tools\Move-DuckStationMoby.ps1`" -MobyIndexes $indexText -OriginalsPath `".\artisans-live-moby-originals.json`" -ExpectedLevelId 10 -CatalogPath `".\artisans-moby-catalog.json`" -Revert"
    }
    else {
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `".\tools\Move-DuckStationMoby.ps1`" -MobyIndexes $indexText -FromNativeEdit -NativeEditsPath `"$EditsPath`" -OriginalsPath `".\artisans-live-moby-originals.json`" -ExpectedLevelId 10 -CatalogPath `".\artisans-moby-catalog.json`" -Apply -HoldSeconds 5"
    }

    @(
        "@echo off",
        "setlocal",
        "cd /d ""%~dp0""",
        $command,
        "pause"
    ) | Set-Content -LiteralPath $Path -Encoding ASCII
}

function Write-RevertAllBatchFile([string]$Path) {
    @(
        "@echo off",
        "setlocal",
        "cd /d ""%~dp0""",
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `".\tools\Move-DuckStationMoby.ps1`" -OriginalsPath `".\artisans-live-moby-originals.json`" -ExpectedLevelId 10 -CatalogPath `".\artisans-moby-catalog.json`" -RevertAll",
        "pause"
    ) | Set-Content -LiteralPath $Path -Encoding ASCII
}

$resolvedCatalog = Resolve-WorkspacePath $CatalogPath
if (-not (Test-Path -LiteralPath $resolvedCatalog)) { throw "Missing Artisans catalog: $resolvedCatalog" }
Clear-OldGeneratedBatchFiles

$catalog = Get-Content -LiteralPath $resolvedCatalog -Raw | ConvertFrom-Json
$requested = @($RecordIndexes | ForEach-Object { [int]$_ })
$requestedSet = @{}
foreach ($index in $requested) { $requestedSet[$index] = $true }

$candidates = @()
foreach ($moby in @($catalog.mobys)) {
    $trueIndex = [int](Get-Field $moby "trueIndex" (Get-Field $moby "index" -1))
    if ($trueIndex -lt 0) { continue }
    if ($requested.Count -gt 0 -and -not $requestedSet.ContainsKey($trueIndex)) { continue }
    if ($requested.Count -eq 0 -and -not (Test-NeedsIdentity $moby)) { continue }

    $category = Get-Category $moby
    if (-not $IncludeControls -and $category -eq "controls") { continue }
    $candidates += [pscustomobject]@{
        moby = $moby
        trueIndex = $trueIndex
        category = $category
        priority = Get-Priority $category
    }
}

$candidates = @($candidates | Sort-Object priority, trueIndex)
if ($candidates.Count -eq 0) {
    $resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
    $resolvedOutMd = Resolve-WorkspacePath $OutMarkdownPath
    $root = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        generatedBy = "New-ArtisansIdentityTestBatches.ps1"
        levelName = "Artisans"
        sourceCatalog = (Resolve-Path -LiteralPath $resolvedCatalog).Path
        expectedLevelId = "0x0A"
        testPad = [ordered]@{ startX = $StartX; startY = $StartY; startZ = $StartZ; specialZ = $SpecialZ; spacingX = $SpacingX; spacingY = $SpacingY }
        note = "No unresolved Artisans identity candidates matched the current filters."
        batchCount = 0
        batches = @()
    }
    $root | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8
    Write-RevertAllBatchFile (Resolve-WorkspacePath ".\Revert All Artisans Identity Live.bat")

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Artisans Identity Test Batches")
    [void]$lines.Add("")
    [void]$lines.Add("No unresolved Artisans identity candidates remain for the current filters.")
    [void]$lines.Add("")
    [void]$lines.Add("- Expected live level ID: 0x0A")
    [void]$lines.Add("- Batches generated: 0")
    $lines | Set-Content -LiteralPath $resolvedOutMd -Encoding UTF8

    Write-Host "Wrote Artisans identity batch plan to $resolvedOutJson"
    Write-Host "Wrote Artisans identity batch report to $resolvedOutMd"
    Write-Host "Generated 0 apply/revert batch pair(s)."
    return
}

$batches = New-Object System.Collections.ArrayList
$batchItems = New-Object System.Collections.ArrayList
$currentCategory = ""
$batchNo = 1
foreach ($candidate in $candidates) {
    if ($batchItems.Count -eq 0) { $currentCategory = [string]$candidate.category }
    $categoryChanged = ([string]$candidate.category -ne $currentCategory)
    $currentBatchLimit = if ($currentCategory -eq "special") { 1 } else { $MaxPerBatch }
    if ($batchItems.Count -ge $currentBatchLimit -or ($categoryChanged -and $batchItems.Count -gt 0)) {
        [void]$batches.Add([ordered]@{ batchNumber = $batchNo; category = $currentCategory; records = @($batchItems.ToArray()) })
        $batchNo++
        if ($batches.Count -ge $MaxBatches) { break }
        $batchItems = New-Object System.Collections.ArrayList
        $currentCategory = [string]$candidate.category
    }
    [void]$batchItems.Add($candidate)
}
if ($batches.Count -lt $MaxBatches -and $batchItems.Count -gt 0) {
    [void]$batches.Add([ordered]@{ batchNumber = $batchNo; category = $currentCategory; records = @($batchItems.ToArray()) })
}

$batchSummaries = New-Object System.Collections.ArrayList
foreach ($batch in @($batches.ToArray())) {
    $batchNumber = [int]$batch.batchNumber
    $category = [string]$batch.category
    $safeCategory = $category -replace "[^A-Za-z0-9._-]", "-"
    $prefix = "artisans-identity-batch-{0:D2}-{1}" -f $batchNumber, $safeCategory
    $editsFileName = "$prefix-edits.json"
    $applyFileName = "Apply Artisans Identity Batch {0:D2} {1} Live.bat" -f $batchNumber, $safeCategory
    $revertFileName = "Revert Artisans Identity Batch {0:D2} {1} Live.bat" -f $batchNumber, $safeCategory
    $editsPath = Resolve-WorkspacePath ".\$editsFileName"
    $applyPath = Resolve-WorkspacePath ".\$applyFileName"
    $revertPath = Resolve-WorkspacePath ".\$revertFileName"

    $edits = New-Object System.Collections.ArrayList
    $placements = New-Object System.Collections.ArrayList
    $indexes = New-Object System.Collections.ArrayList
    $records = @($batch.records)
    for ($i = 0; $i -lt $records.Count; $i++) {
        $slot = $i + 1
        if ($category -eq "special") {
            $x = $StartX
            $y = $StartY
            $z = $SpecialZ
        }
        else {
            $centerOffset = $i - (($records.Count - 1) / 2.0)
            $x = $StartX + ($centerOffset * $SpacingX)
            $y = $StartY + ([Math]::Floor(($batchNumber - 1) % 2) * $SpacingY)
            $z = $StartZ
        }
        $moby = $records[$i].moby
        $trueIndex = [int]$records[$i].trueIndex
        [void]$indexes.Add($trueIndex)
        [void]$edits.Add((New-TestEdit $moby $batchNumber $slot $x $y $z $category))
        [void]$placements.Add([ordered]@{
            slot = $slot
            id = "T$trueIndex"
            typeHex = [string](Get-Field $moby "typeHex" "")
            label = [string](Get-Field $moby "displayTargetLabel" "")
            kind = [string](Get-Field $moby "candidateKind" "")
            confidence = [string](Get-Field $moby "confidence" "")
            x = $x
            y = $y
            z = $z
        })
    }

    $editRoot = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        editor = "NativeSpyroEditor"
        levelName = "Artisans"
        note = "Temporary live RAM identity-test edits. Use the matching revert batch after observing."
        batchNumber = $batchNumber
        category = $category
        editCount = $edits.Count
        edits = @($edits.ToArray())
    }
    $editRoot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $editsPath -Encoding UTF8
    Write-BatchFile $applyPath ".\$editsFileName" @($indexes.ToArray())
    Write-BatchFile $revertPath ".\$editsFileName" @($indexes.ToArray()) -Revert

    [void]$batchSummaries.Add([ordered]@{
        batchNumber = $batchNumber
        category = $category
        goal = Get-Goal $category
        editsPath = $editsPath
        applyBatch = $applyPath
        revertBatch = $revertPath
        placements = @($placements.ToArray())
    })
}

$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-ArtisansIdentityTestBatches.ps1"
    levelName = "Artisans"
    sourceCatalog = (Resolve-Path -LiteralPath $resolvedCatalog).Path
    expectedLevelId = "0x0A"
    testPad = [ordered]@{ startX = $StartX; startY = $StartY; startZ = $StartZ; specialZ = $SpecialZ; spacingX = $SpacingX; spacingY = $SpacingY }
    note = "Apply batches only while DuckStation is in Artisans. Scripts guard on live level ID 0x0A and write RAM only."
    batchCount = $batchSummaries.Count
    batches = @($batchSummaries.ToArray())
}

$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMd = Resolve-WorkspacePath $OutMarkdownPath
$root | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8
Write-RevertAllBatchFile (Resolve-WorkspacePath ".\Revert All Artisans Identity Live.bat")

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Artisans Identity Test Batches")
[void]$lines.Add("")
[void]$lines.Add("These are live-RAM-only batches. Stand in Artisans in DuckStation, run an Apply batch, observe the moved objects in slot order, then run its Revert batch.")
[void]$lines.Add("")
[void]$lines.Add("- Expected live level ID: 0x0A")
[void]$lines.Add("- Batches generated: $($batchSummaries.Count)")
[void]$lines.Add("- Test pad center: X $StartX, Y $StartY")
[void]$lines.Add("- Normal slots are centered left/middle/right around the test pad center.")
[void]$lines.Add("- Normal test Z: $StartZ")
[void]$lines.Add("- Special-object test Z: $SpecialZ")
[void]$lines.Add("- Special-object batches are isolated one object at a time at the open courtyard test point so large actors do not hide each other.")
[void]$lines.Add("")
foreach ($summary in @($batchSummaries.ToArray())) {
    [void]$lines.Add("## Batch $($summary.batchNumber): $($summary.category)")
    [void]$lines.Add("")
    [void]$lines.Add($summary.goal)
    [void]$lines.Add("")
    [void]$lines.Add("- Apply: $([IO.Path]::GetFileName([string]$summary.applyBatch))")
    [void]$lines.Add("- Revert: $([IO.Path]::GetFileName([string]$summary.revertBatch))")
    [void]$lines.Add("")
    [void]$lines.Add("| Slot | ID | Type | Current label | Kind | Confidence | Test XYZ |")
    [void]$lines.Add("| ---: | --- | --- | --- | --- | --- | --- |")
    foreach ($placement in @($summary.placements)) {
        [void]$lines.Add("| $($placement.slot) | $($placement.id) | $($placement.typeHex) | $($placement.label) | $($placement.kind) | $($placement.confidence) | $($placement.x), $($placement.y), $($placement.z) |")
    }
    [void]$lines.Add("")
}
$lines | Set-Content -LiteralPath $resolvedOutMd -Encoding UTF8

Write-Host "Wrote Artisans identity batch plan to $resolvedOutJson"
Write-Host "Wrote Artisans identity batch report to $resolvedOutMd"
Write-Host "Generated $($batchSummaries.Count) apply/revert batch pair(s)."
