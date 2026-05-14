param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$SubfileIndex = 3,
    [string]$OutPath = ".\stonehill-wad-packed-line-overlay.json",
    [int]$StartOffset = 0x10,
    [int]$RecordStride = 8,
    [int]$MaxRecords = 12000,
    [int]$MaxFlagGroups = 6
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
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
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) { $firstDataOffset = $Bytes.Length }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -lt 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-FlagGroups([byte[]]$Bytes, [int]$CandidateStartOffset, [int]$CandidateEndOffset, [int]$LimitRecords) {
    $counts = @{}
    $safeEnd = [Math]::Min($Bytes.Length, $CandidateEndOffset)
    $recordLimit = [Math]::Min($LimitRecords, [Math]::Floor(($safeEnd - $CandidateStartOffset) / $RecordStride))
    for ($i = 0; $i -lt $recordLimit; $i++) {
        $offset = $CandidateStartOffset + ($i * $RecordStride)
        if (($offset + 8) -gt $Bytes.Length) { break }
        $h0 = Get-UInt16LE $Bytes $offset
        $h1 = Get-UInt16LE $Bytes ($offset + 2)
        $h2 = Get-UInt16LE $Bytes ($offset + 4)
        $h3 = Get-UInt16LE $Bytes ($offset + 6)
        if (($h0 -eq 0 -and $h1 -eq 0 -and $h2 -eq 0 -and $h3 -eq 0) -or
            ($h0 -eq 0xFFFF -and $h1 -eq 0xFFFF -and $h2 -eq 0xFFFF -and $h3 -eq 0xFFFF)) {
            continue
        }
        $flags = "{0}-{1}-{2}-{3}" -f ($h0 -shr 13), ($h1 -shr 13), ($h2 -shr 13), ($h3 -shr 13)
        if (-not $counts.ContainsKey($flags)) { $counts[$flags] = 0 }
        $counts[$flags]++
    }
    return @($counts.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 10 | ForEach-Object {
        [ordered]@{ pattern = $_.Key; count = [int]$_.Value }
    })
}

function New-PackedCandidate([byte[]]$Bytes, [int]$Mask, [double]$Scale, [bool]$SwapAxes, [string]$Name, [int]$CandidateStartOffset, [int]$CandidateEndOffset, [string]$FlagPattern = "") {
    $points = @()
    $edges = @()
    $safeEnd = [Math]::Min($Bytes.Length, $CandidateEndOffset)
    $recordLimit = [Math]::Min($MaxRecords, [Math]::Floor(($safeEnd - $CandidateStartOffset) / $RecordStride))

    for ($i = 0; $i -lt $recordLimit; $i++) {
        $offset = $CandidateStartOffset + ($i * $RecordStride)
        if (($offset + 8) -gt $Bytes.Length) { break }
        $h0 = Get-UInt16LE $Bytes $offset
        $h1 = Get-UInt16LE $Bytes ($offset + 2)
        $h2 = Get-UInt16LE $Bytes ($offset + 4)
        $h3 = Get-UInt16LE $Bytes ($offset + 6)
        $flags = "{0}-{1}-{2}-{3}" -f ($h0 -shr 13), ($h1 -shr 13), ($h2 -shr 13), ($h3 -shr 13)
        if ($FlagPattern.Length -gt 0 -and $flags -ne $FlagPattern) { continue }
        if (($h0 -eq 0 -and $h1 -eq 0 -and $h2 -eq 0 -and $h3 -eq 0) -or
            ($h0 -eq 0xFFFF -and $h1 -eq 0xFFFF -and $h2 -eq 0xFFFF -and $h3 -eq 0xFFFF)) {
            continue
        }

        $x1 = [double]($h0 -band $Mask) * $Scale
        $y1 = [double]($h1 -band $Mask) * $Scale
        $x2 = [double]($h2 -band $Mask) * $Scale
        $y2 = [double]($h3 -band $Mask) * $Scale
        if (($x1 -eq 0 -and $y1 -eq 0 -and $x2 -eq 0 -and $y2 -eq 0) -or
            $x1 -gt 20000 -or $x2 -gt 20000 -or $y1 -gt 20000 -or $y2 -gt 20000) {
            continue
        }

        if ($SwapAxes) {
            $tmp = $x1; $x1 = $y1; $y1 = $tmp
            $tmp = $x2; $x2 = $y2; $y2 = $tmp
        }
        $a = $points.Count
        $points += [ordered]@{
            x = [Math]::Round($x1, 3)
            y = [Math]::Round($y1, 3)
            z = 0
            rawX = ("0x{0:X4}" -f $h0)
            rawY = ("0x{0:X4}" -f $h1)
        }
        $b = $points.Count
        $points += [ordered]@{
            x = [Math]::Round($x2, 3)
            y = [Math]::Round($y2, 3)
            z = 0
            rawX = ("0x{0:X4}" -f $h2)
            rawY = ("0x{0:X4}" -f $h3)
        }
        if ($x1 -ne $x2 -or $y1 -ne $y2) {
            $edges += [ordered]@{
                a = $a
                b = $b
                x1 = [Math]::Round($x1, 3)
                y1 = [Math]::Round($y1, 3)
                x2 = [Math]::Round($x2, 3)
                y2 = [Math]::Round($y2, 3)
                source = "wad-packed-record"
                recordIndex = $i
                recordOffset = ("0x{0:X}" -f $offset)
                flags = $flags
            }
        }
    }

    if ($points.Count -eq 0) { return $null }
    $xs = @($points | ForEach-Object { $_.x })
    $ys = @($points | ForEach-Object { $_.y })
    return [ordered]@{
        runtimeAddress = ("wad-packed-lines {0} subfile {1} mask 0x{2:X} scale {3} {4}{5}" -f $Name, $SubfileIndex, $Mask, $Scale, $(if ($SwapAxes) { "yx" } else { "xy" }), $(if ($FlagPattern.Length -gt 0) { " flags $FlagPattern" } else { "" }))
        encoding = "wad-packed-halfword-lines"
        stride = $RecordStride
        sectionName = $Name
        sectionStartOffset = ("0x{0:X}" -f $CandidateStartOffset)
        sectionEndOffset = ("0x{0:X}" -f $safeEnd)
        flagPattern = $FlagPattern
        validVertices = $points.Count
        score = $edges.Count
        projection = $(if ($SwapAxes) { "yx" } else { "xy" })
        fitScore = $edges.Count
        mobysInsideProjection = 0
        projectedBounds = [ordered]@{
            minX = ($xs | Measure-Object -Minimum).Minimum
            maxX = ($xs | Measure-Object -Maximum).Maximum
            minY = ($ys | Measure-Object -Minimum).Minimum
            maxY = ($ys | Measure-Object -Maximum).Maximum
        }
        projectedPoints = $points
        points = $points
        edges = $edges
        edgeSource = "wad-packed-halfword-lines"
        edgeFaceCount = $edges.Count
    }
}

