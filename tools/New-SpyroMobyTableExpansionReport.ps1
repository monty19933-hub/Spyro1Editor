param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [string]$OutJsonPath = ".\spyro-moby-table-expansion-report.json",
    [string]$OutMarkdownPath = ".\spyro-moby-table-expansion-report.md"
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

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
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
            end = $fileOffset + $fileSize
        }
    }
    return $entries
}

function Measure-Run([byte[]]$Bytes, [int]$Start, [byte]$Value) {
    if ($Start -lt 0 -or $Start -ge $Bytes.Length) { return 0 }
    $count = 0
    for ($i = $Start; $i -lt $Bytes.Length; $i++) {
        if ($Bytes[$i] -ne $Value) { break }
        $count++
    }
    return $count
}

function Test-PlausibleSourceRecord([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + $RecordStride) -gt $Bytes.Length) { return $false }
    $x = Get-Int32LE $Bytes ($Offset + 0x0C)
    $y = Get-Int32LE $Bytes ($Offset + 0x10)
    $z = Get-Int32LE $Bytes ($Offset + 0x14)
    if ([Math]::Abs([int64]$x) -gt 4000000 -or [Math]::Abs([int64]$y) -gt 4000000 -or [Math]::Abs([int64]$z) -gt 4000000) { return $false }
    if ($x -eq 0 -and $y -eq 0 -and $z -eq 0) { return $false }
    $type = [int]$Bytes[$Offset + 0x50]
    $state = [int]$Bytes[$Offset + 0x51]
    if ($type -gt 0x7F -or $state -gt 0x7F) { return $false }
    return $true
}

function New-RecordSummary([byte[]]$Bytes, [int]$Offset, [int]$Index) {
    if ($Offset -lt 0 -or ($Offset + $RecordStride) -gt $Bytes.Length) { return $null }
    $x = Get-Int32LE $Bytes ($Offset + 0x0C)
    $y = Get-Int32LE $Bytes ($Offset + 0x10)
    $z = Get-Int32LE $Bytes ($Offset + 0x14)
    $head = @()
    for ($i = 0; $i -lt 16; $i++) { $head += $Bytes[$Offset + $i].ToString("X2") }
    $tail = @()
    for ($i = 0x50; $i -lt 0x58; $i++) { $tail += $Bytes[$Offset + $i].ToString("X2") }
    return [ordered]@{
        index = $Index
        plausible = Test-PlausibleSourceRecord $Bytes $Offset
        typeHex = ("0x{0:X2}" -f [int]$Bytes[$Offset + 0x50])
        stateHex = ("0x{0:X2}" -f [int]$Bytes[$Offset + 0x51])
        flag52Hex = ("0x{0:X2}" -f [int]$Bytes[$Offset + 0x52])
        flag53Hex = ("0x{0:X2}" -f [int]$Bytes[$Offset + 0x53])
        rawX = $x
        rawY = $y
        rawZ = $z
        x = [Math]::Round($x / 16.0, 4)
        y = [Math]::Round($y / 16.0, 4)
        z = [Math]::Round($z / 16.0, 4)
        first16Hex = ($head -join " ")
        last8Hex = ($tail -join " ")
    }
}

function Get-RuntimeMobySummary([string]$RamPath, [int]$SourceCount) {
    $path = Resolve-WorkspacePath $RamPath
    if (-not (Test-Path -LiteralPath $path)) {
        return [ordered]@{ available = $false; path = $path }
    }
    [byte[]]$ram = [IO.File]::ReadAllBytes($path)
    if ($ram.Length -lt 0x7582C) {
        return [ordered]@{ available = $false; path = $path; note = "RAM dump too small." }
    }
    $ptr = [BitConverter]::ToUInt32($ram, 0x75828)
    $dynamic = [BitConverter]::ToUInt32($ram, 0x7573C)
    $start = [int]($ptr -band 0x001FFFFF)
    $dynamicStart = [int]($dynamic -band 0x001FFFFF)
    $count = 0
    if ($dynamicStart -gt $start -and (($dynamicStart - $start) % $RecordStride) -eq 0) {
        $count = [int](($dynamicStart - $start) / $RecordStride)
    }
    $extras = @()
    for ($index = $SourceCount; $index -lt [Math]::Min($count, $SourceCount + 16); $index++) {
        $extras += New-RecordSummary $ram ($start + ($index * $RecordStride)) $index
    }
    return [ordered]@{
        available = $true
        path = $path
        ptrLevelMobys = ("0x{0:X8}" -f $ptr)
        ptrDynamicLevelMobys = ("0x{0:X8}" -f $dynamic)
        tableRamOffset = ("0x{0:X}" -f $start)
        dynamicRamOffset = ("0x{0:X}" -f $dynamicStart)
        runtimeCount = $count
        sourceCount = $SourceCount
        extraRuntimeRecords = $extras
    }
}

