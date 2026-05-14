param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$ChunkRuntimeAddress = "0x800E4EF0",
    [int]$RecordStride = 12,
    [int]$XOffset = 0,
    [int]$YOffset = 4,
    [int]$ZOffset = 8,
    [int]$MaxRecords = 768,
    [string]$OutPath = ".\stonehill-descriptor-chunk-overlay.json",
    [int]$BinSize = 1024,
    [int]$MinSlicePoints = 12
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

function Get-MobySummary([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) { return [ordered]@{ count = 0; points = @(); bounds = $null } }
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

function Test-PlausibleWorldPoint([int]$X, [int]$Y, [int]$Z) {
    return ($X -ge -2000 -and $X -le 15000 -and
        $Y -ge -2000 -and $Y -le 15000 -and
        $Z -ge -4000 -and $Z -le 15000)
}

function Read-ChunkPoints([byte[]]$Ram, [int]$ChunkOffset, [int]$Stride, [int]$Limit) {
    $points = @()
    $badRun = 0
    for ($i = 0; $i -lt $Limit; $i++) {
        $offset = $ChunkOffset + ($i * $Stride)
        if (($offset + $Stride) -gt $Ram.Length) { break }

        $x = Get-Int16LE $Ram ($offset + $script:XOffset)
        $y = Get-Int16LE $Ram ($offset + $script:YOffset)
        $z = Get-Int16LE $Ram ($offset + $script:ZOffset)
        $flag0 = Get-Int16LE $Ram ($offset + 2)
        $flag1 = Get-Int16LE $Ram ($offset + 6)
        $flag2 = Get-Int16LE $Ram ($offset + 10)
        if (Test-PlausibleWorldPoint $x $y $z) {
            $badRun = 0
            $points += [ordered]@{
                index = $i
                sourceRuntimeAddress = Format-RuntimeAddress $offset
                sourceRamOffset = ("0x{0:X}" -f $offset)
                x = $x
                y = $y
                z = $z
                flag0 = $flag0
                flag1 = $flag1
                flag2 = $flag2
            }
        }
        else {
            $badRun++
            if ($points.Count -gt 24 -and $badRun -ge 16) { break }
        }
    }
    return $points
}

function Project-Point($Point, [string]$Projection) {
    $a = $Projection.Substring(0, 1)
    $b = $Projection.Substring(1, 1)
    return [ordered]@{
        index = $Point.index
        sourceRuntimeAddress = $Point.sourceRuntimeAddress
        sourceRamOffset = $Point.sourceRamOffset
        x = [double]$Point.$a
        y = [double]$Point.$b
        z = [double]$Point.z
        rawX = [double]$Point.x
        rawY = [double]$Point.y
        rawZ = [double]$Point.z
        flag0 = $Point.flag0
        flag1 = $Point.flag1
        flag2 = $Point.flag2
    }
}

function Get-Bounds($Points) {
    $xs = @($Points | ForEach-Object { [double]$_.x })
    $ys = @($Points | ForEach-Object { [double]$_.y })
    return [ordered]@{
        minX = ($xs | Measure-Object -Minimum).Minimum
        maxX = ($xs | Measure-Object -Maximum).Maximum
        minY = ($ys | Measure-Object -Minimum).Minimum
        maxY = ($ys | Measure-Object -Maximum).Maximum
    }
}

function Count-MobysInside($MobyPoints, $Bounds, [double]$Padding = 128.0) {
    $count = 0
    foreach ($moby in @($MobyPoints)) {
        $x = [double]$moby.x
        $y = [double]$moby.y
        if ($x -ge ([double]$Bounds.minX - $Padding) -and $x -le ([double]$Bounds.maxX + $Padding) -and
            $y -ge ([double]$Bounds.minY - $Padding) -and $y -le ([double]$Bounds.maxY + $Padding)) {
            $count++
        }
    }
    return $count
}

function New-Candidate([string]$Name, $Points, $MobyPoints, [bool]$WithEdges, [string]$Projection = "xy") {
    $points = @($Points | ForEach-Object { Project-Point $_ $Projection })
    if ($points.Count -eq 0) { return $null }
    $bounds = Get-Bounds $points
    $edges = @()
    if ($WithEdges -and $points.Count -gt 2) {
        for ($i = 0; $i -lt ($points.Count - 2); $i += 3) {
            $a = $points[$i]
            $b = $points[$i + 1]
            $c = $points[$i + 2]
            $edges += [ordered]@{ a = $i; b = $i + 1; x1 = [double]$a.x; y1 = [double]$a.y; x2 = [double]$b.x; y2 = [double]$b.y; source = "sequential-triplet" }
            $edges += [ordered]@{ a = $i + 1; b = $i + 2; x1 = [double]$b.x; y1 = [double]$b.y; x2 = [double]$c.x; y2 = [double]$c.y; source = "sequential-triplet" }
            $edges += [ordered]@{ a = $i + 2; b = $i; x1 = [double]$c.x; y1 = [double]$c.y; x2 = [double]$a.x; y2 = [double]$a.y; source = "sequential-triplet" }
        }
    }
    $mobysInside = Count-MobysInside $MobyPoints $bounds
    return [ordered]@{
        runtimeAddress = ("descriptor-chunk:{0} {1}" -f $Name, $ChunkRuntimeAddress)
        sourceRuntimeAddress = $ChunkRuntimeAddress
        encoding = $(if ($WithEdges) { "descriptor-low-halfword-triplets" } else { "descriptor-low-halfword-points" })
        stride = $RecordStride
        xOffset = $XOffset
        yOffset = $YOffset
        zOffset = $ZOffset
        validVertices = $points.Count
        score = (($mobysInside * 1000) + $points.Count + $edges.Count)
        projection = $Projection
        fitScore = (($mobysInside * 1000) + $points.Count + $edges.Count)
        mobysInsideProjection = $mobysInside
        projectedBounds = $bounds
        projectedPoints = $points
        points = $points
        edges = $edges
        edgeSource = $(if ($WithEdges) { "sequential-triplet-diagnostic" } else { "points-only" })
        edgeFaceCount = $(if ($WithEdges) { [Math]::Floor($points.Count / 3) } else { 0 })
    }
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$chunkOffset = Convert-RuntimeAddressToOffset $ChunkRuntimeAddress
$mobySummary = Get-MobySummary $ram
$points = @(Read-ChunkPoints $ram $chunkOffset $RecordStride $MaxRecords)
if ($points.Count -lt 24) {
    throw "Only decoded $($points.Count) plausible records from $ChunkRuntimeAddress."
}

$candidates = @()
$projections = @("xy", "xz", "yx", "yz", "zx", "zy")
foreach ($projection in $projections) {
    $candidates += (New-Candidate "all-points $projection" $points $mobySummary.points $false $projection)
}

$groups = @{}
foreach ($point in $points) {
    $bin = [int]([Math]::Floor([double]$point.z / [double]$BinSize) * $BinSize)
    $key = [string]$bin
    if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
    $groups[$key] = @($groups[$key] + $point)
}

foreach ($key in @($groups.Keys | Sort-Object { [int]$_ })) {
    $slice = @($groups[$key])
    if ($slice.Count -lt $MinSlicePoints) { continue }
    $zMin = [int]$key
    $zMax = $zMin + $BinSize - 1
    foreach ($projection in $projections) {
        $candidates += (New-Candidate ("z {0}..{1} points {2}" -f $zMin, $zMax, $projection) $slice $mobySummary.points $false $projection)
    }
}

foreach ($projection in $projections) {
    $candidates += (New-Candidate "all-sequential-triplets $projection" $points $mobySummary.points $true $projection)
}

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Descriptor chunk overlay from selected int16 fields. Multiple axis projections are included because the true horizontal axes are not confirmed. Points-only candidates are listed first to avoid drawing misleading inferred topology; sequential triplets are diagnostic only."
    chunkRuntimeAddress = $ChunkRuntimeAddress
    recordStride = $RecordStride
    xOffset = $XOffset
    yOffset = $YOffset
    zOffset = $ZOffset
    decodedRecords = $points.Count
    mobys = $mobySummary
    candidates = @($candidates | Where-Object { $null -ne $_ })
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote descriptor chunk overlay to $resolvedOut"
Write-Host "Decoded records: $($points.Count)"
$result.candidates | Select-Object runtimeAddress, validVertices, mobysInsideProjection, edgeSource, @{n="Edges";e={@($_.edges).Count}} | Format-Table -AutoSize
