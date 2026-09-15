param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterPlaceholder.Sample/Icod.Terminal.RasterPlaceholder.Sample.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Raster placeholder sample project '$project' does not exist."
}

$programPath = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterPlaceholder.Sample/Program.cs'
$programText = [System.IO.File]::ReadAllText($programPath)
foreach ($forbidden in @(
    'Kitty',
    'Sixel',
    'ImageId',
    'ImageNumber',
    'PlacementId',
    'TerminalProtocolBackend',
    '10EEEE',
    'Apc',
    'U=1',
    '\u001b_G'
)) {
    if ($programText.Contains($forbidden, [System.StringComparison]::Ordinal)) {
        throw "Raster placeholder sample must remain protocol-neutral; found forbidden text '$forbidden'."
    }
}

foreach ($required in @(
    'UnicodeRasterPlaceholders',
    'CreatePlaceholderAsync',
    'GetCell',
    'WriteRasterPlaceholderCellAsync',
    'WriteRasterPlaceholderCellsAsync',
    'StringCapability.CursorAddress',
    'CreateRelativePlacementFromPlaceholderAsync'
)) {
    if (-not $programText.Contains($required, [System.StringComparison]::Ordinal)) {
        throw "Raster placeholder sample is missing required semantic API usage '$required'."
    }
}

Write-Host ''
Write-Host '=== Restore raster placeholder sample ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== Raster placeholder sample build: $framework ==="
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
Write-Host "Raster placeholder sample verification completed successfully ($Configuration)."
