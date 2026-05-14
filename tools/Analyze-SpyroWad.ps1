param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [int]$WadLba = 37,

    [int]$WadSize = 110260224,

    [string]$OutPath = ""
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-LevelName([int]$LevelId) {
    $names = @{
        0x0A = "Artisans"
        0x0B = "Stone Hill"
        0x0C = "Dark Hollow"
        0x0D = "Town Square"
        0x0E = "Toasty"
        0x0F = "Sunny Flight"
        0x14 = "Peace Keepers"
        0x15 = "Dry Canyon"
        0x16 = "Cliff Town"
        0x17 = "Ice Cavern"
        0x18 = "Doctor Shemp"
        0x19 = "Night Flight"
        0x1E = "Magic Crafters"
        0x1F = "Alpine Ridge"
        0x20 = "High Caves"
        0x21 = "Wizard Peak"
        0x22 = "Blowhard"
        0x23 = "Crystal Flight"
        0x28 = "Beast Makers"
        0x29 = "Terrace Village"
        0x2A = "Misty Bog"
        0x2B = "Tree Tops"
        0x2C = "Metalhead / Wild Flight"
        0x32 = "Dream Weavers"
        0x33 = "Dark Passage"
        0x34 = "Lofty Castle"
        0x35 = "Haunted Towers"
        0x36 = "Jacques"
        0x37 = "Icy Flight"
        0x3C = "Gnasty's World"
        0x3D = "Gnorc Cove"
        0x3E = "Twilight Harbor"
        0x3F = "Gnasty Gnorc"
        0x40 = "Gnasty's Loot"
    }
    if ($names.ContainsKey($LevelId)) { return $names[$LevelId] }
    return ""
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int]$WadLba, [int64]$Offset, [int]$Length) {
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
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) {
        $firstDataOffset = $Bytes.Length
    }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -lt 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{
            index = [int]($offset / 8)
            offset = $fileOffset
            size = $fileSize
        }
    }
    return $entries
}

function Measure-ZeroRun([byte[]]$Bytes, [int]$Start) {
    $count = 0
    for ($i = $Start; $i -lt $Bytes.Length; $i++) {
        if ($Bytes[$i] -ne 0) { break }
        $count++
    }
    return $count
}

function Guess-EntryKind([int]$Index, [int64]$Size, [byte[]]$Head) {
    $first = Get-UInt32LE $Head 0
    $second = Get-UInt32LE $Head 4
    if ($first -eq 0x800 -and $second -gt $first -and $second -lt $Size) { return "nested-archive" }
    if ($Index -ge 11 -and $Index -le 79 -and ($Index % 2) -eq 1) { return "level-candidate" }
    if ($Index -ge 4 -and $Index -le 7) { return "cutscene-candidate" }
    if ($Index -ge 83 -and $Index -le 102) { return "starring-candidate" }
    if ($Size -eq 524288) { return "vram-sized" }
    $zeroRun = Measure-ZeroRun $Head 0
    if ($zeroRun -gt 128) { return "zero-padded/unknown" }
    return "unknown"
}

function Parse-LevelCandidate([System.IO.FileStream]$Stream, [int]$WadLba, $Entry) {
    $headLength = [Math]::Min([int]$Entry.size, 4096)
    $head = Read-WadBytes $Stream $WadLba $Entry.offset $headLength
    $subEntries = Parse-ArchiveHeader $head $Entry.size
    return [ordered]@{
        subfileCount = @($subEntries).Count
        subfiles = @($subEntries | Select-Object -First 12)
    }
}

function Read-NestedEntryBytes([System.IO.FileStream]$Stream, [int]$WadLba, $ParentEntry, $SubEntry, [int]$MaxBytes) {
    $length = [Math]::Min([int]$SubEntry.size, $MaxBytes)
    return Read-WadBytes $Stream $WadLba ($ParentEntry.offset + $SubEntry.offset) $length
}

