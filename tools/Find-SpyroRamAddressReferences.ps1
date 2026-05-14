param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [Parameter(Mandatory = $true)]
    [string[]]$RuntimeAddress,

    [string]$OutPath = ".\stonehill-address-references.json",
    [int]$NearBytes = 0x100,
    [int]$ContextWords = 8,
    [int]$MaxHitsPerAddress = 200,
    [switch]$Unaligned
)

Set-StrictMode -Version 2.0

$source = @"
using System;
using System.Collections.Generic;

public struct SpyroAddressRefHit {
    public int Offset;
    public uint Value;
}

public static class SpyroAddressRefScanner {
    private static uint U32(byte[] bytes, int offset) {
        if (offset < 0 || offset + 4 > bytes.Length) return 0;
        return BitConverter.ToUInt32(bytes, offset);
    }

    public static SpyroAddressRefHit[] FindNear(byte[] bytes, uint target, uint nearBytes, bool unaligned, int maxHits) {
        List<SpyroAddressRefHit> hits = new List<SpyroAddressRefHit>();
        uint lo = target > nearBytes ? target - nearBytes : 0;
        uint hi = target + nearBytes;
        int step = unaligned ? 1 : 4;
        for (int offset = 0; offset <= bytes.Length - 4; offset += step) {
            uint value = U32(bytes, offset);
            if (value >= lo && value <= hi) {
                SpyroAddressRefHit hit = new SpyroAddressRefHit();
                hit.Offset = offset;
                hit.Value = value;
                hits.Add(hit);
                if (hits.Count >= maxHits) break;
            }
        }
        return hits.ToArray();
    }
}
"@

Add-Type -TypeDefinition $source

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)
$PsxRamEnd = $PsxRamBase + 0x200000

function Convert-RuntimeAddressToUInt32([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        $clean = $clean.Substring(2)
    }
    if ($clean -match '^-?\d+$') {
        $signed = [Int64]::Parse($clean, [System.Globalization.CultureInfo]::InvariantCulture)
        if ($signed -lt 0) {
            $value = [uint64]($signed + 0x100000000L)
        }
        else {
            $value = [uint64]$signed
        }
    }
    else {
        $value = [Convert]::ToUInt64($clean, 16)
    }
    if ($value -lt $script:PsxRamBase -or $value -ge $script:PsxRamEnd) {
        throw "$Address is not inside PS1 main RAM."
    }
    return [uint32]$value
}

function Format-RuntimeAddress([int]$RamOffset) {
    return ("0x{0:X8}" -f ($script:PsxRamBase + [uint64]$RamOffset))
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-ContextWords([byte[]]$Ram, [int]$CenterOffset, [int]$WordsEachSide) {
    $items = @()
    $start = [Math]::Max(0, $CenterOffset - ($WordsEachSide * 4))
    $end = [Math]::Min($Ram.Length - 4, $CenterOffset + ($WordsEachSide * 4))
    for ($offset = $start; $offset -le $end; $offset += 4) {
        $value = Get-UInt32LE $Ram $offset
        $asSigned = [BitConverter]::ToInt32($Ram, $offset)
        $items += [ordered]@{
            ramOffset = ("0x{0:X}" -f $offset)
            runtimeAddress = Format-RuntimeAddress $offset
            valueHex = ("0x{0:X8}" -f $value)
            valueSigned = $asSigned
            isCenter = ($offset -eq $CenterOffset)
        }
    }
    return $items
}

function Classify-Hit([byte[]]$Ram, [int]$Offset) {
    $pointerBefore = 0
    $pointerAfter = 0
    $start = [Math]::Max(0, $Offset - 32)
    $end = [Math]::Min($Ram.Length - 4, $Offset + 32)
    for ($i = $start; $i -le $end; $i += 4) {
        $value = [uint64](Get-UInt32LE $Ram $i)
        if ($value -ge $script:PsxRamBase -and $value -lt $script:PsxRamEnd) {
            if ($i -lt $Offset) { $pointerBefore++ }
            elseif ($i -gt $Offset) { $pointerAfter++ }
        }
    }
    if (($pointerBefore + $pointerAfter) -ge 4) { return "pointer-table-context" }
    if (($Offset % 4) -eq 0) { return "aligned-word" }
    return "unaligned-word"
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$requestedAddresses = @()
foreach ($addressArg in $RuntimeAddress) {
    foreach ($part in ([string]$addressArg).Split(',')) {
        $trimmed = $part.Trim()
        if ($trimmed.Length -gt 0) { $requestedAddresses += $trimmed }
    }
}

$addresses = @()
foreach ($address in $requestedAddresses) {
    $target = Convert-RuntimeAddressToUInt32 $address
    $hits = @([SpyroAddressRefScanner]::FindNear($ram, $target, [uint32]$NearBytes, [bool]$Unaligned, $MaxHitsPerAddress))
    $records = @()
    foreach ($hit in $hits) {
        $delta = [int64]([uint64]$hit.Value) - [int64]([uint64]$target)
        $records += [ordered]@{
            ramOffset = ("0x{0:X}" -f [int]$hit.Offset)
            runtimeAddress = Format-RuntimeAddress ([int]$hit.Offset)
            value = ("0x{0:X8}" -f [uint32]$hit.Value)
            delta = $delta
            exact = ($hit.Value -eq $target)
            classification = Classify-Hit $ram ([int]$hit.Offset)
            context = Get-ContextWords $ram ([int]$hit.Offset) $ContextWords
        }
    }

    $addresses += [ordered]@{
        target = ("0x{0:X8}" -f $target)
        nearBytes = $NearBytes
        hitCount = $records.Count
        exactHitCount = @($records | Where-Object { $_.exact }).Count
        hits = $records
    }
}

$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Finds little-endian RAM words equal or close to target runtime addresses. Close hits help identify parent tables, descriptors, and table headers."
    alignedOnly = (-not [bool]$Unaligned)
    contextWords = $ContextWords
    addresses = $addresses
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote address reference report to $resolvedOut"
foreach ($entry in $addresses) {
    Write-Host ("{0}: {1} near hits, {2} exact hits" -f $entry.target, $entry.hitCount, $entry.exactHitCount)
    $entry.hits | Select-Object -First 12 runtimeAddress, value, delta, exact, classification | Format-Table -AutoSize
}
