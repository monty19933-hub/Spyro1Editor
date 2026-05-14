param(
    [string]$PatchPlanPath = ".\Spyro the Dragon (USA)-nativepatchtest-topranked.bin.patchplan.json",
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$RamPath = "",
    [string]$BeforeRamPath = "",
    [string]$OutPath = ".\stonehill-source-patch-live-validation.json",
    [string]$MarkdownPath = "",
    [switch]$CaptureLive,
    [int]$ToleranceRaw = 32
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$script:MobyRecordStride = 0x50
$script:MobyCoordOffsets = @{ x = 4; y = 8; z = 12 }
$script:MobyTypeOffset = 0x48
$script:MobyStateOffset = 0x49
$script:ValidationLayout = "legacy-0x50"

function Resolve-WorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing file: $Path" }
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

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    $ramBase = [Convert]::ToUInt64("80000000", 16)
    $ramEnd = [Convert]::ToUInt64("80200000", 16)
    return ($address -ge $ramBase -and $address -lt $ramEnd -and (($Value -band [uint32]3) -eq 0))
}

function Convert-PsxPointerToOffset([uint32]$Value) {
    if (-not (Test-PsxPointer $Value)) { return -1 }
    return [int]([uint64]$Value - [Convert]::ToUInt64("80000000", 16))
}

function Test-PlausibleMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + $script:MobyRecordStride) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + $script:MobyTypeOffset]
    $state = [int]$Ram[$Offset + $script:MobyStateOffset]
    if ($type -le 0 -or $type -gt 0x7F -or $state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + $script:MobyCoordOffsets.x)
    $y = Get-Int32LE $Ram ($Offset + $script:MobyCoordOffsets.y)
    $z = Get-Int32LE $Ram ($Offset + $script:MobyCoordOffsets.z)
    if ([Math]::Abs($x) -gt 4000000 -or [Math]::Abs($y) -gt 4000000 -or [Math]::Abs($z) -gt 4000000) { return $false }
    if ([Math]::Abs($x) -lt 16 -and [Math]::Abs($y) -lt 16 -and [Math]::Abs($z) -lt 16) { return $false }
    return $true
}

function Count-PlausibleMobys([byte[]]$Ram, [uint32]$Pointer) {
    $start = Convert-PsxPointerToOffset $Pointer
    if ($start -lt 0) { return 0 }
    $count = 0
    $badRun = 0
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * $script:MobyRecordStride)
        if (($offset + $script:MobyRecordStride) -gt $Ram.Length) { break }
        if (Test-PlausibleMoby $Ram $offset) {
            $count++
            $badRun = 0
        }
        else {
            $badRun++
            if ($count -gt 8 -and $badRun -ge 48) { break }
        }
    }
    return $count
}

function Read-MobyRecord([byte[]]$Ram, [int]$Index) {
    $pointer = Get-UInt32LE $Ram 0x75828
    $start = Convert-PsxPointerToOffset $pointer
    if ($start -lt 0) {
        return [ordered]@{
            index = $Index
            found = $false
            reason = "_ptr_levelMobys is not a valid PS1 RAM pointer"
        }
    }

    $offset = $start + ($Index * $script:MobyRecordStride)
    if (($offset + $script:MobyRecordStride) -gt $Ram.Length) {
        return [ordered]@{
            index = $Index
            found = $false
            reason = "index is outside the live moby table"
        }
    }

    $plausible = Test-PlausibleMoby $Ram $offset
    return [ordered]@{
        index = $Index
        found = $plausible
        reason = $(if ($plausible) { "" } else { "record is not currently a plausible moby" })
        ramOffset = ("0x{0:X}" -f $offset)
        runtimeAddress = ("0x{0:X8}" -f ([uint64][Convert]::ToUInt64("80000000", 16) + [uint64]$offset))
        typeHex = ("0x{0:X2}" -f [int]$Ram[$offset + $script:MobyTypeOffset])
        stateHex = ("0x{0:X2}" -f [int]$Ram[$offset + $script:MobyStateOffset])
        rawX = Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.x)
        rawY = Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.y)
        rawZ = Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.z)
        x = [Math]::Round((Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.x)) / 16.0, 4)
        y = [Math]::Round((Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.y)) / 16.0, 4)
        z = [Math]::Round((Get-Int32LE $Ram ($offset + $script:MobyCoordOffsets.z)) / 16.0, 4)
    }
}

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $numerator = ($LegacyIndex * 0x50) - 8
    if ($numerator -ge 0 -and ($numerator % 0x58) -eq 0) { return [int]($numerator / 0x58) }
    return $null
}

