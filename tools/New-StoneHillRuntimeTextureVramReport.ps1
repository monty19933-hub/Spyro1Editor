param(
    [string]$TextureJsonPath = ".\stonehill-texture-struct-decode-fresh-stonehill.json",
    [string]$VramPath = ".\duckstation-state-gpu-vram-fresh-stonehill.bin",
    [string]$OutJsonPath = ".\stonehill-runtime-texture-vram-report.json",
    [string]$OutMarkdownPath = ".\stonehill-runtime-texture-vram-report.md",
    [string]$OutContactSheetPath = ".\stonehill-runtime-texture-vram-contact-sheet.png",
    [int]$Columns = 8,
    [switch]$UseCloseHq,
    [switch]$SwapTexelBytes
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

function Convert-Psx555ToColor([uint16]$Value) {
    $r = (($Value -band 0x1F) * 255) / 31
    $g = ((($Value -shr 5) -band 0x1F) * 255) / 31
    $b = ((($Value -shr 10) -band 0x1F) * 255) / 31
    return [System.Drawing.Color]::FromArgb(255, [int]$r, [int]$g, [int]$b)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [uint16]($Bytes[$Offset] -bor ($Bytes[$Offset + 1] -shl 8))
}

function Add-Count($Map, [string]$Key) {
    if ($Map.ContainsKey($Key)) { $Map[$Key]++ }
    else { $Map[$Key] = 1 }
}

function Get-FaceCountMap($TextureReport) {
    $map = @{}
    if ($null -ne $TextureReport.sceneFaces -and $null -ne $TextureReport.sceneFaces.topTextures) {
        foreach ($entry in @($TextureReport.sceneFaces.topTextures)) {
            $map[[int]$entry.value] = [int]$entry.count
        }
    }
    return $map
}

function Get-HqGroup($Record, [string]$GroupName) {
    if ($GroupName -eq "hqDataClose" -and ($Record.PSObject.Properties.Name -contains "hqDataClose")) {
        return @($Record.hqDataClose)
    }
    if ($GroupName -eq "hqData" -and ($Record.PSObject.Properties.Name -contains "hqData")) {
        return @($Record.hqData)
    }
    return @()
}

$script:TileX4 = @(0, 32, 0, 32)
$script:TileY4 = @(0, 0, 32, 32)
$script:Matrices = @(
    @( 1,  0,  0,  1),
    @( 0,  1,  1,  0),
    @(-1,  0,  0, -1),
    @( 0, -1,  1,  0),
    @( 0,  1,  1,  0),
    @(-1,  0,  0,  1),
    @( 0, -1, -1,  0),
    @( 1,  0,  0, -1)
)

function Get-TileSample([byte[]]$Vram, $Hq, [bool]$SwapBytes) {
    $paletteByteStart = [int]$Hq.paletteByteStart
    $paletteWords = New-Object System.Collections.Generic.HashSet[string]
    $nonZeroPaletteWords = 0
    $paletteInRange = ($paletteByteStart -ge 0 -and ($paletteByteStart + 512) -le $Vram.Length)
    if ($paletteInRange) {
        for ($i = 0; $i -lt 256; $i++) {
            $word = Get-UInt16LE $Vram ($paletteByteStart + ($i * 2))
            [void]$paletteWords.Add(("0x{0:X4}" -f $word))
            if (($word -band 0x7FFF) -ne 0) { $nonZeroPaletteWords++ }
        }
    }

    $orientation = [int]$Hq.orientation
    if ($orientation -lt 0 -or $orientation -ge $script:Matrices.Count) { $orientation = 0 }
    $matrix = $script:Matrices[$orientation]
    $xx = [int]$matrix[0]
    $xy = [int]$matrix[1]
    $yx = [int]$matrix[2]
    $yy = [int]$matrix[3]
    $srcXStart = [int]$Hq.vramXMin
    $srcYStart = [int]$Hq.vramYMin
    if ($xx -lt 0 -or $xy -lt 0) { $srcXStart += 31 }
    if ($yx -lt 0 -or $yy -lt 0) { $srcYStart += 31 }

    $texelValues = New-Object System.Collections.Generic.HashSet[string]
    $colorValues = New-Object System.Collections.Generic.HashSet[string]
    $nonZeroTexels = 0
    $outOfRangePixels = 0
    $brightnessTotal = 0.0
    $brightnessSamples = 0

    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) {
            $sx = $srcXStart + ($x * $xx) + ($y * $xy)
            $sy = $srcYStart + ($x * $yx) + ($y * $yy)
            if ($sx -lt 0 -or $sx -ge 2048 -or $sy -lt 0 -or $sy -ge 512) {
                $outOfRangePixels++
                continue
            }

            $sampleX = if ($SwapBytes) { $sx -bxor 1 } else { $sx }
            $pixelIndex = [int]$Vram[($sy * 2048) + $sampleX]
            [void]$texelValues.Add([string]$pixelIndex)
            if ($pixelIndex -ne 0) { $nonZeroTexels++ }

            if ($paletteInRange) {
                $color16 = Get-UInt16LE $Vram ($paletteByteStart + ($pixelIndex * 2))
                [void]$colorValues.Add(("0x{0:X4}" -f $color16))
                $r = ($color16 -band 0x1F) / 31.0
                $g = (($color16 -shr 5) -band 0x1F) / 31.0
                $b = (($color16 -shr 10) -band 0x1F) / 31.0
                $brightnessTotal += (($r + $g + $b) / 3.0)
                $brightnessSamples++
            }
        }
    }

    $paletteUnique = if ($paletteInRange) { $paletteWords.Count } else { 0 }
    $texelUnique = $texelValues.Count
    $colorUnique = $colorValues.Count
    $averageBrightness = if ($brightnessSamples -gt 0) { [Math]::Round($brightnessTotal / $brightnessSamples, 4) } else { 0.0 }
    $usable = ($paletteInRange -and $nonZeroPaletteWords -ge 8 -and $paletteUnique -ge 8 -and $texelUnique -ge 4 -and $colorUnique -ge 4 -and $outOfRangePixels -lt 128)

    return [ordered]@{
        index = [int]$Hq.index
        palette = [int]$Hq.palette
        paletteByteStart = $paletteByteStart
        paletteInRange = [bool]$paletteInRange
        paletteUnique = [int]$paletteUnique
        paletteNonZero = [int]$nonZeroPaletteWords
        texelUnique = [int]$texelUnique
        texelNonZero = [int]$nonZeroTexels
        colorUnique = [int]$colorUnique
        outOfRangePixels = [int]$outOfRangePixels
        averageBrightness = $averageBrightness
        usable = [bool]$usable
        vramXMin = [int]$Hq.vramXMin
        vramYMin = [int]$Hq.vramYMin
        orientation = [int]$Hq.orientation
    }
}

