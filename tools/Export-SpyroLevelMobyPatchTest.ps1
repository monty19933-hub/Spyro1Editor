param(
    [ValidateSet("StoneHill", "Artisans", "All")]
    [string]$LevelKey = "StoneHill",
    [string]$NativeEditsPath = "",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = "",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58
$LegacyStride = 0x50
$CoordOffsets = [ordered]@{ x = 0x0C; y = 0x10; z = 0x14 }

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-LevelSourceTable([string]$Key) {
    switch ($Key) {
        "StoneHill" {
            return [ordered]@{
                levelKey = "StoneHill"
                displayName = "Stone Hill"
                editSlug = "stonehill"
                wadEntry = 12
                tableWadOffset = [Convert]::ToInt64("D72B38", 16)
                recordCount = 195
                confidence = "live/source-proven"
            }
        }
        "Artisans" {
            return [ordered]@{
                levelKey = "Artisans"
                displayName = "Artisans Home"
                editSlug = "artisans"
                wadEntry = 10
                tableWadOffset = [Convert]::ToInt64("9D42AC", 16)
                recordCount = 174
                confidence = "coordinate/type/flag matched"
            }
        }
    }
    throw "Unsupported level key: $Key"
}

function Get-LevelSourceTables([string]$Key) {
    if ($Key -eq "All") {
        return @(
            (Get-LevelSourceTable "StoneHill"),
            (Get-LevelSourceTable "Artisans")
        )
    }
    return @((Get-LevelSourceTable $Key))
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

function Convert-BytesToHex([byte[]]$Bytes) {
    return -join ($Bytes | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-HexToBytes([string]$Hex) {
    $bytes = New-Object byte[] ([int]($Hex.Length / 2))
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Write-Int32LE([byte[]]$Bytes, [int]$Offset, [int]$Value) {
    [byte[]]$raw = [BitConverter]::GetBytes([int32]$Value)
    [Array]::Copy($raw, 0, $Bytes, $Offset, 4)
}

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $trueNumerator = ($LegacyIndex * $LegacyStride) - 8
    if ($trueNumerator -lt 0 -or ($trueNumerator % $RecordStride) -ne 0) { return $null }
    return [int]($trueNumerator / $RecordStride)
}

function Resolve-EditTrueIndex($Edit) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $trueIndex = Get-Field $Edit "trueIndex" $null
    if ($null -ne $trueIndex) { return [int]$trueIndex }

    $legacyIndex = Get-Field $Edit "legacyIndex" $null
    if ($null -ne $legacyIndex) {
        $mapped = Convert-LegacyIndexToTrue ([int]$legacyIndex)
        if ($null -ne $mapped) { return $mapped }
    }

    $fallback = Convert-LegacyIndexToTrue $editIndex
    if ($null -ne $fallback) { return $fallback }
    return $editIndex
}

function Convert-PatchInt($Value, [string]$Name) {
    if ($null -eq $Value) { throw "Missing numeric patch field: $Name" }
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int][Convert]::ToInt32($text.Substring(2), 16)
        }
        return [int]$text
    }
    return [int]$Value
}

function Get-RawEditedAxis($Edit, [string]$Axis) {
    $rawEdited = Get-Field $Edit "rawEdited" $null
    $value = Get-Field $rawEdited $Axis $null
    if ($null -ne $value) { return [int]$value }

    $edited = Get-Field $Edit "edited" $null
    $floatValue = [double](Get-Field $edited $Axis 0)
    return [int][Math]::Round($floatValue * 16.0)
}

