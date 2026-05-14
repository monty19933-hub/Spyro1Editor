param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-runtime-scene-overlay.json",
    [int]$MaxSceneCandidates = 4,
    [int]$MaxSectors = 4096,
    [int]$MaxOverlayCandidates = 18,
    [int]$MaxPointsPerCandidate = 16000,
    [int]$MaxEdgesPerCandidate = 22000,
    [int]$MaxPolygonsPerCandidate = 4500,
    [string[]]$Projections = @("xy", "yx", "xz", "zx", "yz", "zy"),
    [switch]$IncludeBruteForce,
    [switch]$BruteForceOnly
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

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Copy-ByteRange([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $copy = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $copy, 0, $Length)
    return $copy
}

function Test-PsxPointerInMainRam([uint32]$Pointer) {
    $address = [uint64]$Pointer
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Convert-PsxAddressToRamOffset([uint32]$Pointer, [int]$RamLength) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return -1 }
    $offset = [int]($Pointer -band 0x001FFFFF)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
}

function Test-PlausibleRuntimeMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 80) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x48]
    $state = [int]$Ram[$Offset + 0x49]
    if ($type -le 0 -or $type -gt 0x7F) { return $false }
    if ($state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + 4)
    $y = Get-Int32LE $Ram ($Offset + 8)
    $z = Get-Int32LE $Ram ($Offset + 12)
    if ([Math]::Abs([int64]$x) -gt 4000000 -or [Math]::Abs([int64]$y) -gt 4000000 -or [Math]::Abs([int64]$z) -gt 4000000) { return $false }
    if ([Math]::Abs([int64]$x) -lt 16 -and [Math]::Abs([int64]$y) -lt 16 -and [Math]::Abs([int64]$z) -lt 16) { return $false }
    return $true
}

function Count-PlausibleMobysAtPointer([byte[]]$Bytes, [int]$WindowBase, [uint32]$Pointer) {
    if (-not (Test-PsxPointerInMainRam $Pointer)) { return 0 }
    $tableOffset = [int]($Pointer -band 0x001FFFFF)
    $count = 0
    for ($i = 0; $i -lt 128; $i++) {
        $recordOffset = $WindowBase + $tableOffset + ($i * 80)
        if (($recordOffset + 80) -gt $Bytes.Length) { break }
        if (Test-PlausibleRuntimeMoby $Bytes $recordOffset) { $count++ }
    }
    return $count
}

function Find-PsxRamWindow([byte[]]$Bytes) {
    $windowSize = 0x200000
    if ($Bytes.Length -lt $windowSize) { throw "RAM dump is smaller than 2 MB." }

    $candidateBases = New-Object System.Collections.Generic.List[int]
    foreach ($base in @(0, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, ($Bytes.Length - $windowSize))) {
        if ($base -ge 0 -and ($base + $windowSize) -le $Bytes.Length -and -not $candidateBases.Contains($base)) {
            [void]$candidateBases.Add($base)
        }
    }
    for ($base = 0; $base -le ($Bytes.Length - $windowSize); $base += 0x10000) {
        if (-not $candidateBases.Contains($base)) { [void]$candidateBases.Add($base) }
    }

    $best = $null
    foreach ($base in $candidateBases) {
        $pointer = Get-UInt32LE $Bytes ($base + 0x75828)
        $count = Count-PlausibleMobysAtPointer $Bytes $base $pointer
        $levelId = Get-UInt32LE $Bytes ($base + 0x758B4)
        $score = $count
        if (Test-PsxPointerInMainRam $pointer) { $score += 4 }
        if ($levelId -gt 0 -and $levelId -lt 0x80) { $score += 2 }
        if ($null -eq $best -or $score -gt $best.score) {
            $best = [ordered]@{
                baseOffset = $base
                pointer = $pointer
                levelId = $levelId
                count = $count
                score = $score
            }
        }
    }

    if ($null -eq $best -or -not (Test-PsxPointerInMainRam ([uint32]$best.pointer)) -or $best.count -eq 0) {
        throw "Could not find a live Spyro moby table in the RAM dump."
    }

    return [ordered]@{
        ram = Copy-ByteRange $Bytes $best.baseOffset $windowSize
        baseOffset = $best.baseOffset
        pointer = [uint32]$best.pointer
        levelId = [uint32]$best.levelId
        mobyCount = [int]$best.count
    }
}

function Get-Bounds($Points) {
    $count = 0
    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    foreach ($point in $Points) {
        $x = [double]$point.x
        $y = [double]$point.y
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
        $count++
    }
    if ($count -eq 0) { return $null }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
}

