param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$PointTableRuntimeAddress = "0x80119F58",
    [int]$PointCount = 35,
    [string]$IndexTableRuntimeAddress = "0x800CF3C4",
    [int]$IndexRecordCount = 52,
    [string]$OutPath = ".\stonehill-indexed-pointer-overlay.json"
)

Set-StrictMode -Version 2.0

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Convert-RuntimeAddressToOffset([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    $value = [Convert]::ToUInt64($clean, 16)
    if ($value -lt $script:PsxRamBase -or $value -ge ($script:PsxRamBase + 0x200000)) {
        throw "$Address is not inside PS1 main RAM."
    }
    return [int]($value - $script:PsxRamBase)
}

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt16($Bytes, $Offset)
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge $script:PsxRamBase -and $address -lt ($script:PsxRamBase + 0x200000))
}

function Convert-PointerToOffset([uint32]$Pointer) {
    return [int]([uint64]$Pointer - $script:PsxRamBase)
}

function Read-IndexRecord([byte[]]$Ram, [int]$Offset) {
    $indices = @()
    for ($i = 0; $i -lt 0x24; $i++) {
        $value = [int]$Ram[$Offset + $i]
        if ($value -eq 0xFF) { break }
        if ($value -lt 0 -or $value -gt 0x3F) { break }
        $indices += $value
    }
    return $indices
}

function Build-Edges([object[]]$IndexRecords, [object[]]$Points) {
    $dict = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    foreach ($record in $IndexRecords) {
        $indices = @($record.indices)
        for ($i = 0; $i -lt ($indices.Count - 1); $i++) {
            foreach ($skip in @(1, 2)) {
                if (($i + $skip) -ge $indices.Count) { continue }
                $a = [int]$indices[$i]
                $b = [int]$indices[$i + $skip]
                if ($a -eq $b -or $a -lt 0 -or $b -lt 0 -or $a -ge $Points.Count -or $b -ge $Points.Count) { continue }
                $ka = $a
                $kb = $b
                if ($ka -gt $kb) {
                    $tmp = $ka
                    $ka = $kb
                    $kb = $tmp
                }
                $pa = $Points[$a]
                $pb = $Points[$b]
                $key = "$ka-$kb"
                if (-not $dict.ContainsKey($key)) {
                    $dict[$key] = [ordered]@{
                        a = $a
                        b = $b
                        x1 = [double]$pa.x
                        y1 = [double]$pa.y
                        x2 = [double]$pb.x
                        y2 = [double]$pb.y
                        source = $(if ($skip -eq 1) { "indexed-pointer-adjacent" } else { "indexed-pointer-skip" })
                        firstRecord = [int]$record.recordIndex
                    }
                }
            }
        }
    }
    return @($dict.Values)
}

function Get-MobySummary([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) { return [ordered]@{ count = 0; points = @() } }
    $start = Convert-PointerToOffset $pointer
    $points = @()
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 0x50)
        if (($offset + 0x50) -gt $Ram.Length) { break }
        $type = [int]$Ram[$offset + 0x48]
        if ($type -le 0 -or $type -gt 0x7F) { continue }
        $x = [BitConverter]::ToInt32($Ram, $offset + 4) / 16.0
        $y = [BitConverter]::ToInt32($Ram, $offset + 8) / 16.0
        if ($x -lt -2000 -or $x -gt 14000 -or $y -lt -2000 -or $y -gt 14000) { continue }
        $points += [ordered]@{ x = [Math]::Round($x, 2); y = [Math]::Round($y, 2) }
    }
    return [ordered]@{ count = $points.Count; points = $points }
}

function Make-Point($PointerIndex, $Pointer, $Offset, [int[]]$Words, [int]$XWord, [int]$YWord, [int]$ZWord, [double]$Scale) {
    return [ordered]@{
        index = $PointerIndex
        sourceRuntimeAddress = ("0x{0:X8}" -f $Pointer)
        sourceRamOffset = ("0x{0:X}" -f $Offset)
        x = [Math]::Round($Words[$XWord] / $Scale, 3)
        y = [Math]::Round($Words[$YWord] / $Scale, 3)
        z = [Math]::Round($Words[$ZWord] / $Scale, 3)
        rawI16 = $Words
    }
}