function Score-HqGroup([byte[]]$Vram, $Tiles, [bool]$SwapBytes) {
    $tileStats = New-Object System.Collections.Generic.List[object]
    foreach ($tile in @($Tiles)) {
        [void]$tileStats.Add((Get-TileSample $Vram $tile $SwapBytes))
    }

    $validTiles = 0
    $blankPaletteTiles = 0
    $outOfRangeTiles = 0
    $totalColorUnique = 0
    foreach ($tile in @($tileStats.ToArray())) {
        if ([bool]$tile.usable) { $validTiles++ }
        if ([int]$tile.paletteNonZero -eq 0) { $blankPaletteTiles++ }
        if ([int]$tile.outOfRangePixels -gt 0) { $outOfRangeTiles++ }
        $totalColorUnique += [int]$tile.colorUnique
    }

    $tileCount = @($Tiles).Count
    return [ordered]@{
        tileCount = [int]$tileCount
        usableTiles = [int]$validTiles
        blankPaletteTiles = [int]$blankPaletteTiles
        outOfRangeTiles = [int]$outOfRangeTiles
        averageColorUnique = if ($tileCount -gt 0) { [Math]::Round($totalColorUnique / [double]$tileCount, 2) } else { 0.0 }
        loadedLikely = [bool]($validTiles -gt 0)
        completeLikely = [bool]($tileCount -gt 0 -and $validTiles -eq $tileCount)
        tiles = @($tileStats.ToArray())
    }
}

