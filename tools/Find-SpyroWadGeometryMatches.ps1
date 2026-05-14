param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$GeometryPath = ".\stonehill-geometry-overlay.json",
    [string]$OutPath = ".\stonehill-wad-geometry-matches.json",

    [int]$MaxCandidates = 8,
    [int]$CandidateIndex = 0,
    [int]$MinSignatureVertices = 4
)

Set-StrictMode -Version 2.0

$source = @"
using System;
using System.Collections.Generic;

public static class SpyroPatternSearch {
    public static int[] FindVertexSignature(byte[] bytes, byte[] pattern, int stride, int maxMatches) {
        List<int> matches = new List<int>();
        if (bytes == null || pattern == null || pattern.Length == 0 || stride < 6 || bytes.Length < pattern.Length)
            return matches.ToArray();

        byte first = pattern[0];
        int limit = bytes.Length - pattern.Length;
        for (int offset = 0; offset <= limit; offset++) {
            if (bytes[offset] != first)
                continue;

            bool good = true;
            for (int cursor = 0; cursor < pattern.Length && good; cursor += stride) {
                for (int b = 0; b < 6; b++) {
                    if (bytes[offset + cursor + b] != pattern[cursor + b]) {
                        good = false;
                        break;
                    }
                }
            }

            if (good) {
                matches.Add(offset);
                if (matches.Count >= maxMatches)
                    return matches.ToArray();
            }
        }
        return matches.ToArray();
    }
}
"@

Add-Type -TypeDefinition $source

$WadLba = 37
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($WadLba + [Math]::Floor($absolute / 2048))
        $sectorBytes = Read-UserSector $Stream $sector
        $toCopy = [Math]::Min(2048 - $sectorOffset, $remaining)
        [Array]::Copy($sectorBytes, $sectorOffset, $result, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $absolute += $toCopy
    }
    return $result
}

