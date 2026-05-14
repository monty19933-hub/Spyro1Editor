param(
    [string]$BeforePath = ".\stonehill-before-gem-clean.bin",
    [string]$AfterPath = "",
    [int[]]$MobyIndex = @(21, 32, 87, 109, 120, 131, 153, 186),
    [string]$CatalogPath = ".\stonehill-moby-catalog.json",
    [string]$SpecialDataPath = ".\stonehill-moby-special-data.json",
    [string]$LiveOverridesPath = ".\stonehill-live-validation-overrides.json",
    [string]$OutJsonPath = ".\stonehill-moby-behavior-diff.json",
    [string]$OutMarkdownPath = "",
    [switch]$CaptureLive,
    [int]$SpecialWindowBytes = 128,
    [int]$LinkedWindowBytes = 64,
    [int]$MaxChangedBytes = 24
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Read-JsonIfPresent([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) { return $null }
    return Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
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

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    try { return [Convert]::ToInt64($clean, 16) }
    catch { return $Default }
}

function Format-Hex32([uint32]$Value) {
    return ("0x{0:X8}" -f $Value)
}

function Format-RuntimeAddressFromOffset([int64]$Offset) {
    if ($Offset -lt 0) { return "" }
    $base = [Convert]::ToUInt64("80000000", 16)
    return ("0x{0:X8}" -f ([uint64]$base + [uint64]$Offset))
}

function Format-HexOffset([int64]$Value) {
    if ($Value -lt 0) { return "" }
    return ("0x{0:X}" -f $Value)
}

function Read-MobyRecord([byte[]]$Ram, [int]$Index) {
    $pointer = Get-UInt32LE $Ram 0x75828
    $start = Convert-PsxPointerToOffset $pointer
    if ($start -lt 0) {
        return [ordered]@{ index = $Index; found = $false; reason = "_ptr_levelMobys is not valid"; ptrLevelMobys = Format-Hex32 $pointer }
    }

    $offset = $start + ($Index * 0x50)
    if (($offset + 0x50) -gt $Ram.Length) {
        return [ordered]@{ index = $Index; found = $false; reason = "record is outside RAM"; ptrLevelMobys = Format-Hex32 $pointer }
    }

    return [ordered]@{
        index = $Index
        found = $true
        ptrLevelMobys = Format-Hex32 $pointer
        ramOffset = Format-HexOffset $offset
        runtimeAddress = Format-RuntimeAddressFromOffset $offset
        specialDataPointer = Format-Hex32 (Get-UInt32LE $Ram $offset)
        rawX = Get-Int32LE $Ram ($offset + 4)
        rawY = Get-Int32LE $Ram ($offset + 8)
        rawZ = Get-Int32LE $Ram ($offset + 12)
        x = [Math]::Round((Get-Int32LE $Ram ($offset + 4)) / 16.0, 4)
        y = [Math]::Round((Get-Int32LE $Ram ($offset + 8)) / 16.0, 4)
        z = [Math]::Round((Get-Int32LE $Ram ($offset + 12)) / 16.0, 4)
        typeHex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x48])
        stateHex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x49])
        flag4AHex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x4A])
        flag4BHex = ("0x{0:X2}" -f [int]$Ram[$offset + 0x4B])
    }
}

function Compare-ByteRange([byte[]]$BeforeRam, [byte[]]$AfterRam, [int]$Offset, [int]$Length, [string]$RuntimeBase) {
    if ($Offset -lt 0 -or $Offset -ge $BeforeRam.Length -or $Offset -ge $AfterRam.Length) {
        return [ordered]@{ valid = $false; changedByteCount = 0; changedBytes = @(); reason = "offset outside RAM" }
    }

    $maxLength = [Math]::Min($Length, [Math]::Min($BeforeRam.Length - $Offset, $AfterRam.Length - $Offset))
    $changes = New-Object System.Collections.ArrayList
    $count = 0
    for ($i = 0; $i -lt $maxLength; $i++) {
        $before = [byte]$BeforeRam[$Offset + $i]
        $after = [byte]$AfterRam[$Offset + $i]
        if ($before -eq $after) { continue }
        $count++
        if ($changes.Count -lt $MaxChangedBytes) {
            [void]$changes.Add([ordered]@{
                offset = ("+0x{0:X}" -f $i)
                ramOffset = Format-HexOffset ($Offset + $i)
                runtimeAddress = Format-RuntimeAddressFromOffset ($Offset + $i)
                beforeHex = ("0x{0:X2}" -f $before)
                afterHex = ("0x{0:X2}" -f $after)
            })
        }
    }

    return [ordered]@{
        valid = $true
        runtimeBase = $RuntimeBase
        ramOffset = Format-HexOffset $Offset
        byteCount = $maxLength
        changedByteCount = $count
        changedBytes = @($changes.ToArray())
        truncatedChangedBytes = [Math]::Max(0, $count - $changes.Count)
    }
}

