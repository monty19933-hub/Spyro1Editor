param(
    [Alias("ImagePath")]
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [string]$StringOffsetHex = "",
    [string]$OriginalText = "",
    [string]$ReplacementText = "",
    [string]$OutputPrefix = "",
    [string]$OutCatalogPath = ".\_local\text\spyro-exe-string-catalog.json",
    [int]$MinLength = 3,
    [switch]$CatalogOnly,
    [switch]$PlanOnly
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-U32LE([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-UserSector([IO.FileStream]$Stream, $Layout, [int]$Lba) {
    $buffer = New-Object byte[] 2048
    $Stream.Position = ([int64]$Lba * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset
    [void]$Stream.Read($buffer, 0, 2048)
    return ,$buffer
}

function Test-Pvd([IO.FileStream]$Stream, [int]$SectorSize, [int]$UserOffset) {
    if ($Stream.Length -lt ((16 * $SectorSize) + $UserOffset + 2048)) { return $null }
    $buffer = New-Object byte[] 2048
    $Stream.Position = (16L * [int64]$SectorSize) + [int64]$UserOffset
    [void]$Stream.Read($buffer, 0, 2048)
    $signature = [Text.Encoding]::ASCII.GetString($buffer, 1, 5)
    if ($buffer[0] -ne 1 -or $signature -ne "CD001") { return $null }
    $root = New-Object byte[] 34
    [Array]::Copy($buffer, 156, $root, 0, 34)
    return [ordered]@{
        sectorSize = $SectorSize
        userOffset = $UserOffset
        rootExtent = [int](Get-U32LE $root 2)
        rootLength = [int](Get-U32LE $root 10)
    }
}

function Detect-DiscLayout([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
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

function Find-RootFileRecord([IO.FileStream]$Stream, $Layout, [string]$NameRegex) {
    $data = New-Object byte[] $Layout.rootLength
    $remaining = $data.Length
    $written = 0
    $sector = [int]$Layout.rootExtent
    while ($remaining -gt 0) {
        $sectorBytes = Read-UserSector $Stream $Layout $sector
        $toCopy = [Math]::Min(2048, $remaining)
        [Array]::Copy($sectorBytes, 0, $data, $written, $toCopy)
        $written += $toCopy
        $remaining -= $toCopy
        $sector++
    }

    $offset = 0
    while ($offset -lt $data.Length) {
        $recordLength = [int]$data[$offset]
        if ($recordLength -eq 0) {
            $offset = ([Math]::Floor($offset / 2048) + 1) * 2048
            continue
        }
        if (($offset + $recordLength) -gt $data.Length -or $recordLength -lt 34) { break }
        $nameLength = [int]$data[$offset + 32]
        $name = [Text.Encoding]::ASCII.GetString($data, ($offset + 33), $nameLength)
        $cleanName = ($name -replace ';1$', '')
        if ($cleanName -match $NameRegex) {
            return [ordered]@{
                name = $cleanName
                lba = [int](Get-U32LE $data ($offset + 2))
                size = [int](Get-U32LE $data ($offset + 10))
            }
        }
        $offset += $recordLength
    }
    throw "Could not find root file record matching $NameRegex."
}

function Read-FileBytes([IO.FileStream]$Stream, $Layout, [int]$FileLba, [int64]$FileOffset, [int]$Length) {
    $result = New-Object byte[] $Length
    $remaining = $Length
    $written = 0
    $absolute = $FileOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($FileLba + [Math]::Floor([double]($absolute / 2048)))
        $toRead = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + [int64]$sectorOffset
        [void]$Stream.Read($result, $written, $toRead)
        $written += $toRead
        $remaining -= $toRead
        $absolute += $toRead
    }
    return ,$result
}

function Write-FileBytes([IO.FileStream]$Stream, $Layout, [int]$FileLba, [int64]$FileOffset, [byte[]]$Bytes) {
    $remaining = $Bytes.Length
    $written = 0
    $absolute = $FileOffset
    while ($remaining -gt 0) {
        $sectorOffset = [int]($absolute % 2048)
        $sector = [int]($FileLba + [Math]::Floor([double]($absolute / 2048)))
        $toWrite = [Math]::Min(2048 - $sectorOffset, $remaining)
        $Stream.Position = ([int64]$sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + [int64]$sectorOffset
        $Stream.Write($Bytes, $written, $toWrite)
        $written += $toWrite
        $remaining -= $toWrite
        $absolute += $toWrite
    }
}

function Convert-FileOffsetToImageOffset($Layout, [int]$FileLba, [int64]$FileOffset) {
    $sector = [int64]$FileLba + [int64][Math]::Floor([double]($FileOffset / 2048))
    $sectorOffset = [int64]($FileOffset % 2048)
    return ($sector * [int64]$Layout.sectorSize) + [int64]$Layout.userOffset + $sectorOffset
}

function Convert-HexToInt64([string]$Value) {
    $clean = $Value.Trim()
    if ($clean.StartsWith("0x", [StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [Convert]::ToInt64($clean, 10)
}

function Convert-BytesToHex([byte[]]$Bytes) {
    $parts = New-Object Collections.Generic.List[string]
    foreach ($b in $Bytes) { [void]$parts.Add(("{0:X2}" -f $b)) }
    return ($parts -join " ")
}

function Get-CueText([string]$SourceCue, [string]$OutBinName) {
    if (-not (Test-Path -LiteralPath $SourceCue)) {
        return @(
            ("FILE ""{0}"" BINARY" -f $OutBinName),
            "  TRACK 01 MODE2/2352",
            "    INDEX 01 00:00:00"
        )
    }
    $lines = Get-Content -LiteralPath $SourceCue
    $updated = New-Object Collections.Generic.List[string]
    $replaced = $false
    foreach ($line in $lines) {
        if (-not $replaced -and $line -match '^\s*FILE\s+".*"\s+BINARY\s*$') {
            [void]$updated.Add(("FILE ""{0}"" BINARY" -f $OutBinName))
            $replaced = $true
        }
        else {
            [void]$updated.Add($line)
        }
    }
    if (-not $replaced) {
        $updated.Insert(0, ("FILE ""{0}"" BINARY" -f $OutBinName))
    }
    return @($updated.ToArray())
}

function Convert-ToSafeSlug([string]$Text) {
    $slug = $Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) { return "text" }
    return $slug
}

function Test-PrintableAscii([byte]$Byte) {
    return $Byte -ge 0x20 -and $Byte -le 0x7E
}

function Get-StringKind([string]$Text) {
    if ($Text -match '%[0-9]*[sdxX]') { return "format" }
    if ($Text -match '^(ARTISANS|STONE HILL|DARK HOLLOW|TOWN SQUARE|SUNNY FLIGHT|DRY CANYON|CLIFF TOWN|ICE CAVERN|DOCTOR SHEMP|NIGHT FLIGHT|PEACE KEEPERS|MAGIC CRAFTERS|ALPINE RIDGE|HIGH CAVES|WIZARD PEAK|BLOWHARD|CRYSTAL FLIGHT|BEAST MAKERS|TERRACE VILLAGE|MISTY BOG|TREE TOPS|METALHEAD|WILD FLIGHT|DREAM WEAVERS|DARK PASSAGE|LOFTY CASTLE|HAUNTED TOWERS|ICY FLIGHT|GNASTY''S WORLD|GNORC COVE|TWILIGHT HARBOR|GNASTY GNORC|GNASTY''S LOOT)$') { return "level-name" }
    if ($Text -match '^(RETURN HOME|PRESS START|PAUSED|YES|NO|OPTIONS|QUIT|CONTINUE|INVENTORY|EXIT LEVEL|SAVE GAME|REPLAY DRAGON|TREASURE FOUND|TOTAL TREASURE|ENTERING|RETURNING|DEMO MODE)') { return "ui" }
    if ($Text.Length -gt 28 -or $Text -match '[,.!?]') { return "dialogue" }
    return "unknown"
}

function New-StringCatalog([byte[]]$ExeBytes, $Exe, $Layout, [int]$MinLength) {
    $strings = New-Object Collections.Generic.List[object]
    $i = 0
    while ($i -lt $ExeBytes.Length) {
        if (-not (Test-PrintableAscii $ExeBytes[$i])) {
            $i++
            continue
        }
        $start = $i
        while ($i -lt $ExeBytes.Length -and (Test-PrintableAscii $ExeBytes[$i])) {
            $i++
        }
        $length = $i - $start
        if ($length -ge $MinLength) {
            $text = [Text.Encoding]::ASCII.GetString($ExeBytes, $start, $length)
            if ($text.Trim().Length -ge $MinLength -and $text -match '[A-Za-z]') {
                [void]$strings.Add([pscustomobject][ordered]@{
                    text = $text
                    offset = ("0x{0:X}" -f $start)
                    imageOffset = ("0x{0:X}" -f (Convert-FileOffsetToImageOffset $Layout ([int]$Exe.lba) ([int64]$start)))
                    length = $length
                    maxReplacementBytes = $length
                    kind = Get-StringKind $text
                    safeFixedSlot = $true
                })
            }
        }
        $i++
    }
    return @($strings.ToArray())
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$outCatalog = Resolve-WorkspacePath $OutCatalogPath
if (-not (Test-Path -LiteralPath $sourceImage)) { throw "Missing source image: $sourceImage" }

$layout = Detect-DiscLayout $sourceImage
$stream = [IO.File]::OpenRead($sourceImage)
try {
    $exe = Find-RootFileRecord $stream $layout '^(SCUS|SCES|SCPS|SLUS|SLES|SLPS)'
    $exeBytes = Read-FileBytes $stream $layout ([int]$exe.lba) 0 ([int]$exe.size)
}
finally {
    $stream.Dispose()
}

if ($CatalogOnly) {
    $strings = New-StringCatalog $exeBytes $exe $layout $MinLength
    $catalog = [pscustomobject][ordered]@{
        generatedAt = (Get-Date).ToString("o")
        sourceImagePath = $sourceImage
        executable = [ordered]@{
            name = [string]$exe.name
            lba = [int]$exe.lba
            size = [int]$exe.size
        }
        stringCount = @($strings).Count
        strings = @($strings)
    }
    $catalogDir = Split-Path -Parent $outCatalog
    if (-not [string]::IsNullOrWhiteSpace($catalogDir)) {
        New-Item -ItemType Directory -Force -Path $catalogDir | Out-Null
    }
    $catalog | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outCatalog -Encoding UTF8
    Write-Host "Wrote $outCatalog"
    Write-Host "Catalog strings: $($catalog.stringCount)"
    return
}

if ([string]::IsNullOrWhiteSpace($ReplacementText)) { throw "ReplacementText is required unless -CatalogOnly is used." }

$targetOffset = -1
$original = $OriginalText
if (-not [string]::IsNullOrWhiteSpace($StringOffsetHex)) {
    $targetOffset = [int](Convert-HexToInt64 $StringOffsetHex)
    if ($targetOffset -lt 0 -or $targetOffset -ge $exeBytes.Length) { throw "StringOffsetHex is outside the executable." }
    $end = $targetOffset
    while ($end -lt $exeBytes.Length -and (Test-PrintableAscii $exeBytes[$end])) { $end++ }
    if ($end -le $targetOffset) { throw "No printable ASCII string starts at $StringOffsetHex." }
    $original = [Text.Encoding]::ASCII.GetString($exeBytes, $targetOffset, $end - $targetOffset)
}
elseif (-not [string]::IsNullOrWhiteSpace($OriginalText)) {
    $needle = [Text.Encoding]::ASCII.GetBytes($OriginalText)
    for ($i = 0; $i -le ($exeBytes.Length - $needle.Length); $i++) {
        $match = $true
        for ($j = 0; $j -lt $needle.Length; $j++) {
            if ($exeBytes[$i + $j] -ne $needle[$j]) { $match = $false; break }
        }
        if ($match) {
            if ($targetOffset -ge 0) { throw "OriginalText appears more than once. Use StringOffsetHex from the catalog." }
            $targetOffset = $i
        }
    }
    if ($targetOffset -lt 0) { throw "Could not find OriginalText in the executable." }
}
else {
    throw "Use StringOffsetHex from the catalog, or provide OriginalText."
}

if ([string]::IsNullOrWhiteSpace($original)) { throw "Could not resolve original string." }
$replacement = $ReplacementText.Trim()
if ([string]::IsNullOrWhiteSpace($replacement)) { throw "ReplacementText cannot be empty." }
if ($replacement -notmatch '^[ -~]+$') { throw "ReplacementText must use printable ASCII characters for this fixed-slot editor." }

$originalBytes = [Text.Encoding]::ASCII.GetBytes($original)
$replacementBytes = [Text.Encoding]::ASCII.GetBytes($replacement)
if ($replacementBytes.Length -gt $originalBytes.Length) {
    throw ("Replacement '{0}' is {1} bytes, but the selected string only has {2} bytes. Use a shorter same-slot edit." -f $replacement, $replacementBytes.Length, $originalBytes.Length)
}

$patchBytes = New-Object byte[] $originalBytes.Length
[Array]::Copy($replacementBytes, 0, $patchBytes, 0, $replacementBytes.Length)
$beforeBytes = New-Object byte[] $originalBytes.Length
[Array]::Copy($exeBytes, $targetOffset, $beforeBytes, 0, $beforeBytes.Length)

if ([string]::IsNullOrWhiteSpace($OutputPrefix)) {
    $OutputPrefix = ".\Spyro the Dragon (USA)-text-$((Convert-ToSafeSlug $original))-$(Convert-ToSafeSlug $replacement)"
}
$outImage = Resolve-WorkspacePath ("{0}.bin" -f $OutputPrefix)
$outCue = Resolve-WorkspacePath ("{0}.cue" -f $OutputPrefix)
$outPlan = Resolve-WorkspacePath ("{0}.exe-string-plan.json" -f $OutputPrefix)

$patch = [pscustomobject][ordered]@{
    kind = "exe-fixed-slot-string"
    originalText = $original
    replacementText = $replacement
    exeName = [string]$exe.name
    exeLba = [int]$exe.lba
    exeFileOffset = ("0x{0:X}" -f $targetOffset)
    byteLength = $patchBytes.Length
    imageOffset = ("0x{0:X}" -f (Convert-FileOffsetToImageOffset $layout ([int]$exe.lba) ([int64]$targetOffset)))
    beforeHexPreview = Convert-BytesToHex $beforeBytes
    afterHexPreview = Convert-BytesToHex $patchBytes
}

$plan = [pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("o")
    sourceImagePath = $sourceImage
    outputImagePath = $outImage
    outputCuePath = $outCue
    originalText = $original
    replacementText = $replacement
    notes = @(
        "Patches one executable ASCII string by exact file offset.",
        "Replacement is fixed-length or shorter and remaining bytes are nulled.",
        "Longer text and pointer-moving are deliberately not attempted by this tool."
    )
    patches = @($patch)
}

$planDir = Split-Path -Parent $outPlan
if (-not [string]::IsNullOrWhiteSpace($planDir)) {
    New-Item -ItemType Directory -Force -Path $planDir | Out-Null
}
$plan | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outPlan -Encoding UTF8

if (-not $PlanOnly) {
    Copy-Item -LiteralPath $sourceImage -Destination $outImage -Force
    $outStream = [IO.File]::Open($outImage, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::Read)
    try {
        Write-FileBytes $outStream $layout ([int]$exe.lba) $targetOffset $patchBytes
    }
    finally {
        $outStream.Dispose()
    }
    $cueText = Get-CueText $sourceCue ([IO.Path]::GetFileName($outImage))
    $cueText | Set-Content -LiteralPath $outCue -Encoding ASCII
}

Write-Host "Wrote $outPlan"
if (-not $PlanOnly) {
    Write-Host "Wrote $outImage"
    Write-Host "Wrote $outCue"
}
Write-Host ("String patch: '{0}' -> '{1}', {2} bytes at 0x{3:X}" -f $original, $replacement, $patchBytes.Length, $targetOffset)
