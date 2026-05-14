param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [ValidateSet("StoneHill", "Artisans")]
    [string]$LevelKey = "StoneHill",
    [int]$DonorIndex = -1,
    [double]$X = [double]::NaN,
    [double]$Y = [double]::NaN,
    [double]$Z = [double]::NaN,
    [ValidateSet("", "red", "green", "blue", "yellow", "purple")]
    [string]$GemColor = "",
    [string]$OutBinPath = "",
    [string]$OutPlanPath = "",
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
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

function Convert-DoubleToRaw([double]$Value) {
    return [int][Math]::Round($Value * 16.0)
}

function Read-Int32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Read-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Write-Int32LE([byte[]]$Bytes, [int]$Offset, [int]$Value) {
    [byte[]]$raw = [BitConverter]::GetBytes([int32]$Value)
    [Array]::Copy($raw, 0, $Bytes, $Offset, 4)
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

function Get-LevelSourceTable([string]$Key) {
    switch ($Key) {
        "StoneHill" {
            return [ordered]@{
                levelKey = "StoneHill"
                displayName = "Stone Hill"
                editSlug = "stonehill"
                tableWadOffset = [Convert]::ToInt64("D72B38", 16)
                sourceRecordCount = 195
                cachedRuntimeRecordCount = 197
                confidence = "source count field found at table-4; append space is zero-filled"
            }
        }
        "Artisans" {
            return [ordered]@{
                levelKey = "Artisans"
                displayName = "Artisans Home"
                editSlug = "artisans"
                tableWadOffset = [Convert]::ToInt64("9D42AC", 16)
                sourceRecordCount = 174
                cachedRuntimeRecordCount = 184
                confidence = "source count field found at table-4; append space is zero-filled"
            }
        }
    }
    throw "Unsupported level key: $Key"
}

function Get-RecordWadOffset($Table, [int]$Index) {
    return [int64]$Table.tableWadOffset + ([int64]$Index * $RecordStride)
}

function New-RecordSummary([int]$Index, [byte[]]$Bytes) {
    return [ordered]@{
        index = $Index
        typeHex = ("0x{0:X2}" -f [int]$Bytes[0x50])
        stateHex = ("0x{0:X2}" -f [int]$Bytes[0x51])
        sourceByte36Hex = ("0x{0:X2}" -f [int]$Bytes[0x36])
        sourceByte4FHex = ("0x{0:X2}" -f [int]$Bytes[0x4F])
        flag52Hex = ("0x{0:X2}" -f [int]$Bytes[0x52])
        flag53Hex = ("0x{0:X2}" -f [int]$Bytes[0x53])
        rawX = Read-Int32LE $Bytes 0x0C
        rawY = Read-Int32LE $Bytes 0x10
        rawZ = Read-Int32LE $Bytes 0x14
        x = [Math]::Round((Read-Int32LE $Bytes 0x0C) / 16.0, 4)
        y = [Math]::Round((Read-Int32LE $Bytes 0x10) / 16.0, 4)
        z = [Math]::Round((Read-Int32LE $Bytes 0x14) / 16.0, 4)
    }
}

function Test-AllZero([byte[]]$Bytes) {
    foreach ($b in $Bytes) {
        if ($b -ne 0) { return $false }
    }
    return $true
}

function Get-GemColorBytes([string]$Color) {
    switch ($Color) {
        "red" { return [ordered]@{ sourceByte36 = 0x53; sourceByte4F = 0x01; rewardByte53 = 0x53; value = 1 } }
        "green" { return [ordered]@{ sourceByte36 = 0x54; sourceByte4F = 0x02; rewardByte53 = 0x54; value = 2 } }
        "blue" { return [ordered]@{ sourceByte36 = 0x55; sourceByte4F = 0x03; rewardByte53 = 0x55; value = 5 } }
        "yellow" { return [ordered]@{ sourceByte36 = 0x56; sourceByte4F = 0x04; rewardByte53 = 0x56; value = 10 } }
        "purple" { return [ordered]@{ sourceByte36 = 0x57; sourceByte4F = 0x05; rewardByte53 = 0x57; value = 25 } }
    }
    throw "Unsupported gem color: $Color"
}

function Add-Patch($List, $Layout, [int64]$WadOffset, [byte[]]$Bytes, [string]$Kind, [string]$Description) {
    [void]$List.Add([ordered]@{
        id = [guid]::NewGuid().ToString()
        kind = $Kind
        description = $Description
        wadRelativeOffset = ("0x{0:X}" -f $WadOffset)
        imageOffset = Convert-DiscFileOffsetToImageOffset $Layout $WadLba $WadOffset
        dataHex = Convert-BytesToHex $Bytes
    })
}

$imagePath = Resolve-WorkspacePath $ImagePath
if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing image: $imagePath" }

$table = Get-LevelSourceTable $LevelKey
if ($DonorIndex -lt 0 -or $DonorIndex -ge [int]$table.sourceRecordCount) {
    throw "DonorIndex T$DonorIndex is outside $($table.displayName) source table range 0..$([int]$table.sourceRecordCount - 1)."
}

$appendIndex = [int]$table.sourceRecordCount
$newSourceRecordCount = $appendIndex + 1
$countWadOffset = [int64]$table.tableWadOffset - 4
$appendWadOffset = Get-RecordWadOffset $table $appendIndex

$levelSlug = [string]$table.editSlug
if ([string]::IsNullOrWhiteSpace($OutBinPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutBinPath = ".\Spyro the Dragon (USA)-$levelSlug-moby-append-$stamp.bin"
}
$outBinPath = Resolve-WorkspacePath $OutBinPath
if ([string]::IsNullOrWhiteSpace($OutPlanPath)) {
    $OutPlanPath = "$outBinPath.patchplan.json"
}
$outPlanPath = Resolve-WorkspacePath $OutPlanPath

$layout = Detect-DiscLayout $imagePath
$patches = New-Object System.Collections.ArrayList

$stream = [IO.File]::OpenRead($imagePath)
try {
    $countBytes = Read-WadBytes $stream $layout $countWadOffset 4
    $currentCount = [int](Read-UInt32LE $countBytes 0)
    if ($currentCount -ne [int]$table.sourceRecordCount) {
        throw "The suspected source-count field at 0x$($countWadOffset.ToString('X')) is $currentCount, expected $($table.sourceRecordCount). Refusing to append."
    }

    $appendBytesBefore = Read-WadBytes $stream $layout $appendWadOffset $RecordStride
    if (-not (Test-AllZero $appendBytesBefore)) {
        throw "Append target T$appendIndex at WAD 0x$($appendWadOffset.ToString('X')) is not empty. Refusing to overwrite possible data."
    }

    $donorBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $DonorIndex) $RecordStride
    $newRecord = New-Object byte[] $RecordStride
    [Array]::Copy($donorBytes, $newRecord, $RecordStride)
    if (-not [double]::IsNaN($X)) { Write-Int32LE $newRecord 0x0C (Convert-DoubleToRaw $X) }
    if (-not [double]::IsNaN($Y)) { Write-Int32LE $newRecord 0x10 (Convert-DoubleToRaw $Y) }
    if (-not [double]::IsNaN($Z)) { Write-Int32LE $newRecord 0x14 (Convert-DoubleToRaw $Z) }

    $gemColorValue = $null
    if (-not [string]::IsNullOrWhiteSpace($GemColor)) {
        $colorBytes = Get-GemColorBytes $GemColor
        $gemColorValue = [int]$colorBytes.value
        if ($newRecord[0x50] -eq 0x18) {
            $newRecord[0x36] = [byte][int]$colorBytes.sourceByte36
            $newRecord[0x4F] = [byte][int]$colorBytes.sourceByte4F
        }
        elseif ($newRecord[0x50] -eq 0x20) {
            $newRecord[0x53] = [byte][int]$colorBytes.rewardByte53
        }
        else {
            throw "Donor T$DonorIndex type 0x$($newRecord[0x50].ToString('X2')) is not a supported gem/reward color target."
        }
    }

    [byte[]]$newCountBytes = [BitConverter]::GetBytes([uint32]$newSourceRecordCount)
    Add-Patch $patches $layout $countWadOffset $newCountBytes "moby-source-count" "Increase $($table.displayName) source moby count from $($table.sourceRecordCount) to $newSourceRecordCount."
    Add-Patch $patches $layout $appendWadOffset $newRecord "moby-record-append" "Append cloned donor T$DonorIndex as new source record T$appendIndex."

    $donorSummary = New-RecordSummary $DonorIndex $donorBytes
    $newSummary = New-RecordSummary $appendIndex $newRecord
}
finally {
    $stream.Dispose()
}

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $imagePath -Destination $outBinPath -Force
    $outStream = [IO.File]::Open($outBinPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
    try {
        foreach ($patch in @($patches)) {
            [byte[]]$bytes = Convert-HexToBytes ([string]$patch.dataHex)
            $outStream.Position = [int64]$patch.imageOffset
            $outStream.Write($bytes, 0, $bytes.Length)
        }
    }
    finally {
        $outStream.Dispose()
    }
    $cuePath = [IO.Path]::ChangeExtension($outBinPath, ".cue")
    $cueText = "FILE `"$([IO.Path]::GetFileName($outBinPath))`" BINARY`r`n  TRACK 01 MODE2/2352`r`n    INDEX 01 00:00:00`r`n"
    Set-Content -LiteralPath $cuePath -Value $cueText -Encoding ASCII
}

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Export-SpyroMobyTableAppendTest.ps1"
    warning = "Experimental true-add source-table expansion patch. Use disposable BIN/CUE outputs only."
    sourceImage = $imagePath
    outputImage = $(if ($PlanOnly) { $null } else { $outBinPath })
    outputCue = $(if ($PlanOnly) { $null } else { [IO.Path]::ChangeExtension($outBinPath, ".cue") })
    level = $table
    sourceCountField = [ordered]@{
        wadRelativeOffset = ("0x{0:X}" -f $countWadOffset)
        oldValue = [int]$table.sourceRecordCount
        newValue = $newSourceRecordCount
    }
    donorIndex = $DonorIndex
    appendedIndex = $appendIndex
    donorRecord = $donorSummary
    appendedRecord = $newSummary
    gemColor = $GemColor
    gemValue = $gemColorValue
    expectation = [ordered]@{
        sourceRecordCountBefore = [int]$table.sourceRecordCount
        sourceRecordCountAfter = $newSourceRecordCount
        cachedRuntimeRecordCountBefore = [int]$table.cachedRuntimeRecordCount
        expectedRuntimeRecordCountAfter = ([int]$table.cachedRuntimeRecordCount + 1)
        note = "If the field is the loader source-count, fresh-loading this CUE should create one additional source moby before the runtime-generated records."
    }
    patchCount = $patches.Count
    patches = @($patches.ToArray())
}
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPlanPath -Encoding UTF8

Write-Host "Moby table append test export complete."
Write-Host "Level: $($table.displayName)"
Write-Host "Donor: T$DonorIndex -> appended T$appendIndex"
Write-Host "Count: $($table.sourceRecordCount) -> $newSourceRecordCount"
Write-Host "Patch plan: $outPlanPath"
if ($PlanOnly) {
    Write-Host "PlanOnly set; no BIN/CUE was written."
}
else {
    Write-Host "Patched BIN: $outBinPath"
    Write-Host "Patched CUE: $([IO.Path]::ChangeExtension($outBinPath, '.cue'))"
}
