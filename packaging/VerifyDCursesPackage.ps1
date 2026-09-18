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

function Assert-FutureConsumerBoundary {
	param(
		[Parameter(Mandatory = $true)]
		[string]$ProjectPath,

		[Parameter(Mandatory = $true)]
		[string]$SourceRoot,

		[Parameter(Mandatory = $true)]
		[string]$PackageVersion,

		[Parameter(Mandatory = $true)]
		[ValidateSet('Debug', 'Staging', 'Release')]
		[string]$Configuration
	)

	$ownedSources = @(
		Get-ChildItem -LiteralPath $SourceRoot -Filter '*.cs' -File -Recurse |
			ForEach-Object { [System.IO.Path]::GetFullPath( $_.FullName ) } |
			Sort-Object
	)
	if ( 0 -eq $ownedSources.Count ) {
		throw "Future DCurses consumer source root '$SourceRoot' contains no C# sources."
	}
	foreach ( $sourcePath in $ownedSources ) {
		$source = [System.IO.File]::ReadAllText( $sourcePath )
		if ( 0 -le $source.IndexOf( 'Icod.TermInfo', [System.StringComparison]::Ordinal ) ) {
			throw "Future DCurses consumer source '$sourcePath' directly uses Icod.TermInfo."
		}
	}

	foreach ( $framework in @('net8.0', 'net9.0', 'net10.0') ) {
		$evaluationText = @(
			& dotnet msbuild `
				$ProjectPath `
				-nologo `
				'-getItem:Compile,PackageReference,ProjectReference,Reference' `
				'-getProperty:Configuration,IcodTerminalPackageVersion' `
				"-property:TargetFramework=$framework" `
				"-property:IcodTerminalPackageVersion=$PackageVersion" `
				"-property:Configuration=$Configuration" `
				'-nodeReuse:false'
		) -join [System.Environment]::NewLine
		if ( 0 -ne $LASTEXITCODE ) {
			throw "Unable to evaluate the future DCurses consumer for $framework."
		}
		$evaluation = $evaluationText | ConvertFrom-Json
		if ( $Configuration -ne [string]$evaluation.Properties.Configuration ) {
			throw "Future DCurses consumer $framework evaluated configuration '$($evaluation.Properties.Configuration)' instead of '$Configuration'."
		}
		if ( $PackageVersion -ne [string]$evaluation.Properties.IcodTerminalPackageVersion ) {
			throw "Future DCurses consumer $framework evaluated Icod.Terminal version '$($evaluation.Properties.IcodTerminalPackageVersion)' instead of '$PackageVersion'."
		}

		$compileInputs = @(
			$evaluation.Items.Compile |
				ForEach-Object {
					[System.IO.Path]::GetFullPath( [string]$_.FullPath )
				} |
				Sort-Object
		)
		$compileDifference = @(
			Compare-Object `
				-ReferenceObject $ownedSources `
				-DifferenceObject $compileInputs
		)
		if ( 0 -ne $compileDifference.Count ) {
			throw "Future DCurses consumer $framework Compile inputs must exactly match the controlled recursive source tree."
		}

		$linkedInputs = @(
			$evaluation.Items.Compile |
				Where-Object {
					$linkProperty = $_.PSObject.Properties['Link']
					$null -ne $linkProperty `
						-and -not [string]::IsNullOrWhiteSpace( [string]$linkProperty.Value )
				}
		)
		if ( 0 -ne $linkedInputs.Count ) {
			throw "Future DCurses consumer $framework contains linked Compile inputs."
		}

		$packageReferences = @(
			$evaluation.Items.PackageReference |
				ForEach-Object { [string]$_.Identity }
		)
		if ( 1 -ne $packageReferences.Count -or 'Icod.Terminal' -ne $packageReferences[0] ) {
			throw "Future DCurses consumer $framework must directly reference only Icod.Terminal."
		}
		if ( 0 -ne @($evaluation.Items.ProjectReference).Count ) {
			throw "Future DCurses consumer $framework must not contain project references."
		}
		if ( 0 -ne @($evaluation.Items.Reference).Count ) {
			throw "Future DCurses consumer $framework must not contain direct assembly references."
		}
	}
}

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
$primaryError = $null
try {
	$stableRoot = Join-Path $acceptanceRoot 'stable'
	$futureRoot = Join-Path $acceptanceRoot 'future'
	$futureSourceRoot = Join-Path $repositoryRoot 'tools/dcurses-screen-contracts-acceptance'
	New-Item -ItemType Directory -Path $stableRoot -Force | Out-Null
	New-Item -ItemType Directory -Path $futureRoot -Force | Out-Null

	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/dcurses-package-acceptance/Icod.Terminal.DCursesPackageAcceptance.csproj' ) `
		-Destination ( Join-Path $stableRoot 'Icod.Terminal.DCursesPackageAcceptance.csproj' )
	Copy-Item `
		-LiteralPath ( Join-Path $repositoryRoot 'tools/dcurses-hardening-soak/Program.cs' ) `
		-Destination ( Join-Path $stableRoot 'Program.cs' )
	Copy-Item `
		-LiteralPath ( Join-Path $futureSourceRoot 'Icod.Terminal.DCursesScreenContractsAcceptance.csproj' ) `
		-Destination ( Join-Path $futureRoot 'Icod.Terminal.DCursesScreenContractsAcceptance.csproj' )
	Copy-Item `
		-LiteralPath ( Join-Path $futureSourceRoot 'Source' ) `
		-Destination ( Join-Path $futureRoot 'Source' ) `
		-Recurse

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

	$stableProject = Join-Path $stableRoot 'Icod.Terminal.DCursesPackageAcceptance.csproj'
	$futureProject = Join-Path $futureRoot 'Icod.Terminal.DCursesScreenContractsAcceptance.csproj'
	Assert-FutureConsumerBoundary `
		-ProjectPath $futureProject `
		-SourceRoot ( Join-Path $futureRoot 'Source' ) `
		-PackageVersion $ExpectedVersion `
		-Configuration $Configuration
	$oldNuGetPackages = $env:NUGET_PACKAGES
	$env:NUGET_PACKAGES = Join-Path $acceptanceRoot 'packages'
	try {
		foreach ( $project in @($stableProject, $futureProject) ) {
			Invoke-DotNet -Arguments @(
				'restore',
				$project,
				'--no-cache',
				'--configfile',
				$nugetConfig,
				"-p:IcodTerminalPackageVersion=$ExpectedVersion",
				"-p:Configuration=$Configuration"
			)
		}

		foreach ( $framework in @('net8.0', 'net9.0', 'net10.0') ) {
			Write-Host ''
			Write-Host "=== Candidate Icod.Terminal + published Icod.DCurses 1.6.0 soak: $framework ==="
			Invoke-DotNet -Arguments @(
				'run',
				'--project',
				$stableProject,
				'-c',
				$Configuration,
				'-f',
				$framework,
				'--no-restore',
				"-p:IcodTerminalPackageVersion=$ExpectedVersion"
			)

			Write-Host ''
			Write-Host "=== Future DCurses Terminal-owned screen contracts: $framework ==="
			Invoke-DotNet -Arguments @(
				'run',
				'--project',
				$futureProject,
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
} catch {
	$primaryError = $_
} finally {
	if ( Test-Path -LiteralPath $acceptanceRoot ) {
		try {
			Remove-Item -LiteralPath $acceptanceRoot -Recurse -Force
		} catch {
			if ( $null -ne $primaryError ) {
				Write-Warning "Could not remove temporary DCurses package acceptance root '$acceptanceRoot' after validation failed: $($_.Exception.Message)"
			} else {
				throw
			}
		}
	}
}

if ( $null -ne $primaryError ) {
	throw $primaryError
}

Write-Host "Stable Icod.DCurses 1.6.0 and future screen-contract package acceptance completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
