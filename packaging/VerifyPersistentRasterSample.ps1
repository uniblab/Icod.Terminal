param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'samples/Icod.Terminal.PersistentRaster.Sample/Icod.Terminal.PersistentRaster.Sample.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Persistent raster sample project '$project' does not exist."
}

$programPath = Join-Path $repositoryRoot 'samples/Icod.Terminal.PersistentRaster.Sample/Program.cs'
$programText = [System.IO.File]::ReadAllText($programPath)
foreach ($forbidden in @('Kitty', 'Sixel', 'ImageId', 'ImageNumber', 'PlacementId')) {
    if ($programText.Contains($forbidden, [System.StringComparison]::Ordinal)) {
        throw "Persistent raster sample must remain backend-neutral; found forbidden text '$forbidden'."
    }
}

Write-Host ''
Write-Host '=== Restore persistent raster sample ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== Persistent raster sample build: $framework ==="
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
Write-Host "Persistent raster sample verification completed successfully ($Configuration)."
