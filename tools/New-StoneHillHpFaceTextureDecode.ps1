param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-hp-face-texture-decode.json",
    [string]$OutMarkdownPath = ".\stonehill-hp-face-texture-decode.md",
    [int]$MaxSamples = 48
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

function Get-RamBytes([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -gt 0x200000) {
        $copy = New-Object byte[] 0x200000
        [Array]::Copy($bytes, 0, $copy, 0, 0x200000)
        return $copy
    }
    return $bytes
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

function Analyze-TextureList([byte[]]$ModelBytes) {
    $textureListSize = [int](Get-UInt32LE $ModelBytes 0)
    $textureCount = [int](Get-UInt32LE $ModelBytes 4)
    $recordBytes = 0
    if ($textureCount -gt 0 -and $textureListSize -gt 8) {
        $payload = $textureListSize - 8
        if (($payload % $textureCount) -eq 0) { $recordBytes = [int]($payload / $textureCount) }
    }
    $records = New-Object System.Collections.Generic.List[object]
    if ($recordBytes -gt 0) {
        for ($i = 0; $i -lt $textureCount; $i++) {
            $offset = 8 + ($i * $recordBytes)
            if (($offset + $recordBytes) -gt $ModelBytes.Length) { break }
            [void]$records.Add([ordered]@{
                index = $i
                offset = ("0x{0:X}" -f $offset)
                first16 = Format-Bytes $ModelBytes $offset ([Math]::Min(16, $recordBytes))
                firstWords = @(
                    ("0x{0:X8}" -f (Get-UInt32LE $ModelBytes $offset)),
                    ("0x{0:X8}" -f (Get-UInt32LE $ModelBytes ($offset + 4))),
                    ("0x{0:X8}" -f (Get-UInt32LE $ModelBytes ($offset + 8))),
                    ("0x{0:X8}" -f (Get-UInt32LE $ModelBytes ($offset + 12)))
                )
            })
        }
    }
    return [ordered]@{
        textureListSize = $textureListSize
        textureCount = $textureCount
        recordBytes = $recordBytes
        isCleanRecordTable = ($textureCount -gt 0 -and $recordBytes -gt 0 -and ((($textureListSize - 8) % $textureCount) -eq 0))
        records = @($records.ToArray())
    }
}

function New-ByteRangeStats {
    $stats = @{}
    for ($i = 0; $i -lt 256; $i++) { $stats[[string]$i] = 0 }
    return $stats
}

function Read-SceneHpFaces([byte[]]$Ram, [uint32]$SceneAddress, [int]$MaxSamples) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $sectorCount = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($sectorCount -le 0 -or $sectorCount -gt 4096) { throw "Scene sector count is not plausible." }

    $faceCount = 0
    $vertexIndexFailures = 0
    $colourIndexFailuresNum = 0
    $colourIndexFailuresDouble = 0
    $triangleLikeFaces = 0
    $uniqueColourKeys = @{}
    $uniqueUvKeys = @{}
    $uniqueTexPageKeys = @{}
    $byteRanges = @{}
    for ($i = 0; $i -lt 16; $i++) { $byteRanges[[string]$i] = New-ByteRangeStats }
    $uvByteTop = @{}
    $samples = New-Object System.Collections.Generic.List[object]

    for ($sectorIndex = 0; $sectorIndex -lt $sectorCount; $sectorIndex++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($sceneOffset + 12 + ($sectorIndex * 4)))
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeader $Ram $sectorOffset
        if ($null -eq $sector -or [int]$sector.numHpFaces -le 0) { continue }

        $dataStart = [int]$sector.offset + 28
        $lpVertexWords = [int]$sector.numLpVertices
        $lpColourWords = [int]$sector.numLpColours
        $lpFaceWords = [int]$sector.numLpFaces * 2
        $hpVertexStartWords = $lpVertexWords + $lpColourWords + $lpFaceWords
        $hpColourStartWords = $hpVertexStartWords + [int]$sector.numHpVertices
        $hpFaceStartWords = $hpColourStartWords + ([int]$sector.numHpColours * 2)
        $hpFaceStart = $dataStart + ($hpFaceStartWords * 4)

        for ($face = 0; $face -lt [int]$sector.numHpFaces; $face++) {
            $offset = $hpFaceStart + ($face * 16)
            if (($offset + 16) -gt $Ram.Length) { continue }
            $faceCount++

            $vertexBad = $false
            $colourBadNum = $false
            $colourBadDouble = $false
            $verts = @()
            $cols = @()
            for ($i = 0; $i -lt 4; $i++) {
                $v = [int]$Ram[$offset + $i]
                $c = [int]$Ram[$offset + 4 + $i]
                $verts += $v
                $cols += $c
                if ($v -ge [int]$sector.numHpVertices) { $vertexBad = $true }
                if ($c -ge [int]$sector.numHpColours) { $colourBadNum = $true }
                if ($c -ge ([int]$sector.numHpColours * 2)) { $colourBadDouble = $true }
            }
            if ($vertexBad) { $vertexIndexFailures++ }
            if ($colourBadNum) { $colourIndexFailuresNum++ }
            if ($colourBadDouble) { $colourIndexFailuresDouble++ }
            if (($verts | Sort-Object -Unique).Count -lt 4) { $triangleLikeFaces++ }

            $colourKey = "{0:X2} {1:X2} {2:X2} {3:X2}" -f $cols[0], $cols[1], $cols[2], $cols[3]
            Add-Count $uniqueColourKeys $colourKey

            $uvPairs = @(
                @{ u = [int]$Ram[$offset + 8]; v = [int]$Ram[$offset + 9] },
                @{ u = [int]$Ram[$offset + 10]; v = [int]$Ram[$offset + 11] },
                @{ u = [int]$Ram[$offset + 12]; v = [int]$Ram[$offset + 13] },
                @{ u = [int]$Ram[$offset + 14]; v = [int]$Ram[$offset + 15] }
            )
            $uvKey = "{0:X2},{1:X2} {2:X2},{3:X2} {4:X2},{5:X2} {6:X2},{7:X2}" -f $uvPairs[0].u, $uvPairs[0].v, $uvPairs[1].u, $uvPairs[1].v, $uvPairs[2].u, $uvPairs[2].v, $uvPairs[3].u, $uvPairs[3].v
            Add-Count $uniqueUvKeys $uvKey
            foreach ($pair in $uvPairs) {
                Add-Count $uvByteTop ("U{0:X2}" -f [int]$pair.u)
                Add-Count $uvByteTop ("V{0:X2}" -f [int]$pair.v)
            }

            $texKey = "{0:X2} {1:X2} {2:X2} {3:X2}" -f [int]$Ram[$offset + 8], [int]$Ram[$offset + 10], [int]$Ram[$offset + 12], [int]$Ram[$offset + 14]
            Add-Count $uniqueTexPageKeys $texKey

            for ($i = 0; $i -lt 16; $i++) {
                $byte = [int]$Ram[$offset + $i]
                $byteRanges[[string]$i][[string]$byte] = [int]$byteRanges[[string]$i][[string]$byte] + 1
            }

            if ($samples.Count -lt $MaxSamples) {
                [void]$samples.Add([ordered]@{
                    sectorIndex = $sectorIndex
                    sectorRuntimeAddress = [string]$sector.runtimeAddress
                    faceIndex = $face
                    faceRuntimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$offset))
                    bytes = Format-Bytes $Ram $offset 16
                    vertexIndexes = $verts
                    colourIndexes = $cols
                    uvPairs = @($uvPairs | ForEach-Object { [ordered]@{ u = [int]$_.u; v = [int]$_.v } })
                    vertexIndexStatus = if ($vertexBad) { "out-of-range" } else { "ok" }
                    colourIndexStatusNumColours = if ($colourBadNum) { "out-of-range" } else { "ok" }
                    colourIndexStatusDoubleColours = if ($colourBadDouble) { "out-of-range" } else { "ok" }
                    numHpVertices = [int]$sector.numHpVertices
                    numHpColours = [int]$sector.numHpColours
                })
            }
        }
    }

    $byteSummaries = @()
    for ($i = 0; $i -lt 16; $i++) {
        $map = $byteRanges[[string]$i]
        $nonZero = @($map.Keys | Where-Object { [int]$map[$_] -gt 0 })
        $byteSummaries += [ordered]@{
            offset = $i
            uniqueValues = $nonZero.Count
            min = if ($nonZero.Count -gt 0) { [int](@($nonZero | ForEach-Object { [int]$_ } | Measure-Object -Minimum).Minimum) } else { 0 }
            max = if ($nonZero.Count -gt 0) { [int](@($nonZero | ForEach-Object { [int]$_ } | Measure-Object -Maximum).Maximum) } else { 0 }
            topValues = @(Convert-TopCounts $map 10)
        }
    }

    return [ordered]@{
        sceneRuntimeAddress = ("0x{0:X8}" -f $SceneAddress)
        sectorCount = $sectorCount
        faceCount = $faceCount
        vertexIndexFailures = $vertexIndexFailures
        colourIndexFailuresAgainstNumHpColours = $colourIndexFailuresNum
        colourIndexFailuresAgainstDoubleHpColours = $colourIndexFailuresDouble
        triangleLikeFaces = $triangleLikeFaces
        uniqueColourIndexQuads = [int]$uniqueColourKeys.Count
        uniqueUvQuads = [int]$uniqueUvKeys.Count
        uniqueTextureCoordinateKeys = [int]$uniqueTexPageKeys.Count
        topColourIndexQuads = @(Convert-TopCounts $uniqueColourKeys 24)
        topUvQuads = @(Convert-TopCounts $uniqueUvKeys 24)
        topTextureCoordinateKeys = @(Convert-TopCounts $uniqueTexPageKeys 24)
        topUvBytes = @(Convert-TopCounts $uvByteTop 32)
        byteSummaries = $byteSummaries
        sampleFaces = @($samples.ToArray())
    }
}

