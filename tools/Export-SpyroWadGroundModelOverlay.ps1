param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutPath = ".\stonehill-wad-ground-model-overlay.json",
    [int]$MaxCandidates = 36,
    [int]$MaxPointsPerCandidate = 3500,
    [int]$MaxEdgesPerCandidate = 4500,
    [int]$PartHeaderShift = 0,
    [int[]]$ModelPolyIndexOffsets = @(0),
    [int[]]$ModelPolyVertexCounts = @(4),
    [double]$MaxModelEdgeLength = 0,
    [switch]$Candidate4Only,
    [switch]$PrefixProbe,
    [switch]$BBoxProbe,
    [switch]$SignedGlobalProbe,
    [switch]$IncludeLod,
    [switch]$EdgeProbe
)

Set-StrictMode -Version 2.0

if ($EdgeProbe) {
    $Candidate4Only = $true
    $ModelPolyIndexOffsets = @(4, 5, 6)
    $ModelPolyVertexCounts = @(3, 4)
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

function Convert-SignedByte([int]$Value) {
    if ($Value -ge 128) { return ($Value - 256) }
    return $Value
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

function Get-BytePermutations {
    return @(
        @(0, 1, 2, 3), @(0, 1, 3, 2), @(0, 2, 1, 3), @(0, 2, 3, 1), @(0, 3, 1, 2), @(0, 3, 2, 1),
        @(1, 0, 2, 3), @(1, 0, 3, 2), @(1, 2, 0, 3), @(1, 2, 3, 0), @(1, 3, 0, 2), @(1, 3, 2, 0),
        @(2, 0, 1, 3), @(2, 0, 3, 1), @(2, 1, 0, 3), @(2, 1, 3, 0), @(2, 3, 0, 1), @(2, 3, 1, 0),
        @(3, 0, 1, 2), @(3, 0, 2, 1), @(3, 1, 0, 2), @(3, 1, 2, 0), @(3, 2, 0, 1), @(3, 2, 1, 0)
    )
}

function Convert-PackedVertex([byte[]]$Bytes, [int]$Offset, [string]$Endian, [bool]$SignedLocal) {
    if (($Offset + 4) -gt $Bytes.Length) { return $null }
    $raw = Get-UInt32LE $Bytes $Offset
    if ($Endian -eq "be-bits") {
        $raw = (($raw -band 0x000000FF) -shl 24) -bor (($raw -band 0x0000FF00) -shl 8) -bor (($raw -band 0x00FF0000) -shr 8) -bor (($raw -band 0xFF000000) -shr 24)
    }
    $x = [int]($raw -band 0x7FF)
    $y = [int](($raw -shr 11) -band 0x7FF)
    $z = [int](($raw -shr 22) -band 0x3FF)
    if ($SignedLocal) {
        if ($x -ge 1024) { $x -= 2048 }
        if ($y -ge 1024) { $y -= 2048 }
        if ($z -ge 512) { $z -= 1024 }
    }
    return [pscustomobject]@{ x = $x; y = $y; z = $z; raw = ("0x{0:X8}" -f $raw) }
}

function Get-PartGlobal([byte[]]$Bytes, [int]$PartOffset, [string]$Mode, [int]$Scale) {
    if (($PartOffset + 4) -gt $Bytes.Length -or $Scale -eq 0) {
        return [pscustomobject]@{ x = 0; y = 0; z = 0 }
    }
    $b0 = [int]$Bytes[$PartOffset]
    $b1 = [int]$Bytes[$PartOffset + 1]
    $b3 = [int]$Bytes[$PartOffset + 3]
    switch ($Mode) {
        "doc-gxgygz" { return [pscustomobject]@{ x = ($b1 * $Scale); y = ($b0 * $Scale); z = ($b3 * $Scale) } }
        "swapped-gxgygz" { return [pscustomobject]@{ x = ($b0 * $Scale); y = ($b1 * $Scale); z = ($b3 * $Scale) } }
        "doc-signed-gxgygz" { return [pscustomobject]@{ x = ((Convert-SignedByte $b1) * $Scale); y = ((Convert-SignedByte $b0) * $Scale); z = ((Convert-SignedByte $b3) * $Scale) } }
        "swapped-signed-gxgygz" { return [pscustomobject]@{ x = ((Convert-SignedByte $b0) * $Scale); y = ((Convert-SignedByte $b1) * $Scale); z = ((Convert-SignedByte $b3) * $Scale) } }
        default { return [pscustomobject]@{ x = 0; y = 0; z = 0 } }
    }
}

function Get-PartGlobal2([byte[]]$Bytes, [int]$PartOffset, [int]$PrefixOffset, [string]$Mode, [int]$Scale) {
    if ($Mode.StartsWith("prefix-bbox-")) {
        return [pscustomobject]@{ x = 0; y = 0; z = 0 }
    }
    if ($Mode.StartsWith("prefix-u16-")) {
        if ($PrefixOffset -lt 0 -or ($PrefixOffset + 12) -gt $Bytes.Length -or $Scale -eq 0) {
            return [pscustomobject]@{ x = 0; y = 0; z = 0 }
        }
        $parts = @($Mode.Substring("prefix-u16-".Length).Split("-"))
        if ($parts.Count -lt 3) { return [pscustomobject]@{ x = 0; y = 0; z = 0 } }
        $words = @()
        for ($i = 0; $i -lt 6; $i++) {
            $words += [int][BitConverter]::ToUInt16($Bytes, ($PrefixOffset + ($i * 2)))
        }
        $ix = [Math]::Max(0, [Math]::Min(5, [int]$parts[0]))
        $iy = [Math]::Max(0, [Math]::Min(5, [int]$parts[1]))
        $iz = [Math]::Max(0, [Math]::Min(5, [int]$parts[2]))
        return [pscustomobject]@{
            x = ($words[$ix] * $Scale)
            y = ($words[$iy] * $Scale)
            z = ($words[$iz] * $Scale)
        }
    }
    return Get-PartGlobal $Bytes $PartOffset $Mode $Scale
}

function Convert-RangeValue([double]$Value, [int]$SourceBits, [double]$Min, [double]$Max) {
    $denominator = [Math]::Max(1.0, [Math]::Pow(2.0, $SourceBits) - 1.0)
    return $Min + (($Value / $denominator) * ($Max - $Min))
}

function Convert-PrefixBBoxVertex($Decoded, [byte[]]$Bytes, [int]$PrefixOffset, [string]$Mode) {
    if (-not $Mode.StartsWith("prefix-bbox-")) { return $null }
    if ($PrefixOffset -lt 0 -or ($PrefixOffset + 12) -gt $Bytes.Length) { return $null }

    $withoutPrefix = $Mode.Substring("prefix-bbox-".Length)
    $modeParts = @($withoutPrefix.Split("-"))
    if ($modeParts.Count -lt 8) { return $null }
    $localMap = [string]$modeParts[7]
    if ([string]::IsNullOrWhiteSpace($localMap) -or $localMap.Length -lt 3) { $localMap = "xyz" }

    $words = @()
    for ($i = 0; $i -lt 6; $i++) {
        $words += [int][BitConverter]::ToUInt16($Bytes, ($PrefixOffset + ($i * 2)))
    }

    $x0 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[0]))
    $x1 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[1]))
    $y0 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[2]))
    $y1 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[3]))
    $z0 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[4]))
    $z1 = [Math]::Max(0, [Math]::Min(5, [int]$modeParts[5]))

    $axisValues = @{
        x = [pscustomobject]@{ value = [double]$Decoded.x; bits = 11 }
        y = [pscustomobject]@{ value = [double]$Decoded.y; bits = 11 }
        z = [pscustomobject]@{ value = [double]$Decoded.z; bits = 10 }
    }

    $srcX = $axisValues[[string]$localMap[0]]
    $srcY = $axisValues[[string]$localMap[1]]
    $srcZ = $axisValues[[string]$localMap[2]]
    if ($null -eq $srcX -or $null -eq $srcY -or $null -eq $srcZ) { return $null }

    return [pscustomobject]@{
        x = Convert-RangeValue $srcX.value $srcX.bits ([Math]::Min($words[$x0], $words[$x1])) ([Math]::Max($words[$x0], $words[$x1]))
        y = Convert-RangeValue $srcY.value $srcY.bits ([Math]::Min($words[$y0], $words[$y1])) ([Math]::Max($words[$y0], $words[$y1]))
        z = Convert-RangeValue $srcZ.value $srcZ.bits ([Math]::Min($words[$z0], $words[$z1])) ([Math]::Max($words[$z0], $words[$z1]))
    }
}

