@echo off
setlocal
cd /d "%~dp0"
rem Experimental true-add test: bumps Stone Hill source moby count and appends a new purple gem as T195.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroMobyTableAppendTest.ps1" -LevelKey StoneHill -DonorIndex 79 -X 8544 -Y 9368 -Z 1292 -GemColor purple -OutBinPath ".\Spyro the Dragon (USA)-stonehill-trueadd-purple-gem-test.bin"
pause
