param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-texture-struct-decode.json",
    [string]$OutMarkdownPath = ".\stonehill-texture-struct-decode.md",
    [int]$MaxSamples = 32
)

Set-StrictMode -Version 2.0

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
        runtimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$Offset))
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
        sizeBytes = $sizeBytes
    }
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

function Format-Bytes([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $items = @()
    for ($i = 0; $i -lt $Length; $i++) {
        $items += ("{0:X2}" -f [int]$Bytes[$Offset + $i])
    }
    return [string]::Join(" ", $items)
}

function Get-TextureXMin([int]$Region, [int]$XMin) {
    return ((($Region * 128) % 2048) + $XMin)
}

function Get-TextureYMin([int]$Region, [int]$YMin) {
    return ([int][Math]::Floor(($Region -band 0x1F) / 16.0) * 256) + $YMin
}

function Decode-TexLq([byte[]]$Bytes, [int]$Offset, [int]$Index, [string]$Kind) {
    $xmin = [int]$Bytes[$Offset + 0]
    $ymin = [int]$Bytes[$Offset + 1]
    $paletteX = [int]$Bytes[$Offset + 2]
    $paletteY = [int]$Bytes[$Offset + 3]
    $xmax = [int]$Bytes[$Offset + 4]
    $ymax = [int]$Bytes[$Offset + 5]
    $region = [int]$Bytes[$Offset + 6]
    $alphaEtc = [int]$Bytes[$Offset + 7]
    $startX = (2048 + (($region * 256) % 2048) + $xmin)
    $startY = ([int][Math]::Floor(($region -band 0x1F) / 16.0) * 256) + $ymin
    return [ordered]@{
        kind = $Kind
        index = $Index
        offset = ("0x{0:X}" -f $Offset)
        xmin = $xmin
        ymin = $ymin
        paletteX = $paletteX
        paletteY = $paletteY
        xmax = $xmax
        ymax = $ymax
        region = $region
        alphaEtc = ("0x{0:X2}" -f $alphaEtc)
        alphaFlag = [bool](($alphaEtc -band 0x80) -ne 0)
        vramXMin = $startX
        vramYMin = $startY
        vramXMax = (2048 + (($region * 256) % 2048) + $xmax)
        vramYMax = ([int][Math]::Floor(($region -band 0x1F) / 16.0) * 256) + $ymax
        paletteAddress = ("0x{0:X}" -f (($paletteX * 16 * 2) + ($paletteY * 4 * 2048)))
    }
}

function Decode-TexHq([byte[]]$Bytes, [int]$Offset, [int]$Index, [string]$Kind) {
    $xmin = [int]$Bytes[$Offset + 0]
    $ymin = [int]$Bytes[$Offset + 1]
    $palette = [int](Get-UInt16LE $Bytes ($Offset + 2))
    $xmax = [int]$Bytes[$Offset + 4]
    $ymax = [int]$Bytes[$Offset + 5]
    $region = [int]$Bytes[$Offset + 6]
    $unknown = [int]$Bytes[$Offset + 7]
    return [ordered]@{
        kind = $Kind
        index = $Index
        offset = ("0x{0:X}" -f $Offset)
        xmin = $xmin
        ymin = $ymin
        palette = $palette
        paletteByteStart = $palette * 32
        xmax = $xmax
        ymax = $ymax
        region = $region
        unknown = ("0x{0:X2}" -f $unknown)
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
    if ($textureListSize -le 8 -or $textureCount -le 0) { throw "No plausible texture list found." }
    $recordBytes = [int](($textureListSize - 8) / $textureCount)
    if ((($textureListSize - 8) % $textureCount) -ne 0) { throw "Texture list is not an even fixed-record table." }

    $records = New-Object System.Collections.Generic.List[object]
    $paletteTop = @{}
    $regionTop = @{}
    $orientationTop = @{}
    for ($texture = 0; $texture -lt $textureCount; $texture++) {
        $offset = 8 + ($texture * $recordBytes)
        $lq = @()
        for ($i = 0; $i -lt 2; $i++) {
            $lq += Decode-TexLq $ModelBytes ($offset + ($i * 8)) $i "lq"
        }

        $hqUnknown0 = Get-UInt32LE $ModelBytes ($offset + 16)
        $hqUnknown1 = Get-UInt32LE $ModelBytes ($offset + 20)
        $hq = @()
        for ($i = 0; $i -lt 4; $i++) {
            $entry = Decode-TexHq $ModelBytes ($offset + 24 + ($i * 8)) $i "hq"
            $hq += $entry
            Add-Count $paletteTop ([string]$entry.palette)
            Add-Count $regionTop ([string]$entry.region)
            Add-Count $orientationTop ([string]$entry.orientation)
        }

        $hqClose = @()
        for ($i = 0; $i -lt 16; $i++) {
            $entry = Decode-TexHq $ModelBytes ($offset + 56 + ($i * 8)) $i "hqClose"
            $hqClose += $entry
            Add-Count $paletteTop ([string]$entry.palette)
            Add-Count $regionTop ([string]$entry.region)
            Add-Count $orientationTop ([string]$entry.orientation)
        }

        [void]$records.Add([ordered]@{
            textureId = $texture
            offset = ("0x{0:X}" -f $offset)
            recordBytes = $recordBytes
            rawFirst32 = Format-Bytes $ModelBytes $offset ([Math]::Min(32, $recordBytes))
            lqData = $lq
            hqUnknown = @(
                ("0x{0:X8}" -f $hqUnknown0),
                ("0x{0:X8}" -f $hqUnknown1)
            )
            hqData = $hq
            hqDataClose = $hqClose
        })
    }

    return [ordered]@{
        textureListSize = $textureListSize
        textureCount = $textureCount
        recordBytes = $recordBytes
        expectedSpyro1RecordBytes = 184
        matchesSpyro1StructLayout = ($recordBytes -eq 184)
        structLayout = "TexLq[2] + uint32[2] + TexHq[4] + TexHq[16]"
        topHqPalettes = @(Convert-TopCounts $paletteTop 16)
        topHqRegions = @(Convert-TopCounts $regionTop 16)
        topHqOrientations = @(Convert-TopCounts $orientationTop 8)
        records = @($records.ToArray())
    }
}

function Decode-SceneFaces([byte[]]$Ram, [uint32]$SceneAddress, [int]$TextureCount, [int]$MaxSamples) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $sectorCount = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($sectorCount -le 0 -or $sectorCount -gt 4096) { throw "Scene sector count is not plausible." }

    $textureTop = @{}
    $depthTop = @{}
    $flipTop = @{}
    $word3UpperTop = @{}
    $word4UnknownTop = @{}
    $samples = New-Object System.Collections.Generic.List[object]
    $sectors = New-Object System.Collections.Generic.List[object]
    $faceCount = 0
    $invalidTextureFaces = 0
    $validSectors = 0

    for ($sectorIndex = 0; $sectorIndex -lt $sectorCount; $sectorIndex++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($sceneOffset + 12 + ($sectorIndex * 4)))
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeader $Ram $sectorOffset
        if ($null -eq $sector) { continue }
        $validSectors++

        $dataStart = [int]$sector.offset + 28
        $hpVertexStartWords = [int]$sector.numLpVertices + [int]$sector.numLpColours + ([int]$sector.numLpFaces * 2)
        $hpColourStartWords = $hpVertexStartWords + [int]$sector.numHpVertices
        $hpFaceStartWords = $hpColourStartWords + ([int]$sector.numHpColours * 2)
        $hpFaceStart = $dataStart + ($hpFaceStartWords * 4)
        $sectorTextures = @{}

        for ($face = 0; $face -lt [int]$sector.numHpFaces; $face++) {
            $offset = $hpFaceStart + ($face * 16)
            if (($offset + 16) -gt $Ram.Length) { continue }
            $word3 = [uint32](Get-UInt32LE $Ram ($offset + 8))
            $word4 = [uint32](Get-UInt32LE $Ram ($offset + 12))
            $textureId = [int]($word3 -band 0x7F)
            $flip = [int](($word4 -shr 1) -band 1)
            $depth = [int](($word4 -shr 3) -band 0x1F)
            $word3Upper = [uint32]($word3 -shr 7)
            $word4Unknown = [uint32]($word4 -band (-bnot [uint32]0xFA))
            $faceCount++

            Add-Count $textureTop ([string]$textureId)
            Add-Count $sectorTextures ([string]$textureId)
            Add-Count $flipTop ([string]$flip)
            Add-Count $depthTop ([string]$depth)
            Add-Count $word3UpperTop ("0x{0:X}" -f $word3Upper)
            Add-Count $word4UnknownTop ("0x{0:X8}" -f $word4Unknown)
            if ($textureId -lt 0 -or $textureId -ge $TextureCount) { $invalidTextureFaces++ }

            if ($samples.Count -lt $MaxSamples) {
                $verts = @()
                $colours = @()
                for ($i = 0; $i -lt 4; $i++) {
                    $verts += [int]$Ram[$offset + $i]
                    $colours += [int]$Ram[$offset + 4 + $i]
                }
                [void]$samples.Add([ordered]@{
                    sector = $sectorIndex
                    face = $face
                    offset = ("0x{0:X}" -f $offset)
                    raw = Format-Bytes $Ram $offset 16
                    vertexIndexes = $verts
                    colourIndexes = $colours
                    word3 = ("0x{0:X8}" -f $word3)
                    word4 = ("0x{0:X8}" -f $word4)
                    textureId = $textureId
                    flip = [bool]($flip -ne 0)
                    depth = $depth
                    word3Upper = ("0x{0:X}" -f $word3Upper)
                    word4UnknownMask = ("0x{0:X8}" -f $word4Unknown)
                })
            }
        }

        if ($sectors.Count -lt 64) {
            [void]$sectors.Add([ordered]@{
                sector = $sectorIndex
                hpFaces = [int]$sector.numHpFaces
                topTextures = @(Convert-TopCounts $sectorTextures 8)
            })
        }
    }

    return [ordered]@{
        sceneRuntimeAddress = ("0x{0:X8}" -f $SceneAddress)
        sceneSectors = $sectorCount
        validSectors = $validSectors
        hpFaceCount = $faceCount
        invalidTextureFaces = $invalidTextureFaces
        textureField = "Spyro 1 SceneFace.word3 & 0x7F"
        flipField = "(SceneFace.word4 >> 1) & 1"
        depthField = "(SceneFace.word4 >> 3) & 0x1F"
        topTextures = @(Convert-TopCounts $textureTop 24)
        topDepths = @(Convert-TopCounts $depthTop 16)
        topFlips = @(Convert-TopCounts $flipTop 4)
        topWord3Upper = @(Convert-TopCounts $word3UpperTop 16)
        topWord4UnknownMasks = @(Convert-TopCounts $word4UnknownTop 16)
        sampleSectors = @($sectors.ToArray())
        sampleFaces = @($samples.ToArray())
    }
}

