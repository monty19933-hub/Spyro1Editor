param(
    [string]$CatalogPath = ".\spyro-level-catalog.json",
    [string]$OutDir = ".\editor-cache",
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $WorkspaceRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $WorkspaceRoot $Path))
}

function Read-U32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or $Offset + 4 -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-I32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or $Offset + 4 -gt $Bytes.Length) { return 0 }
    return [BitConverter]::ToInt32($Bytes, $Offset)
}

function Get-LegacyAliasIndex([int]$TrueIndex) {
    $numerator = ($TrueIndex * 0x58) + 8
    if ($numerator -ge 0 -and ($numerator % 0x50) -eq 0) {
        return [int]($numerator / 0x50)
    }
    return -1
}

function Format-HexByte([int]$Value) {
    return ("0x{0:X2}" -f ($Value -band 0xFF))
}

function Format-HexU32([uint32]$Value) {
    return ("0x{0:X8}" -f $Value)
}

function Decode-MobysFromRam([string]$RamPath) {
    $ram = [System.IO.File]::ReadAllBytes($RamPath)
    $result = New-Object System.Collections.Generic.List[object]
    if ($ram.Length -lt 0x7582C) { return $result }

    $pointer = Read-U32LE $ram 0x75828
    $dynamicPointer = Read-U32LE $ram 0x7573C
    $start = [int]($pointer -band 0x001FFFFF)
    $dynamicStart = [int]($dynamicPointer -band 0x001FFFFF)
    if ($start -lt 0 -or $start + 0x58 -gt $ram.Length) { return $result }

    $count = 512
    if ($dynamicStart -gt $start -and $dynamicStart -le $ram.Length -and (($dynamicStart - $start) % 0x58) -eq 0) {
        $count = [Math]::Min(512, [int](($dynamicStart - $start) / 0x58))
    }

    for ($i = 0; $i -lt $count; $i++) {
        $offset = $start + ($i * 0x58)
        if ($offset + 0x58 -gt $ram.Length) { break }

        $rawX = Read-I32LE $ram ($offset + 0x0C)
        $rawY = Read-I32LE $ram ($offset + 0x10)
        $rawZ = Read-I32LE $ram ($offset + 0x14)
        $type = [int]$ram[$offset + 0x50]
        $state = [int]$ram[$offset + 0x51]
        $specialDataPointer = Read-U32LE $ram ($offset + 0x08)
        $runtimeAddress = [uint32](([uint64]2147483648) + [uint64]$offset)

        [void]$result.Add([ordered]@{
            index = $i
            trueIndex = $i
            legacyIndex = Get-LegacyAliasIndex $i
            x = [Math]::Round($rawX / 16.0, 4)
            y = [Math]::Round($rawY / 16.0, 4)
            z = [Math]::Round($rawZ / 16.0, 4)
            rawX = $rawX
            rawY = $rawY
            rawZ = $rawZ
            typeHex = Format-HexByte $type
            stateHex = Format-HexByte $state
            runtimeAddress = Format-HexU32 $runtimeAddress
            specialDataPointer = Format-HexU32 $specialDataPointer
            sourceByte36Hex = Format-HexByte ([int]$ram[$offset + 0x36])
            sourceByte37Hex = Format-HexByte ([int]$ram[$offset + 0x37])
            sourceByte4FHex = Format-HexByte ([int]$ram[$offset + 0x4F])
            flag4AHex = Format-HexByte ([int]$ram[$offset + 0x52])
            flag4BHex = Format-HexByte ([int]$ram[$offset + 0x53])
        })
    }

    return $result
}

$catalogFull = Resolve-WorkspacePath $CatalogPath
if (-not (Test-Path -LiteralPath $catalogFull)) {
    throw "Missing level catalog: $catalogFull"
}

