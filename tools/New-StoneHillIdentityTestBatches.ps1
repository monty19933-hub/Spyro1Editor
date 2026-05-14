param(
    [string]$AuditPath = ".\stonehill-moby-identity-audit.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutJsonPath = ".\stonehill-identity-test-batches.json",
    [string]$OutMarkdownPath = ".\stonehill-identity-test-batches.md",
    [double]$StartX = 5400,
    [double]$StartY = 9286,
    [double]$StartZ = 1280.125,
    [double]$SpacingX = 420,
    [double]$SpacingY = 420,
    [int]$MaxPerBatch = 3,
    [int]$BatchNumber = 1,
    [int[]]$RecordIndexes = @(),
    [string]$CategoryOverride = "",
    [string]$OutputTag = "",
    [switch]$OneRow,
    [switch]$BuildBin,
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

function Convert-DoubleToRaw([double]$Value) {
    return [int][Math]::Round($Value * 16.0)
}

function New-Vector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = [Math]::Round($X, 4); y = [Math]::Round($Y, 4); z = [Math]::Round($Z, 4) }
}

function New-RawVector([double]$X, [double]$Y, [double]$Z) {
    return [ordered]@{ x = Convert-DoubleToRaw $X; y = Convert-DoubleToRaw $Y; z = Convert-DoubleToRaw $Z }
}

function Get-BatchCategory($Record) {
    $type = [string](Get-Field $Record "typeHex" "")
    $labelText = (([string](Get-Field $Record "label" "")) + " " + ([string](Get-Field $Record "kind" ""))).ToLowerInvariant()
    $evidenceText = ([string](Get-Field $Record "evidence" "")).ToLowerInvariant()
    if ($labelText -match "enemy|ram|shepherd") { return "enemies" }
    if ($labelText -match "cluster|dragon|pedestal|fairy|whirlwind") { return "clusters" }
    if ($type -eq "0x00" -or $labelText -match "control|placeholder") { return "controls" }
    if ($labelText -match "simple-collectible-or-scenery") { return "simple" }
    if ($labelText -match "linked|actor|collectible") { return "linked" }
    if ($labelText -match "container|reward|chest") { return "containers" }
    if ($type -eq "0x30" -or $labelText -match "scenery|tree|lamp|large") { return "scenery" }
    if ($type -eq "0x18" -or $labelText -match "active|small-pickup") { return "active" }
    if ($evidenceText -match "linked special data") { return "linked" }
    if ($evidenceText -match "tested chest behavior") { return "containers" }
    return "other"
}

function Get-CategoryPriority([string]$Category) {
    switch ($Category) {
        "enemies" { return 10 }
        "containers" { return 20 }
        "linked" { return 30 }
        "simple" { return 40 }
        "scenery" { return 50 }
        "active" { return 60 }
        "clusters" { return 70 }
        "other" { return 80 }
        "controls" { return 90 }
        default { return 99 }
    }
}

function Get-BatchGoal([string]$Category) {
    switch ($Category) {
        "enemies" { return "Identify enemy actor records. Watch which slots become rams, shepherds, or invisible/inactive actors." }
        "containers" { return "Identify chest/container visuals and rewards. Break/open each visible object after noting its slot." }
        "linked" { return "Identify linked visible actors/collectibles. Watch for model movement, pickup behavior, or actor behavior." }
        "simple" { return "Identify simple 0x20 objects. Watch for visible props, pickups, or one-off interactables." }
        "scenery" { return "Identify large scenery/NPC-like records. Look for trees, lamps, arches, or dragon-like props." }
        "active" { return "Identify remaining active/small-pickup outliers." }
        "clusters" { return "Identify linked clusters carefully. Move one cluster candidate at a time and watch for split models or helper behavior." }
        "controls" { return "Control/helper records are often invisible; only test with nearby visible clusters." }
        default { return "Identify unresolved records by moving them to the clear test pad." }
    }
}

function New-TestEdit($Record, [int]$Batch, [int]$Slot, [double]$X, [double]$Y, [double]$Z, [string]$Category) {
    $trueIndex = [int](Get-Field $Record "trueIndex" -1)
    $originalX = [double](Get-Field $Record "x" 0)
    $originalY = [double](Get-Field $Record "y" 0)
    $originalZ = [double](Get-Field $Record "z" 0)
    $label = "Identity batch $Batch slot ${Slot}: T$trueIndex $Category"
    return [ordered]@{
        index = $trueIndex
        trueIndex = $trueIndex
        label = $label
        typeHex = [string](Get-Field $Record "typeHex" "")
        stateHex = [string](Get-Field $Record "stateHex" "")
        runtimeAddress = ""
        specialDataPointer = [string](Get-Field $Record "specialDataPointer" "")
        flag4AHex = [string](Get-Field $Record "flag52Hex" "")
        flag4BHex = [string](Get-Field $Record "flag53Hex" "")
        patchStatus = "loader-table-patchable"
        patchLead = "WAD entry 12, true record $trueIndex, XYZ +0x0C/+0x10/+0x14"
        behaviorNote = "Temporary identity-test placement. Do not keep this in production edits."
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
            batchNumber = $Batch
            slot = $Slot
            category = $Category
            originalLabel = [string](Get-Field $Record "label" "")
            confidence = [string](Get-Field $Record "confidence" "")
        }
    }
}

