param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$GitHubOutputPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$solutionPath = Get-RepositorySolution -RepositoryRoot $repositoryRoot -AllowMissing
$hasSolution = $null -ne $solutionPath

# Icod.Terminal is a library package. The executable projects in the solution are
# samples and are intentionally not release-archive products.
$hasExecutables = $false

$result = [ordered]@{
    RepositoryRoot = $repositoryRoot
    HasSolution = $hasSolution
    SolutionPath = if ($hasSolution) { $solutionPath } else { '' }
    HasExecutables = $hasExecutables
}

if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    "has_solution=$($hasSolution.ToString().ToLowerInvariant())" >> $GitHubOutputPath
    "solution_path=$($result.SolutionPath)" >> $GitHubOutputPath
    "has_executables=$($hasExecutables.ToString().ToLowerInvariant())" >> $GitHubOutputPath
}

[pscustomobject]$result
