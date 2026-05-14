param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [string]$OutPath = ".\stonehill-wad-coordinate-layouts.json",
    [int]$MaxLayoutsPerSubfile = 12,
    [int[]]$SubfileIndex = @(1, 2, 3, 4, 5, 6, 7, 8),
    [int]$MaxScanBytes = 131072,
    [int]$StartStep = 16
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt16($Bytes, $Offset)
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

function Test-Coord([int]$Value) {
    return ($Value -ge -4096 -and $Value -le 16384)
}

function Test-RejectFill([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $end = [Math]::Min($Bytes.Length, $Offset + $Length)
    $ff = 0; $zero = 0; $same = 0
    for ($i = $Offset; $i -lt $end; $i++) {
        if ($Bytes[$i] -eq 0xFF) { $ff++ }
        if ($Bytes[$i] -eq 0x00) { $zero++ }
        if ($Bytes[$i] -eq 0x01 -or $Bytes[$i] -eq 0x02 -or $Bytes[$i] -eq 0x1E) { $same++ }
    }
    $count = [Math]::Max(1, $end - $Offset)
    return (($ff / [double]$count) -gt 0.55 -or ($zero / [double]$count) -gt 0.75 -or ($same / [double]$count) -gt 0.70)
}

function Scan-CoordinateLayouts([byte[]]$Bytes, [int]$MaxLayouts) {
    $layouts = @()
    foreach ($stride in @(6, 8, 12, 16, 24, 32)) {
        $wordOffsets = @(0..([Math]::Floor(($stride - 2) / 2)) | ForEach-Object { $_ * 2 })
        for ($start = 0; $start -lt [Math]::Min($Bytes.Length - ($stride * 32), $script:MaxScanBytes); $start += $script:StartStep) {
            foreach ($triple in @(
                @(0, 2, 4), @(0, 4, 8), @(0, 4, 6), @(0, 8, 12),
                @(2, 4, 6), @(4, 0, 2), @(4, 2, 0), @(8, 0, 4),
                @(10, 14, 8), @(0, 8, 2), @(0, 6, 14)
            )) {
                $xOff = $triple[0]; $yOff = $triple[1]; $zOff = $triple[2]
                if ($xOff -gt ($stride - 2) -or $yOff -gt ($stride - 2) -or $zOff -gt ($stride - 2)) { continue }
                $points = 0
                $badRun = 0
                $unique = New-Object 'System.Collections.Generic.HashSet[string]'
                $minX = 999999; $maxX = -999999
                $minY = 999999; $maxY = -999999
                $minZ = 999999; $maxZ = -999999
                for ($i = 0; $i -lt 512; $i++) {
                    $pos = $start + ($i * $stride)
                    if (($pos + $stride) -gt $Bytes.Length) { break }
                    $x = Get-Int16LE $Bytes ($pos + $xOff)
                    $y = Get-Int16LE $Bytes ($pos + $yOff)
                    $z = Get-Int16LE $Bytes ($pos + $zOff)
                    if (Test-Coord $x -and Test-Coord $y -and Test-Coord $z -and -not ($x -eq -1 -and $y -eq -1 -and $z -eq -1)) {
                        $points++
                        $badRun = 0
                        [void]$unique.Add("$x,$y,$z")
                        if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                        if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
                        if ($z -lt $minZ) { $minZ = $z }; if ($z -gt $maxZ) { $maxZ = $z }
                    }
                    else {
                        $badRun++
                        if ($points -eq 0 -and $badRun -gt 8) { break }
                        if ($points -gt 24 -and $badRun -gt 12) { break }
                    }
                }
                if ($points -lt 40 -or $unique.Count -lt 24) { continue }
                if (Test-RejectFill $Bytes $start ([Math]::Min(4096, $points * $stride))) { continue }
                $spanX = $maxX - $minX
                $spanY = $maxY - $minY
                $spanZ = $maxZ - $minZ
                if ($spanX -lt 512 -or $spanY -lt 512) { continue }
                $score = ($points * 8) + $unique.Count + [Math]::Min(2000, ($spanX + $spanY + $spanZ) / 24)
                $layouts += [pscustomobject][ordered]@{
                    offset = $start
                    stride = $stride
                    xOffset = $xOff
                    yOffset = $yOff
                    zOffset = $zOff
                    points = $points
                    uniquePoints = $unique.Count
                    score = [Math]::Round($score, 2)
                    bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
                }
            }
        }
    }
    return @($layouts | Sort-Object -Property score, points -Descending | Select-Object -First $MaxLayouts)
}

$stream = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $ImagePath))
try {
    $wadHead = Read-WadBytes $stream $WadLba 0 4096
    $wadEntries = Parse-ArchiveHeader $wadHead 110260224
    $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
    if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }

    $assetHead = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
    $subfiles = Parse-ArchiveHeader $assetHead $assetEntry.size
    $results = @()
    foreach ($subfile in $subfiles) {
        if ($SubfileIndex.Count -gt 0 -and -not ($SubfileIndex -contains [int]$subfile.index)) { continue }
        if ($subfile.size -lt 4096) { continue }
        $length = [Math]::Min([int]$subfile.size, $MaxScanBytes)
        $bytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $subfile.offset) $length
        $layouts = @(Scan-CoordinateLayouts $bytes $MaxLayoutsPerSubfile)
        if ($layouts.Count -gt 0) {
            $results += [ordered]@{
                subfileIndex = $subfile.index
                subfileOffset = $subfile.offset
                subfileSize = $subfile.size
                scannedBytes = $length
                firstWords = @(
                    ("0x{0:X8}" -f (Get-UInt32LE $bytes 0)),
                    ("0x{0:X8}" -f (Get-UInt32LE $bytes 4)),
                    ("0x{0:X8}" -f (Get-UInt32LE $bytes 8)),
                    ("0x{0:X8}" -f (Get-UInt32LE $bytes 12))
                )
                layouts = $layouts
            }
        }
    }

    $result = [ordered]@{
        sourceImage = (Resolve-Path -LiteralPath $ImagePath).Path
        generatedAt = (Get-Date).ToString("s")
        note = "Heuristic scan of Stone Hill asset WAD subfiles for direct int16 coordinate-like layouts. Strong results still require visual and RAM/WAD validation."
        assetWadIndex = $AssetWadIndex
        assetOffset = $assetEntry.offset
        assetSize = $assetEntry.size
        subfilesWithLayouts = $results
    }
    $resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

    Write-Host "Wrote WAD coordinate layout report to $resolvedOut"
    foreach ($entry in $results) {
        Write-Host "Subfile $($entry.subfileIndex) offset=$($entry.subfileOffset) size=$($entry.subfileSize)"
        $entry.layouts | Select-Object -First 5 offset, stride, xOffset, yOffset, zOffset, points, uniquePoints, score | Format-Table -AutoSize
    }
}
finally {
    $stream.Dispose()
}
