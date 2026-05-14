param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-texture-face-link-probe.json",
    [string]$OutMarkdownPath = ".\stonehill-texture-face-link-probe.md"
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
        return Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)
    }
    finally {
        $stream.Close()
    }
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

function Point-Key([int]$U, [int]$V) {
    return "{0:X2},{1:X2}" -f ($U -band 0xFF), ($V -band 0xFF)
}

function Segment-Key([int]$U0, [int]$V0, [int]$U1, [int]$V1) {
    return "{0}->{1}" -f (Point-Key $U0 $V0), (Point-Key $U1 $V1)
}

function Undirected-Segment-Key([int]$U0, [int]$V0, [int]$U1, [int]$V1) {
    $a = Point-Key $U0 $V0
    $b = Point-Key $U1 $V1
    if ([string]::CompareOrdinal($a, $b) -le 0) { return "$a|$b" }
    return "$b|$a"
}

function Build-TextureCoordinateIndex([byte[]]$ModelBytes) {
    $textureListSize = [int](Get-UInt32LE $ModelBytes 0)
    $textureCount = [int](Get-UInt32LE $ModelBytes 4)
    $recordBytes = [int](($textureListSize - 8) / $textureCount)
    $chunkCount = [int]($recordBytes / 8)
    $pointMap = @{}
    $segmentMap = @{}
    $undirectedSegmentMap = @{}

    for ($texture = 0; $texture -lt $textureCount; $texture++) {
        $recordOffset = 8 + ($texture * $recordBytes)
        for ($chunk = 0; $chunk -lt $chunkCount; $chunk++) {
            $offset = $recordOffset + ($chunk * 8)
            $u0 = [int]$ModelBytes[$offset]
            $v0 = [int]$ModelBytes[$offset + 1]
            $u1 = [int]$ModelBytes[$offset + 4]
            $v1 = [int]$ModelBytes[$offset + 5]
            foreach ($point in @((Point-Key $u0 $v0), (Point-Key $u1 $v1))) {
                if (-not $pointMap.ContainsKey($point)) { $pointMap[$point] = @{} }
                Add-Count $pointMap[$point] ([string]$texture)
            }
            $seg = Segment-Key $u0 $v0 $u1 $v1
            if (-not $segmentMap.ContainsKey($seg)) { $segmentMap[$seg] = @{} }
            Add-Count $segmentMap[$seg] ([string]$texture)
            $useg = Undirected-Segment-Key $u0 $v0 $u1 $v1
            if (-not $undirectedSegmentMap.ContainsKey($useg)) { $undirectedSegmentMap[$useg] = @{} }
            Add-Count $undirectedSegmentMap[$useg] ([string]$texture)
        }
    }

    return [ordered]@{
        textureListSize = $textureListSize
        textureCount = $textureCount
        recordBytes = $recordBytes
        chunksPerRecord = $chunkCount
        pointMap = $pointMap
        segmentMap = $segmentMap
        undirectedSegmentMap = $undirectedSegmentMap
    }
}

function Read-HpFaceUvRecords([byte[]]$Ram, [uint32]$SceneAddress) {
    $sceneOffset = Convert-PsxAddressToRamOffset $SceneAddress $Ram.Length
    if ($sceneOffset -lt 0) { throw "Scene runtime address is outside main RAM." }
    $sectorCount = [int](Get-UInt32LE $Ram ($sceneOffset + 8))
    if ($sectorCount -le 0 -or $sectorCount -gt 4096) { throw "Scene sector count is not plausible." }
    $faces = New-Object System.Collections.Generic.List[object]

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
            $uv = @(
                [ordered]@{ u = [int]$Ram[$offset + 8]; v = [int]$Ram[$offset + 9] },
                [ordered]@{ u = [int]$Ram[$offset + 10]; v = [int]$Ram[$offset + 11] },
                [ordered]@{ u = [int]$Ram[$offset + 12]; v = [int]$Ram[$offset + 13] },
                [ordered]@{ u = [int]$Ram[$offset + 14]; v = [int]$Ram[$offset + 15] }
            )
            [void]$faces.Add([ordered]@{
                sectorIndex = $sectorIndex
                faceIndex = $face
                faceRuntimeAddress = ("0x{0:X8}" -f (0x80000000L + [int64]$offset))
                uv = $uv
            })
        }
    }
    return @($faces.ToArray())
}

