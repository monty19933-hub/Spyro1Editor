param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$OutJsonPath = ".\stonehill-full-disc-source-leads.json",
    [string]$OutMarkdownPath = ".\stonehill-full-disc-source-leads.md",
    [int]$WindowSize = 64,
    [int]$MaxWindowsPerMoby = 24
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$WadLba = 37
$StoneMetadataOffset = 8333312
$StoneMetadataSize = 57344
$StoneAssetOffset = 8390656
$StoneAssetSize = 3682304

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
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

function Get-RawAxis($Edit, [string]$Axis) {
    $raw = Get-Field $Edit "rawOriginal" $null
    if ($null -ne $raw) {
        $value = Get-Field $raw $Axis $null
        if ($null -ne $value) { return [int]$value }
    }
    $original = Get-Field $Edit "original" $null
    return [int][Math]::Round([double](Get-Field $original $Axis 0) * 16.0)
}

function Get-ScaledInt16Candidates([int]$Raw) {
    $scaled = [double]$Raw / 16.0
    $set = New-Object System.Collections.Generic.HashSet[int]
    foreach ($value in @([Math]::Floor($scaled), [Math]::Round($scaled), [Math]::Ceiling($scaled))) {
        $iv = [int]$value
        if ($iv -ge [int16]::MinValue -and $iv -le [int16]::MaxValue) {
            [void]$set.Add($iv)
        }
    }
    return @($set)
}

function Get-DiscRegion([int64]$ImageOffset) {
    $sector = [int64][Math]::Floor($ImageOffset / 2352)
    $sectorOffset = [int]($ImageOffset % 2352)
    if ($sectorOffset -lt 24 -or $sectorOffset -ge (24 + 2048)) {
        return [ordered]@{
            userData = $false
            sector = $sector
            sectorOffset = $sectorOffset
            wadRelativeOffset = ""
            region = "raw-sector-non-user-data"
        }
    }

    $wadOffset = (($sector - $WadLba) * 2048L) + ($sectorOffset - 24)
    $region = "disc-user-data"
    if ($wadOffset -ge $StoneMetadataOffset -and $wadOffset -lt ($StoneMetadataOffset + $StoneMetadataSize)) {
        $region = "stone-hill-metadata-wad"
    }
    elseif ($wadOffset -ge $StoneAssetOffset -and $wadOffset -lt ($StoneAssetOffset + $StoneAssetSize)) {
        $region = "stone-hill-asset-wad"
    }

    return [ordered]@{
        userData = $true
        sector = $sector
        sectorOffset = $sectorOffset
        wadRelativeOffset = $(if ($wadOffset -ge 0) { "0x{0:X}" -f $wadOffset } else { "" })
        region = $region
    }
}

$scannerSource = @"
using System;
using System.Collections.Generic;
using System.Text;

public static class FullDiscInt16WindowScanner {
    private sealed class AxisValue {
        public int Target;
        public string Axis;
        public short Value;
    }

    private sealed class Accumulator {
        public int Target;
        public long WindowStart;
        public int AxisMask;
        public int HitCount;
        public int MinOffset = Int32.MaxValue;
        public int MaxOffset = Int32.MinValue;
        public readonly List<string> Hits = new List<string>();
    }

    private static int AxisBit(string axis) {
        if (axis == "x") return 1;
        if (axis == "y") return 2;
        if (axis == "z") return 4;
        return 0;
    }

    private static int AxisCount(int mask) {
        int count = 0;
        if ((mask & 1) != 0) count++;
        if ((mask & 2) != 0) count++;
        if ((mask & 4) != 0) count++;
        return count;
    }

    public static string Scan(byte[] bytes, string spec, int windowSize, int maxPerTarget) {
        Dictionary<short, List<AxisValue>> valueMap = new Dictionary<short, List<AxisValue>>();
        foreach (string line in spec.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)) {
            string[] parts = line.Trim().Split(',');
            if (parts.Length != 3) continue;
            AxisValue av = new AxisValue();
            av.Target = Int32.Parse(parts[0]);
            av.Axis = parts[1];
            av.Value = (short)Int32.Parse(parts[2]);
            List<AxisValue> list;
            if (!valueMap.TryGetValue(av.Value, out list)) {
                list = new List<AxisValue>();
                valueMap[av.Value] = list;
            }
            list.Add(av);
        }