function Write-MarkdownReport($Report, [string]$Path) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Stone Hill HP Face Texture Decode")
    [void]$lines.Add("")
    [void]$lines.Add(("Generated: {0}" -f $Report.generatedAt))
    [void]$lines.Add("")
    [void]$lines.Add("## Summary")
    [void]$lines.Add("")
    [void]$lines.Add(("- HP face records decoded: {0}" -f $Report.hpFaceDecode.faceCount))
    [void]$lines.Add(("- Vertex index failures in bytes +0..+3: {0}" -f $Report.hpFaceDecode.vertexIndexFailures))
    [void]$lines.Add(('- Colour-index failures if bytes +4..+7 use `numHpColours`: {0}' -f $Report.hpFaceDecode.colourIndexFailuresAgainstNumHpColours))
    [void]$lines.Add(('- Colour-index failures if bytes +4..+7 use `numHpColours * 2`: {0}' -f $Report.hpFaceDecode.colourIndexFailuresAgainstDoubleHpColours))
    [void]$lines.Add(("- Triangle-like HP faces with repeated vertex indexes: {0}" -f $Report.hpFaceDecode.triangleLikeFaces))
    [void]$lines.Add(("- Unique UV quads from bytes +8..+15: {0}" -f $Report.hpFaceDecode.uniqueUvQuads))
    [void]$lines.Add("")
    [void]$lines.Add("## Texture List")
    [void]$lines.Add("")
    [void]$lines.Add(("- Texture-list size: {0}" -f $Report.textureList.textureListSize))
    [void]$lines.Add(("- Texture count word: {0}" -f $Report.textureList.textureCount))
    [void]$lines.Add(("- Inferred record size: {0}" -f $Report.textureList.recordBytes))
    [void]$lines.Add(("- Clean table check: {0}" -f $Report.textureList.isCleanRecordTable))
    [void]$lines.Add("")
    [void]$lines.Add("First texture records:")
    [void]$lines.Add("")
    foreach ($record in @($Report.textureList.records | Select-Object -First 8)) {
        [void]$lines.Add(('- Texture {0}, offset `{1}`: `{2}`' -f $record.index, $record.offset, $record.first16))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Proposed HP Face Layout")
    [void]$lines.Add("")
    [void]$lines.Add("| Bytes | Candidate meaning | Evidence |")
    [void]$lines.Add("| --- | --- | --- |")
    [void]$lines.Add("| +0..+3 | four HP vertex indexes | zero out-of-range failures against sector HP vertex count |")
    [void]$lines.Add("| +4..+7 | four HP colour/lighting indexes | strong index-like distribution; validation target depends on whether HP colour entries are single or paired |")
    [void]$lines.Add("| +8..+15 | four UV-like byte pairs | values look like compact U/V coordinate pairs and produce many unique quads |")
    [void]$lines.Add("")
    [void]$lines.Add("## Byte Summary")
    [void]$lines.Add("")
    [void]$lines.Add("| Offset | Unique | Min | Max | Top values |")
    [void]$lines.Add("| ---: | ---: | ---: | ---: | --- |")
    foreach ($stat in $Report.hpFaceDecode.byteSummaries) {
        $top = @($stat.topValues | Select-Object -First 4 | ForEach-Object { ("{0} ({1})" -f $_.value, $_.count) })
        [void]$lines.Add(("| +{0} | {1} | {2} | {3} | {4} |" -f $stat.offset, $stat.uniqueValues, $stat.min, $stat.max, [string]::Join(", ", $top)))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Top UV Quads")
    [void]$lines.Add("")
    foreach ($row in @($Report.hpFaceDecode.topUvQuads | Select-Object -First 12)) {
        [void]$lines.Add(('- `{0}`: {1}' -f $row.value, $row.count))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Sample Faces")
    [void]$lines.Add("")
    foreach ($sample in @($Report.hpFaceDecode.sampleFaces | Select-Object -First 16)) {
        [void]$lines.Add(('- `{0}` sector {1} face {2}: verts [{3}], colours [{4}], uv [{5}]' -f $sample.bytes, $sample.sectorIndex, $sample.faceIndex, [string]::Join(",", $sample.vertexIndexes), [string]::Join(",", $sample.colourIndexes), [string]::Join(" ", @($sample.uvPairs | ForEach-Object { ("{0},{1}" -f $_.u, $_.v) }))))
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Next Check")
    [void]$lines.Add("")
    [void]$lines.Add("The next concrete check is to render the HP mesh with per-face colours sampled from bytes +4..+7. If that produces recognizable Stone Hill grass/stone/water regions, bytes +4..+7 are confirmed as colour/lighting indexes. Then bytes +8..+15 can be wired into a texture atlas view using the 68 texture records.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$sceneAddressText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddressValue = [Convert]::ToUInt32($sceneAddressText, 16)
$ram = Get-RamBytes $resolvedRam
$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$textureList = Analyze-TextureList ([byte[]]$modelInfo.bytes)
$hpDecode = Read-SceneHpFaces $ram $sceneAddressValue $MaxSamples

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceRam = $resolvedRam
    sourceImage = $resolvedImage
    sceneRuntimeAddress = ("0x{0:X8}" -f $sceneAddressValue)
    wad = [ordered]@{
        wadLba = $WadLba
        assetWadIndex = $AssetWadIndex
        assetOffset = ("0x{0:X}" -f [int64]$modelInfo.assetOffset)
        assetSize = [int64]$modelInfo.assetSize
        modelSubfileIndex = $ModelSubfileIndex
        modelSubfileOffset = ("0x{0:X}" -f [int64]$modelInfo.subfileOffset)
        modelSubfileSize = [int64]$modelInfo.subfileSize
    }
    textureList = $textureList
    hpFaceDecode = $hpDecode
    interpretation = [ordered]@{
        bytes0To3 = "HP vertex indexes"
        bytes4To7 = "candidate HP colour/lighting indexes"
        bytes8To15 = "candidate four U/V byte pairs"
        confidence = "medium; needs rendered visual proof"
    }
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
($report | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-MarkdownReport $report $resolvedMarkdown

Write-Host ("Wrote HP face texture decode to {0}" -f $resolvedJson)
Write-Host ("Wrote markdown summary to {0}" -f $resolvedMarkdown)
Write-Host ("HP faces: {0}; texture records: {1} x {2} bytes; UV quads: {3}" -f $hpDecode.faceCount, $textureList.textureCount, $textureList.recordBytes, $hpDecode.uniqueUvQuads)
