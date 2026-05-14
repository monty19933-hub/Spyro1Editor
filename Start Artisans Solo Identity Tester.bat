@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Start-ArtisansIdentitySoloTester.ps1"
pause
