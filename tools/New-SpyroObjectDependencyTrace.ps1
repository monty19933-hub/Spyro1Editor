param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$TemplatePath = ".\spyro-object-templates.json",
    [string]$TargetLevelKey = "Artisans",
    [string]$TargetRuntimePath = "",
    [string[]]$TemplateId = @(),
    [string[]]$Family = @("key", "springChest", "lockedChest"),
    [string]$OutJsonPath = ".\spyro-object-dependency-trace.json",
    [string]$OutMarkdownPath = ".\spyro-object-dependency-trace.md",
    [int]$MaxHits = 64,
    [int]$RuntimeBlockLength = 0x80,
    [int]$MinPatternBytes = 8
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58
$MainRamBase = [Convert]::ToUInt64("80000000", 16)
$MainRamSize = 0x200000
$CoordOffsets = [ordered]@{ x = 0x0C; y = 0x10; z = 0x14 }
$DefaultActorSearchEntries = 83..101
$ActorPointerTableRamOffset = 0x76378

$scannerSource = @"
using System;
using System.Collections.Generic;

public static class SpyroDependencyScanner {
    public static int[] FindPattern(byte[] data, byte[] pattern, int maxHits) {
        List<int> hits = new List<int>();
        if (data == null || pattern == null || pattern.Length == 0 || data.Length < pattern.Length) {
            return hits.ToArray();
        }
        int last = data.Length - pattern.Length;
        byte first = pattern[0];
        for (int i = 0; i <= last; i++) {
            if (data[i] != first) continue;
            bool ok = true;
            for (int j = 1; j < pattern.Length; j++) {
                if (data[i + j] != pattern[j]) { ok = false; break; }
            }
            if (!ok) continue;
            hits.Add(i);
            if (maxHits > 0 && hits.Count >= maxHits) break;
        }
        return hits.ToArray();
    }

    public static int[] FindU32(byte[] data, UInt32 value, bool alignedOnly, int maxHits) {
        List<int> hits = new List<int>();
        if (data == null || data.Length < 4) return hits.ToArray();
        int step = alignedOnly ? 4 : 1;
        for (int i = 0; i <= data.Length - 4; i += step) {
            UInt32 found = BitConverter.ToUInt32(data, i);
            if (found != value) continue;
            hits.Add(i);
            if (maxHits > 0 && hits.Count >= maxHits) break;
        }
        return hits.ToArray();
    }
}
"@

Add-Type -TypeDefinition $scannerSource

function Resolve-WorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Convert-HexOrInt64($Value) {
    if ($null -eq $Value) { return 0L }
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [Convert]::ToInt64($text.Substring(2), 16)
        }
        if ($text.Length -eq 0) { return 0L }
        return [Convert]::ToInt64($text, 10)
    }
    return [Convert]::ToInt64($Value)
}

function Normalize-LevelKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return (($Value.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) } | ForEach-Object { [char]::ToLowerInvariant($_) }) -join "")
}

function Format-HexByte($Value) {
    return "0x{0:X2}" -f ([int]$Value -band 0xFF)
}

function Format-HexOffset($Value) {
    return "0x{0:X}" -f ([int64]$Value)
}

function Format-Hex32($Value) {
    $signed = [int64]$Value
    if ($signed -lt 0) {
        return "0x{0:X8}" -f ([uint32]([uint64]($signed + 4294967296L)))
    }
    return "0x{0:X8}" -f ([uint32]([uint64]$signed -band [Convert]::ToUInt64("FFFFFFFF", 16)))
}

function Convert-BytesToHex([byte[]]$Bytes, [int]$MaxBytes = 0) {
    if ($null -eq $Bytes) { return "" }
    $items = $Bytes
    if ($MaxBytes -gt 0 -and $Bytes.Length -gt $MaxBytes) {
        $items = New-Object byte[] $MaxBytes
        [Array]::Copy($Bytes, 0, $items, 0, $MaxBytes)
    }
    return -join ($items | ForEach-Object { "{0:X2}" -f $_ })
}