function Get-CountNeedles([int]$Count) {
    return @(
        [ordered]@{ name = "u8"; bytes = @([byte]($Count -band 0xFF)) },
        [ordered]@{ name = "u16le"; bytes = @([byte]($Count -band 0xFF), [byte](($Count -shr 8) -band 0xFF)) },
        [ordered]@{ name = "u32le"; bytes = @([byte]($Count -band 0xFF), [byte](($Count -shr 8) -band 0xFF), [byte](($Count -shr 16) -band 0xFF), [byte](($Count -shr 24) -band 0xFF)) }
    )
}

function Find-CountNeedlesNearTable([byte[]]$EntryBytes, [int]$TableRel, [int]$Count) {
    $windowStart = [Math]::Max(0, $TableRel - 2048)
    $windowEnd = [Math]::Min($EntryBytes.Length, $TableRel + 256)
    $hits = @()
    foreach ($needle in Get-CountNeedles $Count) {
        [byte[]]$pattern = $needle.bytes
        for ($offset = $windowStart; $offset -le ($windowEnd - $pattern.Length); $offset++) {
            $ok = $true
            for ($i = 0; $i -lt $pattern.Length; $i++) {
                if ($EntryBytes[$offset + $i] -ne $pattern[$i]) { $ok = $false; break }
            }
            if (-not $ok) { continue }
            $hits += [ordered]@{
                kind = $needle.name
                value = $Count
                relativeOffset = ("0x{0:X}" -f $offset)
                signedDistanceToTable = $offset - $TableRel
            }
            if (@($hits | Where-Object { $_.kind -eq $needle.name }).Count -ge 16) { break }
        }
    }
    return $hits
}

function Analyze-Level($Level, $Entry, [byte[]]$EntryBytes) {
    $tableRel = [int64]$Level.tableWadOffset - [int64]$Entry.offset
    $tableEndRel = $tableRel + ([int64]$Level.sourceRecordCount * $RecordStride)
    $subfiles = @(Parse-ArchiveHeader $EntryBytes ([int64]$Entry.size))
    $owner = $null
    foreach ($subfile in $subfiles) {
        if ($tableRel -ge [int64]$subfile.offset -and $tableRel -lt [int64]$subfile.end) {
            $owner = $subfile
            break
        }
    }
    $bytesToSubfileEnd = if ($null -ne $owner) { [int64]$owner.end - $tableEndRel } else { $null }
    $bytesToEntryEnd = [int64]$Entry.size - $tableEndRel
    $zeroRun = Measure-Run $EntryBytes ([int]$tableEndRel) 0
    $ffRun = Measure-Run $EntryBytes ([int]$tableEndRel) 0xFF
    $afterRecords = @()
    for ($i = 0; $i -lt 10; $i++) {
        $afterRecords += New-RecordSummary $EntryBytes ([int]($tableEndRel + ($i * $RecordStride))) ([int]$Level.sourceRecordCount + $i)
    }
    $plausibleAfter = @($afterRecords | Where-Object { $null -ne $_ -and $_.plausible })
    $countHits = @()
    foreach ($count in @([int]$Level.sourceRecordCount, [int]$Level.runtimeCount)) {
        $countHits += Find-CountNeedlesNearTable $EntryBytes ([int]$tableRel) $count
    }

    return [ordered]@{
        levelKey = $Level.levelKey
        displayName = $Level.displayName
        wadEntry = [int]$Level.wadEntry
        entryOffset = ("0x{0:X}" -f [int64]$Entry.offset)
        entrySize = ("0x{0:X}" -f [int64]$Entry.size)
        tableRelativeOffset = ("0x{0:X}" -f $tableRel)
        tableWadOffset = ("0x{0:X}" -f [int64]$Level.tableWadOffset)
        sourceRecordCount = [int]$Level.sourceRecordCount
        runtimeCount = [int]$Level.runtimeCount
        recordStride = "0x58"
        tableByteLength = [int64]$Level.sourceRecordCount * $RecordStride
        tableEndRelativeOffset = ("0x{0:X}" -f $tableEndRel)
        tableEndWadOffset = ("0x{0:X}" -f ([int64]$Level.tableWadOffset + ([int64]$Level.sourceRecordCount * $RecordStride)))
        owningSubfile = $(if ($null -eq $owner) { $null } else { [ordered]@{ index = [int]$owner.index; offset = ("0x{0:X}" -f [int64]$owner.offset); size = ("0x{0:X}" -f [int64]$owner.size); end = ("0x{0:X}" -f [int64]$owner.end) } })
        bytesToOwningSubfileEnd = $bytesToSubfileEnd
        bytesToEntryEnd = $bytesToEntryEnd
        zeroRunAfterTable = $zeroRun
        ffRunAfterTable = $ffRun
        immediateAppendLooksSafe = ($zeroRun -ge $RecordStride -and ($null -eq $bytesToSubfileEnd -or $bytesToSubfileEnd -ge $RecordStride))
        plausibleRecordsImmediatelyAfter = @($plausibleAfter).Count
        recordsAfterTable = $afterRecords
        nearbyCountNeedleHits = $countHits
        runtime = Get-RuntimeMobySummary $Level.ramPath ([int]$Level.sourceRecordCount)
    }
}