function Draw-TexturePreview($Graphics, [byte[]]$Vram, $Tiles, [int]$Left, [int]$Top, [bool]$SwapBytes, [int]$PreviewSize) {
    $tileCount = @($Tiles).Count
    if ($tileCount -eq 0) { return }
    $grid = if ($tileCount -le 4) { 2 } else { 4 }
    $drawTileSize = [int]($PreviewSize / $grid)
    $sampleStep = [Math]::Max(1, [int](32 / $drawTileSize))

    for ($tileIndex = 0; $tileIndex -lt $tileCount; $tileIndex++) {
        $hq = @($Tiles)[$tileIndex]
        $gridX = $tileIndex % $grid
        $gridY = [int][Math]::Floor($tileIndex / $grid)
        if ($gridY -ge $grid) { break }

        $orientation = [int]$hq.orientation
        if ($orientation -lt 0 -or $orientation -ge $script:Matrices.Count) { $orientation = 0 }
        $matrix = $script:Matrices[$orientation]
        $xx = [int]$matrix[0]
        $xy = [int]$matrix[1]
        $yx = [int]$matrix[2]
        $yy = [int]$matrix[3]
        $srcXStart = [int]$hq.vramXMin
        $srcYStart = [int]$hq.vramYMin
        if ($xx -lt 0 -or $xy -lt 0) { $srcXStart += 31 }
        if ($yx -lt 0 -or $yy -lt 0) { $srcYStart += 31 }
        $paletteByteStart = [int]$hq.paletteByteStart
        $paletteInRange = ($paletteByteStart -ge 0 -and ($paletteByteStart + 512) -le $Vram.Length)

        for ($dy = 0; $dy -lt $drawTileSize; $dy++) {
            for ($dx = 0; $dx -lt $drawTileSize; $dx++) {
                $x = [Math]::Min(31, $dx * $sampleStep)
                $y = [Math]::Min(31, $dy * $sampleStep)
                $sx = $srcXStart + ($x * $xx) + ($y * $xy)
                $sy = $srcYStart + ($x * $yx) + ($y * $yy)
                $color = [System.Drawing.Color]::FromArgb(255, 18, 22, 25)
                if ($sx -ge 0 -and $sx -lt 2048 -and $sy -ge 0 -and $sy -lt 512 -and $paletteInRange) {
                    $sampleX = if ($SwapBytes) { $sx -bxor 1 } else { $sx }
                    $pixelIndex = [int]$Vram[($sy * 2048) + $sampleX]
                    $color16 = Get-UInt16LE $Vram ($paletteByteStart + ($pixelIndex * 2))
                    $color = Convert-Psx555ToColor $color16
                }
                $brush = New-Object System.Drawing.SolidBrush($color)
                $Graphics.FillRectangle($brush, $Left + ($gridX * $drawTileSize) + $dx, $Top + ($gridY * $drawTileSize) + $dy, 1, 1)
                $brush.Dispose()
            }
        }
    }
}

function New-StatusBrush($Status) {
    switch ($Status) {
        "complete" { return New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 38, 92, 63)) }
        "partial" { return New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 112, 91, 38)) }
        default { return New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 86, 44, 48)) }
    }
}

$resolvedTextureJson = (Resolve-Path -LiteralPath $TextureJsonPath -ErrorAction Stop).Path
$resolvedVram = (Resolve-Path -LiteralPath $VramPath -ErrorAction Stop).Path
$textureReport = Get-Content -LiteralPath $resolvedTextureJson -Raw | ConvertFrom-Json
$vram = [System.IO.File]::ReadAllBytes($resolvedVram)
if ($vram.Length -lt (1024 * 512 * 2)) { throw "VRAM blob must be at least 1,048,576 bytes." }

$faceCounts = Get-FaceCountMap $textureReport
$records = New-Object System.Collections.Generic.List[object]
foreach ($record in @($textureReport.textureRecords.records)) {
    $textureId = [int]$record.textureId
    $hqTiles = Get-HqGroup $record "hqData"
    $closeTiles = Get-HqGroup $record "hqDataClose"
    $hqScore = Score-HqGroup $vram $hqTiles ([bool]$SwapTexelBytes)
    $closeScore = Score-HqGroup $vram $closeTiles ([bool]$SwapTexelBytes)
    $faceCount = if ($faceCounts.ContainsKey($textureId)) { [int]$faceCounts[$textureId] } else { 0 }

    [void]$records.Add([ordered]@{
        textureId = $textureId
        faceCount = $faceCount
        staticRecordOffset = [string]$record.offset
        hqData = $hqScore
        hqDataClose = $closeScore
    })
}

