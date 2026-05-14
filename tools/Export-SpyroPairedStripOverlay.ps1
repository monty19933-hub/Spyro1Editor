param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$DataTableRuntimeAddress = "0x800CD480",
    [string]$IndexTableRuntimeAddress = "0x800CF3C4",
    [string]$OutPath = ".\stonehill-paired-strip-overlay.json",
    [int]$MaxPairs = 52
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

function Convert-PointerToOffset([uint32]$Pointer) {
    return [int]([uint64]$Pointer - $script:PsxRamBase)
}

function Get-MobySummary([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) { return [ordered]@{ count = 0; points = @(); bounds = $null } }
    $start = Convert-PointerToOffset $pointer
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
    return [ordered]@{ count = $points.Count; points = $points }
}

function Read-IndexRecord([byte[]]$Ram, [int]$Offset) {
    $indices = @()
    for ($i = 0; $i -lt 0x24; $i++) {
        $value = [int]$Ram[$Offset + $i]
        if ($value -eq 0xFF) { break }
        if ($value -gt 0x3F) { break }
        $indices += $value
    }
    return $indices
}

function Decode-PackedDataPoints([byte[]]$Ram, [int]$DataOffset, [int]$Length, [int[]]$Indices, [int]$PairIndex) {
    $points = @()
    if ($Indices.Count -eq 0) { return $points }
    $maxIndex = ($Indices | Measure-Object -Maximum).Maximum
    $count = [int]$maxIndex + 1
    if ($count -lt 3) { return $points }

    $cursor = $DataOffset
    $end = [Math]::Min($Ram.Length, $DataOffset + [Math]::Max($Length, 0x24))
    $values = @()
    while (($cursor + 1) -lt $end -and $values.Count -lt ($count * 3)) {
        $values += (Get-Int16LE $Ram $cursor)
        $cursor += 2
    }

    if ($values.Count -lt ($count * 2)) {
        return $points
    }

    for ($i = 0; $i -lt $count; $i++) {
        $a = [int]$values[$i % $values.Count]
        $b = [int]$values[($i + $count) % $values.Count]
        $c = [int]$values[($i + ($count * 2)) % $values.Count]

        # These packed records appear to be local-ish data, not direct world
        # coordinates. Use a stable spread so the index topology can be inspected.
        $x = (($a -band 0x3FFF) / 4.0) + (($PairIndex % 8) * 900)
        $y = (($b -band 0x3FFF) / 4.0) + ([Math]::Floor($PairIndex / 8) * 900)
        $z = (($c -band 0x3FFF) / 4.0)
        $points += [ordered]@{ x = [Math]::Round($x, 2); y = [Math]::Round($y, 2); z = [Math]::Round($z, 2) }
    }
    return $points
}

