param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$WadAnalysisPath = ".\spyro-wad-analysis.json",
    [int[]]$EntryIndexes = @(10, 22, 28),
    [int[]]$ActorIds = @(0x00AE, 0x00C2, 0x01A5, 0x000E, 0x00C1),
    [int]$WadLba = 37
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $WorkspaceRoot $Path
}

function Get-U16([byte[]]$Bytes, [int]$Offset) { return [BitConverter]::ToUInt16($Bytes, $Offset) }
function Get-U32([byte[]]$Bytes, [int]$Offset) { return [BitConverter]::ToUInt32($Bytes, $Offset) }

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

$wad = Get-Content -LiteralPath (Resolve-WorkspacePath $WadAnalysisPath) -Raw | ConvertFrom-Json
$stream = [IO.File]::OpenRead((Resolve-WorkspacePath $ImagePath))
try {
    foreach ($entryIndex in $EntryIndexes) {
        $entry = $wad.entries | Where-Object { [int]$_.index -eq $entryIndex } | Select-Object -First 1
        if ($null -eq $entry) { throw "Missing WAD entry $entryIndex" }
        $head = Read-WadBytes $stream ([int64]$entry.offset) 0x220
        $rows = New-Object System.Collections.Generic.List[object]
        for ($slot = 0x50; $slot -le 0x148; $slot += 4) {
            $rootIndex = [int](($slot - 0x50) / 4)
            [void]$rows.Add([pscustomobject]@{
                index = $rootIndex
                slot = $slot
                root = [int64](Get-U32 $head $slot)
                actor = [int](Get-U16 $head (0x150 + ($rootIndex * 2)))
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

        Write-Host ""
        Write-Host ("Entry {0} roots of interest" -f $entryIndex)
        $rows |
            Where-Object { $ActorIds -contains [int]$_.actor } |
            Select-Object index,
                @{ Name = "slot"; Expression = { "0x{0:X}" -f $_.slot } },
                @{ Name = "root"; Expression = { "0x{0:X}" -f $_.root } },
                @{ Name = "next"; Expression = { "0x{0:X}" -f $_.next } },
                @{ Name = "capacity"; Expression = { "0x{0:X}" -f $_.capacity } },
                @{ Name = "actor"; Expression = { "0x{0:X4}" -f $_.actor } } |
            Format-Table -AutoSize
    }
}
finally {
    $stream.Dispose()
}
