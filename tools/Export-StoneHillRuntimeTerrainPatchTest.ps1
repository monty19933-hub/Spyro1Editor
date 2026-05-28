param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$CustomTexturesPath = "",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin",
    [string]$CuePath = "",
    [string]$PlanPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.json",
    [string]$MarkdownPath = ".\Spyro the Dragon (USA)-runtime-terrainpatchtest.bin.terrainpatchplan.md",
    [int]$WadLba = 37,
    [int]$TextureAssetWadIndex = 10,
    [int]$TexturePagesSubfileIndex = 0,
    [int]$ModelSubfileIndex = 1,
    [switch]$IncludeCollisionLp,
    [double]$CollisionLpPadding = 256.0,
    [int]$MaxCollisionLpVerticesPerEdit = 8,
    [switch]$PlanOnly,
    [switch]$AllowExperimentalWrite
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
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

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    try { return [Convert]::ToInt64($clean, 16) }
    catch { return $Default }
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-BytesHex([byte[]]$Bytes) {
    return (($Bytes | ForEach-Object { $_.ToString("X2") }) -join "")
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
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
    if (($numLpVertices + $numHpVertices) -eq 0 -or ($numLpFaces + $numHpFaces) -eq 0) { return $null }
    return [pscustomobject]@{
        offset = $Offset
        centreRadiusAndFlags = [int](Get-UInt16LE $Ram ($Offset + 4))
        xyPos = [uint32](Get-UInt32LE $Ram ($Offset + 8))
        zPos = [uint32](Get-UInt32LE $Ram ($Offset + 12))
        sizeBytes = $sizeBytes
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
}

function Get-SceneSectorBaseZ($Sector) {
    $zPos = [uint64]$Sector.zPos
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    return [int]($sectorZ -shr 2)
}

function Test-FlatSceneSector($Sector) {
    return ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
}

function Convert-SceneVertexZ([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $z = (Get-SceneSectorBaseZ $Sector) + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [double]$z
}

function Convert-SceneVertex([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $xyPos = [uint64]$Sector.xyPos
    $zPos = [uint64]$Sector.zPos
    $sectorX = [int](($xyPos -shr 16) -band 0xFFFF)
    $sectorY = [int]($xyPos -band 0xFFFF)
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    $sectorZ = $sectorZ -shr 2
    $x = $sectorX + [int]((($cur -shr 19) -band 0x1FFC) -shr 2)
    $y = $sectorY + [int]((($cur -shr 8) -band 0x1FFC) -shr 2)
    $z = $sectorZ + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [pscustomobject]([ordered]@{
        x = [double]$x
        y = [double]$y
        z = [double]$z
    })
}

function Set-SceneVertexZWord([uint32]$Word, $Sector, [double]$TargetZ) {
    $baseZ = Get-SceneSectorBaseZ $Sector
    if (Test-FlatSceneSector $Sector) {
        $encodedZ = [int][Math]::Round(($TargetZ * 8.0) - [double]$baseZ)
    }
    else {
        $encodedZ = [int][Math]::Round($TargetZ - [double]$baseZ)
    }
    if ($encodedZ -lt 0 -or $encodedZ -gt 1023) {
        throw ("Target Z {0:N2} cannot be encoded in sector 0x{1:X}; encoded delta would be {2}." -f $TargetZ, [int]$Sector.offset, $encodedZ)
    }
    return [uint32]((([uint64]$Word) -band [uint64]4294966272) -bor ([uint64]($encodedZ -band 0x3FF)))
}

function Get-SceneVertexOffset($Sector, [string]$DetailName, [int]$VertexIndex) {
    $dataStart = [int]$Sector.offset + 28
    if ([string]::Equals($DetailName, "lp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numLpVertices) { return -1 }
        return $dataStart + ($VertexIndex * 4)
    }
    if ([string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numHpVertices) { return -1 }
        $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
        return $dataStart + (($hpVertexStartWords + $VertexIndex) * 4)
    }
    return -1
}

function Get-LpCollisionCompanionVertices([byte[]]$Ram, $Sector, [string]$DetailName, $VertexIndexes, [double]$Padding, [int]$MaxCount) {
    if (-not [string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) { return @() }
    if ([int]$Sector.numLpVertices -le 0) { return @() }

    $hpCoords = New-Object System.Collections.ArrayList
    $seenHp = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($rawIndex in @($VertexIndexes)) {
        $vertexIndex = [int]$rawIndex
        if (-not $seenHp.Add($vertexIndex)) { continue }
        $runtimeVertexOffset = Get-SceneVertexOffset $Sector "hp" $vertexIndex
        if ($runtimeVertexOffset -lt 0) { continue }
        $word = Get-UInt32LE $Ram $runtimeVertexOffset
        [void]$hpCoords.Add((Convert-SceneVertex $word $Sector))
    }
    if ($hpCoords.Count -le 0) { return @() }

    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    foreach ($coord in @($hpCoords.ToArray())) {
        $minX = [Math]::Min($minX, [double]$coord.x)
        $maxX = [Math]::Max($maxX, [double]$coord.x)
        $minY = [Math]::Min($minY, [double]$coord.y)
        $maxY = [Math]::Max($maxY, [double]$coord.y)
    }
    $centerX = ($minX + $maxX) / 2.0
    $centerY = ($minY + $maxY) / 2.0
    $minX -= $Padding
    $maxX += $Padding
    $minY -= $Padding
    $maxY += $Padding

    $lpCandidates = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt [int]$Sector.numLpVertices; $i++) {
        $runtimeVertexOffset = Get-SceneVertexOffset $Sector "lp" $i
        if ($runtimeVertexOffset -lt 0) { continue }
        $word = Get-UInt32LE $Ram $runtimeVertexOffset
        $coord = Convert-SceneVertex $word $Sector
        $dx = [double]$coord.x - $centerX
        $dy = [double]$coord.y - $centerY
        $inBounds = ([double]$coord.x -ge $minX -and [double]$coord.x -le $maxX -and [double]$coord.y -ge $minY -and [double]$coord.y -le $maxY)
        [void]$lpCandidates.Add([pscustomobject]([ordered]@{
            vertexIndex = $i
            runtimeVertexOffset = $runtimeVertexOffset
            word = [uint32]$word
            x = [double]$coord.x
            y = [double]$coord.y
            z = [double]$coord.z
            distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
            inExpandedHpBounds = $inBounds
        }))
    }

    $selected = @($lpCandidates.ToArray() | Where-Object { $_.inExpandedHpBounds } | Sort-Object distance, vertexIndex)
    if ($selected.Count -le 0) {
        $fallbackCount = [Math]::Min(4, [Math]::Max(1, $MaxCount))
        $selected = @($lpCandidates.ToArray() | Sort-Object distance, vertexIndex | Select-Object -First $fallbackCount)
    }
    elseif ($selected.Count -gt $MaxCount) {
        $selected = @($selected | Select-Object -First $MaxCount)
    }
    return @($selected)
}

function Set-ImageBytesForWadOffset([byte[]]$Image, [int64]$WadOffset, [byte[]]$Bytes) {
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) {
            throw ("Image offset 0x{0:X} is outside image." -f $imageOffset)
        }
        $Image[$imageOffset] = $Bytes[$i]
    }
}

function Get-ImageBytesForWadOffset([byte[]]$Image, [int64]$WadOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) {
            throw ("Image offset 0x{0:X} is outside image." -f $imageOffset)
        }
        $result[$i] = $Image[$imageOffset]
    }
    return $result
}

