@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Materials.ps1" -Food diamond
if errorlevel 1 echo Installation failed.
pause
