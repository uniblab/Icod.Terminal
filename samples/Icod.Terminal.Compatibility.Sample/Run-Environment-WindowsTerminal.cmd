@echo off
setlocal EnableExtensions DisableDelayedExpansion

pushd "%~dp0\..\.."
if errorlevel 1 (
    echo Unable to locate the repository root.
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK is required but was not found on PATH.
    popd
    exit /b 1
)
where wt >nul 2>nul
if errorlevel 1 (
    echo Windows Terminal ^(wt.exe^) is required but was not found on PATH.
    popd
    exit /b 1
)

set "TERMINAL_VERSION="
for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -NonInteractive -Command "$packages = @(Get-AppxPackage -Name Microsoft.WindowsTerminal*); if (0 -lt $packages.Count) { $packages[0].Version.ToString() }"`) do if not defined TERMINAL_VERSION set "TERMINAL_VERSION=%%V"
for /f "usebackq delims=" %%V in (`git rev-parse HEAD`) do set "SOURCE_COMMIT=%%V"
for /f "tokens=2 delims=[]" %%V in ('ver') do set "OS_VERSION=%%V"
if not defined TERMINAL_VERSION (
    set "TERMINAL_VERSION=unknown"
    echo Warning: Windows Terminal version could not be determined; recording "unknown".
)
if not defined SOURCE_COMMIT (
    echo Unable to determine the source commit.
    popd
    exit /b 1
)

set "RUN_DIRECTORY=%TEMP%\Icod.Terminal-Environment-%RANDOM%%RANDOM%"
mkdir "%RUN_DIRECTORY%" >nul 2>nul
if errorlevel 1 (
    echo Unable to create "%RUN_DIRECTORY%".
    popd
    exit /b 1
)

set "PROJECT=samples\Icod.Terminal.Compatibility.Sample\Icod.Terminal.Compatibility.Sample.csproj"
set "MATRIX=%RUN_DIRECTORY%\matrix.md"
set "IDENTITY_EVIDENCE=%RUN_DIRECTORY%\windows-terminal-appearance-query.json"
set "DIMENSIONS_EVIDENCE=%RUN_DIRECTORY%\windows-terminal-appearance-reporting.json"
set "RESIZE_EVIDENCE=%RUN_DIRECTORY%\windows-terminal-in-band-resize.json"

dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --help
if errorlevel 1 goto :failure
dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --list-scenarios
if errorlevel 1 goto :failure
dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --render-matrix docs\compatibility\evidence\1.27.0 "%MATRIX%" --release-version 1.27.0
if errorlevel 1 goto :failure

dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --run query.appearance --terminal windows-terminal --terminal-version "%TERMINAL_VERSION%" --os Windows --os-version "%OS_VERSION%" --source-commit "%SOURCE_COMMIT%" --output "%IDENTITY_EVIDENCE%"
if errorlevel 1 goto :failure
dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --run environment.appearance-reporting --terminal windows-terminal --terminal-version "%TERMINAL_VERSION%" --os Windows --os-version "%OS_VERSION%" --source-commit "%SOURCE_COMMIT%" --output "%DIMENSIONS_EVIDENCE%"
if errorlevel 1 goto :failure
dotnet run --project "%PROJECT%" -c Staging -f net10.0 -- --run environment.in-band-resize --terminal windows-terminal --terminal-version "%TERMINAL_VERSION%" --os Windows --os-version "%OS_VERSION%" --source-commit "%SOURCE_COMMIT%" --output "%RESIZE_EVIDENCE%"
if errorlevel 1 goto :failure

echo.
echo Safe Windows Terminal compatibility evidence was written to:
echo   "%IDENTITY_EVIDENCE%"
echo   "%DIMENSIONS_EVIDENCE%"
echo   "%RESIZE_EVIDENCE%"
echo Review the reports before accepting them into the versioned evidence directory.
popd
exit /b 0

:failure
echo Compatibility launcher failed. Preserved files are under "%RUN_DIRECTORY%".
popd
exit /b 1
