param(
    [string]$GeometryPath = ".\stonehill-runtime-scene-editor-overlay.json",
    [string]$MaterialOverridesPath = ".\stonehill-terrain-material-overrides.json",
    [string]$OutJsonPath = ".\stonehill-terrain-bridge-report.json",
    [string]$OutMdPath = ".\stonehill-terrain-bridge-report.md"
)

Set-StrictMode -Version 2.0

function Get-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Get-Number($Object, [string]$Name, [double]$Default = 0) {
    $value = Get-Field $Object $Name $Default
    if ($null -eq $value) { return $Default }
    return [double]$value
}

function Get-MaterialOverrides([string]$Path) {
    $result = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $result }
    $root = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    foreach ($entry in @($root.overrides)) {
        $textureId = [int](Get-Field $entry "textureId" -1)
        $surface = [string](Get-Field $entry "surface" "")
        if ($textureId -ge 0 -and -not [string]::IsNullOrWhiteSpace($surface)) {
            $result[$textureId] = $surface
        }
    }
    return $result
}

if (-not (Test-Path -LiteralPath $GeometryPath)) {
    throw "Missing geometry overlay: $GeometryPath"
}

$geometry = Get-Content -Raw -LiteralPath $GeometryPath | ConvertFrom-Json
$candidate = @($geometry.candidates)[0]
if ($null -eq $candidate) { throw "Geometry overlay has no candidates." }

$overrides = Get-MaterialOverrides $MaterialOverridesPath
$polygons = @($candidate.polygons)
$groups = @()

foreach ($group in @($polygons | Group-Object textureId | Sort-Object { [int]$_.Name })) {
    $items = @($group.Group)
    if ($items.Count -eq 0) { continue }
    $textureId = [int]$group.Name
    $zValues = @($items | ForEach-Object { Get-Number $_ "avgZ" 0 })
    $sample = $items | Select-Object -First 6
    $manualSurface = if ($overrides.ContainsKey($textureId)) { [string]$overrides[$textureId] } else { "" }
    $groups += [pscustomobject][ordered]@{
        textureId = $textureId
        faceCount = $items.Count
        manualSurface = $manualSurface
        zMin = [int](($zValues | Measure-Object -Minimum).Minimum)
        zAverage = [int](($zValues | Measure-Object -Average).Average)
        zMax = [int](($zValues | Measure-Object -Maximum).Maximum)
        sampleRuntimeFaces = @($sample | ForEach-Object {
            [pscustomobject][ordered]@{
                sectorIndex = [int](Get-Field $_ "sectorIndex" -1)
                faceIndex = [int](Get-Field $_ "faceIndex" -1)
                sectorOffset = [string](Get-Field $_ "sectorOffset" "")
                faceOffset = [string](Get-Field $_ "faceOffset" "")
                word3 = [string](Get-Field $_ "word3" "")
                word4 = [string](Get-Field $_ "word4" "")
                avgZ = [int](Get-Number $_ "avgZ" 0)
            }
        })
    }
}

$report = [pscustomobject][ordered]@{
    generatedAt = (Get-Date).ToString("s")
    purpose = "Stone Hill terrain runtime-to-source bridge inventory. Runtime sector/face handles are the current keys for WAD source mapping."
    geometryPath = (Resolve-Path -LiteralPath $GeometryPath).Path
    materialOverridesPath = $(if (Test-Path -LiteralPath $MaterialOverridesPath) { (Resolve-Path -LiteralPath $MaterialOverridesPath).Path } else { "" })
    coverage = [pscustomobject][ordered]@{
        totalFaces = $polygons.Count
        texturedFaces = @($polygons | Where-Object { $null -ne $_.textureId }).Count
        textureIdGroups = $groups.Count
        manuallyLabelledTextureIds = @($groups | Where-Object { -not [string]::IsNullOrEmpty($_.manualSurface) }).Count
    }
    textureGroups = $groups
}

$resolvedJson = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutJsonPath)
$resolvedMd = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutMdPath)
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedJson -Encoding UTF8

$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add("# Stone Hill Terrain Bridge Report")
[void]$lines.Add("")
[void]$lines.Add("Generated: $($report.generatedAt)")
[void]$lines.Add("")
[void]$lines.Add("Faces: $($report.coverage.totalFaces); textured: $($report.coverage.texturedFaces); texture groups: $($report.coverage.textureIdGroups); manually labelled groups: $($report.coverage.manuallyLabelledTextureIds).")
[void]$lines.Add("")
[void]$lines.Add("| Texture ID | Faces | Manual Surface | Z Range | Sample Runtime Handles |")
[void]$lines.Add("|---:|---:|---|---|---|")
foreach ($group in $groups) {
    $samples = @($group.sampleRuntimeFaces | Select-Object -First 3 | ForEach-Object {
        "S$($_.sectorIndex):F$($_.faceIndex)@$($_.faceOffset)"
    }) -join "<br>"
    $surface = if ([string]::IsNullOrEmpty($group.manualSurface)) { "" } else { $group.manualSurface }
    [void]$lines.Add("| $($group.textureId) | $($group.faceCount) | $surface | $($group.zMin)..$($group.zMax) | $samples |")
}
$lines | Set-Content -LiteralPath $resolvedMd -Encoding UTF8

Write-Host "Wrote terrain bridge report:"
Write-Host "  $resolvedJson"
Write-Host "  $resolvedMd"
Write-Host ("Faces: {0}; texture groups: {1}" -f $report.coverage.totalFaces, $report.coverage.textureIdGroups)
