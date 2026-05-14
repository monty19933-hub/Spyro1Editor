@echo off
setlocal
set "DUCK=%LOCALAPPDATA%\Programs\DuckStation\duckstation-qt-x64-ReleaseLTCG.exe"
set "CUE=%~dp0Spyro the Dragon (USA)-loaderpatchtest.cue"

if not exist "%DUCK%" (
  echo DuckStation not found: "%DUCK%"
  pause
  exit /b 1
)

if not exist "%CUE%" (
  echo Loader patch CUE not found: "%CUE%"
  echo Create Loader BIN from the native editor first.
  pause
  exit /b 1
)

start "" "%DUCK%" -fastboot -- "%CUE%"
