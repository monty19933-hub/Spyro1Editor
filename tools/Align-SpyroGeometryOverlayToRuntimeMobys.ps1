param(
    [Parameter(Mandatory = $true)]
    [string]$GeometryPath,

    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-wad-ground-model-aligned-overlay.json",
    [int]$SourceCandidateIndex = 0,
    [int]$MaxSourceCandidates = 24,
    [double]$LowerPercentile = 0.02,
    [double]$UpperPercentile = 0.98,
    [ValidateSet("xy", "xz", "yz")]
    [string]$RuntimePlane = "xy",
    [switch]$IncludeOrientationVariants
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    return @($value)
}

function Get-Bounds($Points) {
    $minX = [double]::PositiveInfinity
    $maxX = [double]::NegativeInfinity
    $minY = [double]::PositiveInfinity
    $maxY = [double]::NegativeInfinity
    $count = 0
    foreach ($point in $Points) {
        $x = [double](Get-Field $point "x" 0)
        $y = [double](Get-Field $point "y" 0)
        if ($x -lt $minX) { $minX = $x }
        if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
        $count++
    }
    if ($count -eq 0) { return $null }
    return [pscustomobject]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
}

function Get-RuntimeMobyBounds([string]$Path) {
    $ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
    if ($ram.Length -lt 0x7582C) { throw "RAM dump is too small to contain Spyro's live moby pointer." }
    $pointer = Get-UInt32LE $ram 0x75828
    $start = [int64]$pointer - [Convert]::ToInt64("80000000", 16)
    if ($start -lt 0 -or ($start + 80) -gt $ram.Length) {
        throw ("The live moby pointer 0x{0:X8} does not point inside this RAM dump." -f $pointer)
    }

    $points = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt 512; $i++) {
        $offset = [int]($start + ($i * 80))
        if (($offset + 80) -gt $ram.Length) { break }
        $rawX = Get-Int32LE $ram ($offset + 4)
        $rawY = Get-Int32LE $ram ($offset + 8)
        $rawZ = Get-Int32LE $ram ($offset + 12)
        if ($rawX -eq 0 -and $rawY -eq 4096 -and $rawZ -eq 0) { continue }
        if ([Math]::Abs($rawX) -gt 4000000 -or [Math]::Abs($rawY) -gt 4000000 -or [Math]::Abs($rawZ) -gt 4000000) { continue }
        $scaledX = [double]$rawX / 16.0
        $scaledY = [double]$rawY / 16.0
        $scaledZ = [double]$rawZ / 16.0
        switch ($RuntimePlane) {
            "xz" {
                [void]$points.Add([pscustomobject]@{ x = $scaledX; y = $scaledZ; z = $scaledY })
            }
            "yz" {
                [void]$points.Add([pscustomobject]@{ x = $scaledY; y = $scaledZ; z = $scaledX })
            }
            default {
                [void]$points.Add([pscustomobject]@{ x = $scaledX; y = $scaledY; z = $scaledZ })
            }
        }
    }
    if ($points.Count -eq 0) { throw "No plausible runtime moby positions were found in this RAM dump." }
    $bounds = Get-Bounds $points
    $xs = @($points | ForEach-Object { [double]$_.x } | Sort-Object)
    $ys = @($points | ForEach-Object { [double]$_.y } | Sort-Object)
    $lo = [Math]::Max(0.0, [Math]::Min(0.49, $LowerPercentile))
    $hi = [Math]::Max(0.51, [Math]::Min(1.0, $UpperPercentile))
    $loIndex = [Math]::Max(0, [Math]::Min($xs.Count - 1, [int][Math]::Floor(($xs.Count - 1) * $lo)))
    $hiIndex = [Math]::Max(0, [Math]::Min($xs.Count - 1, [int][Math]::Floor(($xs.Count - 1) * $hi)))
    $robustBounds = [pscustomobject]@{
        minX = [double]$xs[$loIndex]
        maxX = [double]$xs[$hiIndex]
        minY = [double]$ys[$loIndex]
        maxY = [double]$ys[$hiIndex]
    }
    return [pscustomobject]@{ pointer = ("0x{0:X8}" -f $pointer); count = $points.Count; bounds = $robustBounds; fullBounds = $bounds }
}

