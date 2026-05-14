@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-StoneHillNativePatchTest.ps1" -OutPath ".\Spyro the Dragon (USA)-nativepatchtest-broad.bin" -PlanPath ".\Spyro the Dragon (USA)-nativepatchtest-broad.bin.patchplan.json"
echo.
pause
