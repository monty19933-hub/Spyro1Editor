param(
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$OutPath = ".\Spyro the Dragon (USA)-loaderpatchtest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$SourceEntryOffset = 12138496
$SourceTableRel = 0x1DF338
$RecordStride = 0x58
$LegacyStride = 0x50
$CoordOffsets = [ordered]@{ x = 0x0C; y = 0x10; z = 0x14 }

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
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

function Convert-LegacyIndexToTrue([int]$LegacyIndex) {
    $trueNumerator = ($LegacyIndex * $LegacyStride) - 8
    if ($trueNumerator -lt 0 -or ($trueNumerator % $RecordStride) -ne 0) { return $null }
    return [int]($trueNumerator / $RecordStride)
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Get-RawEditedAxis($Edit, [string]$Axis) {
    $rawEdited = Get-Field $Edit "rawEdited" $null
    $value = Get-Field $rawEdited $Axis $null
    if ($null -ne $value) { return [int]$value }

    $edited = Get-Field $Edit "edited" $null
    $floatValue = [double](Get-Field $edited $Axis 0)
    return [int][Math]::Round($floatValue * 16.0)
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
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

function Resolve-EditTrueIndex($Edit) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $trueIndex = Get-Field $Edit "trueIndex" $null
    if ($null -ne $trueIndex) { $trueIndex = [int]$trueIndex }
    else { $trueIndex = Convert-LegacyIndexToTrue $editIndex }
    if ($null -eq $trueIndex) { throw "Edit index $editIndex does not map to a true 0x58 loader record." }
    return $trueIndex
}

function Resolve-EditLegacyIndex($Edit) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $legacyIndex = Get-Field $Edit "legacyIndex" $null
    if ($null -ne $legacyIndex) { return [int]$legacyIndex }
    if ($null -eq (Get-Field $Edit "trueIndex" $null)) { return $editIndex }
    return $null
}

function New-PatchRecord($Edit, [string]$Axis) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $trueIndex = Resolve-EditTrueIndex $Edit
    $legacyIndex = Resolve-EditLegacyIndex $Edit

    $raw = Get-RawEditedAxis $Edit $Axis
    $sourceWadOffset = [int64]$SourceEntryOffset + [int64]$SourceTableRel + ([int64]$trueIndex * $RecordStride) + [int64]$CoordOffsets[$Axis]
    $imageOffset = Convert-WadOffsetToImageOffset $sourceWadOffset
    $bytes = [BitConverter]::GetBytes([int]$raw)
    return [ordered]@{
        index = $editIndex
        legacyIndex = $legacyIndex
        trueIndex = $trueIndex
        label = [string](Get-Field $Edit "label" "")
        axis = $Axis
        rawEdited = $raw
        edited = [Math]::Round($raw / 16.0, 4)
        sourceWadOffset = ("0x{0:X}" -f $sourceWadOffset)
        sourceEntry = 12
        sourceEntryRelativeOffset = ("0x{0:X}" -f ([int64]$SourceTableRel + ([int64]$trueIndex * $RecordStride) + [int64]$CoordOffsets[$Axis]))
        imageOffset = ("0x{0:X}" -f $imageOffset)
        imageOffsetInt = $imageOffset
        bytesHex = Get-BytesHex $bytes
        runtimeMobyIndex = $trueIndex
        expectedRaw = $raw
        source = "loader-table-entry12"
    }
}

