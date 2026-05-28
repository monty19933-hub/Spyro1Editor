param(
    [string]$ImagePath = ".\_local\experiments\editor-artisans-springpair-T174-T175-fixed.bin",
    [string]$OutPath = ".\_local\experiments\editor-artisans-springpair-T174-T175-ingame-helper.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [uint32]$LevelPointer = ([Convert]::ToUInt32("8016D3E8", 16)),
    [int]$ControllerIndex = 174,
    [int]$ShellIndex = 175,
    [int]$SpawnGemIndex = 172,
    [int]$ControllerActorId = 0x01FE,
    [int]$RewardGemIdByte = 0x55,
    [uint32]$GemPrivateAddress = ([Convert]::ToUInt32("80174FE8", 16)),
    [uint32]$ShellPrivateAddress = ([Convert]::ToUInt32("80011EE4", 16)),
    [uint32]$HookAddress = ([Convert]::ToUInt32("80012230", 16)),
    [uint32]$OriginalHookCallTarget = ([Convert]::ToUInt32("8003385C", 16)),
    [uint32]$EntryAddress = ([Convert]::ToUInt32("8007314C", 16)),
    [int]$EntryReserveBytes = 0x100,
    [uint32]$PayloadAddress = ([Convert]::ToUInt32("80073940", 16)),
    [int]$PayloadReserveBytes = 0x1000,
    [int]$PayloadStateOffset = 0x700,
    [int]$PayloadSaveOffset = 0x720,
    [uint32]$ScratchSaveAddress = ([Convert]::ToUInt32("8007316C", 16)),
    [uint32]$RewardActiveListSlotAddress = 0,
    [uint32]$RewardPrivatePointerOverride = 0,
    [uint32]$RewardVisualTailWordOverride = 0,
    [int]$EntryGateDelayFrames = 0,
    [int]$SafeHideFrames = 90,
    [int]$RewardZOffset = 0x0558,
    [switch]$NoOpOriginalCallOnly,
    [switch]$GateOnlyOriginalCall,
    [switch]$StateOnlyProbe,
    [switch]$ActorCheckOnlyProbe,
    [switch]$ControllerOnlyProbe,
    [switch]$ControllerPtrOnlyProbe,
    [switch]$ControllerSelfPtrOnlyProbe,
    [switch]$ControllerShellPtrOnlyProbe,
    [switch]$Controller34OnlyProbe,
    [switch]$Controller50OnlyProbe,
    [switch]$ShellOnlyProbe,
    [switch]$SafeArmProbe,
    [switch]$SafeMinimalPop,
    [switch]$SafeMinimalTimedHide,
    [switch]$SafeNoRewardRefresh,
    [switch]$DarkHollowVisibleRewardFlags,
    [switch]$RewardMoveOnly,
    [switch]$SafeRewardPrivateLink,
    [switch]$ArmOnlyProbe,
    [switch]$SmallCaveMinimalPop,
    [switch]$UseEntryGate,
    [switch]$EntryGateOriginalOnly,
    [switch]$EntryGateCountOnly,
    [switch]$SkipHookPatch,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$RecordStride = 0x58
$ExeDestination = [Convert]::ToUInt32("80010000", 16)

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-U16LE([byte[]]$Bytes, [int]$Offset) { return [BitConverter]::ToUInt16($Bytes, $Offset) }
function Get-U32LE([byte[]]$Bytes, [int]$Offset) { return [BitConverter]::ToUInt32($Bytes, $Offset) }

function Set-U16LE([byte[]]$Bytes, [int]$Offset, [uint16]$Value) {
    $Bytes[$Offset + 0] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
}

function Set-U32LE([byte[]]$Bytes, [int]$Offset, [uint32]$Value) {
    $Bytes[$Offset + 0] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
    $Bytes[$Offset + 2] = [byte](($Value -shr 16) -band 0xFF)
    $Bytes[$Offset + 3] = [byte](($Value -shr 24) -band 0xFF)
}

function Set-U32BE([byte[]]$Bytes, [int]$Offset, [uint32]$Value) {
    $Bytes[$Offset + 0] = [byte](($Value -shr 24) -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 16) -band 0xFF)
    $Bytes[$Offset + 2] = [byte](($Value -shr 8) -band 0xFF)
    $Bytes[$Offset + 3] = [byte]($Value -band 0xFF)
}

function Align-Up([int64]$Value, [int64]$Alignment) {
    return [int64]([Math]::Floor([double]($Value + $Alignment - 1) / [double]$Alignment) * [double]$Alignment)
}

function Read-UserSector([System.IO.FileStream]$Stream, $Layout, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
    [void]$Stream.Read($buffer, 0, 2048)
    return ,$buffer
}