function Write-Cue([string]$BinPath, [string]$CueOutPath) {
    $fileName = [System.IO.Path]::GetFileName($BinPath)
    $lines = @(
        "FILE `"$fileName`" BINARY",
        "  TRACK 01 MODE2/2352",
        "    INDEX 01 00:00:00"
    )
    [System.IO.File]::WriteAllLines($CueOutPath, $lines, [System.Text.Encoding]::ASCII)
}

function Set-UInt16LE([byte[]]$Bytes, [int]$Offset, [uint16]$Value) {
    $Bytes[$Offset] = [byte]($Value -band 0xFF)
    $Bytes[$Offset + 1] = [byte](($Value -shr 8) -band 0xFF)
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
    return @($entries)
}

function Get-AssetSubfileInfo([byte[]]$Image, [int]$AssetWadIndex, [int]$SubfileIndex) {
    $wadHeader = Get-ImageBytesForWadOffset $Image 0 4096
    $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
    $assetEntry = @($wadEntries | Where-Object { [int]$_.index -eq $AssetWadIndex } | Select-Object -First 1)
    if ($assetEntry.Count -eq 0) { throw "Could not find WAD entry $AssetWadIndex." }

    $assetHeader = Get-ImageBytesForWadOffset $Image ([int64]$assetEntry[0].offset) 4096
    $subfiles = @(Parse-ArchiveHeader $assetHeader ([int64]$assetEntry[0].size))
    $subfile = @($subfiles | Where-Object { [int]$_.index -eq $SubfileIndex } | Select-Object -First 1)
    if ($subfile.Count -eq 0) { throw "Could not find asset subfile $SubfileIndex in WAD entry $AssetWadIndex." }

    return [pscustomobject]@{
        assetWadIndex = $AssetWadIndex
        subfileIndex = $SubfileIndex
        assetOffset = [int64]$assetEntry[0].offset
        assetSize = [int64]$assetEntry[0].size
        subfileOffset = [int64]$subfile[0].offset
        subfileSize = [int64]$subfile[0].size
        absoluteWadOffset = [int64]$assetEntry[0].offset + [int64]$subfile[0].offset
    }
}

function Get-TextureXMin([int]$Region, [int]$XMin) {
    return ((($Region * 128) % 2048) + $XMin)
}

function Get-TextureYMin([int]$Region, [int]$YMin) {
    return ([int][Math]::Floor(($Region -band 0x1F) / 16.0) * 256) + $YMin
}

function Decode-TexHq([byte[]]$Bytes, [int]$Offset, [int]$Index) {
    $xmin = [int]$Bytes[$Offset + 0]
    $ymin = [int]$Bytes[$Offset + 1]
    $palette = [int](Get-UInt16LE $Bytes ($Offset + 2))
    $xmax = [int]$Bytes[$Offset + 4]
    $ymax = [int]$Bytes[$Offset + 5]
    $region = [int]$Bytes[$Offset + 6]
    $unknown = [int]$Bytes[$Offset + 7]
    return [pscustomobject]@{
        index = $Index
        xmin = $xmin
        ymin = $ymin
        palette = $palette
        paletteByteStart = $palette * 32
        xmax = $xmax
        ymax = $ymax
        region = $region
        unknown = $unknown
        orientation = (($unknown -shr 4) -band 7)
        vramXMin = (Get-TextureXMin $region $xmin)
        vramYMin = (Get-TextureYMin $region $ymin)
        vramXMax = (Get-TextureXMin $region $xmax)
        vramYMax = (Get-TextureYMin $region $ymax)
    }
}

function Decode-TextureRecords([byte[]]$ModelBytes) {
    $textureListSize = [int](Get-UInt32LE $ModelBytes 0)
    $textureCount = [int](Get-UInt32LE $ModelBytes 4)
    if ($textureListSize -le 8 -or $textureCount -le 0) { throw "No plausible texture list found in model subfile." }
    $recordBytes = [int](($textureListSize - 8) / $textureCount)
    if ((($textureListSize - 8) % $textureCount) -ne 0) { throw "Texture list is not an even fixed-record table." }

    $records = New-Object System.Collections.ArrayList
    for ($texture = 0; $texture -lt $textureCount; $texture++) {
        $offset = 8 + ($texture * $recordBytes)
        $hq = New-Object System.Collections.ArrayList
        for ($i = 0; $i -lt 4; $i++) {
            [void]$hq.Add((Decode-TexHq $ModelBytes ($offset + 24 + ($i * 8)) $i))
        }
        $hqClose = New-Object System.Collections.ArrayList
        for ($i = 0; $i -lt 16; $i++) {
            [void]$hqClose.Add((Decode-TexHq $ModelBytes ($offset + 56 + ($i * 8)) $i))
        }
        [void]$records.Add([pscustomobject]@{
            textureId = $texture
            offset = ("0x{0:X}" -f $offset)
            hqData = @($hq.ToArray())
            hqDataClose = @($hqClose.ToArray())
        })
    }

    return [pscustomobject]@{
        textureListSize = $textureListSize
        textureCount = $textureCount
        recordBytes = $recordBytes
        records = @($records.ToArray())
    }
}

function Convert-ColorToPsx555([System.Drawing.Color]$Color) {
    $r = [int][Math]::Round(([double]$Color.R * 31.0) / 255.0)
    $g = [int][Math]::Round(([double]$Color.G * 31.0) / 255.0)
    $b = [int][Math]::Round(([double]$Color.B * 31.0) / 255.0)
    $r = [Math]::Max(0, [Math]::Min(31, $r))
    $g = [Math]::Max(0, [Math]::Min(31, $g))
    $b = [Math]::Max(0, [Math]::Min(31, $b))
    return [uint16](($b -shl 10) -bor ($g -shl 5) -bor $r)
}

function Get-Psx555Distance([int]$A, [int]$B) {
    $ar = ($A -band 31)
    $ag = (($A -shr 5) -band 31)
    $ab = (($A -shr 10) -band 31)
    $br = ($B -band 31)
    $bg = (($B -shr 5) -band 31)
    $bb = (($B -shr 10) -band 31)
    $dr = $ar - $br
    $dg = $ag - $bg
    $db = $ab - $bb
    return (($dr * $dr) + ($dg * $dg) + ($db * $db))
}

function Get-NearestPaletteIndex([int]$Word, [int[]]$PaletteWords, $ExactMap, $NearestCache) {
    $key = [string]$Word
    if ($ExactMap.ContainsKey($key)) { return [int]$ExactMap[$key] }
    if ($NearestCache.ContainsKey($key)) { return [int]$NearestCache[$key] }

    $bestIndex = 0
    $bestDistance = [int]::MaxValue
    for ($i = 0; $i -lt $PaletteWords.Length; $i++) {
        $distance = Get-Psx555Distance $Word ([int]$PaletteWords[$i])
        if ($distance -lt $bestDistance) {
            $bestDistance = $distance
            $bestIndex = $i
            if ($distance -eq 0) { break }
        }
    }
    $NearestCache[$key] = $bestIndex
    return $bestIndex
}

function Resize-CustomTextureImage([string]$Path, [int]$TileSize) {
    $source = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $TileSize, $TileSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.DrawImage($source, 0, 0, $TileSize, $TileSize)
        }
        finally {
            $graphics.Dispose()
        }
        return $bitmap
    }
    finally {
        $source.Dispose()
    }
}

function New-QuantizedPalette([System.Drawing.Bitmap]$Bitmap) {
    $counts = @{}
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $word = [int](Convert-ColorToPsx555 ($Bitmap.GetPixel($x, $y)))
            $key = [string]$word
            if ($counts.ContainsKey($key)) { $counts[$key] = [int]$counts[$key] + 1 }
            else { $counts[$key] = 1 }
        }
    }

    $words = New-Object System.Collections.ArrayList
    foreach ($key in @($counts.Keys | Sort-Object -Property @{ Expression = { $counts[$_] }; Descending = $true }, @{ Expression = { [int]$_ }; Ascending = $true } | Select-Object -First 256)) {
        [void]$words.Add([int]$key)
    }
    while ($words.Count -lt 256) {
        [void]$words.Add(0)
    }

    $palette = [int[]]$words.ToArray([int])
    $exact = @{}
    for ($i = 0; $i -lt $palette.Length; $i++) {
        $key = [string]$palette[$i]
        if (-not $exact.ContainsKey($key)) { $exact[$key] = $i }
    }
    return [pscustomobject]@{
        words = $palette
        exactMap = $exact
        nearestCache = @{}
    }
}

function Add-CustomTexturePatch($PatchMap, $PatchList, [int64]$WadOffset, [byte[]]$OldBytes, [byte[]]$NewBytes, [string]$Kind, $Context) {
    if ($OldBytes.Length -ne $NewBytes.Length) { throw "Custom texture patch length mismatch." }
    $key = "0x{0:X}" -f $WadOffset
    $oldHex = Get-BytesHex $OldBytes
    $newHex = Get-BytesHex $NewBytes
    if ($PatchMap.ContainsKey($key)) {
        $existing = $PatchMap[$key]
        if ([string]$existing.oldBytes -ne $oldHex -or [string]$existing.newBytes -ne $newHex) {
            throw ("Conflicting custom texture patches target {0}: {1}->{2} and {3}->{4}." -f $key, $existing.oldBytes, $existing.newBytes, $oldHex, $newHex)
        }
        return
    }

    $patch = [pscustomobject]@{
        kind = $Kind
        wadOffset = $key
        imageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $WadOffset))
        oldBytes = $oldHex
        newBytes = $newHex
        length = $NewBytes.Length
        context = $Context
    }
    $PatchMap[$key] = $patch
    [void]$PatchList.Add($patch)
}

function New-CustomTerrainTexturePatches([byte[]]$Image, [string]$Path) {
    $patches = New-Object System.Collections.ArrayList
    $patchMap = @{}
    $summaries = New-Object System.Collections.ArrayList
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{ patches = @(); summaries = @(); textureCount = 0; bytePatchCount = 0 }
    }

    $root = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $textures = @(Get-ArrayField $root "textures")
    if ($textures.Count -eq 0) {
        return [pscustomobject]@{ patches = @(); summaries = @(); textureCount = 0; bytePatchCount = 0 }
    }

    $texturePagesInfo = Get-AssetSubfileInfo $Image $TextureAssetWadIndex $TexturePagesSubfileIndex
    $modelInfo = Get-AssetSubfileInfo $Image $TextureAssetWadIndex $ModelSubfileIndex
    $modelBytes = Get-ImageBytesForWadOffset $Image ([int64]$modelInfo.absoluteWadOffset) ([int]$modelInfo.subfileSize)
    $textureIndex = Decode-TextureRecords $modelBytes

    $matrices = @(
        @( 1,  0,  0,  1),
        @( 0,  1,  1,  0),
        @(-1,  0,  0, -1),
        @( 0, -1,  1,  0),
        @( 0,  1,  1,  0),
        @(-1,  0,  0,  1),
        @( 0, -1, -1,  0),
        @( 1,  0,  0, -1)
    )

    foreach ($texture in $textures) {
        $textureId = [int](Get-Field $texture "textureId" -1)
        if ($textureId -lt 0 -or $textureId -ge [int]$textureIndex.textureCount) { throw "Custom texture id $textureId is outside the decoded Stone Hill texture table." }
        $sourcePath = [string](Get-Field $texture "sourceImagePath" "")
        if (-not [System.IO.Path]::IsPathRooted($sourcePath)) { $sourcePath = Resolve-WorkspacePath $sourcePath }
        if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing custom texture image: $sourcePath" }

        $descriptorTier = [string](Get-Field $texture "descriptorTier" "hqData")
        if ($descriptorTier -ne "hqData") { $descriptorTier = "hqDataClose" }
        $tileSize = if ($descriptorTier -eq "hqDataClose") { 128 } else { 64 }
        $tileGridColumns = if ($descriptorTier -eq "hqDataClose") { 4 } else { 2 }
        $record = $textureIndex.records[$textureId]
        $descriptors = if ($descriptorTier -eq "hqDataClose") { @($record.hqDataClose) } else { @($record.hqData) }

        $bitmap = Resize-CustomTextureImage $sourcePath $tileSize
        try {
            $paletteInfo = New-QuantizedPalette $bitmap
            $paletteBytes = New-Object byte[] 512
            for ($i = 0; $i -lt 256; $i++) {
                Set-UInt16LE $paletteBytes ($i * 2) ([uint16]$paletteInfo.words[$i])
            }

            $paletteStarts = New-Object System.Collections.ArrayList
            $pixelPatchCount = 0
            foreach ($hq in $descriptors) {
                $paletteByteStart = [int]$hq.paletteByteStart
                if ($paletteByteStart -lt 0 -or ($paletteByteStart + 512) -gt [int64]$texturePagesInfo.subfileSize) {
                    throw "Texture $textureId palette at 0x$($paletteByteStart.ToString('X')) is outside texture-pages subfile $TexturePagesSubfileIndex."
                }
                if (-not $paletteStarts.Contains($paletteByteStart)) {
                    [void]$paletteStarts.Add($paletteByteStart)
                    $paletteWadOffset = [int64]$texturePagesInfo.absoluteWadOffset + [int64]$paletteByteStart
                    $oldPaletteBytes = Get-ImageBytesForWadOffset $Image $paletteWadOffset 512
                    Add-CustomTexturePatch $patchMap $patches $paletteWadOffset $oldPaletteBytes $paletteBytes "custom-texture-palette" ([ordered]@{
                        textureId = $textureId
                        descriptorTier = $descriptorTier
                        paletteByteStart = ("0x{0:X}" -f $paletteByteStart)
                    })
                }

                $tile = [int]$hq.index
                $destTileX = ($tile % $tileGridColumns) * 32
                $destTileY = [int][Math]::Floor($tile / $tileGridColumns) * 32
                $orientation = [int]$hq.orientation
                $matrix = $matrices[$orientation]
                $xx = [int]$matrix[0]
                $xy = [int]$matrix[1]
                $yx = [int]$matrix[2]
                $yy = [int]$matrix[3]
                $srcXStart = [int]$hq.vramXMin
                $srcYStart = [int]$hq.vramYMin
                if ($xx -lt 0 -or $xy -lt 0) { $srcXStart += 31 }
                if ($yx -lt 0 -or $yy -lt 0) { $srcYStart += 31 }

                for ($y = 0; $y -lt 32; $y++) {
                    for ($x = 0; $x -lt 32; $x++) {
                        $sx = $srcXStart + ($x * $xx) + ($y * $xy)
                        $sy = $srcYStart + ($x * $yx) + ($y * $yy)
                        if ($sx -lt 0 -or $sx -ge 2048 -or $sy -lt 0 -or $sy -ge 512) {
                            throw "Texture $textureId descriptor $tile maps outside the 2048x512 texture-page VRAM."
                        }
                        $relative = ($sy * 2048) + $sx
                        if ($relative -lt 0 -or ($relative + 1) -gt [int64]$texturePagesInfo.subfileSize) {
                            throw "Texture $textureId pixel at texture-pages offset 0x$($relative.ToString('X')) is outside subfile $TexturePagesSubfileIndex."
                        }
                        $pixelWord = [int](Convert-ColorToPsx555 ($bitmap.GetPixel($destTileX + $x, $destTileY + $y)))
                        $paletteIndex = [byte](Get-NearestPaletteIndex $pixelWord $paletteInfo.words $paletteInfo.exactMap $paletteInfo.nearestCache)
                        $pixelWadOffset = [int64]$texturePagesInfo.absoluteWadOffset + [int64]$relative
                        $oldPixelBytes = Get-ImageBytesForWadOffset $Image $pixelWadOffset 1
                        $newPixelBytes = [byte[]]@($paletteIndex)
                        Add-CustomTexturePatch $patchMap $patches $pixelWadOffset $oldPixelBytes $newPixelBytes "custom-texture-pixel" ([ordered]@{
                            textureId = $textureId
                            descriptorTier = $descriptorTier
                            descriptorIndex = $tile
                        })
                        $pixelPatchCount++
                    }
                }
            }

            [void]$summaries.Add([ordered]@{
                textureId = $textureId
                sourceImagePath = $sourcePath
                sourceImageName = [string](Get-Field $texture "sourceImageName" ([System.IO.Path]::GetFileName($sourcePath)))
                descriptorTier = $descriptorTier
                tileSize = $tileSize
                descriptorCount = @($descriptors).Count
                pixelPatchCount = $pixelPatchCount
                palettePatchCount = $paletteStarts.Count
                paletteByteStarts = @($paletteStarts.ToArray() | ForEach-Object { "0x{0:X}" -f [int]$_ })
            })
        }
        finally {
            $bitmap.Dispose()
        }
    }

    return [pscustomobject]@{
        patches = @($patches.ToArray())
        summaries = @($summaries.ToArray())
        textureCount = $summaries.Count
        bytePatchCount = $patches.Count
        texturePages = [ordered]@{
            assetWadIndex = $TextureAssetWadIndex
            subfileIndex = $TexturePagesSubfileIndex
            absoluteWadOffset = ("0x{0:X}" -f [int64]$texturePagesInfo.absoluteWadOffset)
            subfileSize = [int64]$texturePagesInfo.subfileSize
        }
    }
}

function Write-Markdown($Plan, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Runtime Terrain Patch Plan")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Plan.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add(("Status: ``{0}``" -f $Plan.status))
    [void]$lines.Add("")
    [void]$lines.Add("- Terrain edits: $($Plan.terrainEditCount)")
    [void]$lines.Add("- Sector patches: $($Plan.sectorPatchCount)")
    [void]$lines.Add("- Vertex patches: $($Plan.vertexPatchCount)")
    [void]$lines.Add("- Texture face patches: $($Plan.textureFacePatchCount)")
    [void]$lines.Add("- Custom texture imports: $($Plan.customTextureImportCount)")
    [void]$lines.Add("- Custom texture byte patches: $($Plan.customTextureBytePatchCount)")
    [void]$lines.Add("- Collision LP companion patches: $($Plan.collisionLpPatchCount)")
    [void]$lines.Add("- BIN written: $($Plan.wroteBin)")
    [void]$lines.Add("")
    [void]$lines.Add("| # | Kind | Edit | Source sector | Vertex | Source WAD | Runtime Z | Target Z | Bytes |")
    [void]$lines.Add("|---:|---|---|---|---:|---|---:|---:|---|")
    $i = 1
    foreach ($patch in @($Plan.vertexPatches)) {
        [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6:N2} | {7:N2} | {8}->{9} |" -f $i, $patch.kind, $patch.runtimeKey, $patch.sourceSectorWadOffset, $patch.vertexIndex, $patch.sourceVertexWadOffset, [double]$patch.originalZ, [double]$patch.targetZ, $patch.oldBytes, $patch.newBytes))
        $i++
    }
    if (@($Plan.textureFacePatches).Count -gt 0) {
        [void]$lines.Add("")
        [void]$lines.Add("| # | Edit | Source sector | Source face word3 | Texture | Bytes |")
        [void]$lines.Add("|---:|---|---|---|---:|---|")
        $j = 1
        foreach ($patch in @($Plan.textureFacePatches)) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4}->{5} | {6}->{7} |" -f $j, $patch.runtimeKey, $patch.sourceSectorWadOffset, $patch.sourceFaceWord3WadOffset, $patch.originalTextureId, $patch.editedTextureId, $patch.oldBytes, $patch.newBytes))
            $j++
        }
    }
    if (@($Plan.customTextureImports).Count -gt 0) {
        [void]$lines.Add("")
        [void]$lines.Add("| Texture | Source image | Tier | Tile | Pixel patches | Palette patches |")
        [void]$lines.Add("|---:|---|---|---:|---:|---:|")
        foreach ($import in @($Plan.customTextureImports)) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} |" -f $import.textureId, [System.IO.Path]::GetFileName([string]$import.sourceImagePath), $import.descriptorTier, $import.tileSize, $import.pixelPatchCount, $import.palettePatchCount))
        }
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedCustomTextures = if ([string]::IsNullOrWhiteSpace($CustomTexturesPath)) { "" } else { Resolve-WorkspacePath $CustomTexturesPath }
$resolvedSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedOut = Resolve-WorkspacePath $OutPath
$resolvedPlan = Resolve-WorkspacePath $PlanPath
$resolvedMd = Resolve-WorkspacePath $MarkdownPath
$resolvedCue = if ([string]::IsNullOrWhiteSpace($CuePath)) { [System.IO.Path]::ChangeExtension($resolvedOut, ".cue") } else { Resolve-WorkspacePath $CuePath }

if (-not (Test-Path -LiteralPath $resolvedImage)) { throw "Missing image: $resolvedImage" }

if (Test-Path -LiteralPath $resolvedEdits) {
    $editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
    $edits = @(Get-ArrayField $editsRoot "edits")
}
else {
    $editsRoot = $null
    $edits = @()
}

$hasTerrainEdits = $edits.Count -gt 0
$hasCustomTextures = -not [string]::IsNullOrWhiteSpace($resolvedCustomTextures) -and (Test-Path -LiteralPath $resolvedCustomTextures)
if (-not $hasTerrainEdits -and -not $hasCustomTextures) {
    throw "No terrain edits or custom terrain texture imports were found."
}

$ram = $null
$searchRoot = $null
if ($hasTerrainEdits) {
    if (-not (Test-Path -LiteralPath $resolvedRam)) { throw "Missing RAM dump: $resolvedRam" }
    if (-not (Test-Path -LiteralPath $resolvedSearch)) { throw "Missing source search report: $resolvedSearch. Run Find-StoneHillRuntimeTerrainSource.ps1 first." }
    $ram = [System.IO.File]::ReadAllBytes($resolvedRam)
    $searchRoot = Get-Content -LiteralPath $resolvedSearch -Raw | ConvertFrom-Json
}
$searchResults = @{}
if ($null -ne $searchRoot) {
    foreach ($item in @(Get-ArrayField $searchRoot "results")) {
        $searchResults[[string](Get-Field $item "edit" "")] = $item
    }
}

$vertexPatches = New-Object System.Collections.ArrayList
$textureFacePatches = New-Object System.Collections.ArrayList
$sectorPatchCount = 0
foreach ($edit in $edits) {
    $runtimeKey = [string](Get-Field $edit "runtimeKey" "")
    if (-not $searchResults.ContainsKey($runtimeKey)) { throw "No source-search result for edit $runtimeKey." }
    $search = $searchResults[$runtimeKey]
    $sectorHits = @(Get-ArrayField $search "fullSectorHits")
    if ($sectorHits.Count -ne 1) {
        throw "Edit $runtimeKey needs exactly one full-sector source hit, found $($sectorHits.Count)."
    }
    $sourceSectorWadOffset = Convert-HexTextToInt64 ([string](Get-Field $sectorHits[0] "wadOffset" ""))
    if ($sourceSectorWadOffset -lt 0) { throw "Bad source sector WAD offset for edit $runtimeKey." }

    $sectorOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "sectorOffset" "")))
    $detail = [string](Get-Field $edit "detail" "hp")
    $sector = Read-SceneSectorHeader $ram $sectorOffset
    if ($null -eq $sector) { throw "Runtime sector $($edit.sectorOffset) no longer parses." }

    $textureIdEdited = [int](Get-Field $edit "textureIdEdited" -1)
    if ($textureIdEdited -ge 0) {
        if ($textureIdEdited -gt 0x7F) { throw "Texture id $textureIdEdited for $runtimeKey does not fit the decoded SceneFace.word3 low 7-bit field." }
        $runtimeFaceOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "faceOffset" "")))
        if ($runtimeFaceOffset -lt $sectorOffset -or ($runtimeFaceOffset + 16) -gt ($sectorOffset + [int]$sector.sizeBytes)) {
            throw "Bad runtime face offset for texture edit $runtimeKey."
        }
        $runtimeWord3Offset = $runtimeFaceOffset + 8
        $sourceFaceWord3WadOffset = $sourceSectorWadOffset + [int64]($runtimeWord3Offset - $sectorOffset)
        $oldWord3 = [uint32](Get-UInt32LE $ram $runtimeWord3Offset)
        $decodedTextureId = [int]($oldWord3 -band 0x7F)
        $textureIdOriginal = [int](Get-Field $edit "textureIdOriginal" (Get-Field $edit "textureId" $decodedTextureId))
        if ($textureIdOriginal -ge 0 -and $decodedTextureId -ne $textureIdOriginal) {
            throw ("Texture edit {0} expected original texture {1}, but runtime word3 decodes as {2}." -f $runtimeKey, $textureIdOriginal, $decodedTextureId)
        }
        $newWord3 = [uint32]((([uint64]$oldWord3) -band [uint64]4294967168) -bor ([uint64]($textureIdEdited -band 0x7F)))
        $oldBytes = [BitConverter]::GetBytes([uint32]$oldWord3)
        $newBytes = [BitConverter]::GetBytes([uint32]$newWord3)
        [void]$textureFacePatches.Add([ordered]@{
            runtimeKey = $runtimeKey
            kind = "texture-id-word3"
            sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
            faceIndex = [int](Get-Field $edit "faceIndex" -1)
            detail = $detail
            sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
            sourceFaceWord3WadOffset = ("0x{0:X}" -f $sourceFaceWord3WadOffset)
            sourceFaceWord3ImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceFaceWord3WadOffset))
            runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
            runtimeFaceOffset = ("0x{0:X}" -f $runtimeFaceOffset)
            runtimeFaceWord3Offset = ("0x{0:X}" -f $runtimeWord3Offset)
            originalTextureId = $decodedTextureId
            editedTextureId = $textureIdEdited
            oldBytes = Get-BytesHex $oldBytes
            newBytes = Get-BytesHex $newBytes
            oldWord3 = ("0x{0:X8}" -f $oldWord3)
            newWord3 = ("0x{0:X8}" -f $newWord3)
        })
    }

    $deltaZValue = [double](Get-Field $edit "deltaZ" 0.0)
    $hasZEdit = [Math]::Abs($deltaZValue) -gt 0.001
    $vertexIndexes = @(Get-ArrayField $edit "vertexIndexes")
    $editedZ = @(Get-ArrayField $edit "editedZ")
    $originalZ = @(Get-ArrayField $edit "originalZ")
    if ($hasZEdit) {
        $seen = New-Object 'System.Collections.Generic.HashSet[int]'
        for ($i = 0; $i -lt $vertexIndexes.Count; $i++) {
            $vertexIndex = [int]$vertexIndexes[$i]
            if (-not $seen.Add($vertexIndex)) { continue }
            $runtimeVertexOffset = Get-SceneVertexOffset $sector $detail $vertexIndex
            if ($runtimeVertexOffset -lt 0) { throw "Vertex $vertexIndex is not valid for $runtimeKey." }
            $sourceVertexWadOffset = $sourceSectorWadOffset + [int64]($runtimeVertexOffset - $sectorOffset)
            $oldWord = Get-UInt32LE $ram $runtimeVertexOffset
            $targetZ = [double]$editedZ[$i]
            $newWord = Set-SceneVertexZWord $oldWord $sector $targetZ
            $oldBytes = [BitConverter]::GetBytes([uint32]$oldWord)
            $newBytes = [BitConverter]::GetBytes([uint32]$newWord)

            [void]$vertexPatches.Add([ordered]@{
                runtimeKey = $runtimeKey
                kind = "visual-$detail"
                sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
                faceIndex = [int](Get-Field $edit "faceIndex" -1)
                detail = $detail
                sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
                sourceVertexWadOffset = ("0x{0:X}" -f $sourceVertexWadOffset)
                sourceVertexImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceVertexWadOffset))
                runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
                runtimeVertexOffset = ("0x{0:X}" -f $runtimeVertexOffset)
                vertexIndex = $vertexIndex
                originalZ = if ($i -lt $originalZ.Count) { [double]$originalZ[$i] } else { Convert-SceneVertexZ $oldWord $sector }
                decodedOriginalZ = Convert-SceneVertexZ $oldWord $sector
                targetZ = $targetZ
                oldBytes = Get-BytesHex $oldBytes
                newBytes = Get-BytesHex $newBytes
                oldWord = ("0x{0:X8}" -f $oldWord)
                newWord = ("0x{0:X8}" -f $newWord)
            })
        }
    }

    if ($IncludeCollisionLp -and $hasZEdit) {
        $companions = @(Get-LpCollisionCompanionVertices $ram $sector $detail $vertexIndexes $CollisionLpPadding $MaxCollisionLpVerticesPerEdit)
        foreach ($companion in $companions) {
            $runtimeVertexOffset = [int]$companion.runtimeVertexOffset
            $sourceVertexWadOffset = $sourceSectorWadOffset + [int64]($runtimeVertexOffset - $sectorOffset)
            $oldWord = [uint32]$companion.word
            $targetZ = [double]$companion.z + $deltaZValue
            $newWord = Set-SceneVertexZWord $oldWord $sector $targetZ
            $oldBytes = [BitConverter]::GetBytes([uint32]$oldWord)
            $newBytes = [BitConverter]::GetBytes([uint32]$newWord)

            [void]$vertexPatches.Add([ordered]@{
                runtimeKey = $runtimeKey
                kind = "collision-lp-candidate"
                sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
                faceIndex = [int](Get-Field $edit "faceIndex" -1)
                detail = "lp"
                sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
                sourceVertexWadOffset = ("0x{0:X}" -f $sourceVertexWadOffset)
                sourceVertexImageOffset = ("0x{0:X}" -f (Convert-WadOffsetToImageOffset $sourceVertexWadOffset))
                runtimeSectorOffset = ("0x{0:X}" -f $sectorOffset)
                runtimeVertexOffset = ("0x{0:X}" -f $runtimeVertexOffset)
                vertexIndex = [int]$companion.vertexIndex
                originalZ = [double]$companion.z
                decodedOriginalZ = Convert-SceneVertexZ $oldWord $sector
                targetZ = $targetZ
                deltaZ = $deltaZValue
                x = [double]$companion.x
                y = [double]$companion.y
                distanceFromHpFaceCenter = [double]$companion.distance
                inExpandedHpBounds = [bool]$companion.inExpandedHpBounds
                oldBytes = Get-BytesHex $oldBytes
                newBytes = Get-BytesHex $newBytes
                oldWord = ("0x{0:X8}" -f $oldWord)
                newWord = ("0x{0:X8}" -f $newWord)
            })
        }
    }
    $sectorPatchCount++
}

$imageForCustomTextures = $null
$customTextureResult = [pscustomobject]@{ patches = @(); summaries = @(); textureCount = 0; bytePatchCount = 0; texturePages = $null }
if ($hasCustomTextures) {
    $imageForCustomTextures = [System.IO.File]::ReadAllBytes($resolvedImage)
    $customTextureResult = New-CustomTerrainTexturePatches $imageForCustomTextures $resolvedCustomTextures
}

$mergedVertexPatches = New-Object System.Collections.ArrayList
$patchesByWadOffset = @{}
foreach ($patch in @($vertexPatches.ToArray())) {
    $key = [string]$patch.sourceVertexWadOffset
    if (-not $patchesByWadOffset.ContainsKey($key)) {
        $patchesByWadOffset[$key] = $patch
        [void]$mergedVertexPatches.Add($patch)
        continue
    }

    $existing = $patchesByWadOffset[$key]
    if ([string]$existing.oldBytes -ne [string]$patch.oldBytes -or [string]$existing.newBytes -ne [string]$patch.newBytes) {
        throw ("Conflicting terrain vertex edits target {0}: {1}->{2} and {3}->{4}." -f $key, $existing.oldBytes, $existing.newBytes, $patch.oldBytes, $patch.newBytes)
    }
}

$mergedTextureFacePatches = New-Object System.Collections.ArrayList
$texturePatchesByWadOffset = @{}
foreach ($patch in @($textureFacePatches.ToArray())) {
    $key = [string]$patch.sourceFaceWord3WadOffset
    if (-not $texturePatchesByWadOffset.ContainsKey($key)) {
        $texturePatchesByWadOffset[$key] = $patch
        [void]$mergedTextureFacePatches.Add($patch)
        continue
    }

    $existing = $texturePatchesByWadOffset[$key]
    if ([string]$existing.oldBytes -ne [string]$patch.oldBytes -or [string]$existing.newBytes -ne [string]$patch.newBytes) {
        throw ("Conflicting terrain texture edits target {0}: {1}->{2} and {3}->{4}." -f $key, $existing.oldBytes, $existing.newBytes, $patch.oldBytes, $patch.newBytes)
    }
}

$wroteBin = $false
if ($AllowExperimentalWrite) {
    $image = if ($null -ne $imageForCustomTextures) { $imageForCustomTextures } else { [System.IO.File]::ReadAllBytes($resolvedImage) }
    foreach ($patch in @($mergedVertexPatches.ToArray())) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.sourceVertexWadOffset)
        $existing = Get-ImageBytesForWadOffset $image $wadOffset 4
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.sourceVertexWadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        $newBytes = New-Object byte[] 4
        $hex = [string]$patch.newBytes
        for ($i = 0; $i -lt 4; $i++) {
            $newBytes[$i] = [byte][Convert]::ToInt32($hex.Substring($i * 2, 2), 16)
        }
        Set-ImageBytesForWadOffset $image $wadOffset $newBytes
    }
    foreach ($patch in @($mergedTextureFacePatches.ToArray())) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.sourceFaceWord3WadOffset)
        $existing = Get-ImageBytesForWadOffset $image $wadOffset 4
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.sourceFaceWord3WadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        $newBytes = New-Object byte[] 4
        $hex = [string]$patch.newBytes
        for ($i = 0; $i -lt 4; $i++) {
            $newBytes[$i] = [byte][Convert]::ToInt32($hex.Substring($i * 2, 2), 16)
        }
        Set-ImageBytesForWadOffset $image $wadOffset $newBytes
    }
    foreach ($patch in @($customTextureResult.patches)) {
        $wadOffset = Convert-HexTextToInt64 ([string]$patch.wadOffset)
        $existing = Get-ImageBytesForWadOffset $image $wadOffset ([int]$patch.length)
        if ((Get-BytesHex $existing) -ne [string]$patch.oldBytes) {
            throw ("Source bytes at {0} are {1}, expected {2}." -f $patch.wadOffset, (Get-BytesHex $existing), $patch.oldBytes)
        }
        $newBytes = New-Object byte[] ([int]$patch.length)
        $hex = [string]$patch.newBytes
        for ($i = 0; $i -lt $newBytes.Length; $i++) {
            $newBytes[$i] = [byte][Convert]::ToInt32($hex.Substring($i * 2, 2), 16)
        }
        Set-ImageBytesForWadOffset $image $wadOffset $newBytes
    }
    [System.IO.File]::WriteAllBytes($resolvedOut, $image)
    Write-Cue $resolvedOut $resolvedCue
    $wroteBin = $true
}
elseif (-not $PlanOnly) {
    Write-Warning "PlanOnly was not set, but AllowExperimentalWrite was also not set. Wrote a plan only."
}

$collisionLpPatchCount = @($mergedVertexPatches.ToArray() | Where-Object { [string]$_.kind -eq "collision-lp-candidate" }).Count
$visualPatchCount = @($mergedVertexPatches.ToArray() | Where-Object { [string]$_.kind -like "visual-*" }).Count

$plan = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    status = if ($wroteBin) { "experimental-runtime-sector-bin-written" } else { "plan-ready" }
    note = "Patches exact serialized runtime scene-sector bytes and optional texture-page bytes found in the logical WAD stream, not the fuzzy visual model source map."
    terrainEditCount = $edits.Count
    sectorPatchCount = $sectorPatchCount
    vertexPatchCount = @($mergedVertexPatches.ToArray()).Count
    textureFacePatchCount = @($mergedTextureFacePatches.ToArray()).Count
    visualPatchCount = $visualPatchCount
    collisionLpPatchCount = $collisionLpPatchCount
    includeCollisionLp = [bool]$IncludeCollisionLp
    collisionLpPadding = [double]$CollisionLpPadding
    maxCollisionLpVerticesPerEdit = [int]$MaxCollisionLpVerticesPerEdit
    rawVertexPatchCount = @($vertexPatches.ToArray()).Count
    rawTextureFacePatchCount = @($textureFacePatches.ToArray()).Count
    customTextureImportCount = [int]$customTextureResult.textureCount
    customTextureBytePatchCount = [int]$customTextureResult.bytePatchCount
    customTextureManifestPath = $(if ($hasCustomTextures) { (Resolve-Path -LiteralPath $resolvedCustomTextures).Path } else { "" })
    customTexturePages = $customTextureResult.texturePages
    customTextureImports = @($customTextureResult.summaries)
    customTextureSamplePatches = @($customTextureResult.patches | Select-Object -First 24)
    imagePath = (Resolve-Path -LiteralPath $resolvedImage).Path
    ramPath = $(if (Test-Path -LiteralPath $resolvedRam) { (Resolve-Path -LiteralPath $resolvedRam).Path } else { "" })
    terrainEditsPath = $(if (Test-Path -LiteralPath $resolvedEdits) { (Resolve-Path -LiteralPath $resolvedEdits).Path } else { "" })
    sourceSearchPath = $(if (Test-Path -LiteralPath $resolvedSearch) { (Resolve-Path -LiteralPath $resolvedSearch).Path } else { "" })
    outPath = $resolvedOut
    cuePath = $resolvedCue
    wroteBin = $wroteBin
    vertexPatches = @($mergedVertexPatches.ToArray())
    textureFacePatches = @($mergedTextureFacePatches.ToArray())
}

$plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedPlan -Encoding UTF8
Write-Markdown ([pscustomobject]$plan) $resolvedMd

Write-Host "Wrote runtime terrain patch plan to $resolvedPlan"
Write-Host "Wrote runtime terrain patch summary to $resolvedMd"
if ($wroteBin) {
    Write-Host "Wrote runtime terrain patch BIN to $resolvedOut"
    Write-Host "Wrote CUE to $resolvedCue"
}