function Convert-HexStringToBytes([string]$Hex) {
    if ([string]::IsNullOrWhiteSpace($Hex)) { return New-Object byte[] 0 }
    $clean = $Hex.Trim()
    if (($clean.Length % 2) -ne 0) { throw "Hex string has an odd length." }
    $bytes = New-Object byte[] ($clean.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($clean.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    if ($Offset -lt 0 -or $Length -le 0 -or $Offset -ge $Bytes.Length) { return New-Object byte[] 0 }
    $actual = [Math]::Min($Length, $Bytes.Length - $Offset)
    $copy = New-Object byte[] $actual
    [Array]::Copy($Bytes, $Offset, $copy, 0, $actual)
    return $copy
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

function Get-LevelCatalogEntries {
    $path = Resolve-WorkspacePath ".\spyro-level-catalog.json"
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing level catalog: $path" }
    $root = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    return @($root.levels)
}

function Get-LevelSourceTable([string]$Key) {
    $wanted = Normalize-LevelKey $Key
    foreach ($level in Get-LevelCatalogEntries) {
        $matches = @(
            (Normalize-LevelKey ([string]$level.key)),
            (Normalize-LevelKey ([string]$level.scriptKey)),
            (Normalize-LevelKey ([string]$level.displayName))
        )
        if ($matches -notcontains $wanted) { continue }
        $recordCount = [int]$level.sourceRecordCount
        if ($recordCount -le 0) { throw "Level '$Key' has no mapped source moby table yet." }
        return [ordered]@{
            levelKey = [string]$level.scriptKey
            displayName = [string]$level.displayName
            editSlug = [string]$level.key
            wadEntry = [int]$level.sourceWadEntry
            tableWadOffset = Convert-HexOrInt64 $level.sourceTableWadOffset
            tableRelativeOffset = Convert-HexOrInt64 $level.sourceTableRelativeOffset
            recordCount = $recordCount
            confidence = [string]$level.confidence
        }
    }
    throw "Unsupported level key: $Key"
}

function Test-SourceSpecialDataOffset($Table, [uint64]$Offset) {
    if ($Offset -eq 0) { return $false }
    return ($Offset -lt [uint64]$Table.tableRelativeOffset)
}

function Get-DefaultSpecialDataLengthForType([int]$TypeByte) {
    switch ($TypeByte) {
        0x00 { return 0x28 }
        0x18 { return 0x18 }
        0x20 { return 0x18 }
        default { return 0x18 }
    }
}

function Get-SourceRecord($Stream, $Layout, $Table, [int]$TrueIndex) {
    if ($TrueIndex -lt 0 -or $TrueIndex -ge [int]$Table.recordCount) {
        throw "T$TrueIndex is outside $($Table.displayName)'s source table."
    }
    $recordWadOffset = [int64]$Table.tableWadOffset + ([int64]$TrueIndex * $RecordStride)
    [byte[]]$bytes = Read-WadBytes $Stream $Layout $recordWadOffset $RecordStride
    [int]$rawX = Get-Int32LE $bytes ([int]$CoordOffsets.x)
    [int]$rawY = Get-Int32LE $bytes ([int]$CoordOffsets.y)
    [int]$rawZ = Get-Int32LE $bytes ([int]$CoordOffsets.z)
    [uint64]$specialOffset = [uint64](Get-UInt32LE $bytes 0)
    return [pscustomobject]@{
        trueIndex = $TrueIndex
        recordWadOffsetValue = $recordWadOffset
        recordWadOffset = Format-HexOffset $recordWadOffset
        specialDataOffsetValue = $specialOffset
        specialDataOffset = Format-HexOffset $specialOffset
        hasSourceSpecialData = (Test-SourceSpecialDataOffset $Table $specialOffset)
        rawX = $rawX
        rawY = $rawY
        rawZ = $rawZ
        x = [Math]::Round($rawX / 16.0, 4)
        y = [Math]::Round($rawY / 16.0, 4)
        z = [Math]::Round($rawZ / 16.0, 4)
        sourceByte36 = [int]$bytes[0x36]
        sourceByte37 = [int]$bytes[0x37]
        sourceByte4F = [int]$bytes[0x4F]
        sourceByte36Hex = Format-HexByte $bytes[0x36]
        sourceByte37Hex = Format-HexByte $bytes[0x37]
        sourceByte4FHex = Format-HexByte $bytes[0x4F]
        actorId16 = [int]$bytes[0x36] -bor ([int]$bytes[0x37] -shl 8)
        actorId16Hex = ("0x{0:X4}" -f ([int]$bytes[0x36] -bor ([int]$bytes[0x37] -shl 8)))
        type = [int]$bytes[0x50]
        typeHex = Format-HexByte $bytes[0x50]
        state = [int]$bytes[0x51]
        stateHex = Format-HexByte $bytes[0x51]
        flag4A = [int]$bytes[0x52]
        flag4AHex = Format-HexByte $bytes[0x52]
        flag4B = [int]$bytes[0x53]
        flag4BHex = Format-HexByte $bytes[0x53]
        bytesHex = Convert-BytesToHex $bytes
        bytes = $bytes
    }
}

function Get-SpecialDataMap($Stream, $Layout, $Table) {
    $records = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt [int]$Table.recordCount; $i++) {
        [void]$records.Add((Get-SourceRecord $Stream $Layout $Table $i))
    }

    $uniqueOffsets = @($records | Where-Object { $_.hasSourceSpecialData } | Sort-Object specialDataOffsetValue | Group-Object specialDataOffset | ForEach-Object { $_.Group[0] })
    $byOffset = @{}
    [int64]$wadBaseOffset = [int64]$Table.tableWadOffset - [int64]$Table.tableRelativeOffset
    for ($i = 0; $i -lt $uniqueOffsets.Count; $i++) {
        $record = $uniqueOffsets[$i]
        [uint64]$offset = [uint64]$record.specialDataOffsetValue
        [int]$length = 0
        if (($i + 1) -lt $uniqueOffsets.Count) {
            [uint64]$nextOffset = [uint64]$uniqueOffsets[$i + 1].specialDataOffsetValue
            [uint64]$delta = $nextOffset - $offset
            if ($delta -gt 0 -and $delta -le 0x400) { $length = [int]$delta }
        }
        if ($length -le 0) { $length = Get-DefaultSpecialDataLengthForType ([int]$record.type) }
        [int64]$specialWadOffset = $wadBaseOffset + [int64]$offset
        [byte[]]$specialBytes = Read-WadBytes $Stream $Layout $specialWadOffset $length
        $byOffset[$record.specialDataOffset] = [pscustomobject]@{
            offset = $record.specialDataOffset
            offsetValue = $offset
            wadOffset = Format-HexOffset $specialWadOffset
            wadOffsetValue = $specialWadOffset
            length = $length
            bytesHex = Convert-BytesToHex $specialBytes
            bytes = $specialBytes
            word0 = Get-UInt32LE $specialBytes 0
        }
    }

    return [ordered]@{
        records = @($records.ToArray())
        specialByOffset = $byOffset
    }
}

function Get-WadAnalysisEntries {
    $path = Resolve-WorkspacePath ".\spyro-wad-analysis.json"
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing WAD analysis: $path" }
    return @((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).entries)
}

function Get-WadEntry($Entries, [int]$Index) {
    $entry = @($Entries | Where-Object { [int]$_.index -eq $Index } | Select-Object -First 1)
    if ($entry.Count -eq 0) { return $null }
    return $entry[0]
}

function Read-WadEntryBytes($Stream, $Layout, $Entries, [int]$Index) {
    $entry = Get-WadEntry $Entries $Index
    if ($null -eq $entry) { return $null }
    return [pscustomobject]@{
        index = [int]$entry.index
        offset = [int64]$entry.offset
        size = [int]$entry.size
        bytes = (Read-WadBytes $Stream $Layout ([int64]$entry.offset) ([int]$entry.size))
    }
}

function Find-PatternHits([byte[]]$Bytes, [byte[]]$Pattern, [int]$MaxHits) {
    if ($null -eq $Pattern -or $Pattern.Length -lt $MinPatternBytes) { return @() }
    return @([SpyroDependencyScanner]::FindPattern($Bytes, $Pattern, $MaxHits))
}

function Find-U32Hits([byte[]]$Bytes, [uint32]$Value, [bool]$AlignedOnly, [int]$MaxHits) {
    return @([SpyroDependencyScanner]::FindU32($Bytes, $Value, $AlignedOnly, $MaxHits))
}

function Convert-HitsForRam($Hits) {
    $converted = New-Object System.Collections.ArrayList
    foreach ($hit in $Hits) {
        foreach ($item in @($hit)) {
            $offset = [int]$item
            if ($offset -lt 0) { continue }
            [void]$converted.Add([ordered]@{
                ramOffset = Format-HexOffset $offset
                runtimeAddress = Format-Hex32 ($MainRamBase + [uint64]$offset)
            })
        }
    }
    return @($converted.ToArray())
}

function Get-RamWordContext([byte[]]$Ram, [int]$Offset, [int]$WordsEachSide = 4) {
    $items = New-Object System.Collections.ArrayList
    $start = [Math]::Max(0, $Offset - ($WordsEachSide * 4))
    $end = [Math]::Min($Ram.Length - 4, $Offset + ($WordsEachSide * 4))
    for ($i = $start; $i -le $end; $i += 4) {
        [void]$items.Add([ordered]@{
            ramOffset = Format-HexOffset $i
            runtimeAddress = Format-Hex32 ($MainRamBase + [uint64]$i)
            value = Format-Hex32 (Get-UInt32LE $Ram $i)
            signed = (Get-Int32LE $Ram $i)
            isHit = ($i -eq $Offset)
        })
    }
    return @($items.ToArray())
}

function Convert-WordHitsForRam([byte[]]$Ram, $Hits) {
    $converted = New-Object System.Collections.ArrayList
    foreach ($hit in $Hits) {
        foreach ($item in @($hit)) {
            $offset = [int]$item
            if ($offset -lt 0) { continue }
            [void]$converted.Add([ordered]@{
                ramOffset = Format-HexOffset $offset
                runtimeAddress = Format-Hex32 ($MainRamBase + [uint64]$offset)
                contextWords = Get-RamWordContext $Ram $offset 5
            })
        }
    }
    return @($converted.ToArray())
}

function Convert-HitsForWadEntry($Hits, $Entry) {
    $converted = New-Object System.Collections.ArrayList
    foreach ($hit in $Hits) {
        foreach ($item in @($hit)) {
            $offset = [int]$item
            if ($offset -lt 0) { continue }
            [void]$converted.Add([ordered]@{
                entryOffset = Format-HexOffset $offset
                wadOffset = Format-HexOffset ([int64]$Entry.offset + [int64]$offset)
            })
        }
    }
    return @($converted.ToArray())
}

function Search-EntriesForPattern($Entries, [byte[]]$Pattern, [string]$PatternName) {
    if ($null -eq $Pattern -or $Pattern.Length -lt $MinPatternBytes) {
        return [ordered]@{
            pattern = $PatternName
            skipped = $true
            reason = "pattern is shorter than $MinPatternBytes bytes"
            patternLength = $(if ($null -eq $Pattern) { 0 } else { $Pattern.Length })
            hitCount = 0
            entries = @()
        }
    }
    $entryHits = @()
    $total = 0
    foreach ($entry in $Entries) {
        if ($null -eq $entry) { continue }
        $hits = @(Find-PatternHits $entry.bytes $Pattern $MaxHits)
        if ($hits.Count -eq 0) { continue }
        $total += $hits.Count
        $entryHits += [ordered]@{
            entryIndex = [int]$entry.index
            entryOffset = Format-HexOffset ([int64]$entry.offset)
            entrySize = [int]$entry.size
            hitCount = $hits.Count
            hits = Convert-HitsForWadEntry $hits $entry
        }
    }
    return [ordered]@{
        pattern = $PatternName
        skipped = $false
        patternLength = $Pattern.Length
        hitCount = $total
        entries = $entryHits
    }
}

function Search-RamForPattern([byte[]]$Ram, [byte[]]$Pattern, [string]$PatternName) {
    if ($null -eq $Pattern -or $Pattern.Length -lt $MinPatternBytes) {
        return [ordered]@{
            pattern = $PatternName
            skipped = $true
            reason = "pattern is shorter than $MinPatternBytes bytes"
            patternLength = $(if ($null -eq $Pattern) { 0 } else { $Pattern.Length })
            hitCount = 0
            hits = @()
        }
    }
    $hits = @(Find-PatternHits $Ram $Pattern $MaxHits)
    return [ordered]@{
        pattern = $PatternName
        skipped = $false
        patternLength = $Pattern.Length
        hitCount = $hits.Count
        hits = Convert-HitsForRam $hits
    }
}

function Search-RamForWord([byte[]]$Ram, [uint32]$Value, [string]$Name) {
    if ($Value -eq 0) {
        return [ordered]@{
            name = $Name
            value = Format-Hex32 $Value
            skipped = $true
            reason = "zero pointer/value"
            alignedHitCount = 0
            unalignedHitCount = 0
            alignedHits = @()
            unalignedHits = @()
        }
    }
    $aligned = @(Find-U32Hits $Ram $Value $true $MaxHits)
    $unaligned = @(Find-U32Hits $Ram $Value $false $MaxHits)
    return [ordered]@{
        name = $Name
        value = Format-Hex32 $Value
        skipped = $false
        alignedHitCount = $aligned.Count
        unalignedHitCount = $unaligned.Count
        alignedHits = Convert-WordHitsForRam $Ram $aligned
        unalignedHits = Convert-HitsForRam $unaligned
    }
}

function Test-MainRamPointer([uint32]$Value) {
    $as64 = [uint64]$Value
    return ($as64 -ge $MainRamBase -and $as64 -lt ($MainRamBase + [uint64]$MainRamSize))
}

function Get-ChildPointerDependencies([byte[]]$DonorRam, [byte[]]$TargetRam, [byte[]]$BlockBytes, [int]$BlockRamOffset, [string]$Label) {
    $children = New-Object System.Collections.ArrayList
    $seen = @{}
    if ($null -eq $BlockBytes -or $BlockBytes.Length -lt 4) { return @() }
    for ($i = 0; $i -le ($BlockBytes.Length - 4); $i += 4) {
        $value = Get-UInt32LE $BlockBytes $i
        if (-not (Test-MainRamPointer $value)) { continue }
        $key = Format-Hex32 $value
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $childOffset = [int]($value -band 0x001FFFFF)
        [byte[]]$childBytes = Copy-ByteRange $DonorRam $childOffset 0x60
        [byte[]]$childPrefix = Copy-ByteRange $childBytes 0 ([Math]::Min(32, $childBytes.Length))
        [void]$children.Add([ordered]@{
            parentLabel = $Label
            withinParentOffset = Format-HexOffset $i
            pointerValue = Format-Hex32 $value
            pointerRamOffset = Format-HexOffset ($BlockRamOffset + $i)
            childRamOffset = Format-HexOffset $childOffset
            childHexPreview = Convert-BytesToHex $childBytes 64
            donorChildPrefixHits = Search-RamForPattern $DonorRam $childPrefix "child dependency block prefix"
            targetChildPrefixHits = Search-RamForPattern $TargetRam $childPrefix "child dependency block prefix"
            donorChildPointerReferences = Search-RamForWord $DonorRam $value "child dependency block pointer"
            targetChildPointerReferences = Search-RamForWord $TargetRam $value "child dependency block pointer"
        })
    }
    return @($children.ToArray())
}

function Get-Templates([string]$Path) {
    $resolved = Resolve-WorkspacePath $Path
    if (-not (Test-Path -LiteralPath $resolved)) { throw "Missing object template file: $resolved" }
    $root = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
    $rows = @($root.templates)
    if ($Family.Count -gt 0) {
        $wantedFamily = @($Family | ForEach-Object { [string]$_ })
        $rows = @($rows | Where-Object { $wantedFamily -contains [string]$_.family })
    }
    if ($TemplateId.Count -gt 0) {
        $wantedId = @($TemplateId | ForEach-Object { [string]$_ })
        $rows = @($rows | Where-Object { $wantedId -contains [string]$_.id })
    }
    return $rows
}

function Get-DonorMapEntry($Template) {
    $slug = [string]$Template.sourceLevelSlug
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $slug = (Normalize-LevelKey ([string]$Template.sourceLevelKey))
    }
    $path = Resolve-WorkspacePath (".\{0}-runtime-source-donor-map.json" -f $slug)
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing donor map for $($Template.sourceLevelName): $path. Run New-SpyroRuntimeSourceDonorMap.ps1 first."
    }
    $map = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $trueIndex = [int]$Template.sourceTrueIndex
    $entry = @($map.entries | Where-Object { [int]$_.runtimeTrueIndex -eq $trueIndex } | Select-Object -First 1)
    if ($entry.Count -eq 0) {
        throw "Donor map $path does not contain runtime T$trueIndex for template $($Template.id)."
    }
    return [ordered]@{
        path = $path
        map = $map
        entry = $entry[0]
    }
}

