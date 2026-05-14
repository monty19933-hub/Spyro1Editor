param(
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$OriginalRamPath = ".\stonehill-before-gem-clean.bin",
    [string]$RuntimeKey = "",
    [int]$SectorIndex = -1,
    [int]$FaceIndex = -1,
    [string]$Detail = "",
    [string]$ProcessName = "duckstation",
    [int]$ProcessId = 0,
    [int]$CandidateIndex = -1,
    [int]$MaxCandidates = 8,
    [switch]$ListCandidates,
    [switch]$PlanOnly,
    [switch]$Apply,
    [switch]$Revert,
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

function Convert-HexTextToInt([string]$Text, [int]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    try {
        if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
            return [int][Convert]::ToInt64($clean.Substring(2), 16)
        }
        return [int]$clean
    }
    catch {
        return $Default
    }
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 2) -gt $Bytes.Length) { return [uint16]0 }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Test-PsxPointer([uint32]$Value) {
    $ramBase = [uint32]2147483648
    $ramEnd = [uint32]2149580800
    return ($Value -ge $ramBase -and $Value -lt $ramEnd -and (($Value -band [uint32]3) -eq 0))
}

function Convert-PsxPointerToOffset([uint32]$Value) {
    if (-not (Test-PsxPointer $Value)) { return -1 }
    return [int]($Value -band [uint32]0x001FFFFF)
}

function Test-ReadableProtect([uint32]$Protect) {
    $PAGE_NOACCESS = 0x01
    $PAGE_GUARD = 0x100
    if (($Protect -band $PAGE_NOACCESS) -ne 0) { return $false }
    if (($Protect -band $PAGE_GUARD) -ne 0) { return $false }
    return $true
}

$nativeSource = @"
using System;
using System.Runtime.InteropServices;

public static class DuckStationTerrainMoveNative {
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

public struct DuckStationTerrainRamScanResult {
    public bool Found;
    public int Offset;
    public uint Pointer;
    public int MobyCount;
    public int Score;
    public int NonZeroCount;
    public int Checksum;
}

public static class DuckStationTerrainRamScanner {
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