$stream = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $ImagePath))
try {
    $wadHead = Read-WadBytes $stream $WadLba 0 4096
    $wadEntries = Parse-ArchiveHeader $wadHead 110260224
    $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
    if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }

    $assetHead = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
    $subfiles = Parse-ArchiveHeader $assetHead $assetEntry.size
    $subfile = @($subfiles | Where-Object { $_.index -eq $SubfileIndex })[0]
    if ($null -eq $subfile) { throw "Could not find subfile $SubfileIndex in WAD entry $AssetWadIndex." }

    $bytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $subfile.offset) ([int]$subfile.size)
    $headerTotalSize = [int](Get-UInt32LE $bytes 0)
    $headerTailOffset = [int](Get-UInt32LE $bytes 4)
    $headerMidOffset = [int](Get-UInt32LE $bytes 8)

    $sections = @(
        [ordered]@{ name = "header-lines 0x10..0x78"; start = 0x10; end = 0x78; maxRecords = 64 },
        [ordered]@{ name = "early-table 0x80..0x255C"; start = 0x80; end = $headerMidOffset; maxRecords = 1600 },
        [ordered]@{ name = "middle-table 0x255C..0xB800"; start = $headerMidOffset; end = $headerTailOffset; maxRecords = 3600 },
        [ordered]@{ name = "tail-table 0xB800..end"; start = $headerTailOffset; end = [Math]::Min($bytes.Length, $headerTotalSize); maxRecords = 2600 },
        [ordered]@{ name = "all-records"; start = $StartOffset; end = [Math]::Min($bytes.Length, $headerTotalSize); maxRecords = $MaxRecords }
    )

    $candidates = @()
    foreach ($section in $sections) {
        $oldMax = $script:MaxRecords
        $script:MaxRecords = [int]$section.maxRecords
        foreach ($mask in @(0x0FFF, 0x1FFF, 0x3FFF)) {
            foreach ($scale in @(1.0, 2.0)) {
                $candidates += (New-PackedCandidate $bytes $mask $scale $false ([string]$section.name) ([int]$section.start) ([int]$section.end) "")
                $candidates += (New-PackedCandidate $bytes $mask $scale $true ([string]$section.name) ([int]$section.start) ([int]$section.end) "")
            }
        }

        $enableFlagGroups = ([string]$section.name -match "middle|tail")
        $flagGroups = @()
        if ($enableFlagGroups) {
            $flagGroups = @(Get-FlagGroups $bytes ([int]$section.start) ([int]$section.end) ([int]$section.maxRecords) | Where-Object { $_.count -ge 8 } | Select-Object -First $MaxFlagGroups)
        }
        foreach ($group in $flagGroups) {
            foreach ($mask in @(0x1FFF)) {
                $candidates += (New-PackedCandidate $bytes $mask 1.0 $false ("$($section.name) flag $($group.pattern)") ([int]$section.start) ([int]$section.end) ([string]$group.pattern)
                )
                $candidates += (New-PackedCandidate $bytes $mask 1.0 $true ("$($section.name) flag $($group.pattern)") ([int]$section.start) ([int]$section.end) ([string]$group.pattern)
                )
            }
        }
        $script:MaxRecords = $oldMax
    }
    $candidates = @($candidates | Where-Object { $null -ne $_ })

    $result = [ordered]@{
        sourceImage = (Resolve-Path -LiteralPath $ImagePath).Path
        generatedAt = (Get-Date).ToString("s")
        note = "Experimental Stone Hill WAD packed-line overlay. Treats subfile halfword high bits as flags and low bits as 2D line endpoint coordinates. This is a format probe, not verified terrain."
        assetWadIndex = $AssetWadIndex
        subfileIndex = $SubfileIndex
        subfileOffset = $subfile.offset
        subfileSize = $subfile.size
        startOffset = $StartOffset
        recordStride = $RecordStride
        headerTotalSize = $headerTotalSize
        headerMidOffset = $headerMidOffset
        headerTailOffset = $headerTailOffset
        sections = $sections
        candidates = $candidates
    }
    $resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
    Write-Host "Wrote WAD packed line overlay to $resolvedOut"
    $candidates | Select-Object runtimeAddress, validVertices, @{n="Edges";e={@($_.edges).Count}}, @{n="Bounds";e={"$($_.projectedBounds.minX),$($_.projectedBounds.minY)..$($_.projectedBounds.maxX),$($_.projectedBounds.maxY)"}} | Format-Table -AutoSize
}
finally {
    $stream.Dispose()
}
