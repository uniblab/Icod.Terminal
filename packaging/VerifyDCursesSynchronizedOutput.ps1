param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'tools/dcurses-synchronized-output-acceptance/Icod.Terminal.DCursesSynchronizedOutputAcceptance.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "DCurses synchronized-output acceptance project '$project' does not exist."
}

Write-Host ''
Write-Host '=== Restore DCurses synchronized-output acceptance ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project,
    '--no-cache'
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== DCurses synchronized-output acceptance: $framework ==="
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
Write-Host "DCurses synchronized-output acceptance completed successfully ($Configuration)."
