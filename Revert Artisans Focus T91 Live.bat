@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Move-DuckStationMoby.ps1" -MobyIndexes 91 -OriginalsPath ".\artisans-live-moby-originals.json" -ExpectedLevelId 10 -CatalogPath ".\artisans-moby-catalog.json" -Revert
pause