function Write-UserSector([System.IO.FileStream]$Stream, $Layout, [int]$Lba, [byte[]]$Bytes) {
    if ($Bytes.Length -ne 2048) { throw "User-sector writes require exactly 2048 bytes." }
    $Stream.Position = ([int64]$Lba * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
    $Stream.Write($Bytes, 0, 2048)
}

function Test-Pvd([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    if ($Stream.Length -lt ((16 * $SectorSize) + $UserOffset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = (16L * [int64]$SectorSize) + [int64]$UserOffset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [System.Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    $root = New-Object byte[] 34
    [Array]::Copy($buffer, 156, $root, 0, 34)
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        rootExtent = [int](Get-U32LE $root 2)
        rootLength = [int](Get-U32LE $root 10)
    }
}

function Detect-Layout([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-Pvd $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    throw "Could not find an ISO9660 primary volume descriptor."
}

function Find-RootFileRecord([System.IO.FileStream]$Stream, $Layout, [string]$NameRegex) {
    $data = New-Object byte[] $Layout.rootLength
    $remaining = $data.Length
    $written = 0
    $sector = [int]$Layout.rootExtent
    while ($remaining -gt 0) {
        $sectorBytes = Read-UserSector $Stream $Layout $sector
        $toCopy = [Math]::Min(2048, $remaining)
        [Array]::Copy($sectorBytes, 0, $data, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $sector++
    }

    $offset = 0
    while ($offset -lt $data.Length) {
        $recordLength = [int]$data[$offset]
        if ($recordLength -eq 0) {
            $offset = ([Math]::Floor($offset / 2048) + 1) * 2048
            continue
        }
        if (($offset + $recordLength) -gt $data.Length -or $recordLength -lt 34) { break }
        $nameLength = [int]$data[$offset + 32]
        $name = [System.Text.Encoding]::ASCII.GetString($data, ($offset + 33), $nameLength)
        $cleanName = ($name -replace ';1$', '')
        if ($cleanName -match $NameRegex) {
            return [ordered]@{
                name = $cleanName
                rootRecordOffset = $offset
                lba = [int](Get-U32LE $data ($offset + 2))
                size = [int](Get-U32LE $data ($offset + 10))
            }
        }
        $offset += $recordLength
    }
    throw "Could not find root file record matching $NameRegex."
}

function Write-FileBytes([System.IO.FileStream]$Stream, $Layout, [int]$FileLba, [int64]$FileOffset, [byte[]]$Bytes) {
    $remaining = $Bytes.Length
    $written = 0
    $absolute = $FileOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($FileLba + [Math]::Floor([double]($absolute / 2048)))
        $toWrite = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + [int64]$sectorOffset
        $Stream.Write($Bytes, $written, $toWrite)
        $written += $toWrite
        $remaining -= $toWrite
        $absolute += $toWrite
    }
}

function Read-FileBytes([System.IO.FileStream]$Stream, $Layout, [int]$FileLba, [int64]$FileOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $FileOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($FileLba + [Math]::Floor([double]($absolute / 2048)))
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + [int64]$sectorOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return ,$result
}

$Reg = @{
    zero = 0; at = 1; v0 = 2; v1 = 3; a0 = 4; a1 = 5; a2 = 6; a3 = 7
    t0 = 8; t1 = 9; t2 = 10; t3 = 11; t4 = 12; t5 = 13; t6 = 14; t7 = 15
    s0 = 16; s1 = 17; s2 = 18; s3 = 19; s4 = 20; s5 = 21; s6 = 22; s7 = 23
    t8 = 24; t9 = 25; k0 = 26; k1 = 27; gp = 28; sp = 29; fp = 30; ra = 31
}
$Words = New-Object System.Collections.ArrayList
$Labels = @{}
$Fixups = New-Object System.Collections.ArrayList

function Emit-RType([string]$Rs, [string]$Rt, [string]$Rd, [int]$Sh, [int]$Fn) {
    $word = ([uint64]$Reg[$Rs] -shl 21) -bor ([uint64]$Reg[$Rt] -shl 16) -bor ([uint64]$Reg[$Rd] -shl 11) -bor ([uint64]($Sh -band 0x1F) -shl 6) -bor [uint64]($Fn -band 0x3F)
    [void]$Words.Add([uint32]($word -band 0xFFFFFFFFL))
}

function Emit-IType([int]$Op, [string]$Rs, [string]$Rt, [int64]$Imm) {
    $word = ([uint64]$Op -shl 26) -bor ([uint64]$Reg[$Rs] -shl 21) -bor ([uint64]$Reg[$Rt] -shl 16) -bor [uint64]($Imm -band 0xFFFF)
    [void]$Words.Add([uint32]($word -band 0xFFFFFFFFL))
}

function JAbs([int]$Op, [uint32]$Address) {
    $word = ([uint64]$Op -shl 26) -bor ([uint64](($Address -shr 2) -band 0x03FFFFFF))
    [void]$Words.Add([uint32]($word -band 0xFFFFFFFFL))
}

function New-JalWord([uint32]$Address) {
    $word = ([uint64]3 -shl 26) -bor ([uint64](($Address -shr 2) -band 0x03FFFFFF))
    return [uint32]($word -band 0xFFFFFFFFL)
}

function New-JumpWord([int]$Op, [uint32]$Address) {
    $word = ([uint64]$Op -shl 26) -bor ([uint64](($Address -shr 2) -band 0x03FFFFFF))
    return [uint32]($word -band 0xFFFFFFFFL)
}

function New-ITypeWord([int]$Op, [int]$Rs, [int]$Rt, [int64]$Imm) {
    $word = ([uint64]$Op -shl 26) -bor ([uint64]$Rs -shl 21) -bor ([uint64]$Rt -shl 16) -bor [uint64]($Imm -band 0xFFFF)
    return [uint32]($word -band 0xFFFFFFFFL)
}

function New-RTypeWord([int]$Rs, [int]$Rt, [int]$Rd, [int]$Sh, [int]$Fn) {
    $word = ([uint64]$Rs -shl 21) -bor ([uint64]$Rt -shl 16) -bor ([uint64]$Rd -shl 11) -bor ([uint64]($Sh -band 0x1F) -shl 6) -bor [uint64]($Fn -band 0x3F)
    return [uint32]($word -band 0xFFFFFFFFL)
}

function Add-LuiOriAt([System.Collections.ArrayList]$OutWords, [uint32]$Value) {
    [void]$OutWords.Add((New-ITypeWord 15 0 1 (($Value -shr 16) -band 0xFFFF)))
    [void]$OutWords.Add((New-ITypeWord 13 1 1 ($Value -band 0xFFFF)))
}

function New-EntryGateBytes([uint32]$Scratch, [uint32]$CurrentLevelPointerAddress, [uint32]$ExpectedLevelPointer, [uint32]$ControllerRow, [uint32]$ShellRow, [uint32]$PayloadTarget, [uint32]$OriginalTarget, [bool]$OriginalOnly, [bool]$CountOnly, [int]$DelayFrames) {
    $entryWords = New-Object System.Collections.ArrayList
    $branchFixups = New-Object System.Collections.ArrayList
    $callOriginalLabel = -1
    $resetOriginalLabel = -1
    $delayOriginalFixup = -1
    $initDelayFixup = -1
    $callOriginalBranchFixups = New-Object System.Collections.ArrayList
    function Add-BneToCallOriginal([System.Collections.ArrayList]$Words, [System.Collections.ArrayList]$Fixups, [bool]$ResetLatch) {
        [void]$Fixups.Add([ordered]@{ index = $Words.Count; resetLatch = $ResetLatch })
        [void]$Words.Add([uint32]0)
    }

    Add-LuiOriAt $entryWords $Scratch
    [void]$entryWords.Add((New-ITypeWord 43 1 31 0)) # sw ra,0(at)
    [void]$entryWords.Add((New-ITypeWord 43 1 2 4))  # sw v0,4(at)

    Add-LuiOriAt $entryWords $CurrentLevelPointerAddress
    [void]$entryWords.Add((New-ITypeWord 35 1 2 0))  # lw v0,0(at)
    [void]$entryWords.Add([uint32]0)
    Add-LuiOriAt $entryWords $ExpectedLevelPointer
    Add-BneToCallOriginal $entryWords $branchFixups $true # bne v0,at,reset_and_call_original
    [void]$entryWords.Add([uint32]0)                  # delay-slot nop

    Add-LuiOriAt $entryWords $ControllerRow
    [void]$entryWords.Add((New-ITypeWord 37 1 2 0x36)) # lhu v0,0x36(at)
    [void]$entryWords.Add([uint32]0)
    [void]$entryWords.Add((New-ITypeWord 13 0 1 0x01FE)) # ori at,zero,0x01FE
    Add-BneToCallOriginal $entryWords $branchFixups $false
    [void]$entryWords.Add([uint32]0)

    Add-LuiOriAt $entryWords $ShellRow
    [void]$entryWords.Add((New-ITypeWord 37 1 2 0x36)) # lhu v0,0x36(at)
    [void]$entryWords.Add([uint32]0)
    [void]$entryWords.Add((New-ITypeWord 13 0 1 0x0149)) # ori at,zero,0x0149
    Add-BneToCallOriginal $entryWords $branchFixups $false
    [void]$entryWords.Add([uint32]0)

    if ((-not $OriginalOnly -or $CountOnly) -and $DelayFrames -gt 0) {
        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 35 1 2 0x0C)) # lw v0,0x0C(at)
        [void]$entryWords.Add([uint32]0)
        Add-LuiOriAt $entryWords ([uint32]0x53505247)
        $initDelayFixup = $entryWords.Count
        [void]$entryWords.Add([uint32]0)                  # bne v0,at,init_delay
        [void]$entryWords.Add([uint32]0)                  # nop

        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 35 1 2 8)) # lw v0,8(at)
        [void]$entryWords.Add([uint32]0)
        Add-LuiOriAt $entryWords ([uint32]$DelayFrames)
        [void]$entryWords.Add((New-RTypeWord 2 1 2 0 43)) # sltu v0,v0,at
        $delayOriginalFixup = $entryWords.Count
        [void]$entryWords.Add([uint32]0)                  # bne v0,zero,delay_call_original
        [void]$entryWords.Add([uint32]0)                  # nop
    }

    if (-not $OriginalOnly -and -not $CountOnly) {
        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 35 1 2 4))  # lw v0,4(at)
        [void]$entryWords.Add([uint32]0)
        [void]$entryWords.Add((New-JumpWord 2 $PayloadTarget))
        [void]$entryWords.Add([uint32]0)                  # nop
    }

    if (-not $OriginalOnly -and -not $CountOnly) {
        $resetOriginalLabel = $entryWords.Count
        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 43 1 0 8))  # sw zero,8(at)
        [void]$entryWords.Add((New-ITypeWord 43 1 0 0x0C)) # sw zero,0x0C(at)
        [void]$callOriginalBranchFixups.Add($entryWords.Count)
        [void]$entryWords.Add([uint32]0)                  # beq zero,zero,call_original
        [void]$entryWords.Add([uint32]0)                  # nop
    }

    if ((-not $OriginalOnly -or $CountOnly) -and $DelayFrames -gt 0) {
        $initDelayLabel = $entryWords.Count
        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 43 1 0 8))  # sw zero,8(at)
        [void]$entryWords.Add((New-ITypeWord 15 0 2 0x5350)) # lui v0,0x5350
        [void]$entryWords.Add((New-ITypeWord 13 2 2 0x5247)) # ori v0,v0,0x5247
        [void]$entryWords.Add((New-ITypeWord 43 1 2 0x0C)) # sw v0,0x0C(at)
        if ($initDelayFixup -ge 0) {
            $rel = $initDelayLabel - ($initDelayFixup + 1)
            if ($rel -lt -32768 -or $rel -gt 32767) { throw "Entry-gate init branch out of range." }
            $entryWords[$initDelayFixup] = New-ITypeWord 5 2 1 $rel
        }

        $delayOriginalLabel = $entryWords.Count
        Add-LuiOriAt $entryWords $Scratch
        [void]$entryWords.Add((New-ITypeWord 35 1 2 8))  # lw v0,8(at)
        [void]$entryWords.Add([uint32]0)
        [void]$entryWords.Add((New-ITypeWord 9 2 2 1))   # addiu v0,v0,1
        [void]$entryWords.Add((New-ITypeWord 43 1 2 8))  # sw v0,8(at)
        if ($delayOriginalFixup -ge 0) {
            $rel = $delayOriginalLabel - ($delayOriginalFixup + 1)
            if ($rel -lt -32768 -or $rel -gt 32767) { throw "Entry-gate delay branch out of range." }
            $entryWords[$delayOriginalFixup] = New-ITypeWord 5 2 0 $rel
        }
    }

    $callOriginalLabel = $entryWords.Count
    Add-LuiOriAt $entryWords $Scratch
    [void]$entryWords.Add((New-ITypeWord 35 1 2 4))  # lw v0,4(at)
    [void]$entryWords.Add([uint32]0)
    [void]$entryWords.Add((New-JalWord $OriginalTarget))
    [void]$entryWords.Add([uint32]0)                  # nop
    Add-LuiOriAt $entryWords $Scratch
    [void]$entryWords.Add((New-ITypeWord 35 1 31 0)) # lw ra,0(at)
    [void]$entryWords.Add([uint32]0)
    [void]$entryWords.Add((New-RTypeWord 31 0 0 0 8)) # jr ra
    [void]$entryWords.Add([uint32]0)                  # nop

    foreach ($branchFixup in @($branchFixups.ToArray())) {
        $branchIndex = [int]$branchFixup.index
        $shouldReset = [bool]$branchFixup.resetLatch
        $targetLabel = if ($shouldReset -and -not $OriginalOnly -and -not $CountOnly -and $resetOriginalLabel -ge 0) { $resetOriginalLabel } else { $callOriginalLabel }
        $rel = $targetLabel - ($branchIndex + 1)
        if ($rel -lt -32768 -or $rel -gt 32767) { throw "Entry-gate branch out of range." }
        $entryWords[$branchIndex] = New-ITypeWord 5 2 1 $rel
    }
    foreach ($branchIndex in @($callOriginalBranchFixups.ToArray())) {
        $rel = $callOriginalLabel - ([int]$branchIndex + 1)
        if ($rel -lt -32768 -or $rel -gt 32767) { throw "Entry-gate call-original branch out of range." }
        $entryWords[[int]$branchIndex] = New-ITypeWord 4 0 0 $rel
    }

    $entryBytes = New-Object byte[] ($entryWords.Count * 4)
    for ($i = 0; $i -lt $entryWords.Count; $i++) {
        Set-U32LE $entryBytes ($i * 4) ([uint32]$entryWords[$i])
    }
    return ,$entryBytes
}

