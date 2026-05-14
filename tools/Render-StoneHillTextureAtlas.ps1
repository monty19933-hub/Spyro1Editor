param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [int]$WadLba = 37,
    [int]$AssetWadIndex = 10,
    [int]$ModelSubfileIndex = 1,
    [switch]$UseWadTexturePages,
    [int]$TexturePagesSubfileIndex = 0,
    [string]$VramPath = "",
    [string]$SaveStatePath = "",
    [string]$ZstdDllPath = "C:\Users\monty\AppData\Local\Programs\DuckStation\zstd.dll",
    [string]$ProcessName = "duckstation",
    [int]$ProcessId = 0,
    [int]$CandidateIndex = 0,
    [int]$MaxCandidates = 8,
    [int]$CoarseStep = 4096,
    [switch]$ListCandidates,
    [string]$OutPath = ".\stonehill-texture-atlas.png",
    [string]$OutJsonPath = ".\stonehill-texture-atlas.json",
    [string]$OutVramPath = ".\stonehill-live-vram-candidate.bin",
    [switch]$SwapTexelBytes,
    [switch]$UseHqClose
)

Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Drawing

$Script:Ps1VramSize = 1024 * 512 * 2

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Find-BytePattern([byte[]]$Bytes, [byte[]]$Pattern, [int]$StartOffset = 0) {
    if ($null -eq $Bytes -or $null -eq $Pattern -or $Pattern.Length -eq 0) { return -1 }
    $max = $Bytes.Length - $Pattern.Length
    for ($i = [Math]::Max(0, $StartOffset); $i -le $max; $i++) {
        $matched = $true
        for ($j = 0; $j -lt $Pattern.Length; $j++) {
            if ($Bytes[$i + $j] -ne $Pattern[$j]) {
                $matched = $false
                break
            }
        }
        if ($matched) { return $i }
    }
    return -1
}

function Find-BytePatternOffsets([byte[]]$Bytes, [byte[]]$Pattern) {
    $offsets = New-Object System.Collections.Generic.List[int]
    $offset = Find-BytePattern $Bytes $Pattern 0
    while ($offset -ge 0) {
        [void]$offsets.Add($offset)
        $offset = Find-BytePattern $Bytes $Pattern ($offset + 1)
    }
    return [int[]]$offsets.ToArray()
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * 2352L) + 24L
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Read-WadBytes([System.IO.FileStream]$Stream, [int]$WadLba, [int64]$Offset, [int]$Length) {
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
        if ($fileOffset -lt 0 -or $fileSize -le 0 -or ($fileOffset + $fileSize) -gt $ArchiveSize) { continue }
        $entries += [pscustomobject]@{ index = [int]($offset / 8); offset = $fileOffset; size = $fileSize }
    }
    return $entries
}

function Get-ModelSubfileBytes([string]$ImagePath, [int]$WadLba, [int]$AssetWadIndex, [int]$ModelSubfileIndex) {
    $stream = [System.IO.File]::OpenRead($ImagePath)
    try {
        $wadHeader = Read-WadBytes $stream $WadLba 0 4096
        $wadEntries = @(Parse-ArchiveHeader $wadHeader 200000000)
        $assetEntry = @($wadEntries | Where-Object { $_.index -eq $AssetWadIndex })[0]
        if ($null -eq $assetEntry) { throw "Could not find WAD entry $AssetWadIndex." }

        $assetHeader = Read-WadBytes $stream $WadLba $assetEntry.offset 4096
        $subfiles = @(Parse-ArchiveHeader $assetHeader $assetEntry.size)
        $modelSubfile = @($subfiles | Where-Object { $_.index -eq $ModelSubfileIndex })[0]
        if ($null -eq $modelSubfile) { throw "Could not find model subfile $ModelSubfileIndex." }

        $bytes = Read-WadBytes $stream $WadLba ($assetEntry.offset + $modelSubfile.offset) ([int]$modelSubfile.size)
        return [pscustomobject]@{
            bytes = $bytes
            assetOffset = [int64]$assetEntry.offset
            assetSize = [int64]$assetEntry.size
            subfileOffset = [int64]$modelSubfile.offset
            subfileSize = [int64]$modelSubfile.size
        }
    }
    finally {
        $stream.Close()
    }
}

function Get-TextureXMin([int]$Region, [int]$XMin) {
    return ((($Region * 128) % 2048) + $XMin)
}

