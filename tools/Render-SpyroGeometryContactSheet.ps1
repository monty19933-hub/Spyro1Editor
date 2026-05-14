param(
    [Parameter(Mandatory = $true)]
    [string]$GeometryPath,

    [string]$RamPath = "",
    [string]$OutPath = ".\stonehill-geometry-contact-sheet.png",
    [int]$MaxCandidates = 32,
    [int]$Columns = 4,
    [int]$ThumbWidth = 420,
    [int]$ThumbHeight = 300,
    [ValidateSet("xy", "xz", "yz")]
    [string]$RuntimePlane = "xy"
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

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

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
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

function Get-RuntimeMobyPoints([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return @() }
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $resolved) { return @() }
    $ram = [System.IO.File]::ReadAllBytes($resolved.Path)
    if ($ram.Length -lt 0x7582C) { return @() }
    $pointer = Get-UInt32LE $ram 0x75828
    $start = [int64]$pointer - [Convert]::ToInt64("80000000", 16)
    if ($start -lt 0 -or ($start + 80) -gt $ram.Length) { return @() }
    $points = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt 512; $i++) {
        $offset = [int]($start + ($i * 80))
        if (($offset + 80) -gt $ram.Length) { break }
        if (-not (Test-PlausibleRuntimeMoby $ram $offset)) { continue }
        $rawX = Get-Int32LE $ram ($offset + 4)
        $rawY = Get-Int32LE $ram ($offset + 8)
        $rawZ = Get-Int32LE $ram ($offset + 12)
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
    return @($points.ToArray())
}

function Convert-ToScreen($X, $Y, $Bounds, [int]$Left, [int]$Top, [int]$Width, [int]$Height) {
    $pad = 18.0
    $spanX = [Math]::Max(1.0, [double]$Bounds.maxX - [double]$Bounds.minX)
    $spanY = [Math]::Max(1.0, [double]$Bounds.maxY - [double]$Bounds.minY)
    $scale = [Math]::Min((($Width - ($pad * 2)) / $spanX), (($Height - ($pad * 2)) / $spanY))
    $drawWidth = $spanX * $scale
    $drawHeight = $spanY * $scale
    $originX = $Left + (($Width - $drawWidth) / 2.0)
    $originY = $Top + (($Height - $drawHeight) / 2.0)
    return [System.Drawing.PointF]::new(
        [float]($originX + (([double]$X - [double]$Bounds.minX) * $scale)),
        [float]($originY + (([double]$Y - [double]$Bounds.minY) * $scale))
    )
}

$geometry = Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $GeometryPath).Path | ConvertFrom-Json
$candidates = @(Get-ArrayField $geometry "candidates" | Select-Object -First $MaxCandidates)
if ($candidates.Count -eq 0) { throw "No candidates found in $GeometryPath." }
$mobyPoints = Get-RuntimeMobyPoints $RamPath

$rows = [int][Math]::Ceiling($candidates.Count / [double]$Columns)
$sheetWidth = $Columns * $ThumbWidth
$sheetHeight = $rows * $ThumbHeight
$bitmap = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(22, 24, 28))

$font = New-Object System.Drawing.Font("Segoe UI", 8)
$smallFont = New-Object System.Drawing.Font("Segoe UI", 7)
$edgePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 64, 224, 255), 1)
$pointBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(190, 64, 224, 255))
$mobyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 255, 94, 88))
$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 255, 255, 255), 1)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 245, 248, 250))

for ($i = 0; $i -lt $candidates.Count; $i++) {
    $candidate = $candidates[$i]
    $col = $i % $Columns
    $row = [int][Math]::Floor($i / $Columns)
    $left = $col * $ThumbWidth
    $top = $row * $ThumbHeight
    $plotLeft = $left + 8
    $plotTop = $top + 32
    $plotWidth = $ThumbWidth - 16
    $plotHeight = $ThumbHeight - 42
    $graphics.DrawRectangle($framePen, $left + 4, $top + 4, $ThumbWidth - 8, $ThumbHeight - 8)

    $points = @(Get-ArrayField $candidate "projectedPoints")
    $bounds = Get-Field $candidate "projectedBounds" $null
    if ($null -eq $bounds) { $bounds = Get-Bounds $points }
    if ($null -eq $bounds) { continue }

    $title = "#{0} {1}" -f ($i + 1), ([string](Get-Field $candidate "runtimeAddress" "candidate"))
    if ($title.Length -gt 82) { $title = $title.Substring(0, 79) + "..." }
    $graphics.DrawString($title, $font, $textBrush, $left + 8, $top + 8)

    foreach ($edge in @(Get-ArrayField $candidate "edges")) {
        $p1 = Convert-ToScreen (Get-Field $edge "x1" 0) (Get-Field $edge "y1" 0) $bounds $plotLeft $plotTop $plotWidth $plotHeight
        $p2 = Convert-ToScreen (Get-Field $edge "x2" 0) (Get-Field $edge "y2" 0) $bounds $plotLeft $plotTop $plotWidth $plotHeight
        $graphics.DrawLine($edgePen, $p1, $p2)
    }

    $pointStep = [Math]::Max(1, [int][Math]::Ceiling($points.Count / 1200.0))
    for ($pointIndex = 0; $pointIndex -lt $points.Count; $pointIndex += $pointStep) {
        $point = $points[$pointIndex]
        $screen = Convert-ToScreen (Get-Field $point "x" 0) (Get-Field $point "y" 0) $bounds $plotLeft $plotTop $plotWidth $plotHeight
        $graphics.FillRectangle($pointBrush, $screen.X - 1, $screen.Y - 1, 2, 2)
    }

    if ($mobyPoints.Count -gt 0) {
        foreach ($point in $mobyPoints) {
            $screen = Convert-ToScreen (Get-Field $point "x" 0) (Get-Field $point "y" 0) $bounds $plotLeft $plotTop $plotWidth $plotHeight
            $graphics.FillEllipse($mobyBrush, $screen.X - 1.8, $screen.Y - 1.8, 3.6, 3.6)
        }
    }

    $boundsText = "X {0}..{1}  Y {2}..{3}" -f [int][Math]::Round([double]$bounds.minX), [int][Math]::Round([double]$bounds.maxX), [int][Math]::Round([double]$bounds.minY), [int][Math]::Round([double]$bounds.maxY)
    $graphics.DrawString($boundsText, $smallFont, $textBrush, $left + 8, $top + $ThumbHeight - 20)
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$bitmap.Save($resolvedOut, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
Write-Host "Wrote geometry contact sheet to $resolvedOut"
