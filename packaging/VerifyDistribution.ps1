param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$solutionPath = Get-RepositorySolution -RepositoryRoot $repositoryRoot
$validationRoot = Join-Path $repositoryRoot 'artifacts/distribution-validation'
$packageDirectory = Join-Path $validationRoot 'packages'

if (Test-Path -LiteralPath $validationRoot) {
    Remove-Item -LiteralPath $validationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    Invoke-DotNet -Arguments @('restore', $solutionPath)
    Invoke-DotNet -Arguments @(
        'build', $solutionPath,
        '-c', $Configuration,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true'
    )
    Invoke-DotNet -Arguments @(
        'test', $solutionPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '--logger', 'trx'
    )

    & (Join-Path $PSScriptRoot 'VerifyDCursesSynchronizedOutput.ps1') `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyDCursesProgress.ps1') `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyDCursesPointerShape.ps1') `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyDCursesSemanticPrompt.ps1') `
        -Configuration $Configuration

    Invoke-DotNet -Arguments @(
        'pack', $solutionPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '-o', $packageDirectory,
        '-p:ContinuousIntegrationBuild=true'
    )

    & (Join-Path $PSScriptRoot 'VerifyPackageArtifact.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyCursorStylePackage.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifySynchronizedOutputPackage.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyProgressPackage.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifyPointerShapePackage.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    & (Join-Path $PSScriptRoot 'VerifySemanticPromptPackage.ps1') `
        -ArtifactDirectory $packageDirectory `
        -Configuration $Configuration

    Write-Host ''
    Write-Host "Distribution verification completed successfully ($Configuration)."
    Write-Host "  Solution: $solutionPath"
} finally {
    Pop-Location
}
