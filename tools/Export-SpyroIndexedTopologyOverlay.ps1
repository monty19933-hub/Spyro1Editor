param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$IndexTableRuntimeAddress = "0x800CF3C4",
    [string]$OutPath = ".\stonehill-indexed-topology-overlay.json",
    [string]$GeometryPath = ".\stonehill-geometry-overlay.json",
    [int]$MaxIndexRecords = 64,
    [int]$MaxVertexCandidates = 16
)

Set-StrictMode -Version 2.0

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt16($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Convert-RuntimeAddressToOffset([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    $value = [Convert]::ToUInt64($clean, 16)
    if ($value -lt $script:PsxRamBase -or $value -ge ($script:PsxRamBase + 0x200000)) {
        throw "$Address is not inside PS1 main RAM."
    }
    return [int]($value - $script:PsxRamBase)
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge $script:PsxRamBase -and $address -lt ($script:PsxRamBase + 0x200000))
}

function Test-Coordinate([int]$Value) {
    return ($Value -ge -4000 -and $Value -le 16000 -and $Value -ne -1)
}

function Test-PlausibleVertex([int]$X, [int]$Y, [int]$Z) {
    if (-not (Test-Coordinate $X) -or -not (Test-Coordinate $Y) -or -not (Test-Coordinate $Z)) { return $false }
    if ([Math]::Abs($X) -lt 8 -and [Math]::Abs($Y) -lt 8 -and [Math]::Abs($Z) -lt 8) { return $false }
    return $true
}

function Get-MobySummary([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) { return [ordered]@{ count = 0; points = @(); bounds = $null } }
    $start = [int]([uint64]$pointer - $script:PsxRamBase)
    $points = @()
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $type = [int]$Ram[$offset + 0x48]
        if ($type -le 0 -or $type -gt 0x7F) { continue }
        $x = (Get-Int32LE $Ram ($offset + 4)) / 16.0
        $y = (Get-Int32LE $Ram ($offset + 8)) / 16.0
        if ($x -lt -2000 -or $x -gt 14000 -or $y -lt -2000 -or $y -gt 14000) { continue }
        $points += [ordered]@{ x = [Math]::Round($x, 2); y = [Math]::Round($y, 2) }
    }
    if ($points.Count -eq 0) { return [ordered]@{ count = 0; points = @(); bounds = $null } }
    $xs = @($points | ForEach-Object { $_.x })
    $ys = @($points | ForEach-Object { $_.y })
    return [ordered]@{
        count = $points.Count
        points = $points
        bounds = [ordered]@{
            minX = ($xs | Measure-Object -Minimum).Minimum
            maxX = ($xs | Measure-Object -Maximum).Maximum
            minY = ($ys | Measure-Object -Minimum).Minimum
            maxY = ($ys | Measure-Object -Maximum).Maximum
        }
    }
}

function Read-IndexRecords([byte[]]$Ram, [int]$TableOffset, [int]$MaxRecords) {
    $records = @()
    $pointers = @()
    for ($i = 0; $i -lt $MaxRecords; $i++) {
        $value = Get-UInt32LE $Ram ($TableOffset + ($i * 4))
        if (-not (Test-PsxPointer $value)) { break }
        $pointerOffset = [int]([uint64]$value - $script:PsxRamBase)
        if ($pointerOffset -lt 0 -or ($pointerOffset + 0x24) -gt $Ram.Length) { break }
        $indices = @()
        for ($b = 0; $b -lt 0x24; $b++) {
            $index = [int]$Ram[$pointerOffset + $b]
            if ($index -eq 0xFF) { break }
            if ($index -lt 0 -or $index -gt 0x3F) { break }
            $indices += $index
        }
        if ($indices.Count -ge 3) {
            $records += [ordered]@{
                pointer = ("0x{0:X8}" -f $value)
                ramOffset = ("0x{0:X}" -f $pointerOffset)
                indices = $indices
            }
            $pointers += $value
        }
    }
    return [ordered]@{ records = $records; pointers = $pointers }
}

