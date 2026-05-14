param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutPath = ".\stonehill-texture-record-coordinates.png",
    [string]$OutJsonPath = ".\stonehill-texture-record-coordinates.json",
    [int]$CellWidth = 144,
    [int]$CellHeight = 160,
    [int]$Columns = 8
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

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
        return [pscustomobject]@{ bytes = $bytes; subfileOffset = [int64]$modelSubfile.offset; subfileSize = [int64]$modelSubfile.size }
    }
    finally {
        $stream.Close()
    }
}

function Add-Count($Map, [string]$Key) {
    if ($Map.ContainsKey($Key)) { $Map[$Key] = [int]$Map[$Key] + 1 }
    else { $Map[$Key] = 1 }
}

function Convert-TopCounts($Map, [int]$Limit) {
    $rows = @()
    foreach ($key in $Map.Keys) {
        $rows += [pscustomobject]@{ value = $key; count = [int]$Map[$key] }
    }
    return @($rows | Sort-Object -Property @{ Expression = { $_.count }; Descending = $true }, value | Select-Object -First $Limit)
}

function Convert-ToCellPoint([int]$U, [int]$V, [int]$Left, [int]$Top, [int]$Width, [int]$Height) {
    $pad = 14
    return [System.Drawing.PointF]::new(
        [float]($Left + $pad + (($Width - ($pad * 2)) * ([double]$U / 255.0))),
        [float]($Top + $pad + (($Height - ($pad * 2)) * ([double]$V / 255.0)))
    )
}

function Get-MetaColor([int]$Meta) {
    $h = (($Meta * 1103515245 + 12345) -band 0x7FFFFFFF)
    $r = 80 + ($h -band 0x7F)
    $g = 80 + (($h -shr 7) -band 0x7F)
    $b = 80 + (($h -shr 14) -band 0x7F)
    return [System.Drawing.Color]::FromArgb(210, $r, $g, $b)
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$modelBytes = [byte[]]$modelInfo.bytes
$textureListSize = [int](Get-UInt32LE $modelBytes 0)
$textureCount = [int](Get-UInt32LE $modelBytes 4)
if ($textureListSize -le 8 -or $textureCount -le 0) { throw "No plausible texture list found." }
$recordBytes = [int](($textureListSize - 8) / $textureCount)
if ((($textureListSize - 8) % $textureCount) -ne 0) { throw "Texture list is not an even fixed-record table." }
$chunkCount = [int]($recordBytes / 8)

$meta0Top = @{}
$meta1Top = @{}
$pairTop = @{}
$recordSummaries = New-Object System.Collections.Generic.List[object]
$rows = [int][Math]::Ceiling($textureCount / [double]$Columns)
$sheetWidth = $Columns * $CellWidth
$sheetHeight = $rows * $CellHeight
$bitmap = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(25, 28, 31))
$font = New-Object System.Drawing.Font("Consolas", 7)
$titleFont = New-Object System.Drawing.Font("Segoe UI", 8)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 244, 245, 240))
$gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(42, 255, 255, 255), 1)
$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 255, 255, 255), 1)

for ($texture = 0; $texture -lt $textureCount; $texture++) {
    $recordOffset = 8 + ($texture * $recordBytes)
    $col = $texture % $Columns
    $row = [int][Math]::Floor($texture / $Columns)
    $left = $col * $CellWidth
    $top = $row * $CellHeight
    $plotLeft = $left + 8
    $plotTop = $top + 24
    $plotWidth = $CellWidth - 16
    $plotHeight = $CellHeight - 54
    $graphics.DrawRectangle($framePen, $left + 3, $top + 3, $CellWidth - 6, $CellHeight - 6)
    $graphics.DrawString(("T{0:00}" -f $texture), $titleFont, $textBrush, $left + 7, $top + 6)
    for ($g = 0; $g -le 4; $g++) {
        $gx = $plotLeft + [int](($plotWidth * $g) / 4)
        $gy = $plotTop + [int](($plotHeight * $g) / 4)
        $graphics.DrawLine($gridPen, $gx, $plotTop, $gx, $plotTop + $plotHeight)
        $graphics.DrawLine($gridPen, $plotLeft, $gy, $plotLeft + $plotWidth, $gy)
    }

    $chunks = New-Object System.Collections.Generic.List[object]
    for ($chunk = 0; $chunk -lt $chunkCount; $chunk++) {
        $offset = $recordOffset + ($chunk * 8)
        $u0 = [int]$modelBytes[$offset]
        $v0 = [int]$modelBytes[$offset + 1]
        $meta0 = [int](Get-UInt16LE $modelBytes ($offset + 2))
        $u1 = [int]$modelBytes[$offset + 4]
        $v1 = [int]$modelBytes[$offset + 5]
        $meta1 = [int](Get-UInt16LE $modelBytes ($offset + 6))
        Add-Count $meta0Top ("0x{0:X4}" -f $meta0)
        Add-Count $meta1Top ("0x{0:X4}" -f $meta1)
        Add-Count $pairTop ("{0:X2},{1:X2}->{2:X2},{3:X2}" -f $u0, $v0, $u1, $v1)
        $color = Get-MetaColor ($meta0 -bxor $meta1)
        $pen = New-Object System.Drawing.Pen($color, 1.5)
        $brush = New-Object System.Drawing.SolidBrush($color)
        $p0 = Convert-ToCellPoint $u0 $v0 $plotLeft $plotTop $plotWidth $plotHeight
        $p1 = Convert-ToCellPoint $u1 $v1 $plotLeft $plotTop $plotWidth $plotHeight
        $graphics.DrawLine($pen, $p0, $p1)
        $graphics.FillEllipse($brush, $p0.X - 1.5, $p0.Y - 1.5, 3, 3)
        $graphics.FillRectangle($brush, $p1.X - 1.5, $p1.Y - 1.5, 3, 3)
        $pen.Dispose()
        $brush.Dispose()
        [void]$chunks.Add([ordered]@{
            chunk = $chunk
            u0 = $u0
            v0 = $v0
            meta0 = ("0x{0:X4}" -f $meta0)
            u1 = $u1
            v1 = $v1
            meta1 = ("0x{0:X4}" -f $meta1)
        })
    }
    $graphics.DrawString(("{0} chunks" -f $chunkCount), $font, $textBrush, $left + 7, $top + $CellHeight - 24)
    $graphics.DrawString(("off 0x{0:X}" -f $recordOffset), $font, $textBrush, $left + 7, $top + $CellHeight - 12)
    [void]$recordSummaries.Add([ordered]@{
        index = $texture
        offset = ("0x{0:X}" -f $recordOffset)
        chunks = @($chunks.ToArray())
    })
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$bitmap.Save($resolvedOut, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$font.Dispose()
$titleFont.Dispose()
$textBrush.Dispose()
$gridPen.Dispose()
$framePen.Dispose()

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    textureListSize = $textureListSize
    textureCount = $textureCount
    recordBytes = $recordBytes
    chunksPerRecord = $chunkCount
    outputImage = $resolvedOut
    chunkInterpretation = "each 8-byte chunk as (u0,v0,meta0,u1,v1,meta1)"
    topMeta0 = @(Convert-TopCounts $meta0Top 24)
    topMeta1 = @(Convert-TopCounts $meta1Top 24)
    topCoordinatePairs = @(Convert-TopCounts $pairTop 24)
    records = @($recordSummaries.ToArray())
}
$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

Write-Host ("Wrote texture-record coordinate sheet to {0}" -f $resolvedOut)
Write-Host ("Texture records: {0}; chunks per record: {1}" -f $textureCount, $chunkCount)
