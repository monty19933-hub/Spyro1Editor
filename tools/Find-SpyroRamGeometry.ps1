param(
    [Parameter(Mandatory = $true)]
    [string]$RamPath,

    [string]$OutPath = ".\stonehill-geometry-overlay.json",
    [int]$MaxCandidates = 8,
    [string]$MinRuntimeAddress = "0x80080000"
)

Set-StrictMode -Version 2.0

$PsxRamBase = [Convert]::ToUInt64("80000000", 16)

function Convert-RuntimeAddressToOffset([string]$Address) {
    $clean = $Address.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) { $clean = $clean.Substring(2) }
    $value = [Convert]::ToUInt64($clean, 16)
    if ($value -lt $script:PsxRamBase -or $value -ge ($script:PsxRamBase + 0x200000)) {
        throw "$Address is not inside PS1 main RAM."
    }
    return [int]($value - $script:PsxRamBase)
}

$source = @"
using System;
using System.Collections.Generic;

public struct SpyroVertexCloudScanResult {
    public int Offset;
    public int Stride;
    public int ValidVertices;
    public int Score;
    public int MinX;
    public int MaxX;
    public int MinY;
    public int MaxY;
    public int MinZ;
    public int MaxZ;
}

public struct SpyroIndexTableScanResult {
    public int Offset;
    public int RecordStride;
    public int IndexSize;
    public int VerticesPerFace;
    public int FaceCount;
    public int UniqueVertices;
    public int EdgeCount;
    public double Score;
}

public static class SpyroGeometryScanner {
    private static short I16(byte[] bytes, int offset) {
        if (offset < 0 || offset + 2 > bytes.Length)
            return 0;
        return BitConverter.ToInt16(bytes, offset);
    }

    private static bool Plausible(int x, int y, int z) {
        if (x < -2000 || x > 14000) return false;
        if (y < -2000 || y > 14000) return false;
        if (z < -4000 || z > 8000) return false;
        if (Math.Abs(x) < 8 && Math.Abs(y) < 8 && Math.Abs(z) < 8) return false;
        return true;
    }

    private static bool PlausibleAt(byte[] bytes, int offset) {
        return Plausible(I16(bytes, offset), I16(bytes, offset + 2), I16(bytes, offset + 4));
    }

    private static SpyroVertexCloudScanResult ScoreAt(byte[] bytes, int offset, int stride, int maxCount) {
        SpyroVertexCloudScanResult result = new SpyroVertexCloudScanResult();
        result.Offset = offset;
        result.Stride = stride;
        result.MinX = 999999;
        result.MinY = 999999;
        result.MinZ = 999999;
        result.MaxX = -999999;
        result.MaxY = -999999;
        result.MaxZ = -999999;

        int badRun = 0;
        for (int i = 0; i < maxCount; i++) {
            int pos = offset + (i * stride);
            if (pos + 6 > bytes.Length)
                break;

            int x = I16(bytes, pos);
            int y = I16(bytes, pos + 2);
            int z = I16(bytes, pos + 4);
            if (Plausible(x, y, z)) {
                result.ValidVertices++;
                badRun = 0;
                if (x < result.MinX) result.MinX = x;
                if (x > result.MaxX) result.MaxX = x;
                if (y < result.MinY) result.MinY = y;
                if (y > result.MaxY) result.MaxY = y;
                if (z < result.MinZ) result.MinZ = z;
                if (z > result.MaxZ) result.MaxZ = z;
            } else {
                badRun++;
                if (result.ValidVertices > 24 && badRun >= 16)
                    break;
            }
        }

        int spanX = result.MaxX - result.MinX;
        int spanY = result.MaxY - result.MinY;
        int spanZ = result.MaxZ - result.MinZ;
        if (result.ValidVertices < 30 || spanX < 500 || spanY < 500) {
            result.Score = 0;
            return result;
        }

        result.Score = result.ValidVertices;
        if (spanX > 2000) result.Score += 20;
        if (spanY > 2000) result.Score += 20;
        if (spanZ > 300) result.Score += 10;
        return result;
    }

    public static SpyroVertexCloudScanResult[] Find(byte[] bytes, int maxResults) {
        int[] strides = new int[] { 6, 8, 12, 16 };
        List<SpyroVertexCloudScanResult> results = new List<SpyroVertexCloudScanResult>();
        foreach (int stride in strides) {
            int limit = bytes.Length - (80 * stride);
            for (int offset = 0; offset < limit; offset += stride) {
                if (!PlausibleAt(bytes, offset))
                    continue;

                int nextValid = 0;
                for (int probe = 1; probe <= 6; probe++) {
                    int probeOffset = offset + (probe * stride);
                    if (probeOffset + 6 > bytes.Length)
                        break;
                    if (PlausibleAt(bytes, probeOffset))
                        nextValid++;
                }
                if (nextValid < 4)
                    continue;

                SpyroVertexCloudScanResult scored = ScoreAt(bytes, offset, stride, 384);
                if (scored.Score > 0) {
                    results.Add(scored);
                    offset += Math.Max(64, (scored.ValidVertices * stride) - 2);
                }
            }
        }

        results.Sort(delegate(SpyroVertexCloudScanResult a, SpyroVertexCloudScanResult b) {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            return b.ValidVertices.CompareTo(a.ValidVertices);
        });
        if (results.Count > maxResults)
            results.RemoveRange(maxResults, results.Count - maxResults);
        return results.ToArray();
    }