$resolvedAuditPath = Resolve-WorkspacePath $AuditPath
$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedOutJsonPath = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $resolvedAuditPath)) { throw "Missing audit: $resolvedAuditPath" }
if ($BuildBin -and -not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing image: $resolvedImagePath" }

$audit = Get-Content -Raw -LiteralPath $resolvedAuditPath | ConvertFrom-Json
$requestedIndexes = @($RecordIndexes | ForEach-Object { [int]$_ })
$requestedIndexSet = @{}
foreach ($index in $requestedIndexes) { $requestedIndexSet[$index] = $true }

if ($requestedIndexes.Count -gt 0) {
    $allRecordsByIndex = @{}
    foreach ($record in @(Get-ArrayField $audit "records")) {
        $allRecordsByIndex[[int](Get-Field $record "trueIndex" -1)] = $record
    }
    $candidates = @(
        foreach ($index in $requestedIndexes) {
            if (-not $allRecordsByIndex.ContainsKey($index)) { throw "Audit does not contain T$index." }
            $record = $allRecordsByIndex[$index]
            $category = if ([string]::IsNullOrWhiteSpace($CategoryOverride)) { Get-BatchCategory $record } else { $CategoryOverride }
            [pscustomobject]@{
                record = $record
                trueIndex = $index
                category = $category
                priority = Get-CategoryPriority $category
            }
        }
    )
} else {
    $candidates = @(
        Get-ArrayField $audit "questionableRecords" |
            ForEach-Object {
                $category = if ([string]::IsNullOrWhiteSpace($CategoryOverride)) { Get-BatchCategory $_ } else { $CategoryOverride }
                [pscustomobject]@{
                    record = $_
                    trueIndex = [int](Get-Field $_ "trueIndex" -1)
                    category = $category
                    priority = Get-CategoryPriority $category
                }
            } |
            Sort-Object priority, trueIndex
    )
}

$batches = New-Object System.Collections.ArrayList
$batchNo = 1
$slotRecords = New-Object System.Collections.ArrayList
$currentCategory = ""
foreach ($candidate in $candidates) {
    if ($slotRecords.Count -eq 0) {
        $currentCategory = $candidate.category
    }
    $categoryChanged = $candidate.category -ne $currentCategory
    if ($requestedIndexes.Count -eq 0 -and ($slotRecords.Count -ge $MaxPerBatch -or ($categoryChanged -and $slotRecords.Count -gt 0))) {
        [void]$batches.Add([ordered]@{
            batchNumber = $batchNo
            category = $currentCategory
            goal = Get-BatchGoal $currentCategory
            records = @($slotRecords.ToArray())
        })
        $batchNo++
        $slotRecords = New-Object System.Collections.ArrayList
        $currentCategory = $candidate.category
    }
    [void]$slotRecords.Add($candidate.record)
}
if ($slotRecords.Count -gt 0) {
    [void]$batches.Add([ordered]@{
        batchNumber = $batchNo
        category = $currentCategory
        goal = Get-BatchGoal $currentCategory
        records = @($slotRecords.ToArray())
    })
}

$batchSummaries = New-Object System.Collections.ArrayList
foreach ($batch in $batches) {
    $batchNumberValue = [int]$batch.batchNumber
    $category = [string]$batch.category
    $safeCategory = $category.Replace(" ", "-")
    $safeOutputTag = $OutputTag -replace "[^A-Za-z0-9._-]", "-"
    $prefix = if (-not [string]::IsNullOrWhiteSpace($safeOutputTag) -and $batches.Count -eq 1) {
        "stonehill-{0}" -f $safeOutputTag
    } else {
        "stonehill-identity-batch-{0:D2}-{1}" -f $batchNumberValue, $safeCategory
    }
    $editsPath = Resolve-WorkspacePath ".\$prefix-edits.json"
    $binFileName = if (-not [string]::IsNullOrWhiteSpace($safeOutputTag) -and $batches.Count -eq 1) {
        "Spyro the Dragon (USA)-{0}.bin" -f $safeOutputTag
    } else {
        "Spyro the Dragon (USA)-identity-batch-{0:D2}-{1}.bin" -f $batchNumberValue, $safeCategory
    }
    $binPath = Resolve-WorkspacePath ".\$binFileName"
    $planPath = Resolve-WorkspacePath ".\$prefix-patchplan.json"

    $edits = New-Object System.Collections.ArrayList
    $placements = New-Object System.Collections.ArrayList
    $records = @($batch.records)
    for ($i = 0; $i -lt $records.Count; $i++) {
        $slot = $i + 1
        $row = if ($OneRow) { 0 } else { [int][Math]::Floor($i / 4) }
        $col = if ($OneRow) { $i } else { $i % 4 }
        $x = $StartX + ($col * $SpacingX)
        $y = $StartY + ($row * $SpacingY)
        $z = $StartZ
        $record = $records[$i]
        $edit = New-TestEdit $record $batchNumberValue $slot $x $y $z $category
        [void]$edits.Add($edit)
        [void]$placements.Add([ordered]@{
            slot = $slot
            id = "T$([int](Get-Field $record "trueIndex" -1))"
            typeHex = [string](Get-Field $record "typeHex" "")
            originalLabel = [string](Get-Field $record "label" "")
            confidence = [string](Get-Field $record "confidence" "")
            testX = $x
            testY = $y
            testZ = $z
            note = [string](Get-Field $record "testSuggestion" "")
        })
    }

    $editsRoot = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        editor = "New-StoneHillIdentityTestBatches"
        levelName = "Stone Hill"
        note = "Temporary identity-test batch. Use only for disposable Loader BIN tests."
        batchNumber = $batchNumberValue
        category = $category
        editCount = $edits.Count
        edits = @($edits.ToArray())
    }
    $editsRoot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $editsPath -Encoding UTF8

    $summary = [ordered]@{
        batchNumber = $batchNumberValue
        category = $category
        goal = [string]$batch.goal
        editsPath = $editsPath
        binPath = $binPath
        cuePath = [System.IO.Path]::ChangeExtension($binPath, ".cue")
        patchPlanPath = $planPath
        placements = @($placements.ToArray())
    }
    [void]$batchSummaries.Add($summary)
}

