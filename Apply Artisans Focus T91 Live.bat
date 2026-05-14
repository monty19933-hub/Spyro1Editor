@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Move-DuckStationMoby.ps1" -MobyIndexes 91 -FromNativeEdit -NativeEditsPath ".\artisans-focused-t91-edits.json" -OriginalsPath ".\artisans-live-moby-originals.json" -ExpectedLevelId 10 -CatalogPath ".\artisans-moby-catalog.json" -Apply -HoldSeconds 5
pause