function Test-JumpSections([byte[]]$Bytes) {
    $sections = @()
    $offset = 0
    while ($offset -le ($Bytes.Length - 4) -and $sections.Count -lt 80) {
        $size64 = [int64](Get-UInt32LE $Bytes $offset)
        if ($size64 -gt [int]::MaxValue) { break }
        $size = [int]$size64
        if ($size -lt 4 -or $size -gt ($Bytes.Length - $offset)) { break }
        $sections += [ordered]@{
            index = $sections.Count
            offset = $offset
            size = $size
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $Bytes $offset)),
                $(if (($offset + 8) -le $Bytes.Length) { "0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 4)) } else { "" }),
                $(if (($offset + 12) -le $Bytes.Length) { "0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 8)) } else { "" }),
                $(if (($offset + 16) -le $Bytes.Length) { "0x{0:X8}" -f (Get-UInt32LE $Bytes ($offset + 12)) } else { "" })
            )
        }
        $offset += $size
    }
    return [ordered]@{
        sectionCount = @($sections).Count
        parsedBytes = $offset
        sections = $sections
    }
}

function Find-PossibleMobyTables([byte[]]$Bytes) {
    $candidates = @()
    foreach ($stride in @(32, 36, 40, 44, 48, 52, 56, 64)) {
        for ($offset = 0; $offset -lt [Math]::Min($Bytes.Length, 65536); $offset += 4) {
            $score = 0
            $count = 0
            $sample = @()
            for ($i = 0; $i -lt 12; $i++) {
                $recordOffset = $offset + ($i * $stride)
                if (($recordOffset + $stride) -gt $Bytes.Length) { break }
                $words = @(
                    [uint32](Get-UInt32LE $Bytes $recordOffset),
                    [uint32](Get-UInt32LE $Bytes ($recordOffset + 4)),
                    [uint32](Get-UInt32LE $Bytes ($recordOffset + 8)),
                    [uint32](Get-UInt32LE $Bytes ($recordOffset + 12))
                )
                $nonZero = @($words | Where-Object { $_ -ne 0 }).Count
                $hasSmallishShorts = $false
                for ($s = 0; $s -lt [Math]::Min($stride - 2, 24); $s += 2) {
                    $v = [int][BitConverter]::ToInt16($Bytes, $recordOffset + $s)
                    if ([Math]::Abs($v) -lt 32000 -and [Math]::Abs($v) -gt 16) { $hasSmallishShorts = $true; break }
                }
                if ($nonZero -ge 2 -and $hasSmallishShorts) { $score++ }
                $count++
                if ($i -lt 3) {
                    $sample += ("{0:X}: {1:X8} {2:X8} {3:X8} {4:X8}" -f $recordOffset, $words[0], $words[1], $words[2], $words[3])
                }
            }
            if ($count -ge 8 -and $score -ge 7) {
                $candidates += [ordered]@{
                    offset = $offset
                    stride = $stride
                    score = $score
                    sample = $sample
                }
                if ($candidates.Count -ge 80) { return $candidates }
            }
        }
    }
    return $candidates
}

function Analyze-NestedPackage([System.IO.FileStream]$Stream, [int]$WadLba, $Entry) {
    $headLength = [Math]::Min([int]$Entry.size, 65536)
    $head = Read-WadBytes $Stream $WadLba $Entry.offset $headLength
    $subEntries = Parse-ArchiveHeader $head $Entry.size
    $subs = @()
    foreach ($sub in $subEntries) {
        $sample = Read-NestedEntryBytes $Stream $WadLba $Entry $sub 131072
        $jump = Test-JumpSections $sample
        $mobyCandidates = @()
        if ($sub.size -le 262144) {
            $mobyCandidates = Find-PossibleMobyTables $sample
        }
        $subs += [ordered]@{
            index = $sub.index
            offset = $sub.offset
            size = $sub.size
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $sample 0)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 4)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 8)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 12))
            )
            jumpSections = $jump
            mobyCandidates = @($mobyCandidates | Select-Object -First 8)
        }
    }
    return [ordered]@{
        subfileCount = @($subEntries).Count
        subfiles = $subs
    }
}

