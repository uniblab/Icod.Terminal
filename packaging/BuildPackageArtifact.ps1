param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ExpectedVersion = '',

    [string]$ApiOutputDirectory = 'artifacts/public-api'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force
$projectPath = Join-Path $repositoryRoot 'Icod.Terminal.csproj'

if (-not [System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Get-MSBuildProperty `
        -ProjectPath $projectPath `
        -Name 'PackageVersion' `
        -Configuration $Configuration
}
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    throw 'Unable to determine the expected Icod.Terminal package version.'
}

if (Test-Path -LiteralPath $ArtifactDirectory) {
    Remove-Item -LiteralPath $ArtifactDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    Write-Host ''
    Write-Host "=== Restore package project ($Configuration) ==="
    Invoke-DotNet -Arguments @('restore', $projectPath)

    Write-Host ''
    Write-Host "=== Build package project ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'build', $projectPath,
        '-c', $Configuration,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true'
    )

    & (Join-Path $PSScriptRoot 'VerifyPublicApiBaseline.ps1') `
        -Configuration $Configuration `
        -OutputDirectory $ApiOutputDirectory

    Write-Host ''
    Write-Host "=== Pack Icod.Terminal $ExpectedVersion ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'pack', $projectPath,
        '-c', $Configuration,
        '--no-build',
        '--no-restore',
        '-o', $ArtifactDirectory,
        '-p:ContinuousIntegrationBuild=true'
    )

    $packagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.nupkg"
    $symbolPackagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.snupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Expected package '$packagePath' was not produced."
    }
    if (-not (Test-Path -LiteralPath $symbolPackagePath -PathType Leaf)) {
        throw "Expected symbol package '$symbolPackagePath' was not produced."
    }

    Write-Host ''
    Write-Host "Package candidate built successfully: Icod.Terminal $ExpectedVersion ($Configuration)."
} finally {
    Pop-Location
}