function Get-TextureYMin([int]$Region, [int]$YMin) {
    return ([int][Math]::Floor(($Region -band 0x1F) / 16.0) * 256) + $YMin
}

function Decode-TexHq([byte[]]$Bytes, [int]$Offset, [int]$Index) {
    $xmin = [int]$Bytes[$Offset + 0]
    $ymin = [int]$Bytes[$Offset + 1]
    $palette = [int](Get-UInt16LE $Bytes ($Offset + 2))
    $xmax = [int]$Bytes[$Offset + 4]
    $ymax = [int]$Bytes[$Offset + 5]
    $region = [int]$Bytes[$Offset + 6]
    $unknown = [int]$Bytes[$Offset + 7]
    return [ordered]@{
        index = $Index
        xmin = $xmin
        ymin = $ymin
        palette = $palette
        paletteByteStart = $palette * 32
        xmax = $xmax
        ymax = $ymax
        region = $region
        unknown = $unknown
        orientation = (($unknown -shr 4) -band 7)
        vramXMin = (Get-TextureXMin $region $xmin)
        vramYMin = (Get-TextureYMin $region $ymin)
        vramXMax = (Get-TextureXMin $region $xmax)
        vramYMax = (Get-TextureYMin $region $ymax)
    }
}

function Decode-TextureRecords([byte[]]$ModelBytes) {
    $textureListSize = [int](Get-UInt32LE $ModelBytes 0)
    $textureCount = [int](Get-UInt32LE $ModelBytes 4)
    if ($textureListSize -le 8 -or $textureCount -le 0) { throw "No plausible texture list found." }
    $recordBytes = [int](($textureListSize - 8) / $textureCount)
    if ((($textureListSize - 8) % $textureCount) -ne 0) { throw "Texture list is not an even fixed-record table." }

    $records = New-Object System.Collections.Generic.List[object]
    for ($texture = 0; $texture -lt $textureCount; $texture++) {
        $offset = 8 + ($texture * $recordBytes)
        $hq = @()
        for ($i = 0; $i -lt 4; $i++) {
            $hq += Decode-TexHq $ModelBytes ($offset + 24 + ($i * 8)) $i
        }
        $hqClose = @()
        for ($i = 0; $i -lt 16; $i++) {
            $hqClose += Decode-TexHq $ModelBytes ($offset + 56 + ($i * 8)) $i
        }
        [void]$records.Add([ordered]@{
            textureId = $texture
            offset = ("0x{0:X}" -f $offset)
            hqData = $hq
            hqDataClose = $hqClose
        })
    }

    return [ordered]@{
        textureListSize = $textureListSize
        textureCount = $textureCount
        recordBytes = $recordBytes
        records = @($records.ToArray())
    }
}

$scannerSource = @"
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public struct StoneHillVramScanResult {
    public long Address;
    public int Offset;
    public int Score;
    public int Smoothness;
    public int Variation;
    public int PaletteScore;
    public int SampleCount;
    public int Checksum;
}