function Get-EditRuntimeIndex($Edit) {
    $index = [int](Get-Field $Edit "index" -1)
    $trueIndex = Get-Field $Edit "trueIndex" $null
    if ($null -ne $trueIndex) { return [int]$trueIndex }
    if ($script:ValidationLayout -eq "loader-table-0x58") {
        $legacyIndex = Get-Field $Edit "legacyIndex" $null
        if ($null -eq $legacyIndex -and $index -ge 0) { $legacyIndex = $index }
        if ($null -ne $legacyIndex) {
            $mapped = Convert-LegacyIndexToTrue ([int]$legacyIndex)
            if ($null -ne $mapped) { return [int]$mapped }
        }
    }
    return $index
}

function Get-VectorAxis($Vector, [string]$Axis, [double]$Default = 0) {
    return [double](Get-Field $Vector $Axis $Default)
}

function Get-RawAxis($Edit, [string]$VectorName, [string]$Axis) {
    $rawVectorName = if ($VectorName -eq "edited") { "rawEdited" } else { "rawOriginal" }
    $rawVector = Get-Field $Edit $rawVectorName $null
    if ($null -ne $rawVector) {
        $rawValue = Get-Field $rawVector $Axis $null
        if ($null -ne $rawValue) { return [int]$rawValue }
    }

    $vector = Get-Field $Edit $VectorName $null
    return [int][Math]::Round((Get-VectorAxis $vector $Axis 0) * 16.0)
}

function Read-ProgressSnapshot([byte[]]$Ram) {
    return [ordered]@{
        globalGemCount = [int](Get-UInt16LE $Ram 0x75860)
        globalDragonCount = Get-Int32LE $Ram 0x75750
        globalEggCount = Get-Int32LE $Ram 0x75810
        levelId = Get-UInt32LE $Ram 0x758B4
        collectablesStateFlagsAddress = "0x80077900"
        collectablesStateFlagsLength = 1231
    }
}

function Compare-CollectableStateFlags([byte[]]$BeforeRam, [byte[]]$AfterRam) {
    $changes = New-Object System.Collections.ArrayList
    $baseOffset = 0x77900
    $length = 1231
    for ($i = 0; $i -lt $length; $i++) {
        $offset = $baseOffset + $i
        if ($offset -ge $BeforeRam.Length -or $offset -ge $AfterRam.Length) { break }
        $before = [byte]$BeforeRam[$offset]
        $after = [byte]$AfterRam[$offset]
        if ($before -eq $after) { continue }
        [void]$changes.Add([ordered]@{
            index = $i
            ramOffset = ("0x{0:X}" -f $offset)
            runtimeAddress = ("0x{0:X8}" -f ([uint64][Convert]::ToUInt64("80000000", 16) + [uint64]$offset))
            beforeHex = ("0x{0:X2}" -f $before)
            afterHex = ("0x{0:X2}" -f $after)
        })
    }
    return @($changes.ToArray())
}

