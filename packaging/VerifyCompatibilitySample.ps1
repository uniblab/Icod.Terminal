param(
    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Staging',

    [string]$ArtifactDirectory = '',

    [string]$ExpectedVersion = '',

    [string]$EvidenceVersion = '1.26.0',

    [string]$EvidenceRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force
$packageProject = Join-Path $repositoryRoot 'Icod.Terminal.csproj'
$sampleRoot = Join-Path $repositoryRoot 'samples/Icod.Terminal.Compatibility.Sample'
$smokeSource = Join-Path $repositoryRoot 'tools/package-compatibility-smoke'

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Get-MSBuildProperty `
        -ProjectPath $packageProject `
        -Name 'PackageVersion' `
        -Configuration $Configuration
}
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    throw 'Unable to determine the expected Icod.Terminal package version.'
}

if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $repositoryRoot "docs/compatibility/evidence/$EvidenceVersion"
}
$ownedArtifactDirectory = $false
if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-compatibility-package-{0}" -f [Guid]::NewGuid().ToString('N'))
    $ownedArtifactDirectory = $true
} elseif (-not [System.IO.Path]::IsPathRooted($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $repositoryRoot $ArtifactDirectory
}
$ArtifactDirectory = [System.IO.Path]::GetFullPath($ArtifactDirectory)
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null

$packagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.nupkg"
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    Write-Host ''
    Write-Host "=== Build compatibility package candidate: $ExpectedVersion ==="
    Invoke-DotNet -Arguments @('restore', $packageProject)
    Invoke-DotNet -Arguments @(
        'pack', $packageProject,
        '-c', $Configuration,
        '--no-restore',
        '-o', $ArtifactDirectory,
        '-p:ContinuousIntegrationBuild=true'
    )
}
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Expected package '$packagePath' was not produced."
}

$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-compatibility-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $smokeSource 'Icod.Terminal.PackageCompatibilitySmoke.csproj') -Destination $smokeRoot
    Copy-Item -LiteralPath (Join-Path $smokeSource 'Program.cs') -Destination $smokeRoot
    Get-ChildItem -LiteralPath $sampleRoot -Filter '*.cs' -File |
        Where-Object { $_.Name -ne 'Program.cs' } |
        Copy-Item -Destination $smokeRoot
    Copy-Item -LiteralPath (Join-Path $sampleRoot 'fixtures') -Destination $smokeRoot -Recurse
    Copy-Item -LiteralPath $EvidenceRoot -Destination (Join-Path $smokeRoot 'versioned-evidence') -Recurse
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs/compatibility/$EvidenceVersion.md") -Destination (Join-Path $smokeRoot 'fixtures/versioned-matrix.md')

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

    $project = Join-Path $smokeRoot 'Icod.Terminal.PackageCompatibilitySmoke.csproj'
    $oldMatrixVersion = $env:ICOD_COMPATIBILITY_MATRIX_VERSION
    $env:ICOD_COMPATIBILITY_MATRIX_VERSION = $EvidenceVersion
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
            Write-Host "=== Compatibility fresh-package smoke: $framework ==="
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
        $env:ICOD_COMPATIBILITY_MATRIX_VERSION = $oldMatrixVersion
    }
} finally {
    if (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
    if ($ownedArtifactDirectory -and (Test-Path -LiteralPath $ArtifactDirectory)) {
        Remove-Item -LiteralPath $ArtifactDirectory -Recurse -Force
    }
}

Write-Host "Compatibility sample package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
