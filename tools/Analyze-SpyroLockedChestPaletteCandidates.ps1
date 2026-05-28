param(
    [string]$BaselineBinPath = ".\_local\experiments\artisans-locked-diag-z34-slot2-full-sourceflag.bin",
    [string]$OutJsonPath = ".\_local\experiments\artisans-locked-chest-palette-candidates.json",
    [int]$WadLba = 37
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$ArtisansEntryBase = [int64]0x800800
$VramStride = [int]2048
$Roots = @(
    [pscustomobject]@{ name = "imported-00AE"; root = [int64]0x1868D4; length = [int]0x1008 },
    [pscustomobject]@{ name = "artisans-01A5"; root = [int64]0x1AF104; length = [int]0x103C },
    [pscustomobject]@{ name = "artisans-000E"; root = [int64]0x1B0140; length = [int]0x7EC }
)

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $WorkspaceRoot $Path
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sectorOffset = [int]($WadOffset % 2048)
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    return ($sector * 2352L) + 24L + $sectorOffset
}

function Read-WadBytes([IO.FileStream]$Stream, [int64]$WadOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $WadOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = Convert-WadOffsetToImageOffset $absolute
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return $result
}

function Decode-ClutOffset([int]$Word) {
    $x = [int](($Word -band 0x3F) * 16)
    $y = [int](($Word -shr 6) -band 0x1FF)
    return ([int64]$y * [int64]$VramStride) + ([int64]$x * 2L)
}

$resolvedBaseline = Resolve-WorkspacePath $BaselineBinPath
$resolvedOut = Resolve-WorkspacePath $OutJsonPath
[IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOut)) | Out-Null

$stream = [IO.File]::OpenRead($resolvedBaseline)
try {
    $all = New-Object System.Collections.Generic.List[object]
    foreach ($root in $Roots) {
        $bytes = Read-WadBytes $stream ($ArtisansEntryBase + [int64]$root.root) ([int]$root.length)
        for ($offset = 0; $offset + 2 -le $bytes.Length; $offset += 2) {
            $word = [int][BitConverter]::ToUInt16($bytes, $offset)
            $bankOffset = Decode-ClutOffset $word
            if ($bankOffset -ge 0x44C00 -and $bankOffset -le 0x527FF) {
                [void]$all.Add([pscustomobject]@{
                    root = [string]$root.name
                    packageOffset = ("0x{0:X}" -f $offset)
                    word = ("0x{0:X4}" -f $word)
                    bankOffset = ("0x{0:X5}" -f $bankOffset)
                })
            }
        }
    }
}
finally {
    $stream.Dispose()
}

$summary = @($all | Group-Object word | Sort-Object Count -Descending | ForEach-Object {
    [pscustomobject]@{
        word = $_.Name
        count = $_.Count
        sampleBankOffset = $_.Group[0].bankOffset
        roots = @($_.Group | Group-Object root | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ", "
    }
})

$result = [ordered]@{
    baseline = $resolvedBaseline
    range = "0x44C00..0x527FF"
    hitCount = $all.Count
    summary = $summary
    hits = @($all.ToArray())
}

$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
Write-Host ("Found {0} palette-like words in z104-proven bank range." -f $all.Count)
$summary | Select-Object -First 40 | Format-Table -AutoSize