function Compare-ProgressSnapshots([byte[]]$BeforeRam, [byte[]]$AfterRam) {
    $before = Read-ProgressSnapshot $BeforeRam
    $after = Read-ProgressSnapshot $AfterRam
    $counterChanges = New-Object System.Collections.ArrayList
    foreach ($name in @("globalGemCount", "globalDragonCount", "globalEggCount", "levelId")) {
        if ($before[$name] -ne $after[$name]) {
            [void]$counterChanges.Add([ordered]@{
                name = $name
                before = $before[$name]
                after = $after[$name]
                delta = ([int64]$after[$name] - [int64]$before[$name])
            })
        }
    }
    $flagChanges = @(Compare-CollectableStateFlags $BeforeRam $AfterRam)
    return [ordered]@{
        before = $before
        after = $after
        counterChanges = @($counterChanges.ToArray())
        collectableFlagChangeCount = $flagChanges.Count
        collectableFlagChanges = $flagChanges
    }
}

function New-AxisResult($Record, $Edit, [string]$Axis, [bool]$PatchedAxis) {
    $rawField = "raw" + $Axis.ToUpperInvariant()
    $actual = [int](Get-Field $Record $rawField 0)
    $original = Get-RawAxis $Edit "original" $Axis
    $edited = Get-RawAxis $Edit "edited" $Axis
    $deltaEdited = $actual - $edited
    $deltaOriginal = $actual - $original
    $match = if ([Math]::Abs($deltaEdited) -le $ToleranceRaw) {
        "edited"
    }
    elseif ([Math]::Abs($deltaOriginal) -le $ToleranceRaw) {
        "original"
    }
    else {
        "other"
    }
    return [ordered]@{
        axis = $Axis
        patchedAxis = $PatchedAxis
        actualRaw = $actual
        originalRaw = $original
        editedRaw = $edited
        deltaFromEditedRaw = $deltaEdited
        deltaFromOriginalRaw = $deltaOriginal
        match = $match
    }
}

function Get-ValidationStatus($AxisResults, [bool]$HasSourcePatch, [bool]$RecordFound) {
    $patchedAxes = @($AxisResults | Where-Object { [bool](Get-Field $_ "patchedAxis" $false) })
    if ($HasSourcePatch -and $patchedAxes.Count -gt 0) {
        $editedCount = @($patchedAxes | Where-Object { [string](Get-Field $_ "match" "") -eq "edited" }).Count
        $originalCount = @($patchedAxes | Where-Object { [string](Get-Field $_ "match" "") -eq "original" }).Count
        if (-not $RecordFound) {
            if ($editedCount -eq $patchedAxes.Count) { return "source-coordinates-loaded-nonvisual-record" }
            if ($originalCount -eq $patchedAxes.Count) { return "still-original-or-not-fresh-loaded" }
            if ($editedCount -gt 0) { return "partial-source-coordinates-loaded-nonvisual-record" }
            return "not-decoded"
        }
        if ($editedCount -eq $patchedAxes.Count) { return "source-placement-loaded" }
        if ($originalCount -eq $patchedAxes.Count) { return "still-original-or-not-fresh-loaded" }
        if ($editedCount -gt 0) { return "partial-source-placement" }
        return "patched-axes-unexpected-position"
    }

    if (-not $RecordFound) { return "not-decoded" }

    $editedAny = @($AxisResults | Where-Object { [string](Get-Field $_ "match" "") -eq "edited" }).Count
    $originalAll = @($AxisResults | Where-Object { [string](Get-Field $_ "match" "") -eq "original" }).Count -eq @($AxisResults).Count
    if ($editedAny -gt 0) { return "runtime-visual-only-no-source-lead" }
    if ($originalAll) { return "unmoved-no-source-lead" }
    return "changed-without-source-lead"
}

