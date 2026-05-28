param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$CatalogPath = ".\spyro-level-catalog.json",
    [int]$WadLba = 37
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $WorkspaceRoot $Path
}

function FmtHex([int64]$Value) {
    return "0x{0:X}" -f $Value
}

function Get-U16([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Get-U32([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-WadBytes([IO.FileStream]$Stream, [int64]$Offset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $Offset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int64]$WadLba + [int64][Math]::Floor($absolute / 2048)
        $imageOffset = ($sector * 2352L) + 24L + $sectorOffset
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = $imageOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return $result
}

function Convert-HexOrInt64($Value) {
    if ($Value -is [string]) {
        $text = $Value.Trim()
        if ($text.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
            return [Convert]::ToInt64($text.Substring(2), 16)
        }
        return [Convert]::ToInt64($text, 10)
    }
    return [Convert]::ToInt64($Value)
}

function Get-LevelBase($Catalog, [string]$Key) {
    $level = $Catalog.levels | Where-Object { $_.key -eq $Key } | Select-Object -First 1
    if ($null -eq $level) { throw "Missing level catalog entry: $Key" }
    return (Convert-HexOrInt64 $level.sourceTableWadOffset) - (Convert-HexOrInt64 $level.sourceTableRelativeOffset)
}

function Get-ActorRoots([IO.FileStream]$Stream, [int64]$LevelBase) {
    $head = Read-WadBytes $Stream $LevelBase 0x220
    $rows = New-Object System.Collections.Generic.List[object]
    for ($slot = 0x50; $slot -le 0x148; $slot += 4) {
        $index = [int](($slot - 0x50) / 4)
        [void]$rows.Add([pscustomobject]@{
            index = $index
            slot = $slot
            root = [int64](Get-U32 $head $slot)
            actor = [int](Get-U16 $head (0x150 + ($index * 2)))
            next = [int64]0
            capacity = [int64]0
        })
    }

    $sorted = @($rows | Where-Object { $_.root -gt 0 } | Sort-Object root)
    foreach ($row in $rows) {
        $nextRow = $sorted | Where-Object { $_.root -gt $row.root } | Select-Object -First 1
        if ($null -ne $nextRow) {
            $row.next = [int64]$nextRow.root
            $row.capacity = [int64]$nextRow.root - [int64]$row.root
        }
    }
    return @($rows.ToArray())
}

function Get-ExpectedWords([int]$Command) {
    switch ($Command) {
        0x24 { return 7 }
        0x2C { return 9 }
        0x34 { return 9 }
        0x3C { return 12 }
        default { return 0 }
    }
}

function Get-UvWordIndexes([int]$Command) {
    switch ($Command) {
        0x24 { return @(2, 4, 6) }
        0x2C { return @(2, 4, 6, 8) }
        0x34 { return @(2, 5, 8) }
        0x3C { return @(2, 5, 8, 11) }
        default { return @() }
    }
}

function Find-TexturedPrimitives([byte[]]$Bytes) {
    $results = New-Object System.Collections.Generic.List[object]
    for ($offset = 0; $offset + 64 -lt $Bytes.Length; $offset += 4) {
        $command = [int]$Bytes[$offset + 7]
        $expected = Get-ExpectedWords $command
        if ($expected -eq 0 -or ($offset + 4 + ($expected * 4)) -gt $Bytes.Length) { continue }

        $tag = Get-U32 $Bytes $offset
        $tagWords = [int](($tag -shr 24) -band 0xFF)
        if ($tagWords -lt $expected -or $tagWords -gt 16) { continue }

        $uvWords = @(Get-UvWordIndexes $command)
        if ($uvWords.Count -lt 2) { continue }

        $uv0Offset = $offset + 4 + ([int]$uvWords[0] * 4)
        $uv1Offset = $offset + 4 + ([int]$uvWords[1] * 4)
        $clut = [int](Get-U16 $Bytes ($uv0Offset + 2))
        $tpage = [int](Get-U16 $Bytes ($uv1Offset + 2))

        [void]$results.Add([pscustomobject]@{
            offset = $offset
            command = $command
            tagWords = $tagWords
            expectedWords = $expected
            clut = $clut
            tpage = $tpage
            uv0 = ("{0},{1}" -f $Bytes[$uv0Offset], $Bytes[$uv0Offset + 1])
            uv1 = ("{0},{1}" -f $Bytes[$uv1Offset], $Bytes[$uv1Offset + 1])
        })
    }
    return @($results.ToArray())
}

function Write-RootRows([string]$Title, $Rows) {
    Write-Host ""
    Write-Host $Title
    $Rows |
        Select-Object index,
            @{ Name = "slot"; Expression = { FmtHex $_.slot } },
            @{ Name = "root"; Expression = { FmtHex $_.root } },
            @{ Name = "next"; Expression = { FmtHex $_.next } },
            @{ Name = "capacity"; Expression = { FmtHex $_.capacity } },
            @{ Name = "actor"; Expression = { "0x{0:X4}" -f $_.actor } } |
        Format-Table -AutoSize
}

function Write-PrimitiveSummary([string]$Name, [byte[]]$Bytes) {
    $primitives = @(Find-TexturedPrimitives $Bytes)
    Write-Host ""
    Write-Host ("--- {0}: len {1}, textured primitive candidates {2} ---" -f $Name, (FmtHex $Bytes.Length), $primitives.Count)
    $primitives |
        Group-Object { "clut 0x{0:X4} / tpage 0x{1:X4}" -f $_.clut, $_.tpage } |
        Sort-Object Count -Descending |
        Select-Object -First 16 Count, Name |
        Format-Table -AutoSize
    $primitives |
        Select-Object -First 12 @{ Name = "offset"; Expression = { FmtHex $_.offset } },
            @{ Name = "cmd"; Expression = { "0x{0:X2}" -f $_.command } },
            tagWords,
            expectedWords,
            @{ Name = "clut"; Expression = { "0x{0:X4}" -f $_.clut } },
            @{ Name = "tpage"; Expression = { "0x{0:X4}" -f $_.tpage } },
            uv0,
            uv1 |
        Format-Table -AutoSize
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$catalog = Get-Content -LiteralPath (Resolve-WorkspacePath $CatalogPath) -Raw | ConvertFrom-Json
$stream = [IO.File]::OpenRead($resolvedImage)
try {
    $artisansBase = Get-LevelBase $catalog "artisans"
    $peaceKeepersBase = Get-LevelBase $catalog "peacekeepers"
    $artisansRoots = @(Get-ActorRoots $stream $artisansBase)
    $peaceKeepersRoots = @(Get-ActorRoots $stream $peaceKeepersBase)

    Write-Host ("Artisans base: {0}" -f (FmtHex $artisansBase))
    Write-Host ("Peace Keepers base: {0}" -f (FmtHex $peaceKeepersBase))

    Write-RootRows "Artisans roots of interest" (@($artisansRoots | Where-Object { $_.actor -in @(0x00AE, 0x00C2, 0x0156, 0x01DD, 0x01A5, 0x000E) }))
    Write-RootRows "Peace Keepers roots of interest" (@($peaceKeepersRoots | Where-Object { $_.actor -in @(0x00AE, 0x00C2, 0x00C1, 0x01A5, 0x000E) }))

    $packages = @(
        [pscustomobject]@{ name = "Peace Keepers locked chest 00AE"; base = $peaceKeepersBase; root = 0x1B3978; length = 0x1008 },
        [pscustomobject]@{ name = "Peace Keepers locked chest span"; base = $peaceKeepersBase; root = 0x1B3978; length = 0x2830 },
        [pscustomobject]@{ name = "Artisans flame/charge chest 00C2"; base = $artisansBase; root = ($artisansRoots | Where-Object { $_.actor -eq 0x00C2 } | Select-Object -First 1).root; length = ($artisansRoots | Where-Object { $_.actor -eq 0x00C2 } | Select-Object -First 1).capacity },
        [pscustomobject]@{ name = "Artisans shared actor 01A5"; base = $artisansBase; root = ($artisansRoots | Where-Object { $_.actor -eq 0x01A5 } | Select-Object -First 1).root; length = ($artisansRoots | Where-Object { $_.actor -eq 0x01A5 } | Select-Object -First 1).capacity },
        [pscustomobject]@{ name = "Artisans shared actor 000E"; base = $artisansBase; root = ($artisansRoots | Where-Object { $_.actor -eq 0x000E } | Select-Object -First 1).root; length = ($artisansRoots | Where-Object { $_.actor -eq 0x000E } | Select-Object -First 1).capacity },
        [pscustomobject]@{ name = "Artisans tulip/scenery 0156"; base = $artisansBase; root = ($artisansRoots | Where-Object { $_.actor -eq 0x0156 } | Select-Object -First 1).root; length = ($artisansRoots | Where-Object { $_.actor -eq 0x0156 } | Select-Object -First 1).capacity },
        [pscustomobject]@{ name = "Artisans unused slot 01DD"; base = $artisansBase; root = 0x1868D4; length = 0x1920 }
    )

    foreach ($package in $packages) {
        if ([int64]$package.root -le 0 -or [int64]$package.length -le 0) {
            Write-Host ""
            Write-Host ("--- {0}: skipped; no root/capacity ---" -f $package.name)
            continue
        }
        $bytes = Read-WadBytes $stream ([int64]$package.base + [int64]$package.root) ([int]$package.length)
        Write-PrimitiveSummary ("{0} @ {1}" -f $package.name, (FmtHex $package.root)) $bytes
    }
}
finally {
    $stream.Dispose()
}