function Compare-RecordFields($Before, $After) {
    $fields = @("specialDataPointer", "rawX", "rawY", "rawZ", "typeHex", "stateHex", "flag4AHex", "flag4BHex")
    $changes = New-Object System.Collections.ArrayList
    foreach ($field in $fields) {
        $beforeValue = Get-Field $Before $field $null
        $afterValue = Get-Field $After $field $null
        if ([string]$beforeValue -eq [string]$afterValue) { continue }
        [void]$changes.Add([ordered]@{ field = $field; before = $beforeValue; after = $afterValue })
    }
    return @($changes.ToArray())
}

function Read-ProgressSnapshot([byte[]]$Ram) {
    return [ordered]@{
        globalGemCount = [int](Get-UInt16LE $Ram 0x75860)
        globalDragonCount = Get-Int32LE $Ram 0x75750
        globalEggCount = Get-Int32LE $Ram 0x75810
        levelId = Get-UInt32LE $Ram 0x758B4
    }
}

function Compare-Progress([byte[]]$BeforeRam, [byte[]]$AfterRam) {
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

    $flagCount = 0
    for ($i = 0; $i -lt 1231; $i++) {
        $offset = 0x77900 + $i
        if ($offset -ge $BeforeRam.Length -or $offset -ge $AfterRam.Length) { break }
        if ($BeforeRam[$offset] -ne $AfterRam[$offset]) { $flagCount++ }
    }

    return [ordered]@{
        before = $before
        after = $after
        counterChanges = @($counterChanges.ToArray())
        collectableFlagChangeCount = $flagCount
    }
}

function Get-RecordMap($Records) {
    $map = @{}
    foreach ($record in @($Records)) {
        $index = [int](Get-Field $record "index" -1)
        if ($index -ge 0 -and -not $map.ContainsKey($index)) { $map[$index] = $record }
    }
    return $map
}

function Get-LabelMap($Catalog, $LiveOverrides) {
    $map = @{}
    foreach ($source in @($Catalog, $LiveOverrides)) {
        foreach ($moby in @(Get-ArrayField $source "mobys")) {
            $index = [int](Get-Field $moby "index" -1)
            if ($index -lt 0) { continue }
            $label = [string](Get-Field $moby "displayTargetLabel" "")
            if (-not [string]::IsNullOrWhiteSpace($label)) { $map[$index] = $label }
        }
    }
    return $map
}

function Get-PrimarySourceWadOffset($SpecialRecord) {
    foreach ($match in @(Get-ArrayField $SpecialRecord "sourceMatches")) {
        if ([int](Get-Field $match "count" 0) -le 0) { continue }
        $wad = @((Get-ArrayField $match "wadRelativeOffsets") | Select-Object -First 1)
        if ($wad.Count -gt 0) { return Convert-HexTextToInt64 ([string]$wad[0]) -1 }
    }
    return -1
}

function Add-Pointer([System.Collections.ArrayList]$Pointers, [uint32]$Pointer, [string]$Role, [string]$SourceWadOffset) {
    if (-not (Test-PsxPointer $Pointer)) { return }
    $text = Format-Hex32 $Pointer
    foreach ($item in @($Pointers)) {
        if ([string](Get-Field $item "pointer" "") -eq $text -and [string](Get-Field $item "role" "") -eq $Role) { return }
    }
    [void]$Pointers.Add([ordered]@{ pointer = $text; pointerValue = $Pointer; role = $Role; sourceWadOffset = $SourceWadOffset })
}

function Get-PointersToCompare($BeforeRecord, $AfterRecord, $SpecialRecord) {
    $pointers = New-Object System.Collections.ArrayList
    $basePointer = [uint32](Convert-HexTextToInt64 ([string](Get-Field $SpecialRecord "specialDataPointer" "0")) 0)
    $baseWad = Get-PrimarySourceWadOffset $SpecialRecord

    Add-Pointer $pointers ([uint32](Convert-HexTextToInt64 ([string](Get-Field $BeforeRecord "specialDataPointer" "0")) 0)) "record-before-special" ""
    Add-Pointer $pointers ([uint32](Convert-HexTextToInt64 ([string](Get-Field $AfterRecord "specialDataPointer" "0")) 0)) "record-after-special" ""
    Add-Pointer $pointers $basePointer "catalog-special" (Format-HexOffset $baseWad)

    foreach ($field in @(Get-ArrayField $SpecialRecord "pointerFields")) {
        $ptr = [uint32](Convert-HexTextToInt64 ([string](Get-Field $field "pointer" "0")) 0)
        $sourceWad = ""
        if ((Test-PsxPointer $ptr) -and (Test-PsxPointer $basePointer) -and $baseWad -ge 0) {
            $sourceWad = Format-HexOffset ($baseWad + ([int64]$ptr - [int64]$basePointer))
        }
        Add-Pointer $pointers $ptr ("linked" + [string](Get-Field $field "offset" "")) $sourceWad
    }

    return @($pointers.ToArray())
}