function Read-RecordPairs([byte[]]$Ram, [int]$Offset) {
    $pairs = @()
    for ($i = 0; $i -lt 32; $i++) {
        $x = Get-Int16LE $Ram ($Offset + ($i * 4))
        $y = Get-Int16LE $Ram ($Offset + ($i * 4) + 2)
        if ($x -eq -1 -and $y -eq -1) { break }
        if ([Math]::Abs($x) -gt 8192 -or [Math]::Abs($y) -gt 8192) { break }
        $pairs += [ordered]@{ x = $x; y = $y }
    }
    return $pairs
}

function Build-RecordOutlineCandidate([object[]]$Records, [string]$Name, [double]$Scale, [bool]$SwapAxes, [bool]$FlipY, [object]$MobySummary) {
    $points = @()
    $edges = @()
    foreach ($record in $Records) {
        $recordStart = $points.Count
        $pairs = @($record.pairs)
        for ($i = 0; $i -lt $pairs.Count; $i++) {
            $rawX = [double]$pairs[$i].x
            $rawY = [double]$pairs[$i].y
            if ($SwapAxes) {
                $tmp = $rawX
                $rawX = $rawY
                $rawY = $tmp
            }
            if ($FlipY) { $rawY = -$rawY }
            $points += [ordered]@{
                recordIndex = [int]$record.index
                localIndex = $i
                sourceRuntimeAddress = [string]$record.pointer
                sourceRamOffset = [string]$record.ramOffset
                x = [Math]::Round($rawX / $Scale, 3)
                y = [Math]::Round($rawY / $Scale, 3)
                z = [int]$record.index
            }
        }
        for ($i = 0; $i -lt ($pairs.Count - 1); $i++) {
            $a = $recordStart + $i
            $b = $recordStart + $i + 1
            $pa = $points[$a]
            $pb = $points[$b]
            $edges += [ordered]@{
                a = $a; b = $b
                x1 = [double]$pa.x; y1 = [double]$pa.y
                x2 = [double]$pb.x; y2 = [double]$pb.y
                source = "pointer-record-outline"
                recordIndex = [int]$record.index
            }
        }
        if ($pairs.Count -gt 2) {
            $a = $recordStart + $pairs.Count - 1
            $b = $recordStart
            $pa = $points[$a]
            $pb = $points[$b]
            $edges += [ordered]@{
                a = $a; b = $b
                x1 = [double]$pa.x; y1 = [double]$pa.y
                x2 = [double]$pb.x; y2 = [double]$pb.y
                source = "pointer-record-outline-close"
                recordIndex = [int]$record.index
            }
        }
    }

    $xs = @($points | ForEach-Object { [double]$_.x })
    $ys = @($points | ForEach-Object { [double]$_.y })
    return [ordered]@{
        runtimeAddress = ("indexed-pointer-outlines:{0} {1}" -f $Name, $PointTableRuntimeAddress)
        pointTableRuntimeAddress = $PointTableRuntimeAddress
        indexTableRuntimeAddress = $IndexTableRuntimeAddress
        encoding = $Name
        stride = 0
        validVertices = $points.Count
        score = $edges.Count
        projection = $Name
        fitScore = $edges.Count
        mobysInsideProjection = $MobySummary.count
        projectedPoints = $points
        points = $points
        edges = $edges
        edgeSource = "pointer-record-outlines"
        edgeFaceCount = $Records.Count
        projectedBounds = [ordered]@{
            minX = ($xs | Measure-Object -Minimum).Minimum
            maxX = ($xs | Measure-Object -Maximum).Maximum
            minY = ($ys | Measure-Object -Minimum).Minimum
            maxY = ($ys | Measure-Object -Maximum).Maximum
        }
    }
}

function Get-PointBounds([object[]]$Points) {
    $xs = @($Points | ForEach-Object { [double]$_.x })
    $ys = @($Points | ForEach-Object { [double]$_.y })
    return [ordered]@{
        minX = ($xs | Measure-Object -Minimum).Minimum
        maxX = ($xs | Measure-Object -Maximum).Maximum
        minY = ($ys | Measure-Object -Minimum).Minimum
        maxY = ($ys | Measure-Object -Maximum).Maximum
    }
}