function Texture-Candidates-For-Point($PointMap, [string]$PointKey) {
    if (-not $PointMap.ContainsKey($PointKey)) { return @{} }
    return $PointMap[$PointKey]
}

function Add-Candidates($Target, $Source, [int]$Weight) {
    foreach ($key in $Source.Keys) {
        if ($Target.ContainsKey($key)) { $Target[$key] = [int]$Target[$key] + $Weight }
        else { $Target[$key] = $Weight }
    }
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$resolvedRam = (Resolve-Path -LiteralPath $RamPath -ErrorAction Stop).Path
$modelBytes = [byte[]](Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex)
$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
if ($ram.Length -gt 0x200000) {
    $copy = New-Object byte[] 0x200000
    [Array]::Copy($ram, 0, $copy, 0, 0x200000)
    $ram = $copy
}
$sceneText = if ($SceneRuntimeAddress.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) { $SceneRuntimeAddress.Substring(2) } else { $SceneRuntimeAddress }
$sceneAddress = [Convert]::ToUInt32($sceneText, 16)
$textureIndex = Build-TextureCoordinateIndex $modelBytes
$faces = @(Read-HpFaceUvRecords $ram $sceneAddress)

$pointMatchHistogram = @{}
$edgeMatchHistogram = @{}
$bestTextureHistogram = @{}
$samples = New-Object System.Collections.Generic.List[object]

foreach ($face in $faces) {
    $candidateScores = @{}
    $pointMatches = 0
    foreach ($point in @($face.uv)) {
        $key = Point-Key ([int]$point.u) ([int]$point.v)
        $candidates = Texture-Candidates-For-Point $textureIndex.pointMap $key
        if ($candidates.Count -gt 0) {
            $pointMatches++
            Add-Candidates $candidateScores $candidates 1
        }
    }
    $edgeMatches = 0
    for ($i = 0; $i -lt 4; $i++) {
        $a = $face.uv[$i]
        $b = $face.uv[($i + 1) % 4]
        $direct = Segment-Key ([int]$a.u) ([int]$a.v) ([int]$b.u) ([int]$b.v)
        $undirected = Undirected-Segment-Key ([int]$a.u) ([int]$a.v) ([int]$b.u) ([int]$b.v)
        if ($textureIndex.segmentMap.ContainsKey($direct)) {
            $edgeMatches++
            Add-Candidates $candidateScores $textureIndex.segmentMap[$direct] 4
        }
        elseif ($textureIndex.undirectedSegmentMap.ContainsKey($undirected)) {
            $edgeMatches++
            Add-Candidates $candidateScores $textureIndex.undirectedSegmentMap[$undirected] 3
        }
    }
    Add-Count $pointMatchHistogram ([string]$pointMatches)
    Add-Count $edgeMatchHistogram ([string]$edgeMatches)
    $best = @($candidateScores.Keys | ForEach-Object { [pscustomobject]@{ texture = $_; score = [int]$candidateScores[$_] } } | Sort-Object -Property @{ Expression = { $_.score }; Descending = $true }, texture | Select-Object -First 3)
    if ($best.Count -gt 0) { Add-Count $bestTextureHistogram ([string]$best[0].texture) }
    if ($samples.Count -lt 48 -and ($pointMatches -gt 0 -or $edgeMatches -gt 0)) {
        [void]$samples.Add([ordered]@{
            sectorIndex = [int]$face.sectorIndex
            faceIndex = [int]$face.faceIndex
            faceRuntimeAddress = [string]$face.faceRuntimeAddress
            uv = @($face.uv | ForEach-Object { "{0:X2},{1:X2}" -f [int]$_.u, [int]$_.v })
            pointMatches = $pointMatches
            edgeMatches = $edgeMatches
            bestTextures = @($best)
        })
    }
}

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    sourceRam = $resolvedRam
    sceneRuntimeAddress = ("0x{0:X8}" -f $sceneAddress)
    textureIndex = [ordered]@{
        textureListSize = [int]$textureIndex.textureListSize
        textureCount = [int]$textureIndex.textureCount
        recordBytes = [int]$textureIndex.recordBytes
        chunksPerRecord = [int]$textureIndex.chunksPerRecord
        uniquePoints = [int]$textureIndex.pointMap.Count
        uniqueDirectedSegments = [int]$textureIndex.segmentMap.Count
        uniqueUndirectedSegments = [int]$textureIndex.undirectedSegmentMap.Count
    }
    hpFaceCount = [int]$faces.Count
    pointMatchHistogram = @(Convert-TopCounts $pointMatchHistogram 10)
    edgeMatchHistogram = @(Convert-TopCounts $edgeMatchHistogram 10)
    topBestTextureCandidates = @(Convert-TopCounts $bestTextureHistogram 24)
    matchedFaceSamples = @($samples.ToArray())
    interpretation = "Direct matching between HP face UV byte pairs and texture-record coordinate endpoints is only a first-pass probe. Low match rates imply the face UVs may be local/signed offsets or require a transform/page field."
}