function Project-Point($Point, [string]$Projection) {
    switch ($Projection) {
        "yx" { return [pscustomobject]@{ x = [double]$Point.y; y = [double]$Point.x; z = [double]$Point.z } }
        "xz" { return [pscustomobject]@{ x = [double]$Point.x; y = [double]$Point.z; z = [double]$Point.y } }
        "zx" { return [pscustomobject]@{ x = [double]$Point.z; y = [double]$Point.x; z = [double]$Point.y } }
        "yz" { return [pscustomobject]@{ x = [double]$Point.y; y = [double]$Point.z; z = [double]$Point.x } }
        "zy" { return [pscustomobject]@{ x = [double]$Point.z; y = [double]$Point.y; z = [double]$Point.x } }
        default { return [pscustomobject]@{ x = [double]$Point.x; y = [double]$Point.y; z = [double]$Point.z } }
    }
}

function Project-Edge($Edge, [string]$Projection) {
    $p1 = Project-Point ([pscustomobject]@{ x = $Edge.x1; y = $Edge.y1; z = $Edge.z1 }) $Projection
    $p2 = Project-Point ([pscustomobject]@{ x = $Edge.x2; y = $Edge.y2; z = $Edge.z2 }) $Projection
    return [pscustomobject]@{ x1 = $p1.x; y1 = $p1.y; x2 = $p2.x; y2 = $p2.y }
}

function Project-Polygon($Polygon, [string]$Projection) {
    $projected = @($Polygon.vertices | ForEach-Object { Project-Point $_ $Projection })
    $result = [ordered]@{
        points = $projected
        avgZ = [double]$Polygon.avgZ
        minZ = [double]$Polygon.minZ
        maxZ = [double]$Polygon.maxZ
        sectorOffset = [string]$Polygon.sectorOffset
    }
    foreach ($name in @("detail", "sectorIndex", "faceIndex", "faceOffset", "textureId", "flip", "depth", "word3", "word4", "vertexIndexes", "colourIndexes", "faceColor")) {
        if ($Polygon.PSObject.Properties.Name -contains $name) {
            $result[$name] = $Polygon.$name
        }
    }
    if ($Polygon.PSObject.Properties.Name -contains "faceVertices") {
        $faceVertices = @($Polygon.faceVertices)
        if ($faceVertices.Count -ge 4 -and $null -ne $faceVertices[0] -and $null -ne $faceVertices[1] -and $null -ne $faceVertices[2] -and $null -ne $faceVertices[3]) {
            $result["textureCorners"] = [ordered]@{
                topLeft = (Project-Point $faceVertices[3] $Projection)
                topRight = (Project-Point $faceVertices[2] $Projection)
                bottomLeft = (Project-Point $faceVertices[0] $Projection)
                bottomRight = (Project-Point $faceVertices[1] $Projection)
            }
        }
    }
    return [pscustomobject]$result
}

function Select-SampledItems($Items, [int]$MaxItems) {
    $array = @($Items)
    if ($array.Count -le $MaxItems) { return $array }
    $step = [Math]::Max(1, [int][Math]::Ceiling($array.Count / [double]$MaxItems))
    $sample = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $array.Count; $i += $step) {
        [void]$sample.Add($array[$i])
    }
    return @($sample.ToArray() | Select-Object -First $MaxItems)
}

function Get-RuntimeMobyPoints([byte[]]$Ram, [uint32]$Pointer) {
    $start = Convert-PsxAddressToRamOffset $Pointer $Ram.Length
    if ($start -lt 0) { return @() }

    $points = New-Object System.Collections.Generic.List[object]
    $badRun = 0
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        if (-not (Test-PlausibleRuntimeMoby $Ram $offset)) {
            $badRun++
            if ($badRun -ge 48) { break }
            continue
        }
        $badRun = 0
        $rawX = Get-Int32LE $Ram ($offset + 4)
        $rawY = Get-Int32LE $Ram ($offset + 8)
        $rawZ = Get-Int32LE $Ram ($offset + 12)
        if ($rawX -eq 0 -and $rawY -eq 4096 -and $rawZ -eq 0) { continue }
        [void]$points.Add([pscustomobject]@{
            x = [double]$rawX / 16.0
            y = [double]$rawY / 16.0
            z = [double]$rawZ / 16.0
        })
    }
    return @($points.ToArray())
}

function Convert-BuildAddress([uint32]$UpperInstruction, [uint32]$LowerInstruction) {
    $upper = [int64]($UpperInstruction -band 0xFFFF)
    $lower = [int64]($LowerInstruction -band 0xFFFF)
    if ($lower -ge 0x8000) { $lower -= 0x10000 }
    return [int](((($upper -shl 16) + $lower) -band 0x003FFFFF))
}

