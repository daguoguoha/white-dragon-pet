@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Materials.ps1" -Food fish
if errorlevel 1 echo Installation failed.
pause