function Parse-PointerPackage([System.IO.FileStream]$Stream, [int]$WadLba, $Entry) {
    if ($Entry.size -lt 32) { return $null }
    $headLength = [Math]::Min([int]$Entry.size, 1024)
    $head = Read-WadBytes $Stream $WadLba $Entry.offset $headLength
    $levelId = [int](Get-UInt32LE $head 0)
    $levelName = Get-LevelName $levelId
    if ([string]::IsNullOrWhiteSpace($levelName)) { return $null }

    $pointers = @()
    for ($i = 0; $i -lt 64; $i++) {
        if ((4 + ($i * 4) + 4) -gt $head.Length) { break }
        $pointers += [uint32](Get-UInt32LE $head (4 + ($i * 4)))
    }
    $validPointers = @($pointers | Where-Object { ([uint64]$_) -ge 0x80000000L -and ([uint64]$_) -lt 0x80200000L })
    if ($validPointers.Count -eq 0) { return $null }

    $pointerCount = 0
    while ($pointerCount -lt $pointers.Count) {
        $p = [uint64]$pointers[$pointerCount]
        if ($p -lt 0x80000000L -or $p -ge 0x80200000L) { break }
        $pointerCount++
    }
    if ($pointerCount -le 0) { return $null }

    $headerLength = 4 + ($pointerCount * 4) + 8
    $baseAddress = [uint32]($pointers[0] + 8 - $headerLength)
    $rawParts = @()
    for ($i = 0; $i -lt $pointerCount; $i++) {
        $ptr = [uint32]$pointers[$i]
        if ($ptr -lt $baseAddress) { continue }
        $partOffset = [int64]($ptr - $baseAddress + 8)
        if ($partOffset -lt 0 -or $partOffset -ge $Entry.size) { continue }
        $rawParts += [ordered]@{ index = $i; pointer = $ptr; offset = $partOffset }
    }

    $sortedOffsets = @($rawParts | Sort-Object { $_['offset'] } | ForEach-Object { $_['offset'] })
    $parts = @()
    foreach ($raw in $rawParts) {
        $partOffset = [int64]$raw['offset']
        $nextOffset = [int64]$Entry.size
        foreach ($candidate in $sortedOffsets) {
            if ($candidate -gt $partOffset -and $candidate -lt $nextOffset) {
                $nextOffset = $candidate
            }
        }
        $sample = Read-WadBytes $Stream $WadLba ($Entry.offset + $partOffset) ([Math]::Min(32, [int]($Entry.size - $partOffset)))
        $parts += [ordered]@{
            index = $raw['index']
            pointer = ("0x{0:X8}" -f $raw['pointer'])
            offset = $partOffset
            sizeEstimate = ($nextOffset - $partOffset)
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $sample 0)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 4)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 8)),
                ("0x{0:X8}" -f (Get-UInt32LE $sample 12))
            )
        }
    }

    return [ordered]@{
        levelId = $levelId
        levelName = $levelName
        pointerCount = $pointerCount
        baseAddress = ("0x{0:X8}" -f $baseAddress)
        headerLength = $headerLength
        parts = $parts
    }
}

$stream = [System.IO.File]::OpenRead($ImagePath)
try {
    $header = Read-WadBytes $stream $WadLba 0 65536
    $entries = Parse-ArchiveHeader $header $WadSize
    $summaries = @()
    foreach ($entry in $entries) {
        $head = Read-WadBytes $stream $WadLba $entry.offset ([Math]::Min([int]$entry.size, 64))
        $kind = Guess-EntryKind $entry.index $entry.size $head
        $level = $null
        if ($kind -eq "level-candidate" -or $kind -eq "nested-archive") {
            $level = Parse-LevelCandidate $stream $WadLba $entry
        }
        $nestedAnalysis = $null
        if ($kind -eq "nested-archive" -and ($entry.index -eq 10 -or $entry.index -eq 12 -or $entry.index -eq 14)) {
            $nestedAnalysis = Analyze-NestedPackage $stream $WadLba $entry
        }
        $pointerPackage = $null
        if ($kind -eq "level-candidate" -or $kind -eq "unknown") {
            $pointerPackage = Parse-PointerPackage $stream $WadLba $entry
        }
        $summaries += [ordered]@{
            index = $entry.index
            offset = $entry.offset
            size = $entry.size
            kind = $kind
            firstWords = @(
                ("0x{0:X8}" -f (Get-UInt32LE $head 0)),
                ("0x{0:X8}" -f (Get-UInt32LE $head 4)),
                ("0x{0:X8}" -f (Get-UInt32LE $head 8)),
                ("0x{0:X8}" -f (Get-UInt32LE $head 12))
            )
            level = $level
            nestedAnalysis = $nestedAnalysis
            pointerPackage = $pointerPackage
        }
    }
}
finally {
    $stream.Dispose()
}

$result = [ordered]@{
    wad = [ordered]@{
        lba = $WadLba
        size = $WadSize
        entryCount = @($entries).Count
    }
    entries = $summaries
    levels = @($summaries | Where-Object { $null -ne $_['pointerPackage'] } | ForEach-Object {
        $metadataIndex = $_['index']
        $assetEntry = $summaries | Where-Object { $_['index'] -eq ($metadataIndex + 1) } | Select-Object -First 1
        [ordered]@{
            levelId = $_['pointerPackage']['levelId']
            levelName = $_['pointerPackage']['levelName']
            metadataWadIndex = $_['index']
            metadataOffset = $_['offset']
            metadataSize = $_['size']
            assetWadIndex = $(if ($null -ne $assetEntry) { $assetEntry['index'] } else { $null })
            pointerCount = $_['pointerPackage']['pointerCount']
            baseAddress = $_['pointerPackage']['baseAddress']
        }
    })
}

$json = $result | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutPath)) {
    $json | Set-Content -LiteralPath $OutPath -Encoding UTF8
}
else {
    $json
}