function Get-NextAction([string]$Status, [string]$Category) {
    if ($Status -eq "source-placement-loaded") {
        if ($Category -eq "container-linked-behavior-risk") {
            return "Break/hit the chest and capture another RAM pair to verify reward and collectable-state changes."
        }
        if ($Category -eq "collectible-source-candidate") {
            return "Pick it up and compare counters or collectable-state flags against this validation RAM."
        }
        if ($Category -eq "actor-home-anchor-risk") {
            return "Test AI/home behavior and capture before/after RAM if movement does not follow."
        }
        return "Treat placement as a strong source lead; still verify any behavior this object owns."
    }
    if ($Status -eq "source-coordinates-loaded-nonvisual-record") {
        return "Coordinates loaded, but this record is probably a marker/control record; locate the visible paired moby."
    }
    if ($Status -eq "still-original-or-not-fresh-loaded") {
        return "Fresh-load the patched CUE, then rerun validation before judging collision or rewards."
    }
    if ($Status -eq "partial-source-coordinates-loaded-nonvisual-record") {
        return "Some coordinates loaded into a nonvisual record; inspect neighboring/paired records before using it as an object."
    }
    if ($Status -eq "partial-source-placement" -or $Status -eq "patched-axes-unexpected-position") {
        return "Keep this as a source lead, then test a broad BIN or search neighboring source bytes."
    }
    if ($Status -eq "runtime-visual-only-no-source-lead") {
        return "Useful identity evidence only; find a source-window lead before expecting permanent behavior."
    }
    if ($Status -eq "not-decoded") {
        return "Load Stone Hill and rerun against the level moby table."
    }
    return "Needs focused before/after RAM samples or a better source-window lead."
}

function Get-FallbackCategory($Edit, [string]$Label) {
    $text = (($Label, [string](Get-Field $Edit "behaviorNote" ""), [string](Get-Field $Edit "specialDataNote" ""), [string](Get-Field $Edit "patchLead" ""), [string](Get-Field $Edit "typeHex" "")) -join " ").ToLowerInvariant()
    if ($text -match "chest|container|contents|interact|collision") { return "container-linked-behavior-risk" }
    if ($text -match "sheep|fodder|ai/home|home anchor") { return "actor-home-anchor-risk" }
    if ($text -match "gem|treasure|collectible") { return "collectible-source-candidate" }
    if ($text -match "tree|scenery|lamp|flag") { return "scenery-source-candidate" }
    if ($text -match "helper|invisible|inactive|no-collision") { return "helper-or-nonplacement" }
    return "unknown-moby-source-candidate"
}

function Get-InteractionProofStatus([string]$PlacementStatus, [string]$Category, $ProgressDiff) {
    if ($Category -ne "container-linked-behavior-risk" -and
        $Category -ne "collectible-source-candidate" -and
        $Category -ne "actor-home-anchor-risk") {
        return "not-required-for-category"
    }

    if ($PlacementStatus -ne "source-placement-loaded") {
        return "placement-not-proven"
    }

    if ($Category -eq "actor-home-anchor-risk") {
        return "needs-ai-home-behavior-pair"
    }

    if ($null -eq $ProgressDiff) {
        return "needs-before-after-interaction-pair"
    }

    $counterChanges = @(Get-ArrayField $ProgressDiff "counterChanges")
    $flagChanges = [int](Get-Field $ProgressDiff "collectableFlagChangeCount" 0)
    if ($counterChanges.Count -gt 0 -or $flagChanges -gt 0) {
        return "counter-or-flag-change-detected"
    }

    return "no-counter-or-flag-change"
}

function Get-InteractionProofNext([string]$ProofStatus, [string]$Category) {
    switch ($ProofStatus) {
        "counter-or-flag-change-detected" { return "Behavior proof captured; review changed counters/flags for the reward value." }
        "no-counter-or-flag-change" { return "Placement may be loaded, but interaction/reward proof is still missing." }
        "needs-before-after-interaction-pair" { return "Capture this validation RAM, interact with the object, then rerun with -BeforeRamPath." }
        "needs-ai-home-behavior-pair" { return "Capture before/after RAM while testing whether AI or home behavior follows the moved source position." }
        "placement-not-proven" { return "Prove source placement before judging collision, reward, pickup, or AI behavior." }
        default { return "No separate interaction proof is required for this category." }
    }
}

