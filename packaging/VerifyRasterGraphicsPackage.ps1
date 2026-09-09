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
	'T:Icod.Terminal.TerminalRasterPixelFormat',
	'T:Icod.Terminal.TerminalRasterColor',
	'T:Icod.Terminal.TerminalRasterImage',
	'P:Icod.Terminal.TerminalRasterColor.Red',
	'P:Icod.Terminal.TerminalRasterColor.Green',
	'P:Icod.Terminal.TerminalRasterColor.Blue',
	'P:Icod.Terminal.TerminalRasterColor.Alpha',
	'P:Icod.Terminal.TerminalRasterImage.Width',
	'P:Icod.Terminal.TerminalRasterImage.Height',
	'P:Icod.Terminal.TerminalRasterImage.PixelFormat',
	'P:Icod.Terminal.TerminalRasterImage.PixelCount',
	'M:Icod.Terminal.TerminalRasterColor.#ctor(System.Byte,System.Byte,System.Byte,System.Byte)',
	'M:Icod.Terminal.TerminalRasterImage.CreateRgb24(System.Int32,System.Int32,System.ReadOnlySpan{System.Byte})',
	'M:Icod.Terminal.TerminalRasterImage.CreateRgba32(System.Int32,System.Int32,System.ReadOnlySpan{System.Byte})',
	'M:Icod.Terminal.TerminalRasterImage.CreateIndexed8(System.Int32,System.Int32,System.ReadOnlySpan{System.Byte},System.ReadOnlySpan{Icod.Terminal.TerminalRasterColor})',
	'M:Icod.Terminal.TerminalRasterImage.GetPixelColor(System.Int32,System.Int32)',
	'M:Icod.Terminal.TerminalSession.DisplayRasterAsync(Icod.Terminal.TerminalRasterImage,System.Threading.CancellationToken)'
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
				throw "$entryPath is missing required 1.7 raster documentation '$requiredMember'."
			}
		}
	}
} finally {
	$archive.Dispose()
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-raster-package-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-raster-graphics-smoke/Icod.Terminal.PackageRasterGraphicsSmoke.csproj') -Destination (Join-Path $smokeRoot 'Icod.Terminal.PackageRasterGraphicsSmoke.csproj')
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-raster-graphics-smoke/Program.cs') -Destination (Join-Path $smokeRoot 'Program.cs')

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

	$project = Join-Path $smokeRoot 'Icod.Terminal.PackageRasterGraphicsSmoke.csproj'
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

Write-Host "1.7 raster graphics package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
