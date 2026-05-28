param(
    [string]$ImagePath = ".\_local\experiments\editor-artisans-springpair-T174-T175-fixed.bin",
    [string]$OutPath = ".\_local\experiments\editor-artisans-springpair-T174-T175-hook-cave-noop.bin",
    [string]$CuePath = "",
    [string]$PlanPath = "",
    [uint32]$HookAddress = ([Convert]::ToUInt32("80012230", 16)),
    [uint32]$CaveAddress = ([Convert]::ToUInt32("8007314C", 16)),
    [uint32]$OriginalCallTarget = ([Convert]::ToUInt32("8003385C", 16)),
    [switch]$UseStackTrampoline,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$ExeDestination = [Convert]::ToUInt32("80010000", 16)

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-U32LE([byte[]]$Bytes, [int]$Offset) { return [BitConverter]::ToUInt32($Bytes, $Offset) }

function Set-U32LE([byte[]]$Bytes, [int]$Offset, [uint32]$Value) {
    $Bytes[$Offset + 0] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
    $Bytes[$Offset + 2] = [byte](($Value -shr 16) -band 0xFF)
    $Bytes[$Offset + 3] = [byte](($Value -shr 24) -band 0xFF)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba, [int]$SectorSize, [int]$UserOffset) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * [int64]$SectorSize) + [int64]$UserOffset
    [void]$Stream.Read($buffer, 0, 2048)
    return ,$buffer
}

