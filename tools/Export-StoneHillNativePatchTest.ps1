param(
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$PatchMatrixPath = ".\stonehill-patch-test-matrix.json",
    [string]$SourceLinksPath = ".\stonehill-moby-source-links.json",
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SpecialDataPath = ".\stonehill-moby-special-data.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = ".\Spyro the Dragon (USA)-nativepatchtest.bin",
    [string]$PlanPath = "",
    [int]$MaxSourceWindows = 12,
    [switch]$TopRankedOnly,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
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
        if ($Object.Contains($Name)) {
            $value = $Object[$Name]
            if ($null -ne $value) { return $value }
        }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $Default }
    if ($null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return @($value) }
    return @($value)
}

function Set-IfMissingOrBlank([System.Collections.IDictionary]$Target, [string]$Name, $Value) {
    if ($null -eq $Value) { return }
    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) { return }
    if (-not $Target.Contains($Name) -or $null -eq $Target[$Name] -or ([string]$Target[$Name]) -eq "") {
        $Target[$Name] = $Value
    }
}

function Set-IfPresent([System.Collections.IDictionary]$Target, [string]$Name, $Value) {
    if ($null -eq $Value) { return }
    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) { return }
    if ($Value -is [System.Array] -and @($Value).Count -eq 0) { return }
    $Target[$Name] = $Value
}

function Convert-ObjectToOrderedMap($Object) {
    $map = [ordered]@{}
    if ($null -eq $Object) { return $map }
    if ($Object -is [System.Collections.IDictionary]) {
        foreach ($key in $Object.Keys) { $map[$key] = $Object[$key] }
        return $map
    }
    foreach ($prop in $Object.PSObject.Properties) {
        $map[$prop.Name] = $prop.Value
    }
    return $map
}

