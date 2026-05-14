param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$OutPath = ""
)

Set-StrictMode -Version 2.0

$WadLba = 37
$StoneMetadataOffset = 8333312
$StoneMetadataSize = 57344
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt16($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($WadLba + [Math]::Floor($absolute / 2048))
        $sectorBytes = Read-UserSector $Stream $sector
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        [Array]::Copy($sectorBytes, $sectorOffset, $result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Parse-ArchiveHeader([byte[]]$Bytes, [int64]$ArchiveSize) {
    $entries = @()
    $firstDataOffset = [int64](Get-UInt32LE $Bytes 0)
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) { $firstDataOffset = $Bytes.Length }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -le 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
}

function Get-MipsStoresToGlobal([byte[]]$Bytes, [uint64]$LoadAddress, [int[]]$Offsets) {
    $hits = @()
    for ($i = 0; $i -le ($Bytes.Length - 4); $i += 4) {
        $word = [uint32](Get-UInt32LE $Bytes $i)
        $opcode = ($word -shr 26) -band 0x3F
        $rs = ($word -shr 21) -band 0x1F
        $rt = ($word -shr 16) -band 0x1F
        $imm = $word -band 0xFFFF
        if (($opcode -eq 0x2B -or $opcode -eq 0x23) -and ($Offsets -contains $imm)) {
            $hits += [ordered]@{
                fileOffset = $i
                address = ("0x{0:X8}" -f ($LoadAddress + [uint64]$i))
                instruction = ("0x{0:X8}" -f $word)
                op = $(if ($opcode -eq 0x2B) { "sw" } else { "lw" })
                rs = $rs
                rt = $rt
                lowOffset = ("0x{0:X4}" -f $imm)
                context = Get-MipsContext $Bytes $LoadAddress $i
            }
        }
    }
    return $hits
}

function Get-MipsContext([byte[]]$Bytes, [uint64]$LoadAddress, [int]$Offset) {
    $rows = @()
    for ($i = [Math]::Max(0, $Offset - 20); $i -le [Math]::Min($Bytes.Length - 4, $Offset + 20); $i += 4) {
        $rows += ("{0:X8}: {1:X8}" -f ($LoadAddress + [uint64]$i), (Get-UInt32LE $Bytes $i))
    }
    return $rows
}

function Find-AddressConstants([byte[]]$Bytes, [uint64]$LoadAddress) {
    $hits = @()
    $lowerBound = [Convert]::ToUInt64("80070000", 16)
    $upperBound = [Convert]::ToUInt64("80100000", 16)
    for ($i = 0; $i -le ($Bytes.Length - 8); $i += 4) {
        $a = [uint32](Get-UInt32LE $Bytes $i)
        if ([uint64]$a -ge $lowerBound -and [uint64]$a -lt $upperBound) {
            $hits += [ordered]@{
                fileOffset = $i
                address = ("0x{0:X8}" -f ($LoadAddress + [uint64]$i))
                value = ("0x{0:X8}" -f $a)
            }
        }
    }
    return $hits
}

function Find-RecordLikeRuns([byte[]]$Bytes, [int64]$BaseFileOffset) {
    $runs = @()
    foreach ($stride in @(80, 72, 88, 96, 64, 56, 52, 48, 44, 40, 36, 32, 28, 24)) {
        for ($offset = 0; $offset -lt [Math]::Min($Bytes.Length - ($stride * 8), 131072); $offset += 4) {
            $good = 0
            $nonzeroRecords = 0
            for ($r = 0; $r -lt 12; $r++) {
                $ro = $offset + ($r * $stride)
                $w0 = [uint32](Get-UInt32LE $Bytes $ro)
                $w1 = [uint32](Get-UInt32LE $Bytes ($ro + 4))
                $w2 = [uint32](Get-UInt32LE $Bytes ($ro + 8))
                $w3 = [uint32](Get-UInt32LE $Bytes ($ro + 12))
                if (($w0 -bor $w1 -bor $w2 -bor $w3) -ne 0) { $nonzeroRecords++ }
                $shorts = @()
                for ($s = 0; $s -lt [Math]::Min(24, $stride); $s += 2) {
                    $shorts += [int](Get-Int16LE $Bytes ($ro + $s))
                }
                $reasonable = @($shorts | Where-Object { [Math]::Abs($_) -gt 32 -and [Math]::Abs($_) -lt 30000 }).Count
                if ($reasonable -ge 3) { $good++ }
            }
            if ($nonzeroRecords -ge 8 -and $good -ge 9) {
                $samples = @()
                for ($r = 0; $r -lt 4; $r++) {
                    $ro = $offset + ($r * $stride)
                    $samples += ("{0:X}: {1:X8} {2:X8} {3:X8} {4:X8}" -f $ro, (Get-UInt32LE $Bytes $ro), (Get-UInt32LE $Bytes ($ro+4)), (Get-UInt32LE $Bytes ($ro+8)), (Get-UInt32LE $Bytes ($ro+12)))
                }
                $runs += [ordered]@{
                    offset = $offset
                    imageRelativeOffset = $BaseFileOffset + $offset
                    stride = $stride
                    score = $good
                    sample = $samples
                }
                if ($runs.Count -ge 40) { return $runs }
            }
        }
    }
    return $runs
}

function Find-MobyLikeRecords([byte[]]$Bytes, [int64]$BaseFileOffset, [int]$SubfileIndex) {
    $records = @()
    $limit = [Math]::Min($Bytes.Length - 80, 1048576)
    foreach ($baseField in @(0, 4)) {
        for ($offset = 0; $offset -le $limit; $offset += 4) {
            $type = [int]$Bytes[$offset + 0x48]
            $state = [int]$Bytes[$offset + 0x49]
            if ($type -le 0 -or $type -gt 0x7F) { continue }
            if ($state -gt 0x3F) { continue }

            $x = Get-Int32LE $Bytes ($offset + $baseField)
            $y = Get-Int32LE $Bytes ($offset + $baseField + 4)
            $z = Get-Int32LE $Bytes ($offset + $baseField + 8)
            $ax = [Math]::Abs([int64]$x)
            $ay = [Math]::Abs([int64]$y)
            $az = [Math]::Abs([int64]$z)
            if ($ax -lt 64 -and $ay -lt 64 -and $az -lt 64) { continue }
            if ($ax -gt 2000000 -or $ay -gt 2000000 -or $az -gt 2000000) { continue }

            $score = 0
            if ($ax -lt 600000) { $score++ }
            if ($ay -lt 600000) { $score++ }
            if ($az -lt 600000) { $score++ }
            if ($type -lt 0x50) { $score++ }
            if ($state -lt 0x20) { $score++ }

            $records += [ordered]@{
                subfileIndex = $SubfileIndex
                offset = $offset
                imageRelativeOffset = $BaseFileOffset + $offset
                stride = 80
                coordinateFieldOffset = $baseField
                type = $type
                state = $state
                x = $x
                y = $y
                z = $z
                score = $score
                rawFirstWords = @(
                    ("0x{0:X8}" -f (Get-UInt32LE $Bytes $offset)),
                    ("0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 4))),
                    ("0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 8))),
                    ("0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 12)))
                )
            }
            if ($records.Count -ge 250) { return $records }
        }
    }
    return $records
}

$stream = [System.IO.File]::OpenRead($ImagePath)
try {
    $metadata = Read-WadBytes $stream $StoneMetadataOffset $StoneMetadataSize
    $assetHeader = Read-WadBytes $stream $StoneAssetOffset 65536
    $assetSubfiles = Parse-ArchiveHeader $assetHeader $StoneAssetSize
    $subfileSummaries = @()
    foreach ($sub in $assetSubfiles | Select-Object -First 9) {
        $bytes = Read-WadBytes $stream ($StoneAssetOffset + $sub.offset) ([Math]::Min([int]$sub.size, 1048576))
        $subfileSummaries += [ordered]@{
            index = $sub.index
            offset = $sub.offset
            size = $sub.size
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $bytes 0)),
                ("0x{0:X8}" -f (Get-UInt32LE $bytes 4)),
                ("0x{0:X8}" -f (Get-UInt32LE $bytes 8)),
                ("0x{0:X8}" -f (Get-UInt32LE $bytes 12))
            )
            recordRuns = @(Find-RecordLikeRuns $bytes ($StoneAssetOffset + $sub.offset) | Select-Object -First 8)
            mobyLikeRecords = @(Find-MobyLikeRecords $bytes ($StoneAssetOffset + $sub.offset) $sub.index | Sort-Object -Property score -Descending | Select-Object -First 40)
        }
    }
}
finally {
    $stream.Dispose()
}

$levelLoadAddress = [Convert]::ToUInt64("8007B07C", 16)

$result = [ordered]@{
    stoneHill = [ordered]@{
        metadataWadIndex = 9
        metadataOffset = $StoneMetadataOffset
        metadataSize = $StoneMetadataSize
        assetWadIndex = 10
        assetOffset = $StoneAssetOffset
        assetSize = $StoneAssetSize
    }
    metadata = [ordered]@{
        storesOrLoadsToKnownGlobals = Get-MipsStoresToGlobal $metadata $levelLoadAddress @(0x5828, 0x573C, 0x5930)
        addressConstants = @(Find-AddressConstants $metadata $levelLoadAddress | Select-Object -First 100)
    }
    mobyRuntime = [ordered]@{
        pointerGlobalAddress = "0x80075828"
        structStrideBytes = 80
        structStrideHex = "0x50"
        typeOffset = 72
        typeOffsetHex = "0x48"
        stateOffset = 73
        stateOffsetHex = "0x49"
        evidence = "Stone Hill overlay computes index * 0x50 from _ptr_levelMobys, then reads bytes 0x48 and 0x49."
    }
    assetSubfiles = $subfileSummaries
}

$json = $result | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutPath)) {
    $json | Set-Content -LiteralPath $OutPath -Encoding UTF8
}
else {
    $json
}
