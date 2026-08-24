@echo off
setlocal EnableExtensions

if "%~1"=="" goto usage
if "%~2"=="" goto usage
if not "%~3"=="" goto usage

set "ARTIFACT_DIR=%~1"
set "CONFIGURATION=%~2"

if /I "%CONFIGURATION%"=="Staging" (
    set "CONFIGURATION=Staging"
) else if /I "%CONFIGURATION%"=="Release" (
    set "CONFIGURATION=Release"
) else (
    goto usage
)

pushd "%~dp0\..\.." >nul || exit /b 1

set "RESULT=0"

if not exist "%ARTIFACT_DIR%" (
    echo Artifact directory does not exist: %ARTIFACT_DIR% 1>&2
    goto fail
)

for %%I in ("%ARTIFACT_DIR%") do set "ARTIFACT_DIR=%%~fI"

set "PACKAGE_VERSION="
for /f "delims=" %%V in ('dotnet msbuild Icod.Terminal.csproj -nologo -getProperty:PackageVersion') do set "PACKAGE_VERSION=%%V"

if not defined PACKAGE_VERSION (
    echo Unable to determine PackageVersion. 1>&2
    goto fail
)

set "PACKAGE_PATH=%ARTIFACT_DIR%\Icod.Terminal.%PACKAGE_VERSION%.nupkg"
set "SYMBOLS_PATH=%ARTIFACT_DIR%\Icod.Terminal.%PACKAGE_VERSION%.snupkg"

if not exist "%PACKAGE_PATH%" (
    echo Missing package: %PACKAGE_PATH% 1>&2
    goto fail
)

if not exist "%SYMBOLS_PATH%" (
    echo Missing symbols package: %SYMBOLS_PATH% 1>&2
    goto fail
)

dotnet run --project samples\Icod.Terminal.Sample\Icod.Terminal.Sample.csproj -c %CONFIGURATION% --no-build
if errorlevel 1 goto fail

goto cleanup

:usage
echo Usage: verify-release-package.cmd ^<artifact-directory^> ^<Staging^|Release^> 1>&2
exit /b 2

:fail
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" set "RESULT=1"

:cleanup
popd >nul
exit /b %RESULT%
