@echo off
setlocal
rem ============================================================
rem  Char-by Inputer (standalone) build script
rem  Uses the csc.exe bundled with .NET Framework 4.x on Windows.
rem  Usage: double-click, or run "build.bat nopause" from CLI.
rem ============================================================

set CSC64=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set CSC32=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if exist "%CSC64%" (set CSC=%CSC64%) else (set CSC=%CSC32%)
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. This build requires .NET Framework 4.x.
  exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:CharByInputer.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  Program.cs TypeSender.cs MainForm.cs

if errorlevel 1 goto :fail

echo.
echo [OK] Built CharByInputer.exe
if /i not "%1"=="nopause" pause
exit /b 0

:fail
echo.
echo [ERROR] Build failed. See messages above.
if /i not "%1"=="nopause" pause
exit /b 1