param(
    [string]$BaselineBinPath = ".\_local\experiments\artisans-locked-diag-z34-slot2-full-sourceflag.bin",
    [string]$OutPath = ".\_local\experiments\artisans-locked-chest-triplets.json",
    [int]$WadLba = 37
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
$ArtisansEntryBase = [int64]0x800800
$ImportedChestRoot = [int64]0x1868D4
$ImportedChestLength = [int]0x1008
$VramStrideBytes = 2048

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

function Get-U32([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Test-TextureFirstWord([uint32]$Word) {
    $b1 = [int](($Word -shr 8) -band 0xFF)
    $b2 = [int](($Word -shr 16) -band 0xFF)
    $b3 = [int](($Word -shr 24) -band 0xFF)
    return ($b1 -in @(0xC0, 0xDF, 0xE0, 0xFF)) -and
        ($b2 -ge 0x18 -and $b2 -le 0x58) -and
        ($b3 -ge 0x40 -and $b3 -le 0x80)
}

function Test-TextureSecondWord([uint32]$Word) {
    $b1 = [int](($Word -shr 8) -band 0xFF)
    $b2 = [int](($Word -shr 16) -band 0xFF)
    $b3 = [int](($Word -shr 24) -band 0xFF)
    return ($b2 -in @(0x0E, 0x0F)) -and ($b3 -eq 0x00) -and
        ($b1 -in @(0xA0, 0xBF, 0xC0, 0xDF, 0xE0, 0xEF, 0xFF))
}

function Test-TextureThirdWord([uint32]$Word) {
    $b1 = [int](($Word -shr 8) -band 0xFF)
    $b2 = [int](($Word -shr 16) -band 0xFF)
    $b3 = [int](($Word -shr 24) -band 0xFF)
    return ($b1 -in @(0xC0, 0xDF, 0xFF)) -and
        ($b2 -in @(0xC0, 0xDF, 0xE0, 0xFF)) -and
        ($b3 -in @(0x1F, 0x7F, 0xDF, 0xE0, 0xFF))
}

function Decode-ClutWord([int]$Raw) {
    $x = [int](($Raw -band 0x3F) * 16)
    $y = [int](($Raw -shr 6) -band 0x1FF)
    return [pscustomobject]@{
        raw = ("0x{0:X4}" -f $Raw)
        x = $x
        y = $y
        bankOffset = ("0x{0:X5}" -f (([int64]$y * $VramStrideBytes) + ([int64]$x * 2)))
    }
}

function Decode-TPageWord([int]$Raw) {
    $masked = $Raw -band 0x1FF
    $xWord = [int](($masked -band 0x0F) * 64)
    $y = if (($masked -band 0x10) -ne 0) { 256 } else { 0 }
    $mode = [int](($masked -shr 7) -band 3)
    return [pscustomobject]@{
        raw = ("0x{0:X4}" -f $Raw)
        xWord = $xWord
        y = $y
        mode = $mode
        bankOffset = ("0x{0:X5}" -f (([int64]$y * $VramStrideBytes) + ([int64]$xWord * 2)))
    }
}

function New-TripletRow([byte[]]$PackageBytes, [int]$Offset) {
    $w0 = Get-U32 $PackageBytes $Offset
    $w1 = Get-U32 $PackageBytes ($Offset + 4)
    $w2 = Get-U32 $PackageBytes ($Offset + 8)
    $b = New-Object byte[] 12
    [Array]::Copy($PackageBytes, $Offset, $b, 0, 12)
    $candidateClutRaw = [int](($w0 -shr 16) -band 0xFFFF)
    $candidateTpageRaw = [int](($w1 -shr 16) -band 0xFFFF)
    return [pscustomobject]@{
        offset = ("0x{0:X}" -f $Offset)
        words = @(
            ("0x{0:X8}" -f $w0),
            ("0x{0:X8}" -f $w1),
            ("0x{0:X8}" -f $w2)
        )
        bytes = @($b | ForEach-Object { "0x{0:X2}" -f $_ })
        uv = @(
            [pscustomobject]@{ u = [int]$b[0]; v = [int]$b[1] },
            [pscustomobject]@{ u = [int]$b[4]; v = [int]$b[5] },
            [pscustomobject]@{ u = [int]$b[8]; v = [int]$b[9] }
        )
        clutCandidate = Decode-ClutWord $candidateClutRaw
        tpageCandidate = Decode-TPageWord $candidateTpageRaw
    }
}

function Find-TextureTriplets([byte[]]$PackageBytes) {
    $items = New-Object System.Collections.Generic.List[object]
    for ($offset = 0x68; $offset + 12 -le $PackageBytes.Length; $offset += 4) {
        $w0 = Get-U32 $PackageBytes $offset
        $w1 = Get-U32 $PackageBytes ($offset + 4)
        $w2 = Get-U32 $PackageBytes ($offset + 8)
        if ((Test-TextureFirstWord $w0) -and (Test-TextureSecondWord $w1) -and (Test-TextureThirdWord $w2)) {
            [void]$items.Add((New-TripletRow $PackageBytes $offset))
            $offset += 8
        }
    }
    return @($items.ToArray())
}

$resolvedBaseline = Resolve-WorkspacePath $BaselineBinPath
$resolvedOut = Resolve-WorkspacePath $OutPath
[IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOut)) | Out-Null

$stream = [IO.File]::OpenRead($resolvedBaseline)
try {
    $package = Read-WadBytes $stream ($ArtisansEntryBase + $ImportedChestRoot) $ImportedChestLength
}
finally {
    $stream.Dispose()
}

$triplets = @(Find-TextureTriplets $package)
$report = [ordered]@{
    baseline = $resolvedBaseline
    actorPackageEntryRelativeRoot = ("0x{0:X}" -f $ImportedChestRoot)
    tripletCount = $triplets.Count
    clutCandidates = @($triplets | Group-Object { $_.clutCandidate.raw } | Sort-Object Count -Descending | ForEach-Object {
        [pscustomobject]@{ raw = $_.Name; count = $_.Count; exampleBankOffset = $_.Group[0].clutCandidate.bankOffset }
    })
    tpageCandidates = @($triplets | Group-Object { $_.tpageCandidate.raw } | Sort-Object Count -Descending | ForEach-Object {
        [pscustomobject]@{ raw = $_.Name; count = $_.Count; exampleBankOffset = $_.Group[0].tpageCandidate.bankOffset }
    })
    triplets = $triplets
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOut -Encoding UTF8
Write-Host ("Wrote {0} imported chest texture triplets to {1}" -f $triplets.Count, $resolvedOut)
$report.clutCandidates | Select-Object -First 16 | Format-Table -AutoSize
$report.tpageCandidates | Select-Object -First 16 | Format-Table -AutoSize
