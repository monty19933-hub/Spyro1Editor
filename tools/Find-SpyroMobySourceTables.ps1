param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [string]$OutJsonPath = ".\spyro-moby-source-tables.json",
    [string]$OutMarkdownPath = ".\spyro-moby-source-tables.md",
    [int]$MinRecords = 24,
    [int]$MaxCandidatesPerEntry = 16
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$RecordStride = 0x58

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return $prop.Value
}

function Get-ArrayField($Object, [string]$Name) {
    $value = Get-Field $Object $Name @()
    if ($null -eq $value) { return @() }
    if ($value -is [System.Array]) { return @($value) }
    return @($value)
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

$scannerSource = @"
using System;
using System.Collections.Generic;
using System.Text;

public static class SpyroMobySourceTableScanner {
    private struct Hit {
        public int Offset;
        public int Count;
        public int NonzeroTypeCount;
        public int PointerishCount;
        public int Type20;
        public int Type18;
        public int Type30;
        public int Type00;
        public int Score;
    }

    private static int I32(byte[] b, int o) {
        if (o < 0 || o + 4 > b.Length) return 0;
        return BitConverter.ToInt32(b, o);
    }

    private static uint U32(byte[] b, int o) {
        if (o < 0 || o + 4 > b.Length) return 0;
        return BitConverter.ToUInt32(b, o);
    }

    private static bool IsPointerish(uint v) {
        return v == 0u || v == 0x80000000u || (v >= 0x00000010u && v < 0x00020000u);
    }

    private static bool GoodRecord(byte[] b, int o, out int type, out bool pointerish) {
        type = 0;
        pointerish = false;
        if (o < 0 || o + 0x58 > b.Length) return false;
        int x = I32(b, o + 0x0C);
        int y = I32(b, o + 0x10);
        int z = I32(b, o + 0x14);
        if (Math.Abs((long)x) > 4000000L || Math.Abs((long)y) > 4000000L || Math.Abs((long)z) > 4000000L) return false;
        if (x == 0 && y == 0 && z == 0) return false;
        type = b[o + 0x50];
        int state = b[o + 0x51];
        if (type > 0x7F || state > 0x7F) return false;
        uint w1c = U32(b, o + 0x1C);
        uint w50 = U32(b, o + 0x50);
        pointerish = IsPointerish(w1c) || ((w50 & 0xFFu) == 0x20u || (w50 & 0xFFu) == 0x18u || (w50 & 0xFFu) == 0x30u);
        return true;
    }

    public static string Scan(byte[] bytes, int minRecords, int maxCandidates) {
        List<Hit> hits = new List<Hit>();
        for (int start = 0; start <= bytes.Length - (minRecords * 0x58); start += 4) {
            int count = 0;
            int nonzero = 0;
            int ptr = 0;
            int t20 = 0, t18 = 0, t30 = 0, t00 = 0;
            for (int i = 0; i < 360; i++) {
                int o = start + (i * 0x58);
                int type;
                bool pointerish;
                if (!GoodRecord(bytes, o, out type, out pointerish)) break;
                count++;
                if (type != 0) nonzero++;
                if (pointerish) ptr++;
                if (type == 0x20) t20++;
                else if (type == 0x18) t18++;
                else if (type == 0x30) t30++;
                else if (type == 0x00) t00++;
            }

            if (count < minRecords) continue;
            if (nonzero < Math.Max(8, count / 3)) continue;
            Hit h = new Hit();
            h.Offset = start;
            h.Count = count;
            h.NonzeroTypeCount = nonzero;
            h.PointerishCount = ptr;
            h.Type20 = t20;
            h.Type18 = t18;
            h.Type30 = t30;
            h.Type00 = t00;
            h.Score = (count * 10) + (nonzero * 3) + ptr + (t20 + t18 + t30) * 2;
            hits.Add(h);
            start += Math.Max(0, (count - 1) * 0x58);
        }

        hits.Sort(delegate(Hit a, Hit b) {
            int c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;
            c = b.Count.CompareTo(a.Count);
            if (c != 0) return c;
            return a.Offset.CompareTo(b.Offset);
        });

        if (hits.Count > maxCandidates) hits.RemoveRange(maxCandidates, hits.Count - maxCandidates);
        StringBuilder sb = new StringBuilder();
        foreach (Hit h in hits) {
            sb.Append(h.Offset).Append('|')
              .Append(h.Count).Append('|')
              .Append(h.NonzeroTypeCount).Append('|')
              .Append(h.PointerishCount).Append('|')
              .Append(h.Type20).Append('|')
              .Append(h.Type18).Append('|')
              .Append(h.Type30).Append('|')
              .Append(h.Type00).Append('|')
              .Append(h.Score).Append('\n');
        }
        return sb.ToString();
    }
}
"@

Add-Type -TypeDefinition $scannerSource

$imagePath = Resolve-WorkspacePath $ImagePath
$wadAnalysisPath = Resolve-WorkspacePath $WadAnalysisPath
if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing image: $imagePath" }
if (-not (Test-Path -LiteralPath $wadAnalysisPath)) { throw "Missing WAD analysis: $wadAnalysisPath" }

$wadAnalysis = Get-Content -Raw -LiteralPath $wadAnalysisPath | ConvertFrom-Json
$entries = @(Get-ArrayField $wadAnalysis "entries")
$candidates = New-Object System.Collections.ArrayList

$stream = [System.IO.File]::OpenRead($imagePath)
try {
    foreach ($entry in $entries) {
        $index = [int](Get-Field $entry "index" -1)
        $offset = [int64](Get-Field $entry "offset" 0)
        $size64 = [int64](Get-Field $entry "size" 0)
        if ($index -lt 0 -or $size64 -lt ($MinRecords * $RecordStride) -or $size64 -gt [int64][int]::MaxValue) { continue }

        $bytes = Read-WadBytes $stream $offset ([int]$size64)
        $scanText = [SpyroMobySourceTableScanner]::Scan($bytes, $MinRecords, $MaxCandidatesPerEntry)
        foreach ($line in @($scanText -split "`n")) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $parts = $line.Split("|")
            if ($parts.Length -lt 9) { continue }
            $rel = [int]$parts[0]
            $count = [int]$parts[1]
            [void]$candidates.Add([ordered]@{
                wadEntry = $index
                entryOffset = ("0x{0:X}" -f $offset)
                entrySize = ("0x{0:X}" -f $size64)
                tableRelativeOffset = ("0x{0:X}" -f $rel)
                tableWadOffset = ("0x{0:X}" -f ($offset + [int64]$rel))
                recordStride = "0x58"
                recordCount = $count
                nonzeroTypeCount = [int]$parts[2]
                pointerishCount = [int]$parts[3]
                typeCounts = [ordered]@{
                    "0x20" = [int]$parts[4]
                    "0x18" = [int]$parts[5]
                    "0x30" = [int]$parts[6]
                    "0x00" = [int]$parts[7]
                }
                score = [int]$parts[8]
                stoneHillKnownMatch = (($index -eq 12) -and ($rel -eq 0x1DF338))
            })
        }
    }
}
finally {
    $stream.Dispose()
}