function New-FallbackFunctionalTest($Edit, [bool]$HasSourcePatch) {
    $index = Get-EditRuntimeIndex $Edit
    $label = [string](Get-Field $Edit "label" "Moby")
    $category = Get-FallbackCategory $Edit $label
    return [ordered]@{
        runtimeMobyIndex = $index
        label = $label
        mobyType = [string](Get-Field $Edit "typeHex" "")
        category = $category
        sourceWindowPatched = $HasSourcePatch
        expectation = $(if ($HasSourcePatch) { "Fresh-load the generated source BIN and verify placement in RAM, then test behavior." } else { "No source-window patch was emitted; this remains runtime visual evidence only." })
        successSignal = $(if ($category -eq "container-linked-behavior-risk") { "Chest loads at edited position, collides, breaks, and grants or drops reward." } elseif ($category -eq "collectible-source-candidate") { "Collectible loads at edited position and pickup changes counter or collectable flag." } else { "Target loads at edited position and behaves normally from a fresh patched-disc boot." })
    }
}

function Format-VectorText($Record) {
    $x = Get-Field $Record "x" $null
    $y = Get-Field $Record "y" $null
    $z = Get-Field $Record "z" $null
    if ($null -eq $x -or $null -eq $y -or $null -eq $z) { return "-" }
    return "{0}, {1}, {2}" -f $x, $y, $z
}

function Escape-Markdown([string]$Text) {
    if ($null -eq $Text) { return "" }
    return $Text.Replace("|", "\|")
}

$patchPlanPath = Resolve-WorkspacePath $PatchPlanPath
$nativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
$outPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $MarkdownPath = [System.IO.Path]::ChangeExtension($outPath, ".md")
}
$markdownPath = Resolve-WorkspacePath $MarkdownPath

if ($CaptureLive) {
    if ([string]::IsNullOrWhiteSpace($RamPath)) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $RamPath = ".\stonehill-source-patch-live-validation-$stamp.ram.bin"
    }
    $resolvedRamPath = Resolve-WorkspacePath $RamPath
    $dumpScript = Join-Path $PSScriptRoot "Dump-DuckStationRam.ps1"
    & $dumpScript -OutPath $resolvedRamPath
    $RamPath = $resolvedRamPath
}
else {
    $RamPath = Resolve-WorkspacePath $RamPath
}

if ([string]::IsNullOrWhiteSpace($RamPath) -or -not (Test-Path -LiteralPath $RamPath)) {
    throw "Pass -RamPath or use -CaptureLive to validate against DuckStation."
}

$beforeRamPath = Resolve-WorkspacePath $BeforeRamPath
$patchPlan = Read-JsonFile $patchPlanPath
$nativeEdits = Read-JsonFile $nativeEditsPath
$planSource = Get-Field $patchPlan "source" $null
$planStride = [string](Get-Field $planSource "recordStride" "")
$firstPatchSource = ""
$firstPatch = @(Get-ArrayField $patchPlan "binaryPatches" | Select-Object -First 1)
if ($firstPatch.Count -gt 0) { $firstPatchSource = [string](Get-Field $firstPatch[0] "source" "") }
if ($planStride -eq "0x58" -or $firstPatchSource -eq "loader-table-entry12") {
    $script:MobyRecordStride = 0x58
    $script:MobyCoordOffsets = @{ x = 0x0C; y = 0x10; z = 0x14 }
    $script:MobyTypeOffset = 0x50
    $script:MobyStateOffset = 0x51
    $script:ValidationLayout = "loader-table-0x58"
}
$ram = [System.IO.File]::ReadAllBytes($RamPath)
$beforeRam = if (-not [string]::IsNullOrWhiteSpace($beforeRamPath) -and (Test-Path -LiteralPath $beforeRamPath)) { [System.IO.File]::ReadAllBytes($beforeRamPath) } else { $null }
$progressDiff = if ($null -ne $beforeRam) { Compare-ProgressSnapshots $beforeRam $ram } else { $null }