function Read-SceneSectorHeader([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 28) -gt $Ram.Length) { return $null }
    $numLpVertices = [int]$Ram[$Offset + 16]
    $numLpColours = [int]$Ram[$Offset + 17]
    $numLpFaces = [int]$Ram[$Offset + 18]
    $lpUnknown = [int]$Ram[$Offset + 19]
    $numHpVertices = [int]$Ram[$Offset + 20]
    $numHpColours = [int]$Ram[$Offset + 21]
    $numHpFaces = [int]$Ram[$Offset + 22]
    $hpUnknown = [int]$Ram[$Offset + 23]
    $sizeWords = 7 + $numLpVertices + $numLpColours + ($numLpFaces * 2) + $numHpVertices + ($numHpColours * 2) + ($numHpFaces * 4)
    $sizeBytes = $sizeWords * 4
    if ($sizeBytes -lt 28 -or $sizeBytes -gt 0x40000 -or ($Offset + $sizeBytes) -gt $Ram.Length) { return $null }
    if (($numLpVertices + $numHpVertices) -eq 0 -or ($numLpFaces + $numHpFaces) -eq 0) { return $null }

    return [pscustomobject]@{
        offset = $Offset
        centreY = [int](Get-UInt16LE $Ram $Offset)
        centreX = [int](Get-UInt16LE $Ram ($Offset + 2))
        centreRadiusAndFlags = [int](Get-UInt16LE $Ram ($Offset + 4))
        centreZ = [int](Get-UInt16LE $Ram ($Offset + 6))
        xyPos = [uint32](Get-UInt32LE $Ram ($Offset + 8))
        zPos = [uint32](Get-UInt32LE $Ram ($Offset + 12))
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        lpUnknown = $lpUnknown
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
        hpUnknown = $hpUnknown
        zTerminator = [uint32](Get-UInt32LE $Ram ($Offset + 24))
        sizeBytes = $sizeBytes
    }
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
    $flatSector = ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
    if ($flatSector) { $z = $z -shr 3 }
    return [pscustomobject]@{ x = [double]$x; y = [double]$y; z = [double]$z }
}

function New-SceneColorObject([int]$R, [int]$G, [int]$B, [int]$A = 255) {
    return [pscustomobject]([ordered]@{
        r = [Math]::Max(0, [Math]::Min(255, $R))
        g = [Math]::Max(0, [Math]::Min(255, $G))
        b = [Math]::Max(0, [Math]::Min(255, $B))
        a = [Math]::Max(0, [Math]::Min(255, $A))
    })
}

function Read-SceneRawRgbColor([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 3) -gt $Ram.Length) {
        return New-SceneColorObject 96 128 96 255
    }
    return New-SceneColorObject ([int]$Ram[$Offset]) ([int]$Ram[$Offset + 1]) ([int]$Ram[$Offset + 2]) 255
}

function Average-SceneColorObjects($Colors) {
    $count = 0
    $r = 0
    $g = 0
    $b = 0
    foreach ($color in @($Colors)) {
        if ($null -eq $color) { continue }
        $r += [int]$color.r
        $g += [int]$color.g
        $b += [int]$color.b
        $count++
    }
    if ($count -le 0) {
        return New-SceneColorObject 96 128 96 255
    }
    return New-SceneColorObject ([int]($r / $count)) ([int]($g / $count)) ([int]($b / $count)) 255
}

function Add-FaceEdges($Vertices, [int[]]$Indexes, [System.Collections.Generic.List[object]]$Edges, [System.Collections.Generic.HashSet[string]]$EdgeKeys) {
    $ordered = New-Object System.Collections.Generic.List[int]
    foreach ($index in $Indexes) {
        if ($index -lt 0 -or $index -ge $Vertices.Count) { continue }
        if (-not $ordered.Contains($index)) {
            [void]$ordered.Add($index)
        }
    }
    if ($ordered.Count -lt 3) { return }
    for ($i = 0; $i -lt $ordered.Count; $i++) {
        $aIndex = $ordered[$i]
        $bIndex = $ordered[($i + 1) % $ordered.Count]
        if ($aIndex -eq $bIndex) { continue }
        $a = $Vertices[$aIndex]
        $b = $Vertices[$bIndex]
        $keyA = "{0},{1},{2}" -f [int]$a.x, [int]$a.y, [int]$a.z
        $keyB = "{0},{1},{2}" -f [int]$b.x, [int]$b.y, [int]$b.z
        $edgeKey = if ([string]::CompareOrdinal($keyA, $keyB) -le 0) { "$keyA|$keyB" } else { "$keyB|$keyA" }
        if ($EdgeKeys.Add($edgeKey)) {
            [void]$Edges.Add([pscustomobject]@{
                x1 = [double]$a.x; y1 = [double]$a.y; z1 = [double]$a.z
                x2 = [double]$b.x; y2 = [double]$b.y; z2 = [double]$b.z
            })
        }
    }
}

