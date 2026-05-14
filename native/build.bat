@echo off
setlocal
cd /d "%~dp0\.."
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "MAIN=.\native\NativeSpyroEditor.exe"
set "NEXT=.\native\NativeSpyroEditor-next.exe"
set "BUILD=.\native\NativeSpyroEditor-build.exe"

"%CSC%" /nologo /target:winexe /optimize+ /out:"%BUILD%" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ".\native\NativeSpyroEditor.cs"
if errorlevel 1 exit /b %errorlevel%

copy /Y "%BUILD%" "%NEXT%" >nul
if errorlevel 1 (
    echo Built "%BUILD%", but "%NEXT%" is locked by a running editor.
    echo Close the running editor, then run native\build.bat again.
    exit /b 1
)

copy /Y "%BUILD%" "%MAIN%" >nul 2>nul
if errorlevel 1 (
    echo Built "%NEXT%"; "%MAIN%" is locked by a running editor.
) else (
    echo Built "%MAIN%" and "%NEXT%".
)
del "%BUILD%" >nul 2>nul
