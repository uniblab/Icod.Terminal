param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

$project = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterAnimation.Sample/Icod.Terminal.RasterAnimation.Sample.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Raster animation sample project '$project' does not exist."
}

$programPath = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterAnimation.Sample/Program.cs'
$programText = [System.IO.File]::ReadAllText($programPath)
foreach ($forbidden in @(
    'Kitty',
    'Sixel',
    'ImageId',
    'ImageNumber',
    'FrameNumber',
    'TerminalProtocolBackend',
    'Apc',
    '\u001b_G',
    'a=f',
    'a=a',
    's=',
    'v='
)) {
    if ($programText.Contains($forbidden, [System.StringComparison]::Ordinal)) {
        throw "Raster animation sample must remain protocol-neutral; found forbidden text '$forbidden'."
    }
}

foreach ($required in @(
    'TerminalCapability.PersistentRasterAnimation',
    'CreateRasterResourceAsync',
    '.Animation',
    'RootFrame',
    'SetFrameDurationAsync',
    'AddFrameAsync',
    'CreatePlacementAsync',
    'RunLoadingAsync',
    'StopAsync',
    'SelectFrameAsync',
    'RunAsync',
    'TerminalRasterAnimationPlaybackOptions'
)) {
    if (-not $programText.Contains($required, [System.StringComparison]::Ordinal)) {
        throw "Raster animation sample is missing required semantic API usage '$required'."
    }
}

Write-Host ''
Write-Host '=== Restore raster animation sample ==='
Invoke-DotNet -Arguments @(
    'restore',
    $project
)

foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
    Write-Host ''
    Write-Host "=== Raster animation sample build: $framework ==="
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
Write-Host "Raster animation sample verification completed successfully ($Configuration)."