function Convert-Point($Point, $SourceBounds, $TargetBounds, [string]$Mode, [bool]$FlipY) {
    $sourceSpanX = [Math]::Max(1.0, [double]$SourceBounds.maxX - [double]$SourceBounds.minX)
    $sourceSpanY = [Math]::Max(1.0, [double]$SourceBounds.maxY - [double]$SourceBounds.minY)
    $targetSpanX = [Math]::Max(1.0, [double]$TargetBounds.maxX - [double]$TargetBounds.minX)
    $targetSpanY = [Math]::Max(1.0, [double]$TargetBounds.maxY - [double]$TargetBounds.minY)
    $pointX = [double](Get-Field $Point "x" 0)
    $pointY = [double](Get-Field $Point "y" 0)
    $pointZ = [double](Get-Field $Point "z" 0)
    if ($Mode -eq "uniform-center") {
        $scale = [Math]::Min(($targetSpanX / $sourceSpanX), ($targetSpanY / $sourceSpanY))
        $scaledWidth = $sourceSpanX * $scale
        $scaledHeight = $sourceSpanY * $scale
        $originX = [double]$TargetBounds.minX + (($targetSpanX - $scaledWidth) / 2.0)
        $originY = [double]$TargetBounds.minY + (($targetSpanY - $scaledHeight) / 2.0)
        $sourceY = if ($FlipY) { [double]$SourceBounds.maxY - $pointY } else { $pointY - [double]$SourceBounds.minY }
        return [pscustomobject]@{
            x = (($pointX - [double]$SourceBounds.minX) * $scale) + $originX
            y = ($sourceY * $scale) + $originY
            z = $pointZ
        }
    }

    $scaleX = $targetSpanX / $sourceSpanX
    $scaleY = $targetSpanY / $sourceSpanY
    $sourceY2 = if ($FlipY) { [double]$SourceBounds.maxY - $pointY } else { $pointY - [double]$SourceBounds.minY }
    return [pscustomobject]@{
        x = (($pointX - [double]$SourceBounds.minX) * $scaleX) + [double]$TargetBounds.minX
        y = ($sourceY2 * $scaleY) + [double]$TargetBounds.minY
        z = $pointZ
    }
}

function Convert-Edge($Edge, $SourceBounds, $TargetBounds, [string]$Mode, [bool]$FlipY) {
    $p1 = Convert-Point ([pscustomobject]@{ x = [double](Get-Field $Edge "x1" 0); y = [double](Get-Field $Edge "y1" 0); z = 0 }) $SourceBounds $TargetBounds $Mode $FlipY
    $p2 = Convert-Point ([pscustomobject]@{ x = [double](Get-Field $Edge "x2" 0); y = [double](Get-Field $Edge "y2" 0); z = 0 }) $SourceBounds $TargetBounds $Mode $FlipY
    return [pscustomobject]@{ x1 = $p1.x; y1 = $p1.y; x2 = $p2.x; y2 = $p2.y }
}

function Convert-OrientationPoint($Point, $Bounds, [string]$Orientation) {
    $x = [double](Get-Field $Point "x" 0)
    $y = [double](Get-Field $Point "y" 0)
    $z = [double](Get-Field $Point "z" 0)
    $cx = ([double]$Bounds.minX + [double]$Bounds.maxX) / 2.0
    $cy = ([double]$Bounds.minY + [double]$Bounds.maxY) / 2.0
    $dx = $x - $cx
    $dy = $y - $cy
    switch ($Orientation) {
        "rot90" { return [pscustomobject]@{ x = ($cx - $dy); y = ($cy + $dx); z = $z } }
        "rot180" { return [pscustomobject]@{ x = ($cx - $dx); y = ($cy - $dy); z = $z } }
        "rot270" { return [pscustomobject]@{ x = ($cx + $dy); y = ($cy - $dx); z = $z } }
        "mirrorX" { return [pscustomobject]@{ x = ($cx - $dx); y = $y; z = $z } }
        "mirrorY" { return [pscustomobject]@{ x = $x; y = ($cy - $dy); z = $z } }
        "swapXY" { return [pscustomobject]@{ x = ($cx + $dy); y = ($cy + $dx); z = $z } }
        default { return [pscustomobject]@{ x = $x; y = $y; z = $z } }
    }
}