function Test-Pvd([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    if ($Stream.Length -lt ((16 * $SectorSize) + $UserOffset + 2048)) { return $null }
    $buffer = Read-UserSector $Stream 16 $SectorSize $UserOffset
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

function Find-BootExe([System.IO.FileStream]$Stream, $Layout) {
    $data = New-Object byte[] $Layout.rootLength
    $Stream.Position = ([int64]$Layout.rootExtent * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
    [void]$Stream.Read($data, 0, $data.Length)
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
        if ($cleanName -match '^(SCUS|SCES|SCPS|SLUS|SLES|SLPS)') {
            return [ordered]@{
                name = $cleanName
                lba = [int](Get-U32LE $data ($offset + 2))
                size = [int](Get-U32LE $data ($offset + 10))
            }
        }
        $offset += $recordLength
    }
    throw "Could not find the boot executable in the root directory."
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

function New-JalWord([uint32]$Address) {
    $word = ([uint64]3 -shl 26) -bor ([uint64](($Address -shr 2) -band 0x03FFFFFF))
    return [uint32]($word -band 0xFFFFFFFFL)
}

function New-ITypeWord([int]$Op, [int]$Rs, [int]$Rt, [uint16]$Imm) {
    $word = ([uint64]$Op -shl 26) -bor ([uint64]$Rs -shl 21) -bor ([uint64]$Rt -shl 16) -bor [uint64]$Imm
    return [uint32]($word -band 0xFFFFFFFFL)
}

function New-TrampolineBytes([uint32]$OriginalTarget, [uint32]$CaveBase, [bool]$UseStack) {
    if ($UseStack) {
        $words = @(
            [Convert]::ToUInt32("27BDFFF8", 16), # addiu sp,sp,-8
            [Convert]::ToUInt32("AFBF0004", 16), # sw ra,4(sp)
            (New-JalWord $OriginalTarget),        # jal original
            [Convert]::ToUInt32("00000000", 16), # nop
            [Convert]::ToUInt32("8FBF0004", 16), # lw ra,4(sp)
            [Convert]::ToUInt32("03E00008", 16), # jr ra
            [Convert]::ToUInt32("27BD0008", 16)  # addiu sp,sp,8 (delay slot)
        )
    }
    else {
        $scratch = [uint32]([uint64]$CaveBase + [uint64]0x20)
        $hi = [uint16]((([uint64]$scratch + [uint64]0x8000) -shr 16) -band 0xFFFF)
        $lo = [uint16]([uint64]$scratch -band 0xFFFF)
        $at = 1
        $ra = 31
        $words = @(
            (New-ITypeWord 15 0 $at $hi),         # lui at,hi(scratch)
            (New-ITypeWord 43 $at $ra $lo),       # sw ra,lo(scratch)(at)
            (New-JalWord $OriginalTarget),        # jal original
            [Convert]::ToUInt32("00000000", 16), # nop
            (New-ITypeWord 15 0 $at $hi),         # lui at,hi(scratch)
            (New-ITypeWord 35 $at $ra $lo),       # lw ra,lo(scratch)(at)
            [Convert]::ToUInt32("03E00008", 16), # jr ra
            [Convert]::ToUInt32("00000000", 16)  # nop
        )
    }

    $bytes = New-Object byte[] ($words.Count * 4)
    for ($i = 0; $i -lt $words.Count; $i++) {
        Set-U32LE $bytes ($i * 4) $words[$i]
    }
    return ,$bytes
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing image: $resolvedImagePath" }
$resolvedOutPath = Resolve-WorkspacePath $OutPath
if ([string]::IsNullOrWhiteSpace($CuePath)) { $CuePath = [System.IO.Path]::ChangeExtension($resolvedOutPath, ".cue") }
$resolvedCuePath = Resolve-WorkspacePath $CuePath
if ([string]::IsNullOrWhiteSpace($PlanPath)) { $PlanPath = "$resolvedOutPath.hookcaveplan.json" }
$resolvedPlanPath = Resolve-WorkspacePath $PlanPath

$layout = Detect-Layout $resolvedImagePath
$stream = [System.IO.File]::OpenRead($resolvedImagePath)
try {
    $exe = Find-BootExe $stream $layout
    $hookFileOffset = [int64]0x800 + ([int64]$HookAddress - [int64]$ExeDestination)
    $caveFileOffset = [int64]0x800 + ([int64]$CaveAddress - [int64]$ExeDestination)
    $trampoline = New-TrampolineBytes $OriginalCallTarget $CaveAddress ([bool]$UseStackTrampoline)
    $existingCaveBytes = Read-FileBytes $stream $layout ([int]$exe.lba) $caveFileOffset $trampoline.Length
    $existingHookBytes = Read-FileBytes $stream $layout ([int]$exe.lba) $hookFileOffset 4
}
finally {
    $stream.Dispose()
}

$nonZeroCaveBytes = @($existingCaveBytes | Where-Object { $_ -ne 0 })
if ($nonZeroCaveBytes.Count -ne 0) {
    throw ("Cave 0x{0:X8} is not blank in {1}; refusing to overwrite it." -f $CaveAddress, $resolvedImagePath)
}
$originalHookWord = Get-U32LE $existingHookBytes 0
if ($originalHookWord -ne (New-JalWord $OriginalCallTarget)) {
    throw ("Hook site 0x{0:X8} expected jal 0x{1:X8} (0x{2:X8}), got 0x{3:X8}." -f $HookAddress, $OriginalCallTarget, (New-JalWord $OriginalCallTarget), $originalHookWord)
}

$hookBytes = New-Object byte[] 4
Set-U32LE $hookBytes 0 (New-JalWord $CaveAddress)

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Add-SpyroNoOpHookCavePatch.ps1"
    warning = "No-op hook safety test. This should preserve behavior and prove the hook/trampoline can run."
    sourceImage = $resolvedImagePath
    outPath = $(if ($PlanOnly) { $null } else { $resolvedOutPath })
    cuePath = $(if ($PlanOnly) { $null } else { $resolvedCuePath })
    executable = [ordered]@{
        path = [string]$exe.name
        lba = [int]$exe.lba
        size = ("0x{0:X}" -f [int]$exe.size)
    }
    hook = [ordered]@{
        address = ("0x{0:X8}" -f $HookAddress)
        fileOffset = ("0x{0:X}" -f $hookFileOffset)
        originalWord = ("0x{0:X8}" -f $originalHookWord)
        replacementWord = ("0x{0:X8}" -f (Get-U32LE $hookBytes 0))
        caveAddress = ("0x{0:X8}" -f $CaveAddress)
        caveFileOffset = ("0x{0:X}" -f $caveFileOffset)
        originalCallTarget = ("0x{0:X8}" -f $OriginalCallTarget)
        trampolineKind = $(if ($UseStackTrampoline) { "stack-ra-save" } else { "cave-scratch-ra-save" })
        trampolineBytes = $trampoline.Length
        scratchAddress = $(if ($UseStackTrampoline) { $null } else { "0x{0:X8}" -f ([uint32]([uint64]$CaveAddress + [uint64]0x20)) })
    }
}
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedPlanPath -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $resolvedImagePath -Destination $resolvedOutPath -Force
    $outStream = [System.IO.File]::Open($resolvedOutPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    try {
        Write-FileBytes $outStream $layout ([int]$exe.lba) $caveFileOffset $trampoline
        Write-FileBytes $outStream $layout ([int]$exe.lba) $hookFileOffset $hookBytes
    }
    finally {
        $outStream.Dispose()
    }

    $cueFileName = [System.IO.Path]::GetFileName($resolvedOutPath)
    @(
        "FILE `"$cueFileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    ) | Set-Content -LiteralPath $resolvedCuePath -Encoding ASCII
}

Write-Host "Wrote no-op hook cave patch plan to $resolvedPlanPath"
if (-not $PlanOnly) {
    Write-Host "Wrote patched BIN to $resolvedOutPath"
    Write-Host "Wrote CUE to $resolvedCuePath"
    Write-Host ("Patched hook 0x{0:X8} -> cave 0x{1:X8}; cave calls original 0x{2:X8} and returns." -f $HookAddress, $CaveAddress, $OriginalCallTarget)
}
