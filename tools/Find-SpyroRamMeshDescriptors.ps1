param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-mesh-descriptors.json",
    [int]$MaxResults = 40,
    [string]$FocusRuntimeAddress = ""
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

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge $script:PsxRamBase -and $address -lt ($script:PsxRamBase + 0x200000))
}

function Convert-PointerToOffset([uint32]$Value) {
    return [int]([uint64]$Value - $script:PsxRamBase)
}

function Test-PlausibleVertex([int]$X, [int]$Y, [int]$Z) {
    if ($X -lt -4000 -or $X -gt 16000) { return $false }
    if ($Y -lt -4000 -or $Y -gt 16000) { return $false }
    if ($Z -lt -6000 -or $Z -gt 10000) { return $false }
    if ([Math]::Abs($X) -lt 8 -and [Math]::Abs($Y) -lt 8 -and [Math]::Abs($Z) -lt 8) { return $false }
    return $true
}

function Measure-VertexCloud([byte[]]$Ram, [int]$Offset, [int]$Stride, [int]$MaxCount = 384) {
    $valid = 0
    $badRun = 0
    $minX = 999999
    $maxX = -999999
    $minY = 999999
    $maxY = -999999
    $minZ = 999999
    $maxZ = -999999
    $samples = @()

    for ($i = 0; $i -lt $MaxCount; $i++) {
        $pos = $Offset + ($i * $Stride)
        if (($pos + 6) -gt $Ram.Length) { break }
        $x = Get-Int16LE $Ram $pos
        $y = Get-Int16LE $Ram ($pos + 2)
        $z = Get-Int16LE $Ram ($pos + 4)
        if (Test-PlausibleVertex $x $y $z) {
            $valid++
            $badRun = 0
            if ($samples.Count -lt 8) { $samples += [ordered]@{ x = $x; y = $y; z = $z } }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            if ($z -lt $minZ) { $minZ = $z }
            if ($z -gt $maxZ) { $maxZ = $z }
        }
        else {
            $badRun++
            if ($valid -gt 16 -and $badRun -ge 16) { break }
            if ($valid -eq 0 -and $badRun -ge 8) { break }
        }
    }

    $spanX = $maxX - $minX
    $spanY = $maxY - $minY
    $spanZ = $maxZ - $minZ
    $score = 0
    if ($valid -ge 24) {
        $score = $valid
        if ($spanX -gt 1000) { $score += 15 }
        if ($spanY -gt 1000) { $score += 15 }
        if ($spanZ -gt 300) { $score += 8 }
    }

    return [ordered]@{
        stride = $Stride
        validVertices = $valid
        score = $score
        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
        samples = $samples
    }
}

function Measure-HalfwordProfile([byte[]]$Ram, [int]$Offset, [int]$Count = 128) {
    $negativeOne = 0
    $smallPositive = 0
    $largePositive = 0
    $zero = 0
    $samples = @()
    $limit = [Math]::Min($Count, [Math]::Floor(($Ram.Length - $Offset) / 2))
    for ($i = 0; $i -lt $limit; $i++) {
        $value = Get-Int16LE $Ram ($Offset + ($i * 2))
        if ($samples.Count -lt 20) { $samples += $value }
        if ($value -eq -1) { $negativeOne++ }
        elseif ($value -eq 0) { $zero++ }
        elseif ($value -gt 0 -and $value -lt 2048) { $smallPositive++ }
        elseif ($value -ge 2048) { $largePositive++ }
    }
    $score = $smallPositive + ($negativeOne * 0.5)
    return [ordered]@{
        score = [Math]::Round($score, 2)
        negativeOne = $negativeOne
        zero = $zero
        smallPositive = $smallPositive
        largePositive = $largePositive
        samples = $samples
    }
}

