param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RuntimeOverlayPath = ".\stonehill-runtime-scene-editor-overlay.json",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [string]$OutJsonPath = ".\stonehill-terrain-source-map-probe.json",
    [string]$OutMarkdownPath = ".\stonehill-terrain-source-map-probe.md",
    [int]$MaxRuntimePoints = 14000,
    [int]$MaxSourcePoints = 14000,
    [int]$NearestCellSize = 64,
    [int]$TopCandidates = 24,
    [int]$ScoreSampleLimit = 900,
    [switch]$Candidate4Only,
    [string]$OutLinksPath = ".\stonehill-terrain-source-links.json",
    [string]$OutLinksMarkdownPath = ".\stonehill-terrain-source-links.md",
    [int]$MaxLinks = 10000,
    [double]$LinkDistanceThreshold = 128.0
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int64]$Lba) {
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
        $sector = [int64]$WadLba + [int64][Math]::Floor($absolute / 2048)
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
    return @($entries)
}

function Convert-SignedByte([int]$Value) {
    if ($Value -ge 128) { return ($Value - 256) }
    return $Value
}

function Convert-PackedVertex([byte[]]$Bytes, [int]$Offset, [bool]$SignedLocal) {
    if (($Offset + 4) -gt $Bytes.Length) { return $null }
    $raw = Get-UInt32LE $Bytes $Offset
    $raw = (($raw -band 0x000000FF) -shl 24) -bor (($raw -band 0x0000FF00) -shl 8) -bor (($raw -band 0x00FF0000) -shr 8) -bor (($raw -band 0xFF000000) -shr 24)
    $x = [int]($raw -band 0x7FF)
    $y = [int](($raw -shr 11) -band 0x7FF)
    $z = [int](($raw -shr 22) -band 0x3FF)
    if ($SignedLocal) {
        if ($x -ge 1024) { $x -= 2048 }
        if ($y -ge 1024) { $y -= 2048 }
        if ($z -ge 512) { $z -= 1024 }
    }
    return [pscustomobject]@{ x = [double]$x; y = [double]$y; z = [double]$z; raw = ("0x{0:X8}" -f $raw) }
}

function Get-PartGlobal([byte[]]$Bytes, [int]$PartOffset, [string]$Mode, [int]$Scale) {
    if (($PartOffset + 4) -gt $Bytes.Length -or $Scale -eq 0) {
        return [pscustomobject]@{ x = 0.0; y = 0.0; z = 0.0 }
    }
    $b0 = [int]$Bytes[$PartOffset]
    $b1 = [int]$Bytes[$PartOffset + 1]
    $b3 = [int]$Bytes[$PartOffset + 3]
    switch ($Mode) {
        "doc-gxgygz" { return [pscustomobject]@{ x = [double]($b1 * $Scale); y = [double]($b0 * $Scale); z = [double]($b3 * $Scale) } }
        "swapped-gxgygz" { return [pscustomobject]@{ x = [double]($b0 * $Scale); y = [double]($b1 * $Scale); z = [double]($b3 * $Scale) } }
        "doc-signed-gxgygz" { return [pscustomobject]@{ x = [double]((Convert-SignedByte $b1) * $Scale); y = [double]((Convert-SignedByte $b0) * $Scale); z = [double]((Convert-SignedByte $b3) * $Scale) } }
        "swapped-signed-gxgygz" { return [pscustomobject]@{ x = [double]((Convert-SignedByte $b0) * $Scale); y = [double]((Convert-SignedByte $b1) * $Scale); z = [double]((Convert-SignedByte $b3) * $Scale) } }
        default { return [pscustomobject]@{ x = 0.0; y = 0.0; z = 0.0 } }
    }
}

function Get-BytePermutations {
    return @(
        @(0, 1, 2, 3), @(0, 1, 3, 2), @(0, 2, 1, 3), @(0, 2, 3, 1), @(0, 3, 1, 2), @(0, 3, 2, 1),
        @(1, 0, 2, 3), @(1, 0, 3, 2), @(1, 2, 0, 3), @(1, 2, 3, 0), @(1, 3, 0, 2), @(1, 3, 2, 0),
        @(2, 0, 1, 3), @(2, 0, 3, 1), @(2, 1, 0, 3), @(2, 1, 3, 0), @(2, 3, 0, 1), @(2, 3, 1, 0),
        @(3, 0, 1, 2), @(3, 0, 2, 1), @(3, 1, 0, 2), @(3, 1, 2, 0), @(3, 2, 0, 1), @(3, 2, 1, 0)
    )
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
                        score = $score; diff = $diff; headerSize = $headerSize
                        lodV = $lodV; lodC = $lodC; lodP = $lodP
                        mdlV = $mdlV; mdlC = $mdlC; mdlP = $mdlP
                    }
                }
            }
        }
    }
    return $best
}