$editsByIndex = @{}
foreach ($edit in @(Get-ArrayField $nativeEdits "edits")) {
    $index = [int](Get-Field $edit "index" -1)
    if ($index -ge 0) { $editsByIndex[$index] = $edit }
    $trueIndex = Get-Field $edit "trueIndex" $null
    if ($null -ne $trueIndex) { $editsByIndex[[int]$trueIndex] = $edit }
    else {
        $legacyIndex = Get-Field $edit "legacyIndex" $null
        if ($null -eq $legacyIndex -and $index -ge 0) { $legacyIndex = $index }
        if ($null -ne $legacyIndex) {
            $mapped = Convert-LegacyIndexToTrue ([int]$legacyIndex)
            if ($null -ne $mapped) { $editsByIndex[[int]$mapped] = $edit }
        }
    }
}

$patchesByIndex = @{}
foreach ($patch in @(Get-ArrayField $patchPlan "binaryPatches")) {
    $index = [int](Get-Field $patch "runtimeMobyIndex" -1)
    if ($index -lt 0) { continue }
    if (-not $patchesByIndex.ContainsKey($index)) { $patchesByIndex[$index] = New-Object System.Collections.ArrayList }
    [void]$patchesByIndex[$index].Add($patch)
}

$testsToValidate = @(Get-ArrayField $patchPlan "functionalTests")
if ($testsToValidate.Count -eq 0) {
    $fallbackTests = New-Object System.Collections.ArrayList
    foreach ($edit in @(Get-ArrayField $nativeEdits "edits")) {
        $index = Get-EditRuntimeIndex $edit
        if ($index -lt 0) { continue }
        [void]$fallbackTests.Add((New-FallbackFunctionalTest $edit ($patchesByIndex.ContainsKey($index))))
    }
    $testsToValidate = @($fallbackTests.ToArray())
}

$validations = New-Object System.Collections.ArrayList
foreach ($test in @($testsToValidate)) {
    $index = [int](Get-Field $test "runtimeMobyIndex" -1)
    if ($index -lt 0 -or -not $editsByIndex.ContainsKey($index)) { continue }
    $edit = $editsByIndex[$index]
    [object[]]$patches = @(if ($patchesByIndex.ContainsKey($index)) { $patchesByIndex[$index].ToArray() } else { @() })
    $patchedAxes = @($patches | ForEach-Object { [string](Get-Field $_ "axis" "") } | Where-Object { $_ -match "^[xyz]$" } | Select-Object -Unique)
    $record = Read-MobyRecord $ram $index
    $recordFound = [bool](Get-Field $record "found" $false)

    $axisResults = New-Object System.Collections.ArrayList
    foreach ($axis in @("x", "y", "z")) {
        [void]$axisResults.Add((New-AxisResult $record $edit $axis ($patchedAxes -contains $axis)))
    }

    $category = [string](Get-Field $test "category" "unknown-moby-source-candidate")
    $hasSourcePatch = $patches.Count -gt 0
    $status = Get-ValidationStatus @($axisResults.ToArray()) $hasSourcePatch $recordFound
    $interactionProofStatus = Get-InteractionProofStatus $status $category $progressDiff
    [void]$validations.Add([ordered]@{
        runtimeMobyIndex = $index
        label = [string](Get-Field $test "label" (Get-Field $edit "label" "Moby"))
        mobyType = [string](Get-Field $test "mobyType" (Get-Field $edit "typeHex" ""))
        category = $category
        status = $status
        sourceWindowPatched = $hasSourcePatch
        patchedAxes = $patchedAxes
        patchCount = $patches.Count
        currentRecord = $record
        axisResults = @($axisResults.ToArray())
        expectation = [string](Get-Field $test "expectation" "")
        successSignal = [string](Get-Field $test "successSignal" "")
        interactionProofStatus = $interactionProofStatus
        interactionProofNext = Get-InteractionProofNext $interactionProofStatus $category
        nextAction = Get-NextAction $status $category
    })
}

