param(
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [string]$OutPath = ".\stonehill-hp-colour-mesh-contact-sheet.png",
    [string]$OutJsonPath = ".\stonehill-hp-colour-mesh-contact-sheet.json",
    [int]$ThumbWidth = 520,
    [int]$ThumbHeight = 420
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

function Convert-PsxAddressToRamOffset([uint32]$Pointer, [int]$RamLength) {
    $address = [uint64]$Pointer
    if ($address -lt 0x80000000L -or $address -ge 0x80200000L) { return -1 }
    $offset = [int]($Pointer -band 0x001FFFFF)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
}

function Read-SceneSectorHeader([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 28) -gt $Ram.Length) { return $null }
    $numLpVertices = [int]$Ram[$Offset + 16]
    $numLpColours = [int]$Ram[$Offset + 17]
    $numLpFaces = [int]$Ram[$Offset + 18]
    $numHpVertices = [int]$Ram[$Offset + 20]
    $numHpColours = [int]$Ram[$Offset + 21]
    $numHpFaces = [int]$Ram[$Offset + 22]
    $sizeWords = 7 + $numLpVertices + $numLpColours + ($numLpFaces * 2) + $numHpVertices + ($numHpColours * 2) + ($numHpFaces * 4)
    $sizeBytes = $sizeWords * 4
    if ($sizeBytes -lt 28 -or $sizeBytes -gt 0x40000 -or ($Offset + $sizeBytes) -gt $Ram.Length) { return $null }
    return [pscustomobject]@{
        offset = $Offset
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
}

function Convert-SceneVertex([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $xyPos = [uint64](Get-UInt32LE $script:Ram ([int]$Sector.offset + 8))
    $zPos = [uint64](Get-UInt32LE $script:Ram ([int]$Sector.offset + 12))
    $sectorX = [int](($xyPos -shr 16) -band 0xFFFF)
    $sectorY = [int]($xyPos -band 0xFFFF)
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    $sectorZ = $sectorZ -shr 2
    $x = $sectorX + [int]((($cur -shr 19) -band 0x1FFC) -shr 2)
    $y = $sectorY + [int]((($cur -shr 8) -band 0x1FFC) -shr 2)
    $z = $sectorZ + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    $flatSector = ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
    if ($flatSector) { $z = $z -shr 3 }
    return [pscustomobject]@{ x = [double]$x; y = [double]$y; z = [double]$z }
}

function Read-SceneSectorHeaderFull([byte[]]$Ram, [int]$Offset) {
    $sector = Read-SceneSectorHeader $Ram $Offset
    if ($null -eq $sector) { return $null }
    $sector | Add-Member -NotePropertyName centreRadiusAndFlags -NotePropertyValue ([int](Get-UInt16LE $Ram ($Offset + 4)))
    return $sector
}

function Expand-5Bit([int]$Value) {
    $v = $Value -band 31
    return (($v -shl 3) -bor ($v -shr 2))
}

function Convert-Psx555ToColor([int]$Value) {
    $r = Expand-5Bit ($Value -band 0x1F)
    $g = Expand-5Bit (($Value -shr 5) -band 0x1F)
    $b = Expand-5Bit (($Value -shr 10) -band 0x1F)
    return [System.Drawing.Color]::FromArgb(230, $r, $g, $b)
}

function Convert-RawRgbToColor([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 3) -gt $Ram.Length) {
        return [System.Drawing.Color]::FromArgb(230, 0, 0, 0)
    }
    return [System.Drawing.Color]::FromArgb(230, [int]$Ram[$Offset], [int]$Ram[$Offset + 1], [int]$Ram[$Offset + 2])
}

function Average-Colors($Colors) {
    $count = 0
    $r = 0
    $g = 0
    $b = 0
    foreach ($color in $Colors) {
        if ($null -eq $color) { continue }
        $r += [int]$color.R
        $g += [int]$color.G
        $b += [int]$color.B
        $count++
    }
    if ($count -le 0) { return [System.Drawing.Color]::FromArgb(230, 90, 110, 100) }
    return [System.Drawing.Color]::FromArgb(232, [int]($r / $count), [int]($g / $count), [int]($b / $count))
}

function Get-FaceColor($Face, [string]$Variant) {
    $colors = New-Object System.Collections.Generic.List[System.Drawing.Color]
    foreach ($index in @($Face.colourIndexes)) {
        $entryOffset = [int]$Face.colourStart + ([int]$index * 8)
        if (($entryOffset + 8) -gt $script:Ram.Length) { continue }
        switch ($Variant) {
            "psx555+0" { [void]$colors.Add((Convert-Psx555ToColor (Get-UInt16LE $script:Ram $entryOffset))) }
            "psx555+2" { [void]$colors.Add((Convert-Psx555ToColor (Get-UInt16LE $script:Ram ($entryOffset + 2)))) }
            "psx555+4" { [void]$colors.Add((Convert-Psx555ToColor (Get-UInt16LE $script:Ram ($entryOffset + 4)))) }
            "psx555+6" { [void]$colors.Add((Convert-Psx555ToColor (Get-UInt16LE $script:Ram ($entryOffset + 6)))) }
            "raw-rgb+0" { [void]$colors.Add((Convert-RawRgbToColor $script:Ram $entryOffset)) }
            "raw-rgb+4" { [void]$colors.Add((Convert-RawRgbToColor $script:Ram ($entryOffset + 4))) }
        }
    }
    return Average-Colors $colors
}

function Include-Point($Point, [hashtable]$Bounds) {
    $x = [double]$Point.x
    $y = [double]$Point.y
    if ($x -lt [double]$Bounds.minX) { $Bounds.minX = $x }
    if ($x -gt [double]$Bounds.maxX) { $Bounds.maxX = $x }
    if ($y -lt [double]$Bounds.minY) { $Bounds.minY = $y }
    if ($y -gt [double]$Bounds.maxY) { $Bounds.maxY = $y }
}

function Convert-ToScreen($Point, [hashtable]$Bounds, [int]$Left, [int]$Top, [int]$Width, [int]$Height) {
    $pad = 22.0
    $spanX = [Math]::Max(1.0, [double]$Bounds.maxX - [double]$Bounds.minX)
    $spanY = [Math]::Max(1.0, [double]$Bounds.maxY - [double]$Bounds.minY)
    $scale = [Math]::Min((($Width - ($pad * 2)) / $spanX), (($Height - ($pad * 2)) / $spanY))
    $drawWidth = $spanX * $scale
    $drawHeight = $spanY * $scale
    $originX = $Left + (($Width - $drawWidth) / 2.0)
    $originY = $Top + (($Height - $drawHeight) / 2.0)
    return [System.Drawing.PointF]::new(
        [float]($originX + (([double]$Point.x - [double]$Bounds.minX) * $scale)),
        [float]($originY + (([double]$Point.y - [double]$Bounds.minY) * $scale))
    )
}

function Read-HpFaces([byte[]]$Ram, [uint32]$SceneAddress) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $sectorCount = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($sectorCount -le 0 -or $sectorCount -gt 4096) { throw "Scene sector count is not plausible." }

    $faces = New-Object System.Collections.Generic.List[object]
    $bounds = @{ minX = [double]::PositiveInfinity; maxX = [double]::NegativeInfinity; minY = [double]::PositiveInfinity; maxY = [double]::NegativeInfinity }
    $validSectors = 0
    for ($sectorIndex = 0; $sectorIndex -lt $sectorCount; $sectorIndex++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($sceneOffset + 12 + ($sectorIndex * 4)))
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeaderFull $Ram $sectorOffset
        if ($null -eq $sector -or [int]$sector.numHpFaces -le 0) { continue }
        $validSectors++

        $dataStart = [int]$sector.offset + 28
        $lpVertexWords = [int]$sector.numLpVertices
        $lpColourWords = [int]$sector.numLpColours
        $lpFaceWords = [int]$sector.numLpFaces * 2
        $hpVertexStartWords = $lpVertexWords + $lpColourWords + $lpFaceWords
        $hpColourStartWords = $hpVertexStartWords + [int]$sector.numHpVertices
        $hpFaceStartWords = $hpColourStartWords + ([int]$sector.numHpColours * 2)
        $hpVertexStart = $dataStart + ($hpVertexStartWords * 4)
        $hpColourStart = $dataStart + ($hpColourStartWords * 4)
        $hpFaceStart = $dataStart + ($hpFaceStartWords * 4)

        $vertices = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt [int]$sector.numHpVertices; $i++) {
            $word = Get-UInt32LE $Ram ($hpVertexStart + ($i * 4))
            [void]$vertices.Add((Convert-SceneVertex $word $sector))
        }

        for ($face = 0; $face -lt [int]$sector.numHpFaces; $face++) {
            $faceOffset = $hpFaceStart + ($face * 16)
            if (($faceOffset + 16) -gt $Ram.Length) { continue }
            $points = New-Object System.Collections.Generic.List[object]
            $vertexIndexes = @()
            $colourIndexes = @()
            for ($i = 0; $i -lt 4; $i++) {
                $vertexIndex = [int]$Ram[$faceOffset + $i]
                $colourIndex = [int]$Ram[$faceOffset + 4 + $i]
                $vertexIndexes += $vertexIndex
                $colourIndexes += $colourIndex
                if ($vertexIndex -ge 0 -and $vertexIndex -lt $vertices.Count) {
                    $point = $vertices[$vertexIndex]
                    [void]$points.Add($point)
                    Include-Point $point $bounds
                }
            }
            if ($points.Count -ge 3) {
                [void]$faces.Add([pscustomobject]@{
                    points = @($points.ToArray())
                    vertexIndexes = $vertexIndexes
                    colourIndexes = $colourIndexes
                    colourStart = $hpColourStart
                    avgZ = [double](($points | Measure-Object -Property z -Average).Average)
                })
            }
        }
    }

    return [pscustomobject]@{
        faces = @($faces.ToArray())
        bounds = $bounds
        validSectorsWithHp = $validSectors
        sceneSectors = $sectorCount
    }
}