$sortedRecords = @($records.ToArray() | Sort-Object @{ Expression = { [int]$_.faceCount }; Descending = $true }, @{ Expression = { [int]$_.textureId }; Ascending = $true })
$topUsed = @($sortedRecords | Where-Object { [int]$_.faceCount -gt 0 } | Select-Object -First 24)
$blankUsed = @($sortedRecords | Where-Object { [int]$_.faceCount -gt 0 -and -not [bool]$_.hqData.loadedLikely })
$partialUsed = @($sortedRecords | Where-Object { [int]$_.faceCount -gt 0 -and [bool]$_.hqData.loadedLikely -and -not [bool]$_.hqData.completeLikely })
$completeUsed = @($sortedRecords | Where-Object { [int]$_.faceCount -gt 0 -and [bool]$_.hqData.completeLikely })
$selectedGroupName = if ($UseCloseHq) { "hqDataClose" } else { "hqData" }

$cellWidth = 132
$cellHeight = 108
$previewSize = 64
$rows = [int][Math]::Ceiling($records.Count / [double]$Columns)
$bitmap = New-Object System.Drawing.Bitmap ($Columns * $cellWidth), ($rows * $cellHeight), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.Clear([System.Drawing.Color]::FromArgb(255, 26, 31, 34))
$titleFont = New-Object System.Drawing.Font("Segoe UI", 8)
$font = New-Object System.Drawing.Font("Consolas", 7)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 238, 242, 232))
$mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210, 193, 205, 200))
$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(95, 255, 255, 255), 1)

foreach ($entry in @($records.ToArray())) {
    $textureId = [int]$entry.textureId
    $col = $textureId % $Columns
    $row = [int][Math]::Floor($textureId / $Columns)
    $left = $col * $cellWidth
    $top = $row * $cellHeight
    $score = if ($UseCloseHq) { $entry.hqDataClose } else { $entry.hqData }
    $status = if ([bool]$score.completeLikely) { "complete" } elseif ([bool]$score.loadedLikely) { "partial" } else { "missing" }
    $statusBrush = New-StatusBrush $status
    $graphics.FillRectangle($statusBrush, $left + 3, $top + 3, $cellWidth - 6, 16)
    $statusBrush.Dispose()
    $graphics.DrawRectangle($framePen, $left + 3, $top + 3, $cellWidth - 6, $cellHeight - 6)
    $graphics.DrawString(("T{0:00} f{1}" -f $textureId, [int]$entry.faceCount), $titleFont, $textBrush, $left + 7, $top + 4)
    $tilesToDraw = if ($UseCloseHq) { Get-HqGroup ($textureReport.textureRecords.records[$textureId]) "hqDataClose" } else { Get-HqGroup ($textureReport.textureRecords.records[$textureId]) "hqData" }
    Draw-TexturePreview $graphics $vram $tilesToDraw ($left + 7) ($top + 24) ([bool]$SwapTexelBytes) $previewSize
    $graphics.DrawString(("{0}: {1}/{2}" -f $selectedGroupName, [int]$score.usableTiles, [int]$score.tileCount), $font, $mutedBrush, $left + 76, $top + 27)
    $graphics.DrawString(("pal0: {0}" -f [int]$score.blankPaletteTiles), $font, $mutedBrush, $left + 76, $top + 41)
    $graphics.DrawString(("avgC: {0}" -f [double]$score.averageColorUnique), $font, $mutedBrush, $left + 76, $top + 55)
    $graphics.DrawString($status, $font, $mutedBrush, $left + 76, $top + 69)
}

