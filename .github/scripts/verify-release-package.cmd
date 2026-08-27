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

echo.
echo === Verify package structure, dependency closure, symbols, and Source Link (%CONFIGURATION%) ===
dotnet run --project tools\package-verifier\Icod.Terminal.PackageVerifier.csproj -c %CONFIGURATION% -f net10.0 -- "%ARTIFACT_DIR%"
if errorlevel 1 goto fail

set "SMOKE_ROOT=%TEMP%\Icod.Terminal-package-smoke-%RANDOM%-%RANDOM%"
if exist "%SMOKE_ROOT%" rmdir /s /q "%SMOKE_ROOT%"
mkdir "%SMOKE_ROOT%" || goto fail

copy /y tools\package-smoke\Icod.Terminal.PackageSmoke.csproj "%SMOKE_ROOT%\Icod.Terminal.PackageSmoke.csproj" >nul || goto fail
copy /y tools\package-smoke\Program.cs "%SMOKE_ROOT%\Program.cs" >nul || goto fail

set "OLD_NUGET_PACKAGES=%NUGET_PACKAGES%"
set "NUGET_PACKAGES=%SMOKE_ROOT%\packages"

echo.
echo === Fresh package consumer restore ===
dotnet restore "%SMOKE_ROOT%\Icod.Terminal.PackageSmoke.csproj" --no-cache --source "%ARTIFACT_DIR%" --source "https://api.nuget.org/v3/index.json" -p:IcodTerminalPackageVersion=%PACKAGE_VERSION%
if errorlevel 1 goto fail

echo.
echo === Fresh package consumer: net8.0 ===
dotnet run --project "%SMOKE_ROOT%\Icod.Terminal.PackageSmoke.csproj" -c %CONFIGURATION% -f net8.0 --no-restore -p:IcodTerminalPackageVersion=%PACKAGE_VERSION%
if errorlevel 1 goto fail

echo.
echo === Fresh package consumer: net10.0 ===
dotnet run --project "%SMOKE_ROOT%\Icod.Terminal.PackageSmoke.csproj" -c %CONFIGURATION% -f net10.0 --no-restore -p:IcodTerminalPackageVersion=%PACKAGE_VERSION%
if errorlevel 1 goto fail

goto cleanup

:usage
echo Usage: verify-release-package.cmd ^<artifact-directory^> ^<Staging^|Release^> 1>&2
exit /b 2

:fail
set "RESULT=%ERRORLEVEL%"
if "%RESULT%"=="0" set "RESULT=1"

:cleanup
if defined SMOKE_ROOT if exist "%SMOKE_ROOT%" rmdir /s /q "%SMOKE_ROOT%"
if defined OLD_NUGET_PACKAGES (
    set "NUGET_PACKAGES=%OLD_NUGET_PACKAGES%"
) else (
    set "NUGET_PACKAGES="
)
popd >nul
exit /b %RESULT%