public static class StoneHillVramScanner {
    private const int VramSize = 1024 * 512 * 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern IntPtr VirtualQueryEx(IntPtr process, IntPtr address, out MEMORY_BASIC_INFORMATION info, UIntPtr length);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, UIntPtr size, out UIntPtr bytesRead);

    private static bool IsReadable(uint protect) {
        const uint PAGE_NOACCESS = 0x01;
        const uint PAGE_GUARD = 0x100;
        return (protect & PAGE_NOACCESS) == 0 && (protect & PAGE_GUARD) == 0;
    }

    private static int GetU16(byte[] bytes, int offset) {
        if (offset < 0 || offset + 1 >= bytes.Length) return 0;
        return bytes[offset] | (bytes[offset + 1] << 8);
    }

    private static int ColorDistance(int a, int b) {
        int ar = (a & 31) * 255 / 31;
        int ag = ((a >> 5) & 31) * 255 / 31;
        int ab = ((a >> 10) & 31) * 255 / 31;
        int br = (b & 31) * 255 / 31;
        int bg = ((b >> 5) & 31) * 255 / 31;
        int bb = ((b >> 10) & 31) * 255 / 31;
        return Math.Abs(ar - br) + Math.Abs(ag - bg) + Math.Abs(ab - bb);
    }

    private static int FastChecksum(byte[] bytes, int offset) {
        unchecked {
            int hash = (int)2166136261u;
            for (int i = 0; i < VramSize; i += 4096) {
                hash ^= bytes[offset + i];
                hash *= 16777619;
            }
            return hash;
        }
    }

    public static StoneHillVramScanResult ScoreCandidate(byte[] bytes, int offset, int[] xs, int[] ys, int[] palettes) {
        StoneHillVramScanResult result = new StoneHillVramScanResult();
        result.Offset = offset;
        if (bytes == null || offset < 0 || offset + VramSize > bytes.Length || xs == null || ys == null || palettes == null)
            return result;

        int sampleCount = Math.Min(xs.Length, Math.Min(ys.Length, palettes.Length));
        int totalSmooth = 0;
        int totalVariation = 0;
        int totalPalette = 0;
        int valid = 0;

        for (int s = 0; s < sampleCount; s++) {
            int srcX = xs[s];
            int srcY = ys[s];
            int palOffset = palettes[s] * 32;
            if (srcX < 0 || srcX + 31 >= 2048 || srcY < 0 || srcY + 31 >= 512)
                continue;
            if (palOffset < 0 || palOffset + 512 > VramSize)
                continue;

            int nonZeroPalette = 0;
            int paletteVariety = 0;
            int lastPal = -1;
            for (int p = 0; p < 256; p += 4) {
                int clr = GetU16(bytes, offset + palOffset + (p * 2));
                if ((clr & 0x7FFF) != 0) nonZeroPalette++;
                if (clr != lastPal) paletteVariety++;
                lastPal = clr;
            }
            int paletteScore = Math.Min(220, (nonZeroPalette * 3) + paletteVariety);
            if (nonZeroPalette < 6)
                paletteScore -= 160;

            int changes = 0;
            int changeCount = 0;
            int unique = 0;
            int[] seen = new int[64];
            int seenCount = 0;
            int prevRowFirst = -1;

            for (int y = 0; y < 32; y += 8) {
                int prev = -1;
                for (int x = 0; x < 32; x += 8) {
                    int idx = bytes[offset + ((srcY + y) * 2048) + srcX + x];
                    int clr = GetU16(bytes, offset + palOffset + (idx * 2));
                    bool found = false;
                    for (int k = 0; k < seenCount; k++) {
                        if (seen[k] == clr) { found = true; break; }
                    }
                    if (!found && seenCount < seen.Length) {
                        seen[seenCount++] = clr;
                        unique++;
                    }
                    if (prev >= 0) {
                        changes += ColorDistance(prev, clr);
                        changeCount++;
                    }
                    if (x == 0) {
                        if (prevRowFirst >= 0) {
                            changes += ColorDistance(prevRowFirst, clr);
                            changeCount++;
                        }
                        prevRowFirst = clr;
                    }
                    prev = clr;
                }
            }

            int avgChange = changeCount > 0 ? changes / changeCount : 999;
            int smooth = Math.Max(0, 260 - avgChange);
            int variation = unique * 18;
            if (unique < 4)
                variation -= 180;
            if (unique > 28)
                variation -= (unique - 28) * 4;

            totalSmooth += smooth;
            totalVariation += variation;
            totalPalette += paletteScore;
            valid++;
        }

        result.SampleCount = valid;
        result.Smoothness = totalSmooth;
        result.Variation = totalVariation;
        result.PaletteScore = totalPalette;
        result.Score = (totalSmooth * 6) + totalVariation + totalPalette + (valid * 10);
        result.Checksum = FastChecksum(bytes, offset);
        return result;
    }

    private static void AddCandidate(List<StoneHillVramScanResult> results, StoneHillVramScanResult candidate, int maxResults) {
        if (candidate.SampleCount <= 0)
            return;
        if (results.Count < maxResults) {
            results.Add(candidate);
            return;
        }
        int worst = 0;
        for (int i = 1; i < results.Count; i++) {
            if (results[i].Score < results[worst].Score)
                worst = i;
        }
        if (candidate.Score > results[worst].Score)
            results[worst] = candidate;
    }

    public static StoneHillVramScanResult[] ScanProcess(int processId, int[] xs, int[] ys, int[] palettes, int maxResults, int coarseStep) {
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;
        const uint MEM_COMMIT = 0x1000;
        IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Could not open process memory. Try running PowerShell as Administrator.");

        try {
            List<StoneHillVramScanResult> results = new List<StoneHillVramScanResult>();
            long address = 0;
            long maxAddress = 0x7FFFFFFF0000L;
            int step = Math.Max(16, coarseStep);
            int mbiSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));
            UIntPtr mbiSizePtr = new UIntPtr((uint)mbiSize);

            while (address < maxAddress) {
                MEMORY_BASIC_INFORMATION info;
                IntPtr query = VirtualQueryEx(handle, new IntPtr(address), out info, mbiSizePtr);
                if (query == IntPtr.Zero)
                    break;

                long baseAddress = info.BaseAddress.ToInt64();
                long size = info.RegionSize.ToInt64();
                long next = baseAddress + Math.Max(size, 0x1000);

                if (info.State == MEM_COMMIT && IsReadable(info.Protect) && size >= VramSize) {
                    const int chunkSize = 64 * 1024 * 1024;
                    const int overlap = VramSize;
                    long regionOffset = 0;
                    while (regionOffset < size) {
                        long remaining = size - regionOffset;
                        int currentSize = (int)Math.Min(chunkSize, remaining);
                        if (currentSize < VramSize)
                            break;

                        byte[] buffer = new byte[currentSize];
                        UIntPtr bytesRead = UIntPtr.Zero;
                        long chunkBase = baseAddress + regionOffset;
                        if (ReadProcessMemory(handle, new IntPtr(chunkBase), buffer, new UIntPtr((uint)buffer.Length), out bytesRead)) {
                            int maxOffset = buffer.Length - VramSize;
                            for (int offset = 0; offset <= maxOffset; offset += step) {
                                StoneHillVramScanResult candidate = ScoreCandidate(buffer, offset, xs, ys, palettes);
                                candidate.Address = chunkBase + offset;
                                AddCandidate(results, candidate, Math.Max(1, maxResults * 3));
                            }
                        }

                        if (remaining <= chunkSize)
                            break;
                        regionOffset += chunkSize - overlap;
                    }
                }

                if (next <= address)
                    break;
                address = next;
            }

            results.Sort(delegate(StoneHillVramScanResult a, StoneHillVramScanResult b) {
                int score = b.Score.CompareTo(a.Score);
                if (score != 0) return score;
                return b.SampleCount.CompareTo(a.SampleCount);
            });

            int count = Math.Min(maxResults, results.Count);
            StoneHillVramScanResult[] trimmed = new StoneHillVramScanResult[count];
            for (int i = 0; i < count; i++)
                trimmed[i] = results[i];
            return trimmed;
        }
        finally {
            CloseHandle(handle);
        }
    }

    public static byte[] ReadBytes(int processId, long address, int length) {
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;
        IntPtr handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Could not open process memory. Try running PowerShell as Administrator.");
        try {
            byte[] buffer = new byte[length];
            UIntPtr bytesRead = UIntPtr.Zero;
            if (!ReadProcessMemory(handle, new IntPtr(address), buffer, new UIntPtr((uint)length), out bytesRead))
                throw new InvalidOperationException("ReadProcessMemory failed.");
            return buffer;
        }
        finally {
            CloseHandle(handle);
        }
    }
}
"@