function Build-Edges($Indices, $Points) {
    $dict = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    for ($i = 0; $i -lt ($Indices.Count - 1); $i++) {
        foreach ($skip in @(1, 2)) {
            if (($i + $skip) -ge $Indices.Count) { continue }
            $a = [int]$Indices[$i]
            $b = [int]$Indices[$i + $skip]
            if ($a -eq $b -or $a -lt 0 -or $b -lt 0 -or $a -ge $Points.Count -or $b -ge $Points.Count) { continue }
            $ka = $a; $kb = $b
            if ($ka -gt $kb) { $t = $ka; $ka = $kb; $kb = $t }
            $pa = $Points[$a]
            $pb = $Points[$b]
            $dict["$ka-$kb"] = [ordered]@{
                a = $a; b = $b
                x1 = [double]$pa.x; y1 = [double]$pa.y
                x2 = [double]$pb.x; y2 = [double]$pb.y
                source = $(if ($skip -eq 1) { "paired-strip-adjacent" } else { "paired-strip-skip" })
            }
        }
    }
    return @($dict.Values)
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) { throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes." }

$dataTableOffset = Convert-RuntimeAddressToOffset $DataTableRuntimeAddress
$indexTableOffset = Convert-RuntimeAddressToOffset $IndexTableRuntimeAddress
$mobySummary = Get-MobySummary $ram

$candidates = @()
$combinedPoints = @()
$combinedEdges = @()
$pairRecords = @()
for ($i = 0; $i -lt $MaxPairs; $i++) {
    $dataPtr = Get-UInt32LE $ram ($dataTableOffset + ($i * 4))
    $indexPtr = Get-UInt32LE $ram ($indexTableOffset + ($i * 4))
    if (-not (Test-PsxPointer $dataPtr) -or -not (Test-PsxPointer $indexPtr)) { break }
    $dataOffset = Convert-PointerToOffset $dataPtr
    $indexOffset = Convert-PointerToOffset $indexPtr
    $nextDataPtr = $(if (($i + 1) -lt $MaxPairs) { Get-UInt32LE $ram ($dataTableOffset + (($i + 1) * 4)) } else { [uint32]0 })
    $dataLength = $(if (Test-PsxPointer $nextDataPtr -and [uint64]$nextDataPtr -gt [uint64]$dataPtr) { [int]([uint64]$nextDataPtr - [uint64]$dataPtr) } else { 0x80 })
    $indices = @(Read-IndexRecord $ram $indexOffset)
    if ($indices.Count -lt 3) { continue }
    $points = @(Decode-PackedDataPoints $ram $dataOffset $dataLength $indices $i)
    if ($points.Count -lt 3) { continue }
    $edges = @(Build-Edges $indices $points)
    $pointBase = $combinedPoints.Count
    $combinedPoints += $points
    $combinedEdges += @($edges | ForEach-Object {
        [ordered]@{
            a = ([int]$_.a + $pointBase)
            b = ([int]$_.b + $pointBase)
            x1 = [double]$_.x1
            y1 = [double]$_.y1
            x2 = [double]$_.x2
            y2 = [double]$_.y2
            source = $_.source
            pairIndex = $i
        }
    })
    $pairRecords += [pscustomobject][ordered]@{
        pairIndex = $i
        dataPointer = ("0x{0:X8}" -f $dataPtr)
        indexPointer = ("0x{0:X8}" -f $indexPtr)
        dataLength = $dataLength
        indexCount = $indices.Count
        localVertexCount = $points.Count
        edgeCount = $edges.Count
    }
    $candidates += [pscustomobject][ordered]@{
        runtimeAddress = ("paired:{0:D2} {1:X8}/{2:X8}" -f $i, $dataPtr, $indexPtr)
        pairIndex = $i
        dataRuntimeAddress = ("0x{0:X8}" -f $dataPtr)
        indexRuntimeAddress = ("0x{0:X8}" -f $indexPtr)
        encoding = "paired-packed-data-byte-index"
        stride = 0
        validVertices = $points.Count
        score = $edges.Count
        projection = "xy"
        fitScore = $edges.Count
        mobysInsideProjection = $mobySummary.count
        projectedPoints = $points
        points = $points
        edges = $edges
        edgeSource = "paired-byte-index-strip"
        edgeFaceCount = $indices.Count
        projectedBounds = [ordered]@{
            minX = (($points | ForEach-Object { $_.x }) | Measure-Object -Minimum).Minimum
            maxX = (($points | ForEach-Object { $_.x }) | Measure-Object -Maximum).Maximum
            minY = (($points | ForEach-Object { $_.y }) | Measure-Object -Minimum).Minimum
            maxY = (($points | ForEach-Object { $_.y }) | Measure-Object -Maximum).Maximum
        }
    }
}

$allCandidates = @()
if ($combinedPoints.Count -gt 0) {
    $allCandidates += [pscustomobject][ordered]@{
        runtimeAddress = "paired:combined $DataTableRuntimeAddress/$IndexTableRuntimeAddress"
        pairIndex = -1
        dataRuntimeAddress = $DataTableRuntimeAddress
        indexRuntimeAddress = $IndexTableRuntimeAddress
        encoding = "paired-packed-data-byte-index-combined"
        stride = 0
        validVertices = $combinedPoints.Count
        score = $combinedEdges.Count
        projection = "xy"
        fitScore = $combinedEdges.Count
        mobysInsideProjection = $mobySummary.count
        projectedPoints = $combinedPoints
        points = $combinedPoints
        edges = $combinedEdges
        edgeSource = "paired-byte-index-strip-combined"
        edgeFaceCount = $pairRecords.Count
        projectedBounds = [ordered]@{
            minX = (($combinedPoints | ForEach-Object { $_.x }) | Measure-Object -Minimum).Minimum
            maxX = (($combinedPoints | ForEach-Object { $_.x }) | Measure-Object -Maximum).Maximum
            minY = (($combinedPoints | ForEach-Object { $_.y }) | Measure-Object -Minimum).Minimum
            maxY = (($combinedPoints | ForEach-Object { $_.y }) | Measure-Object -Maximum).Maximum
        }
    }
}
$allCandidates += $candidates

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Experimental paired strip overlay. Pairs data table $DataTableRuntimeAddress with byte-index table $IndexTableRuntimeAddress by table index. Coordinates are decoded as a diagnostic packed-data spread, not verified world geometry."
    dataTableRuntimeAddress = $DataTableRuntimeAddress
    indexTableRuntimeAddress = $IndexTableRuntimeAddress
    pairCount = $pairRecords.Count
    pairs = $pairRecords
    mobys = $mobySummary
    candidates = $allCandidates
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote paired strip overlay to $resolvedOut"
Write-Host "Paired records: $($pairRecords.Count)"
$pairRecords | Select-Object -First 20 pairIndex, dataPointer, indexPointer, dataLength, indexCount, localVertexCount, edgeCount | Format-Table -AutoSize
