param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutPath = ".\stonehill-texture-records-contact-sheet.png",
    [string]$OutJsonPath = ".\stonehill-texture-records-contact-sheet.json",
    [int]$CellWidth = 144,
    [int]$CellHeight = 176,
    [int]$Columns = 8
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

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
        $entries += [pscustomobject]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
}

function Get-ModelSubfileBytes([string]$ImagePath, [int]$WadLba, [int]$AssetWadIndex, [int]$ModelSubfileIndex) {
    $stream = [System.IO.File]::OpenRead($ImagePath)
    try {
        $wadHeader = Read-WadBytes $stream $WadLba 0 4096
        $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
        $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
        if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }
        $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
        $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
        $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex })[0]
        if ($null -eq $modelSubfile) { throw "Could not find model subfile $ModelSubfileIndex." }
        $bytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)
        return [pscustomobject]@{
            bytes = $bytes
            assetOffset = [int64]$assetEntry.offset
            assetSize = [int64]$assetEntry.size
            subfileOffset = [int64]$modelSubfile.offset
            subfileSize = [int64]$modelSubfile.size
        }
    }
    finally {
        $stream.Close()
    }
}

function Get-Nibble([byte[]]$Bytes, [int]$NibbleIndex) {
    $byte = [int]$Bytes[[int][Math]::Floor($NibbleIndex / 2)]
    if (($NibbleIndex % 2) -eq 0) { return ($byte -band 0x0F) }
    return (($byte -shr 4) -band 0x0F)
}

function Get-RampColor([int]$Value, [int]$MaxValue) {
    $t = if ($MaxValue -le 0) { 0.0 } else { [double]$Value / [double]$MaxValue }
    $r = [int](40 + (210 * $t))
    $g = [int](48 + (170 * (1.0 - [Math]::Abs($t - 0.55))))
    $b = [int](55 + (190 * (1.0 - $t)))
    return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

function Draw-4BppImage($Graphics, [byte[]]$Record, [int]$ByteOffset, [int]$Width, [int]$Height, [int]$Left, [int]$Top, [int]$Scale) {
    $totalPixels = $Width * $Height
    for ($i = 0; $i -lt $totalPixels; $i++) {
        $nibbleIndex = ($ByteOffset * 2) + $i
        if ([int][Math]::Floor($nibbleIndex / 2) -ge $Record.Length) { break }
        $value = Get-Nibble $Record $nibbleIndex
        $brush = New-Object System.Drawing.SolidBrush((Get-RampColor $value 15))
        $x = $Left + (($i % $Width) * $Scale)
        $y = $Top + ([int][Math]::Floor($i / $Width) * $Scale)
        $Graphics.FillRectangle($brush, $x, $y, $Scale, $Scale)
        $brush.Dispose()
    }
}

function Draw-8BppImage($Graphics, [byte[]]$Record, [int]$ByteOffset, [int]$Width, [int]$Height, [int]$Left, [int]$Top, [int]$Scale) {
    $totalPixels = $Width * $Height
    for ($i = 0; $i -lt $totalPixels; $i++) {
        $byteIndex = $ByteOffset + $i
        if ($byteIndex -ge $Record.Length) { break }
        $value = [int]$Record[$byteIndex]
        $brush = New-Object System.Drawing.SolidBrush((Get-RampColor $value 255))
        $x = $Left + (($i % $Width) * $Scale)
        $y = $Top + ([int][Math]::Floor($i / $Width) * $Scale)
        $Graphics.FillRectangle($brush, $x, $y, $Scale, $Scale)
        $brush.Dispose()
    }
}

function Format-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $items = @()
    for ($i = 0; $i -lt $Length; $i++) { $items += ("{0:X2}" -f [int]$Bytes[$Offset + $i]) }
    return [string]::Join(" ", $items)
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$modelBytes = [byte[]]$modelInfo.bytes
$textureListSize = [int](Get-UInt32LE $modelBytes 0)
$textureCount = [int](Get-UInt32LE $modelBytes 4)
if ($textureListSize -le 8 -or $textureCount -le 0) { throw "No plausible texture list found." }
$recordBytes = [int](($textureListSize - 8) / $textureCount)
if ((($textureListSize - 8) % $textureCount) -ne 0) { throw "Texture list is not an even fixed-record table." }

$rows = [int][Math]::Ceiling($textureCount / [double]$Columns)
$sheetWidth = $Columns * $CellWidth
$sheetHeight = $rows * $CellHeight
$bitmap = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.Clear([System.Drawing.Color]::FromArgb(28, 31, 35))
$font = New-Object System.Drawing.Font("Consolas", 7)
$titleFont = New-Object System.Drawing.Font("Segoe UI", 8)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 244, 245, 240))
$mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(185, 210, 215, 210))
$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), 1)