$root = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    auditPath = (Resolve-Path -LiteralPath $resolvedAuditPath).Path
    clearTestPad = [ordered]@{ startX = $StartX; startY = $StartY; startZ = $StartZ; spacingX = $SpacingX; spacingY = $SpacingY }
    batchCount = $batchSummaries.Count
    batches = @($batchSummaries.ToArray())
}
$root | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJsonPath -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Identity Test Batches")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($root.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("These batches move unresolved/questionable records into a clear test pad near X=$StartX, Y=$StartY. Test one batch at a time, observe each slot left-to-right, then promote confirmed identities into stonehill-live-validation-overrides.json.")
[void]$lines.Add("")
[void]$lines.Add("Build a batch with:")
[void]$lines.Add("")
[void]$lines.Add('```powershell')
[void]$lines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\New-StoneHillIdentityTestBatches.ps1 -BuildBin -BatchNumber 1")
[void]$lines.Add('```')
[void]$lines.Add("")
foreach ($batch in $batchSummaries) {
    $batchTitle = "## Batch {0}: {1}" -f $batch.batchNumber, $batch.category
    [void]$lines.Add($batchTitle)
    [void]$lines.Add("")
    [void]$lines.Add($batch.goal)
    [void]$lines.Add("")
    $editsFileName = [System.IO.Path]::GetFileName([string]$batch.editsPath)
    $cueFileName = [System.IO.Path]::GetFileName([string]$batch.cuePath)
    [void]$lines.Add("- Edits: $editsFileName")
    [void]$lines.Add("- CUE when built: $cueFileName")
    [void]$lines.Add("")
    [void]$lines.Add("| Slot | ID | Type | Current label | What to check |")
    [void]$lines.Add("|---:|---|---|---|---|")
    foreach ($placement in @($batch.placements)) {
        $label = ([string]$placement.originalLabel).Replace("|", "\|")
        $note = ([string]$placement.note).Replace("|", "\|")
        [void]$lines.Add("| $($placement.slot) | $($placement.id) | $($placement.typeHex) | $label | $note |")
    }
    [void]$lines.Add("")
}
$lines | Set-Content -LiteralPath $resolvedOutMarkdownPath -Encoding UTF8

if ($BuildBin) {
    $selected = @($batchSummaries | Where-Object { [int]$_["batchNumber"] -eq $BatchNumber } | Select-Object -First 1)
    if ($selected.Count -eq 0) { throw "No batch $BatchNumber exists." }
    $exporter = Join-Path $PSScriptRoot "Export-StoneHillLoaderTablePatchTest.ps1"
    $args = @(
        "-NativeEditsPath", $selected[0].editsPath,
        "-ImagePath", $resolvedImagePath,
        "-OutPath", $selected[0].binPath,
        "-PlanPath", $selected[0].patchPlanPath
    )
    if ($PlanOnly) { $args += "-PlanOnly" }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $exporter @args
}

Write-Host "Wrote $resolvedOutJsonPath"
Write-Host "Wrote $resolvedOutMarkdownPath"
Write-Host "Batches: $($batchSummaries.Count)"
