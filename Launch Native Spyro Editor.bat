@echo off
setlocal
cd /d "%~dp0"
if exist "%~dp0native\NativeSpyroEditor-next.exe" (
    "%~dp0native\NativeSpyroEditor-next.exe"
) else (
    "%~dp0native\NativeSpyroEditor.exe"
)