function Copy-PointWithTransform($Point, [double]$Scale, [double]$OffsetX, [double]$OffsetY) {
    $copy = [ordered]@{}
    if ($Point -is [System.Collections.IDictionary]) {
        foreach ($key in $Point.Keys) { $copy[$key] = $Point[$key] }
    }
    else {
        foreach ($prop in $Point.PSObject.Properties) {
            $copy[$prop.Name] = $prop.Value
        }
    }
    $x = [double](Get-ObjectFieldCompat $Point "x" 0)
    $y = [double](Get-ObjectFieldCompat $Point "y" 0)
    $copy["rawX"] = $x
    $copy["rawY"] = $y
    $copy["x"] = [Math]::Round(($x * $Scale) + $OffsetX, 3)
    $copy["y"] = [Math]::Round(($y * $Scale) + $OffsetY, 3)
    return $copy
}

function Build-FittedCandidate($Candidate, [object]$MobySummary) {
    $mobyPoints = @($MobySummary.points)
    if ($mobyPoints.Count -lt 2) { return $null }
    $sourcePoints = @(Get-ObjectFieldArrayCompat $Candidate "projectedPoints")
    if ($sourcePoints.Count -eq 0) { return $null }

    $sourceBounds = Get-PointBounds $sourcePoints
    $mobyBounds = Get-PointBounds $mobyPoints
    $sourceWidth = [Math]::Max(1.0, [double]$sourceBounds.maxX - [double]$sourceBounds.minX)
    $sourceHeight = [Math]::Max(1.0, [double]$sourceBounds.maxY - [double]$sourceBounds.minY)
    $mobyWidth = [Math]::Max(1.0, [double]$mobyBounds.maxX - [double]$mobyBounds.minX)
    $mobyHeight = [Math]::Max(1.0, [double]$mobyBounds.maxY - [double]$mobyBounds.minY)
    $scale = [Math]::Min($mobyWidth / $sourceWidth, $mobyHeight / $sourceHeight)
    $sourceCenterX = ([double]$sourceBounds.minX + [double]$sourceBounds.maxX) / 2.0
    $sourceCenterY = ([double]$sourceBounds.minY + [double]$sourceBounds.maxY) / 2.0
    $mobyCenterX = ([double]$mobyBounds.minX + [double]$mobyBounds.maxX) / 2.0
    $mobyCenterY = ([double]$mobyBounds.minY + [double]$mobyBounds.maxY) / 2.0
    $offsetX = $mobyCenterX - ($sourceCenterX * $scale)
    $offsetY = $mobyCenterY - ($sourceCenterY * $scale)

    $points = @()
    foreach ($point in $sourcePoints) {
        $points += (Copy-PointWithTransform $point $scale $offsetX $offsetY)
    }

    $edges = @()
    foreach ($edge in @(Get-ObjectFieldArrayCompat $Candidate "edges")) {
        $copy = [ordered]@{}
        if ($edge -is [System.Collections.IDictionary]) {
            foreach ($key in $edge.Keys) { $copy[$key] = $edge[$key] }
        }
        else {
            foreach ($prop in $edge.PSObject.Properties) {
                $copy[$prop.Name] = $prop.Value
            }
        }
        $x1 = [double](Get-ObjectFieldCompat $edge "x1" 0)
        $y1 = [double](Get-ObjectFieldCompat $edge "y1" 0)
        $x2 = [double](Get-ObjectFieldCompat $edge "x2" 0)
        $y2 = [double](Get-ObjectFieldCompat $edge "y2" 0)
        $copy["rawX1"] = $x1
        $copy["rawY1"] = $y1
        $copy["rawX2"] = $x2
        $copy["rawY2"] = $y2
        $copy["x1"] = [Math]::Round(($x1 * $scale) + $offsetX, 3)
        $copy["y1"] = [Math]::Round(($y1 * $scale) + $offsetY, 3)
        $copy["x2"] = [Math]::Round(($x2 * $scale) + $offsetX, 3)
        $copy["y2"] = [Math]::Round(($y2 * $scale) + $offsetY, 3)
        $edges += $copy
    }

    $bounds = Get-PointBounds $points
    $copyCandidate = [ordered]@{}
    if ($Candidate -is [System.Collections.IDictionary]) {
        foreach ($key in $Candidate.Keys) {
            if ($key -in @("projectedPoints", "points", "edges", "projectedBounds")) { continue }
            $copyCandidate[$key] = $Candidate[$key]
        }
    }
    else {
        foreach ($prop in $Candidate.PSObject.Properties) {
            if ($prop.Name -in @("projectedPoints", "points", "edges", "projectedBounds")) { continue }
            $copyCandidate[$prop.Name] = $prop.Value
        }
    }
    $copyCandidate["runtimeAddress"] = "diagnostic-fit-to-mobys: $([string](Get-ObjectFieldCompat $Candidate "runtimeAddress" "candidate"))"
    $copyCandidate["encoding"] = "diagnostic-fit-to-mobys/$([string](Get-ObjectFieldCompat $Candidate "encoding" "unknown"))"
    $copyCandidate["projection"] = "fit-to-runtime-moby-bounds"
    $copyCandidate["projectedPoints"] = $points
    $copyCandidate["points"] = $points
    $copyCandidate["edges"] = $edges
    $copyCandidate["edgeSource"] = "diagnostic-fit-to-mobys/$([string](Get-ObjectFieldCompat $Candidate "edgeSource" "unknown"))"
    $copyCandidate["projectedBounds"] = $bounds
    $copyCandidate["fitTransform"] = [ordered]@{
        scale = [Math]::Round($scale, 6)
        offsetX = [Math]::Round($offsetX, 3)
        offsetY = [Math]::Round($offsetY, 3)
        sourceBounds = $sourceBounds
        mobyBounds = $mobyBounds
        note = "Diagnostic visualization only. This is not verified game world placement."
    }
    return $copyCandidate
}