function JLabel([int]$Op, [string]$Label) {
    [void]$Fixups.Add([ordered]@{ kind = "j"; index = $Words.Count; op = $Op; label = $Label })
    [void]$Words.Add([uint32]0)
}

function BLabel([int]$Op, [string]$Rs, [string]$Rt, [string]$Label) {
    [void]$Fixups.Add([ordered]@{ kind = "b"; index = $Words.Count; op = $Op; rs = $Rs; rt = $Rt; label = $Label })
    [void]$Words.Add([uint32]0)
}

function L([string]$Name) { $Labels[$Name] = $Words.Count }
function Nop { [void]$Words.Add([uint32]0) }
function Addiu([string]$Rt, [string]$Rs, [int64]$Imm) { Emit-IType 9 $Rs $Rt $Imm }
function Lui([string]$Rt, [int]$Imm) { Emit-IType 15 "zero" $Rt $Imm }
function Ori([string]$Rt, [string]$Rs, [int64]$Imm) { Emit-IType 13 $Rs $Rt $Imm }
function Lw([string]$Rt, [int]$Offset, [string]$Rs) {
    Emit-IType 35 $Rs $Rt $Offset
    Nop
}
function Sw([string]$Rt, [int]$Offset, [string]$Rs) { Emit-IType 43 $Rs $Rt $Offset }
function Lhu([string]$Rt, [int]$Offset, [string]$Rs) {
    Emit-IType 37 $Rs $Rt $Offset
    Nop
}
function Sh([string]$Rt, [int]$Offset, [string]$Rs) { Emit-IType 41 $Rs $Rt $Offset }
function Sb([string]$Rt, [int]$Offset, [string]$Rs) { Emit-IType 40 $Rs $Rt $Offset }
function Addu([string]$Rd, [string]$Rs, [string]$Rt) { Emit-RType $Rs $Rt $Rd 0 33 }
function OrReg([string]$Rd, [string]$Rs, [string]$Rt) { Emit-RType $Rs $Rt $Rd 0 37 }
function Sltu([string]$Rd, [string]$Rs, [string]$Rt) { Emit-RType $Rs $Rt $Rd 0 43 }
function Jr([string]$Rs) { Emit-RType $Rs "zero" "zero" 0 8 }
function Li([string]$Rt, $Value) {
    $Value = [uint32]([int64]$Value -band 0xFFFFFFFFL)
    $hi = [int](($Value -shr 16) -band 0xFFFF)
    $lo = [int]($Value -band 0xFFFF)
    if ($hi -eq 0) {
        Ori $Rt "zero" $lo
    }
    else {
        Lui $Rt $hi
        if ($lo -ne 0) { Ori $Rt $Rt $lo }
    }
}
function Beq([string]$Rs, [string]$Rt, [string]$Label) { BLabel 4 $Rs $Rt $Label }
function Bne([string]$Rs, [string]$Rt, [string]$Label) { BLabel 5 $Rs $Rt $Label }
function Jal([uint32]$Address) { JAbs 3 $Address }
function JalLabel([string]$Label) { JLabel 3 $Label }
function JmpLabel([string]$Label) { JLabel 2 $Label }

function Resolve-Fixups {
    foreach ($fixup in @($Fixups.ToArray())) {
        if (-not $Labels.ContainsKey($fixup.label)) { throw "Unknown label '$($fixup.label)'." }
        $targetIndex = [int]$Labels[$fixup.label]
        $index = [int]$fixup.index
        if ($fixup.kind -eq "b") {
            $rel = $targetIndex - ($index + 1)
            if ($rel -lt -32768 -or $rel -gt 32767) { throw "Branch to $($fixup.label) is out of range." }
            $word = ([uint64]([int]$fixup.op) -shl 26) -bor ([uint64]$Reg[[string]$fixup.rs] -shl 21) -bor ([uint64]$Reg[[string]$fixup.rt] -shl 16) -bor [uint64]($rel -band 0xFFFF)
            $Words[$index] = [uint32]($word -band 0xFFFFFFFFL)
        }
        elseif ($fixup.kind -eq "j") {
            $address = [uint32]($PayloadAddress + ([uint32]$targetIndex * 4))
            $word = ([uint64]([int]$fixup.op) -shl 26) -bor ([uint64](($address -shr 2) -band 0x03FFFFFF))
            $Words[$index] = [uint32]($word -band 0xFFFFFFFFL)
        }
    }
}

function Convert-WordsToBytes {
    Resolve-Fixups
    $bytes = New-Object byte[] ($Words.Count * 4)
    for ($i = 0; $i -lt $Words.Count; $i++) {
        Set-U32LE $bytes ($i * 4) ([uint32]$Words[$i])
    }
    return ,$bytes
}

