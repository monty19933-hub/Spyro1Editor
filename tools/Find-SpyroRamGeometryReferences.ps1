param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$GeometryPath = ".\stonehill-geometry-overlay.json",
    [string]$OutPath = ".\stonehill-geometry-references.json",
    [int]$MaxCandidates = 8
)

Set-StrictMode -Version 2.0

$source = @"
using System;
using System.Collections.Generic;

public struct SpyroPointerHit {
    public int Offset;
    public uint Value;
}

public static class SpyroPointerScanner {
    public static SpyroPointerHit[] FindPointersIntoRange(byte[] bytes, uint start, uint end, int maxHits) {
        List<SpyroPointerHit> hits = new List<SpyroPointerHit>();
        if (bytes == null || bytes.Length < 4 || start >= end)
            return hits.ToArray();

        for (int offset = 0; offset <= bytes.Length - 4; offset += 4) {
            uint value = BitConverter.ToUInt32(bytes, offset);
            if (value >= start && value < end) {
                SpyroPointerHit hit = new SpyroPointerHit();
                hit.Offset = offset;
                hit.Value = value;
                hits.Add(hit);
                if (hits.Count >= maxHits)
                    return hits.ToArray();
            }
        }
        return hits.ToArray();
    }
}
"@

Add-Type -TypeDefinition $source

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-ObjectField($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Get-WordContext([byte[]]$Ram, [int]$CenterOffset, [int]$Before = 64, [int]$After = 96) {
    $rows = @()
    $start = [Math]::Max(0, $CenterOffset - $Before)
    $end = [Math]::Min($Ram.Length - 4, $CenterOffset + $After)
    $start -= ($start % 4)
    for ($offset = $start; $offset -le $end; $offset += 4) {
        $u = Get-UInt32LE $Ram $offset
        $i = Get-Int32LE $Ram $offset
        $kind = "word"
        if (Test-PsxPointer $u) {
            $kind = "psx-pointer"
        }
        elseif ($u -gt 0 -and $u -lt 4096) {
            $kind = "small-count"
        }
        elseif ($i -gt -32768 -and $i -lt 32768) {
            $kind = "small-signed"
        }

        $rows += [ordered]@{
            ramOffset = ("0x{0:X}" -f $offset)
            runtimeAddress = Format-RuntimeAddress $offset
            relativeToHit = $offset - $CenterOffset
            valueHex = ("0x{0:X8}" -f $u)
            valueSigned = $i
            kind = $kind
        }
    }
    return $rows
}

function Measure-DescriptorWindow([byte[]]$Ram, [int]$HitOffset, [int]$CandidateStartOffset, [int]$CandidateEndOffset, [int]$ExpectedVertexCount, [int]$ExpectedStride) {
    $best = $null
    foreach ($baseDelta in @(0, -4, -8, -12, -16, -24, -32, -48, -64)) {
        $base = $HitOffset + $baseDelta
        if ($base -lt 0 -or ($base + 128) -gt $Ram.Length) { continue }
        $pointerCount = 0
        $rangePointerCount = 0
        $smallCountHits = 0
        $strideHits = 0
        $words = @()
        for ($offset = $base; $offset -lt ($base + 128); $offset += 4) {
            $u = Get-UInt32LE $Ram $offset
            $runtimeStart = [uint32]($script:PsxRamBase + [uint64]$CandidateStartOffset)
            $runtimeEnd = [uint32]($script:PsxRamBase + [uint64]$CandidateEndOffset)
            if (Test-PsxPointer $u) {
                $pointerCount++
                if ($u -ge $runtimeStart -and $u -lt $runtimeEnd) {
                    $rangePointerCount++
                }
            }
            if ($u -eq [uint32]$ExpectedVertexCount -or $u -eq [uint32]($ExpectedVertexCount - 1) -or $u -eq [uint32]($ExpectedVertexCount + 1)) {
                $smallCountHits++
            }
            if ($u -eq [uint32]$ExpectedStride) {
                $strideHits++
            }
            if ($words.Count -lt 12) {
                $words += ("0x{0:X8}" -f $u)
            }
        }
        $score = ($rangePointerCount * 80) + ($pointerCount * 8) + ($smallCountHits * 25) + ($strideHits * 15)
        $candidate = [ordered]@{
            baseRamOffset = ("0x{0:X}" -f $base)
            baseRuntimeAddress = Format-RuntimeAddress $base
            hitFieldOffset = $HitOffset - $base
            score = $score
            pointerCount = $pointerCount
            rangePointerCount = $rangePointerCount
            expectedCountHits = $smallCountHits
            strideHits = $strideHits
            firstWords = $words
        }
        if ($null -eq $best -or $candidate.score -gt $best.score) {
            $best = $candidate
        }
    }
    return $best
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$geometry = Get-Content -Raw -LiteralPath $GeometryPath | ConvertFrom-Json
$candidates = @($geometry.candidates | Select-Object -First $MaxCandidates)

$results = @()
foreach ($candidate in $candidates) {
    $ramOffset = [int](Get-ObjectField $candidate "ramOffset" 0)
    $stride = [int](Get-ObjectField $candidate "stride" 0)
    $validVertices = [int](Get-ObjectField $candidate "validVertices" 0)
    $byteLength = [Math]::Max(1, $stride * $validVertices)
    $rangeStart = [uint32]($script:PsxRamBase + [uint64]$ramOffset)
    $rangeEnd = [uint32]($script:PsxRamBase + [uint64]($ramOffset + $byteLength))
    $hits = @([SpyroPointerScanner]::FindPointersIntoRange($ram, $rangeStart, $rangeEnd, 256))
    $hitRows = @()
    foreach ($hit in $hits) {
        $hitOffset = [int]$hit.Offset
        $descriptor = Measure-DescriptorWindow $ram $hitOffset $ramOffset ($ramOffset + $byteLength) $validVertices $stride
        $hitRows += [ordered]@{
            ramOffset = ("0x{0:X}" -f $hitOffset)
            runtimeAddress = Format-RuntimeAddress $hitOffset
            pointerValue = ("0x{0:X8}" -f [uint32]$hit.Value)
            pointerDeltaIntoCandidate = ([int64][uint32]$hit.Value - [int64]$rangeStart)
            descriptorGuess = $descriptor
            context = Get-WordContext $ram $hitOffset 48 80
        }
    }

    $results += [pscustomobject][ordered]@{
        candidateRuntimeAddress = [string](Get-ObjectField $candidate "runtimeAddress" "")
        candidateRamOffset = ("0x{0:X}" -f $ramOffset)
        stride = $stride
        validVertices = $validVertices
        byteLength = $byteLength
        rangeStart = ("0x{0:X8}" -f $rangeStart)
        rangeEnd = ("0x{0:X8}" -f $rangeEnd)
        projection = [string](Get-ObjectField $candidate "projection" "")
        fitScore = [double](Get-ObjectField $candidate "fitScore" 0)
        pointerHitCount = $hitRows.Count
        pointerHits = $hitRows
    }
}

$output = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    geometryPath = (Resolve-Path -LiteralPath $GeometryPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Pointer references into candidate RAM geometry ranges. Descriptor guesses are heuristic windows around pointer hits."
    candidates = $results
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$output | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote geometry reference report to $resolvedOut"
$results | Select-Object candidateRuntimeAddress, stride, validVertices, projection, fitScore, pointerHitCount | Format-Table -AutoSize