function Get-ByteProp($Record, [string]$Name) {
    $prop = $Record.PSObject.Properties[$Name]
    if ($null -eq $prop) { return -1 }
    return [int](Convert-HexOrInt64 $prop.Value)
}

function Get-TargetSignatureSummary($TargetRecords, $SourceRecord, $RuntimeRecord) {
    $runtimeType = Get-ByteProp $RuntimeRecord "typeHex"
    $runtimeState = Get-ByteProp $RuntimeRecord "stateHex"
    $runtimeFlag4A = Get-ByteProp $RuntimeRecord "flag4AHex"
    $runtimeFlag4B = Get-ByteProp $RuntimeRecord "flag4BHex"
    $runtimeByte36 = Get-ByteProp $RuntimeRecord "sourceByte36"
    $runtimeByte4F = Get-ByteProp $RuntimeRecord "sourceByte4F"

    $sameType = @($TargetRecords | Where-Object { [int]$_.type -eq [int]$SourceRecord.type })
    $sameIdentity = @($TargetRecords | Where-Object {
        [int]$_.type -eq [int]$SourceRecord.type -and
        [int]$_.sourceByte36 -eq [int]$SourceRecord.sourceByte36 -and
        [int]$_.sourceByte4F -eq [int]$SourceRecord.sourceByte4F
    })
    $sameSourceSignature = @($TargetRecords | Where-Object {
        [int]$_.type -eq [int]$SourceRecord.type -and
        [int]$_.state -eq [int]$SourceRecord.state -and
        [int]$_.flag4A -eq [int]$SourceRecord.flag4A -and
        [int]$_.flag4B -eq [int]$SourceRecord.flag4B -and
        [int]$_.sourceByte36 -eq [int]$SourceRecord.sourceByte36 -and
        [int]$_.sourceByte4F -eq [int]$SourceRecord.sourceByte4F
    })
    $sameRuntimeSignature = @($TargetRecords | Where-Object {
        [int]$_.type -eq $runtimeType -and
        [int]$_.state -eq $runtimeState -and
        [int]$_.flag4A -eq $runtimeFlag4A -and
        [int]$_.flag4B -eq $runtimeFlag4B -and
        [int]$_.sourceByte36 -eq $runtimeByte36 -and
        [int]$_.sourceByte4F -eq $runtimeByte4F
    })
    $sameRuntimeTypeFlags = @($TargetRecords | Where-Object {
        [int]$_.type -eq $runtimeType -and
        [int]$_.state -eq $runtimeState -and
        [int]$_.flag4A -eq $runtimeFlag4A -and
        [int]$_.flag4B -eq $runtimeFlag4B
    })

    return [ordered]@{
        sameTypeCount = $sameType.Count
        sameIdentityCount = $sameIdentity.Count
        sameSourceSignatureCount = $sameSourceSignature.Count
        sameRuntimeSignatureCount = $sameRuntimeSignature.Count
        sameRuntimeTypeFlagsCount = $sameRuntimeTypeFlags.Count
        sameIdentityExamples = @($sameIdentity | Select-Object -First 8 | ForEach-Object { "T$($_.trueIndex) $($_.typeHex) $($_.stateHex) flags $($_.flag4AHex)/$($_.flag4BHex) +36=$($_.sourceByte36Hex) +4F=$($_.sourceByte4FHex)" })
        sameRuntimeTypeFlagsExamples = @($sameRuntimeTypeFlags | Select-Object -First 8 | ForEach-Object { "T$($_.trueIndex) $($_.typeHex) $($_.stateHex) flags $($_.flag4AHex)/$($_.flag4BHex) +36=$($_.sourceByte36Hex) +4F=$($_.sourceByte4FHex)" })
    }
}

