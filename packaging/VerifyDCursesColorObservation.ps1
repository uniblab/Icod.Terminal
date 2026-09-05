param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'tools/dcurses-color-observation-acceptance/Icod.Terminal.DCursesColorObservationAcceptance.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "DCurses color-observation acceptance project '$project' does not exist."
}

Write-Host ''
Write-Host '=== Restore DCurses color-observation acceptance ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project,
    '--no-cache'
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== DCurses color-observation acceptance: $framework ==="
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
Write-Host "DCurses color-observation acceptance completed successfully ($Configuration)."