function Get-ObjectFieldCompat($Object, [string]$FieldName, $DefaultValue = $null) {
    if ($null -eq $Object) { return $DefaultValue }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($FieldName)) { return $Object[$FieldName] }
        return $DefaultValue
    }
    $prop = $Object.PSObject.Properties[$FieldName]
    if ($null -eq $prop) { return $DefaultValue }
    return $prop.Value
}

function Get-ObjectFieldArrayCompat($Object, [string]$FieldName) {
    if ($null -eq $Object) { return @() }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($FieldName) -or $null -eq $Object[$FieldName]) { return @() }
        return @($Object[$FieldName])
    }
    $prop = $Object.PSObject.Properties[$FieldName]
    if ($null -eq $prop -or $null -eq $prop.Value) { return @() }
    return @($prop.Value)
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$pointTableOffset = Convert-RuntimeAddressToOffset $PointTableRuntimeAddress
$indexTableOffset = Convert-RuntimeAddressToOffset $IndexTableRuntimeAddress
$mobySummary = Get-MobySummary $ram

$rawPointRecords = @()
for ($i = 0; $i -lt $PointCount; $i++) {
    $ptr = Get-UInt32LE $ram ($pointTableOffset + ($i * 4))
    if (-not (Test-PsxPointer $ptr)) {
        throw "Point pointer $i at $PointTableRuntimeAddress is not a valid PS1 RAM pointer."
    }
    $offset = Convert-PointerToOffset $ptr
    $words = @()
    for ($w = 0; $w -lt 12; $w++) {
        $words += (Get-Int16LE $ram ($offset + ($w * 2)))
    }
    $pairs = @(Read-RecordPairs $ram $offset)
    $rawPointRecords += [ordered]@{
        index = $i
        pointer = ("0x{0:X8}" -f $ptr)
        ramOffset = ("0x{0:X}" -f $offset)
        firstWords = $words
        pairs = $pairs
    }
}

$indexRecords = @()
for ($i = 0; $i -lt $IndexRecordCount; $i++) {
    $ptr = Get-UInt32LE $ram ($indexTableOffset + ($i * 4))
    if (-not (Test-PsxPointer $ptr)) { break }
    $offset = Convert-PointerToOffset $ptr
    $indices = @(Read-IndexRecord $ram $offset)
    if ($indices.Count -lt 3) { continue }
    $indexRecords += [ordered]@{
        recordIndex = $i
        pointer = ("0x{0:X8}" -f $ptr)
        ramOffset = ("0x{0:X}" -f $offset)
        indices = $indices
    }
}

