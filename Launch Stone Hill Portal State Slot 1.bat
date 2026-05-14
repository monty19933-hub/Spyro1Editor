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

echo This expects DuckStation save state slot 1 to be saved in Artisans,
echo standing at the Stone Hill portal, before Stone Hill has loaded.
echo Do not use an in-Stone-Hill savestate for source patch testing.
start "" "%DUCK%" -state 1 -- "%CUE%"
