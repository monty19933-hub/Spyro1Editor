param(
    [Alias("ImagePath")]
    [string]$SourceImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$SourceCuePath = ".\Spyro the Dragon (USA).cue",
    [ValidateSet("Artisans", "StoneHill", "DarkHollow", "TownSquare", "SunnyFlight", "DryCanyon", "CliffTown", "IceCavern", "DoctorShemp", "NightFlight", "PeaceKeepers", "MagicCrafters", "AlpineRidge", "HighCaves", "WizardPeak", "Blowhard", "CrystalFlight", "BeastMakers", "TerraceVillage", "MistyBog", "TreeTops", "Metalhead", "WildFlight", "DreamWeavers", "DarkPassage", "LoftyCastle", "HauntedTowers", "IcyFlight", "GnastyWorld", "GnorcCove", "TwilightHarbor", "GnastyGnorc", "GnastyLoot")]
    [string]$TargetLevelKey = "StoneHill",
    [Parameter(Mandatory = $true)]
    [string]$ReplacementName,
    [string]$OutputPrefix = "",
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

function Index-OfBytes([byte[]]$Haystack, [byte[]]$Needle, [int]$Start) {
    if ($Needle.Length -eq 0) { return -1 }
    for ($i = $Start; $i -le ($Haystack.Length - $Needle.Length); $i++) {
        $matched = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) { $matched = $false; break }
        }
        if ($matched) { return $i }
    }
    return -1
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

function Get-TextTargets() {
    return @{
        Artisans = "ARTISANS"
        StoneHill = "STONE HILL"
        DarkHollow = "DARK HOLLOW"
        TownSquare = "TOWN SQUARE"
        SunnyFlight = "SUNNY FLIGHT"
        DryCanyon = "DRY CANYON"
        CliffTown = "CLIFF TOWN"
        IceCavern = "ICE CAVERN"
        DoctorShemp = "DOCTOR SHEMP"
        NightFlight = "NIGHT FLIGHT"
        PeaceKeepers = "PEACE KEEPERS"
        MagicCrafters = "MAGIC CRAFTERS"
        AlpineRidge = "ALPINE RIDGE"
        HighCaves = "HIGH CAVES"
        WizardPeak = "WIZARD PEAK"
        Blowhard = "BLOWHARD"
        CrystalFlight = "CRYSTAL FLIGHT"
        BeastMakers = "BEAST MAKERS"
        TerraceVillage = "TERRACE VILLAGE"
        MistyBog = "MISTY BOG"
        TreeTops = "TREE TOPS"
        Metalhead = "METALHEAD"
        WildFlight = "WILD FLIGHT"
        DreamWeavers = "DREAM WEAVERS"
        DarkPassage = "DARK PASSAGE"
        LoftyCastle = "LOFTY CASTLE"
        HauntedTowers = "HAUNTED TOWERS"
        IcyFlight = "ICY FLIGHT"
        GnastyWorld = "GNASTY'S WORLD"
        GnorcCove = "GNORC COVE"
        TwilightHarbor = "TWILIGHT HARBOR"
        GnastyGnorc = "GNASTY GNORC"
        GnastyLoot = "GNASTY'S LOOT"
    }
}

function Convert-ToSafeSlug([string]$Text) {
    $slug = $Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) { return "text" }
    return $slug
}

$targets = Get-TextTargets
$originalName = [string]$targets[$TargetLevelKey]
if ([string]::IsNullOrWhiteSpace($originalName)) { throw "Unknown level text key: $TargetLevelKey" }

$replacement = $ReplacementName.Trim().ToUpperInvariant()
if ([string]::IsNullOrWhiteSpace($replacement)) { throw "ReplacementName cannot be empty." }
if ($replacement -notmatch '^[A-Z0-9'' .!?&-]+$') {
    throw "ReplacementName uses characters outside the currently safe ASCII set."
}

$originalBytes = [Text.Encoding]::ASCII.GetBytes($originalName)
$replacementBytes = [Text.Encoding]::ASCII.GetBytes($replacement)
if ($replacementBytes.Length -gt $originalBytes.Length) {
    throw ("Replacement '{0}' is {1} bytes, but '{2}' only has {3} bytes. Use a shorter same-slot name for now." -f $replacement, $replacementBytes.Length, $originalName, $originalBytes.Length)
}

if ([string]::IsNullOrWhiteSpace($OutputPrefix)) {
    $OutputPrefix = ".\Spyro the Dragon (USA)-text-$($TargetLevelKey.ToLowerInvariant())-$(Convert-ToSafeSlug $replacement)"
}

$sourceImage = Resolve-WorkspacePath $SourceImagePath
$sourceCue = Resolve-WorkspacePath $SourceCuePath
$outImage = Resolve-WorkspacePath ("{0}.bin" -f $OutputPrefix)
$outCue = Resolve-WorkspacePath ("{0}.cue" -f $OutputPrefix)
$outPlan = Resolve-WorkspacePath ("{0}.level-text-plan.json" -f $OutputPrefix)
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

$anchorStart = [Text.Encoding]::ASCII.GetBytes("DRAGON X")
$anchorEnd = [Text.Encoding]::ASCII.GetBytes("BEHIND YOU.")
$tableStart = Index-OfBytes $exeBytes $anchorStart 0
if ($tableStart -lt 0) { throw "Could not find the level text table anchor in $($exe.name)." }
$tableEnd = Index-OfBytes $exeBytes $anchorEnd $tableStart
if ($tableEnd -lt 0) { throw "Could not find the end of the level text table in $($exe.name)." }

$targetOffset = Index-OfBytes $exeBytes $originalBytes $tableStart
while ($targetOffset -ge 0 -and $targetOffset -gt $tableEnd) {
    $targetOffset = Index-OfBytes $exeBytes $originalBytes ($targetOffset + 1)
}
if ($targetOffset -lt 0 -or $targetOffset -gt $tableEnd) {
    throw "Could not find '$originalName' inside the anchored level-name table."
}

$patchBytes = New-Object byte[] $originalBytes.Length
[Array]::Copy($replacementBytes, 0, $patchBytes, 0, $replacementBytes.Length)
$beforeBytes = New-Object byte[] $originalBytes.Length
[Array]::Copy($exeBytes, $targetOffset, $beforeBytes, 0, $beforeBytes.Length)

$patch = [pscustomobject][ordered]@{
    kind = "exe-level-name-string"
    targetLevelKey = $TargetLevelKey
    originalName = $originalName
    replacementName = $replacement
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
    targetLevelKey = $TargetLevelKey
    originalName = $originalName
    replacementName = $replacement
    notes = @(
        "Patches the anchored executable level-name string table only.",
        "Replacement is fixed-length or shorter and remaining bytes are nulled.",
        "This is expected to affect executable text render paths such as fly-in titles; portal label behavior still needs in-game confirmation."
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
Write-Host ("Level text patch: {0} -> {1}, {2} bytes" -f $originalName, $replacement, $patchBytes.Length)
