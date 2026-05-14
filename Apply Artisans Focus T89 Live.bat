@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Move-DuckStationMoby.ps1" -MobyIndexes 89 -FromNativeEdit -NativeEditsPath ".\artisans-focused-t89-edits.json" -OriginalsPath ".\artisans-live-moby-originals.json" -ExpectedLevelId 10 -CatalogPath ".\artisans-moby-catalog.json" -Apply -HoldSeconds 5
pause
