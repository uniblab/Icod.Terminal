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
    'T:Icod.Terminal.TerminalProgressState',
    'F:Icod.Terminal.TerminalProgressState.Normal',
    'F:Icod.Terminal.TerminalProgressState.Error',
    'F:Icod.Terminal.TerminalProgressState.Attention',
    'T:Icod.Terminal.TerminalProgressLease',
    'M:Icod.Terminal.TerminalProgressLease.ReportAsync(System.Int64,System.Int64,System.Threading.CancellationToken)',
    'M:Icod.Terminal.TerminalProgressLease.ReportAsync(Icod.Terminal.TerminalProgressState,System.Int64,System.Int64,System.Threading.CancellationToken)',
    'M:Icod.Terminal.TerminalProgressLease.SetIndeterminateAsync(System.Threading.CancellationToken)',
    'M:Icod.Terminal.TerminalProgressLease.DisposeAsync',
    'M:Icod.Terminal.TerminalSession.AcquireProgressAsync(System.Threading.CancellationToken)'
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
        $missingMembers = @(
            $requiredMembers |
                Where-Object { $_ -notin $documentedMembers }
        )
        if (0 -ne $missingMembers.Count) {
            throw "$entryPath is missing required 0.10 terminal-progress documentation: $($missingMembers -join ', ')."
        }
    }
} finally {
    $archive.Dispose()
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-progress-package-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-progress-smoke/Icod.Terminal.PackageProgressSmoke.csproj') -Destination (Join-Path $smokeRoot 'Icod.Terminal.PackageProgressSmoke.csproj')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'tools/package-progress-smoke/Program.cs') -Destination (Join-Path $smokeRoot 'Program.cs')

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

    $project = Join-Path $smokeRoot 'Icod.Terminal.PackageProgressSmoke.csproj'
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
            Write-Host "=== Fresh package terminal-progress consumer: $framework ==="
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

Write-Host "0.10 terminal-progress package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
