param(
	[Parameter(Mandatory = $true)]
	[string]$ArtifactDirectory,

	[ValidateSet('Debug', 'Staging', 'Release')]
	[string]$Configuration = 'Release',

	[string]$ExpectedVersion = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath(
	( Join-Path $PSScriptRoot '..' )
)
Import-Module ( Join-Path $PSScriptRoot 'RepositoryTools.psm1' ) -Force

if ( -not [System.IO.Path]::IsPathRooted( $ArtifactDirectory ) ) {
	$ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath( $ArtifactDirectory )
if ( -not ( Test-Path -LiteralPath $ArtifactDirectory -PathType Container ) ) {
	throw "Artifact directory '$ArtifactDirectory' does not exist."
}

if ( [string]::IsNullOrWhiteSpace( $ExpectedVersion ) ) {
	$projectPath = Join-Path $repositoryRoot 'Icod.Terminal.csproj'
	$ExpectedVersion = Get-MSBuildProperty `
		-ProjectPath $projectPath `
		-Name 'PackageVersion' `
		-Configuration $Configuration
}
if ( [string]::IsNullOrWhiteSpace( $ExpectedVersion ) ) {
	throw 'Unable to determine the expected Icod.Terminal package version.'
}

$isPrerelease = $ExpectedVersion.Contains(
	'-',
	[System.StringComparison]::Ordinal
)

if ( -not $isPrerelease ) {
	$curatedReleaseNotesPath = Join-Path $repositoryRoot "docs/releases/$ExpectedVersion.md"
	if ( -not ( Test-Path -LiteralPath $curatedReleaseNotesPath -PathType Leaf ) ) {
		throw "Curated release notes are missing for $ExpectedVersion at '$curatedReleaseNotesPath'."
	}
	$curatedReleaseNotes = [System.IO.File]::ReadAllText( $curatedReleaseNotesPath )
	foreach ( $requiredText in @(
		$ExpectedVersion,
		'net8.0',
		'net9.0',
		'net10.0',
		'Compatibility-and-Versioning.md'
	) ) {
		$curatedHasText = $curatedReleaseNotes.Contains(
			$requiredText,
			[System.StringComparison]::Ordinal
		)
		if ( -not $curatedHasText ) {
			throw "Curated release notes are missing required text '$requiredText'."
		}
	}

	$changelogPath = Join-Path $repositoryRoot 'CHANGELOG.md'
	if ( -not ( Test-Path -LiteralPath $changelogPath -PathType Leaf ) ) {
		throw 'The repository does not contain CHANGELOG.md.'
	}
	$changelog = [System.IO.File]::ReadAllText( $changelogPath )
	$changelogHeading = "## $ExpectedVersion"
	if ( -not $changelog.Contains(
			$changelogHeading,
			[System.StringComparison]::Ordinal
		) ) {
		throw "CHANGELOG.md does not contain release heading '$changelogHeading'."
	}
}

$packagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.nupkg"
if ( -not ( Test-Path -LiteralPath $packagePath -PathType Leaf ) ) {
	throw "Expected package '$packagePath' was not produced."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead( $packagePath )
try {
	$nuspecEntries = @(
		$archive.Entries |
			Where-Object {
				$_.FullName.EndsWith(
					'.nuspec',
					[System.StringComparison]::OrdinalIgnoreCase
				)
			}
	)
	if ( 1 -ne $nuspecEntries.Count ) {
		throw "The package contains $($nuspecEntries.Count) nuspec files; expected exactly one."
	}

	$nuspecReader = [System.IO.StreamReader]::new( $nuspecEntries[ 0 ].Open() )
	try {
		[xml]$nuspec = $nuspecReader.ReadToEnd()
	} finally {
		$nuspecReader.Dispose()
	}

	$metadata = $nuspec.SelectSingleNode(
		"/*[local-name()='package']/*[local-name()='metadata']"
	)
	if ( $null -eq $metadata ) {
		throw 'The package nuspec does not contain metadata.'
	}

	if ( -not $isPrerelease ) {
		$releaseNotesNode = $metadata.SelectSingleNode(
			"*[local-name()='releaseNotes']"
		)
		if ( $null -eq $releaseNotesNode ) {
			throw 'The package nuspec does not contain release notes.'
		}

		$releaseNotes = $releaseNotesNode.InnerText
		foreach ( $requiredText in @(
			$ExpectedVersion,
			"docs/releases/$ExpectedVersion.md",
			'Compatibility-and-Versioning.md'
		) ) {
			$releaseNotesHaveText = $releaseNotes.Contains(
				$requiredText,
				[System.StringComparison]::Ordinal
			)
			if ( -not $releaseNotesHaveText ) {
				throw "Package release notes are missing required text '$requiredText'."
			}
		}

		$readmeEntry = $archive.GetEntry( 'README.md' )
		if ( $null -eq $readmeEntry ) {
			throw 'The package does not contain README.md.'
		}
		$readmeReader = [System.IO.StreamReader]::new( $readmeEntry.Open() )
		try {
			$readme = $readmeReader.ReadToEnd()
		} finally {
			$readmeReader.Dispose()
		}

		foreach ( $requiredText in @(
			$ExpectedVersion,
			'Compatibility-and-Versioning.md',
			'Migration-to-1.0.md',
			"docs/releases/$ExpectedVersion.md",
			'CHANGELOG.md',
			'docs/Architecture.md'
		) ) {
			$readmeHasText = $readme.Contains(
				$requiredText,
				[System.StringComparison]::Ordinal
			)
			if ( -not $readmeHasText ) {
				throw "The packed README is missing required release-line text '$requiredText'."
			}
		}
	}

	$requiredMembers = @(
		'T:Icod.Terminal.ITerminalInput',
		'P:Icod.Terminal.TerminalSession.Output',
		'M:Icod.Terminal.TerminalSession.ReadEventAsync(System.Threading.CancellationToken)',
		'M:Icod.Terminal.TerminalSession.ReadEventAsync(System.TimeSpan,System.Threading.CancellationToken)'
	)
	$forbiddenMember = 'P:Icod.Terminal.TerminalSession.Input'

	foreach ( $framework in @('net8.0', 'net9.0', 'net10.0') ) {
		$entryPath = "lib/$framework/Icod.Terminal.xml"
		$documentationEntry = $archive.GetEntry( $entryPath )
		if ( $null -eq $documentationEntry ) {
			throw "The package is missing generated documentation '$entryPath'."
		}

		$stream = $documentationEntry.Open()
		try {
			$documentation = [System.Xml.XmlDocument]::new()
			$documentation.Load( $stream )
		} finally {
			$stream.Dispose()
		}

		$documentedMembers = @(
			$documentation.SelectNodes( '/doc/members/member' ) |
				ForEach-Object { $_.GetAttribute( 'name' ) }
		)
		$missingMembers = @(
			$requiredMembers |
				Where-Object { $_ -notin $documentedMembers }
		)
		if ( 0 -ne $missingMembers.Count ) {
			throw "$entryPath is missing required stable 1.x documentation: $($missingMembers -join ', ')."
		}
		if ( $forbiddenMember -in $documentedMembers ) {
			throw "$entryPath unexpectedly documents removed public member $forbiddenMember."
		}
	}
} finally {
	$archive.Dispose()
}

$smokeRoot = Join-Path (
	[System.IO.Path]::GetTempPath()
) ( "Icod.Terminal-1.x-package-smoke-{0}" -f [Guid]::NewGuid().ToString( 'N' ) )
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/package-release-line-smoke/Icod.Terminal.PackageReleaseLineSmoke.csproj' ) `
		-Destination ( Join-Path $smokeRoot 'Icod.Terminal.PackageReleaseLineSmoke.csproj' )
	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/package-release-line-smoke/Program.cs' ) `
		-Destination ( Join-Path $smokeRoot 'Program.cs' )

	$nugetConfig = Join-Path $smokeRoot 'NuGet.Config'
	$artifactUri = [System.Security.SecurityElement]::Escape( $ArtifactDirectory )
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
	[System.IO.File]::WriteAllText(
		$nugetConfig,
		$nugetConfigText,
		[System.Text.UTF8Encoding]::new( $false )
	)

	$project = Join-Path $smokeRoot 'Icod.Terminal.PackageReleaseLineSmoke.csproj'
	$oldNuGetPackages = $env:NUGET_PACKAGES
	$env:NUGET_PACKAGES = Join-Path $smokeRoot 'packages'
	try {
		Invoke-DotNet -Arguments @(
			'restore',
			$project,
			'--no-cache',
			'--configfile',
			$nugetConfig,
			"-p:IcodTerminalPackageVersion=$ExpectedVersion"
		)

		foreach ( $framework in @('net8.0', 'net9.0', 'net10.0') ) {
			Write-Host ''
			Write-Host "=== Fresh package stable 1.x release-line consumer: $framework ==="
			Invoke-DotNet -Arguments @(
				'run',
				'--project',
				$project,
				'-c',
				$Configuration,
				'-f',
				$framework,
				'--no-restore',
				"-p:IcodTerminalPackageVersion=$ExpectedVersion"
			)
		}
	} finally {
		$env:NUGET_PACKAGES = $oldNuGetPackages
	}
} finally {
	if ( Test-Path -LiteralPath $smokeRoot ) {
		Remove-Item -LiteralPath $smokeRoot -Recurse -Force
	}
}

Write-Host "Stable 1.x release-line package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
