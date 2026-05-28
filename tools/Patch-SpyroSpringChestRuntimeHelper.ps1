param(
    [string]$ManifestPath = ".\_local\experiments\custom-hit-bluegem-ready-candidates.json",
    [int]$CandidateIndex = 1,
    [string]$ProcessName = "duckstation",
    [int]$ControllerIndex = 32,
    [int]$ShellIndex = 38,
    [int]$SpawnGemIndex = 172,
    [int]$RepeatSeconds = 90,
    [int]$IntervalMs = 16,
    [int]$PostTriggerWatchSeconds = 8,
    [switch]$TriggerImmediately,
    [switch]$UseTownPopGemPrivate,
    [switch]$UseTownPopGemRowTemplate,
    [switch]$CopyTownPopMotionBlock,
    [string]$TownPopPath = ".\_local\experiments\town-spring-pop-live2-candidate01.bin",
    [int]$TownPopGemIndex = 110,
    [int]$RewardGemIdByte = 0x55,
    [int]$GemByte4FOverride = -1,
    [switch]$AutoSelectActiveGem,
    [string]$ActiveListScanStartOffset = "0x72000",
    [string]$ActiveListScanEndOffset = "0x72400",
    [switch]$WriteTownShellPoppedPrivate,
    [switch]$WriteBridgeShellPopBytes,
    [switch]$ResetShellPrePopState,
    [int]$ShellPoppedTimer = 0x46,
    [int]$GemPoppedTimer = 0x34,
    [int]$SpawnZDelta = 0x06EA,
    [switch]$AnimateGemZ,
    [int]$AnimateGemZEndDelta = 0x06EA,
    [int]$AnimateGemZDurationMs = 900,
    [switch]$ResetShellAfterPop,
    [int]$ShellResetDelayMs = 1200,
    [switch]$ClearHitAfterShellReset,
    [int]$ClearHitAfterShellResetDurationMs = 750,
    [switch]$WatchGemCollect,
    [int]$GemCollectMinAgeMs = 700,
    [switch]$HideShellOnGemCollect,
    [int]$HideShellXDelta = 0x200000,
    [int]$HideShellYDelta = 0x200000,
    [int]$HideShellZ = 0,
    [switch]$ClearInactiveNearChestRewardGems,
    [int]$NearChestXYRadius = 256,
    [int]$NearChestZMinDelta = 0x0300,
    [int]$NearChestZMaxDelta = 0x0900,
    [string]$ActiveListSlotOffset = "",
    [switch]$ClampActiveListSlot,
    [string]$OutPath = ".\_local\experiments\custom-hit-bluegem-probe.log"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$nativeSource = @"
using System;
using System.Runtime.InteropServices;

public static class SpyroSpringCustomHitBlueGemNative {
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern IntPtr OpenProcess(UInt32 access, bool inheritHandle, Int32 processId);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool WriteProcessMemory(IntPtr process, UInt64 address, byte[] buffer, UIntPtr size, out UIntPtr written);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool ReadProcessMemory(IntPtr process, UInt64 baseAddress, byte[] buffer, UIntPtr size, out UIntPtr bytesRead);
}
"@

Add-Type -TypeDefinition $nativeSource

function Read-U32([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-U16([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-I32([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [int32]0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Convert-HexU32([string]$Value) {
    if ($Value.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToUInt32($Value.Substring(2), 16)
    }
    return [Convert]::ToUInt32($Value, 16)
}

function Write-U32LE([byte[]]$Bytes, [int]$Offset, [uint32]$Value) {
    $Bytes[$Offset + 0] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
    $Bytes[$Offset + 2] = [byte](($Value -shr 16) -band 0xFF)
    $Bytes[$Offset + 3] = [byte](($Value -shr 24) -band 0xFF)
}

function Write-U16LE([byte[]]$Bytes, [int]$Offset, [uint16]$Value) {
    $Bytes[$Offset + 0] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
}

function New-U32LE([uint32]$Value) {
    return [byte[]]@(
        [byte]($Value -band 0xFF),
        [byte](($Value -shr 8) -band 0xFF),
        [byte](($Value -shr 16) -band 0xFF),
        [byte](($Value -shr 24) -band 0xFF)
    )
}

function Add-DeltaU32([uint32]$BaseValue, [int32]$Delta) {
    return [uint32]([int64][uint32]$BaseValue + [int64]$Delta)
}

function Copy-Row([byte[]]$Bytes, [uint32]$TableAddress, [int]$Index, [int]$Stride) {
    $offset = [int](($TableAddress -band 0x001FFFFF) + ($Index * $Stride))
    $copy = New-Object byte[] $Stride
    [Array]::Copy($Bytes, $offset, $copy, 0, $Stride)
    return ,$copy
}

function Read-ProcessBytes($Handle, [uint64]$Address, [int]$Length) {
    $buffer = New-Object byte[] $Length
    [UIntPtr]$read = [UIntPtr]::Zero
    $ok = [SpyroSpringCustomHitBlueGemNative]::ReadProcessMemory($Handle, $Address, $buffer, [UIntPtr]::op_Explicit([uint64]$Length), [ref]$read)
    if (-not $ok -or $read.ToUInt64() -ne [uint64]$Length) {
        $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "ReadProcessMemory failed at 0x$($Address.ToString('X')): read $($read.ToUInt64())/$Length, err=$err"
    }
    return ,$buffer
}

function Read-ProcessU32($Handle, [uint64]$Address) {
    $bytes = Read-ProcessBytes $Handle $Address 4
    return [BitConverter]::ToUInt32($bytes, 0)
}

function Write-Bytes($Handle, [uint64]$Address, [byte[]]$Bytes, [string]$Label, [switch]$Quiet) {
    [UIntPtr]$written = [UIntPtr]::Zero
    $size = [UIntPtr]::op_Explicit([uint64]$Bytes.Length)
    $ok = [SpyroSpringCustomHitBlueGemNative]::WriteProcessMemory($Handle, $Address, $Bytes, $size, [ref]$written)
    if (-not $ok -or $written.ToUInt64() -ne [uint64]$Bytes.Length) {
        $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "WriteProcessMemory failed for $Label at 0x$($Address.ToString('X')): wrote $($written.ToUInt64())/$($Bytes.Length), err=$err"
    }
    if (-not $Quiet) {
        Write-Host "Wrote $Label at process address 0x$($Address.ToString('X')) ($($Bytes.Length) bytes)."
    }
}

function Test-ActiveListContainsPointer($Handle, [uint64]$BaseAddress, [uint32]$StartOffset, [uint32]$EndOffset, [uint32]$Pointer) {
    for ($slotOffset = [int]$StartOffset; $slotOffset -lt [int]$EndOffset; $slotOffset += 4) {
        $ptr = Read-ProcessU32 $Handle ($BaseAddress + [uint64]$slotOffset)
        if ($ptr -eq $Pointer) { return $true }
    }
    return $false
}

if ($RepeatSeconds -lt 1) { throw "-RepeatSeconds must be at least 1." }
if ($IntervalMs -lt 16) { throw "-IntervalMs must be at least 16." }
if ($ShellResetDelayMs -lt 0) { throw "-ShellResetDelayMs must be non-negative." }
if ($ClearHitAfterShellResetDurationMs -lt 0) { throw "-ClearHitAfterShellResetDurationMs must be non-negative." }
if ($GemCollectMinAgeMs -lt 0) { throw "-GemCollectMinAgeMs must be non-negative." }
if ($NearChestXYRadius -lt 0) { throw "-NearChestXYRadius must be non-negative." }
if ($NearChestZMinDelta -gt $NearChestZMaxDelta) { throw "-NearChestZMinDelta must be <= -NearChestZMaxDelta." }
if ($RewardGemIdByte -lt 0x53 -or $RewardGemIdByte -gt 0x57) { throw "-RewardGemIdByte must be 0x53..0x57." }
if ($GemByte4FOverride -lt 0) {
    $GemByte4FOverride = $RewardGemIdByte - 0x52
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
$candidate = $null
foreach ($item in @($manifest)) {
    if ([int]$item.index -eq $CandidateIndex) { $candidate = $item; break }
}
if ($null -eq $candidate) { throw "Candidate $CandidateIndex was not found in $ManifestPath." }

$baseAddress = [Convert]::ToUInt64(([string]$candidate.address).Substring(2), 16)
$ram = [IO.File]::ReadAllBytes([string]$candidate.path)
if ($ram.Length -lt 0x200000) { throw "Expected a 2 MB RAM capture at $($candidate.path)." }

$levelPtr = Read-U32 $ram 0x75828
if ($levelPtr -ne [Convert]::ToUInt32("8016D3E8", 16)) {
    throw ("Candidate {0} does not look like Artisans: level moby pointer is 0x{1:X8}" -f $CandidateIndex, $levelPtr)
}

$stride = 0x58
$tableOffset = [int]($levelPtr -band 0x001FFFFF)
$controllerOffset = $tableOffset + ($ControllerIndex * $stride)
$shellOffset = $tableOffset + ($ShellIndex * $stride)
$controllerAddress = [uint32]([uint64]$levelPtr + [uint64]($ControllerIndex * $stride))
$shellAddress = [uint32]([uint64]$levelPtr + [uint64]($ShellIndex * $stride))
$activeListSlotOffsetValue = [uint32]0
$activeListPreviousPointer = [uint32]0
$useActiveListSlot = -not [string]::IsNullOrWhiteSpace($ActiveListSlotOffset)
$autoSelectedGemNote = ""
$activeListScanStartValue = Convert-HexU32 $ActiveListScanStartOffset
$activeListScanEndValue = Convert-HexU32 $ActiveListScanEndOffset
if ($activeListScanStartValue -ge $activeListScanEndValue -or $activeListScanEndValue -gt [uint32]$ram.Length) {
    throw ("Invalid active-list scan range 0x{0:X}..0x{1:X}." -f $activeListScanStartValue, $activeListScanEndValue)
}

$controllerActor = Read-U16 $ram ($controllerOffset + 0x36)
$shellActor = Read-U16 $ram ($shellOffset + 0x36)
if (($controllerActor -ne 0x01FE -and $controllerActor -ne 0x00C2) -or $shellActor -ne 0x0149) {
    throw ("Expected current spring T{0}/T{1} actors 0x01FE-or-0x00C2/0x0149, got 0x{2:X4}/0x{3:X4}." -f $ControllerIndex, $ShellIndex, $controllerActor, $shellActor)
}
if ($AutoSelectActiveGem) {
    $tableEnd = [uint32]([uint64]$levelPtr + [uint64](240 * $stride))
    $donors = New-Object System.Collections.ArrayList
    for ($slotOffset = [int]$activeListScanStartValue; $slotOffset -lt [int]$activeListScanEndValue; $slotOffset += 4) {
        $ptr = Read-U32 $ram $slotOffset
        $isRowPointer = (
            $ptr -ge $levelPtr -and
            $ptr -lt $tableEnd -and
            (($ptr - $levelPtr) % $stride) -eq 0
        )
        if (-not $isRowPointer) { continue }

        $index = [int](($ptr - $levelPtr) / $stride)
        if ($index -eq $ControllerIndex -or $index -eq $ShellIndex) { continue }
        $rowOffset = $tableOffset + ($index * $stride)
        $actor = Read-U16 $ram ($rowOffset + 0x36)
        if ($actor -ne 0x0053 -and $actor -ne 0x0054 -and $actor -ne 0x0055) { continue }

        [void]$donors.Add([pscustomobject]@{
            index = $index
            actor = [int]$actor
            slotOffset = $slotOffset
            pointer = $ptr
            priority = if ($actor -eq 0x0055) { 1 } else { 0 }
        })
    }

    $selected = @($donors | Sort-Object -Property priority, @{ Expression = { $_.index }; Descending = $true } | Select-Object -First 1)
    if ($selected.Count -eq 0) {
        throw "AutoSelectActiveGem could not find an active 0x0053/0x0054/0x0055 donor row. Reload the level or dump again before running the probe."
    }

    $SpawnGemIndex = [int]$selected[0].index
    $autoSelectedGemNote = ("autoSelectedActiveGem=T{0}; actor=0x{1:X4}; activeListSlot=0x800{2:X5}" -f $SpawnGemIndex, [int]$selected[0].actor, [int]$selected[0].slotOffset)
    Write-Host $autoSelectedGemNote
}

$spawnGemOffset = $tableOffset + ($SpawnGemIndex * $stride)
$spawnGemAddress = [uint32]([uint64]$levelPtr + [uint64]($SpawnGemIndex * $stride))
$spawnActor = Read-U16 $ram ($spawnGemOffset + 0x36)
if ($spawnActor -ne 0x0053 -and $spawnActor -ne 0x0054 -and $spawnActor -ne 0x0055) {
    throw ("Refusing to reuse T{0}: expected an already-active gem row 0x0053/0x0054/0x0055, got actor 0x{1:X4}." -f $SpawnGemIndex, $spawnActor)
}
if ($useActiveListSlot) {
    $activeListSlotOffsetValue = Convert-HexU32 $ActiveListSlotOffset
    $activeListPreviousPointer = Read-U32 $ram ([int]$activeListSlotOffsetValue)
    $tableEnd = [uint32]([uint64]$levelPtr + [uint64](240 * $stride))
    $isRowPointer = (
        $activeListPreviousPointer -ge $levelPtr -and
        $activeListPreviousPointer -lt $tableEnd -and
        (($activeListPreviousPointer - $levelPtr) % $stride) -eq 0
    )
    if (-not $isRowPointer) {
        throw ("Refusing active-list slot 0x800{0:X5}: expected an occupied moby row pointer, got 0x{1:X8}." -f $activeListSlotOffsetValue, $activeListPreviousPointer)
    }
}

$gemP00 = Read-U32 $ram $spawnGemOffset
$shellP00 = Read-U32 $ram $shellOffset
$shellX = Read-U32 $ram ($shellOffset + 0x0C)
$shellY = Read-U32 $ram ($shellOffset + 0x10)
$shellZ = Read-U32 $ram ($shellOffset + 0x14)

$gemRow = $null
$townPopGemTemplate = $null
if ($UseTownPopGemRowTemplate -or $CopyTownPopMotionBlock) {
    $resolvedTownPopPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($TownPopPath)
    $townPop = [IO.File]::ReadAllBytes($resolvedTownPopPath)
    $townLevelPtr = Read-U32 $townPop 0x75828
    $townGemOffset = [int](($townLevelPtr -band 0x001FFFFF) + ($TownPopGemIndex * $stride))
    $townGemActor = Read-U16 $townPop ($townGemOffset + 0x36)
    if ($townGemActor -ne 0x0055) {
        throw ("Expected Town pop gem template T{0} actor 0x0055, got 0x{1:X4}." -f $TownPopGemIndex, $townGemActor)
    }
    $townPopGemTemplate = Copy-Row $townPop $townLevelPtr $TownPopGemIndex $stride
}
if ($UseTownPopGemRowTemplate) {
    $gemRow = $townPopGemTemplate
}
else {
    $gemRow = Copy-Row $ram $levelPtr $SpawnGemIndex $stride
}
Write-U32LE $gemRow 0x04 ([uint32]0)
Write-U32LE $gemRow 0x08 ([uint32]0)
Write-U32LE $gemRow 0x00 $gemP00
Write-U32LE $gemRow 0x0C $shellX
Write-U32LE $gemRow 0x10 $shellY
Write-U32LE $gemRow 0x14 (Add-DeltaU32 $shellZ ([int32]$SpawnZDelta))
Write-U32LE $gemRow 0x18 ([uint32]0)
if ($CopyTownPopMotionBlock) {
    [Array]::Copy($townPopGemTemplate, 0x1C, $gemRow, 0x1C, 0x18)
    [Array]::Copy($townPopGemTemplate, 0x44, $gemRow, 0x44, 0x04)
}
Write-U16LE $gemRow 0x34 ([uint16]0xFFFF)
Write-U16LE $gemRow 0x36 ([uint16]$RewardGemIdByte)
if ($UseTownPopGemPrivate) {
    $gemRow[0x3A] = [byte]0x00
    Write-U32LE $gemRow 0x50 ([Convert]::ToUInt32("FF400118", 16))
    $gemRow[0x51] = [byte]0x01
}
else {
    $gemRow[0x3A] = [byte]0x7D
    Write-U32LE $gemRow 0x50 ([Convert]::ToUInt32("FF400018", 16))
    $gemRow[0x51] = [byte]0x00
}
if ($GemByte4FOverride -ge 0) {
    if ($GemByte4FOverride -gt 0xFF) { throw "-GemByte4FOverride must be between 0 and 255." }
    $gemRow[0x4F] = [byte]$GemByte4FOverride
}

$townPopGemPrivate = New-Object byte[] 0x20
Write-U32LE $townPopGemPrivate 0x00 ([uint32]0)
Write-U32LE $townPopGemPrivate 0x04 ([uint32]0)
Write-U32LE $townPopGemPrivate 0x08 ([uint32]0)
Write-U32LE $townPopGemPrivate 0x0C ([uint32](([int64]($GemPoppedTimer -band 0xFF) -shl 24) -bor 0x0000000F))
Write-U32LE $townPopGemPrivate 0x10 ([uint32]$GemByte4FOverride)
Write-U32LE $townPopGemPrivate 0x14 ([uint32]0xFF)
Write-U32LE $townPopGemPrivate 0x18 ([uint32]0)
Write-U32LE $townPopGemPrivate 0x1C ([uint32]0)

$process = Get-Process | Where-Object { $_.ProcessName -like "$ProcessName*" } | Sort-Object StartTime -Descending | Select-Object -First 1
if ($null -eq $process) { throw "No process matching '$ProcessName*' was found." }

$access = [uint32]0x0038
$handle = [SpyroSpringCustomHitBlueGemNative]::OpenProcess($access, $false, [int]$process.Id)
if ($handle -eq [IntPtr]::Zero) {
    $err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    throw "OpenProcess failed for pid $($process.Id), err=$err"
}

$zeroWord = [byte[]]@(0, 0, 0, 0)
$zeroByte = [byte[]]@(0)
$oneByte = [byte[]]@(1)
$shellPreByte3A = [byte[]]@(0x7D)
$shellPostByte3A = [byte[]]@(0xFD)
$shellPostWord50 = New-U32LE ([Convert]::ToUInt32("55400120", 16))
$controllerPreWord50 = New-U32LE ([Convert]::ToUInt32("53100020", 16))
$shellPreWord50Value = "55100120"
$shellPreWord50 = New-U32LE ([Convert]::ToUInt32($shellPreWord50Value, 16))
$townSubId = [byte[]]@(0xAC, 0x01)
$activeListSpawnGemPointer = New-U32LE $spawnGemAddress
$townShellPrePrivateHead = New-Object byte[] 8
Write-U32LE $townShellPrePrivateHead 0x00 ([uint32]0)
Write-U32LE $townShellPrePrivateHead 0x04 ([uint32]2)
$townShellPoppedPrivateHead = New-Object byte[] 8
Write-U32LE $townShellPoppedPrivateHead 0x00 $spawnGemAddress
Write-U32LE $townShellPoppedPrivateHead 0x04 ([uint32]$ShellPoppedTimer)
$hiddenShellX = Add-DeltaU32 $shellX ([int32]$HideShellXDelta)
$hiddenShellY = Add-DeltaU32 $shellY ([int32]$HideShellYDelta)
$hiddenShellZ = [uint32]$HideShellZ
$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$lines = New-Object System.Collections.Generic.List[string]

try {
    $liveLevelPtr = Read-ProcessU32 $handle ($baseAddress + [uint64]0x75828)
    if ($liveLevelPtr -ne $levelPtr) {
        throw ("Live process level pointer changed after dump: expected 0x{0:X8}, got 0x{1:X8}. Reload/dump again before running this probe." -f $levelPtr, $liveLevelPtr)
    }

    $liveControllerActor = Read-U16 (Read-ProcessBytes $handle ($baseAddress + [uint64]$controllerOffset) $stride) 0x36
    $liveShellActor = Read-U16 (Read-ProcessBytes $handle ($baseAddress + [uint64]$shellOffset) $stride) 0x36
    $liveSpawnActor = Read-U16 (Read-ProcessBytes $handle ($baseAddress + [uint64]$spawnGemOffset) $stride) 0x36
    if (($liveControllerActor -ne 0x01FE -and $liveControllerActor -ne 0x00C2) -or $liveShellActor -ne 0x0149) {
        throw ("Live spring rows changed: T{0}/T{1} actors are 0x{2:X4}/0x{3:X4}." -f $ControllerIndex, $ShellIndex, $liveControllerActor, $liveShellActor)
    }
    if ($liveSpawnActor -ne $spawnActor) {
        throw ("Live spawn row T{0} changed after dump: expected actor 0x{1:X4}, got 0x{2:X4}." -f $SpawnGemIndex, $spawnActor, $liveSpawnActor)
    }
    if ($useActiveListSlot) {
        $liveActiveListPointer = Read-ProcessU32 $handle ($baseAddress + [uint64]$activeListSlotOffsetValue)
        $tableEnd = [uint32]([uint64]$levelPtr + [uint64](240 * $stride))
        $liveActiveListIsRowPointer = (
            $liveActiveListPointer -ge $levelPtr -and
            $liveActiveListPointer -lt $tableEnd -and
            (($liveActiveListPointer - $levelPtr) % $stride) -eq 0
        )
        if (-not $liveActiveListIsRowPointer) {
            throw ("Live active-list slot 0x800{0:X5} moved to 0x{1:X8}; refusing to write into a boundary/non-row slot." -f $activeListSlotOffsetValue, $liveActiveListPointer)
        }
        $activeListPreviousPointer = $liveActiveListPointer
    }

    if ($ClearInactiveNearChestRewardGems) {
        $liveActiveRowPointers = New-Object 'System.Collections.Generic.HashSet[uint32]'
        $tableEnd = [uint32]([uint64]$levelPtr + [uint64](240 * $stride))
        for ($slotOffset = [int]$activeListScanStartValue; $slotOffset -lt [int]$activeListScanEndValue; $slotOffset += 4) {
            $ptr = Read-ProcessU32 $handle ($baseAddress + [uint64]$slotOffset)
            $isRowPointer = (
                $ptr -ge $levelPtr -and
                $ptr -lt $tableEnd -and
                (($ptr - $levelPtr) % $stride) -eq 0
            )
            if ($isRowPointer) {
                [void]$liveActiveRowPointers.Add([uint32]$ptr)
            }
        }

        $blankRow = New-Object byte[] $stride
        $clearedRows = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt 240; $i++) {
            if ($i -eq $ControllerIndex -or $i -eq $ShellIndex -or $i -eq $SpawnGemIndex) { continue }
            $rowAddress = [uint32]([uint64]$levelPtr + [uint64]($i * $stride))
            if ($liveActiveRowPointers.Contains($rowAddress)) { continue }

            $rowOffset = $tableOffset + ($i * $stride)
            $row = Read-ProcessBytes $handle ($baseAddress + [uint64]$rowOffset) $stride
            $actor = Read-U16 $row 0x36
            if ($actor -lt 0x0053 -or $actor -gt 0x0057) { continue }

            $rowX = [int64][uint32](Read-U32 $row 0x0C)
            $rowY = [int64][uint32](Read-U32 $row 0x10)
            $rowZ = [int64][uint32](Read-U32 $row 0x14)
            $dx = [Math]::Abs($rowX - [int64][uint32]$shellX)
            $dy = [Math]::Abs($rowY - [int64][uint32]$shellY)
            $dz = $rowZ - [int64][uint32]$shellZ
            if ($dx -le $NearChestXYRadius -and $dy -le $NearChestXYRadius -and $dz -ge $NearChestZMinDelta -and $dz -le $NearChestZMaxDelta) {
                Write-Bytes $handle ($baseAddress + [uint64]$rowOffset) $blankRow ("inactive stale near-chest reward gem T{0}" -f $i)
                $clearedRows.Add(("T{0}/deltaZ=0x{1:X}" -f $i, [int]$dz)) | Out-Null
            }
        }
        if ($clearedRows.Count -gt 0) {
            $joinedClearedRows = [string]::Join(",", $clearedRows)
            $lines.Add(("clearedInactiveNearChestRewardGems={0}" -f $joinedClearedRows)) | Out-Null
        }
        Write-Host ("Inactive near-chest reward gem cleanup cleared {0} row(s)." -f $clearedRows.Count)
    }

    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x04)) (New-U32LE $levelPtr) "controller p04 -> level table"
    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x34)) $townSubId "controller sub-id 0x01AC"
    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x50)) $controllerPreWord50 "controller pre-pop flags"
    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x18)) $zeroWord "controller hit clear"
    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x04)) (New-U32LE $controllerAddress) "shell p04 -> controller"
    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x34)) $townSubId "shell sub-id 0x01AC"
    if ($ResetShellPrePopState) {
        Write-Bytes $handle ($baseAddress + [uint64]($shellP00 -band 0x001FFFFF)) $townShellPrePrivateHead "shell p00 -> pre-pop head"
        Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3A)) $shellPreByte3A "shell pre-pop byte3A"
        Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3C)) $zeroByte "shell pre-pop byte3C"
        Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3D)) $zeroByte "shell pre-pop byte3D"
        Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3F)) $zeroByte "shell pre-pop byte3F"
        Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x48)) $zeroByte "shell pre-pop byte48"
    }
    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x50)) $shellPreWord50 "shell pre-pop flags"
    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x18)) $zeroWord "shell hit clear"

    Write-Host ("Custom hit blue-gem probe armed for {0}s. Flame or charge the spring chest now." -f $RepeatSeconds)
    $stopAt = [DateTime]::UtcNow.AddSeconds($RepeatSeconds)
    $triggered = $false
    $triggerAt = [DateTime]::MinValue
    $animationCompleted = $false
    $shellResetCompleted = $false
    $clearHitAfterResetLogged = $false
    $spawnGemWasListed = $false
    $collectionDetected = $false
    $lastText = ""

    while ([DateTime]::UtcNow -lt $stopAt) {
        $shellHit = Read-ProcessU32 $handle ($baseAddress + [uint64]($shellOffset + 0x18))
        $controllerHit = Read-ProcessU32 $handle ($baseAddress + [uint64]($controllerOffset + 0x18))
        $text = ("{0} shellHit=0x{1:X8} controllerHit=0x{2:X8} triggered={3}" -f [DateTime]::UtcNow.ToString("HH:mm:ss.fff"), $shellHit, $controllerHit, $triggered)
        if ($text -ne $lastText -and ($shellHit -ne 0 -or $controllerHit -ne 0 -or $lastText -eq "")) {
            Write-Host $text
            $lines.Add($text) | Out-Null
            $lastText = $text
        }

        if (-not $triggered -and ($TriggerImmediately -or $shellHit -ne 0 -or $controllerHit -ne 0)) {
            if ($UseTownPopGemPrivate) {
                Write-Bytes $handle ($baseAddress + [uint64]($gemP00 -band 0x001FFFFF)) $townPopGemPrivate ("T{0} p00 -> Town popped gem private words" -f $SpawnGemIndex)
            }
            Write-Bytes $handle ($baseAddress + [uint64]$spawnGemOffset) $gemRow ("active row T{0} -> custom gem 0x{1:X4}" -f $SpawnGemIndex, $RewardGemIdByte)
            if ($WriteTownShellPoppedPrivate) {
                Write-Bytes $handle ($baseAddress + [uint64]($shellP00 -band 0x001FFFFF)) $townShellPoppedPrivateHead ("shell p00 -> Town popped head, gem T{0}" -f $SpawnGemIndex)
            }
            if ($useActiveListSlot) {
                Write-Bytes $handle ($baseAddress + [uint64]$activeListSlotOffsetValue) $activeListSpawnGemPointer ("active-list slot 0x800{0:X5} -> T{1}" -f $activeListSlotOffsetValue, $SpawnGemIndex)
            }
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x18)) $zeroWord "shell hit clear after custom pop"
            Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x18)) $zeroWord "controller hit clear after custom pop"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3A)) $shellPostByte3A "shell post-pop byte3A"
            if ($WriteBridgeShellPopBytes) {
                Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3C)) $oneByte "shell bridge pop byte3C"
                Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3D)) $oneByte "shell bridge pop byte3D"
                Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3F)) $oneByte "shell bridge pop byte3F"
                Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x48)) $oneByte "shell bridge pop byte48"
            }
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x50)) $shellPostWord50 "shell post-pop flags"
            $triggered = $true
            $triggerAt = [DateTime]::UtcNow
            $lines.Add(("triggeredAt={0}; spawnGem=T{1}/0x{2:X8}; shell=0x{3:X8}; townPopGemPrivate={4}" -f $triggerAt.ToString("HH:mm:ss.fff"), $SpawnGemIndex, $spawnGemAddress, $shellAddress, [bool]$UseTownPopGemPrivate)) | Out-Null
            if ($WriteTownShellPoppedPrivate) {
                $lines.Add(("shellP00=0x{0:X8}; shellPoppedTimer=0x{1:X}" -f $shellP00, $ShellPoppedTimer)) | Out-Null
            }
            if ($WriteBridgeShellPopBytes) {
                $lines.Add("bridgeShellPopBytes=3C,3D,3F,48") | Out-Null
            }
            if ($useActiveListSlot) {
                $lines.Add(("activeListSlot=0x800{0:X5}; previousPointer=0x{1:X8}; clamp={2}" -f $activeListSlotOffsetValue, $activeListPreviousPointer, [bool]$ClampActiveListSlot)) | Out-Null
            }
            if ($UseTownPopGemRowTemplate) {
                $lines.Add(("townPopGemRowTemplate=T{0}; townPopPath={1}" -f $TownPopGemIndex, $TownPopPath)) | Out-Null
            }
            if ($CopyTownPopMotionBlock) {
                $lines.Add(("copyTownPopMotionBlock=T{0}; ranges=0x1C..0x33,0x44..0x47" -f $TownPopGemIndex)) | Out-Null
            }
            if ($GemByte4FOverride -ge 0) {
                $lines.Add(("gemByte4FOverride=0x{0:X2}" -f $GemByte4FOverride)) | Out-Null
            }
            $lines.Add(("rewardGemIdByte=0x{0:X2}" -f $RewardGemIdByte)) | Out-Null
            $lines.Add(("gemPoppedTimer=0x{0:X2}" -f ($GemPoppedTimer -band 0xFF))) | Out-Null
            $lines.Add(("spawnZDelta=0x{0:X}" -f $SpawnZDelta)) | Out-Null
            if (-not [string]::IsNullOrWhiteSpace($autoSelectedGemNote)) {
                $lines.Add($autoSelectedGemNote) | Out-Null
            }
            if ($ResetShellPrePopState) {
                $lines.Add(("resetShellPrePopState=true; shellPreWord50=0x{0}" -f $shellPreWord50Value)) | Out-Null
            }
            if ($AnimateGemZ) {
                $lines.Add(("animateGemZ=true; startDelta=0x{0:X}; endDelta=0x{1:X}; durationMs={2}" -f $SpawnZDelta, $AnimateGemZEndDelta, $AnimateGemZDurationMs)) | Out-Null
            }
            if ($ResetShellAfterPop) {
                $lines.Add(("resetShellAfterPop=true; delayMs={0}" -f $ShellResetDelayMs)) | Out-Null
            }
            if ($ClearHitAfterShellReset) {
                $lines.Add(("clearHitAfterShellReset=true; durationMs={0}" -f $ClearHitAfterShellResetDurationMs)) | Out-Null
            }
            if ($WatchGemCollect) {
                $lines.Add(("watchGemCollect=true; minAgeMs={0}; hideShellOnCollect={1}" -f $GemCollectMinAgeMs, [bool]$HideShellOnGemCollect)) | Out-Null
            }
            Write-Host ("Custom pop wrote active gem 0x{0:X2} into T{1}. Try touching/collecting the gem now." -f $RewardGemIdByte, $SpawnGemIndex)
        }

        $elapsedSinceTriggerMs = if ($triggered) { [int]([DateTime]::UtcNow - $triggerAt).TotalMilliseconds } else { 0 }
        if ($triggered -and $AnimateGemZ -and -not $animationCompleted) {
            if ($elapsedSinceTriggerMs -ge $AnimateGemZDurationMs) {
                $currentDelta = $AnimateGemZEndDelta
                $animationCompleted = $true
            }
            else {
                $t = [double]$elapsedSinceTriggerMs / [double][Math]::Max(1, $AnimateGemZDurationMs)
                $ease = 1.0 - ((1.0 - $t) * (1.0 - $t))
                $currentDelta = [int][Math]::Round([double]$SpawnZDelta + (([double]$AnimateGemZEndDelta - [double]$SpawnZDelta) * $ease))
            }
            Write-Bytes $handle ($baseAddress + [uint64]($spawnGemOffset + 0x14)) (New-U32LE (Add-DeltaU32 $shellZ ([int32]$currentDelta))) ("animated gem z delta 0x{0:X}" -f $currentDelta) -Quiet
        }

        if ($triggered -and $ResetShellAfterPop -and -not $shellResetCompleted -and $elapsedSinceTriggerMs -ge $ShellResetDelayMs) {
            Write-Bytes $handle ($baseAddress + [uint64]($shellP00 -band 0x001FFFFF)) $townShellPrePrivateHead "shell delayed reset p00 -> pre-pop head"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3A)) $shellPreByte3A "shell delayed reset byte3A"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3C)) $zeroByte "shell delayed reset byte3C"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3D)) $zeroByte "shell delayed reset byte3D"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x3F)) $zeroByte "shell delayed reset byte3F"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x48)) $zeroByte "shell delayed reset byte48"
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x50)) $shellPreWord50 "shell delayed reset pre-pop flags"
            if ($ClearHitAfterShellReset) {
                Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x18)) $zeroWord "shell delayed reset hit clear"
                Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x18)) $zeroWord "controller delayed reset hit clear"
            }
            $shellResetCompleted = $true
            $lines.Add(("shellResetAtMs={0}" -f $elapsedSinceTriggerMs)) | Out-Null
        }

        if ($triggered -and $ClearHitAfterShellReset -and $shellResetCompleted -and $elapsedSinceTriggerMs -le ($ShellResetDelayMs + $ClearHitAfterShellResetDurationMs)) {
            Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x18)) $zeroWord "shell post-reset hit clear hold" -Quiet
            Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x18)) $zeroWord "controller post-reset hit clear hold" -Quiet
            if (-not $clearHitAfterResetLogged) {
                $lines.Add(("clearHitAfterResetHoldUntilMs={0}" -f ($ShellResetDelayMs + $ClearHitAfterShellResetDurationMs))) | Out-Null
                $clearHitAfterResetLogged = $true
            }
        }

        if ($triggered -and $WatchGemCollect -and -not $collectionDetected) {
            $spawnGemListed = Test-ActiveListContainsPointer $handle $baseAddress $activeListScanStartValue $activeListScanEndValue $spawnGemAddress
            if ($spawnGemListed) {
                $spawnGemWasListed = $true
            }
            elseif ($spawnGemWasListed -and $elapsedSinceTriggerMs -ge $GemCollectMinAgeMs) {
                $collectionDetected = $true
                $collectionAt = [DateTime]::UtcNow
                $lines.Add(("gemCollectDetectedAt={0}; elapsedMs={1}; spawnGem=T{2}" -f $collectionAt.ToString("HH:mm:ss.fff"), $elapsedSinceTriggerMs, $SpawnGemIndex)) | Out-Null
                Write-Host ("Detected spawned gem T{0} leaving the active list after {1} ms." -f $SpawnGemIndex, $elapsedSinceTriggerMs)
                if ($HideShellOnGemCollect) {
                    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x0C)) (New-U32LE $hiddenShellX) "shell collect-hide x"
                    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x10)) (New-U32LE $hiddenShellY) "shell collect-hide y"
                    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x14)) (New-U32LE $hiddenShellZ) "shell collect-hide z"
                    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x0C)) (New-U32LE $hiddenShellX) "controller collect-hide x"
                    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x10)) (New-U32LE $hiddenShellY) "controller collect-hide y"
                    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x14)) (New-U32LE $hiddenShellZ) "controller collect-hide z"
                    Write-Bytes $handle ($baseAddress + [uint64]($shellOffset + 0x18)) $zeroWord "shell collect-hide hit clear"
                    Write-Bytes $handle ($baseAddress + [uint64]($controllerOffset + 0x18)) $zeroWord "controller collect-hide hit clear"
                    $lines.Add(("hideShellOnGemCollect=true; x=0x{0:X8}; y=0x{1:X8}; z=0x{2:X8}" -f $hiddenShellX, $hiddenShellY, $hiddenShellZ)) | Out-Null
                }
            }
        }

        if ($triggered -and $useActiveListSlot -and $ClampActiveListSlot) {
            Write-Bytes $handle ($baseAddress + [uint64]$activeListSlotOffsetValue) $activeListSpawnGemPointer ("active-list slot 0x800{0:X5} clamp -> T{1}" -f $activeListSlotOffsetValue, $SpawnGemIndex) -Quiet
        }

        if ($triggered -and [DateTime]::UtcNow -ge $triggerAt.AddSeconds($PostTriggerWatchSeconds)) {
            break
        }

        Start-Sleep -Milliseconds $IntervalMs
    }

    if (-not $triggered) {
        Write-Host "Probe window ended without observing a shell/controller hit."
        $lines.Add("notTriggered=true") | Out-Null
    }
    $lines.Add(("controller=T{0}/0x{1:X8}; shell=T{2}/0x{3:X8}; spawnGem=T{4}/0x{5:X8}; originalSpawnActor=0x{6:X4}" -f $ControllerIndex, $controllerAddress, $ShellIndex, $shellAddress, $SpawnGemIndex, $spawnGemAddress, $spawnActor)) | Out-Null
    $lines | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
    Write-Host "Wrote probe log: $resolvedOut"
}
finally {
    [void][SpyroSpringCustomHitBlueGemNative]::CloseHandle($handle)
}
