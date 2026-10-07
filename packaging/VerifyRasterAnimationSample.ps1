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
$compositionPath = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterAnimation.Sample/RasterAnimationCompositionExample.cs'
if (-not (Test-Path -LiteralPath $compositionPath -PathType Leaf)) {
    throw "Raster animation sample composition step '$compositionPath' does not exist."
}
$compositionText = [System.IO.File]::ReadAllText($compositionPath)
$tileAtlasPath = Join-Path $repositoryRoot 'samples/Icod.Terminal.RasterAnimation.Sample/RasterTileAtlasExample.cs'
if (-not (Test-Path -LiteralPath $tileAtlasPath -PathType Leaf)) {
    throw "Raster animation tile-atlas step '$tileAtlasPath' does not exist."
}
$tileAtlasText = [System.IO.File]::ReadAllText($tileAtlasPath)
$sampleText = $programText + $compositionText + $tileAtlasText
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
    ',s=',
    ',v='
)) {
    if ($sampleText.Contains($forbidden, [System.StringComparison]::Ordinal)) {
        throw "Raster animation sample must remain protocol-neutral; found forbidden text '$forbidden'."
    }
}

foreach ($required in @(
    'TerminalCapability.PersistentRasterAnimation',
    'TerminalCapability.PersistentRasterGraphics',
    'CreateRasterResourceAsync',
    '.Animation',
    'RootFrame',
    'SetFrameDurationAsync',
    'AddFrameAsync',
    'ComposeFrameAsync',
    'TerminalRasterFrameCompositionMode.Replace',
    'CreatePlacementAsync',
    'RunLoadingAsync',
    'StopAsync',
    'SelectFrameAsync',
    'RunAsync',
    'TerminalRasterAnimationPlaybackOptions',
	'TerminalPixelDimensions',
	'QueryCellPixelDimensionsAsync',
	'QueryTerminalPixelDimensionsAsync',
	'TerminalPixelGeometry.TryDeriveCellDimensions',
	'GetRasterPlanningSnapshot',
	'InspectRasterOperation',
	'TerminalRasterOperation.FrameRegionUpdateRgb24',
	'TerminalCapabilitySupport.Verified',
	'TerminalCapabilityEvidenceKind.LiveObservation',
	'.Confirmation',
	'FormatMutationOutcome',
	'FormatExceptionOutcome',
	'FormatCleanupOutcome',
	'Rendered=NotClaimed',
	'.PixelWidth',
	'.PixelHeight',
	'CreatePlaceholderAsync',
	'GetCell',
	'WriteRasterPlaceholderCellsAsync'
)) {
    if (-not $sampleText.Contains($required, [System.StringComparison]::Ordinal)) {
        throw "Raster animation sample is missing required semantic API usage '$required'."
    }
}
if (-not $programText.Contains('RasterAnimationCompositionExample.ComposeAsync', [System.StringComparison]::Ordinal)) {
    throw 'The executable raster animation sample must call the tested composition step.'
}
if (-not $programText.Contains('RasterAnimationCompositionExample.VerifyPrerequisiteAsync', [System.StringComparison]::Ordinal)) {
    throw 'The executable raster animation sample must use the tested capability preflight.'
}
if (-not $programText.Contains('RasterTileAtlasExample.RunAsync', [System.StringComparison]::Ordinal)) {
	throw 'The executable raster animation sample must expose the tile-atlas witness.'
}
foreach ($workload in @('1', '4', '16', '64')) {
	if (-not $tileAtlasText.Contains($workload, [System.StringComparison]::Ordinal)) {
		throw "The tile-atlas witness is missing workload '$workload'."
	}
}
$workloadIndex = $tileAtlasText.IndexOf(
	'foreach ( int regionCount in Workloads )',
	[System.StringComparison]::Ordinal
)
$verifiedEvidenceIndex = $tileAtlasText.IndexOf(
	'TerminalCapabilitySupport.Verified',
	[System.StringComparison]::Ordinal
)
if ($workloadIndex -lt 0 -or $verifiedEvidenceIndex -le $workloadIndex) {
	throw 'The tile-atlas witness must inspect verified focused evidence after its acknowledged workloads.'
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