$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$script:Ram = [System.IO.File]::ReadAllBytes($resolvedRam)
if ($script:Ram.Length -gt 0x200000) {
    $copy = New-Object byte[] 0x200000
    [Array]::Copy($script:Ram, 0, $copy, 0, 0x200000)
    $script:Ram = $copy
}
$sceneText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddress = [Convert]::ToUInt32($sceneText, 16)
$mesh = Read-HpFaces $script:Ram $sceneAddress

$variants = @(
    [pscustomobject]@{ key = "psx555+0"; label = "colour entry +0 as PSX555" },
    [pscustomobject]@{ key = "psx555+2"; label = "colour entry +2 as PSX555" },
    [pscustomobject]@{ key = "psx555+4"; label = "colour entry +4 as PSX555" },
    [pscustomobject]@{ key = "psx555+6"; label = "colour entry +6 as PSX555" },
    [pscustomobject]@{ key = "raw-rgb+0"; label = "colour entry +0 raw RGB" },
    [pscustomobject]@{ key = "raw-rgb+4"; label = "colour entry +4 raw RGB" }
)

$columns = 2
$rows = [int][Math]::Ceiling($variants.Count / [double]$columns)
$sheetWidth = $columns * $ThumbWidth
$sheetHeight = $rows * $ThumbHeight
$bitmap = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(31, 36, 34))
$font = New-Object System.Drawing.Font("Segoe UI", 10)
$smallFont = New-Object System.Drawing.Font("Segoe UI", 8)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 242, 245, 240))
$edgePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(22, 20, 26, 22), 1)
$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 255, 255, 255), 1)