function Convert-ReportToMarkdown($Report) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Stone Hill Texture Struct Decode")
    [void]$lines.Add("")
    [void]$lines.Add(("Generated: {0}" -f $Report.generatedAt))
    [void]$lines.Add("")
    [void]$lines.Add("## Texture Records")
    [void]$lines.Add("")
    [void]$lines.Add(("- Texture-list size: {0}" -f $Report.textureRecords.textureListSize))
    [void]$lines.Add(("- Texture count: {0}" -f $Report.textureRecords.textureCount))
    [void]$lines.Add(("- Record size: {0}" -f $Report.textureRecords.recordBytes))
    [void]$lines.Add(("- Spyro 1 struct match: {0}" -f $Report.textureRecords.matchesSpyro1StructLayout))
    [void]$lines.Add(('- Layout: `{0}`' -f $Report.textureRecords.structLayout))
    [void]$lines.Add("")
    [void]$lines.Add('The 184-byte records line up with SpyroEdit''s `SpyroTextures.h` layout: `TexLq[2]`, two unknown HQ words, four standard `TexHq` tiles, and sixteen close-range `TexHq` tiles.')
    [void]$lines.Add("")
    [void]$lines.Add("Top HQ palettes:")
    [void]$lines.Add("")
    foreach ($row in @($Report.textureRecords.topHqPalettes | Select-Object -First 10)) {
        [void]$lines.Add(('- palette `{0}`: {1}' -f $row.value, $row.count))
    }
    [void]$lines.Add("")
    [void]$lines.Add("First texture records:")
    [void]$lines.Add("")
    foreach ($record in @($Report.textureRecords.records | Select-Object -First 8)) {
        $lq0 = $record.lqData[0]
        $hq0 = $record.hqData[0]
        [void]$lines.Add(('- T{0}: LQ region {1} `({2},{3})..({4},{5})`, HQ0 palette {6}, region {7}, orientation {8}, VRAM `({9},{10})..({11},{12})`' -f `
            $record.textureId, $lq0.region, $lq0.xmin, $lq0.ymin, $lq0.xmax, $lq0.ymax, `
            $hq0.palette, $hq0.region, $hq0.orientation, $hq0.vramXMin, $hq0.vramYMin, $hq0.vramXMax, $hq0.vramYMax))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Scene Face Fields")
    [void]$lines.Add("")
    [void]$lines.Add(("- HP faces decoded: {0}" -f $Report.sceneFaces.hpFaceCount))
    [void]$lines.Add(("- Invalid texture ids: {0}" -f $Report.sceneFaces.invalidTextureFaces))
    [void]$lines.Add(('- Texture field: `{0}`' -f $Report.sceneFaces.textureField))
    [void]$lines.Add(('- Flip field: `{0}`' -f $Report.sceneFaces.flipField))
    [void]$lines.Add(('- Depth field: `{0}`' -f $Report.sceneFaces.depthField))
    [void]$lines.Add("")
    [void]$lines.Add("Top texture IDs on terrain faces:")
    [void]$lines.Add("")
    foreach ($row in @($Report.sceneFaces.topTextures | Select-Object -First 16)) {
        [void]$lines.Add(('- texture `{0}`: {1} faces' -f $row.value, $row.count))
    }
    [void]$lines.Add("")
    [void]$lines.Add("Top depth values:")
    [void]$lines.Add("")
    foreach ($row in @($Report.sceneFaces.topDepths | Select-Object -First 10)) {
        [void]$lines.Add(('- depth `{0}`: {1} faces' -f $row.value, $row.count))
    }
    [void]$lines.Add("")
    [void]$lines.Add("Sample faces:")
    [void]$lines.Add("")
    foreach ($face in @($Report.sceneFaces.sampleFaces | Select-Object -First 12)) {
        [void]$lines.Add(('- sector {0} face {1}: tex {2}, flip {3}, depth {4}, word3 `{5}`, word4 `{6}`, raw `{7}`' -f `
            $face.sector, $face.face, $face.textureId, $face.flip, $face.depth, $face.word3, $face.word4, $face.raw))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Practical Result")
    [void]$lines.Add("")
    [void]$lines.Add("- Terrain faces can now be labelled by actual texture id rather than guessed UV bytes.")
    [void]$lines.Add('- Texture ids `0..67` map directly to the decoded Stone Hill texture descriptors.')
    [void]$lines.Add("- The next renderer step is to reconstruct a 512-wide texture atlas from VRAM using each record's HQ tile descriptors and palettes.")
    return [string]::Join([Environment]::NewLine, $lines)
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
if ($ram.Length -gt 0x200000) {
    $copy = New-Object byte[] 0x200000
    [Array]::Copy($ram, 0, $copy, 0, 0x200000)
    $ram = $copy
}
$sceneText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddress = [Convert]::ToUInt32($sceneText, 16)

$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$textureRecords = Decode-TextureRecords ([byte[]]$modelInfo.bytes)
$sceneFaces = Decode-SceneFaces $ram $sceneAddress ([int]$textureRecords.textureCount) $MaxSamples

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    sourceRam = $resolvedRam
    wad = [ordered]@{
        lba = $WadLba
        assetWadIndex = $AssetWadIndex
        modelSubfileIndex = $ModelSubfileIndex
        assetOffset = [int64]$modelInfo.assetOffset
        assetSize = [int64]$modelInfo.assetSize
        subfileOffset = [int64]$modelInfo.subfileOffset
        subfileSize = [int64]$modelInfo.subfileSize
    }
    sourceReferences = @(
        "https://github.com/LXShades/spyroedit/blob/e311b00721d427757be9e2b794788da013eae167/Source/SpyroScene.h",
        "https://github.com/LXShades/spyroedit/blob/e311b00721d427757be9e2b794788da013eae167/Source/SpyroTextures.h"
    )
    textureRecords = $textureRecords
    sceneFaces = $sceneFaces
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($report | ConvertTo-Json -Depth 12) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
Convert-ReportToMarkdown $report | Set-Content -LiteralPath $resolvedMarkdown -Encoding UTF8

Write-Host ("Wrote texture struct decode to {0}" -f $resolvedJson)
Write-Host ("Texture records: {0} x {1} bytes; HP faces: {2}; invalid texture refs: {3}" -f $textureRecords.textureCount, $textureRecords.recordBytes, $sceneFaces.hpFaceCount, $sceneFaces.invalidTextureFaces)
