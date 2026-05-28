param(
    [string]$OutDir = ".\dist\SpyroEditor-release",
    [switch]$SkipBuild,
    [switch]$Zip
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $WorkspaceRoot

$readinessScript = Join-Path $PSScriptRoot "Test-ReleaseReadiness.ps1"
if (-not (Test-Path -LiteralPath $readinessScript)) {
    throw "Missing release-readiness script: $readinessScript"
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $readinessScript
if ($LASTEXITCODE -ne 0) {
    throw "Release readiness check failed. Fix tracked unsafe files before packaging."
}

if (-not $SkipBuild) {
    & ".\native\build.bat"
    if ($LASTEXITCODE -ne 0) {
        throw "Native editor build failed."
    }
}

$resolvedOutDir = if ([System.IO.Path]::IsPathRooted($OutDir)) {
    [System.IO.Path]::GetFullPath($OutDir)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $WorkspaceRoot $OutDir))
}

$workspaceFull = [System.IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd("\", "/")
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $workspaceFull "dist")).TrimEnd("\", "/")
if (-not $resolvedOutDir.StartsWith($distRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "For safety, release output must stay under $distRoot."
}

if (Test-Path -LiteralPath $resolvedOutDir) {
    Remove-Item -LiteralPath $resolvedOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutDir | Out-Null

function Add-ReleaseFile([string]$RelativePath) {
    $source = Join-Path $WorkspaceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $source)) { return }
    $target = Join-Path $resolvedOutDir $RelativePath
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }
    Copy-Item -LiteralPath $source -Destination $target -Force
}

$tracked = @(& git ls-files)
if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed. Run this script from the editor git checkout."
}

foreach ($path in $tracked) {
    Add-ReleaseFile $path
}

$extraReleaseFiles = @(
    "RELEASE.md",
    "spyro-level-catalog.json",
    "spyro-object-templates.json",
    "tools\Test-ReleaseReadiness.ps1",
    "tools\New-SpyroEditorRelease.ps1"
)

foreach ($path in $extraReleaseFiles) {
    Add-ReleaseFile $path
}

$mainExe = Join-Path $WorkspaceRoot "native\NativeSpyroEditor.exe"
$nextExe = Join-Path $WorkspaceRoot "native\NativeSpyroEditor-next.exe"
$releaseExe = Join-Path $resolvedOutDir "native\NativeSpyroEditor.exe"
$releaseExeDir = Split-Path -Parent $releaseExe
if (-not (Test-Path -LiteralPath $releaseExeDir)) {
    New-Item -ItemType Directory -Path $releaseExeDir | Out-Null
}

if (Test-Path -LiteralPath $mainExe) {
    Copy-Item -LiteralPath $mainExe -Destination $releaseExe -Force
}
elseif (Test-Path -LiteralPath $nextExe) {
    Copy-Item -LiteralPath $nextExe -Destination $releaseExe -Force
}
else {
    throw "No native editor executable was found. Build first or rerun without -SkipBuild."
}

$notePath = Join-Path $resolvedOutDir "RELEASE-NOTES.txt"
@(
    "Spyro PS1 Level Editor release package",
    "",
    "This package contains editor code, scripts, documentation, and the compiled editor only.",
    "It does not include a ROM, BIOS, patched disc, emulator save state, RAM/VRAM dump, extracted WAD payload, texture dump, or generated level overlay.",
    "Users must provide their own legally obtained game image and generate local working files on their own machine."
) | Set-Content -LiteralPath $notePath -Encoding UTF8

if ($Zip) {
    $zipPath = $resolvedOutDir.TrimEnd("\", "/") + ".zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $resolvedOutDir "*") -DestinationPath $zipPath -Force
    Write-Host "Release zip written to $zipPath"
}

Write-Host "Release folder written to $resolvedOutDir"