$variants = @(
    @{ name = "i16-012-xy"; x = 0; y = 1; z = 2; scale = 1.0 },
    @{ name = "i16-021-xz"; x = 0; y = 2; z = 1; scale = 1.0 },
    @{ name = "i16-032-xw"; x = 0; y = 3; z = 2; scale = 1.0 },
    @{ name = "i16-245"; x = 2; y = 4; z = 5; scale = 1.0 },
    @{ name = "i16-012-div16"; x = 0; y = 1; z = 2; scale = 16.0 },
    @{ name = "i16-021-div16"; x = 0; y = 2; z = 1; scale = 16.0 }
)

$candidates = @()
$candidates += (Build-RecordOutlineCandidate $rawPointRecords "record-pairs-xy" 1.0 $false $false $mobySummary)
$candidates += (Build-RecordOutlineCandidate $rawPointRecords "record-pairs-x-flip-y" 1.0 $false $true $mobySummary)
$candidates += (Build-RecordOutlineCandidate $rawPointRecords "record-pairs-yx" 1.0 $true $false $mobySummary)
foreach ($variant in $variants) {
    $points = @()
    foreach ($record in $rawPointRecords) {
        $pointer = [Convert]::ToUInt32(([string]$record.pointer).Substring(2), 16)
        $offset = [Convert]::ToInt32(([string]$record.ramOffset).Substring(2), 16)
        $points += (Make-Point ([int]$record.index) $pointer $offset @($record.firstWords) ([int]$variant.x) ([int]$variant.y) ([int]$variant.z) ([double]$variant.scale))
    }
    $edges = @(Build-Edges $indexRecords $points)
    $xs = @($points | ForEach-Object { [double]$_.x })
    $ys = @($points | ForEach-Object { [double]$_.y })
    $candidates += [ordered]@{
        runtimeAddress = ("indexed-pointer:{0} {1}/{2}" -f $variant.name, $PointTableRuntimeAddress, $IndexTableRuntimeAddress)
        pointTableRuntimeAddress = $PointTableRuntimeAddress
        indexTableRuntimeAddress = $IndexTableRuntimeAddress
        encoding = $variant.name
        stride = 0
        validVertices = $points.Count
        score = $edges.Count
        projection = $variant.name
        fitScore = $edges.Count
        mobysInsideProjection = $mobySummary.count
        projectedPoints = $points
        points = $points
        edges = $edges
        edgeSource = "indexed-pointer-table"
        edgeFaceCount = $indexRecords.Count
        projectedBounds = [ordered]@{
            minX = ($xs | Measure-Object -Minimum).Minimum
            maxX = ($xs | Measure-Object -Maximum).Maximum
            minY = ($ys | Measure-Object -Minimum).Minimum
            maxY = ($ys | Measure-Object -Maximum).Maximum
        }
    }
}

$fittedCandidates = @()
foreach ($candidate in $candidates) {
    $fitted = Build-FittedCandidate $candidate $mobySummary
    if ($null -ne $fitted) { $fittedCandidates += $fitted }
}
$candidates = @($fittedCandidates + $candidates)

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Experimental overlay using the 35-entry pointer table referenced next to the 52-entry byte-index table. Diagnostic fit-to-moby candidates are listed first so local shapes do not pile up at the origin; raw local-coordinate candidates follow."
    pointTableRuntimeAddress = $PointTableRuntimeAddress
    pointCount = $PointCount
    indexTableRuntimeAddress = $IndexTableRuntimeAddress
    indexRecordCount = $indexRecords.Count
    rawPointRecords = $rawPointRecords
    indexRecords = $indexRecords
    mobys = $mobySummary
    candidates = $candidates
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote indexed pointer overlay to $resolvedOut"
Write-Host "Point pointers: $($rawPointRecords.Count)"
Write-Host "Index records: $($indexRecords.Count)"
$candidates | Select-Object runtimeAddress, validVertices, edgeFaceCount, score | Format-Table -AutoSize
