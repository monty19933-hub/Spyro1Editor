param(
    [string]$GeometryPath = ".\stonehill-geometry-overlay.json",
    [string]$OutPath = ".\stonehill-worldspace-slice-overlay.json",
    [int]$BinSize = 512,
    [int]$MinSlicePoints = 8,
    [int]$MaxOutputCandidates = 80,
    [string]$MinRuntimeAddress = "0x80080000"
)

Set-StrictMode -Version 2.0

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Convert-RuntimeAddressToUInt64([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    return [Convert]::ToUInt64($clean, 16)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -ne $prop -and $null -ne $prop.Value) { return $prop.Value }
    return $Default
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    if ($null -eq $value) { return @() }
    return @($value)
}

function Project-Point($Point, [string]$Projection) {
    $a = $Projection.Substring(0, 1)
    $b = $Projection.Substring(1, 1)
    return [ordered]@{
        x = [double](Get-Field $Point $a 0)
        y = [double](Get-Field $Point $b 0)
        z = [double](Get-Field $Point "z" 0)
        rawX = [double](Get-Field $Point "x" 0)
        rawY = [double](Get-Field $Point "y" 0)
        rawZ = [double](Get-Field $Point "z" 0)
    }
}

function Get-Bounds($Points) {
    $xs = @($Points | ForEach-Object { [double](Get-Field $_ "x" 0) })
    $ys = @($Points | ForEach-Object { [double](Get-Field $_ "y" 0) })
    return [ordered]@{
        minX = ($xs | Measure-Object -Minimum).Minimum
        maxX = ($xs | Measure-Object -Maximum).Maximum
        minY = ($ys | Measure-Object -Minimum).Minimum
        maxY = ($ys | Measure-Object -Maximum).Maximum
    }
}

function Get-DistanceSquared($A, $B) {
    $dx = [double](Get-Field $A "x" 0) - [double](Get-Field $B "x" 0)
    $dy = [double](Get-Field $A "y" 0) - [double](Get-Field $B "y" 0)
    return ($dx * $dx) + ($dy * $dy)
}

function Add-Edge([System.Collections.Generic.Dictionary[string, object]]$Edges, $Points, [int]$A, [int]$B, [string]$Source) {
    if ($A -eq $B) { return }
    if ($A -gt $B) {
        $tmp = $A
        $A = $B
        $B = $tmp
    }
    $key = "$A-$B"
    if ($Edges.ContainsKey($key)) { return }
    $pa = $Points[$A]
    $pb = $Points[$B]
    $Edges[$key] = [ordered]@{
        a = $A
        b = $B
        x1 = [Math]::Round([double](Get-Field $pa "x" 0), 3)
        y1 = [Math]::Round([double](Get-Field $pa "y" 0), 3)
        x2 = [Math]::Round([double](Get-Field $pb "x" 0), 3)
        y2 = [Math]::Round([double](Get-Field $pb "y" 0), 3)
        source = $Source
    }
}

function Get-CleanSpatialEdges($Points) {
    $points = @($Points)
    $edges = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    if ($points.Count -lt 2) { return @() }

    $nearestDistances = @()
    for ($i = 0; $i -lt $points.Count; $i++) {
        $best = [double]::MaxValue
        for ($j = 0; $j -lt $points.Count; $j++) {
            if ($i -eq $j) { continue }
            $d = Get-DistanceSquared $points[$i] $points[$j]
            if ($d -gt 16.0 -and $d -lt $best) { $best = $d }
        }
        if ($best -lt [double]::MaxValue) { $nearestDistances += [Math]::Sqrt($best) }
    }

    if ($nearestDistances.Count -eq 0) { return @() }
    $sorted = @($nearestDistances | Sort-Object)
    $median = [double]$sorted[[int][Math]::Floor($sorted.Count / 2)]
    $threshold = [Math]::Max(360.0, [Math]::Min(2400.0, $median * 3.25))
    $thresholdSquared = $threshold * $threshold

    for ($i = 0; $i -lt $points.Count; $i++) {
        $neighbors = @()
        for ($j = 0; $j -lt $points.Count; $j++) {
            if ($i -eq $j) { continue }
            $d = Get-DistanceSquared $points[$i] $points[$j]
            if ($d -gt 16.0 -and $d -le $thresholdSquared) {
                $neighbors += [pscustomobject]@{ index = $j; distance = $d }
            }
        }
        foreach ($neighbor in @($neighbors | Sort-Object distance | Select-Object -First 3)) {
            Add-Edge $edges $points $i ([int]$neighbor.index) "z-slice-clean-neighbor"
        }
    }
    return @($edges.Values)
}

function Count-MobysInside($MobyPoints, $Bounds, [double]$Padding = 128.0) {
    $count = 0
    foreach ($moby in @($MobyPoints)) {
        $x = [double](Get-Field $moby "x" 0)
        $y = [double](Get-Field $moby "y" 0)
        if ($x -ge ([double]$Bounds.minX - $Padding) -and $x -le ([double]$Bounds.maxX + $Padding) -and
            $y -ge ([double]$Bounds.minY - $Padding) -and $y -le ([double]$Bounds.maxY + $Padding)) {
            $count++
        }
    }
    return $count
}

