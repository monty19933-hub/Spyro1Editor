param(
    [int]$MobyIndex = -1,
    [int[]]$MobyIndexes = @(),
    [double]$DeltaX = 0,
    [double]$DeltaY = 0,
    [double]$DeltaZ = 0,
    [switch]$FromNativeEdit,
    [string]$NativeEditsPath = ".\stonehill-native-edits.json",
    [string]$OriginalsPath = ".\stonehill-live-moby-originals.json",
    [string]$ProcessName = "duckstation",
    [int]$ProcessId = 0,
    [int]$CandidateIndex = -1,
    [int]$MaxCandidates = 8,
    [int]$ExpectedLevelId = -1,
    [string]$CatalogPath = "",
    [switch]$ListOnly,
    [switch]$ListCandidates,
    [switch]$ListMobys,
    [switch]$Apply,
    [switch]$Revert,
    [switch]$RevertAll,
    [double]$HoldSeconds = 0,
    [int]$IntervalMs = 100
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

$source = @"
using System;
using System.Runtime.InteropServices;

public static class DuckStationMobyMoveNative {
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

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, UIntPtr size, out UIntPtr bytesWritten);
}

public struct DuckStationMobyRamScanResult {
    public bool Found;
    public int Offset;
    public uint Pointer;
    public int MobyCount;
    public int Score;
    public int NonZeroCount;
    public int Checksum;
}

public static class DuckStationMobyRamScanner {
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

