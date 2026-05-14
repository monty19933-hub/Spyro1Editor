@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroLevelMobyPatchTest.ps1" -LevelKey All -OutPath ".\Spyro the Dragon (USA)-loaderpatchtest.bin"
pause