function Add-FacePolygon($Vertices, [int[]]$Indexes, [System.Collections.Generic.List[object]]$Polygons, [string]$SectorOffset, $Metadata = $null) {
    $ordered = New-Object System.Collections.Generic.List[object]
    $faceVertices = New-Object System.Collections.Generic.List[object]
    $seen = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($index in $Indexes) {
        if ($index -lt 0 -or $index -ge $Vertices.Count) {
            [void]$faceVertices.Add($null)
            continue
        }
        [void]$faceVertices.Add($Vertices[$index])
        if ($seen.Add($index)) {
            [void]$ordered.Add($Vertices[$index])
        }
    }
    if ($ordered.Count -lt 3) { return }

    $minZ = [double]::PositiveInfinity
    $maxZ = [double]::NegativeInfinity
    $sumZ = 0.0
    foreach ($vertex in $ordered) {
        $z = [double]$vertex.z
        if ($z -lt $minZ) { $minZ = $z }
        if ($z -gt $maxZ) { $maxZ = $z }
        $sumZ += $z
    }
    $polygon = [ordered]@{
        vertices = @($ordered.ToArray())
        faceVertices = @($faceVertices.ToArray())
        avgZ = ($sumZ / [Math]::Max(1, $ordered.Count))
        minZ = $minZ
        maxZ = $maxZ
        sectorOffset = $SectorOffset
    }
    if ($null -ne $Metadata) {
        foreach ($prop in $Metadata.PSObject.Properties) {
            $polygon[$prop.Name] = $prop.Value
        }
    }
    [void]$Polygons.Add([pscustomobject]$polygon)
}

function Read-SceneSectorGeometry([byte[]]$Ram, $Sector, [string]$Detail) {
    $dataStart = [int]$Sector.offset + 28
    $points = New-Object System.Collections.Generic.List[object]
    $edges = New-Object System.Collections.Generic.List[object]
    $polygons = New-Object System.Collections.Generic.List[object]
    $edgeKeys = New-Object 'System.Collections.Generic.HashSet[string]'

    $sets = @()
    if ($Detail -eq "lp" -or $Detail -eq "all") {
        $sets += [pscustomobject]@{
            vertexStartWords = 0
            vertexCount = [int]$Sector.numLpVertices
            colourStartWords = [int]$Sector.numLpVertices
            colourEntryBytes = 4
            faceStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours
            faceCount = [int]$Sector.numLpFaces
            faceWords = 2
            detail = "lp"
        }
    }
    if ($Detail -eq "hp" -or $Detail -eq "all") {
        $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
        $hpColourStartWords = $hpVertexStartWords + [int]$Sector.numHpVertices
        $hpFaceStartWords = $hpColourStartWords + ([int]$Sector.numHpColours * 2)
        $sets += [pscustomobject]@{
            vertexStartWords = $hpVertexStartWords
            vertexCount = [int]$Sector.numHpVertices
            colourStartWords = $hpColourStartWords
            colourEntryBytes = 8
            faceStartWords = $hpFaceStartWords
            faceCount = [int]$Sector.numHpFaces
            faceWords = 4
            detail = "hp"
        }
    }

    foreach ($set in $sets) {
        if ($set.vertexCount -le 0 -or $set.faceCount -le 0) { continue }
        $vertices = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $set.vertexCount; $i++) {
            $vertexOffset = $dataStart + (([int]$set.vertexStartWords + $i) * 4)
            $word = Get-UInt32LE $Ram $vertexOffset
            $vertex = Convert-SceneVertex $word $Sector
            [void]$vertices.Add($vertex)
            [void]$points.Add($vertex)
        }
        for ($face = 0; $face -lt $set.faceCount; $face++) {
            $faceOffset = $dataStart + (([int]$set.faceStartWords + ($face * [int]$set.faceWords)) * 4)
            if (($faceOffset + 4) -gt $Ram.Length) { continue }
            $indexes = @([int]$Ram[$faceOffset], [int]$Ram[$faceOffset + 1], [int]$Ram[$faceOffset + 2], [int]$Ram[$faceOffset + 3])
            $metadata = [ordered]@{
                detail = [string]$set.detail
                sectorIndex = if ($Sector.PSObject.Properties.Name -contains "sectorIndex") { [int]$Sector.sectorIndex } else { -1 }
                faceIndex = [int]$face
                faceOffset = ("0x{0:X}" -f $faceOffset)
                vertexIndexes = @($indexes)
            }
            if ([string]$set.detail -eq "hp" -and ($faceOffset + 16) -le $Ram.Length) {
                $word3 = [uint32](Get-UInt32LE $Ram ($faceOffset + 8))
                $word4 = [uint32](Get-UInt32LE $Ram ($faceOffset + 12))
                $colourIndexes = @([int]$Ram[$faceOffset + 4], [int]$Ram[$faceOffset + 5], [int]$Ram[$faceOffset + 6], [int]$Ram[$faceOffset + 7])
                $colourStart = $dataStart + ([int]$set.colourStartWords * 4)
                $vertexColors = New-Object System.Collections.Generic.List[object]
                foreach ($colourIndex in $colourIndexes) {
                    $entryOffset = $colourStart + ($colourIndex * [int]$set.colourEntryBytes)
                    # Stone Hill's HP colour entry +4 raw RGB variant separates grass,
                    # stone, and water correctly in the contact-sheet decode.
                    [void]$vertexColors.Add((Read-SceneRawRgbColor $Ram ($entryOffset + 4)))
                }
                $metadata.textureId = [int]($word3 -band 0x7F)
                $metadata.flip = [bool]((($word4 -shr 1) -band 1) -ne 0)
                $metadata.depth = [int](($word4 -shr 3) -band 0x1F)
                $metadata.word3 = ("0x{0:X8}" -f $word3)
                $metadata.word4 = ("0x{0:X8}" -f $word4)
                $metadata.colourIndexes = @($colourIndexes)
                $metadata.faceColor = Average-SceneColorObjects @($vertexColors.ToArray())
            }
            Add-FaceEdges $vertices $indexes $edges $edgeKeys
            Add-FacePolygon $vertices $indexes $polygons ("0x{0:X}" -f [int]$Sector.offset) ([pscustomobject]$metadata)
        }
    }

    return [pscustomobject]@{ points = @($points.ToArray()); edges = @($edges.ToArray()); polygons = @($polygons.ToArray()) }
}