function Measure-PointerTarget([byte[]]$Ram, [uint32]$Pointer) {
    $cacheKey = "0x{0:X8}" -f $Pointer
    if ($script:TargetProfileCache.ContainsKey($cacheKey)) {
        return $script:TargetProfileCache[$cacheKey]
    }

    $offset = Convert-PointerToOffset $Pointer
    $vertexScores = @()
    foreach ($delta in @(0, 2, 4, 8, 12, 16, 24, 32)) {
        foreach ($stride in @(6, 8, 12, 16)) {
            $probe = $offset + $delta
            if ($probe -lt 0 -or $probe -ge $Ram.Length) { continue }
            $score = Measure-VertexCloud $Ram $probe $stride
            if ([int]$score.score -gt 0) {
                $score.offsetDelta = $delta
                $score.ramOffset = ("0x{0:X}" -f $probe)
                $score.runtimeAddress = Format-RuntimeAddress $probe
                $vertexScores += [pscustomobject]$score
            }
        }
    }
    $bestVertex = $null
    if ($vertexScores.Count -gt 0) {
        $bestVertex = @($vertexScores | Sort-Object -Property score, validVertices -Descending | Select-Object -First 1)[0]
    }

    $profile = Measure-HalfwordProfile $Ram $offset
    $kind = "unknown"
    if ($null -ne $bestVertex -and [int]$bestVertex.score -ge 40) {
        $kind = "vertex-cloud"
    }
    elseif ([double]$profile.score -ge 50 -and [int]$profile.smallPositive -ge 35) {
        $kind = "halfword-table"
    }
    elseif ([int]$profile.negativeOne -gt 40) {
        $kind = "sentinel-table"
    }

    $result = [ordered]@{
        pointer = ("0x{0:X8}" -f $Pointer)
        ramOffset = ("0x{0:X}" -f $offset)
        runtimeAddress = Format-RuntimeAddress $offset
        kind = $kind
        bestVertexCloud = $bestVertex
        halfwordProfile = $profile
    }
    $script:TargetProfileCache[$cacheKey] = $result
    return $result
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$script:TargetProfileCache = @{}
$records = @()
$scanOffsets = @()
if (-not [string]::IsNullOrWhiteSpace($FocusRuntimeAddress)) {
    $clean = $FocusRuntimeAddress.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    $address = [Convert]::ToUInt64($clean, 16)
    if ($address -lt $PsxRamBase -or $address -ge ($PsxRamBase + 0x200000)) {
        throw "FocusRuntimeAddress must be inside PS1 main RAM: 0x80000000..0x801FFFFF."
    }
    $scanOffsets = @([int]($address - $PsxRamBase))
}
else {
    $scanOffsets = 0..([Math]::Floor(($ram.Length - 16) / 4)) | ForEach-Object { [int]($_ * 4) }
}

foreach ($offset in $scanOffsets) {
    $pointers = @()
    for ($field = 0; $field -lt 10; $field++) {
        $value = Get-UInt32LE $ram ($offset + ($field * 4))
        if (Test-PsxPointer $value) {
            $pointers += $value
        }
        else {
            break
        }
    }

    if ($pointers.Count -lt 4) { continue }
    $distinct = @($pointers | Select-Object -Unique)
    if ($distinct.Count -lt 4) { continue }

    $ascendingPairs = 0
    for ($i = 1; $i -lt $pointers.Count; $i++) {
        if ([uint64]$pointers[$i] -ge [uint64]$pointers[$i - 1]) { $ascendingPairs++ }
    }

    $targets = @($pointers | ForEach-Object { Measure-PointerTarget $ram $_ })
    $vertexTargets = @($targets | Where-Object { $_.kind -eq "vertex-cloud" }).Count
    $halfwordTargets = @($targets | Where-Object { $_.kind -eq "halfword-table" }).Count
    $sentinelTargets = @($targets | Where-Object { $_.kind -eq "sentinel-table" }).Count
    $score = ($pointers.Count * 20) + ($vertexTargets * 80) + ($halfwordTargets * 35) + ($sentinelTargets * 20) + ($ascendingPairs * 6)
    if ($score -lt 120) { continue }

    $records += [pscustomobject][ordered]@{
        ramOffset = ("0x{0:X}" -f $offset)
        runtimeAddress = Format-RuntimeAddress $offset
        pointerCount = $pointers.Count
        ascendingPairs = $ascendingPairs
        score = $score
        vertexTargets = $vertexTargets
        halfwordTargets = $halfwordTargets
        sentinelTargets = $sentinelTargets
        pointers = @($pointers | ForEach-Object { "0x{0:X8}" -f $_ })
        targets = $targets
    }

    if ($pointers.Count -gt 4) {
        $offset += (($pointers.Count - 1) * 4)
    }
}

$ranked = @($records | Sort-Object -Property score, pointerCount -Descending | Select-Object -First $MaxResults)
$output = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Heuristic RAM mesh/sector descriptor candidates: records made of consecutive PS1 pointers, with target chunk profiles."
    descriptors = $ranked
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$output | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote mesh descriptor report to $resolvedOut"
$ranked | Select-Object runtimeAddress, pointerCount, score, vertexTargets, halfwordTargets, sentinelTargets, @{n="Pointers";e={$_.pointers -join ", "}} | Format-Table -AutoSize