function Get-ModelSubfileBytes([string]$ResolvedImagePath, [int]$WadLba, [int]$AssetWadIndex, [int]$ModelSubfileIndex) {
    $stream = [System.IO.File]::OpenRead($ResolvedImagePath)
    try {
        $wadHeader = Read-WadBytes $stream $WadLba 0 4096
        $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
        $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex } | Select-Object -First 1)
        if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }
        $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
        $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
        $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex } | Select-Object -First 1)
        if ($null -eq $modelSubfile) { throw "Could not find model subfile $ModelSubfileIndex." }
        $modelBytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)
        return [pscustomobject]@{ assetEntry = $assetEntry; modelSubfile = $modelSubfile; modelBytes = $modelBytes }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-RuntimePoints($Overlay, [int]$MaxPoints) {
    $candidate = @($Overlay.candidates | Select-Object -First 1)
    if ($candidate.Count -eq 0) { throw "Runtime overlay has no candidates." }
    $seen = @{}
    $points = New-Object System.Collections.ArrayList
    foreach ($polygon in @($candidate[0].polygons)) {
        foreach ($point in @($polygon.points)) {
            $key = "{0},{1},{2}" -f ([int][Math]::Round([double]$point.x)), ([int][Math]::Round([double]$point.y)), ([int][Math]::Round([double]$point.z))
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            [void]$points.Add([pscustomobject]@{ x = [double]$point.x; y = [double]$point.y; z = [double]$point.z })
            if ($points.Count -ge $MaxPoints) { return @($points.ToArray()) }
        }
    }
    return @($points.ToArray())
}

function Get-Bounds($Points) {
    $minX = [double]::PositiveInfinity; $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity; $maxY = [double]::NegativeInfinity
    $minZ = [double]::PositiveInfinity; $maxZ = [double]::NegativeInfinity
    foreach ($p in @($Points)) {
        if ([double]$p.x -lt $minX) { $minX = [double]$p.x }
        if ([double]$p.x -gt $maxX) { $maxX = [double]$p.x }
        if ([double]$p.y -lt $minY) { $minY = [double]$p.y }
        if ([double]$p.y -gt $maxY) { $maxY = [double]$p.y }
        if ([double]$p.z -lt $minZ) { $minZ = [double]$p.z }
        if ([double]$p.z -gt $maxZ) { $maxZ = [double]$p.z }
    }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
}

function Decode-Parts([byte[]]$ModelBytes) {
    $textureListSize = [int](Get-UInt32LE $ModelBytes 0)
    $groundJump = [int](Get-UInt32LE $ModelBytes $textureListSize)
    $groundOffset = $textureListSize + 4
    $groundLength = $groundJump - 4
    $ground = New-Object byte[] $groundLength
    [Array]::Copy($ModelBytes, $groundOffset, $ground, 0, $groundLength)
    $partCount = [int](Get-UInt32LE $ground 0)
    $pointers = @()
    for ($i = 0; $i -lt $partCount; $i++) {
        $ptr = [int](Get-UInt32LE $ground (4 + ($i * 4)))
        if ($ptr -gt 0 -and $ptr -lt $ground.Length) { $pointers += $ptr }
    }
    $pointers = @($pointers | Sort-Object -Unique)
    $perms = Get-BytePermutations
    $parts = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $pointers.Count; $i++) {
        $start = [int]$pointers[$i]
        $end = if ($i -lt ($pointers.Count - 1)) { [int]$pointers[$i + 1] } else { $ground.Length }
        if ($start -lt 0 -or $end -le $start) { continue }
        $layout = Find-BestPartLayout $ground $start $end $perms
        if ($null -ne $layout) {
            [void]$parts.Add([pscustomobject]@{ index = $i; offset = $start; end = $end; bytes = $ground; layout = $layout; groundOffset = $groundOffset })
        }
    }
    return [pscustomobject]@{ ground = $ground; groundOffset = $groundOffset; groundLength = $groundLength; partCount = $partCount; parts = @($parts.ToArray()) }
}

