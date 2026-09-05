param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'tools/dcurses-pointer-shape-acceptance/Icod.Terminal.DCursesPointerShapeAcceptance.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "DCurses pointer-shape acceptance project '$project' does not exist."
}

Write-Host ''
Write-Host '=== Restore DCurses pointer-shape acceptance ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project,
    '--no-cache'
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== DCurses pointer-shape acceptance: $framework ==="
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
Write-Host "DCurses pointer-shape acceptance completed successfully ($Configuration)."
