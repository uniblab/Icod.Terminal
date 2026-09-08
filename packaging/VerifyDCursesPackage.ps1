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

$packagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.nupkg"
if ( -not ( Test-Path -LiteralPath $packagePath -PathType Leaf ) ) {
	throw "Expected package '$packagePath' was not produced."
}

$acceptanceRoot = Join-Path (
	[System.IO.Path]::GetTempPath()
) ( "Icod.Terminal-dcurses-package-acceptance-{0}" -f [Guid]::NewGuid().ToString( 'N' ) )
New-Item -ItemType Directory -Path $acceptanceRoot -Force | Out-Null
try {
	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/dcurses-package-acceptance/Icod.Terminal.DCursesPackageAcceptance.csproj' ) `
		-Destination ( Join-Path $acceptanceRoot 'Icod.Terminal.DCursesPackageAcceptance.csproj' )
	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/dcurses-hardening-soak/Program.cs' ) `
		-Destination ( Join-Path $acceptanceRoot 'Program.cs' )

	$nugetConfig = Join-Path $acceptanceRoot 'NuGet.Config'
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

	$project = Join-Path $acceptanceRoot 'Icod.Terminal.DCursesPackageAcceptance.csproj'
	$oldNuGetPackages = $env:NUGET_PACKAGES
	$env:NUGET_PACKAGES = Join-Path $acceptanceRoot 'packages'
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
			Write-Host "=== Fresh Icod.Terminal + published Icod.DCurses package soak: $framework ==="
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
	if ( Test-Path -LiteralPath $acceptanceRoot ) {
		Remove-Item -LiteralPath $acceptanceRoot -Recurse -Force
	}
}

Write-Host "Fresh-package Icod.DCurses release-line acceptance completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