$sorted = @($candidates.ToArray() | Sort-Object -Property @{ Expression = { [int]$_.score }; Descending = $true }, @{ Expression = { [int]$_.wadEntry }; Descending = $false })
$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    imagePath = (Resolve-Path -LiteralPath $imagePath).Path
    wadAnalysisPath = (Resolve-Path -LiteralPath $wadAnalysisPath).Path
    parameters = [ordered]@{
        minRecords = $MinRecords
        maxCandidatesPerEntry = $MaxCandidatesPerEntry
        recordStride = "0x58"
    }
    candidateCount = $sorted.Count
    knownStoneHillCandidate = @($sorted | Where-Object { $_.stoneHillKnownMatch } | Select-Object -First 1)
    candidates = $sorted
}

$outJson = Resolve-WorkspacePath $OutJsonPath
$outMarkdown = Resolve-WorkspacePath $OutMarkdownPath
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Spyro Moby Source Table Candidates")
[void]$lines.Add("")
[void]$lines.Add("- Generated: $($result.generatedAt)")
[void]$lines.Add("- Candidates: $($result.candidateCount)")
[void]$lines.Add("- Record stride: 0x58")
[void]$lines.Add("")
[void]$lines.Add("| rank | entry | table WAD | rel | records | score | type 0x20 | type 0x18 | type 0x30 | type 0x00 | note |")
[void]$lines.Add("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")
$rank = 1
foreach ($candidate in @($sorted | Select-Object -First 80)) {
    $note = if ([bool]$candidate.stoneHillKnownMatch) { "known Stone Hill working table" } else { "" }
    [void]$lines.Add("| $rank | $($candidate.wadEntry) | $($candidate.tableWadOffset) | $($candidate.tableRelativeOffset) | $($candidate.recordCount) | $($candidate.score) | $($candidate.typeCounts.'0x20') | $($candidate.typeCounts.'0x18') | $($candidate.typeCounts.'0x30') | $($candidate.typeCounts.'0x00') | $note |")
    $rank++
}

$lines | Set-Content -LiteralPath $outMarkdown -Encoding UTF8

Write-Host "Wrote moby source table candidates to $outJson"
Write-Host "Wrote moby source table report to $outMarkdown"
Write-Host ("Candidates: {0}" -f $sorted.Count)
if (@($result.knownStoneHillCandidate).Count -gt 0) {
    Write-Host "Rediscovered known Stone Hill table."
}
