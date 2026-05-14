@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroLevelMobyPatchTest.ps1" -LevelKey Artisans -NativeEditsPath ".\artisans-native-edits.json" -OutPath ".\Spyro the Dragon (USA)-artisans-loaderpatchtest.bin"
pause
