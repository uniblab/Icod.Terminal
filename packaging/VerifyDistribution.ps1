param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force
$projectPath = Join-Path $repositoryRoot 'Icod.Terminal.csproj'
$validationRoot = Join-Path $repositoryRoot 'artifacts/distribution-validation'
$packageDirectory = Join-Path $validationRoot 'packages'

if (Test-Path -LiteralPath $validationRoot) {
    Remove-Item -LiteralPath $validationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot 'VerifyRuntime.ps1') `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyPublicApiBaseline.ps1') `
        -Configuration $Configuration `
        -OutputDirectory 'artifacts/distribution-validation/public-api'

    Write-Host ''
    Write-Host "=== Pack distribution candidate ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'pack', $projectPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '-o', $packageDirectory,
        '-p:ContinuousIntegrationBuild=true'
    )

    & (Join-Path $PSScriptRoot 'VerifyPackageDistribution.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    Write-Host ''
    Write-Host "Distribution verification completed successfully ($Configuration)."
} finally {
    Pop-Location
}
