param(
    [string]$OutPath = ".\spyro-mainram-dump.bin",
    [string]$ProcessName = "duckstation",
    [int]$ProcessId = 0,
    [int]$CandidateIndex = -1,
    [int]$MaxCandidates = 8,
    [string]$OutPrefix = "",
    [switch]$ListCandidates,
    [switch]$DumpAllCandidates,
    [switch]$ListOnly
)

Set-StrictMode -Version 2.0

$source = @"
using System;
using System.Runtime.InteropServices;

public static class ProcessMemoryNative {
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
}

public struct SpyroRamScanResult {
    public bool Found;
    public int Offset;
    public uint Pointer;
    public int MobyCount;
    public int Score;
    public int NonZeroCount;
    public int Checksum;
}

public static class SpyroRamScanner {
    private const int MainRamSize = 2 * 1024 * 1024;
    private const int PointerOffset = 0x75828;

    private static uint GetUInt32LE(byte[] bytes, int offset) {
        if (offset < 0 || offset + 4 > bytes.Length)
            return 0;
        return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
    }

    private static int GetInt32LE(byte[] bytes, int offset) {
        if (offset < 0 || offset + 4 > bytes.Length)
            return 0;
        return BitConverter.ToInt32(bytes, offset);
    }

    private static bool IsPsxPointer(uint value) {
        return value >= 0x80000000u && value < 0x80200000u && (value & 0x3u) == 0u;
    }

    private static int PointerToOffset(uint value) {
        return IsPsxPointer(value) ? (int)(value - 0x80000000u) : -1;
    }

    private static bool IsPlausibleMoby(byte[] ram, int offset) {
        if (offset < 0 || offset + 0x50 > ram.Length)
            return false;

        int type = ram[offset + 0x48];
        int state = ram[offset + 0x49];
        if (type <= 0 || type > 0x7F || state > 0x7F)
            return false;

        uint special = GetUInt32LE(ram, offset);
        if (special != 0 && !IsPsxPointer(special))
            return false;

        int x = GetInt32LE(ram, offset + 4);
        int y = GetInt32LE(ram, offset + 8);
        int z = GetInt32LE(ram, offset + 12);
        if (Math.Abs((long)x) > 4000000L || Math.Abs((long)y) > 4000000L || Math.Abs((long)z) > 4000000L)
            return false;

        if (Math.Abs((long)x) < 16L && Math.Abs((long)y) < 16L && Math.Abs((long)z) < 16L)
            return false;

        return true;
    }

    private static int CountPlausibleMobys(byte[] ram, uint pointer) {
        int start = PointerToOffset(pointer);
        if (start < 0 || start + 0x50 > ram.Length)
            return 0;

        int count = 0;
        int badRun = 0;
        for (int i = 0; i < 512; i++) {
            int offset = start + (i * 0x50);
            if (offset + 0x50 > ram.Length)
                break;

            if (IsPlausibleMoby(ram, offset)) {
                count++;
                badRun = 0;
            } else {
                badRun++;
                if (count > 8 && badRun >= 24)
                    break;
            }
        }
        return count;
    }

    public static SpyroRamScanResult FindWindow(byte[] bytes) {
        SpyroRamScanResult best = new SpyroRamScanResult();
        if (bytes == null || bytes.Length < MainRamSize)
            return best;

        int maxCandidate = bytes.Length - MainRamSize;
        int[] steps = new int[] { 0x1000, 0x100, 0x10, 4 };
        foreach (int step in steps) {
            for (int candidate = 0; candidate <= maxCandidate; candidate += step) {
                uint pointer = GetUInt32LE(bytes, candidate + PointerOffset);
                if (!IsPsxPointer(pointer))
                    continue;

                byte[] ram = new byte[MainRamSize];
                Buffer.BlockCopy(bytes, candidate, ram, 0, MainRamSize);
                int mobyCount = CountPlausibleMobys(ram, pointer);
                if (mobyCount <= 0)
                    continue;

                int score = mobyCount;
                if (!best.Found || score > best.Score) {
                    best.Found = true;
                    best.Offset = candidate;
                    best.Pointer = pointer;
                    best.MobyCount = mobyCount;
                    best.Score = score;
                    if (score >= 160)
                        return best;
                }
            }
        }
        return best;
    }