    private static ushort U16(byte[] bytes, int offset) {
        if (offset < 0 || offset + 2 > bytes.Length)
            return 65535;
        return BitConverter.ToUInt16(bytes, offset);
    }

    private static bool GoodIndex(int value, int count) {
        return value >= 0 && value < count;
    }

    private static double DistanceSquared(short[] xs, short[] ys, short[] zs, int a, int b) {
        double dx = xs[a] - xs[b];
        double dy = ys[a] - ys[b];
        double dz = zs[a] - zs[b];
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static double TriangleArea2(short[] xs, short[] ys, short[] zs, int a, int b, int c) {
        double abx = xs[b] - xs[a];
        double aby = ys[b] - ys[a];
        double abz = zs[b] - zs[a];
        double acx = xs[c] - xs[a];
        double acy = ys[c] - ys[a];
        double acz = zs[c] - zs[a];
        double cx = (aby * acz) - (abz * acy);
        double cy = (abz * acx) - (abx * acz);
        double cz = (abx * acy) - (aby * acx);
        return Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
    }

    private static bool GoodTriangle(short[] xs, short[] ys, short[] zs, int count, int a, int b, int c) {
        if (!GoodIndex(a, count) || !GoodIndex(b, count) || !GoodIndex(c, count))
            return false;
        if (a == b || a == c || b == c)
            return false;
        const double maxEdge = 5200.0 * 5200.0;
        if (DistanceSquared(xs, ys, zs, a, b) > maxEdge) return false;
        if (DistanceSquared(xs, ys, zs, b, c) > maxEdge) return false;
        if (DistanceSquared(xs, ys, zs, c, a) > maxEdge) return false;
        return TriangleArea2(xs, ys, zs, a, b, c) > 50.0;
    }

    private static int ReadIndex(byte[] bytes, int offset, int indexSize) {
        if (indexSize == 1) {
            if (offset < 0 || offset >= bytes.Length)
                return -1;
            return bytes[offset];
        }
        return U16(bytes, offset);
    }

    private static SpyroIndexTableScanResult ScoreIndexTable(byte[] bytes, int offset, int recordStride, int indexSize, int verticesPerFace, short[] xs, short[] ys, short[] zs, int count) {
        SpyroIndexTableScanResult result = new SpyroIndexTableScanResult();
        result.Offset = offset;
        result.RecordStride = recordStride;
        result.IndexSize = indexSize;
        result.VerticesPerFace = verticesPerFace;

        HashSet<int> uniqueVertices = new HashSet<int>();
        HashSet<string> uniqueEdges = new HashSet<string>();
        int badRun = 0;
        int records = 0;
        int maxRecords = 192;
        for (int face = 0; face < maxRecords; face++) {
            int pos = offset + (face * recordStride);
            if (pos < 0 || pos + recordStride > bytes.Length)
                break;

            int a = ReadIndex(bytes, pos, indexSize);
            int b = ReadIndex(bytes, pos + indexSize, indexSize);
            int c = ReadIndex(bytes, pos + (indexSize * 2), indexSize);
            bool valid = GoodTriangle(xs, ys, zs, count, a, b, c);
            int d = -1;
            if (valid && verticesPerFace == 4) {
                d = ReadIndex(bytes, pos + (indexSize * 3), indexSize);
                if (GoodIndex(d, count) && d != a && d != b && d != c && GoodTriangle(xs, ys, zs, count, a, c, d)) {
                    AddEdge(uniqueEdges, c, d);
                    AddEdge(uniqueEdges, d, a);
                    uniqueVertices.Add(d);
                }
            }

            if (valid) {
                records++;
                badRun = 0;
                uniqueVertices.Add(a);
                uniqueVertices.Add(b);
                uniqueVertices.Add(c);
                AddEdge(uniqueEdges, a, b);
                AddEdge(uniqueEdges, b, c);
                AddEdge(uniqueEdges, c, a);
            }
            else {
                badRun++;
                if (records == 0 && badRun >= 4)
                    break;
                if (records > 0 && records < 8 && badRun >= 5)
                    break;
                if (records >= 8 && badRun >= 10)
                    break;
            }
        }

        result.FaceCount = records;
        result.UniqueVertices = uniqueVertices.Count;
        result.EdgeCount = uniqueEdges.Count;
        if (records < 16 || result.UniqueVertices < 18 || result.EdgeCount < 24) {
            result.Score = 0.0;
            return result;
        }

        double coverage = (double)result.UniqueVertices / Math.Max(1, count);
        double formatBonus = indexSize == 2 ? 20.0 : 0.0;
        result.Score = (records * 18.0) + (result.EdgeCount * 2.0) + (coverage * 450.0) + formatBonus;
        return result;
    }

    private static void AddEdge(HashSet<string> edges, int a, int b) {
        if (a == b) return;
        if (a > b) {
            int t = a;
            a = b;
            b = t;
        }
        edges.Add(a.ToString() + "-" + b.ToString());
    }

    public static SpyroIndexTableScanResult[] FindIndexTables(byte[] bytes, int searchStart, int searchEnd, short[] xs, short[] ys, short[] zs, int count, int maxResults) {
        List<SpyroIndexTableScanResult> results = new List<SpyroIndexTableScanResult>();
        if (count < 16)
            return results.ToArray();

        int start = Math.Max(0, searchStart);
        int end = Math.Min(bytes.Length - 16, searchEnd);
        int[] ushortStrides = new int[] { 6, 8, 10, 12, 16 };
        int[] byteStrides = new int[] { 3, 4, 6, 8 };

        for (int offset = start; offset < end; offset += 2) {
            foreach (int stride in ushortStrides) {
                int vertsPerFace = stride == 8 ? 4 : 3;
                SpyroIndexTableScanResult scored = ScoreIndexTable(bytes, offset, stride, 2, vertsPerFace, xs, ys, zs, count);
                if (scored.Score > 0)
                    results.Add(scored);
            }
        }

        // Byte-sized index tables are possible, but random byte data creates many
        // false starts. Keep the regular overlay pass focused on uint16 tables;
        // a later deep-scan tool can explore byte formats separately.
        if (count <= 256 && (end - start) <= 0x10000) {
            int byteEnd = Math.Min(end, start + 0x10000);
            for (int offset = start; offset < byteEnd; offset++) {
                foreach (int stride in byteStrides) {
                    int vertsPerFace = stride == 4 || stride == 8 ? 4 : 3;
                    SpyroIndexTableScanResult scored = ScoreIndexTable(bytes, offset, stride, 1, vertsPerFace, xs, ys, zs, Math.Min(count, 256));
                    if (scored.Score > 0)
                        results.Add(scored);
                }
            }
        }

        results.Sort(delegate(SpyroIndexTableScanResult a, SpyroIndexTableScanResult b) {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            return b.FaceCount.CompareTo(a.FaceCount);
        });
        if (results.Count > maxResults)
            results.RemoveRange(maxResults, results.Count - maxResults);
        return results.ToArray();
    }
}
"@

Add-Type -TypeDefinition $source

function Get-Int16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Test-PsxPointer([uint32]$Value) {
    $address = [uint64]$Value
    return ($address -ge [Convert]::ToUInt64("80000000", 16) -and $address -lt [Convert]::ToUInt64("80200000", 16))
}

function Test-PlausibleVertex([int]$X, [int]$Y, [int]$Z) {
    if ($X -lt -2000 -or $X -gt 14000) { return $false }
    if ($Y -lt -2000 -or $Y -gt 14000) { return $false }
    if ($Z -lt -4000 -or $Z -gt 8000) { return $false }
    if ([Math]::Abs($X) -lt 8 -and [Math]::Abs($Y) -lt 8 -and [Math]::Abs($Z) -lt 8) { return $false }
    return $true
}

function Get-VertexCloudScore([byte[]]$Bytes, [int]$Offset, [int]$Stride, [int]$Count) {
    $points = @()
    $badRun = 0
    $valid = 0
    $minX = 999999
    $maxX = -999999
    $minY = 999999
    $maxY = -999999
    $minZ = 999999
    $maxZ = -999999

    for ($i = 0; $i -lt $Count; $i++) {
        $pos = $Offset + ($i * $Stride)
        if (($pos + 6) -gt $Bytes.Length) { break }
        $x = Get-Int16LE $Bytes $pos
        $y = Get-Int16LE $Bytes ($pos + 2)
        $z = Get-Int16LE $Bytes ($pos + 4)
        if (Test-PlausibleVertex $x $y $z) {
            $valid++
            $badRun = 0
            if ($points.Count -lt 700) {
                $points += [ordered]@{ x = $x; y = $y; z = $z }
            }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
            if ($z -lt $minZ) { $minZ = $z }
            if ($z -gt $maxZ) { $maxZ = $z }
        }
        else {
            $badRun++
            if ($valid -gt 24 -and $badRun -ge 16) { break }
        }
    }

    if ($valid -lt 30) { return $null }
    $spanX = $maxX - $minX
    $spanY = $maxY - $minY
    $spanZ = $maxZ - $minZ
    if ($spanX -lt 500 -or $spanY -lt 500) { return $null }

    $score = $valid
    if ($spanX -gt 2000) { $score += 20 }
    if ($spanY -gt 2000) { $score += 20 }
    if ($spanZ -gt 300) { $score += 10 }

    return [ordered]@{
        ramOffset = $Offset
        runtimeAddress = ("0x{0:X8}" -f (0x80000000 + $Offset))
        encoding = "int16 xyz"
        stride = $Stride
        validVertices = $valid
        score = $score
        bounds = [ordered]@{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY; minZ = $minZ; maxZ = $maxZ }
        points = $points
    }
}

function Get-MobySummary([byte[]]$Ram) {
    $pointer = Get-UInt32LE $Ram 0x75828
    if (-not (Test-PsxPointer $pointer)) {
        return [ordered]@{ pointer = ("0x{0:X8}" -f $pointer); count = 0; bounds = $null }
    }
    $start = [int]($pointer -band 0x001FFFFF)
    $points = @()
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 80)
        if (($offset + 80) -gt $Ram.Length) { break }
        $type = [int]$Ram[$offset + 0x48]
        if ($type -le 0 -or $type -gt 0x7F) { continue }
        $x = [BitConverter]::ToInt32($Ram, $offset + 4) / 16.0
        $y = [BitConverter]::ToInt32($Ram, $offset + 8) / 16.0
        $z = [BitConverter]::ToInt32($Ram, $offset + 12) / 16.0
        if ($x -eq 0 -and $y -eq 256 -and $z -eq 0) { continue }
        if ($x -lt -2000 -or $x -gt 14000 -or $y -lt -2000 -or $y -gt 14000) { continue }
        $points += [ordered]@{ index = $i; type = ("0x{0:X2}" -f $type); x = [Math]::Round($x, 2); y = [Math]::Round($y, 2); z = [Math]::Round($z, 2) }
    }

    if ($points.Count -eq 0) {
        return [ordered]@{ pointer = ("0x{0:X8}" -f $pointer); count = 0; bounds = $null }
    }

    $xs = @($points | ForEach-Object { $_.x })
    $ys = @($points | ForEach-Object { $_.y })
    $zs = @($points | ForEach-Object { $_.z })
    return [ordered]@{
        pointer = ("0x{0:X8}" -f $pointer)
        count = $points.Count
        bounds = [ordered]@{
            minX = ($xs | Measure-Object -Minimum).Minimum
            maxX = ($xs | Measure-Object -Maximum).Maximum
            minY = ($ys | Measure-Object -Minimum).Minimum
            maxY = ($ys | Measure-Object -Maximum).Maximum
            minZ = ($zs | Measure-Object -Minimum).Minimum
            maxZ = ($zs | Measure-Object -Maximum).Maximum
        }
        points = $points
    }
}

