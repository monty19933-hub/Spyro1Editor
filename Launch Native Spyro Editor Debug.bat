@echo off
setlocal
cd /d "%~dp0"
if exist native-startup-error.txt del native-startup-error.txt
echo Launching Native Spyro Editor...
"%~dp0native\NativeSpyroEditor.exe"
if exist native-startup-error.txt (
    echo.
    echo Native editor startup error:
    type native-startup-error.txt
    echo.
    pause
)