if ($RewardGemIdByte -lt 0x53 -or $RewardGemIdByte -gt 0x57) { throw "-RewardGemIdByte must be 0x53..0x57." }
if ($EntryGateDelayFrames -lt 0 -or $EntryGateDelayFrames -gt 0x7FFF) { throw "-EntryGateDelayFrames must be 0..32767." }
if ($SafeHideFrames -lt 0 -or $SafeHideFrames -gt 0x7FFF) { throw "-SafeHideFrames must be 0..32767." }
if ($RewardZOffset -lt -32768 -or $RewardZOffset -gt 32767) { throw "-RewardZOffset must fit a signed 16-bit immediate." }
$smallPayloadMode = [bool]($NoOpOriginalCallOnly -or $GateOnlyOriginalCall -or $StateOnlyProbe -or $ActorCheckOnlyProbe -or $ControllerOnlyProbe -or $ControllerPtrOnlyProbe -or $ControllerSelfPtrOnlyProbe -or $ControllerShellPtrOnlyProbe -or $Controller34OnlyProbe -or $Controller50OnlyProbe -or $ShellOnlyProbe -or $SafeArmProbe -or $SafeMinimalPop -or $SafeMinimalTimedHide -or $ArmOnlyProbe -or $SmallCaveMinimalPop)
if (-not $smallPayloadMode -and $PayloadReserveBytes -lt 0x800) { throw "-PayloadReserveBytes must leave at least 0x800 bytes." }
if ($smallPayloadMode -and $PayloadReserveBytes -lt 0x80) { throw "-PayloadReserveBytes must leave at least 0x80 bytes for small payload modes." }
if (($StateOnlyProbe -or $ActorCheckOnlyProbe -or $ControllerOnlyProbe -or $ControllerPtrOnlyProbe -or $ControllerSelfPtrOnlyProbe -or $ControllerShellPtrOnlyProbe -or $Controller34OnlyProbe -or $Controller50OnlyProbe -or $ShellOnlyProbe -or $SafeArmProbe -or $SafeMinimalPop -or $SafeMinimalTimedHide -or $ArmOnlyProbe) -and ($PayloadStateOffset -lt 0x80 -or $PayloadStateOffset -ge $PayloadReserveBytes)) { throw "-PayloadStateOffset must point inside the payload reserve for this probe mode." }
if (-not $smallPayloadMode) {
    if ($PayloadStateOffset -lt 0x400 -or $PayloadStateOffset -ge $PayloadReserveBytes) { throw "-PayloadStateOffset must point inside the payload reserve after the code area." }
    if ($PayloadSaveOffset -le ($PayloadStateOffset + 0x10) -or $PayloadSaveOffset -ge $PayloadReserveBytes) { throw "-PayloadSaveOffset must point inside the payload reserve after state words." }
}

$controllerAddress = [uint32]([uint64]$LevelPointer + [uint64]($ControllerIndex * $RecordStride))
$shellAddress = [uint32]([uint64]$LevelPointer + [uint64]($ShellIndex * $RecordStride))
$spawnGemAddress = [uint32]([uint64]$LevelPointer + [uint64]($SpawnGemIndex * $RecordStride))
$rewardOrdinal = [uint32]($RewardGemIdByte - 0x52)
$shellArmedWord = [uint32](([uint32]$RewardGemIdByte -shl 24) -bor [uint32]0x00100120)
$shellPoppedWord = [uint32](([uint32]$RewardGemIdByte -shl 24) -bor [uint32]0x00400120)
$stateAddress = [uint32]([uint64]$PayloadAddress + [uint64]$PayloadStateOffset)
$timerAddress = [uint32]([uint64]$stateAddress + 0x04)
$wasListedAddress = [uint32]([uint64]$stateAddress + 0x08)
$saveBaseAddress = if ($ScratchSaveAddress -ne 0) { [uint32]$ScratchSaveAddress } else { [uint32]([uint64]$PayloadAddress + [uint64]$PayloadSaveOffset) }
$saveOrder = @("v0","v1","a0","a1","a2","a3","t0","t1","t2","t3","t4","t5","t6","t7","t8","t9")
$saveBytes = 4 + ($saveOrder.Count * 4)
if ($ScratchSaveAddress -eq 0 -and ($PayloadSaveOffset + $saveBytes) -gt $PayloadReserveBytes) { throw "Saved-register scratch area does not fit inside -PayloadReserveBytes." }