$facesByDepth = @($mesh.faces | Sort-Object -Property avgZ)
for ($variantIndex = 0; $variantIndex -lt $variants.Count; $variantIndex++) {
    $variant = $variants[$variantIndex]
    $col = $variantIndex % $columns
    $row = [int][Math]::Floor($variantIndex / $columns)
    $left = $col * $ThumbWidth
    $top = $row * $ThumbHeight
    $plotLeft = $left + 10
    $plotTop = $top + 38
    $plotWidth = $ThumbWidth - 20
    $plotHeight = $ThumbHeight - 58
    $graphics.DrawRectangle($framePen, $left + 4, $top + 4, $ThumbWidth - 8, $ThumbHeight - 8)
    $graphics.DrawString([string]$variant.label, $font, $textBrush, $left + 12, $top + 10)

    foreach ($face in $facesByDepth) {
        $screenPoints = New-Object System.Collections.Generic.List[System.Drawing.PointF]
        foreach ($point in @($face.points)) {
            [void]$screenPoints.Add((Convert-ToScreen $point $mesh.bounds $plotLeft $plotTop $plotWidth $plotHeight))
        }
        if ($screenPoints.Count -lt 3) { continue }
        $color = Get-FaceColor $face ([string]$variant.key)
        $brush = New-Object System.Drawing.SolidBrush($color)
        $graphics.FillPolygon($brush, $screenPoints.ToArray())
        $brush.Dispose()
        $graphics.DrawPolygon($edgePen, $screenPoints.ToArray())
    }

    $caption = "faces {0}, sectors {1}/{2}" -f $mesh.faces.Count, $mesh.validSectorsWithHp, $mesh.sceneSectors
    $graphics.DrawString($caption, $smallFont, $textBrush, $left + 12, $top + $ThumbHeight - 20)
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$bitmap.Save($resolvedOut, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
$font.Dispose()
$smallFont.Dispose()
$textBrush.Dispose()
$edgePen.Dispose()
$framePen.Dispose()

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceRam = $resolvedRam
    sceneRuntimeAddress = ("0x{0:X8}" -f $sceneAddress)
    outputImage = $resolvedOut
    hpFaces = [int]$mesh.faces.Count
    hpSectors = [int]$mesh.validSectorsWithHp
    sceneSectors = [int]$mesh.sceneSectors
    variants = @($variants | ForEach-Object { [ordered]@{ key = $_.key; label = $_.label } })
}
$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

Write-Host ("Wrote HP colour mesh contact sheet to {0}" -f $resolvedOut)
Write-Host ("HP faces: {0}; sectors with HP: {1}/{2}" -f $mesh.faces.Count, $mesh.validSectorsWithHp, $mesh.sceneSectors)
