param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [ValidateSet("StoneHill", "Artisans")]
    [string]$LevelKey = "Artisans",
    [ValidateSet("Replace", "CloneIntoSlot", "Swap", "Hide", "Zero", "RewardColor")]
    [string]$Mode = "Replace",
    [int]$SourceIndex = -1,
    [int]$TargetIndex = -1,
    [int]$SecondIndex = -1,
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

function Write-Int32LE([byte[]]$Bytes, [int]$Offset, [int]$Value) {
    [byte[]]$raw = [BitConverter]::GetBytes([int32]$Value)
    [Array]::Copy($raw, 0, $Bytes, $Offset, 4)
}

function Read-Int32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt32($Bytes, $Offset)
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
                tableWadOffset = [Convert]::ToInt64("D72B38", 16)
                recordCount = 195
                confidence = "live/source-proven"
                note = "Known Stone Hill 0x58 loader-table source."
            }
        }
        "Artisans" {
            return [ordered]@{
                levelKey = "Artisans"
                displayName = "Artisans Home"
                tableWadOffset = [Convert]::ToInt64("9D42AC", 16)
                recordCount = 174
                confidence = "coordinate/type/flag matched"
                note = "Matched Artisans runtime catalog to WAD entry 10 source table for records T0-T173. Runtime records T174+ are not covered by this table yet."
            }
        }
    }
    throw "Unsupported level key: $Key"
}

function Test-RecordIndex($Table, [int]$Index, [string]$Role) {
    if ($Index -lt 0 -or $Index -ge [int]$Table.recordCount) {
        throw "$Role index T$Index is outside $($Table.displayName) source table range 0..$([int]$Table.recordCount - 1)."
    }
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

function Copy-Position([byte[]]$Destination, [byte[]]$Source) {
    [Array]::Copy($Source, 0x0C, $Destination, 0x0C, 12)
}

function Apply-OptionalPosition([byte[]]$Record) {
    if (-not [double]::IsNaN($X)) { Write-Int32LE $Record 0x0C (Convert-DoubleToRaw $X) }
    if (-not [double]::IsNaN($Y)) { Write-Int32LE $Record 0x10 (Convert-DoubleToRaw $Y) }
    if (-not [double]::IsNaN($Z)) { Write-Int32LE $Record 0x14 (Convert-DoubleToRaw $Z) }
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
$levelSlug = $LevelKey.ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($OutBinPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutBinPath = ".\Spyro the Dragon (USA)-$levelSlug-moby-$($Mode.ToLowerInvariant())-$stamp.bin"
}
$outBinPath = Resolve-WorkspacePath $OutBinPath
if ([string]::IsNullOrWhiteSpace($OutPlanPath)) {
    $OutPlanPath = "$outBinPath.patchplan.json"
}
$outPlanPath = Resolve-WorkspacePath $OutPlanPath

$layout = Detect-DiscLayout $imagePath
$patches = New-Object System.Collections.ArrayList
$sourceSummary = $null
$targetSummary = $null
$secondSummary = $null
$resultSummary = $null

$stream = [IO.File]::OpenRead($imagePath)
try {
    if ($Mode -eq "Replace" -or $Mode -eq "CloneIntoSlot") {
        Test-RecordIndex $table $SourceIndex "Source"
        Test-RecordIndex $table $TargetIndex "Target"
        $sourceBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $SourceIndex) $RecordStride
        $targetBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $TargetIndex) $RecordStride
        $newBytes = New-Object byte[] $RecordStride
        [Array]::Copy($sourceBytes, $newBytes, $RecordStride)
        Copy-Position $newBytes $targetBytes
        Apply-OptionalPosition $newBytes
        $sourceSummary = New-RecordSummary $SourceIndex $sourceBytes
        $targetSummary = New-RecordSummary $TargetIndex $targetBytes
        $resultSummary = New-RecordSummary $TargetIndex $newBytes
        $replaceKind = if ($Mode -eq "CloneIntoSlot") { "moby-record-clone-into-slot" } else { "moby-record-replace" }
        Add-Patch $patches $layout (Get-RecordWadOffset $table $TargetIndex) $newBytes $replaceKind "Replace T$TargetIndex with cloned T$SourceIndex identity, keeping target/override position."
    }
    elseif ($Mode -eq "Swap") {
        Test-RecordIndex $table $SourceIndex "First"
        Test-RecordIndex $table $SecondIndex "Second"
        $a = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $SourceIndex) $RecordStride
        $b = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $SecondIndex) $RecordStride
        $aAtB = New-Object byte[] $RecordStride
        $bAtA = New-Object byte[] $RecordStride
        [Array]::Copy($a, $aAtB, $RecordStride)
        [Array]::Copy($b, $bAtA, $RecordStride)
        Copy-Position $aAtB $b
        Copy-Position $bAtA $a
        $sourceSummary = New-RecordSummary $SourceIndex $a
        $secondSummary = New-RecordSummary $SecondIndex $b
        Add-Patch $patches $layout (Get-RecordWadOffset $table $SourceIndex) $bAtA "moby-record-swap-a" "Swap T$SourceIndex identity with T$SecondIndex, keeping original positions."
        Add-Patch $patches $layout (Get-RecordWadOffset $table $SecondIndex) $aAtB "moby-record-swap-b" "Swap T$SecondIndex identity with T$SourceIndex, keeping original positions."
    }
    elseif ($Mode -eq "Hide") {
        Test-RecordIndex $table $TargetIndex "Target"
        $targetBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $TargetIndex) $RecordStride
        $newBytes = New-Object byte[] $RecordStride
        [Array]::Copy($targetBytes, $newBytes, $RecordStride)
        $hideX = if ([double]::IsNaN($X)) { -30000.0 } else { $X }
        $hideY = if ([double]::IsNaN($Y)) { -30000.0 } else { $Y }
        $hideZ = if ([double]::IsNaN($Z)) { -30000.0 } else { $Z }
        Write-Int32LE $newBytes 0x0C (Convert-DoubleToRaw $hideX)
        Write-Int32LE $newBytes 0x10 (Convert-DoubleToRaw $hideY)
        Write-Int32LE $newBytes 0x14 (Convert-DoubleToRaw $hideZ)
        $targetSummary = New-RecordSummary $TargetIndex $targetBytes
        $resultSummary = New-RecordSummary $TargetIndex $newBytes
        Add-Patch $patches $layout (Get-RecordWadOffset $table $TargetIndex) $newBytes "moby-record-hide" "Soft-remove T$TargetIndex by moving it to a hidden out-of-level position."
    }
    elseif ($Mode -eq "Zero") {
        Test-RecordIndex $table $TargetIndex "Target"
        $targetBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $TargetIndex) $RecordStride
        $newBytes = New-Object byte[] $RecordStride
        $targetSummary = New-RecordSummary $TargetIndex $targetBytes
        $resultSummary = New-RecordSummary $TargetIndex $newBytes
        Add-Patch $patches $layout (Get-RecordWadOffset $table $TargetIndex) $newBytes "moby-record-zero" "Hard-remove T$TargetIndex by zeroing the source record. This is riskier than Hide."
    }
    elseif ($Mode -eq "RewardColor") {
        Test-RecordIndex $table $TargetIndex "Target"
        if ([string]::IsNullOrWhiteSpace($GemColor)) { throw "RewardColor mode requires -GemColor red|green|blue|yellow|purple." }
        $targetBytes = Read-WadBytes $stream $layout (Get-RecordWadOffset $table $TargetIndex) $RecordStride
        $newBytes = New-Object byte[] $RecordStride
        [Array]::Copy($targetBytes, $newBytes, $RecordStride)
        $colorBytes = Get-GemColorBytes $GemColor
        if ($newBytes[0x50] -eq 0x18) {
            $newBytes[0x36] = [byte][int]$colorBytes.sourceByte36
            $newBytes[0x4F] = [byte][int]$colorBytes.sourceByte4F
            $kind = "standalone-gem-color"
            $description = "Change standalone gem T$TargetIndex to $GemColor / value $($colorBytes.value)."
        }
        elseif ($newBytes[0x50] -eq 0x20) {
            $newBytes[0x53] = [byte][int]$colorBytes.rewardByte53
            $kind = "type20-reward-color"
            $description = "Change type 0x20 reward byte +0x53 for T$TargetIndex to $GemColor. Confirmed for normal reward chests; enemy families still need per-family checks."
        }
        else {
            throw "T$TargetIndex type 0x$($newBytes[0x50].ToString('X2')) is not a supported gem/reward color target."
        }
        $targetSummary = New-RecordSummary $TargetIndex $targetBytes
        $resultSummary = New-RecordSummary $TargetIndex $newBytes
        Add-Patch $patches $layout (Get-RecordWadOffset $table $TargetIndex) $newBytes $kind $description
    }
}
finally {
    $stream.Dispose()
}

