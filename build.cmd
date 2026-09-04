@echo off
setlocal

set "SECTION=%~1"
if "%SECTION%"=="" set "SECTION=all"

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File packaging\Invoke-Build.ps1 -Section "%SECTION%" -Configuration Debug
exit /b %errorlevel%
