@echo off
setlocal

if "%~1"=="" goto all

if /I "%~1"=="clean"   goto run-clean
if /I "%~1"=="restore" goto run-restore
if /I "%~1"=="build"   goto run-build
if /I "%~1"=="test"    goto run-test
if /I "%~1"=="pack"    goto run-pack
if /I "%~1"=="validate"    goto run-pack

echo Invalid section: "%~1"
echo Usage: %~nx0 [clean^|restore^|build^|test^|pack]
exit /b 1


:all
call :clean   || exit /b 1
call :restore || exit /b 1
call :build   || exit /b 1
call :test    || exit /b 1
call :pack    || exit /b 1
call :validate    || exit /b 1
exit /b 0


:run-clean
call :clean
exit /b %errorlevel%


:run-restore
call :restore
exit /b %errorlevel%


:run-build
call :build
exit /b %errorlevel%

:run-test
call :test
exit /b %errorlevel%


:run-pack
call :pack
exit /b %errorlevel%


:run-validate
call :validate
exit /b %errorlevel%


:clean
echo.
echo === Clean ===
dotnet clean Icod.Terminal.sln -c Debug
exit /b %errorlevel%

:restore
echo.
echo === Restore ===
dotnet restore Icod.Terminal.sln
exit /b %errorlevel%

:build
echo.
echo === Build ===
dotnet build Icod.Terminal.sln -c Debug --no-restore
exit /b %errorlevel%

:test
echo.
echo === Test ===
dotnet test Icod.Terminal.sln -c Debug --no-build
exit /b %errorlevel%

:pack
echo.
echo === Pack ===
dotnet pack Icod.Terminal.sln -c Debug --include-source --include-symbols --no-build --oputput artifacts
exit /b %errorlevel%

:validate
echo.
echo === Validate ===
call .github\scripts\verify-release-package.cmd artifacts Debug
exit /b %errorlevel%
