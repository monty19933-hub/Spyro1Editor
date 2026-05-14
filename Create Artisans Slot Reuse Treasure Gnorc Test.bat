@echo off
setlocal
cd /d "%~dp0"
rem Clones Treasure Gnorc T7 into sheep slot T110. This is the current safe "add by reusing a source slot" workflow.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Export-SpyroMobyRecordMutation.ps1" -LevelKey Artisans -Mode CloneIntoSlot -SourceIndex 7 -TargetIndex 110 -OutBinPath ".\Spyro the Dragon (USA)-artisans-sheep110-to-gnorc7-test.bin"
pause
