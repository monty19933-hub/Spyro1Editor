param(
    [string]$VramPath = ".\duckstation-state-gpu-vram-fresh-stonehill.bin",
    [ValidateSet("Spyro", "CrystalDragon")]
    [string]$Mode = "Spyro",
    [int]$Top = 80,
    [ValidateSet(16, 256)]
    [int]$PaletteEntries = 16,
    [int]$PaletteStepBytes = 32,
    [int]$MinModeColorCount = -1,
    [switch]$IncludeOffModeCandidates,
    [switch]$SkipFramebufferArea,
    [string]$OutJsonPath = ".\spyro-vram-palette-candidates.json",
    [string]$OutImagePath = ".\spyro-vram-palette-candidates.png"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 1) -ge $Bytes.Length) { return [uint16]0 }
    return [uint16]($Bytes[$Offset] -bor ($Bytes[$Offset + 1] -shl 8))
}

function Convert-Psx555ToColor([uint16]$Value) {
    $r5 = $Value -band 0x1F
    $g5 = ($Value -shr 5) -band 0x1F
    $b5 = ($Value -shr 10) -band 0x1F
    $r = [int](($r5 * 255 + 15) / 31)
    $g = [int](($g5 * 255 + 15) / 31)
    $b = [int](($b5 * 255 + 15) / 31)
    return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

function Test-HueRange([double]$Hue, [double]$Min, [double]$Max) {
    if ($Min -le $Max) { return $Hue -ge $Min -and $Hue -le $Max }
    return $Hue -ge $Min -or $Hue -le $Max
}

function Get-ColorRole([System.Drawing.Color]$Color) {
    if ($Color.R -eq 0 -and $Color.G -eq 0 -and $Color.B -eq 0) { return "empty" }
    $h = [double]$Color.GetHue()
    $s = [double]$Color.GetSaturation()
    $v = [double]$Color.GetBrightness()
    if ($s -lt 0.18 -or $v -lt 0.10) { return "neutral" }
    if ((Test-HueRange $h 250 320) -and $s -ge 0.22 -and $v -ge 0.14) { return "purple" }
    if ((Test-HueRange $h 325 25) -and $s -ge 0.35 -and $v -ge 0.20) { return "red" }
    if ((Test-HueRange $h 18 55) -and $s -ge 0.35 -and $v -ge 0.25) { return "orange" }
    if ((Test-HueRange $h 45 75) -and $s -ge 0.30 -and $v -ge 0.35) { return "yellow" }
    if ((Test-HueRange $h 115 175) -and $s -ge 0.25 -and $v -ge 0.22) { return "green" }
    if ((Test-HueRange $h 165 205) -and $s -ge 0.22 -and $v -ge 0.25) { return "cyan" }
    if ((Test-HueRange $h 200 245) -and $s -ge 0.25 -and $v -ge 0.20) { return "blue" }
    return "other"
}

function Score-Palette([byte[]]$Vram, [int]$Offset, [string]$Mode, [int]$Entries) {
    $counts = @{
        empty = 0; neutral = 0; purple = 0; red = 0; orange = 0; yellow = 0;
        green = 0; cyan = 0; blue = 0; other = 0
    }
    $unique = New-Object 'System.Collections.Generic.HashSet[int]'
    $colors = New-Object 'System.Collections.Generic.List[System.Drawing.Color]'
    for ($i = 0; $i -lt $Entries; $i++) {
        $value = Get-UInt16LE $Vram ($Offset + ($i * 2))
        [void]$unique.Add([int]$value)
        $color = Convert-Psx555ToColor $value
        [void]$colors.Add($color)
        $role = Get-ColorRole $color
        $counts[$role] = [int]$counts[$role] + 1
    }

    $nonEmpty = $Entries - [int]$counts.empty
    $warm = [int]$counts.orange + [int]$counts.yellow + [int]$counts.red
    $coolCrystal = [int]$counts.cyan + [int]$counts.green + [int]$counts.blue
    $modeColorCount = 0
    if ($Mode -eq "Spyro") {
        $modeColorCount = [int]$counts.purple
        $score = ([int]$counts.purple * 18) + ($warm * 5) + ([Math]::Min([int]$unique.Count, 32))
        if ([int]$counts.purple -lt 2) { $score -= 80 }
        if ($warm -lt 2) { $score -= 30 }
    }
    else {
        $modeColorCount = $coolCrystal
        $score = ($coolCrystal * 12) + ([int]$counts.purple * 2) + ([Math]::Min([int]$unique.Count, 32))
        if ($coolCrystal -lt 3) { $score -= 70 }
    }
    if ($nonEmpty -eq $Entries -and $unique.Count -gt ($Entries * 0.85)) {
        # Fully busy strips in the visible framebuffer often look like palettes
        # numerically. Prefer compact CLUT-like rows with repeated/shaded colors.
        $score -= 20
    }

    $wordIndex = [int]($Offset / 2)
    return [pscustomobject]@{
        paletteIndex = [int]($Offset / 32)
        byteOffset = ("0x{0:X6}" -f $Offset)
        vramX = [int]($wordIndex % 1024)
        vramY = [int][Math]::Floor($wordIndex / 1024.0)
        entries = [int]$Entries
        score = [int]$score
        modeColorCount = [int]$modeColorCount
        uniqueColors = [int]$unique.Count
        nonEmpty = [int]$nonEmpty
        purple = [int]$counts.purple
        red = [int]$counts.red
        orange = [int]$counts.orange
        yellow = [int]$counts.yellow
        green = [int]$counts.green
        cyan = [int]$counts.cyan
        blue = [int]$counts.blue
        colors = $colors.ToArray()
    }
}

function Render-Candidates($Rows, [string]$Path, [string]$Mode) {
    $rowHeight = 18
    $labelWidth = 236
    $stripWidth = 512
    $width = $labelWidth + $stripWidth + 12
    $height = [Math]::Max(48, ($Rows.Count * $rowHeight) + 30)
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $font = New-Object System.Drawing.Font "Consolas", 8
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(24, 27, 31))
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        try { $graphics.DrawString("Mode: $Mode  top candidates", $font, $brush, 4, 4) }
        finally { $brush.Dispose() }
        for ($r = 0; $r -lt $Rows.Count; $r++) {
            $row = $Rows[$r]
            $y = 24 + ($r * $rowHeight)
            $labelBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(225, 230, 235))
            try {
                $label = "#{0} pal {1} {2} xy {3},{4} s {5}" -f $r, $row.paletteIndex, $row.byteOffset, $row.vramX, $row.vramY, $row.score
                $graphics.DrawString($label, $font, $labelBrush, 4, $y + 2)
            }
            finally { $labelBrush.Dispose() }
            $entryCount = [Math]::Max(1, [int]$row.entries)
            $cellWidth = [Math]::Max(2, [int][Math]::Floor($stripWidth / $entryCount))
            for ($i = 0; $i -lt $entryCount; $i++) {
                $b = New-Object System.Drawing.SolidBrush $row.colors[$i]
                try { $graphics.FillRectangle($b, $labelWidth + ($i * $cellWidth), $y, $cellWidth, $rowHeight - 2) }
                finally { $b.Dispose() }
            }
        }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $font.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$resolvedVram = Resolve-WorkspacePath $VramPath