function Parse-ArchiveHeader([byte[]]$Bytes, [int64]$ArchiveSize) {
    $entries = @()
    $firstDataOffset = [int64](Get-UInt32LE $Bytes 0)
    if ($firstDataOffset -le 0 -or $firstDataOffset -gt $Bytes.Length) { $firstDataOffset = $Bytes.Length }
    for ($offset = 0; $offset -le ([Math]::Min($Bytes.Length, $firstDataOffset) - 8); $offset += 8) {
        $fileOffset = [int64](Get-UInt32LE $Bytes $offset)
        $fileSize = [int64](Get-UInt32LE $Bytes ($offset + 4))
        if ($fileOffset -eq 0 -and $fileSize -eq 0) { continue }
        if ($fileOffset -le 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [ordered]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
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

function Add-Int16Bytes([System.Collections.Generic.List[byte]]$List, [int]$Value) {
    $bytes = [BitConverter]::GetBytes([int16]$Value)
    [void]$List.Add($bytes[0])
    [void]$List.Add($bytes[1])
}

function New-VertexSignature($Points, [int]$Stride, [int]$Start, [int]$Count) {
    $signature = New-Object 'System.Collections.Generic.List[byte]'
    for ($i = 0; $i -lt $Count; $i++) {
        $point = $Points[$Start + $i]
        Add-Int16Bytes $signature ([int](Get-ObjectField $point "x" 0))
        Add-Int16Bytes $signature ([int](Get-ObjectField $point "y" 0))
        Add-Int16Bytes $signature ([int](Get-ObjectField $point "z" 0))
        $padding = $Stride - 6
        for ($p = 0; $p -lt $padding; $p++) {
            [void]$signature.Add(0xFF)
        }
    }
    return $signature.ToArray()
}

function Test-PatternAt([byte[]]$Bytes, [byte[]]$Pattern, [int]$Offset, [int]$Stride) {
    $cursor = 0
    while ($cursor -lt $Pattern.Length) {
        for ($b = 0; $b -lt 6; $b++) {
            if (($Offset + $cursor + $b) -ge $Bytes.Length) { return $false }
            if ($Bytes[$Offset + $cursor + $b] -ne $Pattern[$cursor + $b]) { return $false }
        }
        $cursor += $Stride
    }
    return $true
}

function Find-PatternOffsets([byte[]]$Bytes, [byte[]]$Pattern, [int]$Stride, [int]$MaxMatches = 16) {
    return @([SpyroPatternSearch]::FindVertexSignature($Bytes, $Pattern, $Stride, $MaxMatches))
}

$geometry = Get-Content -Raw -LiteralPath $GeometryPath | ConvertFrom-Json
$candidates = @($geometry.candidates | Select-Object -Skip $CandidateIndex -First $MaxCandidates)
if ($candidates.Count -eq 0) {
    throw "No geometry candidates were found in $GeometryPath."
}

$stream = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $ImagePath))
try {
    $assetHeader = Read-WadBytes $stream $StoneAssetOffset 65536
    $subfiles = @(Parse-ArchiveHeader $assetHeader $StoneAssetSize)
    $subfileBytes = @{}
    foreach ($subfile in $subfiles) {
        $subfileBytes[[int]$subfile.index] = Read-WadBytes $stream ($StoneAssetOffset + [int64]$subfile.offset) ([int]$subfile.size)
    }
}
finally {
    $stream.Dispose()
}

$results = @()
foreach ($candidate in $candidates) {
    $points = @(Get-ObjectField $candidate "points" @())
    $stride = [int](Get-ObjectField $candidate "stride" 0)
    if ($points.Count -lt $MinSignatureVertices -or $stride -lt 6) { continue }
    $starts = @(0, 1, 2, 4, 8, 16, 24, 32) | Where-Object { ($_ + $MinSignatureVertices) -le $points.Count }
    $candidateMatches = @()
    foreach ($start in $starts) {
        $signature = New-VertexSignature $points $stride $start $MinSignatureVertices
        foreach ($subfile in $subfiles) {
            $bytes = [byte[]]$subfileBytes[[int]$subfile.index]
            $matches = @(Find-PatternOffsets $bytes $signature $stride 8)
            foreach ($match in $matches) {
                $candidateMatches += [ordered]@{
                    subfileIndex = [int]$subfile.index
                    subfileOffset = ("0x{0:X}" -f [int64]$subfile.offset)
                    subfileSize = [int64]$subfile.size
                    matchOffset = ("0x{0:X}" -f $match)
                    assetOffset = ("0x{0:X}" -f ([int64]$subfile.offset + [int64]$match))
                    signatureStartVertex = [int]$start
                    signatureVertices = [int]$MinSignatureVertices
                    stride = [int]$stride
                }
            }
        }
    }
    $results += [pscustomobject][ordered]@{
        runtimeAddress = [string](Get-ObjectField $candidate "runtimeAddress" "")
        ramOffset = [int](Get-ObjectField $candidate "ramOffset" 0)
        stride = $stride
        validVertices = [int](Get-ObjectField $candidate "validVertices" 0)
        projection = [string](Get-ObjectField $candidate "projection" "")
        fitScore = [double](Get-ObjectField $candidate "fitScore" 0)
        matches = $candidateMatches
        matchCount = $candidateMatches.Count
    }
}

$output = [ordered]@{
    imagePath = (Resolve-Path -LiteralPath $ImagePath).Path
    geometryPath = (Resolve-Path -LiteralPath $GeometryPath).Path
    generatedAt = (Get-Date).ToString("s")
    stoneHillAsset = [ordered]@{
        wadOffset = $StoneAssetOffset
        size = $StoneAssetSize
        subfileCount = $subfiles.Count
    }
    candidates = $results
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$output | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Wrote WAD geometry match report to $resolvedOut"
$results | Select-Object runtimeAddress, stride, validVertices, projection, fitScore, matchCount | Format-Table -AutoSize
