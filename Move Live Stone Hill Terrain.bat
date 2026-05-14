@echo off
cd /d "%~dp0"
echo This writes Stone Hill terrain face height edits to DuckStation's live PS1 RAM only when you include -Apply.
echo Examples:
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationTerrainFace.ps1 -PlanOnly
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationTerrainFace.ps1 -RuntimeKey "1:0:hp" -Apply -HoldSeconds 3
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationTerrainFace.ps1 -RuntimeKey "1:0:hp" -Revert
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Move-DuckStationTerrainFace.ps1" %*
