param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ExpectedVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

foreach ($shard in @('foundation', 'presentation', 'semantic', 'release')) {
    $parameters = @{
        ArtifactDirectory = $ArtifactDirectory
        Shard = $shard
        Configuration = $Configuration
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
        $parameters.ExpectedVersion = $ExpectedVersion
    }

    & (Join-Path $PSScriptRoot 'VerifyPackageContractShard.ps1') @parameters
}

Write-Host ''
Write-Host "All package contract shards completed successfully ($Configuration)."