$resolvedJson = Resolve-WorkspacePath $OutJsonPath
$resolvedImage = Resolve-WorkspacePath $OutImagePath
$vram = [System.IO.File]::ReadAllBytes($resolvedVram)
if ($vram.Length -lt (1024 * 512 * 2)) { throw "VRAM blob must be at least 1,048,576 bytes." }

$rows = New-Object System.Collections.Generic.List[object]
$step = [Math]::Max(32, $PaletteStepBytes)
$step = [int]([Math]::Floor($step / 32.0) * 32)
if ($PaletteEntries -eq 256) {
    $step = [Math]::Max(512, $step)
}
$bytesPerPalette = $PaletteEntries * 2
$modeColorThreshold = $MinModeColorCount
if ($modeColorThreshold -lt 0) {
    $modeColorThreshold = $(if ($Mode -eq "Spyro") { 1 } else { 2 })
}
for ($offset = 0; ($offset + $bytesPerPalette) -le (1024 * 512 * 2); $offset += $step) {
    $wordIndex = [int]($offset / 2)
    $vramX = [int]($wordIndex % 1024)
    $vramY = [int][Math]::Floor($wordIndex / 1024.0)
    if ($SkipFramebufferArea -and $vramX -lt 512 -and $vramY -lt 480) { continue }

    $row = Score-Palette $vram $offset $Mode $PaletteEntries
    if ($row.nonEmpty -lt 8 -or $row.uniqueColors -lt 4) { continue }
    if ((-not $IncludeOffModeCandidates) -and $row.modeColorCount -lt $modeColorThreshold) { continue }
    [void]$rows.Add($row)
}

$ranked = @($rows.ToArray() | Sort-Object -Property score, uniqueColors -Descending | Select-Object -First $Top)
Render-Candidates $ranked $resolvedImage $Mode

$jsonRows = @($ranked | ForEach-Object {
    [ordered]@{
        paletteIndex = $_.paletteIndex
        byteOffset = $_.byteOffset
        vramX = $_.vramX
        vramY = $_.vramY
        entries = $_.entries
        score = $_.score
        modeColorCount = $_.modeColorCount
        uniqueColors = $_.uniqueColors
        nonEmpty = $_.nonEmpty
        purple = $_.purple
        red = $_.red
        orange = $_.orange
        yellow = $_.yellow
        green = $_.green
        cyan = $_.cyan
        blue = $_.blue
    }
})

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    mode = $Mode
    paletteEntries = $PaletteEntries
    paletteStepBytes = $step
    minModeColorCount = $modeColorThreshold
    includeOffModeCandidates = [bool]$IncludeOffModeCandidates
    skipFramebufferArea = [bool]$SkipFramebufferArea
    vramPath = $resolvedVram
    outImagePath = $resolvedImage
    candidateCount = $rows.Count
    rows = $jsonRows
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8
Write-Host "Wrote palette candidate JSON to $resolvedJson"
Write-Host "Wrote palette candidate sheet to $resolvedImage"
Write-Host "Candidates scanned: $($rows.Count); rendered: $($ranked.Count)"