function Convert-OrientationEdge($Edge, $Bounds, [string]$Orientation) {
    $p1 = Convert-OrientationPoint ([pscustomobject]@{ x = [double](Get-Field $Edge "x1" 0); y = [double](Get-Field $Edge "y1" 0); z = 0 }) $Bounds $Orientation
    $p2 = Convert-OrientationPoint ([pscustomobject]@{ x = [double](Get-Field $Edge "x2" 0); y = [double](Get-Field $Edge "y2" 0); z = 0 }) $Bounds $Orientation
    return [pscustomobject]@{ x1 = $p1.x; y1 = $p1.y; x2 = $p2.x; y2 = $p2.y }
}

$geometry = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $GeometryPath).Path | ConvertFrom-Json
$sourceCandidates = @(Get-ArrayField $geometry "candidates" | Select-Object -Skip $SourceCandidateIndex -First $MaxSourceCandidates)
if ($sourceCandidates.Count -eq 0) { throw "No geometry candidates found in $GeometryPath." }

$runtime = Get-RuntimeMobyBounds $RamPath
$alignedCandidates = New-Object System.Collections.Generic.List[object]

foreach ($candidate in $sourceCandidates) {
    $points = @(Get-ArrayField $candidate "projectedPoints")
    if ($points.Count -eq 0) { continue }
    $sourceBounds = Get-Field $candidate "projectedBounds" $null
    if ($null -eq $sourceBounds) { $sourceBounds = Get-Bounds $points }
    if ($null -eq $sourceBounds) { continue }
    $edges = @(Get-ArrayField $candidate "edges")
    $orientations = if ($IncludeOrientationVariants) { @("identity", "rot90", "rot180", "rot270", "mirrorX", "mirrorY", "swapXY") } else { @("identity") }
    foreach ($orientation in $orientations) {
        $orientedPoints = @($points | ForEach-Object { Convert-OrientationPoint $_ $sourceBounds $orientation })
        $orientedEdges = @($edges | ForEach-Object { Convert-OrientationEdge $_ $sourceBounds $orientation })
        $orientedBounds = Get-Bounds $orientedPoints
        foreach ($mode in @("stretch-bounds", "uniform-center")) {
            foreach ($flip in @($false, $true)) {
                $newPoints = @($orientedPoints | ForEach-Object { Convert-Point $_ $orientedBounds $runtime.bounds $mode $flip })
                $newEdges = @($orientedEdges | ForEach-Object { Convert-Edge $_ $orientedBounds $runtime.bounds $mode $flip })
                $newBounds = Get-Bounds $newPoints
                [void]$alignedCandidates.Add([ordered]@{
                    runtimeAddress = ("aligned-to-runtime {0} orient={1} flipY={2}: {3}" -f $mode, $orientation, $flip, [string](Get-Field $candidate "runtimeAddress" "candidate"))
                    encoding = [string](Get-Field $candidate "encoding" "aligned-geometry")
                    sourceProjection = [string](Get-Field $candidate "projection" "unknown")
                    projection = "runtime-$RuntimePlane"
                    fitScore = [double](Get-Field $candidate "fitScore" 0)
                    mobysInsideProjection = [int]$runtime.count
                    edgeSource = ("runtime-bounds-aligned " + [string](Get-Field $candidate "edgeSource" "unknown"))
                    validVertices = [int](Get-Field $candidate "validVertices" $points.Count)
                    projectedBounds = $newBounds
                    projectedPoints = $newPoints
                    edges = $newEdges
                })
            }
        }
    }
}

$output = [ordered]@{
    sourceGeometry = (Resolve-Path -LiteralPath $GeometryPath).Path
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Geometry overlay candidates transformed into the runtime moby XY bounds. This is a visual alignment aid only; it does not prove the WAD model coordinate transform."
    runtimeMobyPointer = $runtime.pointer
    runtimeMobyCount = $runtime.count
    runtimeMobyBounds = $runtime.bounds
    runtimeMobyFullBounds = $runtime.fullBounds
    runtimeMobyBoundPercentiles = [ordered]@{ lower = $LowerPercentile; upper = $UpperPercentile }
    runtimePlane = $RuntimePlane
    sourceCandidateIndex = $SourceCandidateIndex
    includeOrientationVariants = [bool]$IncludeOrientationVariants
    candidates = @($alignedCandidates.ToArray())
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$output | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
Write-Host "Wrote runtime-aligned geometry overlay to $resolvedOut"
Write-Host "Runtime mobys: $($runtime.count), candidates: $($alignedCandidates.Count)"
