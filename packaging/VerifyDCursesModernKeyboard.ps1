param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'tools/dcurses-modern-keyboard-acceptance/Icod.Terminal.DCursesModernKeyboardAcceptance.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "DCurses modern-keyboard acceptance project '$project' does not exist."
}

Write-Host ''
Write-Host '=== Restore DCurses modern-keyboard acceptance ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== DCurses modern-keyboard acceptance: $framework ==="
    Invoke-DotNet -Arguments @(
        'run',
        '--project', $project,
        '-c', $Configuration,
        '-f', $framework,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true'
    )
}

Write-Host ''
Write-Host "DCurses modern-keyboard acceptance completed successfully ($Configuration)."