function Read-JsonIfPresent([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) { return $null }
    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Get-BehaviorNoteFromCandidates($Moby) {
    foreach ($candidate in @(Get-ArrayField $Moby "publicTargetCandidates")) {
        $category = [string](Get-Field $candidate "category" "")
        if ($category -ne "behavior") { continue }
        $reason = [string](Get-Field $candidate "reason" "")
        if (-not [string]::IsNullOrWhiteSpace($reason)) { return $reason }
        $name = [string](Get-Field $candidate "name" "")
        if (-not [string]::IsNullOrWhiteSpace($name)) { return $name }
    }
    return ""
}

function Get-SourceMatchSummary($SpecialRecord) {
    $parts = New-Object System.Collections.ArrayList
    foreach ($match in @(Get-ArrayField $SpecialRecord "sourceMatches")) {
        if ([int](Get-Field $match "count" 0) -le 0) { continue }
        $kind = [string](Get-Field $match "kind" "")
        $offset = (@(Get-ArrayField $match "wadRelativeOffsets") | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($kind)) { continue }
        if ($null -ne $offset -and -not [string]::IsNullOrWhiteSpace([string]$offset)) {
            [void]$parts.Add("$kind@$offset")
        }
        else {
            [void]$parts.Add($kind)
        }
        if ($parts.Count -ge 2) { break }
    }
    return ($parts.ToArray() -join ", ")
}

function Get-MetadataByIndex($Catalog, $SpecialData, $LiveOverrides) {
    $map = @{}

    foreach ($source in @($Catalog, $LiveOverrides)) {
        foreach ($moby in @(Get-ArrayField $source "mobys")) {
            $index = [int](Get-Field $moby "index" -1)
            if ($index -lt 0) { continue }
            if (-not $map.ContainsKey($index)) { $map[$index] = [ordered]@{} }
            Set-IfMissingOrBlank $map[$index] "label" ([string](Get-Field $moby "displayTargetLabel" ""))
            Set-IfMissingOrBlank $map[$index] "typeHex" ([string](Get-Field $moby "typeHex" ""))
            Set-IfMissingOrBlank $map[$index] "stateHex" ([string](Get-Field $moby "stateHex" ""))
            Set-IfMissingOrBlank $map[$index] "runtimeAddress" ([string](Get-Field $moby "runtimeAddress" ""))
            Set-IfMissingOrBlank $map[$index] "behaviorNote" (Get-BehaviorNoteFromCandidates $moby)
            Set-IfMissingOrBlank $map[$index] "evidence" ([string](Get-Field $moby "evidence" ""))
        }
    }

    foreach ($moby in @(Get-ArrayField $LiveOverrides "mobys")) {
        $index = [int](Get-Field $moby "index" -1)
        if ($index -lt 0) { continue }
        if (-not $map.ContainsKey($index)) { $map[$index] = [ordered]@{} }
        Set-IfPresent $map[$index] "label" ([string](Get-Field $moby "displayTargetLabel" ""))
        Set-IfPresent $map[$index] "typeHex" ([string](Get-Field $moby "typeHex" ""))
        Set-IfPresent $map[$index] "stateHex" ([string](Get-Field $moby "stateHex" ""))
        Set-IfPresent $map[$index] "runtimeAddress" ([string](Get-Field $moby "runtimeAddress" ""))
        Set-IfPresent $map[$index] "behaviorNote" (Get-BehaviorNoteFromCandidates $moby)
        Set-IfPresent $map[$index] "evidence" ([string](Get-Field $moby "evidence" ""))
    }

    foreach ($record in @(Get-ArrayField $SpecialData "records")) {
        $index = [int](Get-Field $record "index" -1)
        if ($index -lt 0) { continue }
        if (-not $map.ContainsKey($index)) { $map[$index] = [ordered]@{} }

        $pointer = [string](Get-Field $record "specialDataPointer" "")
        Set-IfMissingOrBlank $map[$index] "specialDataPointer" $pointer
        if ([bool](Get-Field $record "validMainRamPointer" $false)) {
            $pointerCount = @(Get-ArrayField $record "pointerFields").Count
            $matchText = Get-SourceMatchSummary $record
            $note = "Special data"
            if (-not [string]::IsNullOrWhiteSpace($pointer)) { $note += " $pointer" }
            $note += ": $pointerCount linked pointer"
            if ($pointerCount -ne 1) { $note += "s" }
            if (-not [string]::IsNullOrWhiteSpace($matchText)) { $note += "; WAD match $matchText" }
            Set-IfMissingOrBlank $map[$index] "specialDataNote" $note
            $map[$index]["specialDataLinkedPointerCount"] = $pointerCount
            $map[$index]["specialDataSourceMatches"] = @(Get-ArrayField $record "sourceMatches")
        }
    }

    return $map
}

function Merge-EditMetadata($Edit, [int]$Index, $MetadataByIndex) {
    $merged = Convert-ObjectToOrderedMap $Edit
    if ($MetadataByIndex.ContainsKey($Index)) {
        $meta = $MetadataByIndex[$Index]
        foreach ($key in $meta.Keys) {
            if ([string]$key -in @("label", "typeHex", "stateHex", "runtimeAddress", "behaviorNote", "evidence")) {
                Set-IfPresent $merged ([string]$key) $meta[$key]
            }
            else {
                Set-IfMissingOrBlank $merged ([string]$key) $meta[$key]
            }
        }
    }
    return [pscustomobject]$merged
}

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    try { return [Convert]::ToInt64($clean, 16) }
    catch { return $Default }
}

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-Int16ToBytes([int]$Value) {
    if ($Value -lt -32768 -or $Value -gt 32767) { return $null }
    return [BitConverter]::GetBytes([int16]$Value)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [System.Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    $volumeId = [System.Text.Encoding]::ASCII.GetString($buffer, 40, 32).Trim()
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
        description = $(if ($SectorSize -eq 2352) { "Raw PS1 BIN/CUE track (2352-byte sectors)" } else { "ISO image (2048-byte sectors)" })
        volumeId = $volumeId
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-PvdAt $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    throw "Could not detect PS1 disc layout for $Path"
}

function Convert-DiscFileOffsetToImageOffset($Layout, [int]$FileLba, [int64]$FileOffset) {
    $sectorOffset = [int]($FileOffset % 2048)
    $sector = [int64]$FileLba + [int64][Math]::Floor($FileOffset / 2048)
    return ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + $sectorOffset
}

function Parse-SourceWindowValue([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    if ($Text -notmatch '^([xyz])=(-?\d+)@\+(\d+)$') { return $null }
    return [ordered]@{
        axis = $matches[1]
        value = [int]$matches[2]
        inWindow = [int]$matches[3]
    }
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

function Get-FunctionalCategory($Edit, [string]$Label) {
    $text = (($Label, [string](Get-Field $Edit "behaviorNote" ""), [string](Get-Field $Edit "specialDataNote" ""), [string](Get-Field $Edit "patchLead" ""), [string](Get-Field $Edit "typeHex" "")) -join " ").ToLowerInvariant()
    if ($text -match "chest|container|contents|interact|collision") { return "container-linked-behavior-risk" }
    if ($text -match "sheep|fodder|ai/home|home anchor") { return "actor-home-anchor-risk" }
    if ($text -match "gem|treasure|collectible") { return "collectible-source-candidate" }
    if ($text -match "tree|scenery|lamp|flag") { return "scenery-source-candidate" }
    if ($text -match "helper|invisible|inactive|no-collision") { return "helper-or-nonplacement" }
    return "unknown-moby-source-candidate"
}

function Get-FunctionalExpectation([string]$Category, [bool]$HasSourceWindow) {
    if (-not $HasSourceWindow) {
        return "No source-window patch was emitted; this edit remains an editor/runtime visual move until a source record is found."
    }
    switch ($Category) {
        "container-linked-behavior-risk" {
            return "Fresh-load this source BIN to test whether the level loader rebuilds chest collision and reward behavior at the edited position. Live RAM XYZ moves are known to be visual-only for this class."
        }
        "actor-home-anchor-risk" {
            return "Fresh-load this source BIN and test movement/AI home behavior. If the model moves but AI stays anchored, search for a linked home/behavior record near the source lead."
        }
        "collectible-source-candidate" {
            return "Fresh-load this source BIN and verify the gem or collectible appears at the edited position and still increments the gem counter when picked up."
        }
        "scenery-source-candidate" {
            return "Fresh-load this source BIN and verify the prop appears at the edited position. Scenery may not have collision or rewards."
        }
        "helper-or-nonplacement" {
            return "This looks like a helper or inactive record. Treat any visible movement as weak evidence and prefer focused RAM pairs before relying on it."
        }
        default {
            return "Fresh-load this source BIN and verify both visible placement and normal in-game behavior before treating the source window as proven."
        }
    }
}

function Get-FunctionalSuccessSignal([string]$Category) {
    switch ($Category) {
        "container-linked-behavior-risk" { return "Chest loads at edited position, has collision, breaks normally, and grants or drops the expected gem reward." }
        "actor-home-anchor-risk" { return "Actor loads at edited position and its AI/home behavior follows the move." }
        "collectible-source-candidate" { return "Collectible loads at edited position and pickup changes the gem counter or collectable-state flag." }
        "scenery-source-candidate" { return "Prop loads at edited position after a fresh boot from the patched CUE." }
        "helper-or-nonplacement" { return "A focused before/after RAM pair ties the helper record to a concrete object or state change." }
        default { return "Target loads at edited position and behaves normally from a fresh patched-disc boot." }
    }
}

function New-FunctionalTestRecord($Edit, [int]$Index, [string]$Label, [bool]$HasSourceWindow, [int]$PatchCount, [string]$SkippedReason) {
    $category = Get-FunctionalCategory $Edit $Label
    return [ordered]@{
        runtimeMobyIndex = $Index
        label = $Label
        mobyType = [string](Get-Field $Edit "typeHex" "")
        specialDataPointer = [string](Get-Field $Edit "specialDataPointer" "")
        category = $category
        sourceWindowPatched = $HasSourceWindow
        patchCount = $PatchCount
        skippedReason = $SkippedReason
        behaviorNote = [string](Get-Field $Edit "behaviorNote" "")
        specialDataNote = [string](Get-Field $Edit "specialDataNote" "")
        specialDataLinkedPointerCount = [int](Get-Field $Edit "specialDataLinkedPointerCount" 0)
        expectation = Get-FunctionalExpectation $category $HasSourceWindow
        successSignal = Get-FunctionalSuccessSignal $category
    }
}

$nativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
$patchMatrixPath = Resolve-WorkspacePath $PatchMatrixPath
$sourceLinksPath = Resolve-WorkspacePath $SourceLinksPath
$catalogPath = Resolve-WorkspacePath $CatalogPath
$specialDataPath = Resolve-WorkspacePath $SpecialDataPath
$liveOverridesPath = Resolve-WorkspacePath $LiveOverridesPath
$imagePath = Resolve-WorkspacePath $ImagePath
$outPathWasDefault = -not $PSBoundParameters.ContainsKey("OutPath")
$planPathWasDefault = [string]::IsNullOrWhiteSpace($PlanPath)
$outPath = Resolve-WorkspacePath $OutPath
if ($planPathWasDefault) {
    $planPath = "$outPath.patchplan.json"
}
else {
    $planPath = Resolve-WorkspacePath $PlanPath
}

$nativeEdits = Read-JsonFile $nativeEditsPath
$patchMatrix = Read-JsonFile $patchMatrixPath
$sourceLinks = if (Test-Path -LiteralPath $sourceLinksPath) { Read-JsonFile $sourceLinksPath } else { $null }
$catalog = if (Test-Path -LiteralPath $catalogPath) { Read-JsonFile $catalogPath } else { $null }
$specialData = if (Test-Path -LiteralPath $specialDataPath) { Read-JsonFile $specialDataPath } else { $null }
$liveOverrides = if (Test-Path -LiteralPath $liveOverridesPath) { Read-JsonFile $liveOverridesPath } else { $null }
$metadataByIndex = Get-MetadataByIndex $catalog $specialData $liveOverrides
if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing Spyro disc image: $imagePath" }
if ([System.IO.Path]::GetFullPath($imagePath) -eq [System.IO.Path]::GetFullPath($outPath)) {
    throw "OutPath must be a disposable copy path, not the source image path."
}

$layout = Detect-DiscLayout $imagePath
$wadLba = 37

$testsByIndex = @{}
foreach ($test in @(Get-ArrayField $patchMatrix "rankedPatchTests")) {
    $index = [int](Get-Field $test "index" -1)
    if ($index -ge 0 -and -not $testsByIndex.ContainsKey($index)) {
        $testsByIndex[$index] = $test
    }
}

$sourceWindowsByIndex = @{}
if ($null -ne $sourceLinks) {
    foreach ($record in @(Get-ArrayField $sourceLinks "records")) {
        $index = [int](Get-Field $record "index" -1)
        if ($index -lt 0) { continue }
        $windows = @(Get-ArrayField $record "scaledAxisWindows")
        if ($windows.Count -gt 0) {
            $sourceWindowsByIndex[$index] = $windows
        }
    }
}

$patches = New-Object System.Collections.ArrayList
$skipped = New-Object System.Collections.ArrayList
$functionalTests = New-Object System.Collections.ArrayList
$seenImageOffsets = @{}

foreach ($edit in @(Get-ArrayField $nativeEdits "edits")) {
    $index = [int](Get-Field $edit "index" -1)
    $label = [string](Get-Field $edit "label" "Moby")
    if ($index -lt 0) { continue }
    $edit = Merge-EditMetadata $edit $index $metadataByIndex
    $label = [string](Get-Field $edit "label" $label)

    $test = if ($testsByIndex.ContainsKey($index)) { $testsByIndex[$index] } else { $null }
    $matrixLabel = [string](Get-Field $test "currentLabel" "")
    if (-not [string]::IsNullOrWhiteSpace($matrixLabel)) {
        $label = $matrixLabel
    }
    $lead = Get-Field $test "sourceLead" $null
    $sourceWindows = New-Object System.Collections.ArrayList

    if (-not $TopRankedOnly -and $sourceWindowsByIndex.ContainsKey($index)) {
        foreach ($window in @($sourceWindowsByIndex[$index] | Select-Object -First $MaxSourceWindows)) {
            [void]$sourceWindows.Add($window)
        }
    }

    if ($sourceWindows.Count -eq 0) {
        $status = [string](Get-Field $lead "status" "")
        if ($status -eq "patch-testable") {
            [void]$sourceWindows.Add($lead)
        }
        elseif ($null -eq $test) {
            $reason = "no source-window candidates"
            [void]$skipped.Add([ordered]@{ index = $index; label = $label; reason = $reason })
            [void]$functionalTests.Add((New-FunctionalTestRecord $edit $index $label $false 0 $reason))
            continue
        }
        else {
            $reason = "source lead is $status"
            [void]$skipped.Add([ordered]@{ index = $index; label = $label; reason = $reason })
            [void]$functionalTests.Add((New-FunctionalTestRecord $edit $index $label $false 0 $reason))
            continue
        }
    }

    $queuedForEdit = 0
    $windowRank = 0
    $functionalCategory = Get-FunctionalCategory $edit $label
    $functionalSuccessSignal = Get-FunctionalSuccessSignal $functionalCategory
    $functionalExpectation = Get-FunctionalExpectation $functionalCategory $true
    foreach ($window in @($sourceWindows)) {
        $windowRank++
        $wadWindow = Convert-HexTextToInt64 ([string](Get-Field $window "wadRelativeWindow" "")) -1
        if ($wadWindow -lt 0) { continue }

        foreach ($valueText in @(Get-ArrayField $window "values")) {
            $parsed = Parse-SourceWindowValue ([string]$valueText)
            if ($null -eq $parsed) { continue }
            $axis = [string](Get-Field $parsed "axis" "")
            $oldRaw = Get-RawAxis $edit "original" $axis
            $newRaw = Get-RawAxis $edit "edited" $axis
            $sourceDelta = [int][Math]::Round(($newRaw - $oldRaw) / 16.0)
            if ($sourceDelta -eq 0) { continue }

            $oldSourceValue = [int](Get-Field $parsed "value" 0)
            $newSourceValue = $oldSourceValue + $sourceDelta
            $patchBytes = Convert-Int16ToBytes $newSourceValue
            if ($null -eq $patchBytes) {
                [void]$skipped.Add([ordered]@{ index = $index; label = $label; axis = $axis; reason = "new source value $newSourceValue is outside int16 range" })
                continue
            }

            $wadRelativeOffset = $wadWindow + [int](Get-Field $parsed "inWindow" 0)
            $imageOffset = Convert-DiscFileOffsetToImageOffset $layout $wadLba $wadRelativeOffset
            if ($seenImageOffsets.ContainsKey([string]$imageOffset)) { continue }
            $seenImageOffsets[[string]$imageOffset] = $true

            [void]$patches.Add([ordered]@{
                id = [guid]::NewGuid().ToString()
                kind = "stone-hill-native-edit-scaled-int16-source-window"
                entityName = "L$index $label"
                runtimeMobyIndex = $index
                mobyType = [string](Get-Field $edit "typeHex" "")
                imageOffset = $imageOffset
                dataHex = Convert-BytesToHex $patchBytes
                wadRelativeOffset = ("0x{0:X}" -f $wadRelativeOffset)
                wadRelativeWindow = ("0x{0:X}" -f $wadWindow)
                sourceWindowRank = $windowRank
                sourceWindowMode = $(if ($TopRankedOnly -or $sourceWindows.Count -eq 1) { "top-ranked" } else { "broad-source-links" })
                axis = $axis
                oldSourceValue = $oldSourceValue
                newSourceValue = $newSourceValue
                oldRawAxis = $oldRaw
                newRawAxis = $newRaw
                functionalCategory = $functionalCategory
                functionalExpectation = $functionalExpectation
                functionalSuccessSignal = $functionalSuccessSignal
                behaviorNote = [string](Get-Field $edit "behaviorNote" "")
                specialDataNote = [string](Get-Field $edit "specialDataNote" "")
                specialDataPointer = [string](Get-Field $edit "specialDataPointer" "")
                specialDataLinkedPointerCount = [int](Get-Field $edit "specialDataLinkedPointerCount" 0)
                confidence = "diagnostic-native-edit-source-window"
                notes = "Generated from stonehill-native-edits.json and Stone Hill source-window research data. Test only on a disposable BIN; source windows are still research leads."
            })
            $queuedForEdit++
        }
    }

    if ($queuedForEdit -eq 0) {
        $reason = "edit did not move any axis present in this source lead"
        [void]$skipped.Add([ordered]@{ index = $index; label = $label; reason = $reason })
        [void]$functionalTests.Add((New-FunctionalTestRecord $edit $index $label $false 0 $reason))
    }
    else {
        [void]$functionalTests.Add((New-FunctionalTestRecord $edit $index $label $true $queuedForEdit ""))
    }
}

if ($patches.Count -gt 0 -and -not $PlanOnly) {
    try {
        Copy-Item -LiteralPath $imagePath -Destination $outPath -Force
    }
    catch {
        if (-not $outPathWasDefault) { throw }
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $fallbackName = "Spyro the Dragon (USA)-nativepatchtest-$stamp.bin"
        $outPath = Join-Path (Split-Path -Parent $outPath) $fallbackName
        if ($planPathWasDefault) {
            $planPath = "$outPath.patchplan.json"
        }
        Write-Warning "Default patched BIN is locked; writing $fallbackName instead."
        Copy-Item -LiteralPath $imagePath -Destination $outPath -Force
    }
    $stream = [System.IO.File]::Open($outPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite)
    try {
        foreach ($patch in @($patches)) {
            $bytes = New-Object byte[] ([int]($patch.dataHex.Length / 2))
            for ($i = 0; $i -lt $bytes.Length; $i++) {
                $bytes[$i] = [Convert]::ToByte($patch.dataHex.Substring($i * 2, 2), 16)
            }
            $stream.Position = [int64]$patch.imageOffset
            $stream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $stream.Dispose()
    }

    $cuePath = [System.IO.Path]::ChangeExtension($outPath, ".cue")
    $cueText = "FILE `"$([System.IO.Path]::GetFileName($outPath))`" BINARY`r`n  TRACK 01 MODE2/2352`r`n    INDEX 01 00:00:00`r`n"
    Set-Content -LiteralPath $cuePath -Value $cueText -Encoding ASCII
}

$functionalCategorySummary = New-Object System.Collections.ArrayList
foreach ($group in @($functionalTests | Group-Object { Get-Field $_ "category" "unknown" } | Sort-Object Name)) {
    [void]$functionalCategorySummary.Add([ordered]@{
        category = [string]$group.Name
        count = [int]$group.Count
    })
}

$plan = [ordered]@{
    target = "Spyro the Dragon PS1 disc image"
    generatedBy = "Export-StoneHillNativePatchTest.ps1"
    generatedAt = (Get-Date).ToString("s")
    warning = "Diagnostic Stone Hill source-window patch test. Use only on a disposable BIN copy."
    functionalWarning = "Live Apply only moves runtime XYZ fields. Functional checks for collision, chest rewards, AI, and gem counters require fresh-loading the patched CUE so Spyro's level loader rebuilds runtime behavior from source data."
    sourceImage = $imagePath
    outputImage = $(if ($PlanOnly) { $null } else { $outPath })
    outputCue = $(if ($PlanOnly -or $patches.Count -eq 0) { $null } else { [System.IO.Path]::ChangeExtension($outPath, ".cue") })
    discLayout = $layout
    nativeEditsPath = $nativeEditsPath
    patchMatrixPath = $patchMatrixPath
    sourceLinksPath = $sourceLinksPath
    catalogPath = $(if (Test-Path -LiteralPath $catalogPath) { $catalogPath } else { $null })
    specialDataPath = $(if (Test-Path -LiteralPath $specialDataPath) { $specialDataPath } else { $null })
    liveOverridesPath = $(if (Test-Path -LiteralPath $liveOverridesPath) { $liveOverridesPath } else { $null })
    topRankedOnly = [bool]$TopRankedOnly
    maxSourceWindows = $MaxSourceWindows
    patchCount = $patches.Count
    functionalCategorySummary = @($functionalCategorySummary.ToArray())
    functionalTests = @($functionalTests.ToArray())
    binaryPatches = @($patches.ToArray())
    skippedEdits = @($skipped.ToArray())
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planPath -Encoding UTF8

Write-Host "Native edit patch-test export complete."
Write-Host "Patch count: $($patches.Count)"
Write-Host "Skipped edits: $($skipped.Count)"
Write-Host "Patch plan: $planPath"
if ($patches.Count -gt 0 -and -not $PlanOnly) {
    Write-Host "Patched copy: $outPath"
}
elseif ($patches.Count -eq 0) {
    Write-Host "No patched copy was created because no patchable moved source axes were found."
}
elseif ($PlanOnly) {
    Write-Host "Plan-only mode; no BIN copy was written."
}
