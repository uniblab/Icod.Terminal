param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'samples/Icod.Terminal.Notification.Sample/Icod.Terminal.Notification.Sample.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Notification sample project '$project' does not exist."
}

Write-Host ''
Write-Host '=== Restore notification sample ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project,
    '--no-cache'
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== Notification sample build: $framework ==="
    Invoke-DotNet -Arguments @(
        'build',
        $project,
        '-c', $Configuration,
        '-f', $framework,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true'
    )
}

Write-Host ''
Write-Host "Notification sample verification completed successfully ($Configuration)."