    public static DuckStationMobyRamScanResult[] FindWindows(byte[] bytes, int maxResults) {
        if (bytes == null || bytes.Length < MainRamSize)
            return new DuckStationMobyRamScanResult[0];

        DuckStationMobyRamScanResult[] results = new DuckStationMobyRamScanResult[Math.Max(1, maxResults)];
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

            DuckStationMobyRamScanResult item = new DuckStationMobyRamScanResult();
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

        DuckStationMobyRamScanResult[] trimmed = new DuckStationMobyRamScanResult[resultCount];
        Array.Copy(results, trimmed, resultCount);
        Array.Sort(trimmed, delegate(DuckStationMobyRamScanResult a, DuckStationMobyRamScanResult b) {
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

function Convert-PsxPointerToOffset([uint32]$Value) {
    $v = [uint32]$Value
    $ramBase = [uint32]2147483648
    $ramEnd = [uint32]2149580800
    if ($v -lt $ramBase -or $v -ge $ramEnd -or (($v -band [uint32]3) -ne 0)) { return -1 }
    return [int]($v - $ramBase)
}

function Test-PlausibleMoby([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 0x58) -gt $Ram.Length) { return $false }
    $type = [int]$Ram[$Offset + 0x50]
    $state = [int]$Ram[$Offset + 0x51]
    if ($type -le 0 -or $type -gt 0x7F -or $state -gt 0x7F) { return $false }
    $x = Get-Int32LE $Ram ($Offset + 0x0C)
    $y = Get-Int32LE $Ram ($Offset + 0x10)
    $z = Get-Int32LE $Ram ($Offset + 0x14)
    if ([Math]::Abs($x) -gt 4000000 -or [Math]::Abs($y) -gt 4000000 -or [Math]::Abs($z) -gt 4000000) { return $false }
    if ([Math]::Abs($x) -lt 16 -and [Math]::Abs($y) -lt 16 -and [Math]::Abs($z) -lt 16) { return $false }
    return $true
}

function Test-ReadableProtect([uint32]$Protect) {
    $PAGE_NOACCESS = 0x01
    $PAGE_GUARD = 0x100
    if (($Protect -band $PAGE_NOACCESS) -ne 0) { return $false }
    if (($Protect -band $PAGE_GUARD) -ne 0) { return $false }
    return $true
}

function Get-ProcessCandidates {
    $processes = @(Get-Process | Where-Object { $_.ProcessName -like "$ProcessName*" -or $_.ProcessName -like "*$ProcessName*" } | Sort-Object WorkingSet64 -Descending)
    if ($processes.Count -eq 0) {
        throw "No running process matched '$ProcessName'. Start DuckStation, load into Stone Hill, then run this script."
    }
    return $processes
}

function Get-SpyroRamWindows([byte[]]$Bytes, [int]$MaxResults = 16) {
    $mainRamSize = 2MB
    if ($Bytes.Length -lt $mainRamSize) { return @() }
    $scans = [DuckStationMobyRamScanner]::FindWindows($Bytes, $MaxResults)
    $windows = @()
    foreach ($scan in $scans) {
        if (-not $scan.Found) { continue }
        $ram = New-Object byte[] $mainRamSize
        [Array]::Copy($Bytes, $scan.Offset, $ram, 0, $mainRamSize)
        $windows += [ordered]@{
            offset = [int]$scan.Offset
            pointer = [uint32]$scan.Pointer
            levelId = [uint32](Get-UInt32LE $ram 0x758B4)
            mobyCount = [int]$scan.MobyCount
            score = [int]$scan.Score
            nonZeroCount = [int]$scan.NonZeroCount
            checksum = [int]$scan.Checksum
            ram = $ram
        }
    }
    return $windows
}

function Find-LiveRamWindow($Handle) {
    $address = [int64]0
    $maxAddress = [int64]0x7FFFFFFF0000
    $mbiSize = [UIntPtr]::new([uint64][Runtime.InteropServices.Marshal]::SizeOf([type][DuckStationMobyMoveNative+MEMORY_BASIC_INFORMATION]))
    $allCandidates = @()
    $chunkSize = 64MB
    $overlap = 2MB

    while ($address -lt $maxAddress) {
        $info = New-Object DuckStationMobyMoveNative+MEMORY_BASIC_INFORMATION
        $result = [DuckStationMobyMoveNative]::VirtualQueryEx($Handle, [IntPtr]$address, [ref]$info, $mbiSize)
        if ($result -eq [IntPtr]::Zero) { break }

        $base = $info.BaseAddress.ToInt64()
        $size = $info.RegionSize.ToInt64()
        $next = $base + [Math]::Max($size, 0x1000)

        $MEM_COMMIT = 0x1000
        if ($info.State -eq $MEM_COMMIT -and (Test-ReadableProtect $info.Protect) -and $size -ge 2MB) {
            $regionOffset = [int64]0
            while ($regionOffset -lt $size) {
                $remaining = $size - $regionOffset
                $currentSize = [int][Math]::Min($chunkSize, $remaining)
                if ($currentSize -lt 2MB) { break }

                $buffer = New-Object byte[] $currentSize
                $bytesRead = [UIntPtr]::Zero
                $readSize = [UIntPtr]::new([uint64]$buffer.Length)
                $chunkBase = $base + $regionOffset
                if ([DuckStationMobyMoveNative]::ReadProcessMemory($Handle, [IntPtr]$chunkBase, $buffer, $readSize, [ref]$bytesRead)) {
                    $foundWindows = @(Get-SpyroRamWindows $buffer ([Math]::Max(16, $MaxCandidates * 2)))
                    foreach ($found in $foundWindows) {
                        $found.processBase = $chunkBase
                        $found.absoluteAddress = [int64]$chunkBase + [int64]$found.offset
                        $allCandidates += $found
                    }
                }

                if ($remaining -le $chunkSize) { break }
                $regionOffset += ($chunkSize - $overlap)
            }
        }

        if ($next -le $address) { break }
        $address = $next
    }

    $ranked = @($allCandidates | Sort-Object -Property @{ Expression = { $_.score }; Descending = $true }, @{ Expression = { $_.nonZeroCount }; Descending = $true }, absoluteAddress | Select-Object -First ([Math]::Max(1, $MaxCandidates)))
    if ($ListCandidates) {
        $rows = @()
        for ($i = 0; $i -lt $ranked.Count; $i++) {
            $candidate = $ranked[$i]
            $rows += [pscustomobject]@{
                Index = $i
                Address = ("0x{0:X}" -f [int64]$candidate.absoluteAddress)
                Pointer = ("0x{0:X8}" -f [uint32]$candidate.pointer)
                Level = if ($candidate.Contains("levelId")) { ("0x{0:X2}" -f [uint32]$candidate.levelId) } else { "" }
                Mobys = [int]$candidate.mobyCount
                NonZero = [int]$candidate.nonZeroCount
                Checksum = ("0x{0:X8}" -f ([int64]$candidate.checksum -band 0xFFFFFFFFL))
            }
        }
        if ($rows.Count -eq 0) {
            Write-Host "No candidate Spyro RAM windows found."
        }
        else {
            $rows | Format-Table -AutoSize | Out-Host
        }
        return $null
    }
    if ($ranked.Count -eq 0) {
        throw "Could not find Spyro's live 2 MB main RAM window. Make sure DuckStation is running in the expected Spyro level."
    }
    if ($ExpectedLevelId -ge 0) {
        $ranked = @($ranked | Where-Object { $_.Contains("levelId") -and ([uint32]$_.levelId -eq [uint32]$ExpectedLevelId) })
        if ($ranked.Count -eq 0) {
            throw ("Could not find a live Spyro RAM window for expected level 0x{0:X2}. Run with -ListCandidates to inspect candidates." -f [uint32]$ExpectedLevelId)
        }
    }
    if ($CandidateIndex -ge 0) {
        if ($CandidateIndex -ge $ranked.Count) {
            throw "Candidate index $CandidateIndex is not available. Run with -ListCandidates to see valid indexes."
        }
        return $ranked[$CandidateIndex]
    }
    return $ranked[0]
}

function Read-MobyRecord($Window, [int]$Index) {
    $start = Convert-PsxPointerToOffset ([uint32]$Window.pointer)
    if ($start -lt 0) { throw "_ptr_levelMobys is not a valid PS1 RAM pointer." }
    $offset = $start + ($Index * 0x58)
    if (($offset + 0x58) -gt $Window.ram.Length) { throw "Moby index $Index is outside the live moby table." }
    if (-not (Test-PlausibleMoby $Window.ram $offset)) {
        throw "Moby index $Index is not currently a plausible live moby record."
    }
    return [ordered]@{
        index = $Index
        offset = $offset
        absoluteAddress = ([int64]$Window.absoluteAddress + [int64]$offset)
        type = [int]$Window.ram[$offset + 0x50]
        state = [int]$Window.ram[$offset + 0x51]
        rawX = Get-Int32LE $Window.ram ($offset + 0x0C)
        rawY = Get-Int32LE $Window.ram ($offset + 0x10)
        rawZ = Get-Int32LE $Window.ram ($offset + 0x14)
    }
}

function Load-Originals([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return [ordered]@{ generatedAt = (Get-Date).ToString("s"); originals = @() }
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Save-Originals([string]$Path, $Store) {
    $Store.generatedAt = (Get-Date).ToString("s")
    $Store | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-OriginalRecord($Store, [int]$Index) {
    foreach ($item in @(Get-ArrayField $Store "originals")) {
        if ([int]$item.index -eq $Index) { return $item }
    }
    return $null
}

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop -or $null -eq $prop.Value) { return @() }
    if ($prop.Value -is [System.Array]) { return @($prop.Value) }
    return @($prop.Value)
}

function Add-OriginalRecord($Store, $Record) {
    if ($null -ne (Get-OriginalRecord $Store ([int]$Record.index))) { return }
    $items = New-Object System.Collections.ArrayList
    foreach ($item in @(Get-ArrayField $Store "originals")) { [void]$items.Add($item) }
    [void]$items.Add([ordered]@{
        index = [int]$Record.index
        type = ("0x{0:X2}" -f [int]$Record.type)
        state = ("0x{0:X2}" -f [int]$Record.state)
        rawX = [int]$Record.rawX
        rawY = [int]$Record.rawY
        rawZ = [int]$Record.rawZ
        savedAt = (Get-Date).ToString("s")
    })
    $Store.originals = @($items.ToArray())
}

function Get-NativeEditTarget([int]$Index, [string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Native edit file not found: $Path" }
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($edit in @(Get-ArrayField $json "edits")) {
        $editIndex = if ($null -ne $edit.PSObject.Properties["index"]) { [int]$edit.index } else { -1 }
        $editTrueIndex = if ($null -ne $edit.PSObject.Properties["trueIndex"]) { [int]$edit.trueIndex } else { -1 }
        if ($editTrueIndex -ne $Index -and $editIndex -ne $Index) { continue }
        $raw = $edit.rawEdited
        if ($null -eq $raw) { throw "Native edit for T$Index does not contain rawEdited coordinates." }
        return [ordered]@{
            rawX = [int]$raw.x
            rawY = [int]$raw.y
            rawZ = [int]$raw.z
            label = [string]$edit.label
        }
    }
    throw "No saved native edit was found for L$Index."
}

function Get-MobyLabelMap {
    $map = @{}
    $catalogPaths = if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
        @(".\stonehill-moby-catalog.json", ".\stonehill-live-validation-overrides.json")
    }
    else {
        @($CatalogPath)
    }
    foreach ($relativePath in $catalogPaths) {
        $catalogPath = Resolve-WorkspacePath $relativePath
        if (-not (Test-Path -LiteralPath $catalogPath)) { continue }
        $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
        foreach ($moby in @(Get-ArrayField $catalog "mobys")) {
            $label = [string]$moby.displayTargetLabel
            if (-not [string]::IsNullOrWhiteSpace($label)) {
                $key = if ($null -ne $moby.PSObject.Properties["trueIndex"]) { [int]$moby.trueIndex } else { [int]$moby.index }
                $map[$key] = $label
            }
        }
    }
    return $map
}

function Write-Int32($Handle, [int64]$Address, [int]$Value) {
    $bytes = [BitConverter]::GetBytes([int32]$Value)
    $written = [UIntPtr]::Zero
    $ok = [DuckStationMobyMoveNative]::WriteProcessMemory($Handle, [IntPtr]$Address, $bytes, [UIntPtr]::new([uint64]4), [ref]$written)
    if (-not $ok -or $written.ToUInt64() -ne 4) {
        throw ("WriteProcessMemory failed at 0x{0:X}" -f $Address)
    }
}

function Write-MobyPosition($Handle, $Record, [int]$RawX, [int]$RawY, [int]$RawZ) {
    Write-Int32 $Handle ([int64]$Record.absoluteAddress + 0x0C) $RawX
    Write-Int32 $Handle ([int64]$Record.absoluteAddress + 0x10) $RawY
    Write-Int32 $Handle ([int64]$Record.absoluteAddress + 0x14) $RawZ
}

$processes = @(Get-ProcessCandidates)
if ($ListOnly) {
    $processes | Select-Object Id, ProcessName, MainWindowTitle, WorkingSet64 | Format-Table -AutoSize
    return
}

$process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction Stop } else { $processes[0] }
Write-Host "Using process $($process.ProcessName) pid=$($process.Id)"

$PROCESS_QUERY_INFORMATION = 0x0400
$PROCESS_VM_READ = 0x0010
$PROCESS_VM_WRITE = 0x0020
$PROCESS_VM_OPERATION = 0x0008
$needsWrite = ($Apply -or $Revert -or $RevertAll)
$access = $PROCESS_QUERY_INFORMATION -bor $PROCESS_VM_READ
if ($needsWrite) { $access = $access -bor $PROCESS_VM_WRITE -bor $PROCESS_VM_OPERATION }

$handle = [DuckStationMobyMoveNative]::OpenProcess($access, $false, $process.Id)
if ($handle -eq [IntPtr]::Zero) {
    throw "Could not open process memory. Try running PowerShell as Administrator."
}

try {
    $window = Find-LiveRamWindow $handle
    if ($ListCandidates) { return }
    Write-Host ("Live RAM window: 0x{0:X}; level: 0x{1:X2}; pointer: 0x{2:X8}; plausible mobys: {3}" -f [int64]$window.absoluteAddress, [uint32]$window.levelId, [uint32]$window.pointer, [int]$window.mobyCount)

    $labelMap = Get-MobyLabelMap
    if ($ListMobys) {
        $start = Convert-PsxPointerToOffset ([uint32]$window.pointer)
        $rows = @()
        for ($i = 0; $i -lt 512; $i++) {
            $offset = $start + ($i * 0x58)
            if (($offset + 0x58) -gt $window.ram.Length) { break }
            if (-not (Test-PlausibleMoby $window.ram $offset)) { continue }
            $label = if ($labelMap.ContainsKey($i)) { $labelMap[$i] } else { "" }
            $rows += [pscustomobject]@{
                T = $i
                Label = $label
                Type = ("0x{0:X2}" -f [int]$window.ram[$offset + 0x50])
                State = ("0x{0:X2}" -f [int]$window.ram[$offset + 0x51])
                X = [Math]::Round((Get-Int32LE $window.ram ($offset + 0x0C)) / 16.0, 2)
                Y = [Math]::Round((Get-Int32LE $window.ram ($offset + 0x10)) / 16.0, 2)
                Z = [Math]::Round((Get-Int32LE $window.ram ($offset + 0x14)) / 16.0, 2)
            }
        }
        $rows | Format-Table -AutoSize
        return
    }

    $originalsPath = Resolve-WorkspacePath $OriginalsPath
    $store = Load-Originals $originalsPath

    if ($RevertAll) {
        $count = 0
        foreach ($original in @(Get-ArrayField $store "originals")) {
            $record = Read-MobyRecord $window ([int]$original.index)
            Write-MobyPosition $handle $record ([int]$original.rawX) ([int]$original.rawY) ([int]$original.rawZ)
            $count++
        }
        Write-Host "Reverted $count saved live moby position(s)."
        return
    }

    if (($null -eq $MobyIndexes -or $MobyIndexes.Count -eq 0) -and $MobyIndex -ge 0) {
        $MobyIndexes = @($MobyIndex)
    }
    if ($null -eq $MobyIndexes -or $MobyIndexes.Count -eq 0) {
        throw "Pass -MobyIndex <T#> or -MobyIndexes <T#,...>, or use -ListMobys / -ListCandidates."
    }

    $wroteCount = 0
    foreach ($activeMobyIndex in @($MobyIndexes)) {
        $record = Read-MobyRecord $window $activeMobyIndex
        $label = if ($labelMap.ContainsKey($activeMobyIndex)) { $labelMap[$activeMobyIndex] } else { "" }
        Write-Host ("Selected T{0} {1} at 0x{2:X}: type 0x{3:X2}, state 0x{4:X2}" -f $activeMobyIndex, $label, [int64]$record.absoluteAddress, [int]$record.type, [int]$record.state)
        Write-Host ("Current raw XYZ: {0}, {1}, {2}  editor XYZ: {3:N2}, {4:N2}, {5:N2}" -f [int]$record.rawX, [int]$record.rawY, [int]$record.rawZ, ([int]$record.rawX / 16.0), ([int]$record.rawY / 16.0), ([int]$record.rawZ / 16.0))

        if ($Revert) {
            $original = Get-OriginalRecord $store $activeMobyIndex
            if ($null -eq $original) {
                Write-Warning "No saved original position exists for T$activeMobyIndex in $originalsPath; skipping this moby."
                continue
            }
            Write-MobyPosition $handle $record ([int]$original.rawX) ([int]$original.rawY) ([int]$original.rawZ)
            Write-Host ("Reverted T{0} to raw XYZ {1}, {2}, {3}" -f $activeMobyIndex, [int]$original.rawX, [int]$original.rawY, [int]$original.rawZ)
            continue
        }

        Add-OriginalRecord $store $record
        Save-Originals $originalsPath $store

        if ($FromNativeEdit) {
            $target = Get-NativeEditTarget $activeMobyIndex (Resolve-WorkspacePath $NativeEditsPath)
            $targetRawX = [int]$target.rawX
            $targetRawY = [int]$target.rawY
            $targetRawZ = [int]$target.rawZ
            Write-Host "Using saved native editor target for T$activeMobyIndex $($target.label)."
        }
        else {
            $original = Get-OriginalRecord $store $activeMobyIndex
            $targetRawX = [int]$original.rawX + [int][Math]::Round($DeltaX * 16.0)
            $targetRawY = [int]$original.rawY + [int][Math]::Round($DeltaY * 16.0)
            $targetRawZ = [int]$original.rawZ + [int][Math]::Round($DeltaZ * 16.0)
        }

        Write-Host ("Target raw XYZ: {0}, {1}, {2}  editor XYZ: {3:N2}, {4:N2}, {5:N2}" -f $targetRawX, $targetRawY, $targetRawZ, ($targetRawX / 16.0), ($targetRawY / 16.0), ($targetRawZ / 16.0))
        if (-not $Apply) {
            Write-Host "Dry run only. Re-run with -Apply to write this position into DuckStation RAM."
            continue
        }

        $end = (Get-Date).AddSeconds([Math]::Max(0, $HoldSeconds))
        do {
            Write-MobyPosition $handle $record $targetRawX $targetRawY $targetRawZ
            if ($HoldSeconds -le 0) { break }
            Start-Sleep -Milliseconds ([Math]::Max(16, $IntervalMs))
        } while ((Get-Date) -lt $end)

        $wroteCount++
        Write-Host ("Wrote T{0} live position. Use -Revert -MobyIndex {0} to restore it." -f $activeMobyIndex)
    }
    if ($wroteCount -gt 1) {
        Write-Host "Wrote $wroteCount linked live moby position(s)."
    }
}
finally {
    [void][DuckStationMobyMoveNative]::CloseHandle($handle)
}
