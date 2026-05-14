param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-pointer-tables.json",
    [int]$MinPointers = 4,
    [int]$MaxResults = 120
)

Set-StrictMode -Version 2.0

$source = @"
using System;
using System.Collections.Generic;

public struct SpyroPointerTableHit {
    public int Offset;
    public int Count;
    public uint First;
    public uint Last;
    public int AscendingPairs;
    public int RegularDelta;
    public int RegularDeltaHits;
}

public static class SpyroPointerTableScanner {
    private const uint Base = 0x80000000;
    private const uint End = 0x80200000;

    private static uint U32(byte[] bytes, int offset) {
        if (offset < 0 || offset + 4 > bytes.Length) return 0;
        return BitConverter.ToUInt32(bytes, offset);
    }

    private static bool IsPtr(uint value) {
        return value >= Base && value < End;
    }

    public static SpyroPointerTableHit[] Find(byte[] bytes, int minPointers, int maxResults) {
        List<SpyroPointerTableHit> hits = new List<SpyroPointerTableHit>();
        for (int offset = 0; offset <= bytes.Length - (minPointers * 4); offset += 4) {
            List<uint> values = new List<uint>();
            for (int i = 0; i < 512; i++) {
                int pos = offset + (i * 4);
                if (pos + 4 > bytes.Length) break;
                uint value = U32(bytes, pos);
                if (!IsPtr(value)) break;
                values.Add(value);
            }

            if (values.Count < minPointers) continue;

            int ascending = 0;
            Dictionary<int, int> deltas = new Dictionary<int, int>();
            for (int i = 1; i < values.Count; i++) {
                if (values[i] >= values[i - 1]) ascending++;
                int delta = unchecked((int)(values[i] - values[i - 1]));
                if (!deltas.ContainsKey(delta)) deltas[delta] = 0;
                deltas[delta]++;
            }
            int bestDelta = 0;
            int bestDeltaHits = 0;
            foreach (KeyValuePair<int, int> pair in deltas) {
                if (pair.Value > bestDeltaHits) {
                    bestDelta = pair.Key;
                    bestDeltaHits = pair.Value;
                }
            }

            SpyroPointerTableHit hit = new SpyroPointerTableHit();
            hit.Offset = offset;
            hit.Count = values.Count;
            hit.First = values[0];
            hit.Last = values[values.Count - 1];
            hit.AscendingPairs = ascending;
            hit.RegularDelta = bestDelta;
            hit.RegularDeltaHits = bestDeltaHits;
            hits.Add(hit);

            offset += Math.Max(0, (values.Count - 1) * 4);
        }

        hits.Sort(delegate(SpyroPointerTableHit a, SpyroPointerTableHit b) {
            int count = b.Count.CompareTo(a.Count);
            if (count != 0) return count;
            int regular = b.RegularDeltaHits.CompareTo(a.RegularDeltaHits);
            if (regular != 0) return regular;
            return a.Offset.CompareTo(b.Offset);
        });

        if (hits.Count > maxResults)
            hits.RemoveRange(maxResults, hits.Count - maxResults);
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

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Convert-PointerToOffset([uint32]$Pointer) {
    return [int]([uint64]$Pointer - $script:PsxRamBase)
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge $script:PsxRamBase -and $address -lt ($script:PsxRamBase + 0x200000))
}

function Get-ByteProfile([byte[]]$Ram, [int]$Offset, [int]$Length = 36) {
    $end = [Math]::Min($Ram.Length, $Offset + $Length)
    $zero = 0
    $ff = 0
    $smallIndex = 0
    $printable = 0
    $unique = New-Object 'System.Collections.Generic.HashSet[byte]'
    for ($i = $Offset; $i -lt $end; $i++) {
        $b = $Ram[$i]
        [void]$unique.Add($b)
        if ($b -eq 0) { $zero++ }
        if ($b -eq 0xFF) { $ff++ }
        if ($b -le 0x3F) { $smallIndex++ }
        if ($b -ge 0x20 -and $b -le 0x7E) { $printable++ }
    }
    $count = [Math]::Max(1, $end - $Offset)
    return [ordered]@{
        zeroRatio = [Math]::Round($zero / [double]$count, 3)
        ffRatio = [Math]::Round($ff / [double]$count, 3)
        smallIndexRatio = [Math]::Round($smallIndex / [double]$count, 3)
        printableRatio = [Math]::Round($printable / [double]$count, 3)
        unique = $unique.Count
    }
}

function Classify-Target([byte[]]$Ram, [uint32]$Pointer) {
    if (-not (Test-PsxPointer $Pointer)) { return [ordered]@{ kind = "invalid" } }
    $offset = Convert-PointerToOffset $Pointer
    $profile = Get-ByteProfile $Ram $offset 36
    $kind = "unknown"
    if ($profile.smallIndexRatio -ge 0.85 -and $profile.ffRatio -gt 0) {
        $kind = "byte-index-record"
    }
    elseif ($profile.smallIndexRatio -ge 0.8 -and $profile.unique -ge 8) {
        $kind = "byte-index-like"
    }
    elseif ($profile.zeroRatio -gt 0.75) {
        $kind = "mostly-zero"
    }
    elseif ($profile.printableRatio -gt 0.55) {
        $kind = "packed-or-text-like"
    }
    return [ordered]@{
        pointer = ("0x{0:X8}" -f $Pointer)
        ramOffset = ("0x{0:X}" -f $offset)
        kind = $kind
        profile = $profile
    }
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$hits = @([SpyroPointerTableScanner]::Find($ram, $MinPointers, $MaxResults))
$tables = @()
foreach ($hit in $hits) {
    $targets = @()
    $kindCounts = @{}
    $sampleCount = [Math]::Min([int]$hit.Count, 24)
    for ($i = 0; $i -lt $sampleCount; $i++) {
        $pointer = Get-UInt32LE $ram ([int]$hit.Offset + ($i * 4))
        $target = Classify-Target $ram $pointer
        $targets += $target
        $kind = [string]$target.kind
        if (-not $kindCounts.ContainsKey($kind)) { $kindCounts[$kind] = 0 }
        $kindCounts[$kind]++
    }
    $dominantKind = "unknown"
    $dominantCount = 0
    foreach ($key in $kindCounts.Keys) {
        if ($kindCounts[$key] -gt $dominantCount) {
            $dominantKind = $key
            $dominantCount = $kindCounts[$key]
        }
    }

    $tables += [pscustomobject][ordered]@{
        ramOffset = ("0x{0:X}" -f [int]$hit.Offset)
        runtimeAddress = Format-RuntimeAddress ([int]$hit.Offset)
        count = [int]$hit.Count
        firstPointer = ("0x{0:X8}" -f [uint32]$hit.First)
        lastPointer = ("0x{0:X8}" -f [uint32]$hit.Last)
        ascendingPairs = [int]$hit.AscendingPairs
        regularDelta = ("0x{0:X}" -f [int]$hit.RegularDelta)
        regularDeltaHits = [int]$hit.RegularDeltaHits
        dominantTargetKind = $dominantKind
        dominantTargetKindCount = $dominantCount
        targetSamples = $targets
    }
}

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "RAM pointer table survey. Target classification is heuristic and based on sampled pointed-to bytes."
    tables = $tables
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote pointer table survey to $resolvedOut"
$tables | Select-Object runtimeAddress, count, firstPointer, lastPointer, regularDelta, regularDeltaHits, dominantTargetKind, dominantTargetKindCount | Format-Table -AutoSize
