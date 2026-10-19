@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Read the error above.
  pause
  exit /b 1
)
echo.
call "%~dp0Start-Quota.cmd"
pause