if ($NoOpOriginalCallOnly) {
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($GateOnlyOriginalCall) {
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Sw "v0" 4 "at"
    Li "at" 0x80075828
    Lw "v0" 0 "at"
    Li "at" $LevelPointer
    Bne "v0" "at" "gate_only_call_original"
    Nop
    L "gate_only_call_original"
    Li "at" $saveBaseAddress
    Lw "v0" 4 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($StateOnlyProbe) {
    $probeSave = @("v0","t0","t1","t2")
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Sw $probeSave[$i] (4 + ($i * 4)) "at" }

    Li "t0" 0x80075828
    Lw "t1" 0 "t0"
    Li "t2" $LevelPointer
    Bne "t1" "t2" "state_probe_restore"
    Nop
    Li "t0" $stateAddress
    Lw "t1" 0 "t0"
    Addiu "t1" "t1" 1
    Sw "t1" 0 "t0"

    L "state_probe_restore"
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Lw $probeSave[$i] (4 + ($i * 4)) "at" }
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($ActorCheckOnlyProbe -or $ControllerOnlyProbe -or $ControllerPtrOnlyProbe -or $ControllerSelfPtrOnlyProbe -or $ControllerShellPtrOnlyProbe -or $Controller34OnlyProbe -or $Controller50OnlyProbe -or $ShellOnlyProbe -or $SafeArmProbe) {
    $probeSave = @("v0","t0","t1","t2","t3","t4")
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Sw $probeSave[$i] (4 + ($i * 4)) "at" }

    Li "t0" 0x80075828
    Lw "t1" 0 "t0"
    Li "t2" $LevelPointer
    Bne "t1" "t2" "split_probe_restore"
    Nop
    Li "t0" $controllerAddress
    Lhu "t1" 0x36 "t0"
    Li "t2" ([uint32]$ControllerActorId)
    Bne "t1" "t2" "split_probe_restore"
    Nop
    Li "t3" $shellAddress
    Lhu "t1" 0x36 "t3"
    Li "t2" 0x0149
    Bne "t1" "t2" "split_probe_restore"
    Nop

    if ($ControllerOnlyProbe -or $ControllerPtrOnlyProbe) {
        Li "t1" $LevelPointer
        Sw "t1" 0x04 "t0"
    }
    if ($ControllerSelfPtrOnlyProbe) {
        Li "t1" $controllerAddress
        Sw "t1" 0x04 "t0"
    }
    if ($ControllerShellPtrOnlyProbe) {
        Li "t1" $shellAddress
        Sw "t1" 0x04 "t0"
    }
    if ($ControllerOnlyProbe -or $Controller34OnlyProbe -or $SafeArmProbe) {
        Li "t2" 0x01AC
        Sh "t2" 0x34 "t0"
    }
    if ($ControllerOnlyProbe -or $Controller50OnlyProbe) {
        Li "t2" 0x53100020
        Sw "t2" 0x50 "t0"
    }
    if ($ShellOnlyProbe -or $SafeArmProbe) {
        Li "t1" $controllerAddress
        Sw "t1" 0x04 "t3"
        Li "t2" 0x01AC
        Sh "t2" 0x34 "t3"
        Li "t2" $shellArmedWord
        Sw "t2" 0x50 "t3"
    }

    Li "t4" $stateAddress
    Lw "t1" 0 "t4"
    Addiu "t1" "t1" 1
    Sw "t1" 0 "t4"

    L "split_probe_restore"
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Lw $probeSave[$i] (4 + ($i * 4)) "at" }
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($ArmOnlyProbe) {
    $probeSave = @("v0","t0","t1","t2","t3","t4")
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Sw $probeSave[$i] (4 + ($i * 4)) "at" }

    Li "t0" 0x80075828
    Lw "t1" 0 "t0"
    Li "t2" $LevelPointer
    Bne "t1" "t2" "arm_probe_restore"
    Nop
    Li "t0" $controllerAddress
    Lhu "t1" 0x36 "t0"
    Li "t2" ([uint32]$ControllerActorId)
    Bne "t1" "t2" "arm_probe_restore"
    Nop
    Li "t3" $shellAddress
    Lhu "t1" 0x36 "t3"
    Li "t2" 0x0149
    Bne "t1" "t2" "arm_probe_restore"
    Nop

    Li "t1" $LevelPointer
    Sw "t1" 0x04 "t0"
    Li "t2" 0x01AC
    Sh "t2" 0x34 "t0"
    Li "t2" 0x53100020
    Sw "t2" 0x50 "t0"
    Li "t1" $controllerAddress
    Sw "t1" 0x04 "t3"
    Li "t2" 0x01AC
    Sh "t2" 0x34 "t3"
    Li "t2" $shellArmedWord
    Sw "t2" 0x50 "t3"

    Li "t4" $stateAddress
    Lw "t1" 0 "t4"
    Addiu "t1" "t1" 1
    Sw "t1" 0 "t4"

    L "arm_probe_restore"
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $probeSave.Count; $i++) { Lw $probeSave[$i] (4 + ($i * 4)) "at" }
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($SafeMinimalPop -or $SafeMinimalTimedHide) {
    $minimalSave = @("v0","v1","a0","a1","t0","t1","t2","t3","t4","t5","t6","t7")
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $minimalSave.Count; $i++) { Sw $minimalSave[$i] (4 + ($i * 4)) "at" }

    Li "t0" 0x80075828
    Lw "t1" 0 "t0"
    Li "t2" $LevelPointer
    Bne "t1" "t2" "safe_min_done"
    Nop

    Li "t0" $controllerAddress
    Lhu "t1" 0x36 "t0"
    Li "t2" ([uint32]$ControllerActorId)
    Bne "t1" "t2" "safe_min_done"
    Nop
    Li "t3" $shellAddress
    Lhu "t1" 0x36 "t3"
    Li "t2" 0x0149
    Bne "t1" "t2" "safe_min_done"
    Nop

    Li "t7" $stateAddress
    Lw "t4" 0 "t7"
    Li "t5" 2
    Beq "t4" "t5" "safe_min_done"
    Nop
    Li "t5" 1
    Beq "t4" "t5" "safe_min_wait_collect"
    Nop

    # Dark Hollow cannot tolerate controller +0x04 or +0x50 writes.
    Li "t2" 0x01AC
    Sh "t2" 0x34 "t0"
    Li "t1" $controllerAddress
    Sw "t1" 0x04 "t3"
    Sh "t2" 0x34 "t3"
    Li "t2" $shellArmedWord
    Sw "t2" 0x50 "t3"

    Lw "t4" 0x18 "t0"
    Lw "t5" 0x18 "t3"
    OrReg "t4" "t4" "t5"
    Beq "t4" "zero" "safe_min_done"
    Nop

    Li "t5" 0
    L "safe_min_write_spawn"
    if ($SafeRewardPrivateLink) {
        Li "t6" $GemPrivateAddress
        Sw "zero" 0x00 "t6"
        Sw "zero" 0x04 "t6"
        Sw "zero" 0x08 "t6"
        Li "t1" 0x4600000F
        Sw "t1" 0x0C "t6"
        Li "t1" $rewardOrdinal
        Sw "t1" 0x10 "t6"
        Li "t1" 0xFF
        Sw "t1" 0x14 "t6"
        Sw "zero" 0x18 "t6"
        Sw "zero" 0x1C "t6"
    }
    Li "t6" $spawnGemAddress
    if ($RewardPrivatePointerOverride -ne 0) {
        Li "t1" $RewardPrivatePointerOverride
        Sw "t1" 0x00 "t6"
    }
    if (-not $RewardMoveOnly) {
        Sw "zero" 0x04 "t6"
        Sw "zero" 0x08 "t6"
    }
    Lw "t1" 0x0C "t3"
    Sw "t1" 0x0C "t6"
    Lw "t1" 0x10 "t3"
    Sw "t1" 0x10 "t6"
    Lw "t1" 0x14 "t3"
    Addiu "t1" "t1" $RewardZOffset
    Sw "t1" 0x14 "t6"
    if (-not $RewardMoveOnly) {
        Sw "zero" 0x18 "t6"
        Li "t1" 0xFFFF
        Sh "t1" 0x34 "t6"
        Li "t1" ([uint32]$RewardGemIdByte)
        Sh "t1" 0x36 "t6"
        if ($DarkHollowVisibleRewardFlags) {
            Li "t1" 0x7D
            Sb "t1" 0x3A "t6"
        }
        else {
            Sb "zero" 0x3A "t6"
        }
        Sb "zero" 0x48 "t6"
        Li "t1" 1
        Sb "t1" 0x49 "t6"
        Li "t1" 0x18
        Sb "t1" 0x4A "t6"
        Sb "zero" 0x4B "t6"
        Sb "zero" 0x4C "t6"
        Sb "zero" 0x4D "t6"
        Sb "zero" 0x4E "t6"
        Li "t1" $rewardOrdinal
        Sb "t1" 0x4F "t6"
        if ($RewardVisualTailWordOverride -ne 0) {
            Li "t1" $RewardVisualTailWordOverride
            Sw "t1" 0x44 "t6"
        }
        Li "t1" 0xFF400118
        Sw "t1" 0x50 "t6"
        Li "t1" 0x0000107F
        Sw "t1" 0x54 "t6"
    }
    if ($SafeRewardPrivateLink) {
        Li "t6" $ShellPrivateAddress
        Li "t1" $spawnGemAddress
        Sw "t1" 0x00 "t6"
        Li "t1" 0x46
        Sw "t1" 0x04 "t6"
    }
    if ($RewardActiveListSlotAddress -ne 0) {
        Li "t6" $RewardActiveListSlotAddress
        Li "t1" $spawnGemAddress
        Sw "t1" 0x00 "t6"
        Sw "zero" 0x04 "t6"
    }
    Bne "t5" "zero" "safe_min_done"
    Nop

    Li "t1" 0xFD
    Sb "t1" 0x3A "t3"
    Li "t1" 1
    Sb "t1" 0x3C "t3"
    Sb "t1" 0x3D "t3"
    Sb "t1" 0x3F "t3"
    Sb "t1" 0x48 "t3"
    Li "t1" $shellPoppedWord
    Sw "t1" 0x50 "t3"
    Sw "zero" 0x18 "t3"
    Sw "zero" 0x18 "t0"
    Li "t1" 1
    Sw "t1" 0 "t7"
    if ($SafeMinimalTimedHide) {
        Li "t1" ([uint32]$SafeHideFrames)
        Sw "t1" 4 "t7"
        Li "t1" 1
        Sw "t1" 8 "t7"
    }
    else {
        Sw "zero" 8 "t7"
    }
    JmpLabel "safe_min_done"
    Nop

    L "safe_min_wait_collect"
    if ($SafeMinimalTimedHide) {
        Lw "t4" 4 "t7"
        Beq "t4" "zero" "safe_min_hide_chest"
        Nop
        Addiu "t4" "t4" -1
        Sw "t4" 4 "t7"
        if ($SafeNoRewardRefresh) {
            JmpLabel "safe_min_done"
            Nop
        }
        Li "t6" $spawnGemAddress
        Lhu "t1" 0x36 "t6"
        Li "t2" ([uint32]$RewardGemIdByte)
        Bne "t1" "t2" "safe_min_hide_chest"
        Nop
        Li "t5" 1
        JmpLabel "safe_min_write_spawn"
        Nop
    }
    else {
        Li "t6" $spawnGemAddress
        Li "t1" 0x80072000
        Li "t2" 0x80072400
        L "safe_min_scan_loop"
        Lw "t4" 0 "t1"
        Beq "t4" "t6" "safe_min_gem_listed"
        Nop
        Addiu "t1" "t1" 4
        Sltu "t5" "t1" "t2"
        Bne "t5" "zero" "safe_min_scan_loop"
        Nop
        Li "t5" 1
        Lw "t4" 8 "t7"
        Beq "t4" "zero" "safe_min_write_spawn"
        Nop
    }
    L "safe_min_hide_chest"
    Lw "t1" 0x0C "t3"
    Lw "t2" 0x10 "t3"
    Lui "t4" 0x0020
    Addu "t1" "t1" "t4"
    Addu "t2" "t2" "t4"
    Sw "t1" 0x0C "t3"
    Sw "t2" 0x10 "t3"
    Sw "zero" 0x14 "t3"
    Sw "zero" 0x18 "t3"
    Sw "t1" 0x0C "t0"
    Sw "t2" 0x10 "t0"
    Sw "zero" 0x14 "t0"
    Sw "zero" 0x18 "t0"
    Li "t1" 2
    Sw "t1" 0 "t7"
    JmpLabel "safe_min_done"
    Nop

    L "safe_min_gem_listed"
    Li "t4" 1
    Sw "t4" 8 "t7"

    L "safe_min_done"
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $minimalSave.Count; $i++) { Lw $minimalSave[$i] (4 + ($i * 4)) "at" }
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
elseif ($SmallCaveMinimalPop) {
    $minimalSave = @("v0","v1","a0","a1","t0","t1","t2","t3","t4","t5","t6","t7")
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $minimalSave.Count; $i++) { Sw $minimalSave[$i] (4 + ($i * 4)) "at" }

    Li "t0" 0x80075828
    Lw "t1" 0 "t0"
    Li "t2" $LevelPointer
    Bne "t1" "t2" "minimal_done"
    Nop

    Li "t0" $controllerAddress
    Lhu "t1" 0x36 "t0"
    Li "t2" ([uint32]$ControllerActorId)
    Bne "t1" "t2" "minimal_done"
    Nop
    Li "t3" $shellAddress
    Lhu "t1" 0x36 "t3"
    Li "t2" 0x0149
    Bne "t1" "t2" "minimal_done"
    Nop

    Li "t7" $stateAddress
    Lw "t4" 0 "t7"
    Li "t5" 2
    Beq "t4" "t5" "minimal_done"
    Nop
    Li "t5" 1
    Beq "t4" "t5" "minimal_wait_collect"
    Nop

    # Arm the imported controller/shell rows until the first successful hit.
    Li "t1" $LevelPointer
    Sw "t1" 0x04 "t0"
    Li "t2" 0x01AC
    Sh "t2" 0x34 "t0"
    Li "t2" 0x53100020
    Sw "t2" 0x50 "t0"
    Li "t1" $controllerAddress
    Sw "t1" 0x04 "t3"
    Li "t2" 0x01AC
    Sh "t2" 0x34 "t3"
    Li "t2" $shellArmedWord
    Sw "t2" 0x50 "t3"

    Lw "t4" 0x18 "t0"
    Lw "t5" 0x18 "t3"
    OrReg "t4" "t4" "t5"
    Beq "t4" "zero" "minimal_done"
    Nop

    # Spawn a simple collectible reward gem above the shell.
    Li "t6" $spawnGemAddress
    Sw "zero" 0x04 "t6"
    Sw "zero" 0x08 "t6"
    Lw "t1" 0x0C "t3"
    Sw "t1" 0x0C "t6"
    Lw "t1" 0x10 "t3"
    Sw "t1" 0x10 "t6"
    Lw "t1" 0x14 "t3"
    Addiu "t1" "t1" 0x0558
    Sw "t1" 0x14 "t6"
    Sw "zero" 0x18 "t6"
    Li "t1" 0xFFFF
    Sh "t1" 0x34 "t6"
    Li "t1" ([uint32]$RewardGemIdByte)
    Sh "t1" 0x36 "t6"
    Sb "zero" 0x3A "t6"
    Li "t1" $rewardOrdinal
    Sb "t1" 0x4F "t6"
    Li "t1" 0xFF400118
    Sw "t1" 0x50 "t6"

    # Put the shell in the popped visual state and clear hit bits.
    Li "t1" 0xFD
    Sb "t1" 0x3A "t3"
    Li "t1" 1
    Sb "t1" 0x3C "t3"
    Sb "t1" 0x3D "t3"
    Sb "t1" 0x3F "t3"
    Sb "t1" 0x48 "t3"
    Li "t1" $shellPoppedWord
    Sw "t1" 0x50 "t3"
    Sw "zero" 0x18 "t3"
    Sw "zero" 0x18 "t0"
    Li "t1" 1
    Sw "t1" 0 "t7"
    Sw "zero" 8 "t7"
    JmpLabel "minimal_done"
    Nop

    L "minimal_wait_collect"
    Li "t6" $spawnGemAddress
    Li "t1" 0x80072000
    Li "t2" 0x80072400
    L "minimal_scan_loop"
    Lw "t4" 0 "t1"
    Beq "t4" "t6" "minimal_gem_listed"
    Nop
    Addiu "t1" "t1" 4
    Sltu "t5" "t1" "t2"
    Bne "t5" "zero" "minimal_scan_loop"
    Nop
    Lw "t4" 8 "t7"
    Beq "t4" "zero" "minimal_done"
    Nop
    # The gem was active earlier and is now gone, so hide the chest pair.
    Lw "t1" 0x0C "t3"
    Lw "t2" 0x10 "t3"
    Lui "t4" 0x0020
    Addu "t1" "t1" "t4"
    Addu "t2" "t2" "t4"
    Sw "t1" 0x0C "t3"
    Sw "t2" 0x10 "t3"
    Sw "zero" 0x14 "t3"
    Sw "zero" 0x18 "t3"
    Sw "t1" 0x0C "t0"
    Sw "t2" 0x10 "t0"
    Sw "zero" 0x14 "t0"
    Sw "zero" 0x18 "t0"
    Li "t1" 2
    Sw "t1" 0 "t7"
    JmpLabel "minimal_done"
    Nop

    L "minimal_gem_listed"
    Li "t4" 1
    Sw "t4" 8 "t7"

    L "minimal_done"
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $minimalSave.Count; $i++) { Lw $minimalSave[$i] (4 + ($i * 4)) "at" }
    Lw "ra" 0 "at"
    Jr "ra"
    Nop
}
else {
    # Do not touch helper state during title, logos, or other levels.
    # Use the same call-original-and-return shape as the passing no-op hook.
    Li "at" $saveBaseAddress
    Sw "ra" 0 "at"
    Sw "v0" 4 "at"
    Li "at" 0x80075828
    Lw "v0" 0 "at"
    Li "at" $LevelPointer
    Bne "v0" "at" "call_original_only"
    Nop
    Li "at" $saveBaseAddress
    Lw "v0" 4 "at"

    # Preserve original call semantics: save external RA, call the original frame function,
    # then save the post-call register state before running our helper logic.
    Jal $OriginalHookCallTarget
    Nop
    Li "at" $saveBaseAddress
    for ($i = 0; $i -lt $saveOrder.Count; $i++) { Sw $saveOrder[$i] (4 + ($i * 4)) "at" }

# Main spring helper state machine.
Li "t7" $stateAddress
Li "t0" 0x80075828
Lw "t1" 0 "t0"
Li "t2" $LevelPointer
Bne "t1" "t2" "reset_state"
Nop
Li "t3" $controllerAddress
Lhu "t4" 0x36 "t3"
Li "t5" ([uint32]$ControllerActorId)
Bne "t4" "t5" "reset_state"
Nop
Li "t6" $shellAddress
Lhu "t4" 0x36 "t6"
Li "t5" 0x00000149
Bne "t4" "t5" "reset_state"
Nop
Lw "t8" 0 "t7"
Beq "t8" "zero" "state_init"
Nop
Li "t9" 1
Beq "t8" "t9" "state_armed"
Nop
Li "t9" 2
Beq "t8" "t9" "state_popped"
Nop
Li "t9" 3
Beq "t8" "t9" "state_wait_collect"
Nop
JmpLabel "done_logic"
Nop

L "reset_state"
Sw "zero" 0 "t7"
Sw "zero" 4 "t7"
Sw "zero" 8 "t7"
JmpLabel "done_logic"
Nop

L "state_init"
JalLabel "write_pre_state"
Nop
Li "t7" $stateAddress
Li "t0" 1
Sw "t0" 0 "t7"
Sw "zero" 4 "t7"
Sw "zero" 8 "t7"
JmpLabel "done_logic"
Nop

L "state_armed"
# Keep structural pointers/flags fresh, but do not clear hit fields while armed.
Li "t0" $controllerAddress
Li "t1" $LevelPointer
Sw "t1" 0x04 "t0"
Li "t2" 0x01AC
Sh "t2" 0x34 "t0"
Li "t2" 0x53100020
Sw "t2" 0x50 "t0"
Li "t3" $shellAddress
Li "t4" $controllerAddress
Sw "t4" 0x04 "t3"
Li "t2" 0x01AC
Sh "t2" 0x34 "t3"
Li "t2" $shellArmedWord
Sw "t2" 0x50 "t3"
Lw "t0" 0x18 "t3"
Li "t1" $controllerAddress
Lw "t2" 0x18 "t1"
OrReg "t0" "t0" "t2"
Beq "t0" "zero" "done_logic"
Nop
JalLabel "write_pop_state"
Nop
Li "t7" $stateAddress
Li "t0" 2
Sw "t0" 0 "t7"
Sw "zero" 4 "t7"
Sw "zero" 8 "t7"
JmpLabel "done_logic"
Nop

L "state_popped"
Li "t7" $stateAddress
Lw "t0" 4 "t7"
Addiu "t0" "t0" 1
Sw "t0" 4 "t7"
JalLabel "scan_active_gem"
Nop
Bne "v0" "zero" "mark_listed_popped"
Nop
Lw "t1" 8 "t7"
Beq "t1" "zero" "check_lid_reset"
Nop
Li "t1" 20
Sltu "t2" "t0" "t1"
Bne "t2" "zero" "check_lid_reset"
Nop
JalLabel "hide_chest"
Nop
Li "t7" $stateAddress
Li "t0" 4
Sw "t0" 0 "t7"
JmpLabel "done_logic"
Nop

L "mark_listed_popped"
Li "t1" 1
Sw "t1" 8 "t7"

L "check_lid_reset"
Li "t1" 45
Sltu "t2" "t0" "t1"
Bne "t2" "zero" "done_logic"
Nop
JalLabel "write_pre_state"
Nop
Li "t7" $stateAddress
Li "t0" 3
Sw "t0" 0 "t7"
JmpLabel "done_logic"
Nop

L "state_wait_collect"
Li "t7" $stateAddress
Lw "t0" 4 "t7"
Addiu "t0" "t0" 1
Sw "t0" 4 "t7"
JalLabel "scan_active_gem"
Nop
Bne "v0" "zero" "mark_listed_wait"
Nop
Lw "t1" 8 "t7"
Beq "t1" "zero" "done_logic"
Nop
JalLabel "hide_chest"
Nop
Li "t7" $stateAddress
Li "t0" 4
Sw "t0" 0 "t7"
JmpLabel "done_logic"
Nop

L "mark_listed_wait"
Li "t1" 1
Sw "t1" 8 "t7"
JmpLabel "done_logic"
Nop

L "done_logic"
Li "at" $saveBaseAddress
for ($i = 0; $i -lt $saveOrder.Count; $i++) { Lw $saveOrder[$i] (4 + ($i * 4)) "at" }
Lw "ra" 0 "at"
Jr "ra"
Nop

L "write_pre_state"
Li "t0" $controllerAddress
Li "t1" $LevelPointer
Sw "t1" 0x04 "t0"
Li "t2" 0x01AC
Sh "t2" 0x34 "t0"
Li "t2" 0x53100020
Sw "t2" 0x50 "t0"
Sw "zero" 0x18 "t0"
Li "t0" $shellAddress
Li "t1" $controllerAddress
Sw "t1" 0x04 "t0"
Li "t2" 0x01AC
Sh "t2" 0x34 "t0"
Li "t2" 0x7D
Sb "t2" 0x3A "t0"
Sb "zero" 0x3C "t0"
Sb "zero" 0x3D "t0"
Sb "zero" 0x3F "t0"
Sb "zero" 0x48 "t0"
Li "t2" $shellArmedWord
Sw "t2" 0x50 "t0"
Sw "zero" 0x18 "t0"
Li "t0" $ShellPrivateAddress
Sw "zero" 0x00 "t0"
Li "t1" 2
Sw "t1" 0x04 "t0"
Jr "ra"
Nop

L "write_pop_state"
Li "t0" $GemPrivateAddress
Sw "zero" 0x00 "t0"
Sw "zero" 0x04 "t0"
Sw "zero" 0x08 "t0"
Li "t1" 0x4600000F
Sw "t1" 0x0C "t0"
Li "t1" $rewardOrdinal
Sw "t1" 0x10 "t0"
Li "t1" 0xFF
Sw "t1" 0x14 "t0"
Sw "zero" 0x18 "t0"
Sw "zero" 0x1C "t0"
Li "t3" $shellAddress
Lw "t4" 0x0C "t3"
Lw "t5" 0x10 "t3"
Lw "t6" 0x14 "t3"
Li "t0" $spawnGemAddress
Sw "zero" 0x04 "t0"
Sw "zero" 0x08 "t0"
Sw "t4" 0x0C "t0"
Sw "t5" 0x10 "t0"
Addiu "t6" "t6" 0x0558
Sw "t6" 0x14 "t0"
Sw "zero" 0x18 "t0"
Li "t1" 0xFFFF
Sh "t1" 0x34 "t0"
Li "t1" ([uint32]$RewardGemIdByte)
Sh "t1" 0x36 "t0"
Sb "zero" 0x3A "t0"
Li "t1" $rewardOrdinal
Sb "t1" 0x4F "t0"
Li "t1" 0xFF400118
Sw "t1" 0x50 "t0"
Li "t0" $ShellPrivateAddress
Li "t1" $spawnGemAddress
Sw "t1" 0x00 "t0"
Li "t1" 0x46
Sw "t1" 0x04 "t0"
Li "t0" $shellAddress
Li "t1" 0xFD
Sb "t1" 0x3A "t0"
Li "t1" 1
Sb "t1" 0x3C "t0"
Sb "t1" 0x3D "t0"
Sb "t1" 0x3F "t0"
Sb "t1" 0x48 "t0"
Li "t1" $shellPoppedWord
Sw "t1" 0x50 "t0"
Sw "zero" 0x18 "t0"
Li "t0" $controllerAddress
Sw "zero" 0x18 "t0"
Jr "ra"
Nop

L "scan_active_gem"
Li "t0" 0x80072000
Li "t1" 0x80072400
Li "t2" $spawnGemAddress
L "scan_loop"
Lw "t3" 0 "t0"
Beq "t3" "t2" "scan_found"
Nop
Addiu "t0" "t0" 4
Sltu "t4" "t0" "t1"
Bne "t4" "zero" "scan_loop"
Nop
Addu "v0" "zero" "zero"
Jr "ra"
Nop
L "scan_found"
Li "v0" 1
Jr "ra"
Nop

L "hide_chest"
Li "t0" $shellAddress
Lw "t1" 0x0C "t0"
Lw "t2" 0x10 "t0"
Lui "t3" 0x0020
Addu "t1" "t1" "t3"
Addu "t2" "t2" "t3"
Sw "t1" 0x0C "t0"
Sw "t2" 0x10 "t0"
Sw "zero" 0x14 "t0"
Sw "zero" 0x18 "t0"
Li "t0" $controllerAddress
Sw "t1" 0x0C "t0"
Sw "t2" 0x10 "t0"
Sw "zero" 0x14 "t0"
Sw "zero" 0x18 "t0"
Jr "ra"
Nop

L "call_original_only"
Li "at" $saveBaseAddress
Lw "v0" 4 "at"
Jal $OriginalHookCallTarget
Nop
Li "at" $saveBaseAddress
Lw "ra" 0 "at"
Jr "ra"
Nop
}