function Test-SceneAtOffset([byte[]]$Ram, [int]$SceneOffset, [string]$Source, [int]$MaxSectors) {
    if ($SceneOffset -lt 0 -or ($SceneOffset + 16) -gt $Ram.Length) { return $null }
    $size = [uint32](Get-UInt32LE $Ram $SceneOffset)
    $iForget = [uint32](Get-UInt32LE $Ram ($SceneOffset + 4))
    $numSectors = [uint32](Get-UInt32LE $Ram ($SceneOffset + 8))
    if ($numSectors -lt 1 -or $numSectors -gt [uint32]$MaxSectors) { return $null }
    if ((12 + ([int64]$numSectors * 4)) -gt $Ram.Length) { return $null }
    if (($SceneOffset + 12 + ([int64]$numSectors * 4)) -gt $Ram.Length) { return $null }
    # SpyroEdit names this first field "size", but in the Stone Hill RAM scene it
    # does not behave like a bounded byte size. The pointer table is the reliable
    # shape check here.

    $valid = 0
    $terminators = 0
    $hpVertices = 0
    $hpFaces = 0
    $lpVertices = 0
    $lpFaces = 0
    $sectors = New-Object System.Collections.Generic.List[object]
    $firstPointers = New-Object System.Collections.Generic.List[string]

    for ($i = 0; $i -lt [int]$numSectors; $i++) {
        $ptr = [uint32](Get-UInt32LE $Ram ($SceneOffset + 12 + ($i * 4)))
        if ($i -lt 6) { [void]$firstPointers.Add(("0x{0:X8}" -f $ptr)) }
        if (-not (Test-PsxPointerInMainRam $ptr)) { continue }
        $sectorOffset = Convert-PsxAddressToRamOffset $ptr $Ram.Length
        if ($sectorOffset -lt 0) { continue }
        $sector = Read-SceneSectorHeader $Ram $sectorOffset
        if ($null -eq $sector) { continue }
        $valid++
        if ((([int64]$sector.zTerminator) -band 0xFFFFFFFFL) -eq 0xFFFFFFFFL) { $terminators++ }
        $hpVertices += [int]$sector.numHpVertices
        $hpFaces += [int]$sector.numHpFaces
        $lpVertices += [int]$sector.numLpVertices
        $lpFaces += [int]$sector.numLpFaces
        $sector | Add-Member -NotePropertyName sectorIndex -NotePropertyValue $i -Force
        [void]$sectors.Add($sector)
    }

    if ($valid -lt 4) { return $null }
    $validRatio = [double]$valid / [double]$numSectors
    if ($validRatio -lt 0.55) { return $null }
    if (($hpVertices + $lpVertices) -lt 64 -or ($hpFaces + $lpFaces) -lt 32) { return $null }

    $score = ($valid * 10.0) + ($terminators * 3.0) + ([Math]::Min(8000, $hpVertices + $lpVertices) / 25.0)
    if ($Source -eq "code-pointer") { $score += 500.0 }
    if ($validRatio -gt 0.9) { $score += 200.0 }
    return [pscustomobject]@{
        sceneOffset = $SceneOffset
        runtimeAddress = ("0x{0:X8}" -f ([uint64][Convert]::ToUInt64("80000000", 16) + [uint64]$SceneOffset))
        source = $Source
        score = [double]$score
        size = [uint32]$size
        iForget = [uint32]$iForget
        numSectors = [int]$numSectors
        validSectors = [int]$valid
        terminatorSectors = [int]$terminators
        hpVertices = [int]$hpVertices
        hpFaces = [int]$hpFaces
        lpVertices = [int]$lpVertices
        lpFaces = [int]$lpFaces
        firstPointers = @($firstPointers.ToArray())
        sectors = @($sectors.ToArray())
    }
}