        Dictionary<string, Accumulator> windows = new Dictionary<string, Accumulator>();
        for (int i = 0; i < bytes.Length - 1; i++) {
            short value = unchecked((short)(bytes[i] | (bytes[i + 1] << 8)));
            List<AxisValue> values;
            if (!valueMap.TryGetValue(value, out values)) continue;

            foreach (AxisValue av in values) {
                long windowStart = ((long)i / windowSize) * windowSize;
                string key = av.Target.ToString() + ":" + windowStart.ToString();
                Accumulator acc;
                if (!windows.TryGetValue(key, out acc)) {
                    acc = new Accumulator();
                    acc.Target = av.Target;
                    acc.WindowStart = windowStart;
                    windows[key] = acc;
                }
                int inWindow = (int)(i - windowStart);
                acc.AxisMask |= AxisBit(av.Axis);
                acc.HitCount++;
                if (inWindow < acc.MinOffset) acc.MinOffset = inWindow;
                if (inWindow > acc.MaxOffset) acc.MaxOffset = inWindow;
                if (acc.Hits.Count < 12) {
                    acc.Hits.Add(av.Axis + "=" + ((int)av.Value).ToString() + "@+" + inWindow.ToString());
                }
            }
        }

        List<Accumulator> filtered = new List<Accumulator>();
        foreach (Accumulator acc in windows.Values) {
            if (AxisCount(acc.AxisMask) >= 2) filtered.Add(acc);
        }
        filtered.Sort(delegate(Accumulator a, Accumulator b) {
            int c = a.Target.CompareTo(b.Target);
            if (c != 0) return c;
            c = AxisCount(b.AxisMask).CompareTo(AxisCount(a.AxisMask));
            if (c != 0) return c;
            int spreadA = a.MaxOffset - a.MinOffset;
            int spreadB = b.MaxOffset - b.MinOffset;
            c = spreadA.CompareTo(spreadB);
            if (c != 0) return c;
            c = a.HitCount.CompareTo(b.HitCount);
            if (c != 0) return c;
            return a.WindowStart.CompareTo(b.WindowStart);
        });

        Dictionary<int, int> emittedByTarget = new Dictionary<int, int>();
        StringBuilder sb = new StringBuilder();
        foreach (Accumulator acc in filtered) {
            int emitted;
            if (!emittedByTarget.TryGetValue(acc.Target, out emitted)) emitted = 0;
            if (emitted >= maxPerTarget) continue;
            emittedByTarget[acc.Target] = emitted + 1;
            sb.Append(acc.Target).Append('|')
              .Append(acc.WindowStart).Append('|')
              .Append(AxisCount(acc.AxisMask)).Append('|')
              .Append(acc.HitCount).Append('|')
              .Append(acc.MaxOffset - acc.MinOffset).Append('|')
              .Append(String.Join(";", acc.Hits.ToArray()))
              .Append('\n');
        }
        return sb.ToString();
    }
}
"@

Add-Type -TypeDefinition $scannerSource

$imagePath = Resolve-WorkspacePath $ImagePath
$nativeEditsPath = Resolve-WorkspacePath $NativeEditsPath
$outJsonPath = Resolve-WorkspacePath $OutJsonPath
$outMarkdownPath = Resolve-WorkspacePath $OutMarkdownPath
if (-not (Test-Path -LiteralPath $imagePath)) { throw "Missing image: $imagePath" }
if (-not (Test-Path -LiteralPath $nativeEditsPath)) { throw "Missing native edits: $nativeEditsPath" }