$payload = Convert-WordsToBytes
if ($payload.Length -gt $PayloadStateOffset) {
    throw ("Payload is {0} bytes and would overlap state offset 0x{1:X}." -f $payload.Length, $PayloadStateOffset)
}
if ($payload.Length -gt $PayloadReserveBytes) {
    throw ("Payload is {0} bytes, larger than reserve {1}." -f $payload.Length, $PayloadReserveBytes)
}
$payloadBlock = New-Object byte[] $PayloadReserveBytes
[Array]::Copy($payload, 0, $payloadBlock, 0, $payload.Length)
$entry = New-Object byte[] 0
$entryBlock = New-Object byte[] 0
if ($UseEntryGate) {
    if ($EntryReserveBytes -lt 0xC0) { throw "-EntryReserveBytes must leave at least 0xC0 bytes." }
    $entry = New-EntryGateBytes $saveBaseAddress ([Convert]::ToUInt32("80075828", 16)) $LevelPointer $controllerAddress $shellAddress $PayloadAddress $OriginalHookCallTarget ([bool]$EntryGateOriginalOnly) ([bool]$EntryGateCountOnly) $EntryGateDelayFrames
    if ($entry.Length -gt $EntryReserveBytes) {
        throw ("Entry gate is {0} bytes, larger than reserve {1}." -f $entry.Length, $EntryReserveBytes)
    }
    $entryBlock = New-Object byte[] $EntryReserveBytes
    [Array]::Copy($entry, 0, $entryBlock, 0, $entry.Length)
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing image: $resolvedImagePath" }
$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) { $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue") }
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) { $PlanPath = "$resolvedOutPath.ingamepatchplan.json" }
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath

