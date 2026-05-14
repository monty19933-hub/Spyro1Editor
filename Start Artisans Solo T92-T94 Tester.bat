@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& '.\tools\Start-ArtisansIdentitySoloTester.ps1' -RecordIndexes @(92,93,94)"
pause