function New-TraceForTemplate($Stream, $Layout, $WadEntries, $Template, $TargetTable, $TargetMap, [byte[]]$TargetRam, $TargetLevelEntries, $TargetActorEntries) {
    $donor = Get-DonorMapEntry $Template
    $donorMap = $donor.map
    $donorEntry = $donor.entry
    $sourceTable = Get-LevelSourceTable ([string]$Template.sourceLevelKey)
    $sourceMap = Get-SpecialDataMap $Stream $Layout $sourceTable
    $sourceRecord = Get-SourceRecord $Stream $Layout $sourceTable ([int]$Template.sourceTrueIndex)
    $sourceSpecial = if ($sourceRecord.hasSourceSpecialData) { $sourceMap.specialByOffset[$sourceRecord.specialDataOffset] } else { $null }
    [byte[]]$runtimeRecordBytes = Convert-HexStringToBytes ([string]$donorEntry.runtimeRecord.bytesHex)
    [byte[]]$sourceRecordBytes = $sourceRecord.bytes
    [byte[]]$sourceSpecialBytes = if ($null -ne $sourceSpecial) { $sourceSpecial.bytes } else { New-Object byte[] 0 }
    [byte[]]$sourceIdentityTail = Copy-ByteRange $sourceRecordBytes 0x36 ($RecordStride - 0x36)
    [byte[]]$runtimeIdentityTail = Copy-ByteRange $runtimeRecordBytes 0x36 ($RecordStride - 0x36)
    $sourceActorId16 = [int]$sourceRecord.actorId16
    $runtimeActorId16 = [int]$runtimeRecordBytes[0x36] -bor ([int]$runtimeRecordBytes[0x37] -shl 8)
    $sourceActorSlotOffset = $ActorPointerTableRamOffset + ($sourceActorId16 * 4)
    $runtimeActorSlotOffset = $ActorPointerTableRamOffset + ($runtimeActorId16 * 4)
    $targetSourceActorPointer = if (($sourceActorSlotOffset + 4) -le $TargetRam.Length) { Get-UInt32LE $TargetRam $sourceActorSlotOffset } else { 0 }
    $targetRuntimeActorPointer = if (($runtimeActorSlotOffset + 4) -le $TargetRam.Length) { Get-UInt32LE $TargetRam $runtimeActorSlotOffset } else { 0 }

    $donorRuntimePath = Resolve-WorkspacePath ([string]$donorMap.runtime.path)
    if (-not (Test-Path -LiteralPath $donorRuntimePath)) { throw "Missing donor runtime RAM dump: $donorRuntimePath" }
    [byte[]]$donorRam = [IO.File]::ReadAllBytes($donorRuntimePath)
    if ($donorRam.Length -ne $MainRamSize) { throw "Expected 2 MB donor RAM dump: $donorRuntimePath" }

    $runtimeSpecialPointer = [uint32](Convert-HexOrInt64 ([string]$donorEntry.runtimeRecord.specialDataPointer))
    $runtimeRecordAddress = [uint32](Convert-HexOrInt64 ([string]$donorEntry.runtimeRecord.runtimeAddress))
    $runtimeSpecialPointer64 = [uint64]$runtimeSpecialPointer
    $runtimeSpecialOffset = if ($runtimeSpecialPointer64 -ge $MainRamBase -and $runtimeSpecialPointer64 -lt ($MainRamBase + [uint64]$MainRamSize)) { [int]($runtimeSpecialPointer -band 0x001FFFFF) } else { -1 }
    [byte[]]$runtimeSpecialBlock = if ($runtimeSpecialOffset -ge 0) { Copy-ByteRange $donorRam $runtimeSpecialOffset $RuntimeBlockLength } else { New-Object byte[] 0 }
    [byte[]]$runtimeSpecialPrefix = if ($runtimeSpecialBlock.Length -gt 0) { Copy-ByteRange $runtimeSpecialBlock 0 ([Math]::Min(32, $runtimeSpecialBlock.Length)) } else { New-Object byte[] 0 }
    $runtimeSpecialChildDependencies = Get-ChildPointerDependencies $donorRam $TargetRam $runtimeSpecialBlock $runtimeSpecialOffset "runtime special-data block"

    $donorLevelEntries = @(
        (Read-WadEntryBytes $Stream $Layout $WadEntries ([int]$sourceTable.wadEntry)),
        (Read-WadEntryBytes $Stream $Layout $WadEntries ([int]$sourceTable.wadEntry + 1))
    ) | Where-Object { $null -ne $_ }

    $targetSignature = Get-TargetSignatureSummary $TargetMap.records $sourceRecord $donorEntry.runtimeRecord

    $donorRamPatterns = [ordered]@{
        sourceSpecialBlock = Search-RamForPattern $donorRam $sourceSpecialBytes "source special-data block"
        runtimeSpecialPrefix = Search-RamForPattern $donorRam $runtimeSpecialPrefix "runtime special-data prefix"
        sourceRecord = Search-RamForPattern $donorRam $sourceRecordBytes "source record"
        runtimeRecord = Search-RamForPattern $donorRam $runtimeRecordBytes "runtime record"
        sourceIdentityTail = Search-RamForPattern $donorRam $sourceIdentityTail "source identity tail"
        runtimeIdentityTail = Search-RamForPattern $donorRam $runtimeIdentityTail "runtime identity tail"
    }
    $targetRamPatterns = [ordered]@{
        sourceSpecialBlock = Search-RamForPattern $TargetRam $sourceSpecialBytes "source special-data block"
        runtimeSpecialPrefix = Search-RamForPattern $TargetRam $runtimeSpecialPrefix "runtime special-data prefix"
        sourceRecord = Search-RamForPattern $TargetRam $sourceRecordBytes "source record"
        runtimeRecord = Search-RamForPattern $TargetRam $runtimeRecordBytes "runtime record"
        sourceIdentityTail = Search-RamForPattern $TargetRam $sourceIdentityTail "source identity tail"
        runtimeIdentityTail = Search-RamForPattern $TargetRam $runtimeIdentityTail "runtime identity tail"
    }

    $donorWadPatterns = [ordered]@{
        sourceSpecialBlock = Search-EntriesForPattern $donorLevelEntries $sourceSpecialBytes "source special-data block"
        sourceRecord = Search-EntriesForPattern $donorLevelEntries $sourceRecordBytes "source record"
        sourceIdentityTail = Search-EntriesForPattern $donorLevelEntries $sourceIdentityTail "source identity tail"
    }
    $targetWadPatterns = [ordered]@{
        sourceSpecialBlock = Search-EntriesForPattern $TargetLevelEntries $sourceSpecialBytes "source special-data block"
        sourceRecord = Search-EntriesForPattern $TargetLevelEntries $sourceRecordBytes "source record"
        sourceIdentityTail = Search-EntriesForPattern $TargetLevelEntries $sourceIdentityTail "source identity tail"
    }
    $actorWadPatterns = [ordered]@{
        sourceSpecialBlock = Search-EntriesForPattern $TargetActorEntries $sourceSpecialBytes "source special-data block"
        sourceRecord = Search-EntriesForPattern $TargetActorEntries $sourceRecordBytes "source record"
        sourceIdentityTail = Search-EntriesForPattern $TargetActorEntries $sourceIdentityTail "source identity tail"
        runtimeIdentityTail = Search-EntriesForPattern $TargetActorEntries $runtimeIdentityTail "runtime identity tail"
    }

    $donorWords = [ordered]@{
        specialDataPointer = Search-RamForWord $donorRam $runtimeSpecialPointer "runtime special-data pointer"
        runtimeRecordAddress = Search-RamForWord $donorRam $runtimeRecordAddress "runtime record address"
    }
    $targetWords = [ordered]@{
        specialDataPointer = Search-RamForWord $TargetRam $runtimeSpecialPointer "donor runtime special-data pointer"
        runtimeRecordAddress = Search-RamForWord $TargetRam $runtimeRecordAddress "donor runtime record address"
    }

    $secondaryBlocks = New-Object System.Collections.ArrayList
    $runtimeRecordSpecialPointerField = [uint32]($runtimeRecordAddress + 8)
    foreach ($hit in @($donorWords.specialDataPointer.alignedHits)) {
        $hitRuntimeAddress = [uint32](Convert-HexOrInt64 ([string]$hit.runtimeAddress))
        if ($hitRuntimeAddress -eq $runtimeRecordSpecialPointerField) { continue }
        $hitOffset = [int](Convert-HexOrInt64 ([string]$hit.ramOffset))
        $blockStart = [Math]::Max(0, $hitOffset - 0x14)
        $blockPointer = [uint32]($MainRamBase + [uint64]$blockStart)
        [byte[]]$blockBytes = Copy-ByteRange $donorRam $blockStart 0x60
        [byte[]]$blockPrefix = Copy-ByteRange $blockBytes 0 ([Math]::Min(32, $blockBytes.Length))
        [void]$secondaryBlocks.Add([ordered]@{
            pointerHitOffset = Format-HexOffset $hitOffset
            pointerHitAddress = Format-Hex32 $hitRuntimeAddress
            inferredBlockOffset = Format-HexOffset $blockStart
            inferredBlockAddress = Format-Hex32 $blockPointer
            blockLength = $blockBytes.Length
            blockHexPreview = Convert-BytesToHex $blockBytes 64
            donorBlockPrefixHits = Search-RamForPattern $donorRam $blockPrefix "secondary dependency block prefix"
            targetBlockPrefixHits = Search-RamForPattern $TargetRam $blockPrefix "secondary dependency block prefix"
            donorLevelWadBlockPrefixHits = Search-EntriesForPattern $donorLevelEntries $blockPrefix "secondary dependency block prefix"
            targetLevelWadBlockPrefixHits = Search-EntriesForPattern $TargetLevelEntries $blockPrefix "secondary dependency block prefix"
            targetActorWadBlockPrefixHits = Search-EntriesForPattern $TargetActorEntries $blockPrefix "secondary dependency block prefix"
            donorBlockPointerReferences = Search-RamForWord $donorRam $blockPointer "secondary dependency block pointer"
            targetBlockPointerReferences = Search-RamForWord $TargetRam $blockPointer "secondary dependency block pointer"
            childPointerDependencies = Get-ChildPointerDependencies $donorRam $TargetRam $blockBytes $blockStart "secondary dependency block"
        })
    }

    $missing = New-Object System.Collections.ArrayList
    if ($targetSignature.sameIdentityCount -eq 0) {
        [void]$missing.Add("$($TargetTable.displayName) has no source moby with donor identity bytes +36=$($sourceRecord.sourceByte36Hex), +4F=$($sourceRecord.sourceByte4FHex)")
    }
    if ($targetSignature.sameRuntimeSignatureCount -eq 0) {
        [void]$missing.Add("$($TargetTable.displayName) has no source moby matching the donor's runtime type/state/flags/identity")
    }
    if ($runtimeActorId16 -gt 0 -and $targetRuntimeActorPointer -eq 0) {
        [void]$missing.Add("$($TargetTable.displayName) actor pointer table slot 0x$($runtimeActorSlotOffset.ToString('X')) for actor id 0x$($runtimeActorId16.ToString('X4')) is zero, so the target level has not loaded this actor package")
    }
    if ($sourceSpecialBytes.Length -ge $MinPatternBytes -and [int]$targetWadPatterns.sourceSpecialBlock.hitCount -eq 0) {
        [void]$missing.Add("donor source special-data block is absent from the target level WAD entries")
    }
    if ($runtimeSpecialPrefix.Length -ge $MinPatternBytes -and [int]$targetRamPatterns.runtimeSpecialPrefix.hitCount -eq 0) {
        [void]$missing.Add("donor runtime special-data prefix is absent from the target RAM capture")
    }
    if ($runtimeSpecialPointer -ne 0 -and [int]$donorWords.specialDataPointer.alignedHitCount -gt 0 -and [int]$targetWords.specialDataPointer.alignedHitCount -eq 0) {
        [void]$missing.Add("donor level contains aligned references to the special-data pointer, but the target level has none")
    }
    foreach ($block in @($secondaryBlocks.ToArray())) {
        if ([int]$block.targetBlockPrefixHits.hitCount -eq 0 -and [int]$block.targetBlockPointerReferences.alignedHitCount -eq 0) {
            [void]$missing.Add("donor has a secondary dependency block at $($block.inferredBlockAddress) that is absent from the target RAM capture")
        }
        foreach ($child in @($block.childPointerDependencies)) {
            if ([int]$child.targetChildPrefixHits.hitCount -eq 0 -and [int]$child.targetChildPointerReferences.alignedHitCount -eq 0) {
                [void]$missing.Add("secondary dependency child $($child.pointerValue) is absent from the target RAM capture")
            }
        }
    }
    foreach ($child in @($runtimeSpecialChildDependencies)) {
        if ([int]$child.targetChildPrefixHits.hitCount -eq 0 -and [int]$child.targetChildPointerReferences.alignedHitCount -eq 0) {
            [void]$missing.Add("runtime special-data child $($child.pointerValue) is absent from the target RAM capture")
        }
    }

    $recommendation = if ([string]$Template.family -eq "key") {
        "Treat as a lightweight external object for now: this donor has no runtime special-data pointer, and user testing proved it can spawn and be collected in Artisans. Keep dependency tracing on it as the control sample."
    }
    elseif ($targetSignature.sameIdentityCount -eq 0 -and $runtimeSpecialPointer -ne 0) {
        "Next experiment should import or remap the donor's level-local visual/behavior package, not just append the moby row. The source row and special data can be copied, but the target level does not already carry the donor identity/package."
    }
    elseif ($runtimeSpecialPointer -ne 0) {
        "Next experiment should copy the source row plus its special-data block and then test whether a local identity remap is enough."
    }
    else {
        "Record-only import is likely enough unless runtime testing proves otherwise."
    }

    return [ordered]@{
        templateId = [string]$Template.id
        family = [string]$Template.family
        displayName = [string]$Template.displayName
        sourceLevel = [ordered]@{
            key = [string]$sourceTable.levelKey
            displayName = [string]$sourceTable.displayName
            wadEntry = [int]$sourceTable.wadEntry
            sourceRecordCount = [int]$sourceTable.recordCount
            donorMapPath = $donor.path
            donorRuntimePath = $donorRuntimePath
        }
        donorRuntime = [ordered]@{
            trueIndex = [int]$donorEntry.runtimeTrueIndex
            runtimeAddress = [string]$donorEntry.runtimeRecord.runtimeAddress
            ramOffset = [string]$donorEntry.runtimeRecord.ramOffset
            specialDataPointer = [string]$donorEntry.runtimeRecord.specialDataPointer
            specialDataPointerMasked = [string]$donorEntry.runtimeRecord.specialDataPointerMasked
            xyz = $donorEntry.runtimeRecord.xyz
            typeHex = [string]$donorEntry.runtimeRecord.typeHex
            stateHex = [string]$donorEntry.runtimeRecord.stateHex
            flag4AHex = [string]$donorEntry.runtimeRecord.flag4AHex
            flag4BHex = [string]$donorEntry.runtimeRecord.flag4BHex
            sourceByte36 = [string]$donorEntry.runtimeRecord.sourceByte36
            sourceByte37 = ("0x{0:X2}" -f [int]$runtimeRecordBytes[0x37])
            sourceByte4F = [string]$donorEntry.runtimeRecord.sourceByte4F
            actorId16 = ("0x{0:X4}" -f $runtimeActorId16)
            bytesHexPreview = Convert-BytesToHex $runtimeRecordBytes 32
        }
        donorSource = [ordered]@{
            trueIndex = [int]$sourceRecord.trueIndex
            recordWadOffset = [string]$sourceRecord.recordWadOffset
            specialDataOffset = [string]$sourceRecord.specialDataOffset
            hasSourceSpecialData = [bool]$sourceRecord.hasSourceSpecialData
            xyz = [ordered]@{ x = $sourceRecord.x; y = $sourceRecord.y; z = $sourceRecord.z; rawX = $sourceRecord.rawX; rawY = $sourceRecord.rawY; rawZ = $sourceRecord.rawZ }
            typeHex = [string]$sourceRecord.typeHex
            stateHex = [string]$sourceRecord.stateHex
            flag4AHex = [string]$sourceRecord.flag4AHex
            flag4BHex = [string]$sourceRecord.flag4BHex
            sourceByte36 = [string]$sourceRecord.sourceByte36Hex
            sourceByte37 = [string]$sourceRecord.sourceByte37Hex
            sourceByte4F = [string]$sourceRecord.sourceByte4FHex
            actorId16 = [string]$sourceRecord.actorId16Hex
            bytesHexPreview = Convert-BytesToHex $sourceRecordBytes 32
        }
        actorPointerTable = [ordered]@{
            tableBase = ("0x{0:X8}" -f ($MainRamBase + [uint64]$ActorPointerTableRamOffset))
            sourceActorId16 = ("0x{0:X4}" -f $sourceActorId16)
            sourceActorSlotAddress = ("0x{0:X8}" -f ($MainRamBase + [uint64]$sourceActorSlotOffset))
            targetSourceActorPointer = Format-Hex32 $targetSourceActorPointer
            runtimeActorId16 = ("0x{0:X4}" -f $runtimeActorId16)
            runtimeActorSlotAddress = ("0x{0:X8}" -f ($MainRamBase + [uint64]$runtimeActorSlotOffset))
            targetRuntimeActorPointer = Format-Hex32 $targetRuntimeActorPointer
            note = "Object actor ids are the little-endian +0x36/+0x37 pair. The loader indexes the RAM actor pointer table with that id; a zero target pointer means the target level did not load that actor package."
        }
        donorSpecialData = if ($null -eq $sourceSpecial) { $null } else {
            [ordered]@{
                sourceOffset = [string]$sourceSpecial.offset
                wadOffset = [string]$sourceSpecial.wadOffset
                length = [int]$sourceSpecial.length
                word0 = Format-Hex32 $sourceSpecial.word0
                bytesHexPreview = Convert-BytesToHex $sourceSpecial.bytes 64
                runtimePointer = Format-Hex32 $runtimeSpecialPointer
                runtimeOffset = $(if ($runtimeSpecialOffset -ge 0) { Format-HexOffset $runtimeSpecialOffset } else { "" })
                runtimeBlockLength = $runtimeSpecialBlock.Length
                runtimeBlockHexPreview = Convert-BytesToHex $runtimeSpecialBlock 64
            }
        }
        targetCompatibility = $targetSignature
        pointerReferences = [ordered]@{
            donorRam = $donorWords
            targetRam = $targetWords
        }
        runtimeSpecialChildDependencies = @($runtimeSpecialChildDependencies)
        secondaryDependencyBlocks = @($secondaryBlocks.ToArray())
        patternPresence = [ordered]@{
            donorRam = $donorRamPatterns
            targetRam = $targetRamPatterns
            donorLevelWad = $donorWadPatterns
            targetLevelWad = $targetWadPatterns
            targetActorWad = $actorWadPatterns
        }
        likelyMissingDependencies = @($missing.ToArray())
        recommendation = $recommendation
    }
}

