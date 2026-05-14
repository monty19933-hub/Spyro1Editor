param(
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$OverlayPath = ".\stonehill-runtime-scene-editor-overlay.json",
    [string]$OutPath = ".\stonehill-terrain-edits-sculpt.json",
    [double]$InnerRadius = 160.0,
    [double]$OuterRadius = 640.0,
    [double]$MinimumWeight = 0.03
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot

function Resolve-WorkspacePath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $WorkspaceRoot $Path)
}

function Get-ArrayField($Object, [string]$Name) {
    if ($null -eq $Object) { return @() }
    if ($Object -is [System.Collections.IDictionary]) {
        if (-not $Object.Contains($Name) -or $null -eq $Object[$Name]) { return @() }
        return @($Object[$Name])
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return @() }
    return @($property.Value)
}

function Get-Field($Object, [string]$Name, $Fallback = $null) {
    if ($null -eq $Object) { return $Fallback }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
        return $Fallback
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Fallback
}

function Get-PolygonKey($Polygon) {
    return ("{0}:{1}:{2}" -f `
        [int](Get-Field $Polygon "sectorIndex" -1), `
        [int](Get-Field $Polygon "faceIndex" -1), `
        [string](Get-Field $Polygon "detail" ""))
}

function Convert-ToPlainArray($Values) {
    $items = @($Values)
    $result = New-Object object[] $items.Count
    for ($i = 0; $i -lt $items.Count; $i++) { $result[$i] = $items[$i] }
    return $result
}

function Read-AllPolygons($OverlayRoot) {
    $polygons = New-Object System.Collections.ArrayList
    foreach ($candidate in @(Get-ArrayField $OverlayRoot "candidates")) {
        foreach ($polygon in @(Get-ArrayField $candidate "polygons")) {
            [void]$polygons.Add($polygon)
        }
    }
    return @($polygons)
}

$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedOverlay = Resolve-WorkspacePath $OverlayPath
$resolvedOut = Resolve-WorkspacePath $OutPath

if (-not (Test-Path -LiteralPath $resolvedEdits)) { throw "Missing terrain edits file: $resolvedEdits" }
if (-not (Test-Path -LiteralPath $resolvedOverlay)) { throw "Missing overlay file: $resolvedOverlay" }
if ($OuterRadius -le 0.0) { throw "OuterRadius must be positive." }
if ($InnerRadius -lt 0.0) { throw "InnerRadius cannot be negative." }
if ($OuterRadius -lt $InnerRadius) { throw "OuterRadius must be greater than or equal to InnerRadius." }

$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$overlayRoot = Get-Content -LiteralPath $resolvedOverlay -Raw | ConvertFrom-Json
$allPolygons = @(Read-AllPolygons $overlayRoot)

$polygonByKey = @{}
foreach ($polygon in $allPolygons) {
    $key = Get-PolygonKey $polygon
    if (-not $polygonByKey.ContainsKey($key)) { $polygonByKey[$key] = $polygon }
}

$sculptedEdits = New-Object System.Collections.ArrayList
$reports = New-Object System.Collections.ArrayList

foreach ($edit in @(Get-ArrayField $editsRoot "edits")) {
    $key = [string](Get-Field $edit "runtimeKey" "")
    if ([string]::IsNullOrWhiteSpace($key)) {
        $key = ("{0}:{1}:{2}" -f `
            [int](Get-Field $edit "sectorIndex" -1), `
            [int](Get-Field $edit "faceIndex" -1), `
            [string](Get-Field $edit "detail" ""))
    }

    if (-not $polygonByKey.ContainsKey($key)) {
        throw "Could not find terrain polygon $key in overlay $resolvedOverlay."
    }

    $selected = $polygonByKey[$key]
    $selectedPoints = @(Get-ArrayField $selected "points")
    if ($selectedPoints.Count -lt 3) { throw "Terrain polygon $key has too few points for sculpt expansion." }

    $centerX = 0.0
    $centerY = 0.0
    foreach ($point in $selectedPoints) {
        $centerX += [double](Get-Field $point "x" 0.0)
        $centerY += [double](Get-Field $point "y" 0.0)
    }
    $centerX /= [double]$selectedPoints.Count
    $centerY /= [double]$selectedPoints.Count

    $sectorIndex = [int](Get-Field $selected "sectorIndex" -1)
    $detail = [string](Get-Field $selected "detail" "hp")
    $deltaZ = [double](Get-Field $edit "deltaZ" 0.0)

    $selectedVertexIndexes = @{}
    foreach ($idx in @(Get-ArrayField $edit "vertexIndexes")) {
        $selectedVertexIndexes[[int]$idx] = $true
    }

    $verticesByIndex = @{}
    foreach ($polygon in $allPolygons) {
        if ([int](Get-Field $polygon "sectorIndex" -1) -ne $sectorIndex) { continue }
        if ([string](Get-Field $polygon "detail" "") -ne $detail) { continue }
        $points = @(Get-ArrayField $polygon "points")
        $vertexIndexes = @(Get-ArrayField $polygon "vertexIndexes")
        $count = [Math]::Min($points.Count, $vertexIndexes.Count)
        for ($i = 0; $i -lt $count; $i++) {
            $vertexIndex = [int]$vertexIndexes[$i]
            if ($verticesByIndex.ContainsKey($vertexIndex)) { continue }
            $point = $points[$i]
            $x = [double](Get-Field $point "x" 0.0)
            $y = [double](Get-Field $point "y" 0.0)
            $z = [double](Get-Field $point "z" 0.0)
            $dx = $x - $centerX
            $dy = $y - $centerY
            $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))

            $weight = 0.0
            if ($selectedVertexIndexes.ContainsKey($vertexIndex)) {
                $weight = 1.0
            } elseif ($distance -le $InnerRadius) {
                $weight = 1.0
            } elseif ($distance -lt $OuterRadius) {
                $weight = 1.0 - (($distance - $InnerRadius) / [Math]::Max(1.0, ($OuterRadius - $InnerRadius)))
            }

            if ($weight -ge $MinimumWeight) {
                $verticesByIndex[$vertexIndex] = [ordered]@{
                    vertexIndex = $vertexIndex
                    x = $x
                    y = $y
                    originalZ = $z
                    weight = $weight
                    editedZ = $z + ($deltaZ * $weight)
                    distance = $distance
                }
            }
        }
    }

    foreach ($idx in $selectedVertexIndexes.Keys) {
        if ($verticesByIndex.ContainsKey($idx)) { continue }
        throw "Selected vertex $idx for $key was not found in overlay sector $sectorIndex."
    }

    $orderedVertices = @($verticesByIndex.Values | Sort-Object distance, vertexIndex)

    $sculpted = [ordered]@{
        runtimeKey = $key
        sectorIndex = $sectorIndex
        faceIndex = [int](Get-Field $selected "faceIndex" -1)
        detail = $detail
        sectorOffset = [string](Get-Field $selected "sectorOffset" (Get-Field $edit "sectorOffset" ""))
        faceOffset = [string](Get-Field $selected "faceOffset" (Get-Field $edit "faceOffset" ""))
        textureId = [int](Get-Field $selected "textureId" (Get-Field $edit "textureId" -1))
        word3 = [string](Get-Field $selected "word3" (Get-Field $edit "word3" ""))
        word4 = [string](Get-Field $selected "word4" (Get-Field $edit "word4" ""))
        deltaZ = $deltaZ
        sculptMode = "radial-falloff"
        sculptInnerRadius = $InnerRadius
        sculptOuterRadius = $OuterRadius
        sculptMinimumWeight = $MinimumWeight
        sculptCenter = [ordered]@{ x = $centerX; y = $centerY }
        vertexIndexes = Convert-ToPlainArray @($orderedVertices | ForEach-Object { [int]$_.vertexIndex })
        originalZ = Convert-ToPlainArray @($orderedVertices | ForEach-Object { [double]$_.originalZ })
        editedZ = Convert-ToPlainArray @($orderedVertices | ForEach-Object { [double]$_.editedZ })
        sculptWeights = Convert-ToPlainArray @($orderedVertices | ForEach-Object { [double]$_.weight })
    }

    [void]$sculptedEdits.Add($sculpted)
    [void]$reports.Add([ordered]@{
        runtimeKey = $key
        sectorIndex = $sectorIndex
        detail = $detail
        deltaZ = $deltaZ
        centerX = $centerX
        centerY = $centerY
        originalVertexCount = @(Get-ArrayField $edit "vertexIndexes").Count
        sculptVertexCount = $orderedVertices.Count
        innerRadius = $InnerRadius
        outerRadius = $OuterRadius
        minWeight = $MinimumWeight
        maxDistance = if ($orderedVertices.Count -gt 0) { [double](@($orderedVertices | Sort-Object distance -Descending | Select-Object -First 1).distance) } else { 0.0 }
    })
}

$outRoot = [ordered]@{
    generatedAt = (Get-Date).ToString("s")
    editor = "New-StoneHillTerrainSculptEdit"
    levelName = "Stone Hill"
    note = "Brush-expanded terrain edits. The selected face keeps full height while surrounding same-sector vertices use radial falloff to avoid torn holes."
    sourceTerrainEditsPath = (Resolve-Path -LiteralPath $resolvedEdits).Path
    overlayPath = (Resolve-Path -LiteralPath $resolvedOverlay).Path
    editCount = $sculptedEdits.Count
    edits = Convert-ToPlainArray $sculptedEdits
    sculptReport = Convert-ToPlainArray $reports
}

$outDir = Split-Path -Parent $resolvedOut
if (-not [string]::IsNullOrWhiteSpace($outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$json = $outRoot | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($resolvedOut, $json, [System.Text.Encoding]::UTF8)

Write-Host "Wrote sculpt-expanded terrain edits: $resolvedOut"
foreach ($report in $reports) {
    Write-Host ("- {0}: {1} -> {2} vertices, radius {3:N0}/{4:N0}" -f `
        $report.runtimeKey, $report.originalVertexCount, $report.sculptVertexCount, $report.innerRadius, $report.outerRadius)
}
