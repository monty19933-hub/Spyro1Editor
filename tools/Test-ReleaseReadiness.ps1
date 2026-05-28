param(
    [switch]$FailOnLocalUnsafe
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$WorkspaceRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $WorkspaceRoot

$unsafeExtensions = @(
    ".bin", ".cue", ".iso", ".img", ".chd", ".ecm", ".m3u", ".ccd", ".sub", ".toc",
    ".sav", ".srm", ".mcr", ".mcd", ".dmp", ".dump", ".state"
)

$unsafeRegexes = @(
    "(^|[\\/])Spyro the Dragon .*\\.(bin|cue|iso|img|chd|ecm|m3u|ccd|sub|toc)$",
    "(^|[\\/]).*-(before-clean|mainram|vram).*\\.bin$",
    "(^|[\\/]).*-live-capture-.*\\.bin$",
    "(^|[\\/]).*-runtime-scene.*\\.json$",
    "(^|[\\/]).*-runtime-moby.*\\.json$",
    "(^|[\\/]).*-custom-terrain-textures\\.json$",
    "(^|[\\/])_local[\\/]custom-textures[\\/]",
    "(^|[\\/]).*terrain-source-links.*\\.json$",
    "(^|[\\/]).*source-window-layouts.*\\.json$",
    "(^|[\\/]).*texture-vram.*\\.(json|png)$",
    "(^|[\\/]).*wad-subfile.*\\.(bin|png)$",
    "(^|[\\/])actor-texture-atlases[\\/]",
    "(^|[\\/])color-research-snapshots[\\/]",
    "(^|[\\/])unsafe-tests[\\/]"
)

function Convert-ToRepoPath([string]$Path) {
    $full = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $WorkspaceRoot $Path))
    }
    $root = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $full.Substring($root.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        return $relative.Replace("\", "/")
    }
    return $Path.Replace("\", "/")
}

function Get-UnsafeReleaseReason([string]$Path) {
    $repoPath = Convert-ToRepoPath $Path
    $extension = [System.IO.Path]::GetExtension($repoPath).ToLowerInvariant()
    if ($unsafeExtensions -contains $extension) {
        return "unsafe release extension $extension"
    }
    foreach ($regex in $unsafeRegexes) {
        if ($repoPath -match $regex) {
            return "matches generated/local game-data pattern"
        }
    }
    return $null
}

function Get-GitTrackedFiles {
    $output = & git ls-files
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed. Run this script from the editor git checkout."
    }
    return @($output)
}

$trackedUnsafe = New-Object System.Collections.ArrayList
foreach ($path in Get-GitTrackedFiles) {
    $reason = Get-UnsafeReleaseReason $path
    if ($null -ne $reason) {
        [void]$trackedUnsafe.Add([pscustomobject]@{
            path = Convert-ToRepoPath $path
            reason = $reason
        })
    }
}

if ($trackedUnsafe.Count -gt 0) {
    Write-Host "Release readiness FAILED: tracked files include unsafe game data or generated local artifacts." -ForegroundColor Red
    $trackedUnsafe | Format-Table -AutoSize
    exit 1
}

$localUnsafe = New-Object System.Collections.ArrayList
$localTotalBytes = [int64]0
foreach ($item in Get-ChildItem -LiteralPath $WorkspaceRoot -Recurse -File -Force) {
    $repoPath = Convert-ToRepoPath $item.FullName
    if ($repoPath -like ".git/*" -or $repoPath -like "dist/*" -or $repoPath -like "release/*") { continue }
    $reason = Get-UnsafeReleaseReason $item.FullName
    if ($null -eq $reason) { continue }
    $localTotalBytes += [int64]$item.Length
    [void]$localUnsafe.Add([pscustomobject]@{
        path = $repoPath
        sizeMB = [Math]::Round(([double]$item.Length / 1MB), 2)
        reason = $reason
    })
}

Write-Host "Release readiness passed: no tracked files matched unsafe release patterns." -ForegroundColor Green

if ($localUnsafe.Count -gt 0) {
    Write-Host ""
    Write-Host ("Local generated/game-data artifacts found: {0} files, {1:N1} MB total." -f $localUnsafe.Count, ([double]$localTotalBytes / 1MB)) -ForegroundColor Yellow
    Write-Host "These can stay on this machine for testing, but should remain ignored and should not be packaged."
    $localUnsafe | Select-Object -First 25 | Format-Table -AutoSize
    if ($localUnsafe.Count -gt 25) {
        Write-Host ("... plus {0} more local artifact(s)." -f ($localUnsafe.Count - 25))
    }
    if ($FailOnLocalUnsafe) {
        exit 2
    }
}

exit 0
