param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string[]]$ChunkRuntimeAddress = @("0x800CFC00", "0x800D36B4", "0x800E4EF0", "0x80114EE4", "0x80118EE4"),
    [string]$OutPath = ".\stonehill-descriptor-chunk-layouts.json",
    [int]$MaxRecords = 384,
    [int]$MaxLayoutsPerChunk = 24
)

Set-StrictMode -Version 2.0

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Convert-RuntimeAddressToOffset([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    if ($clean -match '^-?\d+$') {
        $signed = [Int64]::Parse($clean, [System.Globalization.CultureInfo]::InvariantCulture)
        if ($signed -lt 0) { $value = [uint64]($signed + 0x100000000L) }
        else { $value = [uint64]$signed }
    }
    else {
        $value = [Convert]::ToUInt64($clean, 16)
    }
    if ($value -lt $script:PsxRamBase -or $value -ge ($script:PsxRamBase + 0x200000)) {
        throw "$Address is not inside PS1 main RAM."
    }
    return [int]($value - $script:PsxRamBase)
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

function Get-MobyBounds([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) { return $null }
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
        $points += [ordered]@{ x = $x; y = $y }
    }
    if ($points.Count -eq 0) { return $null }
    $xs = @($points | ForEach-Object { $_.x })
    $ys = @($points | ForEach-Object { $_.y })
    return [ordered]@{
        count = $points.Count
        points = $points
        minX = ($xs | Measure-Object -Minimum).Minimum
        maxX = ($xs | Measure-Object -Maximum).Maximum
        minY = ($ys | Measure-Object -Minimum).Minimum
        maxY = ($ys | Measure-Object -Maximum).Maximum
    }
}

function Test-Coord([int]$Value) {
    return ($Value -ge -3000 -and $Value -le 16000)
}

function Count-MobysInside($MobyBounds, [double]$MinX, [double]$MaxX, [double]$MinY, [double]$MaxY) {
    if ($null -eq $MobyBounds) { return 0 }
    $count = 0
    foreach ($moby in @($MobyBounds.points)) {
        if ([double]$moby.x -ge ($MinX - 128) -and [double]$moby.x -le ($MaxX + 128) -and
            [double]$moby.y -ge ($MinY - 128) -and [double]$moby.y -le ($MaxY + 128)) {
            $count++
        }
    }
    return $count
}

function Test-MostlyCodeOrFill([byte[]]$Ram, [int]$Offset, [int]$Length) {
    $end = [Math]::Min($Ram.Length, $Offset + $Length)
    $ff = 0
    $zero = 0
    $same01 = 0
    for ($i = $Offset; $i -lt $end; $i++) {
        if ($Ram[$i] -eq 0xFF) { $ff++ }
        if ($Ram[$i] -eq 0x00) { $zero++ }
        if ($Ram[$i] -eq 0x01 -or $Ram[$i] -eq 0x02 -or $Ram[$i] -eq 0x1E) { $same01++ }
    }
    $count = [Math]::Max(1, $end - $Offset)
    return (($ff / [double]$count) -gt 0.55 -or ($zero / [double]$count) -gt 0.75 -or ($same01 / [double]$count) -gt 0.75)
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) { throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes." }
$mobyBounds = Get-MobyBounds $ram

$requestedChunks = @()
foreach ($chunkArg in $ChunkRuntimeAddress) {
    foreach ($part in ([string]$chunkArg).Split(',')) {
        $trimmed = $part.Trim()
        if ($trimmed.Length -gt 0) { $requestedChunks += $trimmed }
    }
}

$layouts = @()
$strides = @(6, 8, 12, 16)
foreach ($address in $requestedChunks) {
    $chunkOffset = Convert-RuntimeAddressToOffset $address
    $chunkLayouts = @()
    foreach ($stride in $strides) {
        $wordOffsets = @(0..([Math]::Floor(($stride - 2) / 2)) | ForEach-Object { $_ * 2 })
        for ($xi = 0; $xi -lt $wordOffsets.Count; $xi++) {
            for ($yi = 0; $yi -lt $wordOffsets.Count; $yi++) {
                if ($yi -eq $xi) { continue }
                for ($zi = 0; $zi -lt $wordOffsets.Count; $zi++) {
                    if ($zi -eq $xi -or $zi -eq $yi) { continue }
                    $xOff = $wordOffsets[$xi]
                    $yOff = $wordOffsets[$yi]
                    $zOff = $wordOffsets[$zi]
                    $points = @()
                    $badRun = 0
                    $unique = New-Object 'System.Collections.Generic.HashSet[string]'
                    $minX = 999999; $maxX = -999999
                    $minY = 999999; $maxY = -999999
                    $minZ = 999999; $maxZ = -999999
                    for ($i = 0; $i -lt $MaxRecords; $i++) {
                        $pos = $chunkOffset + ($i * $stride)
                        if (($pos + $stride) -gt $ram.Length) { break }
                        $x = Get-Int16LE $ram ($pos + $xOff)
                        $y = Get-Int16LE $ram ($pos + $yOff)
                        $z = Get-Int16LE $ram ($pos + $zOff)
                        if (Test-Coord $x -and Test-Coord $y -and Test-Coord $z -and -not ($x -eq -1 -and $y -eq -1 -and $z -eq -1)) {
                            $badRun = 0
                            $points += [ordered]@{ x = $x; y = $y; z = $z }
                            [void]$unique.Add("$x,$y,$z")
                            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
                            if ($z -lt $minZ) { $minZ = $z }; if ($z -gt $maxZ) { $maxZ = $z }
                        }
                        else {
                            $badRun++
                            if ($points.Count -eq 0 -and $badRun -gt 12) { break }
                            if ($points.Count -gt 16 -and $badRun -gt 16) { break }
                        }
                    }
                    if ($points.Count -lt 24) { continue }
                    if (Test-MostlyCodeOrFill $ram $chunkOffset ([Math]::Min(2048, $points.Count * $stride))) { continue }
                    $spanX = $maxX - $minX
                    $spanY = $maxY - $minY
                    $spanZ = $maxZ - $minZ
                    if ($spanX -lt 512 -or $spanY -lt 512) { continue }
                    $mobysInside = Count-MobysInside $mobyBounds $minX $maxX $minY $maxY
                    $score = ($mobysInside * 1000) + ($points.Count * 4) + [Math]::Min(2000, ($spanX + $spanY) / 16)
                    $chunkLayouts += [pscustomobject][ordered]@{
                        runtimeAddress = $address
                        stride = $stride
                        xOffset = $xOff
                        yOffset = $yOff
                        zOffset = $zOff
                        points = $points.Count
                        uniquePoints = $unique.Count
                        mobysInside = $mobysInside
                        score = [Math]::Round($score, 2)
                        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
                    }
                }
            }
        }
    }
    $layouts += [ordered]@{
        runtimeAddress = $address
        ramOffset = ("0x{0:X}" -f $chunkOffset)
        layouts = @($chunkLayouts | Sort-Object -Property score, points -Descending | Select-Object -First $MaxLayoutsPerChunk)
    }
}

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Brute-force scan for plausible int16 coordinate layouts inside descriptor chunks. Scores are heuristic and include overlap with runtime moby bounds."
    mobyBounds = $mobyBounds
    chunks = $layouts
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote descriptor chunk layout report to $resolvedOut"
foreach ($chunk in $layouts) {
    Write-Host "Chunk $($chunk.runtimeAddress)"
    $chunk.layouts | Select-Object -First 8 runtimeAddress, stride, xOffset, yOffset, zOffset, points, mobysInside, score | Format-Table -AutoSize
}