$resolvedContact = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutContactSheetPath)
$bitmap.Save($resolvedContact, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$titleFont.Dispose()
$font.Dispose()
$textBrush.Dispose()
$mutedBrush.Dispose()
$framePen.Dispose()

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    textureJson = $resolvedTextureJson
    vram = $resolvedVram
    swapTexelBytes = [bool]$SwapTexelBytes
    contactSheetGroup = $selectedGroupName
    contactSheet = $resolvedContact
    textureCount = [int]$textureReport.textureRecords.textureCount
    hpFaceCount = if ($null -ne $textureReport.sceneFaces) { [int]$textureReport.sceneFaces.hpFaceCount } else { 0 }
    usedTextureIdsInTopHistogram = @($records.ToArray() | Where-Object { [int]$_.faceCount -gt 0 }).Count
    usedCompleteHq = $completeUsed.Count
    usedPartialHq = $partialUsed.Count
    usedMissingHq = $blankUsed.Count
    topUsedTextures = @($topUsed)
    usedMissingHqTextures = @($blankUsed | Select-Object textureId, faceCount, hqData, hqDataClose)
    usedPartialHqTextures = @($partialUsed | Select-Object textureId, faceCount, hqData, hqDataClose)
    records = @($records.ToArray())
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 12) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Runtime Texture VRAM Report")
[void]$md.Add("")
[void]$md.Add(("- Generated: {0}" -f $summary.generatedAt))
[void]$md.Add(('- VRAM: `{0}`' -f $resolvedVram))
[void]$md.Add(('- Texture decode JSON: `{0}`' -f $resolvedTextureJson))
[void]$md.Add(('- Contact sheet: `{0}`' -f $resolvedContact))
[void]$md.Add(('- Contact sheet descriptor group: `{0}`' -f $selectedGroupName))
[void]$md.Add(('- Swap texel bytes: `{0}`' -f [bool]$SwapTexelBytes))
[void]$md.Add("")
[void]$md.Add("## Summary")
[void]$md.Add("")
[void]$md.Add(("- Texture records: {0}" -f [int]$textureReport.textureRecords.textureCount))
[void]$md.Add(("- HP terrain faces in runtime report: {0}" -f $summary.hpFaceCount))
[void]$md.Add(("- Used texture IDs covered by top histogram: {0}" -f $summary.usedTextureIdsInTopHistogram))
[void]$md.Add(("- Used HQ textures complete in source pages: {0}" -f $summary.usedCompleteHq))
[void]$md.Add(("- Used HQ textures partial in source pages: {0}" -f $summary.usedPartialHq))
[void]$md.Add(("- Used HQ textures missing/blank in source pages: {0}" -f $summary.usedMissingHq))
[void]$md.Add("")
[void]$md.Add("## Top Used Texture IDs")
[void]$md.Add("")
[void]$md.Add("| Texture | Faces | HQ usable | HQ blank palettes | Close usable | Close blank palettes |")
[void]$md.Add("|---:|---:|---:|---:|---:|---:|")
foreach ($entry in @($topUsed)) {
    [void]$md.Add(("| T{0} | {1} | {2}/{3} | {4} | {5}/{6} | {7} |" -f [int]$entry.textureId, [int]$entry.faceCount, [int]$entry.hqData.usableTiles, [int]$entry.hqData.tileCount, [int]$entry.hqData.blankPaletteTiles, [int]$entry.hqDataClose.usableTiles, [int]$entry.hqDataClose.tileCount, [int]$entry.hqDataClose.blankPaletteTiles))
}

if ($blankUsed.Count -gt 0) {
    [void]$md.Add("")
[void]$md.Add("## Used Textures Missing From HQ Source Pages")
    [void]$md.Add("")
    [void]$md.Add("| Texture | Faces | HQ usable | HQ blank palettes | Close usable | Close blank palettes |")
    [void]$md.Add("|---:|---:|---:|---:|---:|---:|")
    foreach ($entry in @($blankUsed)) {
        [void]$md.Add(("| T{0} | {1} | {2}/{3} | {4} | {5}/{6} | {7} |" -f [int]$entry.textureId, [int]$entry.faceCount, [int]$entry.hqData.usableTiles, [int]$entry.hqData.tileCount, [int]$entry.hqData.blankPaletteTiles, [int]$entry.hqDataClose.usableTiles, [int]$entry.hqDataClose.tileCount, [int]$entry.hqDataClose.blankPaletteTiles))
    }
}

[void]$md.Add("")
[void]$md.Add("## Interpretation")
[void]$md.Add("")
[void]$md.Add("- `complete` means every tile in that descriptor group had a usable palette, varied texels, and varied final colors in the supplied VRAM/page blob.")
[void]$md.Add("- `partial` means at least one tile looked usable, but some tiles were blank, out of range, or low-variance.")
[void]$md.Add("- `missing/blank` on a used terrain texture means the static descriptor points at data that is not currently present in the supplied VRAM/page blob. For DuckStation captures this can be camera/load-state dependent; for WAD texture-page sources it points to another descriptor group or subfile.")

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
[System.IO.File]::WriteAllLines($resolvedMarkdown, [string[]]$md.ToArray(), [System.Text.UTF8Encoding]::new($false))

Write-Host ("Wrote runtime texture VRAM report to {0}" -f $resolvedJson)
Write-Host ("Wrote markdown summary to {0}" -f $resolvedMarkdown)
Write-Host ("Wrote contact sheet to {0}" -f $resolvedContact)
