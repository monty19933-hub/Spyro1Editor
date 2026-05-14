@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-StoneHillNativePatchTest.ps1" -TopRankedOnly -OutPath ".\Spyro the Dragon (USA)-nativepatchtest-topranked.bin" -PlanPath ".\Spyro the Dragon (USA)-nativepatchtest-topranked.bin.patchplan.json"
echo.
pause