$imagePath = Resolve-WorkspacePath $ImagePath
$wadAnalysisPath = Resolve-WorkspacePath $WadAnalysisPath
$outJsonPath = Resolve-WorkspacePath $OutJsonPath
$outMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing image: $imagePath" }
if (-not (Test-Path -LiteralPath $wadAnalysisPath)) { throw "Missing WAD analysis: $wadAnalysisPath" }

$wad = Get-Content -Raw -LiteralPath $wadAnalysisPath | ConvertFrom-Json
$entries = @(Get-ArrayField $wad "entries")
$levels = @(
    [ordered]@{ levelKey = "StoneHill"; displayName = "Stone Hill"; wadEntry = 12; tableWadOffset = [Convert]::ToInt64("D72B38", 16); sourceRecordCount = 195; runtimeCount = 197; ramPath = ".\stonehill-before-gem-clean.bin" },
    [ordered]@{ levelKey = "Artisans"; displayName = "Artisans Home"; wadEntry = 10; tableWadOffset = [Convert]::ToInt64("9D42AC", 16); sourceRecordCount = 174; runtimeCount = 184; ramPath = ".\artisans-before-clean.bin" }
)

$layout = Detect-DiscLayout $imagePath
$levelReports = @()
$stream = [IO.File]::OpenRead($imagePath)
try {
    foreach ($level in $levels) {
        $entry = @($entries | Where-Object { [int](Get-Field $_ "index" -1) -eq [int]$level.wadEntry } | Select-Object -First 1)[0]
        if ($null -eq $entry) { throw "Could not find WAD entry $($level.wadEntry) in $wadAnalysisPath." }
        [byte[]]$entryBytes = Read-WadBytes $stream $layout ([int64](Get-Field $entry "offset" 0)) ([int](Get-Field $entry "size" 0))
        $levelReports += Analyze-Level $level $entry $entryBytes
    }
}
finally {
    $stream.Dispose()
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    imagePath = (Resolve-Path -LiteralPath $imagePath).Path
    wadAnalysisPath = (Resolve-Path -LiteralPath $wadAnalysisPath).Path
    conclusion = "Initial true-add probe. Immediate append is only safe if there is slack after the known source table and a count/allocation mechanism can be patched."
    levels = $levelReports
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJsonPath -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Spyro Moby Table Expansion Report")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- Goal: determine whether true new moby records can be appended safely.")
[void]$lines.Add("")
[void]$lines.Add("## Summary")
[void]$lines.Add("")
[void]$lines.Add("| level | source records | runtime records | table end | owning subfile | bytes to subfile end | zero run after table | plausible records after | immediate append? |")
[void]$lines.Add("|---|---:|---:|---:|---:|---:|---:|---:|---|")
foreach ($level in $levelReports) {
    $ownerText = if ($null -eq $level.owningSubfile) { "" } else { "subfile " + $level.owningSubfile.index }
    [void]$lines.Add("| $($level.displayName) | $($level.sourceRecordCount) | $($level.runtime.runtimeCount) | $($level.tableEndWadOffset) | $ownerText | $($level.bytesToOwningSubfileEnd) | $($level.zeroRunAfterTable) | $($level.plausibleRecordsImmediatelyAfter) | $($level.immediateAppendLooksSafe) |")
}
[void]$lines.Add("")
[void]$lines.Add("## Findings")
[void]$lines.Add("")
foreach ($level in $levelReports) {
    [void]$lines.Add("### $($level.displayName)")
    [void]$lines.Add("")
    [void]$lines.Add("- Source table: WAD entry $($level.wadEntry), rel $($level.tableRelativeOffset), WAD $($level.tableWadOffset).")
    [void]$lines.Add("- Source count currently mapped: $($level.sourceRecordCount); runtime count from cached RAM: $($level.runtime.runtimeCount).")
    [void]$lines.Add("- Table end: $($level.tableEndWadOffset); bytes to owning subfile end: $($level.bytesToOwningSubfileEnd); zero run after table: $($level.zeroRunAfterTable).")
    [void]$lines.Add("- Immediate append verdict: $($level.immediateAppendLooksSafe).")
    if (@($level.runtime.extraRuntimeRecords).Count -gt 0) {
        [void]$lines.Add("- Runtime records beyond the mapped source table:")
        foreach ($record in @($level.runtime.extraRuntimeRecords | Select-Object -First 8)) {
            [void]$lines.Add("  - T$($record.index): type $($record.typeHex), state $($record.stateHex), flags $($record.flag52Hex)/$($record.flag53Hex), XYZ $($record.x), $($record.y), $($record.z)")
        }
    }
    if (@($level.recordsAfterTable).Count -gt 0) {
        [void]$lines.Add("- First records/bytes immediately after mapped source table:")
        foreach ($record in @($level.recordsAfterTable | Select-Object -First 4)) {
            [void]$lines.Add("  - slot+$($record.index - $level.sourceRecordCount) as T$($record.index): plausible=$($record.plausible), type $($record.typeHex), state $($record.stateHex), flags $($record.flag52Hex)/$($record.flag53Hex), raw XYZ $($record.rawX), $($record.rawY), $($record.rawZ)")
        }
    }
    if (@($level.nearbyCountNeedleHits).Count -gt 0) {
        [void]$lines.Add("- Nearby count-like byte hits within 2 KB before/256 bytes after table:")
        foreach ($hit in @($level.nearbyCountNeedleHits | Select-Object -First 12)) {
            [void]$lines.Add("  - $($hit.kind) $($hit.value) at rel $($hit.relativeOffset), distance $($hit.signedDistanceToTable)")
        }
    }
    [void]$lines.Add("")
}
[void]$lines.Add("## Next Experiments")
[void]$lines.Add("")
[void]$lines.Add("1. If no slack exists after a source table, do not append in-place; use slot reuse until a relocation/repack path is proven.")
[void]$lines.Add("2. Find where the loader derives runtime count. Candidate fields are not trusted until a patched count changes `_ptrDynamicLevelMobys - _ptrLevelMobys` after a fresh load.")
[void]$lines.Add("3. If a level has unmapped runtime records beyond the source table, locate their source before trying table expansion; they may be generated from a second list.")
[void]$lines.Add("4. Once the count source is known, create a one-record append test with a harmless gem donor and validate fresh-load runtime count plus pickup behavior.")

$lines | Set-Content -LiteralPath $outMarkdownPath -Encoding UTF8
Write-Host "Wrote moby table expansion report to $outJsonPath"
Write-Host "Wrote summary to $outMarkdownPath"
