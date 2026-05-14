@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\New-StoneHillTerrainAxisTestBatches.ps1" %*
echo.
echo Done. Check stonehill-terrain-axis-test-batches.md for the generated test list.
pause