$layout = Detect-Layout $resolvedImagePath
$sourceStream = [System.IO.File]::OpenRead($resolvedImagePath)
try {
    $exe = Find-RootFileRecord $sourceStream $layout '^(SCUS|SCES|SCPS|SLUS|SLES|SLPS)'
}
finally {
    $sourceStream.Dispose()
}

$hookFileOffset = [int64]0x800 + ([int64]$HookAddress - [int64]$ExeDestination)
$entryFileOffset = [int64]0x800 + ([int64]$EntryAddress - [int64]$ExeDestination)
$payloadFileOffset = [int64]0x800 + ([int64]$PayloadAddress - [int64]$ExeDestination)
if ($hookFileOffset -lt 0x800) { throw "Hook address is before the executable load segment." }
if ($UseEntryGate -and ($entryFileOffset -lt 0x800 -or ($entryFileOffset + $entryBlock.Length) -gt [int64]$exe.size)) {
    throw "Entry gate cave must stay inside the original executable body."
}
if ($payloadFileOffset -lt 0x800 -or ($payloadFileOffset + $payloadBlock.Length) -gt [int64]$exe.size) {
    throw "Payload cave must stay inside the original executable body."
}

$expectedOriginalHookWord = New-JalWord $OriginalHookCallTarget
$sourceStream = [System.IO.File]::OpenRead($resolvedImagePath)
try {
    $existingHookBytes = Read-FileBytes $sourceStream $layout ([int]$exe.lba) $hookFileOffset 4
    $existingEntryBytes = if ($UseEntryGate) { Read-FileBytes $sourceStream $layout ([int]$exe.lba) $entryFileOffset $entryBlock.Length } else { New-Object byte[] 0 }
    $existingPayloadBytes = Read-FileBytes $sourceStream $layout ([int]$exe.lba) $payloadFileOffset $payloadBlock.Length
}
finally {
    $sourceStream.Dispose()
}
$originalHookWord = Get-U32LE $existingHookBytes 0
if ($originalHookWord -ne $expectedOriginalHookWord) {
    throw ("Hook site 0x{0:X8} expected jal 0x{1:X8} (0x{2:X8}), got 0x{3:X8}." -f $HookAddress, $OriginalHookCallTarget, $expectedOriginalHookWord, $originalHookWord)
}
$nonZeroPayloadBytes = @($existingPayloadBytes | Where-Object { $_ -ne 0 })
if ($nonZeroPayloadBytes.Count -ne 0) {
    throw ("Payload cave 0x{0:X8}..0x{1:X8} is not blank; refusing to overwrite original executable bytes." -f $PayloadAddress, ([uint32]([uint64]$PayloadAddress + [uint64]$payloadBlock.Length - 1)))
}
$nonZeroEntryBytes = @($existingEntryBytes | Where-Object { $_ -ne 0 })
if ($UseEntryGate -and $nonZeroEntryBytes.Count -ne 0) {
    throw ("Entry cave 0x{0:X8}..0x{1:X8} is not blank; refusing to overwrite original executable bytes." -f $EntryAddress, ([uint32]([uint64]$EntryAddress + [uint64]$entryBlock.Length - 1)))
}