function Get-ProjectedCoordinate($Point, [string]$Axis) {
    switch ($Axis) {
        "x" { return [double](Get-ObjectFieldValue $Point "x" 0) }
        "y" { return [double](Get-ObjectFieldValue $Point "y" 0) }
        "z" { return [double](Get-ObjectFieldValue $Point "z" 0) }
        default { return 0.0 }
    }
}

function Get-ObjectFieldValue($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Measure-ProjectionFit($Candidate, $MobySummary) {
    $mobyPoints = @(Get-ObjectFieldValue $MobySummary "points" @())
    if ($mobyPoints.Count -eq 0) {
        return [ordered]@{ name = "xy"; score = [double](Get-ObjectFieldValue $Candidate "score" 0); mobysInside = 0; overlapRatio = 0.0; projectedBounds = $Candidate.bounds; projectedPoints = $Candidate.points }
    }

    $candidatePoints = @(Get-ObjectFieldValue $Candidate "points" @())
    $mobyBounds = Get-ObjectFieldValue $MobySummary "bounds" $null
    if ($candidatePoints.Count -eq 0 -or $null -eq $mobyBounds) {
        return [ordered]@{ name = "xy"; score = 0; mobysInside = 0; overlapRatio = 0.0; projectedBounds = $null; projectedPoints = @() }
    }

    $best = $null
    foreach ($projection in @("xy", "xz", "yx", "yz", "zx", "zy")) {
        $axisA = $projection.Substring(0, 1)
        $axisB = $projection.Substring(1, 1)
        $projected = @()
        $minA = 999999.0
        $maxA = -999999.0
        $minB = 999999.0
        $maxB = -999999.0
        foreach ($point in $candidatePoints) {
            $a = Get-ProjectedCoordinate $point $axisA
            $b = Get-ProjectedCoordinate $point $axisB
            $projected += [ordered]@{ x = [Math]::Round($a, 2); y = [Math]::Round($b, 2); z = [double](Get-ObjectFieldValue $point "z" 0) }
            if ($a -lt $minA) { $minA = $a }
            if ($a -gt $maxA) { $maxA = $a }
            if ($b -lt $minB) { $minB = $b }
            if ($b -gt $maxB) { $maxB = $b }
        }

        $pad = 900.0
        $inside = 0
        foreach ($moby in $mobyPoints) {
            $mx = [double](Get-ObjectFieldValue $moby "x" 0)
            $my = [double](Get-ObjectFieldValue $moby "y" 0)
            if ($mx -ge ($minA - $pad) -and $mx -le ($maxA + $pad) -and $my -ge ($minB - $pad) -and $my -le ($maxB + $pad)) {
                $inside++
            }
        }

        $mMinX = [double](Get-ObjectFieldValue $mobyBounds "minX" 0)
        $mMaxX = [double](Get-ObjectFieldValue $mobyBounds "maxX" 0)
        $mMinY = [double](Get-ObjectFieldValue $mobyBounds "minY" 0)
        $mMaxY = [double](Get-ObjectFieldValue $mobyBounds "maxY" 0)
        $interW = [Math]::Max(0, [Math]::Min($maxA, $mMaxX) - [Math]::Max($minA, $mMinX))
        $interH = [Math]::Max(0, [Math]::Min($maxB, $mMaxY) - [Math]::Max($minB, $mMinY))
        $mobyArea = [Math]::Max(1, ($mMaxX - $mMinX) * ($mMaxY - $mMinY))
        $overlapRatio = [Math]::Min(1.0, ($interW * $interH) / $mobyArea)

        $spanA = $maxA - $minA
        $spanB = $maxB - $minB
        $spanScore = 0
        if ($spanA -gt 2000 -and $spanB -gt 2000) { $spanScore = 25 }

        $score = ([double](Get-ObjectFieldValue $Candidate "score" 0) * 0.4) + ($inside * 125.0) + ($overlapRatio * 300.0) + $spanScore
        $fit = [ordered]@{
            name = $projection
            score = [Math]::Round($score, 2)
            mobysInside = $inside
            overlapRatio = [Math]::Round($overlapRatio, 3)
            projectedBounds = [ordered]@{ minX = [Math]::Round($minA, 2); maxX = [Math]::Round($maxA, 2); minY = [Math]::Round($minB, 2); maxY = [Math]::Round($maxB, 2) }
            projectedPoints = $projected
        }
        if ($null -eq $best -or $fit.score -gt $best.score) {
            $best = $fit
        }
    }
    return $best
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-Point2D($Points, [int]$Index) {
    if ($Index -lt 0 -or $Index -ge $Points.Count) { return $null }
    return $Points[$Index]
}

function Get-PointDistanceSquared($A, $B) {
    $ax = [double](Get-ObjectFieldValue $A "x" 0)
    $ay = [double](Get-ObjectFieldValue $A "y" 0)
    $bx = [double](Get-ObjectFieldValue $B "x" 0)
    $by = [double](Get-ObjectFieldValue $B "y" 0)
    $dx = $ax - $bx
    $dy = $ay - $by
    return ($dx * $dx) + ($dy * $dy)
}

function Get-TriangleArea2($A, $B, $C) {
    $ax = [double](Get-ObjectFieldValue $A "x" 0)
    $ay = [double](Get-ObjectFieldValue $A "y" 0)
    $bx = [double](Get-ObjectFieldValue $B "x" 0)
    $by = [double](Get-ObjectFieldValue $B "y" 0)
    $cx = [double](Get-ObjectFieldValue $C "x" 0)
    $cy = [double](Get-ObjectFieldValue $C "y" 0)
    return [Math]::Abs((($bx - $ax) * ($cy - $ay)) - (($by - $ay) * ($cx - $ax)))
}

function Add-EdgeByIndex([System.Collections.Generic.Dictionary[string, object]]$Edges, $Points, [int]$A, [int]$B) {
    if ($A -eq $B) { return }
    if ($A -gt $B) {
        $t = $A
        $A = $B
        $B = $t
    }
    $key = "$A-$B"
    if ($Edges.ContainsKey($key)) { return }
    $pointA = Get-Point2D $Points $A
    $pointB = Get-Point2D $Points $B
    if ($null -eq $pointA -or $null -eq $pointB) { return }
    $Edges[$key] = [ordered]@{
        a = $A
        b = $B
        x1 = [Math]::Round([double](Get-ObjectFieldValue $pointA "x" 0), 2)
        y1 = [Math]::Round([double](Get-ObjectFieldValue $pointA "y" 0), 2)
        x2 = [Math]::Round([double](Get-ObjectFieldValue $pointB "x" 0), 2)
        y2 = [Math]::Round([double](Get-ObjectFieldValue $pointB "y" 0), 2)
    }
}

function Test-TriangleIndices($Points, [int]$A, [int]$B, [int]$C) {
    if ($A -eq $B -or $A -eq $C -or $B -eq $C) { return $false }
    if ($A -lt 0 -or $B -lt 0 -or $C -lt 0) { return $false }
    if ($A -ge $Points.Count -or $B -ge $Points.Count -or $C -ge $Points.Count) { return $false }
    $pa = Get-Point2D $Points $A
    $pb = Get-Point2D $Points $B
    $pc = Get-Point2D $Points $C
    $maxEdgeSquared = 25000000.0
    if ((Get-PointDistanceSquared $pa $pb) -gt $maxEdgeSquared) { return $false }
    if ((Get-PointDistanceSquared $pb $pc) -gt $maxEdgeSquared) { return $false }
    if ((Get-PointDistanceSquared $pc $pa) -gt $maxEdgeSquared) { return $false }
    if ((Get-TriangleArea2 $pa $pb $pc) -lt 20.0) { return $false }
    return $true
}

function Get-IndexValue([byte[]]$Bytes, [int]$Offset, [int]$IndexSize) {
    if ($IndexSize -eq 1) {
        if ($Offset -lt 0 -or $Offset -ge $Bytes.Length) { return -1 }
        return [int]$Bytes[$Offset]
    }
    return [int](Get-UInt16LE $Bytes $Offset)
}

function Find-IndexedGeometryEdges([byte[]]$Ram, $Candidate, $ProjectedPoints) {
    $points = @($ProjectedPoints)
    $rawPoints = @(Get-ObjectFieldValue $Candidate "points" @())
    if ($points.Count -lt 16) {
        return [ordered]@{ source = "none"; score = 0; offset = $null; faceCount = 0; edgeCount = 0; edges = @() }
    }

    $vertexOffset = [int](Get-ObjectFieldValue $Candidate "ramOffset" 0)
    $stride = [int](Get-ObjectFieldValue $Candidate "stride" 0)
    $validVertices = [Math]::Min([int](Get-ObjectFieldValue $Candidate "validVertices" $points.Count), $points.Count)
    $vertexBytes = [Math]::Max(0, $validVertices * [Math]::Max(1, $stride))

    $count = [Math]::Min($validVertices, $rawPoints.Count)
    $xs = New-Object 'System.Int16[]' $count
    $ys = New-Object 'System.Int16[]' $count
    $zs = New-Object 'System.Int16[]' $count
    for ($i = 0; $i -lt $count; $i++) {
        $xs[$i] = [int16](Get-ObjectFieldValue $rawPoints[$i] "x" 0)
        $ys[$i] = [int16](Get-ObjectFieldValue $rawPoints[$i] "y" 0)
        $zs[$i] = [int16](Get-ObjectFieldValue $rawPoints[$i] "z" 0)
    }

    $searchStart = [Math]::Max(0, $vertexOffset - 0x18000)
    $searchEnd = [Math]::Min($Ram.Length - 16, $vertexOffset + $vertexBytes + 0x28000)
    $tableCandidates = @([SpyroGeometryScanner]::FindIndexTables($Ram, $searchStart, $searchEnd, $xs, $ys, $zs, $count, 6))
    if ($tableCandidates.Count -gt 0 -and [double]$tableCandidates[0].Score -gt 0) {
        $best = $tableCandidates[0]
        $edges = New-Object 'System.Collections.Generic.Dictionary[string, object]'
        $faceLimit = [Math]::Min([int]$best.FaceCount + 24, 900)
        for ($face = 0; $face -lt $faceLimit; $face++) {
            $pos = [int]$best.Offset + ($face * [int]$best.RecordStride)
            if (($pos + [int]$best.RecordStride) -gt $Ram.Length) { break }
            $i0 = Get-IndexValue $Ram $pos ([int]$best.IndexSize)
            $i1 = Get-IndexValue $Ram ($pos + [int]$best.IndexSize) ([int]$best.IndexSize)
            $i2 = Get-IndexValue $Ram ($pos + ([int]$best.IndexSize * 2)) ([int]$best.IndexSize)
            if (Test-TriangleIndices $points $i0 $i1 $i2) {
                Add-EdgeByIndex $edges $points $i0 $i1
                Add-EdgeByIndex $edges $points $i1 $i2
                Add-EdgeByIndex $edges $points $i2 $i0
                if ([int]$best.VerticesPerFace -eq 4) {
                    $i3 = Get-IndexValue $Ram ($pos + ([int]$best.IndexSize * 3)) ([int]$best.IndexSize)
                    if ($i3 -ge 0 -and $i3 -lt $points.Count -and $i3 -ne $i0 -and $i3 -ne $i1 -and $i3 -ne $i2) {
                        Add-EdgeByIndex $edges $points $i2 $i3
                        Add-EdgeByIndex $edges $points $i3 $i0
                    }
                }
            }
        }

        return [ordered]@{
            source = "compiled-index-table-$([int]$best.IndexSize * 8)-bit-$([int]$best.VerticesPerFace)v"
            score = [Math]::Round([double]$best.Score, 2)
            offset = [int]$best.Offset
            runtimeAddress = ("0x{0:X8}" -f (0x80000000 + [int]$best.Offset))
            recordStride = [int]$best.RecordStride
            indexSize = [int]$best.IndexSize
            verticesPerFace = [int]$best.VerticesPerFace
            faceCount = [int]$best.FaceCount
            uniqueVertices = [int]$best.UniqueVertices
            edgeCount = $edges.Count
            edges = @($edges.Values | Select-Object -First 1200)
            tableCandidates = @($tableCandidates | Select-Object -First 6 | ForEach-Object {
                [ordered]@{
                    runtimeAddress = ("0x{0:X8}" -f (0x80000000 + [int]$_.Offset))
                    ramOffset = [int]$_.Offset
                    recordStride = [int]$_.RecordStride
                    indexSize = [int]$_.IndexSize
                    verticesPerFace = [int]$_.VerticesPerFace
                    faceCount = [int]$_.FaceCount
                    uniqueVertices = [int]$_.UniqueVertices
                    edgeCount = [int]$_.EdgeCount
                    score = [Math]::Round([double]$_.Score, 2)
                }
            })
        }
    }
    return [ordered]@{ source = "none"; score = 0; offset = $null; faceCount = 0; edgeCount = 0; edges = @() }
}

function Get-InferredGeometryEdges($ProjectedPoints) {
    $points = @($ProjectedPoints)
    $edges = New-Object 'System.Collections.Generic.Dictionary[string, object]'
    if ($points.Count -lt 2) {
        return [ordered]@{ source = "none"; edgeCount = 0; edges = @() }
    }

    $maxDistanceSquared = 2250000.0
    $limit = [Math]::Min($points.Count, 520)
    for ($i = 0; $i -lt $limit; $i++) {
        $bestA = -1
        $bestADistance = [double]::MaxValue
        $bestB = -1
        $bestBDistance = [double]::MaxValue
        $probeEnd = [Math]::Min($limit - 1, $i + 120)
        for ($j = [Math]::Max(0, $i - 24); $j -le $probeEnd; $j++) {
            if ($i -eq $j) { continue }
            $distance = Get-PointDistanceSquared $points[$i] $points[$j]
            if ($distance -gt 25.0 -and $distance -le $maxDistanceSquared) {
                if ($distance -lt $bestADistance) {
                    $bestB = $bestA
                    $bestBDistance = $bestADistance
                    $bestA = $j
                    $bestADistance = $distance
                }
                elseif ($distance -lt $bestBDistance) {
                    $bestB = $j
                    $bestBDistance = $distance
                }
            }
        }
        if ($bestA -ge 0) {
            Add-EdgeByIndex $edges $points $i $bestA
        }
        if ($bestB -ge 0) {
            Add-EdgeByIndex $edges $points $i $bestB
        }
        if ($edges.Count -ge 900) { break }
    }

    return [ordered]@{
        source = "nearest-neighbor-preview"
        edgeCount = $edges.Count
        edges = @($edges.Values | Select-Object -First 900)
    }
}

$ram = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RamPath))
if ($ram.Length -ne 0x200000) {
    throw "Expected a raw 2 MB PS1 main RAM dump. Got $($ram.Length) bytes."
}