    public static SpyroRamScanResult[] FindWindows(byte[] bytes, int maxResults) {
        if (bytes == null || bytes.Length < MainRamSize)
            return new SpyroRamScanResult[0];

        SpyroRamScanResult[] results = new SpyroRamScanResult[Math.Max(1, maxResults)];
        int resultCount = 0;
        int maxCandidate = bytes.Length - MainRamSize;

        for (int candidate = 0; candidate <= maxCandidate; candidate += 0x1000) {
            uint pointer = GetUInt32LE(bytes, candidate + PointerOffset);
            if (!IsPsxPointer(pointer))
                continue;

            byte[] ram = new byte[MainRamSize];
            Buffer.BlockCopy(bytes, candidate, ram, 0, MainRamSize);
            int mobyCount = CountPlausibleMobys(ram, pointer);
            if (mobyCount <= 0)
                continue;

            SpyroRamScanResult item = new SpyroRamScanResult();
            item.Found = true;
            item.Offset = candidate;
            item.Pointer = pointer;
            item.MobyCount = mobyCount;
            item.Score = mobyCount;
            item.NonZeroCount = CountNonZero(ram);
            item.Checksum = FastChecksum(ram);

            if (resultCount < results.Length) {
                results[resultCount++] = item;
            } else {
                int worstIndex = 0;
                for (int i = 1; i < results.Length; i++) {
                    if (results[i].Score < results[worstIndex].Score)
                        worstIndex = i;
                }
                if (item.Score > results[worstIndex].Score)
                    results[worstIndex] = item;
            }
        }

        SpyroRamScanResult[] trimmed = new SpyroRamScanResult[resultCount];
        Array.Copy(results, trimmed, resultCount);
        Array.Sort(trimmed, delegate(SpyroRamScanResult a, SpyroRamScanResult b) {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            return b.NonZeroCount.CompareTo(a.NonZeroCount);
        });
        return trimmed;
    }

    private static int CountNonZero(byte[] bytes) {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++) {
            if (bytes[i] != 0)
                count++;
        }
        return count;
    }

    private static int FastChecksum(byte[] bytes) {
        unchecked {
            int hash = (int)2166136261u;
            for (int i = 0; i < bytes.Length; i += 16) {
                hash ^= bytes[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
"@

Add-Type -TypeDefinition $source

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [uint32]($Bytes[$Offset] -bor ($Bytes[$Offset + 1] -shl 8) -bor ($Bytes[$Offset + 2] -shl 16) -bor ($Bytes[$Offset + 3] -shl 24))
}

function Get-Int32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Test-PsxPointer([uint32]$Value) {
    return ($Value -ge 0x80000000 -and $Value -lt 0x80200000 -and (($Value -band [uint32]3) -eq 0))
}

function Convert-PsxPointerToOffset([uint32]$Value) {
    if (-not (Test-PsxPointer $Value)) { return -1 }
    return [int]($Value - 0x80000000)
}

function Test-PlausibleMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 0x50) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x48]
    $state = [int]$Ram[$Offset + 0x49]
    if ($type -le 0 -or $type -gt 0x7F -or $state -gt 0x7F) { return $false }

    $special = Get-UInt32LE $Ram $Offset
    if ($special -ne 0 -and -not (Test-PsxPointer $special)) { return $false }

    $x = Get-Int32LE $Ram ($Offset + 4)
    $y = Get-Int32LE $Ram ($Offset + 8)
    $z = Get-Int32LE $Ram ($Offset + 12)
    if ([Math]::Abs($x) -gt 4000000 -or [Math]::Abs($y) -gt 4000000 -or [Math]::Abs($z) -gt 4000000) { return $false }
    if ([Math]::Abs($x) -lt 16 -and [Math]::Abs($y) -lt 16 -and [Math]::Abs($z) -lt 16) { return $false }

    return $true
}

function Count-PlausibleMobys([byte[]]$Ram, [uint32]$Pointer) {
    $start = Convert-PsxPointerToOffset $Pointer
    if ($start -lt 0 -or ($start + 0x50) -gt $Ram.Length) { return 0 }

    $count = 0
    $badRun = 0
    for ($i = 0; $i -lt 512; $i++) {
        $offset = $start + ($i * 0x50)
        if (($offset + 0x50) -gt $Ram.Length) { break }
        if (Test-PlausibleMoby $Ram $offset) {
            $count++
            $badRun = 0
        }
        else {
            $badRun++
            if ($count -gt 8 -and $badRun -ge 24) { break }
        }
    }
    return $count
}

function Find-SpyroRamWindow([byte[]]$Bytes) {
    $mainRamSize = 2MB
    if ($Bytes.Length -lt $mainRamSize) { return $null }

    $scan = [SpyroRamScanner]::FindWindow($Bytes)
    if (-not $scan.Found) { return $null }

    $ram = New-Object byte[] $mainRamSize
    [Array]::Copy($Bytes, $scan.Offset, $ram, 0, $mainRamSize)
    return [ordered]@{
        offset = [int]$scan.Offset
        pointer = [uint32]$scan.Pointer
        mobyCount = [int]$scan.MobyCount
        score = [int]$scan.Score
        ram = $ram
    }
}