$resolvedImagePath = Resolve-WorkspacePath $ImagePath
$resolvedTemplatePath = Resolve-WorkspacePath $TemplatePath
$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMarkdown = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $resolvedImagePath)) { throw "Missing image: $resolvedImagePath" }
if (-not (Test-Path -LiteralPath $resolvedTemplatePath)) { throw "Missing template file: $resolvedTemplatePath" }

$targetTable = Get-LevelSourceTable $TargetLevelKey
if ([string]::IsNullOrWhiteSpace($TargetRuntimePath)) {
    $TargetRuntimePath = ".\$($targetTable.editSlug)-before-clean.bin"
}
$resolvedTargetRuntimePath = Resolve-WorkspacePath $TargetRuntimePath
if (-not (Test-Path -LiteralPath $resolvedTargetRuntimePath)) { throw "Missing target runtime RAM dump: $resolvedTargetRuntimePath" }
[byte[]]$targetRamBytes = [IO.File]::ReadAllBytes($resolvedTargetRuntimePath)
if ($targetRamBytes.Length -ne $MainRamSize) { throw "Expected 2 MB target RAM dump: $resolvedTargetRuntimePath" }

$templates = @(Get-Templates $resolvedTemplatePath)
if ($templates.Count -eq 0) { throw "No templates matched the requested filters." }