$minDataOffset = Convert-RuntimeAddressToOffset $MinRuntimeAddress
$scanResults = @([SpyroGeometryScanner]::Find($ram, [Math]::Max(64, $MaxCandidates * 12)) | Where-Object { [int]$_.Offset -ge $minDataOffset })
$ranked = @()
$mobySummary = Get-MobySummary $ram
foreach ($scan in $scanResults) {
    $points = @()
    for ($i = 0; $i -lt [Math]::Min(700, [int]$scan.ValidVertices); $i++) {
        $pos = [int]$scan.Offset + ($i * [int]$scan.Stride)
        if (($pos + 6) -gt $ram.Length) { break }
        $x = Get-Int16LE $ram $pos
        $y = Get-Int16LE $ram ($pos + 2)
        $z = Get-Int16LE $ram ($pos + 4)
        if (Test-PlausibleVertex $x $y $z) {
            $points += [ordered]@{ x = $x; y = $y; z = $z }
        }
    }
    $candidate = [pscustomobject][ordered]@{
        ramOffset = [int]$scan.Offset
        runtimeAddress = ("0x{0:X8}" -f (0x80000000 + [int]$scan.Offset))
        encoding = "int16 xyz"
        stride = [int]$scan.Stride
        validVertices = [int]$scan.ValidVertices
        score = [int]$scan.Score
        bounds = [ordered]@{ minX = [int]$scan.MinX; maxX = [int]$scan.MaxX; minY = [int]$scan.MinY; maxY = [int]$scan.MaxY; minZ = [int]$scan.MinZ; maxZ = [int]$scan.MaxZ }
        points = $points
    }
    $fit = Measure-ProjectionFit $candidate $mobySummary
    $candidate | Add-Member -NotePropertyName fitScore -NotePropertyValue ([double]$fit.score)
    $candidate | Add-Member -NotePropertyName projection -NotePropertyValue $fit.name
    $candidate | Add-Member -NotePropertyName mobysInsideProjection -NotePropertyValue ([int]$fit.mobysInside)
    $candidate | Add-Member -NotePropertyName projectionOverlapRatio -NotePropertyValue ([double]$fit.overlapRatio)
    $candidate | Add-Member -NotePropertyName projectedBounds -NotePropertyValue $fit.projectedBounds
    $candidate | Add-Member -NotePropertyName projectedPoints -NotePropertyValue $fit.projectedPoints
    $ranked += $candidate
}
$ranked = @($ranked | Sort-Object -Property fitScore, score -Descending | Select-Object -First $MaxCandidates)
foreach ($candidate in $ranked) {
    $projectedPoints = @(Get-ObjectFieldValue $candidate "projectedPoints" @())
    $indexedEdges = Find-IndexedGeometryEdges $ram $candidate $projectedPoints
    if ([int](Get-ObjectFieldValue $indexedEdges "edgeCount" 0) -gt 0) {
        $candidate | Add-Member -NotePropertyName edgeSource -NotePropertyValue $indexedEdges.source
        $candidate | Add-Member -NotePropertyName edgeScore -NotePropertyValue ([double](Get-ObjectFieldValue $indexedEdges "score" 0))
        $candidate | Add-Member -NotePropertyName edgeRuntimeAddress -NotePropertyValue (Get-ObjectFieldValue $indexedEdges "runtimeAddress" $null)
        $candidate | Add-Member -NotePropertyName edgeFaceCount -NotePropertyValue ([int](Get-ObjectFieldValue $indexedEdges "faceCount" 0))
        $candidate | Add-Member -NotePropertyName edges -NotePropertyValue (Get-ObjectFieldValue $indexedEdges "edges" @())
    }
    else {
        $inferredEdges = Get-InferredGeometryEdges $projectedPoints
        $candidate | Add-Member -NotePropertyName edgeSource -NotePropertyValue $inferredEdges.source
        $candidate | Add-Member -NotePropertyName edgeScore -NotePropertyValue 0.0
        $candidate | Add-Member -NotePropertyName edgeRuntimeAddress -NotePropertyValue $null
        $candidate | Add-Member -NotePropertyName edgeFaceCount -NotePropertyValue 0
        $candidate | Add-Member -NotePropertyName edges -NotePropertyValue $inferredEdges.edges
    }
}
$result = [ordered]@{
    sourceRam = (Resolve-Path -LiteralPath $RamPath).Path
    generatedAt = (Get-Date).ToString("s")
    note = "Experimental geometry overlay. These are candidate int16 vertex clouds from live RAM. Edges are either nearby uint16 index-table candidates or nearest-neighbor preview edges; this is not verified Stone Hill mesh data yet."
    minRuntimeAddress = $MinRuntimeAddress
    mobys = $mobySummary
    candidates = $ranked
}

$resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8

Write-Host "Moby placement points used for fit: $($mobySummary.count)"
Write-Host "Found $($scanResults.Count) vertex-cloud candidates. Wrote top $($ranked.Count) overlap-ranked candidates to $resolvedOut"
$ranked | Select-Object @{n="Offset";e={("0x{0:X}" -f $_.ramOffset)}}, runtimeAddress, stride, validVertices, score, fitScore, projection, @{n="Mobys";e={$_.mobysInsideProjection}}, @{n="EdgeSource";e={$_.edgeSource}}, @{n="Faces";e={$_.edgeFaceCount}}, @{n="Edges";e={@($_.edges).Count}}, @{n="ProjectedBounds";e={"X $($_.projectedBounds.minX)..$($_.projectedBounds.maxX), Y $($_.projectedBounds.minY)..$($_.projectedBounds.maxY)"}} | Format-Table -AutoSize
