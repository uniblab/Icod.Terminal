param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory,

    [ValidateSet('Debug', 'Staging', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ExpectedVersion = '',

    [string]$GitHubOutputPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

function Assert-TitleApiDocumentation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )

    $requiredMembers = @(
        'M:Icod.Terminal.TerminalSession.SetTitleAsync(System.String,System.Threading.CancellationToken)',
        'M:Icod.Terminal.TerminalSession.SetIconNameAsync(System.String,System.Threading.CancellationToken)',
        'M:Icod.Terminal.TerminalSession.SetWindowTitleAsync(System.String,System.Threading.CancellationToken)'
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
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
                throw "$entryPath is missing required 0.4 title API documentation: $($missingMembers -join ', ')."
            }
        }
    } finally {
        $archive.Dispose()
    }
}

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

$packages = @(
    Get-ChildItem -LiteralPath $ArtifactDirectory -Filter '*.nupkg' -File |
        Where-Object {
            $metadata = Get-PackageMetadata -PackagePath $_.FullName
            $metadata.Id -eq 'Icod.Terminal' -and $metadata.Version -eq $ExpectedVersion
        }
)
if (1 -ne $packages.Count) {
    throw "Expected exactly one Icod.Terminal $ExpectedVersion .nupkg in '$ArtifactDirectory'; found $($packages.Count)."
}

$package = $packages[0]
$metadata = Get-PackageMetadata -PackagePath $package.FullName
if ('README.md' -ne $metadata.Readme) {
    throw "Expected package readme 'README.md'; found '$($metadata.Readme)'."
}

$symbolPackagePath = Join-Path $ArtifactDirectory "Icod.Terminal.$ExpectedVersion.snupkg"
if (-not (Test-Path -LiteralPath $symbolPackagePath -PathType Leaf)) {
    throw "Expected symbol package '$symbolPackagePath' was not produced."
}

Push-Location $repositoryRoot
try {
    Write-Host ''
    Write-Host "=== Verify package structure, dependency closure, symbols, and Source Link ($Configuration) ==="
    Invoke-DotNet -Arguments @(
        'run',
        '--project', 'tools/package-verifier/Icod.Terminal.PackageVerifier.csproj',
        '-c', $Configuration,
        '-f', 'net10.0',
        '--', $ArtifactDirectory
    )

    Write-Host ''
    Write-Host '=== Verify 0.4 title XML documentation ==='
    Assert-TitleApiDocumentation -PackagePath $package.FullName

    $smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Icod.Terminal-package-smoke-{0}" -f [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
    try {
        $generalSmokeRoot = Join-Path $smokeRoot 'general'
        $titleSmokeRoot = Join-Path $smokeRoot 'title'
        New-Item -ItemType Directory -Path $generalSmokeRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $titleSmokeRoot -Force | Out-Null

        Copy-Item -LiteralPath 'tools/package-smoke/Icod.Terminal.PackageSmoke.csproj' -Destination (Join-Path $generalSmokeRoot 'Icod.Terminal.PackageSmoke.csproj')
        Copy-Item -LiteralPath 'tools/package-smoke/Program.cs' -Destination (Join-Path $generalSmokeRoot 'Program.cs')
        Copy-Item -LiteralPath 'tools/package-title-smoke/Icod.Terminal.PackageTitleSmoke.csproj' -Destination (Join-Path $titleSmokeRoot 'Icod.Terminal.PackageTitleSmoke.csproj')
        Copy-Item -LiteralPath 'tools/package-title-smoke/Program.cs' -Destination (Join-Path $titleSmokeRoot 'Program.cs')

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

        $oldNuGetPackages = $env:NUGET_PACKAGES
        $env:NUGET_PACKAGES = Join-Path $smokeRoot 'packages'
        try {
            Write-Host ''
            Write-Host '=== Fresh package consumer restore ==='
            Invoke-DotNet -Arguments @(
                'restore', (Join-Path $generalSmokeRoot 'Icod.Terminal.PackageSmoke.csproj'),
                '--no-cache',
                '--configfile', $nugetConfig,
                "-p:IcodTerminalPackageVersion=$ExpectedVersion"
            )
            Invoke-DotNet -Arguments @(
                'restore', (Join-Path $titleSmokeRoot 'Icod.Terminal.PackageTitleSmoke.csproj'),
                '--no-cache',
                '--configfile', $nugetConfig,
                "-p:IcodTerminalPackageVersion=$ExpectedVersion"
            )

            foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
                Write-Host ''
                Write-Host "=== Fresh package consumer: $framework ==="
                Invoke-DotNet -Arguments @(
                    'run',
                    '--project', (Join-Path $generalSmokeRoot 'Icod.Terminal.PackageSmoke.csproj'),
                    '-c', $Configuration,
                    '-f', $framework,
                    '--no-restore',
                    "-p:IcodTerminalPackageVersion=$ExpectedVersion"
                )

                Write-Host ''
                Write-Host "=== Fresh package OSC title consumer: $framework ==="
                Invoke-DotNet -Arguments @(
                    'run',
                    '--project', (Join-Path $titleSmokeRoot 'Icod.Terminal.PackageTitleSmoke.csproj'),
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
} finally {
    Pop-Location
}

if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    'has_packages=true' >> $GitHubOutputPath
    'package_count=1' >> $GitHubOutputPath
    "package_version=$ExpectedVersion" >> $GitHubOutputPath
}

Write-Host "Exact package verification completed successfully for Icod.Terminal $ExpectedVersion ($Configuration)."