function New-BytePatchRecord($Edit, $ByteEdit) {
    $editIndex = [int](Get-Field $Edit "index" -1)
    $trueIndex = Resolve-EditTrueIndex $Edit
    $legacyIndex = Resolve-EditLegacyIndex $Edit
    $byteOffset = Convert-PatchInt (Get-Field $ByteEdit "offset" (Get-Field $ByteEdit "offsetHex" $null)) "sourceByteEdits.offset"
    $value = Convert-PatchInt (Get-Field $ByteEdit "value" (Get-Field $ByteEdit "valueHex" $null)) "sourceByteEdits.value"
    if ($byteOffset -lt 0 -or $byteOffset -ge $RecordStride) { throw "Byte edit offset 0x$($byteOffset.ToString('X')) is outside loader record stride 0x58." }
    if ($value -lt 0 -or $value -gt 255) { throw "Byte edit value 0x$($value.ToString('X')) is outside byte range." }

    $sourceWadOffset = [int64]$SourceEntryOffset + [int64]$SourceTableRel + ([int64]$trueIndex * $RecordStride) + [int64]$byteOffset
    $imageOffset = Convert-WadOffsetToImageOffset $sourceWadOffset
    return [ordered]@{
        index = $editIndex
        legacyIndex = $legacyIndex
        trueIndex = $trueIndex
        label = [string](Get-Field $Edit "label" "")
        field = [string](Get-Field $ByteEdit "field" "source-byte")
        byteOffset = ("+0x{0:X2}" -f $byteOffset)
        value = $value
        valueHex = ("0x{0:X2}" -f $value)
        sourceWadOffset = ("0x{0:X}" -f $sourceWadOffset)
        sourceEntry = 12
        sourceEntryRelativeOffset = ("0x{0:X}" -f ([int64]$SourceTableRel + ([int64]$trueIndex * $RecordStride) + [int64]$byteOffset))
        imageOffset = ("0x{0:X}" -f $imageOffset)
        imageOffsetInt = $imageOffset
        bytesHex = ("{0:X2}" -f $value)
        runtimeMobyIndex = $trueIndex
        expectedRaw = $value
        source = "loader-table-entry12"
        kind = "stone-hill-loader-table-byte-edit"
    }
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

$editsRoot = Get-Content -Raw -LiteralPath $resolvedNativeEditsPath | ConvertFrom-Json
$patches = @()
foreach ($edit in @(Get-ArrayField $editsRoot "edits")) {
    foreach ($axis in @("x", "y", "z")) {
        $patches += [pscustomobject](New-PatchRecord $edit $axis)
    }
    foreach ($byteEdit in @(Get-ArrayField $edit "sourceByteEdits")) {
        $patches += [pscustomobject](New-BytePatchRecord $edit $byteEdit)
    }
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    outPath = $resolvedOutPath
    cuePath = $resolvedCuePath
    nativeEditsPath = (Resolve-Path -LiteralPath $resolvedNativeEditsPath).Path
    source = [ordered]@{
        wadEntry = 12
        wadEntryOffset = ("0x{0:X}" -f $SourceEntryOffset)
        tableRelativeOffset = ("0x{0:X}" -f $SourceTableRel)
        recordStride = "0x58"
        coordinateOffsets = [ordered]@{ x = "+0x0C"; y = "+0x10"; z = "+0x14" }
        sourceByteOffsets = [ordered]@{ gemIdByte = "+0x36"; gemValueByte = "+0x4F" }
        note = "This table was found by matching corrected 0x58 runtime moby coordinate triples; it is not the older Stone Hill asset candidate family."
    }
    patchCount = $patches.Count
    patches = $patches
    binaryPatches = $patches
}

$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $stream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        foreach ($patch in $patches) {
            $byteCount = [int]($patch.bytesHex.Length / 2)
            $bytes = New-Object byte[] $byteCount
            for ($i = 0; $i -lt $byteCount; $i++) {
                $bytes[$i] = [Convert]::ToByte($patch.bytesHex.Substring($i * 2, 2), 16)
            }
            $stream.Position = [int64]$patch.imageOffsetInt
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

Write-Host "Wrote loader-table patch plan to $resolvedPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
}
Write-Host ("Patches: {0} loader-table writes across {1} edited mobys" -f $patches.Count, @($patches | Select-Object -ExpandProperty trueIndex -Unique).Count)
