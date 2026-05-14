param(
    [string]$ImagePath = ".\Spyro the Dragon (USA).bin",
    [string]$RamPath = ".\stonehill-before-gem-clean.bin",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$SourceSearchPath = ".\stonehill-runtime-terrain-source-search.json",
    [string]$OutJsonPath = ".\stonehill-scene-sector-repack-report.json",
    [string]$OutMarkdownPath = ".\stonehill-scene-sector-repack-report.md",
    [string]$SceneRuntimeAddress = "0x8008C134",
    [int]$WadLba = 37,
    [int]$RequestedExtraBytes = 96
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
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Default
    }
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

function Convert-HexTextToInt64([string]$Text, [int64]$Default = -1) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Default }
    $clean = $Text.Trim()
    if ($clean.StartsWith("0x", [System.StringComparison]::OrdinalIgnoreCase)) {
        return [Convert]::ToInt64($clean.Substring(2), 16)
    }
    return [int64]$clean
}

function Convert-PsxAddressToRamOffset([uint32]$Address, [int]$RamLength) {
    $ramBase = [Convert]::ToUInt32("80000000", 16)
    $ramEnd = [Convert]::ToUInt32("80200000", 16)
    if ($Address -lt $ramBase -or $Address -ge $ramEnd) { return -1 }
    $offset = [int]($Address - $ramBase)
    if ($offset -lt 0 -or $offset -ge $RamLength) { return -1 }
    return $offset
}

function Convert-WadOffsetToImageOffset([int64]$WadOffset) {
    $sector = [int64]$WadLba + [int64][Math]::Floor($WadOffset / 2048)
    $sectorOffset = [int]($WadOffset % 2048)
    return ($sector * 2352L) + 24L + [int64]$sectorOffset
}

function Get-UInt32LE([byte[]]$Bytes, [int]$Offset) {
    if ($Offset -lt 0 -or ($Offset + 4) -gt $Bytes.Length) { return [uint32]0 }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

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
    return [pscustomobject]([ordered]@{
        offset = $Offset
        sizeBytes = $sizeBytes
        numLpVertices = $numLpVertices
        numLpColours = $numLpColours
        numLpFaces = $numLpFaces
        numHpVertices = $numHpVertices
        numHpColours = $numHpColours
        numHpFaces = $numHpFaces
    })
}

function Get-ZeroSlackAtWadOffset([byte[]]$Image, [int64]$WadOffset, [int]$MaxBytes) {
    $count = 0
    for ($i = 0; $i -lt $MaxBytes; $i++) {
        $imageOffset = Convert-WadOffsetToImageOffset ($WadOffset + [int64]$i)
        if ($imageOffset -lt 0 -or $imageOffset -ge $Image.Length) { break }
        if ($Image[$imageOffset] -ne 0) { break }
        $count++
    }
    return $count
}

$resolvedImage = Resolve-WorkspacePath $ImagePath
$resolvedRam = Resolve-WorkspacePath $RamPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedSourceSearch = Resolve-WorkspacePath $SourceSearchPath
$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMd = Resolve-WorkspacePath $OutMarkdownPath

$ram = [System.IO.File]::ReadAllBytes($resolvedRam)
$image = [System.IO.File]::ReadAllBytes($resolvedImage)
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")
if ($edits.Count -lt 1) { throw "Terrain edits file has no edits." }
$edit = $edits[0]

$searchRoot = Get-Content -LiteralPath $resolvedSourceSearch -Raw | ConvertFrom-Json
$result = @(Get-ArrayField $searchRoot "results" | Where-Object { [string](Get-Field $_ "edit" "") -eq [string](Get-Field $edit "runtimeKey" "") }) | Select-Object -First 1
if ($null -eq $result) { throw "No source-search result found for terrain edit." }
$sectorHits = @(Get-ArrayField $result "fullSectorHits")
if ($sectorHits.Count -ne 1) { throw "Terrain edit needs exactly one source-sector hit, found $($sectorHits.Count)." }

$sectorIndex = [int](Get-Field $edit "sectorIndex" -1)
$sectorOffset = [int](Convert-HexTextToInt64 ([string](Get-Field $edit "sectorOffset" "")))
$sourceSectorWadOffset = Convert-HexTextToInt64 ([string](Get-Field $sectorHits[0] "wadOffset" ""))
$runtimeToWadDelta = $sourceSectorWadOffset - [int64]$sectorOffset

$sceneAddressValue = [uint32](Convert-HexTextToInt64 $SceneRuntimeAddress)
$sceneOffset = Convert-PsxAddressToRamOffset $sceneAddressValue $ram.Length
if ($sceneOffset -lt 0) { throw "SceneRuntimeAddress is outside main RAM." }
$sceneWadOffset = [int64]$sceneOffset + $runtimeToWadDelta
$numSectors = [int](Get-UInt32LE $ram ($sceneOffset + 8))
if ($sectorIndex -lt 0 -or $sectorIndex -ge $numSectors) { throw "Bad sector index $sectorIndex." }

