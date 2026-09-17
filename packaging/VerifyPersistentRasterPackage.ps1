param(
	[Parameter(Mandatory = $true)]
	[string]$ArtifactDirectory,

	[ValidateSet('Debug', 'Staging', 'Release')]
	[string]$Configuration = 'Release',

	[string]$ExpectedVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

if (-not [System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
	$ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
	throw "Artifact directory '$ArtifactDirectory' does not exist."
}

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
	$projectPath = Join-Path $repositoryRoot 'Icod.Terminal.csproj'
	$ExpectedVersion = Get-MSBuildProperty -ProjectPath $projectPath -Name 'PackageVersion' -Configuration $Configuration
}
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
	throw 'Unable to determine the expected Icod.Terminal package version.'
}

$packagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.nupkg"
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
	throw "Expected package '$packagePath' was not produced."
}

$requiredMembers = @(
	'F:Icod.Terminal.TerminalCapability.PersistentRasterGraphics',
	'F:Icod.Terminal.TerminalCapability.UnicodeRasterPlaceholders',
	'F:Icod.Terminal.TerminalCapability.PersistentRasterAnimation',
	'T:Icod.Terminal.TerminalRasterAnimationStatus',
	'F:Icod.Terminal.TerminalRasterAnimationStatus.Current',
	'F:Icod.Terminal.TerminalRasterAnimationStatus.SequenceUncertain',
	'F:Icod.Terminal.TerminalRasterAnimationStatus.Stale',
	'F:Icod.Terminal.TerminalRasterAnimationStatus.Released',
	'F:Icod.Terminal.TerminalRasterAnimationStatus.OwnerDisposed',
	'T:Icod.Terminal.TerminalRasterAnimationLossReason',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.None',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.FrameSequenceAmbiguous',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.SessionStateLost',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.ResourceMissing',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.ResourceReleased',
	'F:Icod.Terminal.TerminalRasterAnimationLossReason.ExplicitResourceDisposal',
	'T:Icod.Terminal.TerminalRasterAnimationState',
	'M:Icod.Terminal.TerminalRasterAnimationState.#ctor(Icod.Terminal.TerminalRasterAnimationStatus,Icod.Terminal.TerminalRasterAnimationLossReason)',
	'P:Icod.Terminal.TerminalRasterAnimationState.Status',
	'P:Icod.Terminal.TerminalRasterAnimationState.LossReason',
	'T:Icod.Terminal.TerminalRasterAnimationPlaybackOptions',
	'P:Icod.Terminal.TerminalRasterAnimationPlaybackOptions.RepeatCount',
	'T:Icod.Terminal.TerminalRasterAnimationFrame',
	'T:Icod.Terminal.TerminalRasterAnimation',
	'P:Icod.Terminal.TerminalRasterAnimation.RootFrame',
	'P:Icod.Terminal.TerminalRasterAnimation.State',
	'M:Icod.Terminal.TerminalRasterAnimation.AddFrameAsync(Icod.Terminal.TerminalRasterImage,System.TimeSpan,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterAnimation.SetFrameDurationAsync(Icod.Terminal.TerminalRasterAnimationFrame,System.TimeSpan,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterAnimation.SelectFrameAsync(Icod.Terminal.TerminalRasterAnimationFrame,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterAnimation.StopAsync(System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterAnimation.RunLoadingAsync(System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterAnimation.RunAsync(Icod.Terminal.TerminalRasterAnimationPlaybackOptions,System.Threading.CancellationToken)',
	'T:Icod.Terminal.TerminalRasterOwnershipStatus',
	'F:Icod.Terminal.TerminalRasterOwnershipStatus.Current',
	'F:Icod.Terminal.TerminalRasterOwnershipStatus.Stale',
	'F:Icod.Terminal.TerminalRasterOwnershipStatus.Released',
	'F:Icod.Terminal.TerminalRasterOwnershipStatus.Disposed',
	'T:Icod.Terminal.TerminalRasterOwnershipLossReason',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.None',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.SessionStateLost',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.ResourceMissing',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.ParentPlacementLost',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.AncestorReleased',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.ResourceReleased',
	'F:Icod.Terminal.TerminalRasterOwnershipLossReason.ExplicitDisposal',
	'T:Icod.Terminal.TerminalRasterOwnershipState',
	'M:Icod.Terminal.TerminalRasterOwnershipState.#ctor(Icod.Terminal.TerminalRasterOwnershipStatus,Icod.Terminal.TerminalRasterOwnershipLossReason)',
	'P:Icod.Terminal.TerminalRasterOwnershipState.Status',
	'P:Icod.Terminal.TerminalRasterOwnershipState.LossReason',
	'T:Icod.Terminal.TerminalRasterSourceRectangle',
	'M:Icod.Terminal.TerminalRasterSourceRectangle.#ctor(System.Int32,System.Int32,System.Int32,System.Int32)',
	'P:Icod.Terminal.TerminalRasterSourceRectangle.X',
	'P:Icod.Terminal.TerminalRasterSourceRectangle.Y',
	'P:Icod.Terminal.TerminalRasterSourceRectangle.Width',
	'P:Icod.Terminal.TerminalRasterSourceRectangle.Height',
	'T:Icod.Terminal.TerminalRasterPlacementOptions',
	'P:Icod.Terminal.TerminalRasterPlacementOptions.SourceRectangle',
	'P:Icod.Terminal.TerminalRasterPlacementOptions.Columns',
	'P:Icod.Terminal.TerminalRasterPlacementOptions.Rows',
	'P:Icod.Terminal.TerminalRasterPlacementOptions.ZIndex',
	'T:Icod.Terminal.TerminalRasterPlaceholderOptions',
	'P:Icod.Terminal.TerminalRasterPlaceholderOptions.Columns',
	'P:Icod.Terminal.TerminalRasterPlaceholderOptions.Rows',
	'T:Icod.Terminal.TerminalRasterPlaceholderCell',
	'P:Icod.Terminal.TerminalRasterPlaceholderCell.Row',
	'P:Icod.Terminal.TerminalRasterPlaceholderCell.Column',
	'T:Icod.Terminal.TerminalRasterPlaceholder',
	'P:Icod.Terminal.TerminalRasterPlaceholder.Columns',
	'P:Icod.Terminal.TerminalRasterPlaceholder.Rows',
	'P:Icod.Terminal.TerminalRasterPlaceholder.OwnershipState',
	'M:Icod.Terminal.TerminalRasterPlaceholder.GetCell(System.Int32,System.Int32)',
	'M:Icod.Terminal.TerminalRasterPlaceholder.DisposeAsync',
	'T:Icod.Terminal.TerminalRasterResource',
	'P:Icod.Terminal.TerminalRasterResource.OwnershipState',
	'P:Icod.Terminal.TerminalRasterResource.Animation',
	'M:Icod.Terminal.TerminalRasterResource.CreatePlacementAsync(Icod.Terminal.TerminalRasterPlacementOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterResource.CreateRelativePlacementAsync(Icod.Terminal.TerminalRasterPlacement,System.Int32,System.Int32,Icod.Terminal.TerminalRasterPlacementOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterResource.CreatePlaceholderAsync(Icod.Terminal.TerminalRasterPlaceholderOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterResource.CreateRelativePlacementFromPlaceholderAsync(Icod.Terminal.TerminalRasterPlaceholder,System.Int32,System.Int32,Icod.Terminal.TerminalRasterPlacementOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterResource.DisposeAsync',
	'T:Icod.Terminal.TerminalRasterPlacement',
	'P:Icod.Terminal.TerminalRasterPlacement.OwnershipState',
	'M:Icod.Terminal.TerminalRasterPlacement.UpdateAsync(Icod.Terminal.TerminalRasterPlacementOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterPlacement.UpdateRelativeAsync(System.Int32,System.Int32,Icod.Terminal.TerminalRasterPlacementOptions,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalRasterPlacement.DisposeAsync',
	'M:Icod.Terminal.TerminalSession.CreateRasterResourceAsync(Icod.Terminal.TerminalRasterImage,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalSession.WriteRasterPlaceholderCellAsync(Icod.Terminal.TerminalRasterPlaceholderCell,System.Threading.CancellationToken)',
	'M:Icod.Terminal.TerminalSession.WriteRasterPlaceholderCellsAsync(System.ReadOnlyMemory{Icod.Terminal.TerminalRasterPlaceholderCell},System.Threading.CancellationToken)'
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
	foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
		$entryPath = "lib/$framework/Icod.Terminal.xml"
		$entry = $archive.GetEntry($entryPath)
		if ($null -eq $entry) {
			throw "Package is missing generated documentation '$entryPath'."
		}

		$stream = $entry.Open()
		try {
			$documentation = [System.Xml.XmlDocument]::new()
			$documentation.Load($stream)
		} finally {
			$stream.Dispose()
		}

		$documentedMembers = @(
			$documentation.SelectNodes('/doc/members/member') |
				ForEach-Object { $_.GetAttribute('name') }
		)
		foreach ($requiredMember in $requiredMembers) {
			if ($requiredMember -notin $documentedMembers) {
				throw "$entryPath is missing required persistent-raster documentation '$requiredMember'."
			}
		}
	}
} finally {
	$archive.Dispose()
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-persistent-raster-package-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-persistent-raster-smoke/Icod.Terminal.PackagePersistentRasterSmoke.csproj') -Destination (Join-Path $smokeRoot 'Icod.Terminal.PackagePersistentRasterSmoke.csproj')
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-persistent-raster-smoke/Program.cs') -Destination (Join-Path $smokeRoot 'Program.cs')

	$nugetConfig = Join-Path $smokeRoot 'NuGet.Config'
	$artifactUri = [System.Security.SecurityElement]::Escape($ArtifactDirectory)
	$nugetConfigText = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="Icod.Terminal artifacts" value="$artifactUri" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
	[System.IO.File]::WriteAllText($nugetConfig, $nugetConfigText, [System.Text.UTF8Encoding]::new($false))

	$project = Join-Path $smokeRoot 'Icod.Terminal.PackagePersistentRasterSmoke.csproj'
	$oldNuGetPackages = $env:NUGET_PACKAGES
	$env:NUGET_PACKAGES = Join-Path $smokeRoot 'packages'
	try {
		Invoke-DotNet -Arguments @(
			'restore', $project,
			'--no-cache',
			'--configfile', $nugetConfig,
			"-p:IcodTerminalPackageVersion=$ExpectedVersion"
		)

		foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
			Invoke-DotNet -Arguments @(
				'run',
				'--project', $project,
				'-c', $Configuration,
				'-f', $framework,
				'--no-restore',
				"-p:IcodTerminalPackageVersion=$ExpectedVersion"
			)
		}
	} finally {
		$env:NUGET_PACKAGES = $oldNuGetPackages
	}
} finally {
	if (Test-Path -LiteralPath $smokeRoot) {
		Remove-Item -LiteralPath $smokeRoot -Recurse -Force
	}
}

Write-Host "1.16 persistent-raster animation and Unicode-placeholder package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
