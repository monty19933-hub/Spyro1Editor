@echo off
setlocal
cd /d "%~dp0"
rem Experimental: changes Flame/Charge chest T50 reward candidate byte +0x53 to purple.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroMobyRecordMutation.ps1" -LevelKey Artisans -Mode RewardColor -TargetIndex 50 -GemColor purple -OutBinPath ".\Spyro the Dragon (USA)-artisans-chest50-purple-reward-test.bin"
pause