function Write-MarkdownReport($Report, [string]$Path) {
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("# Stone Hill Texture / Face Link Probe")
    [void]$lines.Add("")
    [void]$lines.Add(("Generated: {0}" -f $Report.generatedAt))
    [void]$lines.Add("")
    [void]$lines.Add("## Summary")
    [void]$lines.Add("")
    [void]$lines.Add(("- Texture records: {0} x {1} bytes, {2} chunks per record" -f $Report.textureIndex.textureCount, $Report.textureIndex.recordBytes, $Report.textureIndex.chunksPerRecord))
    [void]$lines.Add(("- Unique texture coordinate endpoints: {0}" -f $Report.textureIndex.uniquePoints))
    [void]$lines.Add(("- Unique texture coordinate segments: {0}" -f $Report.textureIndex.uniqueUndirectedSegments))
    [void]$lines.Add(("- HP faces checked: {0}" -f $Report.hpFaceCount))
    [void]$lines.Add("")
    [void]$lines.Add("## Match Histograms")
    [void]$lines.Add("")
    [void]$lines.Add("Point matches per HP face:")
    foreach ($row in $Report.pointMatchHistogram) { [void]$lines.Add(("- {0}: {1}" -f $row.value, $row.count)) }
    [void]$lines.Add("")
    [void]$lines.Add("Edge matches per HP face:")
    foreach ($row in $Report.edgeMatchHistogram) { [void]$lines.Add(("- {0}: {1}" -f $row.value, $row.count)) }
    [void]$lines.Add("")
    [void]$lines.Add("## Interpretation")
    [void]$lines.Add("")
    [void]$lines.Add($Report.interpretation)
    [void]$lines.Add("")
    [void]$lines.Add("## Matched Samples")
    [void]$lines.Add("")
    foreach ($sample in @($Report.matchedFaceSamples | Select-Object -First 24)) {
        $best = @($sample.bestTextures | ForEach-Object { ("T{0}={1}" -f $_.texture, $_.score) })
        [void]$lines.Add(("- {0} sector {1} face {2}: uv [{3}], point matches {4}, edge matches {5}, best [{6}]" -f $sample.faceRuntimeAddress, $sample.sectorIndex, $sample.faceIndex, [string]::Join(" ", $sample.uv), $sample.pointMatches, $sample.edgeMatches, [string]::Join(", ", $best)))
    }
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
($report | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-MarkdownReport $report $resolvedMarkdown

Write-Host ("Wrote texture/face link probe to {0}" -f $resolvedJson)
Write-Host ("HP faces: {0}; texture endpoints: {1}; texture segments: {2}" -f $faces.Count, $textureIndex.pointMap.Count, $textureIndex.undirectedSegmentMap.Count)