$layout = Detect-DiscLayout $resolvedImagePath
$wadEntries = Get-WadAnalysisEntries
$stream = [IO.File]::OpenRead($resolvedImagePath)
try {
    $targetMap = Get-SpecialDataMap $stream $layout $targetTable
    $targetLevelEntries = @(
        (Read-WadEntryBytes $stream $layout $wadEntries ([int]$targetTable.wadEntry)),
        (Read-WadEntryBytes $stream $layout $wadEntries ([int]$targetTable.wadEntry + 1))
    ) | Where-Object { $null -ne $_ }
    $targetActorEntries = @($DefaultActorSearchEntries | ForEach-Object { Read-WadEntryBytes $stream $layout $wadEntries ([int]$_) } | Where-Object { $null -ne $_ })

    $traces = @()
    foreach ($template in $templates) {
        $traces += New-TraceForTemplate $stream $layout $wadEntries $template $targetTable $targetMap $targetRamBytes $targetLevelEntries $targetActorEntries
    }
}
finally {
    $stream.Dispose()
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "New-SpyroObjectDependencyTrace.ps1"
    imagePath = (Resolve-Path -LiteralPath $resolvedImagePath).Path
    targetLevel = [ordered]@{
        key = [string]$targetTable.levelKey
        displayName = [string]$targetTable.displayName
        wadEntry = [int]$targetTable.wadEntry
        sourceRecordCount = [int]$targetTable.recordCount
        runtimePath = (Resolve-Path -LiteralPath $resolvedTargetRuntimePath).Path
    }
    note = "Dependency trace for cross-level object imports. It compares source moby rows, source special-data blocks, live runtime blocks, direct pointer references, target-level source signatures, and target actor/package WAD presence."
    settings = [ordered]@{
        maxHits = $MaxHits
        runtimeBlockLength = $RuntimeBlockLength
        minPatternBytes = $MinPatternBytes
        searchedActorEntries = @($DefaultActorSearchEntries)
    }
    traces = $traces
}