$sectors = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt $numSectors; $i++) {
    $ptr = [uint32](Get-UInt32LE $ram ($sceneOffset + 12 + ($i * 4)))
    $offset = Convert-PsxAddressToRamOffset $ptr $ram.Length
    $header = Read-SceneSectorHeader $ram $offset
    if ($null -eq $header) {
        [void]$sectors.Add([pscustomobject]([ordered]@{
            index = $i
            runtimeOffset = ("0x{0:X}" -f $offset)
            sizeBytes = 0
            endRuntimeOffset = ("0x{0:X}" -f $offset)
            gapToNext = $null
            valid = $false
        }))
        continue
    }
    [void]$sectors.Add([pscustomobject]([ordered]@{
        index = $i
        runtimeOffset = ("0x{0:X}" -f $offset)
        wadOffset = ("0x{0:X}" -f ([int64]$offset + $runtimeToWadDelta))
        sizeBytes = [int]$header.sizeBytes
        endRuntimeOffset = ("0x{0:X}" -f ($offset + [int]$header.sizeBytes))
        endWadOffset = ("0x{0:X}" -f ([int64]$offset + $runtimeToWadDelta + [int]$header.sizeBytes))
        gapToNext = $null
        hpVertices = [int]$header.numHpVertices
        hpFaces = [int]$header.numHpFaces
        valid = $true
    }))
}

$sectorObjects = @($sectors.ToArray())
for ($i = 0; $i -lt ($sectorObjects.Count - 1); $i++) {
    if (-not [bool](Get-Field $sectorObjects[$i] "valid" $false) -or -not [bool](Get-Field $sectorObjects[$i + 1] "valid" $false)) {
        $sectorObjects[$i].gapToNext = $null
        continue
    }
    $end = Convert-HexTextToInt64 ([string](Get-Field $sectorObjects[$i] "endRuntimeOffset" ""))
    $next = Convert-HexTextToInt64 ([string](Get-Field $sectorObjects[$i + 1] "runtimeOffset" ""))
    $sectorObjects[$i].gapToNext = [int]($next - $end)
}

$target = $sectorObjects[$sectorIndex]
$sceneEndRuntime = 0
foreach ($sector in $sectorObjects) {
    if (-not [bool](Get-Field $sector "valid" $false)) { continue }
    $end = Convert-HexTextToInt64 ([string](Get-Field $sector "endRuntimeOffset" ""))
    if ($end -gt $sceneEndRuntime) { $sceneEndRuntime = $end }
}
$sceneEndWad = [int64]$sceneEndRuntime + $runtimeToWadDelta
$tailSlack = Get-ZeroSlackAtWadOffset $image $sceneEndWad 8192
$targetGap = [int](Get-Field $target "gapToNext" 0)
$canAppendInPlace = ($targetGap -ge $RequestedExtraBytes)
$canShiftTailInPlace = ($tailSlack -ge $RequestedExtraBytes)

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Feasibility report for adding Stone Hill scene-sector topology without borrowing existing faces."
    terrainEdit = [string](Get-Field $edit "runtimeKey" "")
    requestedExtraBytes = $RequestedExtraBytes
    sceneRuntimeOffset = ("0x{0:X}" -f $sceneOffset)
    sceneWadOffset = ("0x{0:X}" -f $sceneWadOffset)
    sourceSectorWadOffset = ("0x{0:X}" -f $sourceSectorWadOffset)
    runtimeToWadDelta = ("0x{0:X}" -f $runtimeToWadDelta)
    numSectors = $numSectors
    targetSector = $target
    sceneEndRuntimeOffset = ("0x{0:X}" -f $sceneEndRuntime)
    sceneEndWadOffset = ("0x{0:X}" -f $sceneEndWad)
    zeroSlackAfterSceneBytes = $tailSlack
    canAppendTargetInPlace = $canAppendInPlace
    canShiftSceneTailInPlace = $canShiftTailInPlace
    conclusion = if ($canAppendInPlace) {
        "Target sector has enough direct gap for the requested topology bytes."
    } elseif ($canShiftTailInPlace) {
        "Target sector has no direct gap, but the scene tail has enough zero slack for a contiguous tail shift."
    } else {
        "No direct target gap and no scene-tail slack. Clean topology addition needs whole-block relocation, WAD/file repacking, or a proven hidden donor region."
    }
    nearbySectors = @($sectorObjects | Where-Object {
        $idx = [int](Get-Field $_ "index" -1)
        $idx -ge ($sectorIndex - 6) -and $idx -le ($sectorIndex + 6)
    })
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Scene-Sector Repack Report")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($report.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("- Edit: $($report.terrainEdit)")
[void]$lines.Add("- Requested extra bytes: $RequestedExtraBytes")
[void]$lines.Add("- Scene runtime/WAD: $($report.sceneRuntimeOffset) / $($report.sceneWadOffset)")
[void]$lines.Add("- Target sector: $sectorIndex at $($target.runtimeOffset), size $($target.sizeBytes), gap to next $($target.gapToNext)")
[void]$lines.Add("- Scene end WAD: $($report.sceneEndWadOffset)")
[void]$lines.Add("- Zero slack after scene block: $tailSlack bytes")
[void]$lines.Add("- Append target in-place: $canAppendInPlace")
[void]$lines.Add("- Shift scene tail in-place: $canShiftTailInPlace")
[void]$lines.Add("")
[void]$lines.Add("Conclusion: $($report.conclusion)")
[void]$lines.Add("")
[void]$lines.Add("| Sector | Runtime | Size | Gap to next | HP verts | HP faces |")
[void]$lines.Add("|---:|---|---:|---:|---:|---:|")
foreach ($sector in @($report.nearbySectors)) {
    [void]$lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} |" -f `
        [int](Get-Field $sector "index" -1), `
        [string](Get-Field $sector "runtimeOffset" ""), `
        [int](Get-Field $sector "sizeBytes" 0), `
        [string](Get-Field $sector "gapToNext" ""), `
        [int](Get-Field $sector "hpVertices" 0), `
        [int](Get-Field $sector "hpFaces" 0)))
}
$lines | Set-Content -LiteralPath $resolvedOutMd -Encoding UTF8

Write-Host "Wrote scene-sector repack report to $resolvedOutJson"
Write-Host "Wrote scene-sector repack summary to $resolvedOutMd"