function Find-SceneCandidatesByCode([byte[]]$Ram, [int]$MaxSectors) {
    $results = New-Object System.Collections.Generic.List[object]
    $inst02023021 = [Convert]::ToUInt32("02023021", 16)
    $inst00C08021 = [Convert]::ToUInt32("00C08021", 16)
    $inst24C60004 = [Convert]::ToUInt32("24C60004", 16)
    $inst8CC20000 = [Convert]::ToUInt32("8CC20000", 16)
    $wordCount = [int][Math]::Floor($Ram.Length / 4)
    for ($i = 0; $i -lt ($wordCount - 8); $i++) {
        $offset = $i * 4
        $w0 = Get-UInt32LE $Ram $offset
        if ($w0 -ne $inst02023021) { continue }
        $w1 = Get-UInt32LE $Ram ($offset + 4)
        $w2 = Get-UInt32LE $Ram ($offset + 8)
        $w3 = Get-UInt32LE $Ram ($offset + 12)
        $w4 = Get-UInt32LE $Ram ($offset + 16)
        $w5 = Get-UInt32LE $Ram ($offset + 20)
        $w6 = Get-UInt32LE $Ram ($offset + 24)
        if ($w1 -ne $inst00C08021 -or $w2 -ne $inst24C60004 -or $w3 -ne $inst8CC20000 -or $w4 -ne $inst24C60004) { continue }
        if (($w5 -shr 16) -ne [uint32]0x3C01 -or ($w6 -shr 16) -ne [uint32]0xAC26) { continue }

        $addr = Convert-BuildAddress $w5 $w6
        if ($addr -lt 0 -or ($addr + 4) -gt $Ram.Length) { continue }
        $ptr = [uint32](Get-UInt32LE $Ram $addr)
        $sceneOffset = [int]((([int64]$ptr - 0x0C) -band 0x001FFFFF))
        $candidate = Test-SceneAtOffset $Ram $sceneOffset "code-pointer" $MaxSectors
        if ($null -ne $candidate) {
            $candidate | Add-Member -NotePropertyName codeOffset -NotePropertyValue ("0x{0:X}" -f $offset)
            $candidate | Add-Member -NotePropertyName pointerLoadOffset -NotePropertyValue ("0x{0:X}" -f $addr)
            $candidate | Add-Member -NotePropertyName pointerValue -NotePropertyValue ("0x{0:X8}" -f $ptr)
            [void]$results.Add($candidate)
        }
    }
    return @($results.ToArray())
}

function Find-SceneCandidatesByScan([byte[]]$Ram, [int]$MaxSectors) {
    $results = New-Object System.Collections.Generic.List[object]
    for ($offset = 0; $offset -lt ($Ram.Length - 32); $offset += 4) {
        $numSectors = [uint32](Get-UInt32LE $Ram ($offset + 8))
        if ($numSectors -lt 4 -or $numSectors -gt [uint32]$MaxSectors) { continue }
        $first = [uint32](Get-UInt32LE $Ram ($offset + 12))
        $second = [uint32](Get-UInt32LE $Ram ($offset + 16))
        if (-not (Test-PsxPointerInMainRam $first) -or -not (Test-PsxPointerInMainRam $second)) { continue }
        $firstOffset = Convert-PsxAddressToRamOffset $first $Ram.Length
        if ($firstOffset -lt 0 -or $null -eq (Read-SceneSectorHeader $Ram $firstOffset)) { continue }
        $candidate = Test-SceneAtOffset $Ram $offset "brute-scan" $MaxSectors
        if ($null -ne $candidate) {
            [void]$results.Add($candidate)
        }
    }
    return @($results.ToArray())
}

function Get-MobyProjectionStats($MobyPoints, [string]$Projection, $GeometryBounds) {
    $projected = @($MobyPoints | ForEach-Object { Project-Point $_ $Projection })
    $mobyBounds = Get-Bounds $projected
    if ($null -eq $mobyBounds -or $null -eq $GeometryBounds) {
        return [pscustomobject]@{ inside = 0; fitScore = 0.0; mobyBounds = $mobyBounds }
    }
    $inside = 0
    foreach ($point in $projected) {
        if ([double]$point.x -ge [double]$GeometryBounds.minX -and [double]$point.x -le [double]$GeometryBounds.maxX -and
            [double]$point.y -ge [double]$GeometryBounds.minY -and [double]$point.y -le [double]$GeometryBounds.maxY) {
            $inside++
        }
    }
    $geomSpanX = [Math]::Max(1.0, [double]$GeometryBounds.maxX - [double]$GeometryBounds.minX)
    $geomSpanY = [Math]::Max(1.0, [double]$GeometryBounds.maxY - [double]$GeometryBounds.minY)
    $mobySpanX = [Math]::Max(1.0, [double]$mobyBounds.maxX - [double]$mobyBounds.minX)
    $mobySpanY = [Math]::Max(1.0, [double]$mobyBounds.maxY - [double]$mobyBounds.minY)
    $ratioX = [Math]::Min($geomSpanX, $mobySpanX) / [Math]::Max($geomSpanX, $mobySpanX)
    $ratioY = [Math]::Min($geomSpanY, $mobySpanY) / [Math]::Max($geomSpanY, $mobySpanY)
    $insideRatio = if ($projected.Count -gt 0) { [double]$inside / [double]$projected.Count } else { 0.0 }
    $fit = ($insideRatio * 60.0) + ($ratioX * 20.0) + ($ratioY * 20.0)
    return [pscustomobject]@{ inside = $inside; fitScore = $fit; mobyBounds = $mobyBounds }
}

