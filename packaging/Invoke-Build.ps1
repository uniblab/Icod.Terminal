param(
    [ValidateSet('all', 'clean', 'restore', 'build', 'test', 'pack', 'validate')]
    [string]$Section = 'all',

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force
$solutionPath = Get-RepositorySolution -RepositoryRoot $repositoryRoot
$artifactDirectory = Join-Path $repositoryRoot 'artifacts'

function Invoke-Clean {
    Write-Host ''
    Write-Host "=== Clean ($Configuration) ==="
    Invoke-DotNet -Arguments @('clean', $solutionPath, '-c', $Configuration)
}

function Invoke-Restore {
    Write-Host ''
    Write-Host '=== Restore ==='
    Invoke-DotNet -Arguments @('restore', $solutionPath)
}

function Invoke-Build {
    Write-Host ''
    Write-Host "=== Build ($Configuration) ==="
    Invoke-DotNet -Arguments @('build', $solutionPath, '-c', $Configuration, '--no-restore')
}

function Invoke-Test {
    Write-Host ''
    Write-Host "=== Test ($Configuration) ==="
    Invoke-DotNet -Arguments @('test', $solutionPath, '-c', $Configuration, '--no-build', '--no-restore')
}

function Invoke-Pack {
    Write-Host ''
    Write-Host "=== Pack ($Configuration) ==="
    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
    Invoke-DotNet -Arguments @(
        'pack', $solutionPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '-o', $artifactDirectory
    )
}

function Invoke-Validate {
    Write-Host ''
    Write-Host "=== Validate ($Configuration) ==="
    & (Join-Path $PSScriptRoot 'VerifyPackageArtifact.ps1') `
        -ArtifactDirectory $artifactDirectory `
        -Configuration $Configuration
}

Push-Location $repositoryRoot
try {
    switch ($Section) {
        'all' {
            Invoke-Clean
            Invoke-Restore
            Invoke-Build
            Invoke-Test
            Invoke-Pack
            Invoke-Validate
        }
        'clean' { Invoke-Clean }
        'restore' { Invoke-Restore }
        'build' { Invoke-Build }
        'test' { Invoke-Test }
        'pack' { Invoke-Pack }
        'validate' { Invoke-Validate }
    }
} finally {
    Pop-Location
}