function Get-Bounds($Points) {
    if (@($Points).Count -eq 0) { return $null }
    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    foreach ($point in $Points) {
        if ([double]$point.x -lt $minX) { $minX = [double]$point.x }
        if ([double]$point.x -gt $maxX) { $maxX = [double]$point.x }
        if ([double]$point.y -lt $minY) { $minY = [double]$point.y }
        if ([double]$point.y -gt $maxY) { $maxY = [double]$point.y }
    }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
}

function Project-Point($Point, [string]$Projection) {
    switch ($Projection) {
        "xz" { return [pscustomobject]@{ x = [double]$Point.x; y = [double]$Point.z; z = [double]$Point.y } }
        "yz" { return [pscustomobject]@{ x = [double]$Point.y; y = [double]$Point.z; z = [double]$Point.x } }
        default { return [pscustomobject]@{ x = [double]$Point.x; y = [double]$Point.y; z = [double]$Point.z } }
    }
}

function Find-BestPartLayout([byte[]]$Bytes, [int]$PartOffset, [int]$PartEnd, $Permutations) {
    $size = $PartEnd - $PartOffset
    if ($size -lt 64 -or ($PartOffset + 16) -gt $Bytes.Length) { return $null }
    $lod = @([int]$Bytes[$PartOffset + 4], [int]$Bytes[$PartOffset + 5], [int]$Bytes[$PartOffset + 6], [int]$Bytes[$PartOffset + 7])
    $mdl = @([int]$Bytes[$PartOffset + 8], [int]$Bytes[$PartOffset + 9], [int]$Bytes[$PartOffset + 10], [int]$Bytes[$PartOffset + 11])
    $best = $null
    foreach ($headerSize in @(16, 20, 24, 28, 32)) {
        foreach ($lp in $Permutations) {
            foreach ($mp in $Permutations) {
                $lodV = $lod[$lp[0]]
                $lodC = $lod[$lp[1]]
                $lodP = $lod[$lp[2]]
                $mdlV = $mdl[$mp[0]]
                $mdlC = $mdl[$mp[1]]
                $mdlP = $mdl[$mp[2]]
                if (($lodV + $mdlV) -le 0 -or ($lodV + $mdlV) -gt 420) { continue }
                if (($lodP + $mdlP) -gt 360) { continue }
                $calc = $headerSize + ($lodV * 4) + ($lodC * 4) + ($lodP * 8) + ($mdlV * 4) + ($mdlC * 8) + ($mdlP * 16)
                $diff = $size - $calc
                if ($diff -lt 0 -or $diff -gt 160) { continue }
                $score = (160 - $diff) + (($lodV + $mdlV) * 2) + (($lodP + $mdlP) * 3)
                if ($null -eq $best -or $score -gt $best.score) {
                    $best = [pscustomobject]@{
                        score = $score
                        diff = $diff
                        headerSize = $headerSize
                        lodV = $lodV
                        lodC = $lodC
                        lodP = $lodP
                        mdlV = $mdlV
                        mdlC = $mdlC
                        mdlP = $mdlP
                    }
                }
            }
        }
    }
    return $best
}