function Decode-SourcePoints($Parts, [bool]$SignedLocal, [string]$GlobalMode, [int]$GlobalScale, [bool]$IncludeLod) {
    $points = New-Object System.Collections.ArrayList
    foreach ($part in @($Parts)) {
        $layout = $part.layout
        $global = Get-PartGlobal $part.bytes $part.offset $GlobalMode $GlobalScale
        $lodStart = $part.offset + $layout.headerSize
        $mdlStart = $lodStart + ($layout.lodV * 4) + ($layout.lodC * 4) + ($layout.lodP * 8)
        $runs = @()
        if ($IncludeLod -and $layout.lodV -gt 0) { $runs += [pscustomobject]@{ kind = "lod"; start = $lodStart; count = $layout.lodV } }
        if ($layout.mdlV -gt 0) { $runs += [pscustomobject]@{ kind = "mdl"; start = $mdlStart; count = $layout.mdlV } }
        foreach ($run in $runs) {
            for ($i = 0; $i -lt $run.count; $i++) {
                $offset = $run.start + ($i * 4)
                $decoded = Convert-PackedVertex $part.bytes $offset $SignedLocal
                if ($null -eq $decoded) { continue }
                [void]$points.Add([pscustomobject]@{
                    x = ([double]$decoded.x + [double]$global.x)
                    y = ([double]$decoded.y + [double]$global.y)
                    z = ([double]$decoded.z + [double]$global.z)
                    localX = [double]$decoded.x
                    localY = [double]$decoded.y
                    localZ = [double]$decoded.z
                    part = [int]$part.index
                    kind = [string]$run.kind
                    vertexIndex = $i
                    raw = [string]$decoded.raw
                    sourceOffset = ("0x{0:X}" -f $offset)
                    modelSubfileOffset = ("0x{0:X}" -f ([int]$part.groundOffset + $offset))
                })
                if ($points.Count -ge $MaxSourcePoints) { return @($points.ToArray()) }
            }
        }
    }
    return @($points.ToArray())
}

function Select-Axis($Point, [string]$Axis) {
    switch ($Axis) {
        "x" { return [double]$Point.x }
        "y" { return [double]$Point.y }
        default { return [double]$Point.z }
    }
}

function New-Grid($Points, [int]$CellSize) {
    $grid = @{}
    foreach ($p in @($Points)) {
        $cx = [int][Math]::Floor([double]$p.x / $CellSize)
        $cy = [int][Math]::Floor([double]$p.y / $CellSize)
        $key = "$cx,$cy"
        if (-not $grid.ContainsKey($key)) { $grid[$key] = New-Object System.Collections.ArrayList }
        [void]$grid[$key].Add($p)
    }
    return $grid
}

function Find-Nearest2D($Grid, [double]$X, [double]$Y, [int]$CellSize) {
    $cx = [int][Math]::Floor($X / $CellSize)
    $cy = [int][Math]::Floor($Y / $CellSize)
    $best = [double]::PositiveInfinity
    for ($dy = -1; $dy -le 1; $dy++) {
        for ($dx = -1; $dx -le 1; $dx++) {
            $key = "$($cx + $dx),$($cy + $dy)"
            if (-not $Grid.ContainsKey($key)) { continue }
            foreach ($p in @($Grid[$key])) {
                $ddx = [double]$p.x - $X
                $ddy = [double]$p.y - $Y
                $dist = [Math]::Sqrt(($ddx * $ddx) + ($ddy * $ddy))
                if ($dist -lt $best) { $best = $dist }
            }
        }
    }
    return $best
}

function Find-NearestPoint2D($Grid, [double]$X, [double]$Y, [int]$CellSize) {
    $cx = [int][Math]::Floor($X / $CellSize)
    $cy = [int][Math]::Floor($Y / $CellSize)
    $best = $null
    $bestDistance = [double]::PositiveInfinity
    for ($radius = 1; $radius -le 3; $radius++) {
        for ($dy = -$radius; $dy -le $radius; $dy++) {
            for ($dx = -$radius; $dx -le $radius; $dx++) {
                $key = "$($cx + $dx),$($cy + $dy)"
                if (-not $Grid.ContainsKey($key)) { continue }
                foreach ($p in @($Grid[$key])) {
                    $ddx = [double]$p.x - $X
                    $ddy = [double]$p.y - $Y
                    $dist = [Math]::Sqrt(($ddx * $ddx) + ($ddy * $ddy))
                    if ($dist -lt $bestDistance) {
                        $bestDistance = $dist
                        $best = $p
                    }
                }
            }
        }
    }
    if ($null -eq $best) { return $null }
    return [pscustomobject]@{ point = $best; distance = $bestDistance }
}