function New-Candidate($SourceCandidate, [int]$SourceIndex, [string]$Name, $SourcePoints, [string]$Projection, $MobyPoints, [string]$Kind, [int]$ZMin, [int]$ZMax) {
    $projected = @($SourcePoints | ForEach-Object { Project-Point $_ $Projection })
    if ($projected.Count -lt 2) { return $null }
    $bounds = Get-Bounds $projected
    $edges = @(Get-CleanSpatialEdges $projected)
    $mobysInside = Count-MobysInside $MobyPoints $bounds
    $kindBonus = $(if ($Kind -eq "zband") { 100000 } else { 0 })
    $densityBonus = [Math]::Min(35000, $projected.Count * 450)
    $score = $kindBonus + ($mobysInside * 450) + $densityBonus + ($edges.Count * 4)
    return [pscustomobject][ordered]@{
        runtimeAddress = ("world-slice:{0} source#{1} {2} {3}" -f $Kind, $SourceIndex, [string](Get-Field $SourceCandidate "runtimeAddress" "unknown"), $Name)
        sourceCandidateIndex = $SourceIndex
        sourceRuntimeAddress = [string](Get-Field $SourceCandidate "runtimeAddress" "unknown")
        encoding = "worldspace-$Kind"
        stride = [int](Get-Field $SourceCandidate "stride" 0)
        validVertices = $projected.Count
        score = [Math]::Round($score, 3)
        projection = $Projection
        fitScore = [Math]::Round($score, 3)
        mobysInsideProjection = $mobysInside
        zMin = $ZMin
        zMax = $ZMax
        projectedBounds = $bounds
        projectedPoints = $projected
        points = $projected
        edges = $edges
        edgeSource = "z-slice-clean-neighbor"
        edgeFaceCount = 0
    }
}

$geometry = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $GeometryPath) | ConvertFrom-Json
$sourceCandidates = @(Get-ArrayField $geometry "candidates")
if ($sourceCandidates.Count -eq 0) { throw "No geometry candidates found in $GeometryPath." }
$minRuntimeValue = Convert-RuntimeAddressToUInt64 $MinRuntimeAddress

$mobyPoints = @()
$mobyObj = Get-Field $geometry "mobys" $null
if ($null -ne $mobyObj) {
    $mobyPoints = @(Get-ArrayField $mobyObj "points")
}

$outputCandidates = @()
for ($ci = 0; $ci -lt $sourceCandidates.Count; $ci++) {
    $source = $sourceCandidates[$ci]
    $sourceAddress = [string](Get-Field $source "runtimeAddress" "0x00000000")
    if ((Convert-RuntimeAddressToUInt64 $sourceAddress) -lt $minRuntimeValue) { continue }
    $sourcePoints = @(Get-ArrayField $source "points")
    if ($sourcePoints.Count -lt $MinSlicePoints) { continue }
    $projection = [string](Get-Field $source "projection" "xy")
    if ($projection.Length -ne 2) { $projection = "xy" }

    $outputCandidates += (New-Candidate $source $ci "all-heights" $sourcePoints $projection $mobyPoints "all" -999999 999999)

    $groups = @{}
    foreach ($point in $sourcePoints) {
        $z = [int][Math]::Round([double](Get-Field $point "z" 0))
        $bin = [int]([Math]::Floor($z / [double]$BinSize) * $BinSize)
        $key = [string]$bin
        if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
        $groups[$key] = @($groups[$key] + $point)
    }

    foreach ($key in $groups.Keys) {
        $points = @($groups[$key])
        if ($points.Count -lt $MinSlicePoints) { continue }
        $zMin = [int]$key
        $zMax = $zMin + $BinSize - 1
        $name = ("z {0}..{1}" -f $zMin, $zMax)
        $outputCandidates += (New-Candidate $source $ci $name $points $projection $mobyPoints "zband" $zMin $zMax)
    }
}

$outputCandidates = @($outputCandidates |
    Where-Object { $null -ne $_ } |
    Sort-Object -Property fitScore, validVertices -Descending |
    Select-Object -First $MaxOutputCandidates)

$result = [ordered]@{
    sourceGeometry = (Resolve-Path -LiteralPath $GeometryPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "World-space diagnostic overlay derived from the broad RAM vertex clouds. Candidates are split by Z bands and use short spatial-neighbor edges to reduce all-height tangles. This is still a visual reverse-engineering aid, not verified mesh topology."
    binSize = $BinSize
    minSlicePoints = $MinSlicePoints
    minRuntimeAddress = $MinRuntimeAddress
    mobys = Get-Field $geometry "mobys" $null
    candidates = $outputCandidates
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote world-space slice overlay to $resolvedOut"
Write-Host "Candidates: $($outputCandidates.Count)"
$outputCandidates | Select-Object -First 24 runtimeAddress, validVertices, projection, mobysInsideProjection, zMin, zMax, @{n="Edges";e={@($_.edges).Count}} | Format-Table -AutoSize