$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
$md.Add("# Spyro Object Dependency Trace") | Out-Null
$md.Add("") | Out-Null
$md.Add("Target: $($targetTable.displayName) ($($targetTable.levelKey))") | Out-Null
$md.Add("") | Out-Null
$md.Add("This report checks whether a cross-level object donor brings dependencies that the target level already has, or whether the editor needs to import/remap more than the moby row and special-data block.") | Out-Null
$md.Add("") | Out-Null
foreach ($trace in $traces) {
    $md.Add("## $($trace.displayName) - $($trace.sourceLevel.displayName) T$($trace.donorRuntime.trueIndex)") | Out-Null
    $md.Add("") | Out-Null
    $md.Add("- Runtime signature: $($trace.donorRuntime.typeHex) $($trace.donorRuntime.stateHex) flags $($trace.donorRuntime.flag4AHex)/$($trace.donorRuntime.flag4BHex) actor=$($trace.donorRuntime.actorId16) (+36=$($trace.donorRuntime.sourceByte36), +37=$($trace.donorRuntime.sourceByte37), +4F=$($trace.donorRuntime.sourceByte4F)), pointer $($trace.donorRuntime.specialDataPointer)") | Out-Null
    $md.Add("- Source signature: $($trace.donorSource.typeHex) $($trace.donorSource.stateHex) flags $($trace.donorSource.flag4AHex)/$($trace.donorSource.flag4BHex) actor=$($trace.donorSource.actorId16) (+36=$($trace.donorSource.sourceByte36), +37=$($trace.donorSource.sourceByte37), +4F=$($trace.donorSource.sourceByte4F)), special $($trace.donorSource.specialDataOffset)") | Out-Null
    $md.Add("- Actor pointer table: slot $($trace.actorPointerTable.runtimeActorSlotAddress) for actor $($trace.actorPointerTable.runtimeActorId16) has target pointer $($trace.actorPointerTable.targetRuntimeActorPointer)") | Out-Null
    if ($null -ne $trace.donorSpecialData) {
        $md.Add("- Special data: source length $($trace.donorSpecialData.length), source WAD $($trace.donorSpecialData.wadOffset), runtime pointer $($trace.donorSpecialData.runtimePointer)") | Out-Null
    } else {
        $md.Add("- Special data: none") | Out-Null
    }
    $md.Add("- Target source matches: type=$($trace.targetCompatibility.sameTypeCount), identity=$($trace.targetCompatibility.sameIdentityCount), sourceSignature=$($trace.targetCompatibility.sameSourceSignatureCount), runtimeSignature=$($trace.targetCompatibility.sameRuntimeSignatureCount), runtimeTypeFlags=$($trace.targetCompatibility.sameRuntimeTypeFlagsCount)") | Out-Null
    $md.Add("- Target WAD hits: sourceSpecial=$($trace.patternPresence.targetLevelWad.sourceSpecialBlock.hitCount), sourceRecord=$($trace.patternPresence.targetLevelWad.sourceRecord.hitCount), sourceIdentityTail=$($trace.patternPresence.targetLevelWad.sourceIdentityTail.hitCount)") | Out-Null
    $md.Add("- Target RAM hits: runtimeSpecialPrefix=$($trace.patternPresence.targetRam.runtimeSpecialPrefix.hitCount), runtimeRecord=$($trace.patternPresence.targetRam.runtimeRecord.hitCount), runtimeIdentityTail=$($trace.patternPresence.targetRam.runtimeIdentityTail.hitCount)") | Out-Null
    $md.Add("- Target actor/package hits: sourceSpecial=$($trace.patternPresence.targetActorWad.sourceSpecialBlock.hitCount), sourceIdentityTail=$($trace.patternPresence.targetActorWad.sourceIdentityTail.hitCount), runtimeIdentityTail=$($trace.patternPresence.targetActorWad.runtimeIdentityTail.hitCount)") | Out-Null
    $md.Add("- Pointer refs: donor special-pointer aligned refs=$($trace.pointerReferences.donorRam.specialDataPointer.alignedHitCount), target aligned refs to donor pointer=$($trace.pointerReferences.targetRam.specialDataPointer.alignedHitCount)") | Out-Null
    if ($trace.runtimeSpecialChildDependencies.Count -gt 0) {
        $md.Add("- Runtime special-data child pointers: " + (($trace.runtimeSpecialChildDependencies | ForEach-Object { "$($_.pointerValue) targetBlockHits=$($_.targetChildPrefixHits.hitCount) targetPointerRefs=$($_.targetChildPointerReferences.alignedHitCount)" }) -join "; ")) | Out-Null
    } else {
        $md.Add("- Runtime special-data child pointers: none") | Out-Null
    }
    if ($trace.secondaryDependencyBlocks.Count -gt 0) {
        $md.Add("- Secondary dependency blocks: " + (($trace.secondaryDependencyBlocks | ForEach-Object { "$($_.inferredBlockAddress) donorWadHits=$($_.donorLevelWadBlockPrefixHits.hitCount) targetRamHits=$($_.targetBlockPrefixHits.hitCount) targetWadHits=$($_.targetLevelWadBlockPrefixHits.hitCount) targetActorHits=$($_.targetActorWadBlockPrefixHits.hitCount) targetPointerRefs=$($_.targetBlockPointerReferences.alignedHitCount) childPointers=$($_.childPointerDependencies.Count)" }) -join "; ")) | Out-Null
    } else {
        $md.Add("- Secondary dependency blocks: none found outside the donor moby record") | Out-Null
    }
    if ($trace.likelyMissingDependencies.Count -gt 0) {
        $md.Add("- Likely missing: " + ($trace.likelyMissingDependencies -join "; ")) | Out-Null
    } else {
        $md.Add("- Likely missing: none identified by this trace") | Out-Null
    }
    $md.Add("- Recommendation: $($trace.recommendation)") | Out-Null
    $md.Add("") | Out-Null
}

