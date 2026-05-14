@echo off
setlocal
cd /d "%~dp0"
echo This writes to DuckStation's live PS1 RAM only when you include -Apply.
echo Examples:
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationMoby.ps1 -ListMobys
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationMoby.ps1 -MobyIndex 109 -FromNativeEdit -Apply -HoldSeconds 3
echo   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Move-DuckStationMoby.ps1 -MobyIndex 109 -Revert
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Move-DuckStationMoby.ps1" %*
echo.
pause