function Get-Interpretation($FieldChanges, $MainDiff, $SpecialDiffs, $Label) {
    $xyzChanged = @($FieldChanges | Where-Object { [string](Get-Field $_ "field" "") -in @("rawX", "rawY", "rawZ") }).Count -gt 0
    $stateChanged = @($FieldChanges | Where-Object { [string](Get-Field $_ "field" "") -eq "stateHex" }).Count -gt 0
    $specialChanged = @($SpecialDiffs | Where-Object { [int](Get-Field (Get-Field $_ "diff" $null) "changedByteCount" 0) -gt 0 }).Count -gt 0
    $labelText = ([string]$Label).ToLowerInvariant()

    if ($labelText -match "chest|container" -and $xyzChanged -and -not $specialChanged) {
        return "Main XYZ changed but linked special-data blocks did not; this supports treating RAM-only chest moves as visual placement, not collision/reward proof."
    }
    if ($labelText -match "sheep|fodder" -and $xyzChanged -and -not $specialChanged) {
        return "Main XYZ changed without linked behavior-block movement; expect a separate home/AI anchor until a source boot proves otherwise."
    }
    if ($specialChanged) {
        return "Linked special-data bytes changed; inspect the listed blocks before deciding whether behavior, contents, or collision followed the move."
    }
    if ($stateChanged) {
        return "State changed in the main moby record; pair this with progress counters/flags to classify pickup, defeat, or interaction behavior."
    }
    if ($xyzChanged) {
        return "Only placement-like main record fields changed in this pair."
    }
    if ([int](Get-Field $MainDiff "changedByteCount" 0) -gt 0) {
        return "Main record changed outside the primary XYZ/type/state summary."
    }
    return "No relevant main or linked special-data changes detected for this moby."
}

if ($CaptureLive) {
    if ([string]::IsNullOrWhiteSpace($AfterPath)) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $AfterPath = ".\stonehill-moby-behavior-live-$stamp.bin"
    }
    $resolvedAfterPath = Resolve-WorkspacePath $AfterPath
    $dumpScript = Join-Path $PSScriptRoot "Dump-DuckStationRam.ps1"
    & $dumpScript -OutPath $resolvedAfterPath
    $AfterPath = $resolvedAfterPath
}
elseif ([string]::IsNullOrWhiteSpace($AfterPath)) {
    throw "Pass -AfterPath with a second RAM dump to compare, or use -CaptureLive."
}
if ([string]::IsNullOrWhiteSpace($OutMarkdownPath)) {
    $OutMarkdownPath = [System.IO.Path]::ChangeExtension($OutJsonPath, ".md")
}

$beforePath = Resolve-WorkspacePath $BeforePath
$afterPath = Resolve-WorkspacePath $AfterPath
$outJsonPath = Resolve-WorkspacePath $OutJsonPath
$outMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $beforePath)) { throw "Missing before RAM: $beforePath" }
if (-not (Test-Path -LiteralPath $afterPath)) { throw "Missing after RAM: $afterPath" }

$beforeRam = [System.IO.File]::ReadAllBytes($beforePath)
$afterRam = [System.IO.File]::ReadAllBytes($afterPath)
$catalog = Read-JsonIfPresent $CatalogPath
$specialData = Read-JsonIfPresent $SpecialDataPath
$liveOverrides = Read-JsonIfPresent $LiveOverridesPath
$specialByIndex = Get-RecordMap (Get-ArrayField $specialData "records")
$labelsByIndex = Get-LabelMap $catalog $liveOverrides
$progress = Compare-Progress $beforeRam $afterRam

