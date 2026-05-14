param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$OutPath = ""
)

Set-StrictMode -Version 2.0

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-UInt16LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-UserSector([System.IO.FileStream]$Stream, [int]$Lba, [int]$SectorSize, [int]$UserOffset) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * [int64]$SectorSize) + [int64]$UserOffset
    [void]$Stream.Read($buffer, 0, 2048)
    return $buffer
}

function Test-Pvd([System.IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    if ($Stream.Length -lt ((16 * $SectorSize) + $UserOffset + 2048)) { return $null }
    $buffer = Read-UserSector $Stream 16 $SectorSize $UserOffset
    $signature = [System.Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    $root = New-Object byte[] 34
    [Array]::Copy($buffer, 156, $root, 0, 34)
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        volumeId = [System.Text.Encoding]::ASCII.GetString($buffer, 40, 32).Trim()
        volumeSpaceSectors = [int](Get-UInt32LE $buffer 80)
        rootExtent = [int](Get-UInt32LE $root 2)
        rootLength = [int](Get-UInt32LE $root 10)
    }
}

function Detect-Layout([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        foreach ($candidate in @(
            @{ SectorSize = 2048; UserOffset = 0 },
            @{ SectorSize = 2352; UserOffset = 24 },
            @{ SectorSize = 2336; UserOffset = 8 }
        )) {
            $layout = Test-Pvd $stream $candidate.SectorSize $candidate.UserOffset
            if ($null -ne $layout) { return $layout }
        }
    }
    finally {
        $stream.Dispose()
    }
    throw "Could not find an ISO9660 primary volume descriptor."
}

function Read-UserData([System.IO.FileStream]$Stream, $Layout, [int]$Lba, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $sector = $Lba
    while ($remaining -gt 0) {
        $toRead = [Math]::Min(2048, $remaining)
        $sectorBytes = Read-UserSector $Stream $sector $Layout.sectorSize $Layout.userOffset
        [Array]::Copy($sectorBytes, 0, $result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $sector++
    }
    return $result
}

function Parse-Directory([System.IO.FileStream]$Stream, $Layout, [int]$Extent, [int]$Length, [string]$Path, [int]$Depth) {
    if ($Depth -gt 8) { return @() }
    $data = Read-UserData $Stream $Layout $Extent $Length
    $entries = @()
    $offset = 0
    while ($offset -lt $data.Length) {
        $recordLength = [int]$data[$offset]
        if ($recordLength -eq 0) {
            $offset = ([Math]::Floor($offset / 2048) + 1) * 2048
            continue
        }
        if (($offset + $recordLength) -gt $data.Length -or $recordLength -lt 34) { break }
        $lba = [int](Get-UInt32LE $data ($offset + 2))
        $size = [int](Get-UInt32LE $data ($offset + 10))
        $flags = [int]$data[$offset + 25]
        $nameLength = [int]$data[$offset + 32]
        $name = [System.Text.Encoding]::ASCII.GetString($data, ($offset + 33), $nameLength)
        $name = ($name -replace ';1$', '')
        if ($name -ne [char]0 -and $name -ne [char]1) {
            $fullPath = $(if ([string]::IsNullOrWhiteSpace($Path)) { $name } else { "$Path/$name" })
            $isDirectory = (($flags -band 2) -ne 0)
            $entry = [ordered]@{
                path = $fullPath
                name = $name
                lba = $lba
                size = $size
                isDirectory = $isDirectory
                imageOffset = ([int64]$lba * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
            }
            $entries += $entry
            if ($isDirectory) {
                $entries += Parse-Directory $Stream $Layout $lba $size $fullPath ($Depth + 1)
            }
        }
        $offset += $recordLength
    }
    return $entries
}

function Find-AsciiMarkers([System.IO.FileStream]$Stream) {
    $markers = [ordered]@{
        serials = @()
        fileNames = @()
        levelish = @()
    }
    $bufferSize = 1048576
    $buffer = New-Object byte[] $bufferSize
    $overlap = ""
    $Stream.Position = 0
    while ($true) {
        $read = $Stream.Read($buffer, 0, $bufferSize)
        if ($read -le 0) { break }
        $text = $overlap + [System.Text.Encoding]::ASCII.GetString($buffer, 0, $read)
        foreach ($match in [regex]::Matches($text, '(SCUS|SCES|SCPS|SLUS|SLES|SLPS)[_.-]?\d{3}[_.-]?\d{2}')) {
            if ($markers.serials -notcontains $match.Value) { $markers.serials += $match.Value }
        }
        foreach ($match in [regex]::Matches($text, '[A-Z0-9_]{1,12}\.(WAD|DAT|BIN|XA|STR|SEQ|VH|VB)')) {
            if ($markers.fileNames.Count -lt 100 -and $markers.fileNames -notcontains $match.Value) {
                $markers.fileNames += $match.Value
            }
        }
        foreach ($match in [regex]::Matches($text, '(ARTISANS|STONE|HILL|DARK|HOLLOW|TOWN|SQUARE|PEACE|KEEPERS|DRY|CANYON|CLIFF|MAGIC|CRAFTERS|ALPINE|BEAST|MAKERS|TREE|TOPS|DREAM|WEAVERS|HAUNTED|TOWERS|GNASTY|GNORC)')) {
            if ($markers.levelish.Count -lt 100 -and $markers.levelish -notcontains $match.Value) {
                $markers.levelish += $match.Value
            }
        }
        $overlap = $text.Substring([Math]::Max(0, $text.Length - 64))
    }
    return $markers
}

function Read-ExecutableHeader([System.IO.FileStream]$Stream, $Layout, $Entries) {
    $exe = $Entries | Where-Object { $_.name -match '^(SCUS|SCES|SCPS|SLUS|SLES|SLPS)' } | Select-Object -First 1
    if ($null -eq $exe) { return $null }
    $bytes = Read-UserData $Stream $Layout $exe.lba ([Math]::Min($exe.size, 2048))
    return [ordered]@{
        path = $exe.path
        lba = $exe.lba
        size = $exe.size
        magic = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 8).Trim([char]0)
        initialPc = ("0x{0:X8}" -f (Get-UInt32LE $bytes 0x10))
        destinationAddress = ("0x{0:X8}" -f (Get-UInt32LE $bytes 0x18))
        fileSize = [int](Get-UInt32LE $bytes 0x1C)
    }
}

$item = Get-Item -LiteralPath $ImagePath
$layout = Detect-Layout $ImagePath
$stream = [System.IO.File]::OpenRead($ImagePath)
try {
    $entries = Parse-Directory $stream $layout $layout.rootExtent $layout.rootLength "" 0
    $exeHeader = Read-ExecutableHeader $stream $layout $entries
    $markers = Find-AsciiMarkers $stream
}
finally {
    $stream.Dispose()
}

$result = [ordered]@{
    image = [ordered]@{
        path = $item.FullName
        sizeBytes = [int64]$item.Length
        sectorCount = [int]($item.Length / $layout.sectorSize)
    }
    layout = $layout
    files = $entries
    executable = $exeHeader
    markers = $markers
}

$json = $result | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutPath)) {
    $json | Set-Content -LiteralPath $OutPath -Encoding UTF8
}
else {
    $json
}