$levelPointer = Get-UInt32LE $ram 0x75828
$summaryGroups = New-Object System.Collections.ArrayList
foreach ($group in @($validations | Group-Object { Get-Field $_ "status" "unknown" } | Sort-Object Name)) {
    [void]$summaryGroups.Add([ordered]@{ status = [string]$group.Name; count = [int]$group.Count })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Validate-StoneHillSourcePatch.ps1"
    purpose = "Validate whether a fresh-loaded source-BIN patch plan rebuilt Stone Hill moby placement in runtime RAM."
    patchPlanPath = $patchPlanPath
    nativeEditsPath = $nativeEditsPath
    ramPath = $RamPath
    beforeRamPath = $(if ($null -ne $beforeRam) { $beforeRamPath } else { $null })
    toleranceRaw = $ToleranceRaw
    validationLayout = $script:ValidationLayout
    runtime = [ordered]@{
        ptrLevelMobys = ("0x{0:X8}" -f $levelPointer)
        levelId = ("0x{0:X8}" -f (Get-UInt32LE $ram 0x758B4))
        decodedPlausibleMobys = Count-PlausibleMobys $ram $levelPointer
        progress = Read-ProgressSnapshot $ram
    }
    statusSummary = @($summaryGroups.ToArray())
    progressDiff = $progressDiff
    validations = @($validations.ToArray())
}

$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $outPath -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
$ramName = [System.IO.Path]::GetFileName($RamPath)
$planName = [System.IO.Path]::GetFileName($patchPlanPath)
[void]$lines.Add("# Stone Hill Source Patch Validation")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- RAM: $ramName")
[void]$lines.Add("- Plan: $planName")
[void]$lines.Add("- Layout: $($result.validationLayout)")
[void]$lines.Add("- Level: $($result.runtime.levelId), moby pointer: $($result.runtime.ptrLevelMobys), decoded plausible mobys: $($result.runtime.decodedPlausibleMobys)")
[void]$lines.Add("")
[void]$lines.Add("| Moby | Category | Patched axes | Status | Interaction proof | Current XYZ | Next |")
[void]$lines.Add("| --- | --- | --- | --- | --- | --- | --- |")
foreach ($item in @($validations)) {
    $moby = "T$($item.runtimeMobyIndex) $($item.label)"
    $axes = if (@($item.patchedAxes).Count -gt 0) { @($item.patchedAxes) -join "," } else { "-" }
    [void]$lines.Add("| $(Escape-Markdown $moby) | $(Escape-Markdown $item.category) | $(Escape-Markdown $axes) | $(Escape-Markdown $item.status) | $(Escape-Markdown $item.interactionProofStatus) | $(Escape-Markdown (Format-VectorText $item.currentRecord)) | $(Escape-Markdown $item.nextAction) |")
}
if ($null -ne $result.progressDiff) {
    [void]$lines.Add("")
    [void]$lines.Add("## Progress Diff")
    [void]$lines.Add("")
    [void]$lines.Add("- Counter changes: $(@($result.progressDiff.counterChanges).Count)")
    [void]$lines.Add("- Collectable flag changes: $($result.progressDiff.collectableFlagChangeCount)")
}
[void]$lines.Add("")
[void]$lines.Add("Use `source-placement-loaded` as placement evidence only. Chests, actors, and pickups still require their listed interaction/counter tests after this placement pass.")

$lines | Set-Content -LiteralPath $markdownPath -Encoding UTF8

Write-Host "Stone Hill source patch validation complete."
Write-Host "Validation JSON: $outPath"
Write-Host "Validation report: $markdownPath"
foreach ($item in @($validations)) {
    Write-Host ("T{0} {1}: {2}" -f $item.runtimeMobyIndex, $item.label, $item.status)
}