$editsRoot = Get-Content -Raw -LiteralPath $nativeEditsPath | ConvertFrom-Json
$specLines = New-Object System.Collections.ArrayList
foreach ($edit in @(Get-ArrayField $editsRoot "edits")) {
    $index = [int](Get-Field $edit "index" -1)
    if ($index -lt 0) { continue }
    foreach ($axis in @("x", "y", "z")) {
        $raw = Get-RawAxis $edit $axis
        foreach ($value in @(Get-ScaledInt16Candidates $raw)) {
            if ([Math]::Abs($value) -lt 32) { continue }
            [void]$specLines.Add("$index,$axis,$value")
        }
    }
}

$bytes = [System.IO.File]::ReadAllBytes($imagePath)
$scanText = [FullDiscInt16WindowScanner]::Scan($bytes, ($specLines.ToArray() -join "`n"), $WindowSize, $MaxWindowsPerMoby)
$rows = New-Object System.Collections.ArrayList
foreach ($line in @($scanText -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split("|")
    if ($parts.Length -lt 6) { continue }
    $windowStart = [int64]$parts[1]
    $region = Get-DiscRegion $windowStart
    [void]$rows.Add([ordered]@{
        runtimeMobyIndex = [int]$parts[0]
        imageWindow = ("0x{0:X}" -f $windowStart)
        imageWindowDecimal = $windowStart
        axisCount = [int]$parts[2]
        hitCount = [int]$parts[3]
        spread = [int]$parts[4]
        hits = @($parts[5].Split(";"))
        discRegion = $region.region
        userData = $region.userData
        sector = $region.sector
        sectorOffset = $region.sectorOffset
        wadRelativeWindow = $region.wadRelativeOffset
    })
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    generatedBy = "Find-StoneHillFullDiscSourceLeads.ps1"
    purpose = "Full-disc scaled-int16 window search for moved Stone Hill mobys after the Stone Hill asset-only source windows failed."
    warning = "These are candidates only. Do not treat them as source records until a patched CUE fresh-loads with source-placement-loaded."
    imagePath = $imagePath
    nativeEditsPath = $nativeEditsPath
    windowSize = $WindowSize
    maxWindowsPerMoby = $MaxWindowsPerMoby
    searchedValues = @($specLines.ToArray())
    candidateWindows = @($rows.ToArray())
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outJsonPath -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# Stone Hill Full-Disc Source Leads")
[void]$md.Add("")
[void]$md.Add("Generated: $($result.generatedAt)")
[void]$md.Add("")
[void]$md.Add("The broad BIN was patched correctly, but live validation still loaded original positions. This report looks outside the previous Stone Hill asset-only search for stronger scaled-int16 source candidates.")
[void]$md.Add("")
[void]$md.Add("| Moby | Axes | Hits | Spread | Region | WAD window | Image window | Values |")
[void]$md.Add("| --- | --- | --- | --- | --- | --- | --- | --- |")
foreach ($row in @($rows | Sort-Object runtimeMobyIndex, @{ Expression = { if ($_.discRegion -eq "stone-hill-metadata-wad") { 0 } elseif ($_.discRegion -eq "disc-user-data") { 1 } elseif ($_.discRegion -eq "stone-hill-asset-wad") { 2 } else { 3 } } }, axisCount, spread | Select-Object -First 120)) {
    $values = (@($row.hits) -join "; ").Replace("|", "/")
    [void]$md.Add("| L$($row.runtimeMobyIndex) | $($row.axisCount) | $($row.hitCount) | $($row.spread) | $($row.discRegion) | $($row.wadRelativeWindow) | $($row.imageWindow) | $values |")
}
[void]$md.Add("")
[void]$md.Add("Next use: pick a tiny set of candidates outside the already-failed Stone Hill asset windows, create a disposable probe BIN, then validate by fresh-loading the CUE.")
$md | Set-Content -LiteralPath $outMarkdownPath -Encoding UTF8

Write-Host "Wrote full-disc source lead JSON to $outJsonPath"
Write-Host "Wrote full-disc source lead Markdown to $outMarkdownPath"
Write-Host "Candidate windows: $($rows.Count)"
