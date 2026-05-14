param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$FocusRuntimeAddress = "0x800CFBEC",
    [string]$OutPath = ".\stonehill-descriptor-geometry-overlay.json",
    [int]$MaxPointers = 8
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

function Test-PlausibleVertex([int]$X, [int]$Y, [int]$Z) {
    if ($X -lt -4000 -or $X -gt 16000) { return $false }
    if ($Y -lt -4000 -or $Y -gt 16000) { return $false }
    if ($Z -lt -6000 -or $Z -gt 10000) { return $false }
    if ([Math]::Abs($X) -lt 8 -and [Math]::Abs($Y) -lt 8 -and [Math]::Abs($Z) -lt 8) { return $false }
    return $true
}

function Get-FieldValue($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Measure-VertexCloud([byte[]]$Ram, [int]$Offset, [int]$Stride) {
    $points = @()
    $unique = New-Object 'System.Collections.Generic.HashSet[string]'
    $coordinateFields = 0
    $negativeOneFields = 0
    $zeroFields = 0
    $badRun = 0
    $minX = 999999; $maxX = -999999
    $minY = 999999; $maxY = -999999
    $minZ = 999999; $maxZ = -999999

    for ($i = 0; $i -lt 512; $i++) {
        $pos = $Offset + ($i * $Stride)
        if (($pos + 6) -gt $Ram.Length) { break }
        $x = Get-Int16LE $Ram $pos
        $y = Get-Int16LE $Ram ($pos + 2)
        $z = Get-Int16LE $Ram ($pos + 4)
        if (Test-PlausibleVertex $x $y $z) {
            $badRun = 0
            if ($points.Count -lt 700) { $points += [ordered]@{ x = $x; y = $y; z = $z } }
            [void]$unique.Add("$x,$y,$z")
            foreach ($value in @($x, $y, $z)) {
                $coordinateFields++
                if ($value -eq -1) { $negativeOneFields++ }
                elseif ($value -eq 0) { $zeroFields++ }
            }
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            if ($z -lt $minZ) { $minZ = $z }; if ($z -gt $maxZ) { $maxZ = $z }
        }
        else {
            $badRun++
            if ($points.Count -gt 16 -and $badRun -ge 16) { break }
            if ($points.Count -eq 0 -and $badRun -ge 10) { break }
        }
    }

    $spanX = $maxX - $minX
    $spanY = $maxY - $minY
    $spanZ = $maxZ - $minZ
    $negativeOneRatio = $(if ($coordinateFields -gt 0) { $negativeOneFields / [double]$coordinateFields } else { 0.0 })
    $uniqueRatio = $(if ($points.Count -gt 0) { $unique.Count / [double]$points.Count } else { 0.0 })
    $score = 0
    if ($points.Count -ge 24 -and $unique.Count -ge 12 -and $negativeOneRatio -le 0.08 -and $uniqueRatio -ge 0.18) {
        $score = $points.Count + $unique.Count
        if ($spanX -gt 1000) { $score += 25 }
        if ($spanY -gt 1000) { $score += 25 }
        if ($spanZ -gt 300) { $score += 10 }
        if (($spanX -lt 500 -and $spanY -lt 500) -or ($spanX -lt 500 -and $spanZ -lt 500) -or ($spanY -lt 500 -and $spanZ -lt 500)) {
            $score = 0
        }
    }

    return [ordered]@{
        ramOffset = $Offset
        runtimeAddress = Format-RuntimeAddress $Offset
        stride = $Stride
        validVertices = $points.Count
        uniqueVertices = $unique.Count
        negativeOneFields = $negativeOneFields
        negativeOneRatio = [Math]::Round($negativeOneRatio, 3)
        zeroFields = $zeroFields
        uniqueRatio = [Math]::Round($uniqueRatio, 3)
        score = $score
        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
        points = $points
    }
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

function Project-Point($Point, [string]$Projection) {
    $a = $Projection.Substring(0, 1)
    $b = $Projection.Substring(1, 1)
    return [ordered]@{
        x = [double](Get-FieldValue $Point $a 0)
        y = [double](Get-FieldValue $Point $b 0)
        z = [double](Get-FieldValue $Point "z" 0)
    }
}

function Add-Edge($Edges, $Points, [int]$A, [int]$B) {
    if ($A -eq $B) { return }
    if ($A -gt $B) { $t = $A; $A = $B; $B = $t }
    $key = "$A-$B"
    if ($Edges.ContainsKey($key)) { return }
    $pa = $Points[$A]; $pb = $Points[$B]
    $Edges[$key] = [ordered]@{ a = $A; b = $B; x1 = $pa.x; y1 = $pa.y; x2 = $pb.x; y2 = $pb.y }
}

function Get-NearestEdges($ProjectedPoints) {
    $points = @($ProjectedPoints)
    $edges = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    $limit = [Math]::Min($points.Count, 500)
    for ($i = 0; $i -lt $limit; $i++) {
        $best = -1; $bestD = [double]::MaxValue
        for ($j = [Math]::Max(0, $i - 24); $j -lt [Math]::Min($limit, $i + 96); $j++) {
            if ($i -eq $j) { continue }
            $dx = [double]$points[$i].x - [double]$points[$j].x
            $dy = [double]$points[$i].y - [double]$points[$j].y
            $d = ($dx * $dx) + ($dy * $dy)
            if ($d -gt 16 -and $d -lt $bestD -and $d -lt 3000000) {
                $best = $j; $bestD = $d
            }
        }
        if ($best -ge 0) { Add-Edge $edges $points $i $best }
    }
    return @($edges.Values)
}

function Choose-Projection($Candidate, $MobySummary) {
    $best = $null
    foreach ($projection in @("xy", "yx", "xz", "zx", "yz", "zy")) {
        $projected = @($Candidate.points | ForEach-Object { Project-Point $_ $projection })
        if ($projected.Count -eq 0) { continue }
        $xs = @($projected | ForEach-Object { $_.x })
        $ys = @($projected | ForEach-Object { $_.y })
        $minX = ($xs | Measure-Object -Minimum).Minimum
        $maxX = ($xs | Measure-Object -Maximum).Maximum
        $minY = ($ys | Measure-Object -Minimum).Minimum
        $maxY = ($ys | Measure-Object -Maximum).Maximum
        $inside = 0
        foreach ($moby in @($MobySummary.points)) {
            if ($moby.x -ge ($minX - 900) -and $moby.x -le ($maxX + 900) -and $moby.y -ge ($minY - 900) -and $moby.y -le ($maxY + 900)) {
                $inside++
            }
        }
        $score = ([double]$Candidate.score * 0.35) + ($inside * 125.0)
        $fit = [ordered]@{
            projection = $projection
            fitScore = [Math]::Round($score, 2)
            mobysInside = $inside
            projectedBounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
            projectedPoints = $projected
        }
        if ($null -eq $best -or $fit.fitScore -gt $best.fitScore) { $best = $fit }
    }
    return $best
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) { throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes." }

$descriptorOffset = Convert-RuntimeAddressToOffset $FocusRuntimeAddress
$pointers = @()
for ($i = 0; $i -lt $MaxPointers; $i++) {
    $value = Get-UInt32LE $ram ($descriptorOffset + ($i * 4))
    if (-not (Test-PsxPointer $value)) { break }
    $pointers += $value
}
if ($pointers.Count -eq 0) { throw "No PS1 pointers found at $FocusRuntimeAddress." }

$mobySummary = Get-MobySummary $ram
$candidates = @()
foreach ($pointer in $pointers) {
    $baseOffset = [int]([uint64]$pointer - $PsxRamBase)
    $best = $null
    foreach ($delta in @(0, 2, 4, 8, 12, 16, 24, 32)) {
        foreach ($stride in @(6, 8, 12, 16)) {
            $probe = Measure-VertexCloud $ram ($baseOffset + $delta) $stride
            if ([int]$probe.score -le 0) { continue }
            $probe.pointer = ("0x{0:X8}" -f $pointer)
            $probe.pointerOffsetDelta = $delta
            if ($null -eq $best -or [int]$probe.score -gt [int]$best.score) { $best = $probe }
        }
    }
    if ($null -ne $best) {
        $fit = Choose-Projection $best $mobySummary
        $best.projection = $fit.projection
        $best.fitScore = $fit.fitScore
        $best.mobysInsideProjection = $fit.mobysInside
        $best.projectedBounds = $fit.projectedBounds
        $best.projectedPoints = $fit.projectedPoints
        $best.edgeSource = "descriptor-nearest-neighbor-preview"
        $best.edgeFaceCount = 0
        $best.edges = Get-NearestEdges $fit.projectedPoints
        $candidates += [pscustomobject]$best
    }
}

$ranked = @($candidates | Sort-Object -Property fitScore, score -Descending)
$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Experimental descriptor-derived geometry overlay from pointer record $FocusRuntimeAddress. Still heuristic; use for visual comparison against mobys."
    descriptorRuntimeAddress = $FocusRuntimeAddress
    descriptorPointers = @($pointers | ForEach-Object { "0x{0:X8}" -f $_ })
    mobys = $mobySummary
    candidates = $ranked
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote descriptor geometry overlay to $resolvedOut"
$ranked | Select-Object runtimeAddress, pointer, pointerOffsetDelta, stride, validVertices, uniqueVertices, score, projection, fitScore, mobysInsideProjection, @{n="Edges";e={@($_.edges).Count}} | Format-Table -AutoSize