    public static DuckStationTerrainRamScanResult[] FindWindows(byte[] bytes, int maxResults) {
        if (bytes == null || bytes.Length < MainRamSize)
            return new DuckStationTerrainRamScanResult[0];

        DuckStationTerrainRamScanResult[] results = new DuckStationTerrainRamScanResult[Math.Max(1, maxResults)];
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

            DuckStationTerrainRamScanResult item = new DuckStationTerrainRamScanResult();
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

        DuckStationTerrainRamScanResult[] trimmed = new DuckStationTerrainRamScanResult[resultCount];
        Array.Copy(results, trimmed, resultCount);
        Array.Sort(trimmed, delegate(DuckStationTerrainRamScanResult a, DuckStationTerrainRamScanResult b) {
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

Add-Type -TypeDefinition $nativeSource

function Read-SceneSectorHeader([byte[]]$Ram, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 28) -gt $Ram.Length) { return $null }
    $numLpVertices = [int]$Ram[$Offset + 16]
    $numLpColours = [int]$Ram[$Offset + 17]
    $numLpFaces = [int]$Ram[$Offset + 18]
    $numHpVertices = [int]$Ram[$Offset + 20]
    $numHpColours = [int]$Ram[$Offset + 21]
    $numHpFaces = [int]$Ram[$Offset + 22]
    $sizeWords = 7 + $numLpVertices + $numLpColours + ($numLpFaces * 2) + $numHpVertices + ($numHpColours * 2) + ($numHpFaces * 4)
    $sizeBytes = $sizeWords * 4
    if ($sizeBytes -lt 28 -or $sizeBytes -gt 0x40000 -or ($Offset + $sizeBytes) -gt $Ram.Length) { return $null }
    if (($numLpVertices + $numHpVertices) -eq 0 -or ($numLpFaces + $numHpFaces) -eq 0) { return $null }
    return [pscustomobject]@{
        offset = $Offset
        centreRadiusAndFlags = [int](Get-UInt16LE $Ram ($Offset + 4))
        xyPos = [uint32](Get-UInt32LE $Ram ($Offset + 8))
        zPos = [uint32](Get-UInt32LE $Ram ($Offset + 12))
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    }
}

function Get-SceneSectorBaseZ($Sector) {
    $zPos = [uint64]$Sector.zPos
    $sectorZ = [int](($zPos -shr 14) -band 0xFFFF)
    return [int]($sectorZ -shr 2)
}

function Test-FlatSceneSector($Sector) {
    return ((([int]$Sector.centreRadiusAndFlags -shr 12) -band 1) -eq 1)
}

function Convert-SceneVertexZ([uint32]$Word, $Sector) {
    $cur = [uint64]$Word
    $z = (Get-SceneSectorBaseZ $Sector) + [int]((($cur -shl 3) -band 0x1FFC) -shr 3)
    if (Test-FlatSceneSector $Sector) { $z = $z -shr 3 }
    return [double]$z
}

function Set-SceneVertexZWord([uint32]$Word, $Sector, [double]$TargetZ) {
    $baseZ = Get-SceneSectorBaseZ $Sector
    if (Test-FlatSceneSector $Sector) {
        $encodedZ = [int][Math]::Round(($TargetZ * 8.0) - [double]$baseZ)
    }
    else {
        $encodedZ = [int][Math]::Round($TargetZ - [double]$baseZ)
    }
    if ($encodedZ -lt 0 -or $encodedZ -gt 1023) {
        throw ("Target Z {0:N2} cannot be encoded in sector 0x{1:X}; encoded delta would be {2}." -f $TargetZ, [int]$Sector.offset, $encodedZ)
    }
    return [uint32]((([uint64]$Word) -band [uint64]4294966272) -bor ([uint64]($encodedZ -band 0x3FF)))
}

function Get-SceneVertexOffset($Sector, [string]$DetailName, [int]$VertexIndex) {
    $dataStart = [int]$Sector.offset + 28
    if ([string]::Equals($DetailName, "lp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numLpVertices) { return -1 }
        return $dataStart + ($VertexIndex * 4)
    }
    if ([string]::Equals($DetailName, "hp", [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($VertexIndex -lt 0 -or $VertexIndex -ge [int]$Sector.numHpVertices) { return -1 }
        $hpVertexStartWords = [int]$Sector.numLpVertices + [int]$Sector.numLpColours + ([int]$Sector.numLpFaces * 2)
        return $dataStart + (($hpVertexStartWords + $VertexIndex) * 4)
    }
    return -1
}

function Read-TerrainEdits([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $selected = New-Object System.Collections.ArrayList
    foreach ($edit in @(Get-ArrayField $json "edits")) {
        $key = [string](Get-Field $edit "runtimeKey" "")
        $sector = [int](Get-Field $edit "sectorIndex" -1)
        $face = [int](Get-Field $edit "faceIndex" -1)
        $editDetail = [string](Get-Field $edit "detail" "")
        if (-not [string]::IsNullOrWhiteSpace($RuntimeKey) -and $key -ne $RuntimeKey) { continue }
        if ($SectorIndex -ge 0 -and $sector -ne $SectorIndex) { continue }
        if ($FaceIndex -ge 0 -and $face -ne $FaceIndex) { continue }
        if (-not [string]::IsNullOrWhiteSpace($Detail) -and $editDetail -ne $Detail) { continue }
        [void]$selected.Add($edit)
    }
    return @($selected.ToArray())
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
    $scans = [DuckStationTerrainRamScanner]::FindWindows($Bytes, $MaxResults)
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

function Find-LiveRamWindow($Handle) {
    $address = [int64]0
    $maxAddress = [int64]0x7FFFFFFF0000
    $mbiSize = [UIntPtr]::new([uint64][Runtime.InteropServices.Marshal]::SizeOf([type][DuckStationTerrainMoveNative+MEMORY_BASIC_INFORMATION]))
    $allCandidates = @()
    $chunkSize = 64MB
    $overlap = 2MB

    while ($address -lt $maxAddress) {
        $info = New-Object DuckStationTerrainMoveNative+MEMORY_BASIC_INFORMATION
        $result = [DuckStationTerrainMoveNative]::VirtualQueryEx($Handle, [IntPtr]$address, [ref]$info, $mbiSize)
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
                if ([DuckStationTerrainMoveNative]::ReadProcessMemory($Handle, [IntPtr]$chunkBase, $buffer, $readSize, [ref]$bytesRead)) {
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
        throw "Could not find Spyro's live 2 MB main RAM window. Make sure DuckStation is running in Stone Hill."
    }
    if ($CandidateIndex -ge 0) {
        if ($CandidateIndex -ge $ranked.Count) {
            throw "Candidate index $CandidateIndex is not available. Run with -ListCandidates to see valid indexes."
        }
        return $ranked[$CandidateIndex]
    }
    return $ranked[0]
}

function Write-UInt32($Handle, [int64]$Address, [uint32]$Value) {
    $bytes = [BitConverter]::GetBytes([uint32]$Value)
    $written = [UIntPtr]::Zero
    $ok = [DuckStationTerrainMoveNative]::WriteProcessMemory($Handle, [IntPtr]$Address, $bytes, [UIntPtr]::new([uint64]4), [ref]$written)
    if (-not $ok -or $written.ToUInt64() -ne 4) {
        throw ("WriteProcessMemory failed at 0x{0:X}" -f $Address)
    }
}

function New-VertexPlan($Window, [byte[]]$OriginalRam, $Edits) {
    $targets = @{}
    foreach ($edit in @($Edits)) {
        $sectorOffset = Convert-HexTextToInt ([string](Get-Field $edit "sectorOffset" ""))
        $detailName = [string](Get-Field $edit "detail" "")
        $deltaZ = [double](Get-Field $edit "deltaZ" 0)
        if ($Revert) { $deltaZ = 0.0 }
        if ($sectorOffset -lt 0) { continue }

        $liveSector = Read-SceneSectorHeader $Window.ram $sectorOffset
        $originalSector = if ($null -ne $OriginalRam) { Read-SceneSectorHeader $OriginalRam $sectorOffset } else { $null }
        if ($null -eq $liveSector) { throw ("Could not read live scene sector at 0x{0:X}." -f $sectorOffset) }
        if ($null -eq $originalSector) { $originalSector = $liveSector }

        $seenIndexes = New-Object 'System.Collections.Generic.HashSet[int]'
        foreach ($rawIndex in @(Get-ArrayField $edit "vertexIndexes")) {
            $vertexIndex = [int]$rawIndex
            if (-not $seenIndexes.Add($vertexIndex)) { continue }
            $vertexOffset = Get-SceneVertexOffset $liveSector $detailName $vertexIndex
            if ($vertexOffset -lt 0) { continue }
            $key = [string]$vertexOffset
            if (-not $targets.ContainsKey($key)) {
                $targets[$key] = [ordered]@{
                    vertexOffset = $vertexOffset
                    sectorOffset = $sectorOffset
                    detail = $detailName
                    vertexIndex = $vertexIndex
                    liveSector = $liveSector
                    originalSector = $originalSector
                    deltas = New-Object System.Collections.ArrayList
                    runtimeKeys = New-Object System.Collections.ArrayList
                }
            }
            [void]$targets[$key].deltas.Add($deltaZ)
            [void]$targets[$key].runtimeKeys.Add([string](Get-Field $edit "runtimeKey" ""))
        }
    }

    $plan = New-Object System.Collections.ArrayList
    foreach ($entry in $targets.Values) {
        $sum = 0.0
        foreach ($d in @($entry.deltas)) { $sum += [double]$d }
        $avgDelta = if ($entry.deltas.Count -gt 0) { $sum / [double]$entry.deltas.Count } else { 0.0 }
        $vertexOffset = [int]$entry.vertexOffset
        $liveWord = Get-UInt32LE $Window.ram $vertexOffset
        $originalWord = if ($null -ne $OriginalRam) { Get-UInt32LE $OriginalRam $vertexOffset } else { $liveWord }
        $originalZ = Convert-SceneVertexZ $originalWord $entry.originalSector
        $targetZ = $originalZ + $avgDelta
        $targetWord = Set-SceneVertexZWord $liveWord $entry.liveSector $targetZ
        [void]$plan.Add([pscustomobject]@{
            vertexOffset = $vertexOffset
            absoluteAddress = ([int64]$Window.absoluteAddress + [int64]$vertexOffset)
            sectorOffset = [int]$entry.sectorOffset
            detail = [string]$entry.detail
            vertexIndex = [int]$entry.vertexIndex
            faceCount = [int]$entry.deltas.Count
            deltaZ = [double]$avgDelta
            originalZ = [double]$originalZ
            targetZ = [double]$targetZ
            liveWord = [uint32]$liveWord
            targetWord = [uint32]$targetWord
            runtimeKeys = @($entry.runtimeKeys.ToArray())
        })
    }
    return @($plan.ToArray())
}

$terrainEditsPath = Resolve-WorkspacePath $TerrainEditsPath
$originalRamPath = Resolve-WorkspacePath $OriginalRamPath
$edits = @(Read-TerrainEdits $terrainEditsPath)
if ($edits.Count -eq 0 -and $PlanOnly) {
    Write-Host "No matching terrain edits were found in $terrainEditsPath."
    return
}
if ($edits.Count -eq 0 -and -not $ListCandidates) {
    throw "No matching terrain edits were found in $terrainEditsPath."
}

if ($PlanOnly) {
    $rows = foreach ($edit in $edits) {
        [pscustomobject]@{
            RuntimeKey = [string](Get-Field $edit "runtimeKey" "")
            Sector = [int](Get-Field $edit "sectorIndex" -1)
            Face = [int](Get-Field $edit "faceIndex" -1)
            Detail = [string](Get-Field $edit "detail" "")
            SectorOffset = [string](Get-Field $edit "sectorOffset" "")
            FaceOffset = [string](Get-Field $edit "faceOffset" "")
            DeltaZ = [double](Get-Field $edit "deltaZ" 0)
            Vertices = (@(Get-ArrayField $edit "vertexIndexes") -join ",")
        }
    }
    $rows | Format-Table -AutoSize
    return
}

$originalRam = $null
if (Test-Path -LiteralPath $originalRamPath) {
    $originalRam = [IO.File]::ReadAllBytes($originalRamPath)
    if ($originalRam.Length -lt 2MB) {
        Write-Warning "Original RAM source is smaller than 2 MB; live words will be used as the baseline."
        $originalRam = $null
    }
}
elseif ($Revert) {
    throw "Revert needs the original terrain RAM baseline: $originalRamPath"
}

$processes = @(Get-ProcessCandidates)
$process = if ($ProcessId -ne 0) { Get-Process -Id $ProcessId -ErrorAction Stop } else { $processes[0] }
Write-Host "Using process $($process.ProcessName) pid=$($process.Id)"

$PROCESS_QUERY_INFORMATION = 0x0400
$PROCESS_VM_READ = 0x0010
$PROCESS_VM_WRITE = 0x0020
$PROCESS_VM_OPERATION = 0x0008
$needsWrite = ($Apply -or $Revert)
$access = $PROCESS_QUERY_INFORMATION -bor $PROCESS_VM_READ
if ($needsWrite) { $access = $access -bor $PROCESS_VM_WRITE -bor $PROCESS_VM_OPERATION }

$handle = [DuckStationTerrainMoveNative]::OpenProcess($access, $false, $process.Id)
if ($handle -eq [IntPtr]::Zero) {
    throw "Could not open process memory. Try running PowerShell as Administrator."
}

try {
    $window = Find-LiveRamWindow $handle
    if ($ListCandidates) { return }
    Write-Host ("Live RAM window: 0x{0:X}; pointer: 0x{1:X8}; plausible mobys: {2}" -f [int64]$window.absoluteAddress, [uint32]$window.pointer, [int]$window.mobyCount)

    $plan = @(New-VertexPlan $window $originalRam $edits)
    if ($plan.Count -eq 0) {
        throw "No terrain vertices could be resolved from the selected edits."
    }

    $rows = foreach ($item in $plan) {
        [pscustomobject]@{
            Offset = ("0x{0:X}" -f [int]$item.vertexOffset)
            Address = ("0x{0:X}" -f [int64]$item.absoluteAddress)
            Detail = [string]$item.detail
            Vertex = [int]$item.vertexIndex
            Faces = [int]$item.faceCount
            OriginalZ = [Math]::Round([double]$item.originalZ, 2)
            TargetZ = [Math]::Round([double]$item.targetZ, 2)
            DeltaZ = [Math]::Round([double]$item.deltaZ, 2)
        }
    }
    $rows | Format-Table -AutoSize

    if (-not $needsWrite) {
        Write-Host "Dry run only. Re-run with -Apply to write terrain height edits, or -Revert to restore original Z."
        return
    }

    $end = (Get-Date).AddSeconds([Math]::Max(0, $HoldSeconds))
    do {
        foreach ($item in $plan) {
            Write-UInt32 $handle ([int64]$item.absoluteAddress) ([uint32]$item.targetWord)
        }
        if ($HoldSeconds -le 0) { break }
        Start-Sleep -Milliseconds ([Math]::Max(16, $IntervalMs))
    } while ((Get-Date) -lt $end)

    if ($Revert) {
        Write-Host ("Reverted {0} terrain vertex word(s)." -f $plan.Count)
    }
    else {
        Write-Host ("Wrote {0} terrain vertex word(s) from {1} face edit(s)." -f $plan.Count, $edits.Count)
    }
}
finally {
    [void][DuckStationTerrainMoveNative]::CloseHandle($handle)
}
