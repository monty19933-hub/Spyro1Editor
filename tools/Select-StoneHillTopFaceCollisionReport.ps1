param(
    [string]$CollisionReportPath = ".\stonehill-donorplatform2-collision-bridge-report.json",
    [string]$TerrainEditsPath = ".\stonehill-terrain-edits.json",
    [string]$OutJsonPath = ".\stonehill-donorplatform2-topface-collision-bridge-report.json",
    [string]$OutMarkdownPath = ".\stonehill-donorplatform2-topface-collision-bridge-report.md"
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

$resolvedReport = Resolve-WorkspacePath $CollisionReportPath
$resolvedEdits = Resolve-WorkspacePath $TerrainEditsPath
$resolvedOutJson = Resolve-WorkspacePath $OutJsonPath
$resolvedOutMd = Resolve-WorkspacePath $OutMarkdownPath

if (-not (Test-Path -LiteralPath $resolvedReport)) { throw "Missing collision report: $resolvedReport" }
if (-not (Test-Path -LiteralPath $resolvedEdits)) { throw "Missing terrain edits: $resolvedEdits" }

$report = Get-Content -LiteralPath $resolvedReport -Raw | ConvertFrom-Json
$editsRoot = Get-Content -LiteralPath $resolvedEdits -Raw | ConvertFrom-Json
$edits = @(Get-ArrayField $editsRoot "edits")
if ($edits.Count -ne 1) { throw "Top-face selector currently expects exactly one terrain edit." }

$topVertexSet = @{}
foreach ($idx in @(Get-ArrayField $edits[0] "vertexIndexes")) {
    $topVertexSet[[int]$idx] = $true
}

$selectedMatches = New-Object System.Collections.ArrayList
foreach ($match in @(Get-ArrayField $report "matches")) {
    $allOnTopFace = $true
    foreach ($vertex in @(Get-ArrayField $match "matchedVertices")) {
        $vertexIndex = [int](Get-Field $vertex "vertexIndex" -1)
        if (-not $topVertexSet.ContainsKey($vertexIndex)) {
            $allOnTopFace = $false
            break
        }
    }
    if ($allOnTopFace) {
        [void]$selectedMatches.Add($match)
    }
}

if ($selectedMatches.Count -le 0) {
    throw "No collision matches were fully contained by the selected top-face vertices."
}

$report.matches = @($selectedMatches.ToArray())
$report.collisionMatchCount = [int]$selectedMatches.Count
$report.note = ([string](Get-Field $report "note" "") + " Filtered to collision triangles whose three matched HP vertices are all on the selected terrain face.")

$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $resolvedOutJson -Encoding UTF8

$lines = New-Object System.Collections.ArrayList
[void]$lines.Add("# Stone Hill Top-Face Collision Report")
[void]$lines.Add("")
[void]$lines.Add("Generated: $(Get-Date -Format s)")
[void]$lines.Add("")
[void]$lines.Add("- Source report: $resolvedReport")
[void]$lines.Add("- Top-face vertex indexes: $([string]::Join(', ', @($topVertexSet.Keys | Sort-Object)))")
[void]$lines.Add("- Selected collision triangles: $($selectedMatches.Count)")
[void]$lines.Add("")
[void]$lines.Add("| Edit | Collision tri | Moved points | Runtime offset | Old bytes |")
[void]$lines.Add("|---|---:|---:|---|---|")
foreach ($match in @($selectedMatches.ToArray())) {
    [void]$lines.Add(("| {0} | {1} | {2} | {3} | `{4}` |" -f `
        [string](Get-Field $match "runtimeKey" ""), `
        [int](Get-Field $match "triangleIndex" -1), `
        [int](Get-Field $match "touchedMovedVertexCount" 0), `
        [string](Get-Field $match "runtimeTriangleOffset" ""), `
        [string](Get-Field $match "oldBytes" "")))
}
[System.IO.File]::WriteAllLines($resolvedOutMd, $lines.ToArray(), [System.Text.Encoding]::UTF8)

Write-Host "Wrote top-face collision report to $resolvedOutJson"
Write-Host "Wrote top-face collision summary to $resolvedOutMd"