$rows = New-Object System.Collections.ArrayList
foreach ($index in @($MobyIndex | Sort-Object -Unique)) {
    $beforeRecord = Read-MobyRecord $beforeRam $index
    $afterRecord = Read-MobyRecord $afterRam $index
    $label = if ($labelsByIndex.ContainsKey($index)) { [string]$labelsByIndex[$index] } else { "L$index" }
    $recordOffset = Convert-HexTextToInt64 ([string](Get-Field $beforeRecord "ramOffset" "")) -1
    $mainDiff = Compare-ByteRange $beforeRam $afterRam $recordOffset 0x50 ([string](Get-Field $beforeRecord "runtimeAddress" ""))
    $fieldChanges = Compare-RecordFields $beforeRecord $afterRecord
    $specialDiffs = New-Object System.Collections.ArrayList

    $specialRecord = if ($specialByIndex.ContainsKey($index)) { $specialByIndex[$index] } else { $null }
    foreach ($pointer in @(Get-PointersToCompare $beforeRecord $afterRecord $specialRecord)) {
        $ptrValue = [uint32](Get-Field $pointer "pointerValue" 0)
        $offset = Convert-PsxPointerToOffset $ptrValue
        $windowBytes = if ([string](Get-Field $pointer "role" "") -like "*special*") { $SpecialWindowBytes } else { $LinkedWindowBytes }
        [void]$specialDiffs.Add([ordered]@{
            pointer = [string](Get-Field $pointer "pointer" "")
            role = [string](Get-Field $pointer "role" "")
            sourceWadOffset = [string](Get-Field $pointer "sourceWadOffset" "")
            diff = Compare-ByteRange $beforeRam $afterRam $offset $windowBytes ([string](Get-Field $pointer "pointer" ""))
        })
    }

    [void]$rows.Add([ordered]@{
        index = [int]$index
        label = $label
        beforeRecord = $beforeRecord
        afterRecord = $afterRecord
        fieldChanges = @($fieldChanges)
        mainRecordDiff = $mainDiff
        specialDataDiffs = @($specialDiffs.ToArray())
        interpretation = Get-Interpretation $fieldChanges $mainDiff @($specialDiffs.ToArray()) $label
    })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Compare-StoneHillMobyBehavior.ps1"
    purpose = "Focused Stone Hill behavior diff: compares main moby records plus linked special-data blocks so visual movement is not mistaken for collision/reward proof."
    beforePath = $beforePath
    afterPath = $afterPath
    comparedIndexes = @($MobyIndex | Sort-Object -Unique)
    progressDiff = $progress
    rows = @($rows.ToArray())
}

$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outJsonPath -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Moby Behavior Diff")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("- Before: $([System.IO.Path]::GetFileName($beforePath))")
[void]$md.Add("- After: $([System.IO.Path]::GetFileName($afterPath))")
[void]$md.Add("- Counter changes: $(@($progress.counterChanges).Count)")
[void]$md.Add("- Collectable flag changes: $($progress.collectableFlagChangeCount)")
[void]$md.Add("")
[void]$md.Add("| Moby | Field changes | Main bytes | Special blocks changed | Interpretation |")
[void]$md.Add("| --- | --- | --- | --- | --- |")
foreach ($row in @($rows)) {
    $fieldText = if (@($row.fieldChanges).Count -gt 0) { (@($row.fieldChanges) | ForEach-Object { "$($_.field):$($_.before)->$($_.after)" }) -join "; " } else { "none" }
    $specialChanged = @($row.specialDataDiffs | Where-Object { [int](Get-Field (Get-Field $_ "diff" $null) "changedByteCount" 0) -gt 0 })
    $specialText = if ($specialChanged.Count -gt 0) {
        ($specialChanged | ForEach-Object { "$($_.pointer) $($_.role) bytes=$($_.diff.changedByteCount)" }) -join "; "
    } else { "none" }
    $fieldText = $fieldText.Replace("|", "/")
    $specialText = $specialText.Replace("|", "/")
    $interpretation = ([string]$row.interpretation).Replace("|", "/")
    [void]$md.Add("| L$($row.index) $($row.label) | $fieldText | $($row.mainRecordDiff.changedByteCount) | $specialText | $interpretation |")
}
[void]$md.Add("")
[void]$md.Add("Use this alongside source-BIN validation: a real functional move needs source placement first, then an interaction pair showing counters/flags or linked behavior state changing after the object is used.")

$md | Set-Content -LiteralPath $outMarkdownPath -Encoding UTF8

Write-Host "Wrote Stone Hill moby behavior diff JSON to $outJsonPath"
Write-Host "Wrote Stone Hill moby behavior diff Markdown to $outMarkdownPath"
Write-Host "Compared mobys: $($rows.Count); counter changes: $(@($progress.counterChanges).Count); collectable flag changes: $($progress.collectableFlagChangeCount)"