$records = New-Object System.Collections.Generic.List[object]
for ($texture = 0; $texture -lt $textureCount; $texture++) {
    $recordOffset = 8 + ($texture * $recordBytes)
    $record = New-Object byte[] $recordBytes
    [Array]::Copy($modelBytes, $recordOffset, $record, 0, $recordBytes)
    [void]$records.Add([ordered]@{
        index = $texture
        offset = ("0x{0:X}" -f $recordOffset)
        first32 = Format-Bytes $record 0 ([Math]::Min(32, $recordBytes))
    })

    $col = $texture % $Columns
    $row = [int][Math]::Floor($texture / $Columns)
    $left = $col * $CellWidth
    $top = $row * $CellHeight
    $graphics.DrawRectangle($framePen, $left + 3, $top + 3, $CellWidth - 6, $CellHeight - 6)
    $graphics.DrawString(("T{0:00} @ {1}" -f $texture, ("0x{0:X}" -f $recordOffset)), $titleFont, $textBrush, $left + 7, $top + 6)

    # Candidate A: first 128 bytes as 16x16 4bpp, then common mip sizes.
    Draw-4BppImage $graphics $record 0 16 16 ($left + 8) ($top + 24) 3
    Draw-4BppImage $graphics $record 128 8 8 ($left + 62) ($top + 24) 3
    Draw-4BppImage $graphics $record 160 4 4 ($left + 91) ($top + 24) 3
    Draw-4BppImage $graphics $record 168 2 2 ($left + 108) ($top + 24) 3
    $graphics.DrawString("4bpp mip", $font, $mutedBrush, $left + 8, $top + 76)

    # Candidate B: the whole record as 16x23 4bpp.
    Draw-4BppImage $graphics $record 0 16 23 ($left + 8) ($top + 92) 2
    $graphics.DrawString("4bpp 16x23", $font, $mutedBrush, $left + 44, $top + 92)

    # Candidate C: first 176 bytes as 16x11 8bpp.
    Draw-8BppImage $graphics $record 0 16 11 ($left + 84) ($top + 92) 2
    $graphics.DrawString("8bpp", $font, $mutedBrush, $left + 84, $top + 117)

    $graphics.DrawString((Format-Bytes $record 0 8), $font, $mutedBrush, $left + 8, $top + 146)
    $graphics.DrawString((Format-Bytes $record 8 8), $font, $mutedBrush, $left + 8, $top + 158)
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$bitmap.Save($resolvedOut, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$font.Dispose()
$titleFont.Dispose()
$textBrush.Dispose()
$mutedBrush.Dispose()
$framePen.Dispose()

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    wadLba = $WadLba
    assetWadIndex = $AssetWadIndex
    modelSubfileIndex = $ModelSubfileIndex
    textureListSize = $textureListSize
    textureCount = $textureCount
    recordBytes = $recordBytes
    outputImage = $resolvedOut
    renderInterpretations = @(
        "first 128 bytes as 16x16 4bpp plus 8x8/4x4/2x2 mip probes",
        "whole 184-byte record as 16x23 4bpp",
        "first 176 bytes as 16x11 8bpp"
    )
    records = @($records.ToArray())
}
$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

Write-Host ("Wrote texture-record contact sheet to {0}" -f $resolvedOut)
Write-Host ("Texture records: {0} x {1} bytes" -f $textureCount, $recordBytes)