function Get-SpyroRamWindows([byte[]]$Bytes, [int]$MaxResults = 16) {
    $mainRamSize = 2MB
    if ($Bytes.Length -lt $mainRamSize) { return @() }
    $scans = [SpyroRamScanner]::FindWindows($Bytes, $MaxResults)
    $windows = @()
    foreach ($scan in $scans) {
        if (-not $scan.Found) { continue }
        $ram = New-Object byte[] $mainRamSize
        [Array]::Copy($Bytes, $scan.Offset, $ram, 0, $mainRamSize)
        $windows += [ordered]@{
            offset = [int]$scan.Offset
            pointer = [uint32]$scan.Pointer
            mobyCount = [int]$scan.MobyCount
            score = [int]$scan.Score
            nonZeroCount = [int]$scan.NonZeroCount
            checksum = [int]$scan.Checksum
            ram = $ram
        }
    }
    return $windows
}

function Test-ReadableProtect([uint32]$Protect) {
    $PAGE_NOACCESS = 0x01
    $PAGE_GUARD = 0x100
    if (($Protect -band $PAGE_NOACCESS) -ne 0) { return $false }
    if (($Protect -band $PAGE_GUARD) -ne 0) { return $false }
    return $true
}

$processes = @(Get-Process | Where-Object { $_.ProcessName -like "$ProcessName*" -or $_.ProcessName -like "*$ProcessName*" } | Sort-Object WorkingSet64 -Descending)
if ($processes.Count -eq 0) {
    throw "No running process matched '$ProcessName'. Start DuckStation, load into the level, then run this script."
}

if ($ListOnly) {
    $processes | Select-Object Id, ProcessName, MainWindowTitle, WorkingSet64 | Format-Table -AutoSize
    return
}

if ($ProcessId -ne 0) {
    $process = Get-Process -Id $ProcessId -ErrorAction Stop
}
else {
    $process = $processes[0]
}
Write-Host "Using process $($process.ProcessName) pid=$($process.Id)"

$PROCESS_QUERY_INFORMATION = 0x0400
$PROCESS_VM_READ = 0x0010
$handle = [ProcessMemoryNative]::OpenProcess(($PROCESS_QUERY_INFORMATION -bor $PROCESS_VM_READ), $false, $process.Id)
if ($handle -eq [IntPtr]::Zero) {
    throw "Could not open process memory. Try running PowerShell as Administrator."
}