$outFull = Resolve-WorkspacePath $OutDir
if ((Test-Path -LiteralPath $outFull) -and $Force) {
    Remove-Item -LiteralPath $outFull -Recurse -Force
}
if (-not (Test-Path -LiteralPath $outFull)) {
    New-Item -ItemType Directory -Path $outFull | Out-Null
}

$catalog = Get-Content -LiteralPath $catalogFull -Raw | ConvertFrom-Json
$summary = New-Object System.Collections.Generic.List[object]

foreach ($level in $catalog.levels) {
    $key = [string]$level.key
    if ([string]::IsNullOrWhiteSpace($key)) { continue }

    $ramCandidates = @(
        ".\$key-before-clean.bin"
    )
    if ($key -eq "stonehill") {
        $ramCandidates = @(
            ".\stonehill-before-clean.bin",
            ".\stonehill-before-gem-clean.bin",
            ".\duckstation-mainram-fresh-stonehill.bin"
        )
    }

    $ramPath = $null
    foreach ($candidate in $ramCandidates) {
        $full = Resolve-WorkspacePath $candidate
        if (Test-Path -LiteralPath $full) {
            $ramPath = $full
            break
        }
    }

    $overlayPath = Resolve-WorkspacePath ".\$key-runtime-scene-editor-overlay.json"
    $cacheMobyPath = Join-Path $outFull "$key-mobys.json"
    $cacheOverlayPath = Join-Path $outFull "$key-runtime-scene-editor-overlay.json"

    $mobyCount = 0
    $hasMobys = $false
    if ($ramPath) {
        $mobys = Decode-MobysFromRam $ramPath
        $mobyCount = $mobys.Count
        $mobyCache = [ordered]@{
            generatedBy = "New-SpyroPortableEditorCache.ps1"
            generatedAt = (Get-Date).ToString("o")
            purpose = "Portable native editor moby cache decoded from a clean DuckStation RAM capture. This lets the editor load the level without the raw RAM dump."
            levelKey = $key
            displayName = [string]$level.displayName
            levelId = [int]$level.levelId
            sourceRamFile = Split-Path -Leaf $ramPath
            recordStride = "0x58"
            mobyCount = $mobyCount
            mobys = $mobys
        }
        $mobyCache | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $cacheMobyPath -Encoding UTF8
        $hasMobys = $true
    }

    $hasOverlay = $false
    if (Test-Path -LiteralPath $overlayPath) {
        Copy-Item -LiteralPath $overlayPath -Destination $cacheOverlayPath -Force
        $hasOverlay = $true
    }

    [void]$summary.Add([ordered]@{
        key = $key
        displayName = [string]$level.displayName
        hasRam = [bool]$ramPath
        hasMobyCache = $hasMobys
        mobyCount = $mobyCount
        hasOverlay = $hasOverlay
        sourceRamFile = if ($ramPath) { Split-Path -Leaf $ramPath } else { "" }
        mobyCache = if ($hasMobys) { "editor-cache/$key-mobys.json" } else { "" }
        overlayCache = if ($hasOverlay) { "editor-cache/$key-runtime-scene-editor-overlay.json" } else { "" }
    })
}

$index = [ordered]@{
    generatedBy = "New-SpyroPortableEditorCache.ps1"
    generatedAt = (Get-Date).ToString("o")
    purpose = "Portable editor cache index. Copy this folder with the editor to load captured levels without raw RAM dumps."
    levelCount = $summary.Count
    levels = $summary
}
$index | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outFull "index.json") -Encoding UTF8

$missing = @($summary | Where-Object { -not $_.hasMobyCache -or -not $_.hasOverlay })
Write-Host ("Portable editor cache written to {0}" -f $outFull)
Write-Host ("Cached {0} level(s); {1} incomplete." -f $summary.Count, $missing.Count)
if ($missing.Count -gt 0) {
    Write-Host "Incomplete levels:"
    foreach ($item in $missing) {
        Write-Host (" - {0}: mobys={1}, overlay={2}" -f $item.displayName, $item.hasMobyCache, $item.hasOverlay)
    }
}
