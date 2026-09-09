param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force
$solutionPath = Get-RepositorySolution -RepositoryRoot $repositoryRoot

Push-Location $repositoryRoot
try {
    Write-Host ''
    Write-Host "=== Restore runtime graph ($Configuration) ==="
    Invoke-DotNet -Arguments @('restore', $solutionPath)

    Write-Host ''
    Write-Host "=== Build runtime graph ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'build', $solutionPath,
        '-c', $Configuration,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true'
    )

    Write-Host ''
    Write-Host "=== Test runtime graph ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'test', $solutionPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '--logger', 'trx'
    )

    foreach ($script in @(
        'VerifyNotificationSample.ps1',
        'VerifyITerm2ShellIntegrationSample.ps1',
        'VerifyDCursesSynchronizedOutput.ps1',
        'VerifyDCursesProgress.ps1',
        'VerifyDCursesPointerShape.ps1',
        'VerifyDCursesSemanticPrompt.ps1',
        'VerifyDCursesColorObservation.ps1',
        'VerifyDCursesModernKeyboard.ps1',
        'VerifyDCursesHardeningSoak.ps1'
    )) {
        & (Join-Path $PSScriptRoot $script) -Configuration $Configuration
    }

    Write-Host ''
    Write-Host "Runtime validation completed successfully ($Configuration)."
    Write-Host "  Solution: $solutionPath"
} finally {
    Pop-Location
}