function New-OverlayCandidate($Scene, [string]$Detail, [string]$Projection, $Points3D, $Edges3D, $Polygons3D, $MobyPoints) {
    $projectedPointsAll = @($Points3D | ForEach-Object { Project-Point $_ $Projection })
    $projectedEdgesAll = @($Edges3D | ForEach-Object { Project-Edge $_ $Projection })
    $projectedPolygonsAll = @($Polygons3D | ForEach-Object { Project-Polygon $_ $Projection })
    $bounds = Get-Bounds $projectedPointsAll
    $stats = Get-MobyProjectionStats $MobyPoints $Projection $bounds
    $sampledPoints = Select-SampledItems $projectedPointsAll $MaxPointsPerCandidate
    $sampledEdges = Select-SampledItems $projectedEdgesAll $MaxEdgesPerCandidate
    $sampledPolygons = Select-SampledItems $projectedPolygonsAll $MaxPolygonsPerCandidate
    $minZ = [double]::PositiveInfinity
    $maxZ = [double]::NegativeInfinity
    foreach ($point in $Points3D) {
        $z = [double]$point.z
        if ($z -lt $minZ) { $minZ = $z }
        if ($z -gt $maxZ) { $maxZ = $z }
    }
    $heightBounds = if ([double]::IsInfinity($minZ) -or [double]::IsInfinity($maxZ)) { $null } else { [pscustomobject]@{ minZ = $minZ; maxZ = $maxZ } }
    return [ordered]@{
        runtimeAddress = ("{0} {1} {2}" -f $Scene.runtimeAddress, $Detail, $Projection)
        source = [string]$Scene.source
        sceneRuntimeAddress = [string]$Scene.runtimeAddress
        sceneOffset = ("0x{0:X}" -f [int]$Scene.sceneOffset)
        encoding = "spyroedit-runtime-scene-sector"
        detail = $Detail
        projection = "runtime-scene-$Projection"
        editorPlane = if ($Projection -eq "xy") { "runtime-moby-xy" } else { "diagnostic-axis-probe" }
        fitScore = [double]$stats.fitScore
        mobysInsideProjection = [int]$stats.inside
        edgeSource = "scene-sector-faces"
        sectorCount = [int]$Scene.numSectors
        validSectors = [int]$Scene.validSectors
        validVertices = [int]$Points3D.Count
        validEdges = [int]$Edges3D.Count
        validPolygons = [int]$Polygons3D.Count
        sampledVertices = [int]$sampledPoints.Count
        sampledEdges = [int]$sampledEdges.Count
        sampledPolygons = [int]$sampledPolygons.Count
        totalEdges = [int]$Edges3D.Count
        totalPolygons = [int]$Polygons3D.Count
        projectedBounds = $bounds
        heightBounds = $heightBounds
        runtimeMobyBounds = $stats.mobyBounds
        polygons = @($sampledPolygons)
        projectedPoints = @($sampledPoints)
        edges = @($sampledEdges)
    }
}

function Get-ProjectionPriority($Candidate) {
    $projection = [string]$Candidate["projection"]
    switch ($projection) {
        "runtime-scene-xy" { return 0 }
        "runtime-scene-yx" { return 1 }
        "runtime-scene-xz" { return 2 }
        "runtime-scene-zx" { return 3 }
        "runtime-scene-yz" { return 4 }
        "runtime-scene-zy" { return 5 }
        default { return 9 }
    }
}

function Get-DetailPriority($Candidate) {
    $detail = [string]$Candidate["detail"]
    switch ($detail) {
        "hp" { return 0 }
        "all" { return 1 }
        "lp" { return 2 }
        default { return 9 }
    }
}

function Build-SceneGeometry($Scene, [byte[]]$Ram, [string]$Detail) {
    $points = New-Object System.Collections.Generic.List[object]
    $edges = New-Object System.Collections.Generic.List[object]
    $polygons = New-Object System.Collections.Generic.List[object]
    foreach ($sector in @($Scene.sectors)) {
        $geometry = Read-SceneSectorGeometry $Ram $sector $Detail
        foreach ($point in @($geometry.points)) { [void]$points.Add($point) }
        foreach ($edge in @($geometry.edges)) { [void]$edges.Add($edge) }
        foreach ($polygon in @($geometry.polygons)) { [void]$polygons.Add($polygon) }
    }
    return [pscustomobject]@{ points = @($points.ToArray()); edges = @($edges.ToArray()); polygons = @($polygons.ToArray()) }
}