function Get-IndexEdges($IndexRecords) {
    $edges = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    foreach ($record in $IndexRecords) {
        $indices = @($record.indices)
        for ($i = 0; $i -lt ($indices.Count - 1); $i++) {
            $a = [int]$indices[$i]
            $b = [int]$indices[$i + 1]
            if ($a -ne $b) {
                if ($a -gt $b) { $t = $a; $a = $b; $b = $t }
                $edges["$a-$b"] = [ordered]@{ a = $a; b = $b; source = "strip-adjacent" }
            }
            if (($i + 2) -lt $indices.Count) {
                $a = [int]$indices[$i]; $b = [int]$indices[$i + 2]
                if ($a -ne $b) {
                    if ($a -gt $b) { $t = $a; $a = $b; $b = $t }
                    $edges["$a-$b"] = [ordered]@{ a = $a; b = $b; source = "strip-skip" }
                }
            }
        }
    }
    return @($edges.Values)
}

function Read-VertexBlock([byte[]]$Ram, [int]$Offset, [int]$Stride, [int]$Count) {
    $points = @()
    $unique = New-Object 'System.Collections.Generic.HashSet[string]'
    $bad = 0
    $sentinelFields = 0
    $minX = 999999; $maxX = -999999
    $minY = 999999; $maxY = -999999
    $minZ = 999999; $maxZ = -999999
    for ($i = 0; $i -lt $Count; $i++) {
        $pos = $Offset + ($i * $Stride)
        if (($pos + 6) -gt $Ram.Length) { return $null }
        $x = Get-Int16LE $Ram $pos
        $y = Get-Int16LE $Ram ($pos + 2)
        $z = Get-Int16LE $Ram ($pos + 4)
        foreach ($value in @($x, $y, $z)) {
            if ($value -eq -1) { $sentinelFields++ }
        }
        if (-not (Test-PlausibleVertex $x $y $z)) { $bad++ }
        $points += [ordered]@{ x = $x; y = $y; z = $z }
        [void]$unique.Add("$x,$y,$z")
        if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        if ($z -lt $minZ) { $minZ = $z }; if ($z -gt $maxZ) { $maxZ = $z }
    }
    $spanX = $maxX - $minX
    $spanY = $maxY - $minY
    $spanZ = $maxZ - $minZ
    $sentinelRatio = $sentinelFields / [double]($Count * 3)
    if ($bad -gt [Math]::Floor($Count * 0.45)) { return $null }
    if ($sentinelRatio -gt 0.35) { return $null }
    if ($unique.Count -lt [Math]::Floor($Count * 0.38)) { return $null }
    if (($spanX -lt 500 -and $spanY -lt 500) -or ($spanX -lt 500 -and $spanZ -lt 500) -or ($spanY -lt 500 -and $spanZ -lt 500)) { return $null }
    $score = ($Count - $bad) + $unique.Count - [Math]::Round($sentinelRatio * 80)
    if ($spanX -gt 1200) { $score += 15 }
    if ($spanY -gt 1200) { $score += 15 }
    if ($spanZ -gt 300) { $score += 8 }
    return [ordered]@{
        ramOffset = $Offset
        runtimeAddress = Format-RuntimeAddress $Offset
        stride = $Stride
        points = $points
        badVertices = $bad
        sentinelRatio = [Math]::Round($sentinelRatio, 3)
        uniqueVertices = $unique.Count
        score = $score
        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
    }
}