$md.Add("## Next Experiment") | Out-Null
$md.Add("") | Out-Null
$md.Add("- Keep the key path as the control case: it has no runtime special-data pointer and already works in Artisans.") | Out-Null
$md.Add("- For spring and locked chests, build an import/remap experiment around the full dependency cluster: source row, source special data, runtime special-data block, secondary dependency block, child pointer blocks, and the level-global pointer slot that references the secondary block.") | Out-Null
$md.Add("- If the level-global pointer slot cannot be sourced cleanly from the level WAD, test a local identity remap next: use an Artisans-resident visible chest identity while preserving the donor reward/content behavior, so we can separate visual package loading from chest logic.") | Out-Null

$md | Set-Content -LiteralPath $resolvedOutMarkdown -Encoding UTF8

Write-Host "Wrote $resolvedOutJson"
Write-Host "Wrote $resolvedOutMarkdown"
foreach ($trace in $traces) {
    Write-Host ("{0}: target identity matches {1}, runtime-special RAM hits {2}, target pointer refs {3}" -f $trace.displayName, $trace.targetCompatibility.sameIdentityCount, $trace.patternPresence.targetRam.runtimeSpecialPrefix.hitCount, $trace.pointerReferences.targetRam.specialDataPointer.alignedHitCount)
}