$hookTargetAddress = if ($UseEntryGate) { $EntryAddress } else { $PayloadAddress }
$hookWord = New-JalWord $hookTargetAddress
$hookBytes = New-Object byte[] 4
Set-U32LE $hookBytes 0 $hookWord

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Add-SpyroSpringChestInGamePatch.ps1"
    warning = "Experimental code-cave injected BIN/CUE. Does not extend the EXE or alter startup BSS clearing."
    sourceImage = $resolvedImagePath
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
    executable = [ordered]@{
        path = [string]$exe.name
        lba = [int]$exe.lba
        originalDirectorySize = ("0x{0:X}" -f [int]$exe.size)
        newDirectorySize = $null
        newLoadSize = $null
        payloadPlacement = "existing-executable-code-cave"
        useEntryGate = [bool]$UseEntryGate
        entryGateOriginalOnly = [bool]$EntryGateOriginalOnly
        entryGateCountOnly = [bool]$EntryGateCountOnly
        entryGateDelayFrames = $EntryGateDelayFrames
        entryAddress = $(if ($UseEntryGate) { "0x{0:X8}" -f $EntryAddress } else { $null })
        entryFileOffset = $(if ($UseEntryGate) { "0x{0:X}" -f $entryFileOffset } else { $null })
        entryBytes = $(if ($UseEntryGate) { $entry.Length } else { 0 })
        entryReservedBytes = $(if ($UseEntryGate) { $EntryReserveBytes } else { 0 })
        payloadAddress = ("0x{0:X8}" -f $PayloadAddress)
        payloadFileOffset = ("0x{0:X}" -f $payloadFileOffset)
        payloadBytes = $payload.Length
        payloadReservedBytes = $PayloadReserveBytes
        hookAddress = ("0x{0:X8}" -f $HookAddress)
        hookTargetAddress = ("0x{0:X8}" -f $hookTargetAddress)
        hookOriginalTarget = ("0x{0:X8}" -f $OriginalHookCallTarget)
        hookOriginalWord = ("0x{0:X8}" -f $originalHookWord)
        hookReplacementWord = ("0x{0:X8}" -f $hookWord)
        skipHookPatch = [bool]$SkipHookPatch
        noOpOriginalCallOnly = [bool]$NoOpOriginalCallOnly
    }
    springChest = [ordered]@{
        levelPointer = ("0x{0:X8}" -f $LevelPointer)
        controllerIndex = $ControllerIndex
        controllerAddress = ("0x{0:X8}" -f $controllerAddress)
        controllerActorId = ("0x{0:X4}" -f $ControllerActorId)
        shellIndex = $ShellIndex
        shellAddress = ("0x{0:X8}" -f $shellAddress)
        spawnGemIndex = $SpawnGemIndex
        spawnGemAddress = ("0x{0:X8}" -f $spawnGemAddress)
        rewardActiveListSlotAddress = $(if ($RewardActiveListSlotAddress -eq 0) { $null } else { "0x{0:X8}" -f $RewardActiveListSlotAddress })
        rewardPrivatePointerOverride = $(if ($RewardPrivatePointerOverride -eq 0) { $null } else { "0x{0:X8}" -f $RewardPrivatePointerOverride })
        rewardVisualTailWordOverride = $(if ($RewardVisualTailWordOverride -eq 0) { $null } else { "0x{0:X8}" -f $RewardVisualTailWordOverride })
        rewardGemIdByte = ("0x{0:X2}" -f $RewardGemIdByte)
        rewardOrdinal = $rewardOrdinal
        gemPrivateAddress = ("0x{0:X8}" -f $GemPrivateAddress)
        shellPrivateAddress = ("0x{0:X8}" -f $ShellPrivateAddress)
        stateAddress = ("0x{0:X8}" -f $stateAddress)
        timerAddress = ("0x{0:X8}" -f $timerAddress)
        wasListedAddress = ("0x{0:X8}" -f $wasListedAddress)
        saveBaseAddress = ("0x{0:X8}" -f $saveBaseAddress)
        saveBytes = $saveBytes
    }
    patches = @()
}
$plan.patches += [ordered]@{ kind = "main-loop-jal-hook"; address = ("0x{0:X8}" -f $HookAddress); fileOffset = ("0x{0:X}" -f $hookFileOffset); skipped = [bool]$SkipHookPatch; bytesHex = (($hookBytes | ForEach-Object { $_.ToString("X2") }) -join "") }
if ($UseEntryGate) {
    $plan.patches += [ordered]@{ kind = "entry-gate-code-cave"; address = ("0x{0:X8}" -f $EntryAddress); fileOffset = ("0x{0:X}" -f $entryFileOffset); bytes = $entry.Length; reservedBytes = $EntryReserveBytes }
}
$plan.patches += [ordered]@{ kind = "injected-helper-payload-code-cave"; address = ("0x{0:X8}" -f $PayloadAddress); fileOffset = ("0x{0:X}" -f $payloadFileOffset); bytes = $payload.Length; reservedBytes = $PayloadReserveBytes }
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $stream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        if ($UseEntryGate) {
            Write-FileBytes $stream $layout ([int]$exe.lba) $entryFileOffset $entryBlock
        }
        Write-FileBytes $stream $layout ([int]$exe.lba) $payloadFileOffset $payloadBlock
        if (-not $SkipHookPatch) {
            Write-FileBytes $stream $layout ([int]$exe.lba) $hookFileOffset $hookBytes
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

Write-Host "Wrote in-game spring chest patch plan to $resolvedPlanPath"
if (-not $PlanOnly) {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
    if ($SkipHookPatch) {
        Write-Host ("Injected {0} bytes at 0x{1:X8}; hook 0x{2:X8} left unchanged for cave safety testing." -f $payload.Length, $PayloadAddress, $HookAddress)
    }
    else {
        if ($UseEntryGate) {
            Write-Host ("Injected entry {0} bytes at 0x{1:X8} and helper {2} bytes at 0x{3:X8}; hook 0x{4:X8} now calls the entry gate." -f $entry.Length, $EntryAddress, $payload.Length, $PayloadAddress, $HookAddress)
        }
        else {
            Write-Host ("Injected {0} bytes at 0x{1:X8}; hook 0x{2:X8} now calls the in-game helper." -f $payload.Length, $PayloadAddress, $HookAddress)
        }
    }
}