try {
    $address = [int64]0
    $maxAddress = [int64]0x7FFFFFFF0000
    $mbiSize = [UIntPtr]::new([uint64][Runtime.InteropServices.Marshal]::SizeOf([type][ProcessMemoryNative+MEMORY_BASIC_INFORMATION]))
    $best = $null
    $allCandidates = @()
    $scannedRegions = 0
    $readableRegions = 0
    $readableBytes = [int64]0

    while ($address -lt $maxAddress) {
        $info = New-Object ProcessMemoryNative+MEMORY_BASIC_INFORMATION
        $result = [ProcessMemoryNative]::VirtualQueryEx($handle, [IntPtr]$address, [ref]$info, $mbiSize)
        if ($result -eq [IntPtr]::Zero) { break }

        $base = $info.BaseAddress.ToInt64()
        $size = $info.RegionSize.ToInt64()
        $next = $base + [Math]::Max($size, 0x1000)

        $MEM_COMMIT = 0x1000
        if ($info.State -eq $MEM_COMMIT -and (Test-ReadableProtect $info.Protect) -and $size -ge 2MB) {
            $readableRegions++
            $chunkSize = 64MB
            $overlap = 2MB
            $regionOffset = [int64]0
            while ($regionOffset -lt $size) {
                $remaining = $size - $regionOffset
                $currentSize = [int][Math]::Min($chunkSize, $remaining)
                if ($currentSize -lt 2MB) { break }

                $buffer = New-Object byte[] $currentSize
                $bytesRead = [UIntPtr]::Zero
                $readSize = [UIntPtr]::new([uint64]$buffer.Length)
                $chunkBase = $base + $regionOffset
                if ([ProcessMemoryNative]::ReadProcessMemory($handle, [IntPtr]$chunkBase, $buffer, $readSize, [ref]$bytesRead)) {
                    $scannedRegions++
                    $readableBytes += [int64]$buffer.Length
                    $foundWindows = @(Get-SpyroRamWindows $buffer ([Math]::Max(16, $MaxCandidates * 2)))
                    foreach ($found in $foundWindows) {
                        $found.processBase = $chunkBase
                        $found.absoluteAddress = [int64]$chunkBase + [int64]$found.offset
                        $allCandidates += $found
                        if ($null -eq $best -or $found.score -gt $best.score -or ($found.score -eq $best.score -and $found.nonZeroCount -gt $best.nonZeroCount)) {
                            $best = $found
                        }
                    }
                }

                if ($remaining -le $chunkSize) { break }
                $regionOffset += ($chunkSize - $overlap)
            }
        }

        if ($next -le $address) { break }
        $address = $next
    }

    $rankedCandidates = @($allCandidates | Sort-Object -Property @{ Expression = { $_.score }; Descending = $true }, @{ Expression = { $_.nonZeroCount }; Descending = $true }, absoluteAddress | Select-Object -First ([Math]::Max(1, $MaxCandidates)))

    if ($ListCandidates) {
        if ($rankedCandidates.Count -eq 0) {
            Write-Host "No candidate Spyro RAM windows found."
        }
        else {
            $display = for ($i = 0; $i -lt $rankedCandidates.Count; $i++) {
                $candidate = $rankedCandidates[$i]
                [pscustomobject]@{
                    Index = $i
                    Address = ("0x{0:X}" -f [int64]$candidate.absoluteAddress)
                    Pointer = ("0x{0:X8}" -f [uint32]$candidate.pointer)
                    Mobys = [int]$candidate.mobyCount
                    NonZero = [int]$candidate.nonZeroCount
                    Checksum = ("0x{0:X8}" -f ([int64]$candidate.checksum -band 0xFFFFFFFFL))
                }
            }
            $display | Format-Table -AutoSize
        }
        return
    }

    if ($DumpAllCandidates) {
        if ($rankedCandidates.Count -eq 0) {
            throw "No candidate Spyro RAM windows found to dump."
        }
        $prefix = $OutPrefix
        if ([string]::IsNullOrWhiteSpace($prefix)) {
            $withoutExt = [System.IO.Path]::Combine(
                [System.IO.Path]::GetDirectoryName($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)),
                [System.IO.Path]::GetFileNameWithoutExtension($OutPath)
            )
            $prefix = $withoutExt
        }
        $resolvedPrefix = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($prefix)
        $manifest = @()
        for ($i = 0; $i -lt $rankedCandidates.Count; $i++) {
            $candidate = $rankedCandidates[$i]
            $candidatePath = "{0}-candidate{1:D2}.bin" -f $resolvedPrefix, $i
            [System.IO.File]::WriteAllBytes($candidatePath, [byte[]]$candidate.ram)
            $manifest += [ordered]@{
                index = $i
                path = $candidatePath
                address = ("0x{0:X}" -f [int64]$candidate.absoluteAddress)
                pointer = ("0x{0:X8}" -f [uint32]$candidate.pointer)
                mobys = [int]$candidate.mobyCount
                nonZero = [int]$candidate.nonZeroCount
                checksum = ("0x{0:X8}" -f ([int64]$candidate.checksum -band 0xFFFFFFFFL))
            }
        }
        $manifestPath = "$resolvedPrefix-candidates.json"
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        Write-Host "Dumped $($rankedCandidates.Count) candidate RAM windows."
        Write-Host "Manifest: $manifestPath"
        return
    }

    if ($CandidateIndex -ge 0) {
        if ($CandidateIndex -ge $rankedCandidates.Count) {
            throw "Candidate index $CandidateIndex is not available. Run with -ListCandidates to see valid indexes."
        }
        $best = $rankedCandidates[$CandidateIndex]
    }

    if ($null -eq $best) {
        throw "Could not find Spyro's live 2 MB main RAM window in process $($process.ProcessName) pid=$($process.Id). Scanned $scannedRegions readable chunk(s) from $readableRegions region(s), about $([Math]::Round($readableBytes / 1MB, 1)) MB. Run this script with -ListOnly and, if more than one DuckStation process appears, retry with -ProcessId <pid>."
    }

    $resolvedOut = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPath)
    [System.IO.File]::WriteAllBytes($resolvedOut, [byte[]]$best.ram)
    Write-Host ("Wrote {0} bytes to {1}" -f $best.ram.Length, $resolvedOut)
    Write-Host ("Runtime moby pointer: 0x{0:X8}; plausible mobys: {1}; process address: 0x{2:X}; candidate index: {3}" -f [uint32]$best.pointer, [int]$best.mobyCount, ([int64]$best.processBase + [int64]$best.offset), $CandidateIndex)
    Write-Host "If before/after dumps are identical, run with -ListCandidates and retry using a different -CandidateIndex."
}
finally {
    [void][ProcessMemoryNative]::CloseHandle($handle)
}