function Test-PvdAt([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    $offset = (16 * $SectorSize) + $UserOffset
    if ($Stream.Length -lt ($offset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = $offset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        userSize = 2048
        description = $(if ($SectorSize -eq 2352) { "Raw PS1 BIN/CUE track (2352-byte sectors)" } else { "ISO image (2048-byte sectors)" })
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
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

function Read-WadBytes([IO.FileStream]$Stream, $Layout, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $absolute
        $sectorOffset = [int]($absolute % 2048)
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = $imageOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return $result
}

function New-PatchRecord($Table, $Layout, $Edit, [int]$TrueIndex, [string]$Kind, [int]$RecordOffset, [byte[]]$Bytes, [string]$Description) {
    $wadOffset = [int64]$Table.tableWadOffset + ([int64]$TrueIndex * $RecordStride) + [int64]$RecordOffset
    return [ordered]@{
        levelKey = [string]$Table.levelKey
        levelName = [string]$Table.displayName
        sourceEntry = [int]$Table.wadEntry
        index = [int](Get-Field $Edit "index" -1)
        trueIndex = $TrueIndex
        label = [string](Get-Field $Edit "label" "")
        kind = $Kind
        description = $Description
        recordOffset = ("+0x{0:X2}" -f $RecordOffset)
        wadRelativeOffset = ("0x{0:X}" -f $wadOffset)
        imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $wadOffset
        bytesHex = Convert-BytesToHex $Bytes
    }
}

$tables = @(Get-LevelSourceTables $LevelKey)
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $outSlug = if ($LevelKey -eq "All") { "loaderpatchtest" } else { "$($tables[0].editSlug)-loaderpatchtest" }
    $OutPath = ".\Spyro the Dragon (USA)-$outSlug.bin"
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) {
    $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue")
}
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) {
    $PlanPath = "$resolvedOutPath.patchplan.json"
}
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath

if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }

$layout = Detect-DiscLayout $resolvedImagePath
$patches = New-Object System.Collections.ArrayList
$editSources = New-Object System.Collections.ArrayList

$stream = [IO.File]::OpenRead($resolvedImagePath)
try {
    foreach ($table in $tables) {
        $nativePath = if ([string]::IsNullOrWhiteSpace($NativeEditsPath) -or $LevelKey -eq "All") {
            Resolve-WorkspacePath ".\$($table.editSlug)-native-edits.json"
        }
        else {
            Resolve-WorkspacePath $NativeEditsPath
        }
        if (-not (Test-Path -LiteralPath $nativePath)) {
            if ($LevelKey -eq "All") { continue }
            throw "Missing native edits: $nativePath"
        }

        $editsRoot = Get-Content -Raw -LiteralPath $nativePath | ConvertFrom-Json
        $edits = @(Get-ArrayField $editsRoot "edits")
        [void]$editSources.Add([ordered]@{
            levelKey = $table.levelKey
            displayName = $table.displayName
            nativeEditsPath = (Resolve-Path -LiteralPath $nativePath).Path
            editCount = $edits.Count
        })

        foreach ($edit in $edits) {
            $trueIndex = Resolve-EditTrueIndex $edit
            if ($trueIndex -lt 0 -or $trueIndex -ge [int]$table.recordCount) {
                Write-Warning "Skipping $($table.displayName) T$trueIndex; it is outside source table range 0..$([int]$table.recordCount - 1)."
                continue
            }

            $recordMutation = Get-Field $edit "recordMutation" $null
            $mutationMode = [string](Get-Field $recordMutation "mode" "")
            if ($mutationMode -eq "cloneIntoSlot") {
                $sourceTrueIndex = [int](Get-Field $recordMutation "sourceTrueIndex" -1)
                if ($sourceTrueIndex -lt 0 -or $sourceTrueIndex -ge [int]$table.recordCount) {
                    throw "Clone source T$sourceTrueIndex is outside $($table.displayName) source table range."
                }
                $sourceWadOffset = [int64]$table.tableWadOffset + ([int64]$sourceTrueIndex * $RecordStride)
                [byte[]]$recordBytes = Read-WadBytes $stream $layout $sourceWadOffset $RecordStride
                foreach ($axis in @("x", "y", "z")) {
                    Write-Int32LE $recordBytes ([int]$CoordOffsets[$axis]) (Get-RawEditedAxis $edit $axis)
                }
                foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
                    $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                    $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                    if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                    if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                    $recordBytes[$byteOffset] = [byte]$value
                }
                $description = "Clone source record T$sourceTrueIndex into T$trueIndex, preserving edited target position."
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-record-clone-into-slot" 0x00 $recordBytes $description))
                continue
            }

            foreach ($axis in @("x", "y", "z")) {
                $raw = Get-RawEditedAxis $edit $axis
                [byte[]]$bytes = [BitConverter]::GetBytes([int32]$raw)
                $kind = if ($mutationMode -eq "hide") { "moby-coordinate-hide" } else { "moby-coordinate" }
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex $kind ([int]$CoordOffsets[$axis]) $bytes ("Set " + $axis.ToUpperInvariant() + " to raw " + $raw.ToString())))
            }

            foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
                $byteOffset = Convert-PatchInt (Get-Field $byteEdit "offset" (Get-Field $byteEdit "offsetHex" $null)) "sourceByteEdits.offset"
                $value = Convert-PatchInt (Get-Field $byteEdit "value" (Get-Field $byteEdit "valueHex" $null)) "sourceByteEdits.value"
                if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
                if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }
                [byte[]]$bytes = @([byte]$value)
                $field = [string](Get-Field $byteEdit "field" "source-byte")
                [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-source-byte" $byteOffset $bytes ("Set " + $field + " to 0x" + $value.ToString("X2"))))
            }
        }
    }
}
finally {
    $stream.Dispose()
}