if ($patches.Count -eq 0) { throw "No patches were generated." }

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
    generatedBy = "Export-SpyroMobyRecordMutation.ps1"
    warning = "Experimental full-record/source-byte moby mutation patch. Use disposable BIN/CUE outputs only."
    sourceImage = $imagePath
    outputImage = $(if ($PlanOnly) { $null } else { $outBinPath })
    outputCue = $(if ($PlanOnly) { $null } else { [IO.Path]::ChangeExtension($outBinPath, ".cue") })
    level = $table
    mode = $Mode
    sourceIndex = $SourceIndex
    targetIndex = $TargetIndex
    secondIndex = $SecondIndex
    gemColor = $GemColor
    sourceRecord = $sourceSummary
    targetRecordBefore = $targetSummary
    secondRecordBefore = $secondSummary
    targetRecordAfter = $resultSummary
    patchCount = $patches.Count
    patches = @($patches.ToArray())
    notes = @(
        "Replace/CloneIntoSlot can be used as an enemy/object swap or a slot-reuse add: clone a donor record into a chosen target slot while preserving the target/override position.",
        "Hide is the safer first remove operation because it keeps the record valid but moves it out of play.",
        "Zero is a harder delete and may affect loader loops or linked behavior; test only after Hide is understood.",
        "RewardColor is proven for standalone 0x18 gems. For 0x20 reward chests it patches confirmed byte +0x53; enemy families should still be tested individually."
    )
}
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPlanPath -Encoding UTF8

Write-Host "Moby mutation patch export complete."
Write-Host "Level: $($table.displayName)  Mode: $Mode"
Write-Host "Patch count: $($patches.Count)"
Write-Host "Patch plan: $outPlanPath"
if (-not $PlanOnly) {
    Write-Host "Patched BIN: $outBinPath"
    Write-Host "Patched CUE: $([IO.Path]::ChangeExtension($outBinPath, '.cue'))"
}