$resolvedRam = Resolve-Path -LiteralPath $RamPath -ErrorAction Stop
$rawBytes = [System.IO.File]::ReadAllBytes($resolvedRam.Path)
$window = Find-PsxRamWindow $rawBytes
$ram = [byte[]]$window.ram
$mobyPoints = @(Get-RuntimeMobyPoints $ram ([uint32]$window.pointer))

$sceneCandidates = New-Object System.Collections.Generic.List[object]
if (-not $BruteForceOnly) {
    foreach ($candidate in @(Find-SceneCandidatesByCode $ram $MaxSectors)) {
        [void]$sceneCandidates.Add($candidate)
    }
}
if ($BruteForceOnly -or $IncludeBruteForce -or $sceneCandidates.Count -eq 0) {
    foreach ($candidate in @(Find-SceneCandidatesByScan $ram $MaxSectors)) {
        $duplicate = $false
        foreach ($existing in @($sceneCandidates.ToArray())) {
            if ([int]$existing.sceneOffset -eq [int]$candidate.sceneOffset) {
                $duplicate = $true
                break
            }
        }
        if (-not $duplicate) {
            [void]$sceneCandidates.Add($candidate)
        }
    }
}

$rankedScenes = @($sceneCandidates.ToArray() | Sort-Object -Property @{ Expression = { [double]$_.score }; Descending = $true }, sceneOffset | Select-Object -First $MaxSceneCandidates)
if ($rankedScenes.Count -eq 0) {
    throw "Could not find a plausible Spyro runtime scene sector table in this RAM dump."
}

$overlayCandidates = New-Object System.Collections.Generic.List[object]
foreach ($scene in $rankedScenes) {
    foreach ($detail in @("hp", "lp", "all")) {
        $geometry = Build-SceneGeometry $scene $ram $detail
        if ($geometry.points.Count -lt 16 -or $geometry.edges.Count -lt 8) { continue }
        foreach ($projection in $Projections) {
            [void]$overlayCandidates.Add((New-OverlayCandidate $scene $detail $projection @($geometry.points) @($geometry.edges) @($geometry.polygons) $mobyPoints))
        }
    }
}

$rankedOverlayCandidates = @($overlayCandidates.ToArray() | Sort-Object -Property @{ Expression = { Get-ProjectionPriority $_ }; Descending = $false }, @{ Expression = { Get-DetailPriority $_ }; Descending = $false }, @{ Expression = { [double]$_["fitScore"] }; Descending = $true }, @{ Expression = { [int]$_["sampledEdges"] }; Descending = $true } | Select-Object -First $MaxOverlayCandidates)

$sceneSummary = @($rankedScenes | ForEach-Object {
    [ordered]@{
        runtimeAddress = [string]$_.runtimeAddress
        source = [string]$_.source
        score = [Math]::Round([double]$_.score, 2)
        size = ("0x{0:X}" -f [uint32]$_.size)
        iForget = ("0x{0:X8}" -f [uint32]$_.iForget)
        numSectors = [int]$_.numSectors
        validSectors = [int]$_.validSectors
        terminatorSectors = [int]$_.terminatorSectors
        hpVertices = [int]$_.hpVertices
        hpFaces = [int]$_.hpFaces
        lpVertices = [int]$_.lpVertices
        lpFaces = [int]$_.lpFaces
        firstPointers = @($_.firstPointers)
    }
})

$output = [ordered]@{
    sourceRam = $resolvedRam.Path
    generatedAt = (Get-Date).ToString("s")
    note = "Runtime scene geometry decoded from live PS1 RAM using the SpyroEdit scene-sector structures. This bypasses WAD compression and should be treated as the current best Stone Hill geometry source."
    ramWindow = [ordered]@{
        baseOffset = ("0x{0:X}" -f [int]$window.baseOffset)
        levelId = ("0x{0:X}" -f [uint32]$window.levelId)
        mobyPointer = ("0x{0:X8}" -f [uint32]$window.pointer)
        mobyCount = [int]$window.mobyCount
    }
    sceneCandidates = $sceneSummary
    candidates = $rankedOverlayCandidates
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
($output | ConvertTo-Json -Depth 8 -Compress) | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
Write-Host ("Wrote runtime scene overlay to {0}" -f $resolvedOut)
Write-Host ("Scene candidates: {0}; overlay candidates: {1}; best: {2}" -f $rankedScenes.Count, $rankedOverlayCandidates.Count, [string]$rankedOverlayCandidates[0].runtimeAddress)