Add-Type -TypeDefinition $scannerSource

function Convert-Psx555ToColor([uint16]$Value) {
    $r = (($Value -band 0x1F) * 255) / 31
    $g = ((($Value -shr 5) -band 0x1F) * 255) / 31
    $b = ((($Value -shr 10) -band 0x1F) * 255) / 31
    return [System.Drawing.Color]::FromArgb(255, [int]$r, [int]$g, [int]$b)
}

function Render-Atlas([byte[]]$Vram, $TextureIndex, [string]$OutPath, [bool]$SwapTexelBytes, [bool]$UseHqClose) {
    if ($Vram.Length -lt (1024 * 512 * 2)) { throw "VRAM blob must be at least 1,048,576 bytes." }

    $textureCount = [int]$TextureIndex.textureCount
    $columns = 8
    $rows = [int][Math]::Ceiling($textureCount / [double]$columns)
    $tileSize = if ($UseHqClose) { 128 } else { 64 }
    $tileGridColumns = if ($UseHqClose) { 4 } else { 2 }
    $atlasWidth = $columns * $tileSize
    $atlasHeight = $rows * $tileSize
    $bitmap = New-Object System.Drawing.Bitmap $atlasWidth, $atlasHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $matrices = @(
        @( 1,  0,  0,  1),
        @( 0,  1,  1,  0),
        @(-1,  0,  0, -1),
        @( 0, -1,  1,  0),
        @( 0,  1,  1,  0),
        @(-1,  0,  0,  1),
        @( 0, -1, -1,  0),
        @( 1,  0,  0, -1)
    )

    foreach ($record in @($TextureIndex.records)) {
        $textureId = [int]$record.textureId
        $baseX = ($textureId % $columns) * $tileSize
        $baseY = [int][Math]::Floor($textureId / $columns) * $tileSize
        $descriptors = if ($UseHqClose) { @($record.hqDataClose) } else { @($record.hqData) }
        foreach ($hq in $descriptors) {
            $tile = [int]$hq.index
            $destTileX = ($tile % $tileGridColumns) * 32
            $destTileY = [int][Math]::Floor($tile / $tileGridColumns) * 32
            $orientation = [int]$hq.orientation
            $matrix = $matrices[$orientation]
            $xx = [int]$matrix[0]
            $xy = [int]$matrix[1]
            $yx = [int]$matrix[2]
            $yy = [int]$matrix[3]
            $srcXStart = [int]$hq.vramXMin
            $srcYStart = [int]$hq.vramYMin
            if ($xx -lt 0 -or $xy -lt 0) { $srcXStart += 31 }
            if ($yx -lt 0 -or $yy -lt 0) { $srcYStart += 31 }
            $paletteByteStart = [int]$hq.paletteByteStart
            if ($paletteByteStart -lt 0 -or ($paletteByteStart + 512) -gt $Vram.Length) { continue }

            for ($y = 0; $y -lt 32; $y++) {
                for ($x = 0; $x -lt 32; $x++) {
                    $sx = $srcXStart + ($x * $xx) + ($y * $xy)
                    $sy = $srcYStart + ($x * $yx) + ($y * $yy)
                    if ($sx -lt 0 -or $sx -ge 2048 -or $sy -lt 0 -or $sy -ge 512) { continue }
                    $sampleX = $(if ($SwapTexelBytes) { $sx -bxor 1 } else { $sx })
                    $pixelIndex = [int]$Vram[($sy * 2048) + $sampleX]
                    $paletteOffset = $paletteByteStart + ($pixelIndex * 2)
                    if (($paletteOffset + 1) -ge $Vram.Length) { continue }
                    $color16 = [uint16]($Vram[$paletteOffset] -bor ($Vram[$paletteOffset + 1] -shl 8))
                    $bitmap.SetPixel($baseX + $destTileX + $x, $baseY + $destTileY + $y, (Convert-Psx555ToColor $color16))
                }
            }
        }
    }

    $resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
    $bitmap.Save($resolvedOut, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return $resolvedOut
}

function Get-ScanSamples($TextureIndex) {
    $priorityIds = @(40, 32, 18, 29, 11, 16, 15, 19, 24, 23, 27, 21, 4, 20, 6, 13, 0, 1, 2, 3)
    $xs = New-Object System.Collections.Generic.List[int]
    $ys = New-Object System.Collections.Generic.List[int]
    $palettes = New-Object System.Collections.Generic.List[int]
    foreach ($textureId in $priorityIds) {
        if ($textureId -lt 0 -or $textureId -ge [int]$TextureIndex.textureCount) { continue }
        $record = $TextureIndex.records[$textureId]
        foreach ($hq in @($record.hqData)) {
            [void]$xs.Add([int]$hq.vramXMin)
            [void]$ys.Add([int]$hq.vramYMin)
            [void]$palettes.Add([int]$hq.palette)
        }
    }
    return [pscustomobject]@{
        xs = [int[]]$xs.ToArray()
        ys = [int[]]$ys.ToArray()
        palettes = [int[]]$palettes.ToArray()
    }
}

function Add-ZstdNativeType([string]$DllPath) {
    if ("StoneHillZstdNative" -as [type]) { return }
    $resolvedDll = (Resolve-Path -LiteralPath $DllPath -ErrorAction Stop).Path
    $escapedDll = $resolvedDll.Replace("\", "\\")
    $source = @"
using System;
using System.Runtime.InteropServices;

public static class StoneHillZstdNative {
    private const ulong ZSTD_CONTENTSIZE_ERROR = 0xFFFFFFFFFFFFFFFFUL;
    private const ulong ZSTD_CONTENTSIZE_UNKNOWN = 0xFFFFFFFFFFFFFFFEUL;

    [DllImport("$escapedDll", CallingConvention=CallingConvention.Cdecl)]
    private static extern UIntPtr ZSTD_decompress(byte[] dst, UIntPtr dstCapacity, byte[] src, UIntPtr compressedSize);

    [DllImport("$escapedDll", CallingConvention=CallingConvention.Cdecl)]
    private static extern ulong ZSTD_getFrameContentSize(byte[] src, UIntPtr srcSize);

    [DllImport("$escapedDll", CallingConvention=CallingConvention.Cdecl)]
    private static extern uint ZSTD_isError(UIntPtr code);

    [DllImport("$escapedDll", CallingConvention=CallingConvention.Cdecl)]
    private static extern IntPtr ZSTD_getErrorName(UIntPtr code);

    public static byte[] DecompressFrame(byte[] frame) {
        if (frame == null || frame.Length == 0)
            throw new InvalidOperationException("Empty Zstandard frame.");

        ulong contentSize = ZSTD_getFrameContentSize(frame, new UIntPtr((uint)frame.Length));
        if (contentSize == ZSTD_CONTENTSIZE_ERROR)
            throw new InvalidOperationException("Invalid Zstandard frame.");
        if (contentSize == ZSTD_CONTENTSIZE_UNKNOWN)
            throw new InvalidOperationException("Zstandard frame does not expose its decompressed size.");
        if (contentSize > int.MaxValue)
            throw new InvalidOperationException("Zstandard frame is too large for this extractor.");

        byte[] output = new byte[(int)contentSize];
        UIntPtr result = ZSTD_decompress(output, new UIntPtr(contentSize), frame, new UIntPtr((uint)frame.Length));
        if (ZSTD_isError(result) != 0) {
            string name = Marshal.PtrToStringAnsi(ZSTD_getErrorName(result));
            throw new InvalidOperationException("Zstandard decompression failed: " + name);
        }

        ulong actual = result.ToUInt64();
        if (actual != contentSize)
            Array.Resize(ref output, (int)actual);
        return output;
    }
}
"@
    Add-Type -TypeDefinition $source
}

function Expand-ZstdFrame([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    if ($Offset -lt 0 -or $Length -le 0 -or ($Offset + $Length) -gt $Bytes.Length) {
        throw "Invalid Zstandard frame range."
    }
    $frame = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $frame, 0, $Length)
    return [StoneHillZstdNative]::DecompressFrame($frame)
}

function Get-DuckStationSaveStateVram([string]$Path, [string]$DllPath) {
    Add-ZstdNativeType $DllPath

    $stateBytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
    $zstdMagic = [byte[]](0x28, 0xB5, 0x2F, 0xFD)
    $gpuLabel = [System.Text.Encoding]::ASCII.GetBytes("GPU-VRAM")
    $frameOffsets = @(Find-BytePatternOffsets $stateBytes $zstdMagic)
    if ($frameOffsets.Count -eq 0) { throw "No Zstandard frames found in DuckStation save-state." }

    for ($i = 0; $i -lt $frameOffsets.Count; $i++) {
        $frameOffset = [int]$frameOffsets[$i]
        $frameEnd = $(if (($i + 1) -lt $frameOffsets.Count) { [int]$frameOffsets[$i + 1] } else { [int]$stateBytes.Length })
        $frameLength = $frameEnd - $frameOffset
        try {
            $decompressed = Expand-ZstdFrame $stateBytes $frameOffset $frameLength
        }
        catch {
            continue
        }

        $labelOffset = Find-BytePattern $decompressed $gpuLabel 0
        if ($labelOffset -lt 0) { continue }

        # DuckStation's StateWrapper writes the section label immediately before
        # the raw PS1 VRAM bytes. Leading zeroes are valid VRAM data; do not skip
        # to the first nonzero byte or texture/palette lookups become misaligned.
        $payloadOffset = $labelOffset + $gpuLabel.Length
        if (($payloadOffset + $Script:Ps1VramSize) -gt $decompressed.Length) {
            throw "GPU-VRAM section was found, but there were not enough bytes after the section label."
        }

        $vram = New-Object byte[] $Script:Ps1VramSize
        [Array]::Copy($decompressed, $payloadOffset, $vram, 0, $Script:Ps1VramSize)
        return [pscustomobject]@{
            bytes = $vram
            frameIndex = $i
            compressedFrameOffset = $frameOffset
            decompressedFrameSize = $decompressed.Length
            gpuVramLabelOffset = $labelOffset
            gpuVramPayloadOffset = $payloadOffset
        }
    }

    throw "No GPU-VRAM section found in DuckStation save-state frames."
}

function Resolve-DuckStationProcess([string]$ProcessName, [int]$ProcessId) {
    if ($ProcessId -ne 0) { return Get-Process -Id $ProcessId -ErrorAction Stop }
    $processes = @(Get-Process | Where-Object { $_.ProcessName -like "$ProcessName*" -or $_.ProcessName -like "*$ProcessName*" } | Sort-Object WorkingSet64 -Descending)
    if ($processes.Count -eq 0) { throw "No running process matched '$ProcessName'." }
    return $processes[0]
}

$resolvedImage = (Resolve-Path -LiteralPath $ImagePath -ErrorAction Stop).Path
$modelInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $ModelSubfileIndex
$textureIndex = Decode-TextureRecords ([byte[]]$modelInfo.bytes)
$scanSamples = Get-ScanSamples $textureIndex

$scanResults = @()
$vram = $null
$vramSource = ""
$saveStateSource = ""
$saveStateVramInfo = $null
$selectedCandidate = $null
$wadTexturePagesInfo = $null

$explicitVramSources = 0
if (-not [string]::IsNullOrWhiteSpace($VramPath)) { $explicitVramSources++ }
if (-not [string]::IsNullOrWhiteSpace($SaveStatePath)) { $explicitVramSources++ }
if ($UseWadTexturePages) { $explicitVramSources++ }
if ($explicitVramSources -gt 1) {
    throw "Use only one VRAM source: -VramPath, -SaveStatePath, or -UseWadTexturePages."
}

if ($UseWadTexturePages) {
    $wadTexturePagesInfo = Get-ModelSubfileBytes $resolvedImage $WadLba $AssetWadIndex $TexturePagesSubfileIndex
    $vram = New-Object byte[] $Script:Ps1VramSize
    [Array]::Copy([byte[]]$wadTexturePagesInfo.bytes, 0, $vram, 0, [Math]::Min($Script:Ps1VramSize, ([byte[]]$wadTexturePagesInfo.bytes).Length))
    $resolvedOutVram = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutVramPath)
    [System.IO.File]::WriteAllBytes($resolvedOutVram, [byte[]]$vram)
    $vramSource = $resolvedOutVram
}
elseif (-not [string]::IsNullOrWhiteSpace($VramPath)) {
    $resolvedVram = (Resolve-Path -LiteralPath $VramPath -ErrorAction Stop).Path
    $vram = [System.IO.File]::ReadAllBytes($resolvedVram)
    $vramSource = $resolvedVram
}
elseif (-not [string]::IsNullOrWhiteSpace($SaveStatePath)) {
    $saveStateSource = (Resolve-Path -LiteralPath $SaveStatePath -ErrorAction Stop).Path
    $saveStateVramInfo = Get-DuckStationSaveStateVram $saveStateSource $ZstdDllPath
    $vram = [byte[]]$saveStateVramInfo.bytes
    $resolvedOutVram = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutVramPath)
    [System.IO.File]::WriteAllBytes($resolvedOutVram, [byte[]]$vram)
    $vramSource = $resolvedOutVram
}
else {
    $process = Resolve-DuckStationProcess $ProcessName $ProcessId
    Write-Host "Scanning process $($process.ProcessName) pid=$($process.Id) for a 1 MB PS1 VRAM image..."
    $scanResults = @([StoneHillVramScanner]::ScanProcess([int]$process.Id, $scanSamples.xs, $scanSamples.ys, $scanSamples.palettes, $MaxCandidates, $CoarseStep))
    if ($scanResults.Count -eq 0) { throw "No plausible VRAM candidates found in DuckStation process memory." }

    if ($ListCandidates) {
        $candidateRows = for ($i = 0; $i -lt $scanResults.Count; $i++) {
            $c = $scanResults[$i]
            [pscustomobject]@{
                Index = $i
                Address = ("0x{0:X}" -f [int64]$c.Address)
                Score = [int]$c.Score
                Smoothness = [int]$c.Smoothness
                Variation = [int]$c.Variation
                Palette = [int]$c.PaletteScore
                Samples = [int]$c.SampleCount
                Checksum = ("0x{0:X8}" -f ([int64]$c.Checksum -band 0xFFFFFFFFL))
            }
        }
        $candidateRows | Format-Table -AutoSize
        return
    }

    if ($CandidateIndex -lt 0 -or $CandidateIndex -ge $scanResults.Count) { throw "Candidate index $CandidateIndex is not available." }
    $selectedCandidate = $scanResults[$CandidateIndex]
    $vram = [StoneHillVramScanner]::ReadBytes([int]$process.Id, [int64]$selectedCandidate.Address, $Script:Ps1VramSize)
    $resolvedOutVram = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutVramPath)
    [System.IO.File]::WriteAllBytes($resolvedOutVram, [byte[]]$vram)
    $vramSource = $resolvedOutVram
}