function New-GroundCandidate($Parts, [string]$Endian, [bool]$SignedLocal, [string]$GlobalMode, [int]$GlobalScale, [string]$Projection, [bool]$ModelOnly, [int]$PolyIndexOffset, [int]$PolyVertexCount) {
    $points3d = New-Object System.Collections.Generic.List[object]
    $edges3d = New-Object System.Collections.Generic.List[object]
    $partCount = 0
    foreach ($part in $Parts) {
        $layout = $part.layout
        if ($null -eq $layout) { continue }
        $global = Get-PartGlobal2 $part.bytes $part.offset $part.prefixOffset $GlobalMode $GlobalScale
        $partPointStart = $points3d.Count
        $lodStart = $part.offset + $layout.headerSize
        $mdlStart = $lodStart + ($layout.lodV * 4) + ($layout.lodC * 4) + ($layout.lodP * 8)
        $vertexRuns = @()
        if (-not $ModelOnly -and $layout.lodV -gt 0) {
            $vertexRuns += [pscustomobject]@{ start = $lodStart; count = $layout.lodV; baseIndex = $partPointStart }
        }
        $modelBaseIndex = $partPointStart
        if (-not $ModelOnly) { $modelBaseIndex += $layout.lodV }
        if ($layout.mdlV -gt 0) {
            $vertexRuns += [pscustomobject]@{ start = $mdlStart; count = $layout.mdlV; baseIndex = $modelBaseIndex }
        }
        foreach ($run in $vertexRuns) {
            for ($i = 0; $i -lt $run.count; $i++) {
                $decoded = Convert-PackedVertex $part.bytes ($run.start + ($i * 4)) $Endian $SignedLocal
                if ($null -eq $decoded) { continue }
                $bboxPoint = Convert-PrefixBBoxVertex $decoded $part.bytes $part.prefixOffset $GlobalMode
                if ($null -ne $bboxPoint) {
                    [void]$points3d.Add([pscustomobject]@{
                        x = [double]$bboxPoint.x
                        y = [double]$bboxPoint.y
                        z = [double]$bboxPoint.z
                        part = $part.index
                        sourceOffset = ("0x{0:X}" -f ($run.start + ($i * 4)))
                    })
                    continue
                }
                [void]$points3d.Add([pscustomobject]@{
                    x = ([double]$decoded.x + [double]$global.x)
                    y = ([double]$decoded.y + [double]$global.y)
                    z = ([double]$decoded.z + [double]$global.z)
                    part = $part.index
                    sourceOffset = ("0x{0:X}" -f ($run.start + ($i * 4)))
                })
            }
        }

        $polyStart = $mdlStart + ($layout.mdlV * 4) + ($layout.mdlC * 8)
        for ($i = 0; $i -lt $layout.mdlP; $i++) {
            if ($edges3d.Count -ge ($MaxEdgesPerCandidate * 2)) { break }
            $offset = $polyStart + ($i * 16) + $PolyIndexOffset
            if (($offset + $PolyVertexCount) -gt $part.bytes.Length) { break }
            $indices = @()
            for ($indexByte = 0; $indexByte -lt $PolyVertexCount; $indexByte++) {
                $indices += [int]$part.bytes[$offset + $indexByte]
            }
            $valid = @($indices | Where-Object { $_ -ge 0 -and $_ -lt $layout.mdlV })
            if ($valid.Count -lt 3) { continue }
            $base = $modelBaseIndex
            for ($edgeIndex = 0; $edgeIndex -lt $valid.Count; $edgeIndex++) {
                $a = $base + $valid[$edgeIndex]
                $b = $base + $valid[(($edgeIndex + 1) % $valid.Count)]
                if ($a -lt $points3d.Count -and $b -lt $points3d.Count -and $a -ne $b) {
                    if ($MaxModelEdgeLength -gt 0) {
                        $pa = $points3d[$a]
                        $pb = $points3d[$b]
                        $dx = [double]$pa.x - [double]$pb.x
                        $dz = [double]$pa.z - [double]$pb.z
                        $edgeLength = [Math]::Sqrt(($dx * $dx) + ($dz * $dz))
                        if ($edgeLength -gt $MaxModelEdgeLength) { continue }
                    }
                    [void]$edges3d.Add([pscustomobject]@{ a = $a; b = $b })
                    if ($edges3d.Count -ge ($MaxEdgesPerCandidate * 2)) { break }
                }
            }
        }
        $partCount++
    }

    $projected = @($points3d | ForEach-Object { Project-Point $_ $Projection })
    $edges = New-Object System.Collections.Generic.List[object]
    foreach ($edge in $edges3d) {
        if ($edge.a -ge $projected.Count -or $edge.b -ge $projected.Count) { continue }
        $p1 = $projected[$edge.a]
        $p2 = $projected[$edge.b]
        [void]$edges.Add([pscustomobject]@{ x1 = $p1.x; y1 = $p1.y; x2 = $p2.x; y2 = $p2.y })
    }
    $bounds = Get-Bounds $projected
    $area = if ($null -ne $bounds) { [Math]::Max(1.0, ([double]$bounds.maxX - [double]$bounds.minX) * ([double]$bounds.maxY - [double]$bounds.minY)) } else { 1.0 }
    $areaScore = [int](2000 - [Math]::Min(2000, [Math]::Abs([Math]::Log10($area) - 9.2) * 850))
    $score = ($projected.Count * 2) + ($edges.Count * 3) + $areaScore
    $pointTotal = $projected.Count
    $edgeTotal = $edges.Count
    if ($projected.Count -gt $MaxPointsPerCandidate) {
        $step = [Math]::Max(1, [int][Math]::Ceiling($projected.Count / [double]$MaxPointsPerCandidate))
        $sampled = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $projected.Count; $i += $step) {
            [void]$sampled.Add($projected[$i])
        }
        $projected = @($sampled.ToArray())
    }
    if ($edges.Count -gt $MaxEdgesPerCandidate) {
        $step = [Math]::Max(1, [int][Math]::Ceiling($edges.Count / [double]$MaxEdgesPerCandidate))
        $sampledEdges = New-Object System.Collections.Generic.List[object]
        for ($i = 0; $i -lt $edges.Count; $i += $step) {
            [void]$sampledEdges.Add($edges[$i])
        }
        $edges = $sampledEdges
    }
    $vertexSetName = if ($ModelOnly) { "mdl" } else { "lod+mdl" }
    return [ordered]@{
        runtimeAddress = ("wad-ground-model subfile {0} shift={1} {2} {3} signed={4} global={5}x{6} {7} poly+{8}/{9}" -f $ModelSubfileIndex, $PartHeaderShift, $vertexSetName, $Endian, $SignedLocal, $GlobalMode, $GlobalScale, $Projection, $PolyIndexOffset, $PolyVertexCount)
        encoding = "spyro1-documented-ground-model-probe"
        stride = 4
        projection = $Projection
        fitScore = $score
        mobysInsideProjection = 0
        edgeSource = ("model-poly-byte-indices+{0}/{1}" -f $PolyIndexOffset, $PolyVertexCount)
        validVertices = $pointTotal
        sampledVertices = $projected.Count
        sampledEdges = $edges.Count
        totalEdges = $edgeTotal
        parsedParts = $partCount
        projectedBounds = $bounds
        projectedPoints = @($projected)
        edges = @($edges.ToArray())
    }
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$stream = [System.IO.File]::OpenRead($resolvedImage)
try {
    $wadHeader = Read-WadBytes $stream $WadLba 0 4096
    $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
    $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
    if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }

    $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
    $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
    $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex })[0]
    if ($null -eq $modelSubfile) { throw "Could not find subfile $ModelSubfileIndex in WAD entry $AssetWadIndex." }

    $modelBytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)
    $textureListSize = [int](Get-UInt32LE $modelBytes 0)
    if ($textureListSize -le 0 -or ($textureListSize + 4) -gt $modelBytes.Length) {
        throw "Subfile $ModelSubfileIndex does not have a valid texture-list jump."
    }
    $groundJump = [int](Get-UInt32LE $modelBytes $textureListSize)
    if ($groundJump -le 4 -or ($textureListSize + $groundJump) -gt $modelBytes.Length) {
        throw "Subfile $ModelSubfileIndex does not have a valid ground-model jump."
    }

    $groundLength = $groundJump - 4
    $ground = New-Object byte[] $groundLength
    [Array]::Copy($modelBytes, ($textureListSize + 4), $ground, 0, $groundLength)
    $partCount = [int](Get-UInt32LE $ground 0)
    if ($partCount -le 0 -or $partCount -gt 1024) { throw "Ground model part count $partCount is not plausible." }

    $pointers = @()
    for ($i = 0; $i -lt $partCount; $i++) {
        $ptr = [int](Get-UInt32LE $ground (4 + ($i * 4)))
        if ($ptr -gt 0 -and $ptr -lt $ground.Length) { $pointers += $ptr }
    }
    $pointers = @($pointers | Sort-Object -Unique)
    $perms = Get-BytePermutations
    $parts = @()
    for ($i = 0; $i -lt $pointers.Count; $i++) {
        $start = [int]$pointers[$i] + $PartHeaderShift
        $end = if ($i -lt ($pointers.Count - 1)) { [int]$pointers[$i + 1] + $PartHeaderShift } else { $ground.Length }
        if ($start -lt 0 -or $end -le $start -or $start -ge $ground.Length) { continue }
        if ($end -gt $ground.Length) { $end = $ground.Length }
        $layout = Find-BestPartLayout $ground $start $end $perms
        if ($null -ne $layout) {
            $parts += [pscustomobject]@{ index = $i; offset = $start; prefixOffset = ([int]$pointers[$i]); end = $end; bytes = $ground; layout = $layout }
        }
    }
    if ($parts.Count -eq 0) { throw "No ground model parts could be decoded." }

    $candidates = New-Object System.Collections.Generic.List[object]
    $signedOptions = if ($Candidate4Only -or $BBoxProbe) { @($false) } else { @($true, $false) }
    $globalOptions = if ($SignedGlobalProbe) {
        @(
            @{ mode = "doc-signed-gxgygz"; scale = 1024 },
            @{ mode = "doc-signed-gxgygz"; scale = 512 },
            @{ mode = "doc-signed-gxgygz"; scale = 256 },
            @{ mode = "swapped-signed-gxgygz"; scale = 1024 },
            @{ mode = "swapped-signed-gxgygz"; scale = 512 },
            @{ mode = "swapped-signed-gxgygz"; scale = 256 }
        )
    }
    elseif ($BBoxProbe) {
        @(
            @{ mode = "prefix-bbox-0-1-2-3-4-5-local-xyz"; scale = 1 },
            @{ mode = "prefix-bbox-0-1-2-3-4-5-local-xzy"; scale = 1 },
            @{ mode = "prefix-bbox-4-5-2-3-0-1-local-xyz"; scale = 1 },
            @{ mode = "prefix-bbox-4-5-2-3-0-1-local-xzy"; scale = 1 },
            @{ mode = "prefix-bbox-0-1-4-5-2-3-local-xyz"; scale = 1 },
            @{ mode = "prefix-bbox-0-1-4-5-2-3-local-xzy"; scale = 1 },
            @{ mode = "prefix-bbox-1-0-3-2-5-4-local-xyz"; scale = 1 },
            @{ mode = "prefix-bbox-1-0-3-2-5-4-local-xzy"; scale = 1 }
        )
    }
    elseif ($PrefixProbe) {
        @(
            @{ mode = "prefix-u16-0-1-4"; scale = 1 },
            @{ mode = "prefix-u16-0-5-1"; scale = 1 },
            @{ mode = "prefix-u16-4-1-5"; scale = 1 },
            @{ mode = "prefix-u16-4-5-1"; scale = 1 },
            @{ mode = "prefix-u16-0-4-1"; scale = 1 },
            @{ mode = "prefix-u16-2-0-4"; scale = 1 }
        )
    }
    elseif ($Candidate4Only) {
        @(@{ mode = "doc-gxgygz"; scale = 256 })
    }
    else {
        @(
            @{ mode = "doc-gxgygz"; scale = 1024 },
            @{ mode = "doc-gxgygz"; scale = 512 },
            @{ mode = "doc-gxgygz"; scale = 256 },
            @{ mode = "doc-signed-gxgygz"; scale = 1024 },
            @{ mode = "doc-signed-gxgygz"; scale = 512 },
            @{ mode = "doc-signed-gxgygz"; scale = 256 },
            @{ mode = "swapped-gxgygz"; scale = 1024 },
            @{ mode = "swapped-gxgygz"; scale = 512 },
            @{ mode = "swapped-gxgygz"; scale = 256 },
            @{ mode = "swapped-signed-gxgygz"; scale = 1024 },
            @{ mode = "swapped-signed-gxgygz"; scale = 512 },
            @{ mode = "swapped-signed-gxgygz"; scale = 256 }
        )
    }
    $projectionOptions = if ($Candidate4Only -and -not $PrefixProbe -and -not $BBoxProbe) { @("xz") } else { @("xy", "xz", "yz") }
    $modelOnlyOptions = if ($IncludeLod) { @($true, $false) } else { @($true) }
    foreach ($modelOnly in $modelOnlyOptions) {
        foreach ($endian in @("be-bits")) {
            foreach ($signed in $signedOptions) {
                foreach ($globalConfig in $globalOptions) {
                    foreach ($projection in $projectionOptions) {
                        foreach ($polyIndexOffset in $ModelPolyIndexOffsets) {
                            foreach ($polyVertexCount in $ModelPolyVertexCounts) {
                                if ($polyIndexOffset -lt 0 -or $polyVertexCount -lt 3 -or ($polyIndexOffset + $polyVertexCount) -gt 16) { continue }
                                $candidate = New-GroundCandidate $parts $endian $signed $globalConfig.mode $globalConfig.scale $projection $modelOnly $polyIndexOffset $polyVertexCount
                                if ([int]$candidate["validVertices"] -ge 40) {
                                    [void]$candidates.Add($candidate)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    $ranked = @($candidates | Sort-Object -Property @{ Expression = { [double]$_["fitScore"] }; Descending = $true } | Select-Object -First $MaxCandidates)

    $overlay = [ordered]@{
        sourceImage = $resolvedImage
        generatedAt = (Get-Date).ToString("s")
        note = "Experimental Stone Hill WAD ground-model overlay from the documented Spyro 1 level model block. This is the first pass at the actual structured terrain model; candidates vary packed-vertex endian, signedness, global-sector scaling, and projection."
        sourceReference = "https://www.spyroforum.com/viewtopic.php?id=13872"
        assetWadIndex = $AssetWadIndex
        modelSubfileIndex = $ModelSubfileIndex
        modelSubfileOffset = $modelSubfile.offset
        modelSubfileSize = $modelSubfile.size
        textureListSize = $textureListSize
        groundModelOffset = ($textureListSize + 4)
        groundModelSize = $groundLength
        documentedPartCount = $partCount
        partHeaderShift = $PartHeaderShift
        decodedPartCount = $parts.Count
        candidates = $ranked
    }
    $resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
    $overlay | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
    Write-Host "Wrote WAD ground model overlay to $resolvedOut"
    Write-Host "Ground parts: $partCount documented, $($parts.Count) decoded. Candidates: $($ranked.Count)"
}
finally {
    $stream.Close()
}
