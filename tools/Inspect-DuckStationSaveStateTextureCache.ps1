param(
    [string]$SaveStatePath = "C:\Users\monty\AppData\Local\DuckStation\savestates\SCUS-94228_1.sav",
    [string]$ZstdDllPath = "C:\Users\monty\AppData\Local\Programs\DuckStation\zstd.dll",
    [string]$OutJsonPath = ".\duckstation-savestate-texture-cache-inspection.json",
    [string]$OutMarkdownPath = ".\duckstation-savestate-texture-cache-inspection.md",
    [string]$OutTextureCacheSlicePath = ".\duckstation-gpu-texture-cache-slice.bin",
    [int]$TextureCacheSliceBytes = 262144
)

Set-StrictMode -Version 2.0

function Add-ZstdNativeType([string]$DllPath) {
    if ("DuckStationSaveStateZstdNative" -as [type]) { return }
    $resolvedDll = (Resolve-Path -LiteralPath $DllPath -ErrorAction Stop).Path
    $escapedDll = $resolvedDll.Replace("\", "\\")
    $source = @"
using System;
using System.Runtime.InteropServices;

public static class DuckStationSaveStateZstdNative {
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

function Find-Sequence([byte[]]$Bytes, [byte[]]$Needle, [int]$Start) {
    if ($Needle.Length -eq 0 -or $Bytes.Length -lt $Needle.Length) { return @() }
    $hits = New-Object System.Collections.Generic.List[int]
    $limit = $Bytes.Length - $Needle.Length
    for ($i = $Start; $i -le $limit; $i++) {
        if ($Bytes[$i] -ne $Needle[0]) { continue }
        $matched = $true
        for ($j = 1; $j -lt $Needle.Length; $j++) {
            if ($Bytes[$i + $j] -ne $Needle[$j]) {
                $matched = $false
                break
            }
        }
        if ($matched) { [void]$hits.Add($i) }
    }
    return @($hits.ToArray())
}

function Find-ZstdFrames([byte[]]$Bytes) {
    $magic = [byte[]](0x28, 0xB5, 0x2F, 0xFD)
    return @(Find-Sequence $Bytes $magic 0)
}

function Expand-ZstdFrame([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    $frame = New-Object byte[] $Length
    [Array]::Copy($Bytes, $Offset, $frame, 0, $Length)
    return [DuckStationSaveStateZstdNative]::DecompressFrame($frame)
}

function Get-AsciiContext([byte[]]$Bytes, [int]$Offset, [int]$Before, [int]$After) {
    $start = [Math]::Max(0, $Offset - $Before)
    $length = [Math]::Min($Bytes.Length - $start, $Before + $After)
    $chars = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $length; $i++) {
        $b = [int]$Bytes[$start + $i]
        if ($b -ge 32 -and $b -le 126) { [void]$chars.Append([char]$b) }
        elseif ($b -eq 9 -or $b -eq 10 -or $b -eq 13) { [void]$chars.Append(" ") }
        else { [void]$chars.Append(".") }
    }
    return $chars.ToString()
}

function Find-AsciiRuns([byte[]]$Bytes, [int]$MinLength) {
    $runs = New-Object System.Collections.Generic.List[object]
    $start = -1
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        $b = [int]$Bytes[$i]
        $printable = ($b -ge 32 -and $b -le 126)
        if ($printable) {
            if ($start -lt 0) { $start = $i }
        }
        elseif ($start -ge 0) {
            $length = $i - $start
            if ($length -ge $MinLength) {
                $text = [System.Text.Encoding]::ASCII.GetString($Bytes, $start, $length)
                [void]$runs.Add([ordered]@{
                    offset = $start
                    offsetHex = ("0x{0:X}" -f $start)
                    length = $length
                    text = $text
                })
            }
            $start = -1
        }
    }
    if ($start -ge 0) {
        $length = $Bytes.Length - $start
        if ($length -ge $MinLength) {
            $text = [System.Text.Encoding]::ASCII.GetString($Bytes, $start, $length)
            [void]$runs.Add([ordered]@{
                offset = $start
                offsetHex = ("0x{0:X}" -f $start)
                length = $length
                text = $text
            })
        }
    }
    return @($runs.ToArray())
}

$resolvedState = (Resolve-Path -LiteralPath $SaveStatePath -ErrorAction Stop).Path
Add-ZstdNativeType $ZstdDllPath
$stateBytes = [System.IO.File]::ReadAllBytes($resolvedState)
$frameOffsets = @(Find-ZstdFrames $stateBytes)
if ($frameOffsets.Count -eq 0) { throw "No Zstandard frames found in save state." }

$labels = @(
    "GPU-VRAM",
    "GPUTextureCache",
    "GPU-HW",
    "GPU",
    "CPU",
    "RAM",
    "CDROM",
    "Timers",
    "SPU",
    "DMA",
    "Interrupt"
)
$labelBytes = @{}
foreach ($label in $labels) { $labelBytes[$label] = [System.Text.Encoding]::ASCII.GetBytes($label) }

$frameReports = New-Object System.Collections.Generic.List[object]
$textureCacheSliceWritten = $false
for ($frameIndex = 0; $frameIndex -lt $frameOffsets.Count; $frameIndex++) {
    $frameOffset = [int]$frameOffsets[$frameIndex]
    $nextFrameOffset = if ($frameIndex + 1 -lt $frameOffsets.Count) { [int]$frameOffsets[$frameIndex + 1] } else { $stateBytes.Length }
    $frameLength = $nextFrameOffset - $frameOffset
    $decompressed = Expand-ZstdFrame $stateBytes $frameOffset $frameLength

    $hits = New-Object System.Collections.Generic.List[object]
    foreach ($label in $labels) {
        foreach ($hit in @(Find-Sequence $decompressed $labelBytes[$label] 0)) {
            [void]$hits.Add([ordered]@{
                label = $label
                offset = [int]$hit
                offsetHex = ("0x{0:X}" -f [int]$hit)
                context = Get-AsciiContext $decompressed ([int]$hit) 32 128
            })
        }
    }

    $asciiRuns = @(Find-AsciiRuns $decompressed 6 | Select-Object -First 80)
    [void]$frameReports.Add([ordered]@{
        frameIndex = $frameIndex
        compressedOffset = $frameOffset
        compressedOffsetHex = ("0x{0:X}" -f $frameOffset)
        compressedLength = $frameLength
        decompressedLength = $decompressed.Length
        labelHits = @($hits.ToArray() | Sort-Object offset)
        firstAsciiRuns = $asciiRuns
    })

    $textureHit = @($hits.ToArray() | Where-Object { $_.label -eq "GPUTextureCache" } | Select-Object -First 1)
    if (-not $textureCacheSliceWritten -and $textureHit.Count -gt 0) {
        $sliceOffset = [int]$textureHit[0].offset
        $sliceLength = [Math]::Min($TextureCacheSliceBytes, $decompressed.Length - $sliceOffset)
        $slice = New-Object byte[] $sliceLength
        [Array]::Copy($decompressed, $sliceOffset, $slice, 0, $sliceLength)
        $resolvedSlice = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutTextureCacheSlicePath)
        [System.IO.File]::WriteAllBytes($resolvedSlice, $slice)
        $textureCacheSliceWritten = $true
    }
}

$summary = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    saveState = $resolvedState
    zstdDll = (Resolve-Path -LiteralPath $ZstdDllPath -ErrorAction Stop).Path
    zstdFrames = $frameOffsets.Count
    textureCacheSlice = if ($textureCacheSliceWritten) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutTextureCacheSlicePath) } else { $null }
    frames = @($frameReports.ToArray())
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
($summary | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$md = New-Object System.Collections.Generic.List[string]
[void]$md.Add("# DuckStation Save-State Texture Cache Inspection")
[void]$md.Add("")
[void]$md.Add(("- Generated: {0}" -f $summary.generatedAt))
[void]$md.Add(('- Save-state: `{0}`' -f $summary.saveState))
[void]$md.Add(("- Zstandard frames: {0}" -f $summary.zstdFrames))
if ($textureCacheSliceWritten) { [void]$md.Add(('- Texture-cache slice: `{0}`' -f $summary.textureCacheSlice)) }
[void]$md.Add("")
foreach ($frame in @($frameReports.ToArray())) {
    [void]$md.Add(("## Frame {0}" -f [int]$frame.frameIndex))
    [void]$md.Add("")
    [void]$md.Add(('- Compressed offset: `{0}`' -f [string]$frame.compressedOffsetHex))
    [void]$md.Add(("- Compressed length: {0}" -f [int]$frame.compressedLength))
    [void]$md.Add(("- Decompressed length: {0}" -f [int]$frame.decompressedLength))
    [void]$md.Add("")
    [void]$md.Add("| Label | Offset | Context |")
    [void]$md.Add("|---|---:|---|")
    foreach ($hit in @($frame.labelHits)) {
        $context = ([string]$hit.context).Replace("|", "/")
        [void]$md.Add(('| {0} | `{1}` | `{2}` |' -f [string]$hit.label, [string]$hit.offsetHex, $context))
    }
    [void]$md.Add("")
}

$resolvedMarkdown = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMarkdownPath)
[System.IO.File]::WriteAllLines($resolvedMarkdown, [string[]]$md.ToArray(), [System.Text.UTF8Encoding]::new($false))

Write-Host ("Wrote save-state inspection to {0}" -f $resolvedJson)
Write-Host ("Wrote markdown summary to {0}" -f $resolvedMarkdown)
if ($textureCacheSliceWritten) {
    Write-Host ("Wrote GPUTextureCache slice to {0}" -f $summary.textureCacheSlice)
}
