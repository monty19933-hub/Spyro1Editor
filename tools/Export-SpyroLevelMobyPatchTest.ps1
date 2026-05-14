param(
    [ValidateSet("StoneHill", "Artisans")]
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

function New-PatchRecord($Table, $Layout, $Edit, [int]$TrueIndex, [string]$Kind, [int]$RecordOffset, [byte[]]$Bytes, [string]$Description) {
    $wadOffset = [int64]$Table.tableWadOffset + ([int64]$TrueIndex * $RecordStride) + [int64]$RecordOffset
    return [ordered]@{
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

$table = Get-LevelSourceTable $LevelKey
if ([string]::IsNullOrWhiteSpace($NativeEditsPath)) {
    $NativeEditsPath = ".\$($table.editSlug)-native-edits.json"
}
if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = ".\Spyro the Dragon (USA)-$($table.editSlug)-loaderpatchtest.bin"
}

$resolvedNativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
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

if (-not (Test-Path -LiteralPath $resolvedNativeEditsPath)) { throw "Missing native edits: $resolvedNativeEditsPath" }
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing source image: $resolvedImagePath" }

$layout = Detect-DiscLayout $resolvedImagePath
$editsRoot = Get-Content -Raw -LiteralPath $resolvedNativeEditsPath | ConvertFrom-Json
$patches = New-Object System.Collections.ArrayList

foreach ($edit in @(Get-ArrayField $editsRoot "edits")) {
    $trueIndex = Resolve-EditTrueIndex $edit
    if ($trueIndex -lt 0 -or $trueIndex -ge [int]$table.recordCount) {
        Write-Warning "Skipping T$trueIndex; it is outside $($table.displayName) source table range 0..$([int]$table.recordCount - 1)."
        continue
    }

    foreach ($axis in @("x", "y", "z")) {
        $raw = Get-RawEditedAxis $edit $axis
        [byte[]]$bytes = [BitConverter]::GetBytes([int32]$raw)
        [void]$patches.Add((New-PatchRecord $table $layout $edit $trueIndex "moby-coordinate" ([int]$CoordOffsets[$axis]) $bytes ("Set " + $axis.ToUpperInvariant() + " to raw " + $raw.ToString())))
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

if ($patches.Count -eq 0) { throw "No patchable moby edits were found for $($table.displayName)." }

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroLevelMobyPatchTest.ps1"
    warning = "Experimental level moby source-table patch. Use disposable BIN/CUE outputs only."
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
    nativeEditsPath = (Resolve-Path -LiteralPath $resolvedNativeEditsPath).Path
    discLayout = $layout
    source = [ordered]@{
        wadLba = $WadLba
        wadEntry = $table.wadEntry
        tableWadOffset = ("0x{0:X}" -f [int64]$table.tableWadOffset)
        recordCount = $table.recordCount
        recordStride = "0x58"
        coordinateOffsets = [ordered]@{ x = "+0x0C"; y = "+0x10"; z = "+0x14" }
        confidence = $table.confidence
    }
    level = $table
    editCount = @(Get-ArrayField $editsRoot "edits").Count
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

Write-Host "Wrote $($table.displayName) moby patch plan to $resolvedPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
$patchedTrueIndexes = @($patches | ForEach-Object { [int]$_["trueIndex"] } | Select-Object -Unique)
Write-Host ("Patches: {0} source-table writes across {1} edited mobys" -f $patches.Count, $patchedTrueIndexes.Count)