function Get-RuntimeVertexHandles($Overlay) {
    $candidate = @($Overlay.candidates | Select-Object -First 1)
    if ($candidate.Count -eq 0) { throw "Runtime overlay has no candidates." }

    $byKey = @{}
    foreach ($polygon in @($candidate[0].polygons)) {
        $indexes = @($polygon.vertexIndexes)
        $points = @($polygon.points)
        if ($indexes.Count -eq 0 -or $points.Count -eq 0) { continue }

        $seenInFace = @{}
        $pointOrdinal = 0
        foreach ($rawIndex in $indexes) {
            $vertexIndex = [int]$rawIndex
            if ($seenInFace.ContainsKey([string]$vertexIndex)) { continue }
            $seenInFace[[string]$vertexIndex] = $true
            if ($pointOrdinal -ge $points.Count) { break }

            $point = $points[$pointOrdinal]
            $pointOrdinal++
            $key = "{0}:{1}:{2}" -f [string]$polygon.detail, [int]$polygon.sectorIndex, $vertexIndex
            if (-not $byKey.ContainsKey($key)) {
                $byKey[$key] = [pscustomobject]@{
                    key = $key
                    x = [double]$point.x
                    y = [double]$point.y
                    z = [double]$point.z
                    detail = [string]$polygon.detail
                    sectorIndex = [int]$polygon.sectorIndex
                    sectorOffset = [string]$polygon.sectorOffset
                    vertexIndex = $vertexIndex
                    faceCount = 0
                    sampleFaceIndex = [int]$polygon.faceIndex
                    sampleFaceOffset = [string]$polygon.faceOffset
                }
            }
            $byKey[$key].faceCount = [int]$byKey[$key].faceCount + 1
        }
    }
    return @($byKey.Values)
}

function New-SourceRuntimeLinks($SourcePoints, $Candidate, $RuntimeBounds, $RuntimeGrid, [double]$DistanceThreshold, [int]$MaxRows) {
    $xb = Get-AxisBounds $SourcePoints ([string]$Candidate.axisX)
    $yb = Get-AxisBounds $SourcePoints ([string]$Candidate.axisY)
    $rows = New-Object System.Collections.ArrayList
    $within32 = 0
    $within64 = 0
    $within128 = 0
    $tested = 0

    foreach ($sp in @($SourcePoints)) {
        $tested++
        $tx = Convert-Range (Select-Axis $sp ([string]$Candidate.axisX)) $xb.min $xb.max $RuntimeBounds.minX $RuntimeBounds.maxX ([bool]$Candidate.flipX)
        $ty = Convert-Range (Select-Axis $sp ([string]$Candidate.axisY)) $yb.min $yb.max $RuntimeBounds.minY $RuntimeBounds.maxY ([bool]$Candidate.flipY)
        $nearest = Find-NearestPoint2D $RuntimeGrid $tx $ty $NearestCellSize
        if ($null -eq $nearest) { continue }
        $distance = [double]$nearest.distance
        if ($distance -le 32.0) { $within32++ }
        if ($distance -le 64.0) { $within64++ }
        if ($distance -le 128.0) { $within128++ }
        if ($distance -gt $DistanceThreshold) { continue }
        $rp = $nearest.point
        [void]$rows.Add([pscustomobject][ordered]@{
            distance = [Math]::Round($distance, 2)
            projectedX = [Math]::Round($tx, 2)
            projectedY = [Math]::Round($ty, 2)
            source = [ordered]@{
                part = [int]$sp.part
                kind = [string]$sp.kind
                vertexIndex = [int]$sp.vertexIndex
                sourceOffset = [string]$sp.sourceOffset
                modelSubfileOffset = [string]$sp.modelSubfileOffset
                raw = [string]$sp.raw
                localX = [Math]::Round([double]$sp.localX, 2)
                localY = [Math]::Round([double]$sp.localY, 2)
                localZ = [Math]::Round([double]$sp.localZ, 2)
                x = [Math]::Round([double]$sp.x, 2)
                y = [Math]::Round([double]$sp.y, 2)
                z = [Math]::Round([double]$sp.z, 2)
            }
            runtime = [ordered]@{
                key = [string]$rp.key
                detail = [string]$rp.detail
                sectorIndex = [int]$rp.sectorIndex
                sectorOffset = [string]$rp.sectorOffset
                vertexIndex = [int]$rp.vertexIndex
                sampleFaceIndex = [int]$rp.sampleFaceIndex
                sampleFaceOffset = [string]$rp.sampleFaceOffset
                faceCount = [int]$rp.faceCount
                x = [Math]::Round([double]$rp.x, 2)
                y = [Math]::Round([double]$rp.y, 2)
                z = [Math]::Round([double]$rp.z, 2)
            }
        })
        if ($rows.Count -ge $MaxRows) { break }
    }

    return [ordered]@{
        testedSourcePoints = $tested
        linkCount = $rows.Count
        distanceThreshold = $DistanceThreshold
        within32 = $within32
        within64 = $within64
        within128 = $within128
        links = @($rows.ToArray() | Sort-Object -Property @{ Expression = { [double]$_.distance }; Descending = $false })
    }
}

