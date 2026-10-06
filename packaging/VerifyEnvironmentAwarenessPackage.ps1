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
    'T:Icod.Terminal.TerminalAppearance',
    'T:Icod.Terminal.TerminalAppearanceEvent',
    'P:Icod.Terminal.TerminalAppearanceEvent.Appearance',
    'T:Icod.Terminal.TerminalInBandResizeEvent',
    'P:Icod.Terminal.TerminalInBandResizeEvent.Dimensions',
    'P:Icod.Terminal.TerminalInBandResizeEvent.PixelDimensions',
    'P:Icod.Terminal.TerminalSemanticEvent.Appearance',
    'P:Icod.Terminal.TerminalSemanticEvent.InBandResize',
    'M:Icod.Terminal.TerminalSession.QueryAppearanceAsync(System.TimeSpan,System.Threading.CancellationToken)',
    'M:Icod.Terminal.TerminalSession.AcquireAppearanceReportingAsync(System.TimeSpan,System.Threading.CancellationToken)',
    'M:Icod.Terminal.TerminalSession.AcquireInBandResizeReportingAsync(System.TimeSpan,System.Threading.CancellationToken)',
    'T:Icod.Terminal.TerminalAppearanceReportingLease',
    'T:Icod.Terminal.TerminalInBandResizeReportingLease',
    'M:Icod.Terminal.TerminalAppearanceReportingLease.DisposeAsync',
    'M:Icod.Terminal.TerminalInBandResizeReportingLease.DisposeAsync'
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('Icod.Terminal.nuspec').Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $project = Join-Path $repositoryRoot 'Icod.Terminal.csproj'
    $evaluated = (& dotnet msbuild $project -getItem:PackageReference | Out-String) | ConvertFrom-Json
    if (0 -ne $LASTEXITCODE) { throw 'Unable to evaluate declared package dependencies.' }
    $groups = @($nuspec.SelectNodes("//*[local-name()='dependencies']/*[local-name()='group']"))
    if (3 -ne $groups.Count) { throw 'Package must declare dependency groups for all three target frameworks.' }
    foreach ($group in $groups) {
        $dependencies = @($group.SelectNodes("*[local-name()='dependency']"))
        if ($dependencies.Count -ne @($evaluated.Items.PackageReference).Count) { throw 'Package dependency count differs from the project.' }
        foreach ($declared in $evaluated.Items.PackageReference) {
            $actual = @($dependencies | Where-Object { $_.id -ceq $declared.Identity })
            if (1 -ne $actual.Count -or $actual[0].version -cne $declared.Version) { throw "Packed dependency does not match declared version: $($declared.Identity)." }
        }
    }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('README.md').Open())
    try { $readme = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $notes = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='releaseNotes']").InnerText
    foreach ($token in @($ExpectedVersion, "docs/releases/$ExpectedVersion.md", 'Compatibility-and-Versioning.md')) {
        if (-not $readme.Contains($token) -or -not $notes.Contains($token)) { throw "Packed release documentation is missing '$token'." }
    }
    foreach ($token in @('terminal appearance', 'in-band resize', 'native', 'QueryAppearanceAsync', 'AcquireAppearanceReportingAsync', 'AcquireInBandResizeReportingAsync')) {
        if (-not $readme.Contains($token)) { throw "Packed README is missing environment contract '$token'." }
    }
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
				throw "$entryPath is missing required environment-awareness documentation '$requiredMember'."
			}
		}
	}
} finally {
	$archive.Dispose()
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-environment-awareness-package-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-environment-awareness-smoke/Icod.Terminal.PackageEnvironmentAwarenessSmoke.csproj') -Destination (Join-Path $smokeRoot 'Icod.Terminal.PackageEnvironmentAwarenessSmoke.csproj')
	Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-environment-awareness-smoke/Program.cs') -Destination (Join-Path $smokeRoot 'Program.cs')

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

	$project = Join-Path $smokeRoot 'Icod.Terminal.PackageEnvironmentAwarenessSmoke.csproj'
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
			Write-Host ''
			Write-Host "=== Fresh package environment-awareness consumer: $framework ==="
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

Write-Host "1.27 environment-awareness package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