if ($patches.Count -eq 0) { throw "No patchable moby edits were found for $LevelKey." }

$totalEditCount = 0
foreach ($source in @($editSources.ToArray())) {
    $totalEditCount += [int]$source["editCount"]
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroLevelMobyPatchTest.ps1"
    warning = "Experimental level moby source-table patch. Use disposable BIN/CUE outputs only."
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
    nativeEditsPath = $(if ($LevelKey -eq "All") { $null } else { @($editSources.ToArray())[0].nativeEditsPath })
    editSources = @($editSources.ToArray())
    discLayout = $layout
    sources = @($tables | ForEach-Object {
        [ordered]@{
            levelKey = $_.levelKey
            displayName = $_.displayName
            wadLba = $WadLba
            wadEntry = $_.wadEntry
            tableWadOffset = ("0x{0:X}" -f [int64]$_.tableWadOffset)
            recordCount = $_.recordCount
            recordStride = "0x58"
            coordinateOffsets = [ordered]@{ x = "+0x0C"; y = "+0x10"; z = "+0x14" }
            confidence = $_.confidence
        }
    })
    source = $(if ($tables.Count -eq 1) { $tables[0] } else { $null })
    level = $(if ($tables.Count -eq 1) { $tables[0] } else { [ordered]@{ levelKey = "All"; displayName = "All mapped levels" } })
    editCount = $totalEditCount
    patchCount = $patches.Count
    patches = @($patches.ToArray())
    binaryPatches = @($patches.ToArray())
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $stream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        foreach ($patch in @($patches.ToArray())) {
            [byte[]]$bytes = Convert-HexToBytes ([string]$patch.bytesHex)
            $stream.Position = [int64]$patch.imageOffset
            $stream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $stream.Dispose()
    }

    $cueFileName = [System.IO.Path]::GetFileName($resolvedOutPath)
    @(
        "FILE `"$cueFileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $resolvedCuePath -Encoding ASCII
}

$levelText = if ($LevelKey -eq "All") { "all mapped levels" } else { $tables[0].displayName }
Write-Host "Wrote $levelText moby patch plan to $resolvedPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
$patchedRecordKeys = @($patches | ForEach-Object { ([string]$_["levelKey"]) + ":T" + ([int]$_["trueIndex"]).ToString() } | Select-Object -Unique)
Write-Host ("Patches: {0} source-table writes across {1} edited mobys" -f $patches.Count, $patchedRecordKeys.Count)