function Get-MapValue($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name)) { return $Object[$Name] }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -ne $prop) { return $prop.Value }
    return $null
}

function New-HeightFits($Links, [double]$MaxDistance) {
    $rows = @($Links | Where-Object { [double](Get-MapValue $_ "distance") -le $MaxDistance })
    $fits = New-Object System.Collections.ArrayList
    foreach ($axis in @("localX", "localY", "localZ", "x", "y", "z")) {
        $xs = New-Object System.Collections.ArrayList
        $ys = New-Object System.Collections.ArrayList
        foreach ($row in $rows) {
            $source = Get-MapValue $row "source"
            $runtime = Get-MapValue $row "runtime"
            $xValue = Get-MapValue $source $axis
            $yValue = Get-MapValue $runtime "z"
            if ($null -eq $xValue -or $null -eq $yValue) { continue }
            [void]$xs.Add([double]$xValue)
            [void]$ys.Add([double]$yValue)
        }
        if ($xs.Count -lt 2) { continue }
        $sumX = 0.0; $sumY = 0.0
        for ($i = 0; $i -lt $xs.Count; $i++) {
            $sumX += [double]$xs[$i]
            $sumY += [double]$ys[$i]
        }
        $meanX = $sumX / [double]$xs.Count
        $meanY = $sumY / [double]$ys.Count
        $num = 0.0; $den = 0.0
        for ($i = 0; $i -lt $xs.Count; $i++) {
            $dx = [double]$xs[$i] - $meanX
            $dy = [double]$ys[$i] - $meanY
            $num += $dx * $dy
            $den += $dx * $dx
        }
        $slope = if ([Math]::Abs($den) -gt 0.000001) { $num / $den } else { 0.0 }
        $intercept = $meanY - ($slope * $meanX)
        $errors = New-Object System.Collections.ArrayList
        $sumSquared = 0.0
        for ($i = 0; $i -lt $xs.Count; $i++) {
            $predicted = ($slope * [double]$xs[$i]) + $intercept
            $error = $predicted - [double]$ys[$i]
            $sumSquared += $error * $error
            [void]$errors.Add([Math]::Abs($error))
        }
        $sortedErrors = @($errors.ToArray() | Sort-Object)
        [void]$fits.Add([ordered]@{
            sourceAxis = $axis
            sampleCount = $xs.Count
            maxDistance = $MaxDistance
            slope = [Math]::Round($slope, 8)
            intercept = [Math]::Round($intercept, 4)
            medianAbsError = [Math]::Round([double]$sortedErrors[[int][Math]::Floor($sortedErrors.Count / 2)], 2)
            p90AbsError = [Math]::Round([double]$sortedErrors[[int][Math]::Floor($sortedErrors.Count * 0.9)], 2)
            rmse = [Math]::Round([Math]::Sqrt($sumSquared / [double]$xs.Count), 2)
        })
    }
    return @($fits.ToArray() | Sort-Object -Property @{ Expression = { [double]$_.rmse }; Descending = $false })
}

function Get-AxisBounds($Points, [string]$Axis) {
    $min = [double]::PositiveInfinity
    $max = [double]::NegativeInfinity
    foreach ($p in @($Points)) {
        $v = Select-Axis $p $Axis
        if ($v -lt $min) { $min = $v }
        if ($v -gt $max) { $max = $v }
    }
    return [pscustomobject]@{ min = $min; max = $max }
}

function Convert-Range([double]$Value, [double]$SourceMin, [double]$SourceMax, [double]$TargetMin, [double]$TargetMax, [bool]$Flip) {
    $den = $SourceMax - $SourceMin
    if ([Math]::Abs($den) -lt 0.000001) { return ($TargetMin + $TargetMax) / 2.0 }
    $t = ($Value - $SourceMin) / $den
    if ($Flip) { $t = 1.0 - $t }
    return $TargetMin + ($t * ($TargetMax - $TargetMin))
}