function Get-ObjectField($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Read-VertexWindowFromPoints($SourceCandidate, [int]$Start, [int]$Count) {
    $sourcePoints = @(Get-ObjectField $SourceCandidate "points" @())
    if (($Start + $Count) -gt $sourcePoints.Count) { return $null }
    $points = @()
    $unique = New-Object 'System.Collections.Generic.HashSet[string]'
    $bad = 0
    $sentinelFields = 0
    $minX = 999999; $maxX = -999999
    $minY = 999999; $maxY = -999999
    $minZ = 999999; $maxZ = -999999
    for ($i = 0; $i -lt $Count; $i++) {
        $point = $sourcePoints[$Start + $i]
        $x = [int](Get-ObjectField $point "x" 0)
        $y = [int](Get-ObjectField $point "y" 0)
        $z = [int](Get-ObjectField $point "z" 0)
        foreach ($value in @($x, $y, $z)) {
            if ($value -eq -1) { $sentinelFields++ }
        }
        if (-not (Test-PlausibleVertex $x $y $z)) { $bad++ }
        $points += [ordered]@{ x = $x; y = $y; z = $z }
        [void]$unique.Add("$x,$y,$z")
        if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        if ($z -lt $minZ) { $minZ = $z }; if ($z -gt $maxZ) { $maxZ = $z }
    }
    $spanX = $maxX - $minX
    $spanY = $maxY - $minY
    $spanZ = $maxZ - $minZ
    $sentinelRatio = $sentinelFields / [double]($Count * 3)
    if ($bad -gt [Math]::Floor($Count * 0.45)) { return $null }
    if ($sentinelRatio -gt 0.35) { return $null }
    if ($unique.Count -lt [Math]::Floor($Count * 0.38)) { return $null }
    if (($spanX -lt 300 -and $spanY -lt 300) -or ($spanX -lt 300 -and $spanZ -lt 300) -or ($spanY -lt 300 -and $spanZ -lt 300)) { return $null }
    $score = ($Count - $bad) + $unique.Count - [Math]::Round($sentinelRatio * 80)
    if ($spanX -gt 1200) { $score += 15 }
    if ($spanY -gt 1200) { $score += 15 }
    if ($spanZ -gt 300) { $score += 8 }
    $sourceAddress = [string](Get-ObjectField $SourceCandidate "runtimeAddress" "unknown")
    return [ordered]@{
        ramOffset = [int](Get-ObjectField $SourceCandidate "ramOffset" 0)
        runtimeAddress = "$sourceAddress+$Start"
        sourceRuntimeAddress = $sourceAddress
        sourceWindowStart = $Start
        stride = [int](Get-ObjectField $SourceCandidate "stride" 0)
        points = $points
        badVertices = $bad
        sentinelRatio = [Math]::Round($sentinelRatio, 3)
        uniqueVertices = $unique.Count
        score = $score
        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
    }
}

function Project-Point($Point, [string]$Projection) {
    $a = $Projection.Substring(0, 1)
    $b = $Projection.Substring(1, 1)
    return [ordered]@{
        x = [double]$Point[$a]
        y = [double]$Point[$b]
        z = [double]$Point["z"]
    }
}

function Measure-Projection($Candidate, $MobySummary) {
    $best = $null
    foreach ($projection in @("xy", "yx", "xz", "zx", "yz", "zy")) {
        $projected = @($Candidate.points | ForEach-Object { Project-Point $_ $projection })
        $xs = @($projected | ForEach-Object { $_.x })
        $ys = @($projected | ForEach-Object { $_.y })
        $minX = ($xs | Measure-Object -Minimum).Minimum
        $maxX = ($xs | Measure-Object -Maximum).Maximum
        $minY = ($ys | Measure-Object -Minimum).Minimum
        $maxY = ($ys | Measure-Object -Maximum).Maximum
        $inside = 0
        foreach ($moby in @($MobySummary.points)) {
            if ($moby.x -ge ($minX - 900) -and $moby.x -le ($maxX + 900) -and $moby.y -ge ($minY - 900) -and $moby.y -le ($maxY + 900)) { $inside++ }
        }
        $fitScore = ([double]$Candidate.score * 0.45) + ($inside * 125.0)
        $fit = [ordered]@{
            projection = $projection
            fitScore = [Math]::Round($fitScore, 2)
            mobysInside = $inside
            projectedBounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
            projectedPoints = $projected
        }
        if ($null -eq $best -or $fit.fitScore -gt $best.fitScore) { $best = $fit }
    }
    return $best
}

function Build-ProjectedEdges($IndexEdges, $ProjectedPoints) {
    $edges = @()
    foreach ($edge in $IndexEdges) {
        $a = [int]$edge.a
        $b = [int]$edge.b
        if ($a -lt 0 -or $b -lt 0 -or $a -ge $ProjectedPoints.Count -or $b -ge $ProjectedPoints.Count) { continue }
        $pa = $ProjectedPoints[$a]
        $pb = $ProjectedPoints[$b]
        $edges += [ordered]@{
            a = $a; b = $b
            x1 = [Math]::Round([double]$pa.x, 2)
            y1 = [Math]::Round([double]$pa.y, 2)
            x2 = [Math]::Round([double]$pb.x, 2)
            y2 = [Math]::Round([double]$pb.y, 2)
            source = $edge.source
        }
    }
    return $edges
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) { throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes." }

$indexTableOffset = Convert-RuntimeAddressToOffset $IndexTableRuntimeAddress
$indexRead = Read-IndexRecords $ram $indexTableOffset $MaxIndexRecords
$indexRecords = @($indexRead.records)
if ($indexRecords.Count -eq 0) { throw "No byte-index records found at $IndexTableRuntimeAddress." }
$maxIndex = (($indexRecords | ForEach-Object { $_.indices } | Measure-Object -Maximum).Maximum)
$vertexCount = [int]$maxIndex + 1
$indexEdges = @(Get-IndexEdges $indexRecords)

$searchRanges = @(
    @(0xCFC00, 0xCFC00 + 0x8000),
    @(0xD3000, 0xD3000 + 0x12000),
    @(0xE4000, 0xE4000 + 0x18000),
    @(0x114000, 0x114000 + 0x9000),
    @(0x117000, 0x117000 + 0x24000)
)

$mobySummary = Get-MobySummary $ram
$vertexCandidates = @()
foreach ($range in $searchRanges) {
    $start = [int]$range[0]
    $end = [Math]::Min([int]$range[1], $ram.Length - ($vertexCount * 6))
    for ($offset = $start; $offset -lt $end; $offset += 2) {
        foreach ($stride in @(6, 8, 12, 16)) {
            $block = Read-VertexBlock $ram $offset $stride $vertexCount
            if ($null -eq $block) { continue }
            $fit = Measure-Projection $block $mobySummary
            $block.projection = $fit.projection
            $block.fitScore = $fit.fitScore
            $block.mobysInsideProjection = $fit.mobysInside
            $block.projectedBounds = $fit.projectedBounds
            $block.projectedPoints = $fit.projectedPoints
            $block.edges = Build-ProjectedEdges $indexEdges $fit.projectedPoints
            $block.edgeSource = "byte-index-strip-table"
            $block.edgeFaceCount = $indexRecords.Count
            $block.indexTableRuntimeAddress = $IndexTableRuntimeAddress
            $block.indexRecordCount = $indexRecords.Count
            $block.localVertexCount = $vertexCount
            $vertexCandidates += [pscustomobject]$block
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($GeometryPath) -and (Test-Path -LiteralPath $GeometryPath)) {
    $geometry = Get-Content -Raw -LiteralPath $GeometryPath | ConvertFrom-Json
    foreach ($sourceCandidate in @($geometry.candidates)) {
        $sourcePoints = @(Get-ObjectField $sourceCandidate "points" @())
        if ($sourcePoints.Count -lt $vertexCount) { continue }
        for ($start = 0; $start -le ($sourcePoints.Count - $vertexCount); $start += 1) {
            $block = Read-VertexWindowFromPoints $sourceCandidate $start $vertexCount
            if ($null -eq $block) { continue }
            $fit = Measure-Projection $block $mobySummary
            $block.projection = $fit.projection
            $block.fitScore = $fit.fitScore
            $block.mobysInsideProjection = $fit.mobysInside
            $block.projectedBounds = $fit.projectedBounds
            $block.projectedPoints = $fit.projectedPoints
            $block.edges = Build-ProjectedEdges $indexEdges $fit.projectedPoints
            $block.edgeSource = "byte-index-strip-table-over-geometry-window"
            $block.edgeFaceCount = $indexRecords.Count
            $block.indexTableRuntimeAddress = $IndexTableRuntimeAddress
            $block.indexRecordCount = $indexRecords.Count
            $block.localVertexCount = $vertexCount
            $vertexCandidates += [pscustomobject]$block
        }
    }
}

$ranked = @($vertexCandidates | Sort-Object -Property fitScore, score -Descending | Select-Object -First $MaxVertexCandidates)

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Experimental indexed-topology overlay. Byte index strips from $IndexTableRuntimeAddress are paired with candidate $vertexCount-vertex coordinate blocks or windows from $GeometryPath."
    indexTableRuntimeAddress = $IndexTableRuntimeAddress
    indexRecordCount = $indexRecords.Count
    localVertexCount = $vertexCount
    indexEdgeCount = $indexEdges.Count
    indexRecords = $indexRecords
    mobys = $mobySummary
    candidates = $ranked
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote indexed topology overlay to $resolvedOut"
Write-Host "Index records: $($indexRecords.Count), local vertices: $vertexCount, edges: $($indexEdges.Count), vertex candidates: $($ranked.Count)"
$ranked | Select-Object runtimeAddress, stride, badVertices, uniqueVertices, score, projection, fitScore, mobysInsideProjection, @{n="Edges";e={@($_.edges).Count}} | Format-Table -AutoSize