$resolvedAtlas = Render-Atlas ([byte[]]$vram) $textureIndex $OutPath ([bool]$SwapTexelBytes) ([bool]$UseHqClose)

$scanRows = @()
foreach ($candidate in @($scanResults)) {
    $scanRows += [ordered]@{
        address = ("0x{0:X}" -f [int64]$candidate.Address)
        score = [int]$candidate.Score
        smoothness = [int]$candidate.Smoothness
        variation = [int]$candidate.Variation
        paletteScore = [int]$candidate.PaletteScore
        sampleCount = [int]$candidate.SampleCount
        checksum = ("0x{0:X8}" -f ([int64]$candidate.Checksum -band 0xFFFFFFFFL))
    }
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    sourceImage = $resolvedImage
    saveStateSource = $saveStateSource
    vramSource = $vramSource
    outputImage = $resolvedAtlas
    textureCount = [int]$textureIndex.textureCount
    atlasWidth = $(if ($UseHqClose) { 1024 } else { 512 })
    atlasHeight = ([int][Math]::Ceiling([int]$textureIndex.textureCount / 8.0) * $(if ($UseHqClose) { 128 } else { 64 }))
    textureTileSize = $(if ($UseHqClose) { 128 } else { 64 })
    textureDescriptorTier = $(if ($UseHqClose) { "hqDataClose" } else { "hqData" })
    layout = $(if ($UseHqClose) { "8 textures per row, 128x128 per texture, sixteen 32x32 HQ-close tiles per texture" } else { "8 textures per row, 64x64 per texture, four 32x32 HQ tiles per texture" })
    swapTexelBytes = [bool]$SwapTexelBytes
    wadTexturePages = $(if ($null -ne $wadTexturePagesInfo) {
        [ordered]@{
            assetWadIndex = $AssetWadIndex
            subfileIndex = $TexturePagesSubfileIndex
            subfileOffset = [int64]$wadTexturePagesInfo.subfileOffset
            subfileSize = [int64]$wadTexturePagesInfo.subfileSize
            paddedToBytes = $Script:Ps1VramSize
        }
    } else { $null })
    selectedCandidate = $(if ($null -ne $selectedCandidate) {
        [ordered]@{
            address = ("0x{0:X}" -f [int64]$selectedCandidate.Address)
            score = [int]$selectedCandidate.Score
            smoothness = [int]$selectedCandidate.Smoothness
            variation = [int]$selectedCandidate.Variation
            paletteScore = [int]$selectedCandidate.PaletteScore
            sampleCount = [int]$selectedCandidate.SampleCount
            checksum = ("0x{0:X8}" -f ([int64]$selectedCandidate.Checksum -band 0xFFFFFFFFL))
        }
    } else { $null })
    saveStateVram = $(if ($null -ne $saveStateVramInfo) {
        [ordered]@{
            frameIndex = [int]$saveStateVramInfo.frameIndex
            compressedFrameOffset = ("0x{0:X}" -f [int]$saveStateVramInfo.compressedFrameOffset)
            decompressedFrameSize = [int]$saveStateVramInfo.decompressedFrameSize
            gpuVramLabelOffset = ("0x{0:X}" -f [int]$saveStateVramInfo.gpuVramLabelOffset)
            gpuVramPayloadOffset = ("0x{0:X}" -f [int]$saveStateVramInfo.gpuVramPayloadOffset)
        }
    } else { $null })
    scanCandidates = @($scanRows)
    textureIndex = [ordered]@{
        textureListSize = [int]$textureIndex.textureListSize
        recordBytes = [int]$textureIndex.recordBytes
        source = "Stone Hill model subfile TexHq descriptors"
    }
    sourceReferences = @(
        "https://github.com/LXShades/spyroedit/blob/e311b00721d427757be9e2b794788da013eae167/Source/SpyroTextures.h",
        "https://github.com/LXShades/spyroedit/blob/e311b00721d427757be9e2b794788da013eae167/Source/SpyroTextures.cpp"
    )
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

Write-Host ("Wrote Stone Hill texture atlas to {0}" -f $resolvedAtlas)
Write-Host ("Wrote atlas metadata to {0}" -f $resolvedJson)
if ($vramSource) { Write-Host ("VRAM source: {0}" -f $vramSource) }