function Score-Candidate($SourcePoints, $RuntimeBounds, $RuntimeGrid, [string]$AxisX, [string]$AxisY, [bool]$FlipX, [bool]$FlipY) {
    $xb = Get-AxisBounds $SourcePoints $AxisX
    $yb = Get-AxisBounds $SourcePoints $AxisY
    if (($xb.max - $xb.min) -lt 1.0 -or ($yb.max - $yb.min) -lt 1.0) { return $null }

    $distances = New-Object System.Collections.ArrayList
    $within32 = 0
    $within64 = 0
    $within128 = 0
    $step = [Math]::Max(1, [int][Math]::Ceiling(@($SourcePoints).Count / [double][Math]::Max(1, $ScoreSampleLimit)))
    for ($i = 0; $i -lt @($SourcePoints).Count; $i += $step) {
        $sp = $SourcePoints[$i]
        $tx = Convert-Range (Select-Axis $sp $AxisX) $xb.min $xb.max $RuntimeBounds.minX $RuntimeBounds.maxX $FlipX
        $ty = Convert-Range (Select-Axis $sp $AxisY) $yb.min $yb.max $RuntimeBounds.minY $RuntimeBounds.maxY $FlipY
        $dist = Find-Nearest2D $RuntimeGrid $tx $ty $NearestCellSize
        if ([double]::IsInfinity($dist)) { $dist = 1000000.0 }
        [void]$distances.Add($dist)
        if ($dist -le 32.0) { $within32++ }
        if ($dist -le 64.0) { $within64++ }
        if ($dist -le 128.0) { $within128++ }
    }
    $sorted = @($distances.ToArray() | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    $median = [double]$sorted[[int][Math]::Floor($sorted.Count / 2)]
    $p75 = [double]$sorted[[int][Math]::Floor($sorted.Count * 0.75)]
    $p90 = [double]$sorted[[int][Math]::Floor($sorted.Count * 0.90)]
    $score = ($within32 * 12.0) + ($within64 * 6.0) + ($within128 * 2.0) - ($median * 0.5) - ($p75 * 0.15)
    return [pscustomobject]@{
        axisX = $AxisX
        axisY = $AxisY
        flipX = $FlipX
        flipY = $FlipY
        sampleCount = $sorted.Count
        within32 = $within32
        within64 = $within64
        within128 = $within128
        medianDistance = [Math]::Round($median, 2)
        p75Distance = [Math]::Round($p75, 2)
        p90Distance = [Math]::Round($p90, 2)
        score = [Math]::Round($score, 2)
        sourceAxisBounds = [ordered]@{
            x = [ordered]@{ min = [Math]::Round($xb.min, 2); max = [Math]::Round($xb.max, 2) }
            y = [ordered]@{ min = [Math]::Round($yb.min, 2); max = [Math]::Round($yb.max, 2) }
        }
    }
}

function Write-Markdown($Report, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Source Map Probe")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Report.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add("Runtime unique points: $($Report.runtime.pointCount); WAD parts decoded: $($Report.wad.decodedPartCount) / $($Report.wad.partCount).")
    [void]$lines.Add("")
    [void]$lines.Add("## Best Candidates")
    [void]$lines.Add("")
    [void]$lines.Add("| Rank | Score | Decode | Axes | Flip | Within 32/64/128 | Median | P75 | P90 |")
    [void]$lines.Add("|---:|---:|---|---|---|---:|---:|---:|---:|")
    $rank = 1
    foreach ($c in @($Report.candidates | Select-Object -First 16)) {
        $decode = "signed=$($c.signedLocal) global=$($c.globalMode)x$($c.globalScale) lod=$($c.includeLod)"
        $axes = "$($c.axisX)/$($c.axisY)"
        $flip = "$(if ($c.flipX) { 'X' } else { '-' })$(if ($c.flipY) { 'Y' } else { '-' })"
        [void]$lines.Add("| $rank | $($c.score) | $decode | $axes | $flip | $($c.within32)/$($c.within64)/$($c.within128) | $($c.medianDistance) | $($c.p75Distance) | $($c.p90Distance) |")
        $rank++
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Interpretation")
    [void]$lines.Add("")
    [void]$lines.Add("This is a source-mapping probe, not a patcher yet. A useful candidate has many source vertices landing near the decoded runtime terrain point cloud after axis/flip/bounds fitting. The next pass should use the best candidate to emit per-source-vertex nearest runtime handles and then verify a few hand-picked terrain edits against DuckStation.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

function Write-LinksMarkdown($Report, [string]$Path) {
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.Add("# Stone Hill Terrain Source Links")
    [void]$lines.Add("")
    [void]$lines.Add("Generated: $($Report.generatedAt)")
    [void]$lines.Add("")
    [void]$lines.Add(("Decode: signed={0} global={1}x{2} lod={3}; axes={4}/{5}; flipX={6}; flipY={7}" -f $Report.candidate.signedLocal, $Report.candidate.globalMode, $Report.candidate.globalScale, $Report.candidate.includeLod, $Report.candidate.axisX, $Report.candidate.axisY, $Report.candidate.flipX, $Report.candidate.flipY))
    [void]$lines.Add("")
    [void]$lines.Add(("Runtime handles: {0}; tested source vertices: {1}; links <= {2}px: {3}." -f $Report.runtimeHandleCount, $Report.links.testedSourcePoints, $Report.links.distanceThreshold, $Report.links.linkCount))
    [void]$lines.Add(("Nearest-source totals: within 32={0}, within 64={1}, within 128={2}." -f $Report.links.within32, $Report.links.within64, $Report.links.within128))
    [void]$lines.Add("")
    $heightFits = @(Get-MapValue $Report "heightFits")
    if ($heightFits.Count -gt 0) {
        [void]$lines.Add("## Height Axis Fits")
        [void]$lines.Add("")
        [void]$lines.Add("| Source axis | Samples | Slope | Intercept | Median abs | P90 abs | RMSE |")
        [void]$lines.Add("|---|---:|---:|---:|---:|---:|---:|")
        foreach ($fit in $heightFits) {
            [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |" -f $fit.sourceAxis, $fit.sampleCount, $fit.slope, $fit.intercept, $fit.medianAbsError, $fit.p90AbsError, $fit.rmse))
        }
        [void]$lines.Add("")
    }
    [void]$lines.Add("## Closest Links")
    [void]$lines.Add("")
    [void]$lines.Add("| Rank | Dist | Source part/vertex | Source offset | Runtime sector/vertex | Runtime sample face |")
    [void]$lines.Add("|---:|---:|---|---|---|---|")
    $rank = 1
    foreach ($link in @($Report.links.links | Select-Object -First 32)) {
        [void]$lines.Add(("| {0} | {1} | {2}/{3} {4} | {5} / {6} | {7} sector {8} vertex {9} | face {10} {11} |" -f $rank, $link.distance, $link.source.part, $link.source.vertexIndex, $link.source.kind, $link.source.sourceOffset, $link.source.modelSubfileOffset, $link.runtime.detail, $link.runtime.sectorIndex, $link.runtime.vertexIndex, $link.runtime.sampleFaceIndex, $link.runtime.sampleFaceOffset))
        $rank++
    }
    [void]$lines.Add("")
    [void]$lines.Add("## Notes")
    [void]$lines.Add("")
    [void]$lines.Add("These rows are nearest-neighbor candidates for permanent terrain export. They should be validated with a small DuckStation live terrain edit before they are trusted as the final BIN patch map.")
    [System.IO.File]::WriteAllLines($Path, $lines.ToArray(), [System.Text.Encoding]::UTF8)
}

$resolvedImage = (Resolve-Path -LiteralPath (Resolve-WorkspacePath $ImagePath) -ErrorAction Stop).Path
$resolvedOverlay = (Resolve-Path -LiteralPath (Resolve-WorkspacePath $RuntimeOverlayPath) -ErrorAction Stop).Path
$overlay = Get-Content -LiteralPath $resolvedOverlay -Raw | ConvertFrom-Json
$runtimePoints = @(Get-RuntimePoints $overlay $MaxRuntimePoints)
$runtimeBounds = Get-Bounds $runtimePoints
$runtimeGrid = New-Grid $runtimePoints $NearestCellSize

$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$decode = Decode-Parts ([byte[]]$modelInfo.modelBytes)

$globalConfigs = if ($Candidate4Only) {
    @(@{ mode = "doc-gxgygz"; scale = 256 })
}
else {
    @(
        @{ mode = "doc-gxgygz"; scale = 1024 },
        @{ mode = "doc-gxgygz"; scale = 512 },
        @{ mode = "doc-gxgygz"; scale = 256 },
        @{ mode = "swapped-gxgygz"; scale = 1024 },
        @{ mode = "swapped-gxgygz"; scale = 512 },
        @{ mode = "swapped-gxgygz"; scale = 256 },
        @{ mode = "doc-signed-gxgygz"; scale = 1024 },
        @{ mode = "doc-signed-gxgygz"; scale = 512 },
        @{ mode = "doc-signed-gxgygz"; scale = 256 },
        @{ mode = "swapped-signed-gxgygz"; scale = 1024 },
        @{ mode = "swapped-signed-gxgygz"; scale = 512 },
        @{ mode = "swapped-signed-gxgygz"; scale = 256 }
    )
}
if ($Candidate4Only) {
    $axisPairs = New-Object System.Collections.ArrayList
    [void]$axisPairs.Add(@("x","z"))
}
else {
    $axisPairs = New-Object System.Collections.ArrayList
    foreach ($pair in @(@("x","y"), @("x","z"), @("y","x"), @("y","z"), @("z","x"), @("z","y"))) {
        [void]$axisPairs.Add($pair)
    }
}
$results = New-Object System.Collections.ArrayList

$signedOptions = if ($Candidate4Only) { @($false) } else { @($true, $false) }
$includeLodOptions = if ($Candidate4Only) { @($false) } else { @($false, $true) }

foreach ($signed in $signedOptions) {
    foreach ($includeLod in $includeLodOptions) {
        foreach ($global in $globalConfigs) {
            $sourcePoints = @(Decode-SourcePoints $decode.parts $signed ([string]$global.mode) ([int]$global.scale) $includeLod)
            if ($sourcePoints.Count -lt 100) { continue }
            foreach ($pair in $axisPairs) {
                foreach ($flipX in @($false, $true)) {
                    foreach ($flipY in @($false, $true)) {
                        $score = Score-Candidate $sourcePoints $runtimeBounds $runtimeGrid $pair[0] $pair[1] $flipX $flipY
                        if ($null -eq $score) { continue }
                        $score | Add-Member -NotePropertyName signedLocal -NotePropertyValue $signed -Force
                        $score | Add-Member -NotePropertyName includeLod -NotePropertyValue $includeLod -Force
                        $score | Add-Member -NotePropertyName globalMode -NotePropertyValue ([string]$global.mode) -Force
                        $score | Add-Member -NotePropertyName globalScale -NotePropertyValue ([int]$global.scale) -Force
                        $score | Add-Member -NotePropertyName sourcePointCount -NotePropertyValue $sourcePoints.Count -Force
                        [void]$results.Add($score)
                    }
                }
            }
        }
    }
}

$ranked = @($results.ToArray() | Sort-Object -Property @{ Expression = { [double]$_.score }; Descending = $true }, @{ Expression = { [int]$_.within64 }; Descending = $true } | Select-Object -First $TopCandidates)
$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    runtimeOverlay = $resolvedOverlay
    runtime = [ordered]@{
        pointCount = $runtimePoints.Count
        bounds = $runtimeBounds
        nearestCellSize = $NearestCellSize
    }
    wad = [ordered]@{
        assetWadIndex = $AssetWadIndex
        modelSubfileIndex = $ModelSubfileIndex
        modelSubfileOffset = ("0x{0:X}" -f [int64]$modelInfo.modelSubfile.offset)
        modelSubfileSize = [int64]$modelInfo.modelSubfile.size
        groundModelOffset = ("0x{0:X}" -f [int]$decode.groundOffset)
        groundModelSize = [int]$decode.groundLength
        partCount = [int]$decode.partCount
        decodedPartCount = @($decode.parts).Count
    }
    candidates = $ranked
}

$linksReport = $null
if (@($ranked).Count -gt 0) {
    $topCandidate = @($ranked)[0]
    $runtimeHandles = @(Get-RuntimeVertexHandles $overlay)
    $runtimeHandleGrid = New-Grid $runtimeHandles $NearestCellSize
    $topSourcePoints = @(Decode-SourcePoints $decode.parts ([bool]$topCandidate.signedLocal) ([string]$topCandidate.globalMode) ([int]$topCandidate.globalScale) ([bool]$topCandidate.includeLod))
    $links = New-SourceRuntimeLinks $topSourcePoints $topCandidate $runtimeBounds $runtimeHandleGrid $LinkDistanceThreshold $MaxLinks
    $heightFits = New-HeightFits -Links @($links.links) -MaxDistance 64.0
    $linksReport = [ordered]@{
        generatedAt = (Get-Date).ToString("s")
        sourceImage = $resolvedImage
        runtimeOverlay = $resolvedOverlay
        candidate = [ordered]@{
            signedLocal = [bool]$topCandidate.signedLocal
            includeLod = [bool]$topCandidate.includeLod
            globalMode = [string]$topCandidate.globalMode
            globalScale = [int]$topCandidate.globalScale
            axisX = [string]$topCandidate.axisX
            axisY = [string]$topCandidate.axisY
            flipX = [bool]$topCandidate.flipX
            flipY = [bool]$topCandidate.flipY
            sourcePointCount = [int]$topCandidate.sourcePointCount
            score = [double]$topCandidate.score
        }
        runtimeHandleCount = $runtimeHandles.Count
        wad = $report.wad
        heightFits = $heightFits
        links = $links
    }
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutJsonPath))
$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutMarkdownPath))
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-Markdown $report $resolvedMarkdown
Write-Host "Wrote terrain source map probe to $resolvedJson"
Write-Host "Wrote markdown summary to $resolvedMarkdown"
if ($null -ne $linksReport) {
    $resolvedLinksJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutLinksPath))
    $resolvedLinksMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Resolve-WorkspacePath $OutLinksMarkdownPath))
    $linksReport | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedLinksJson -Encoding UTF8
    Write-LinksMarkdown $linksReport $resolvedLinksMarkdown
    Write-Host "Wrote terrain source links to $resolvedLinksJson"
    Write-Host "Wrote source-link summary to $resolvedLinksMarkdown"
}
