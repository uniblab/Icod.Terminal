param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [string]$GitHubOutputPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $PSScriptRoot 'RepositoryTools.psm1') -Force

foreach ($variableName in @('SourceDirectory', 'DestinationDirectory')) {
    $value = Get-Variable -Name $variableName -ValueOnly
    if (-not [System.IO.Path]::IsPathRooted($value)) {
        $value = Join-Path $repositoryRoot $value
    }
    Set-Variable -Name $variableName -Value ([System.IO.Path]::GetFullPath($value))
}

if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    throw "Source package directory '$SourceDirectory' does not exist."
}
if (Test-Path -LiteralPath $DestinationDirectory) {
    Remove-Item -LiteralPath $DestinationDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

$selected = @()
$packages = @(
    Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.nupkg' -File |
        Where-Object { -not $_.Name.EndsWith('.symbols.nupkg', [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object Name
)

foreach ($package in $packages) {
    $metadata = Get-PackageMetadata -PackagePath $package.FullName
    if ($metadata.Version -ne $ExpectedVersion) {
        Write-Host "Skipping $($metadata.Id) $($metadata.Version); release tag expects $ExpectedVersion."
        continue
    }

    $destination = Join-Path $DestinationDirectory $package.Name
    Copy-Item -LiteralPath $package.FullName -Destination $destination
    $selected += $destination
    Write-Host "Selected $($metadata.Id) $($metadata.Version)."

    $symbolPackage = Join-Path $SourceDirectory "$($metadata.Id).$($metadata.Version).snupkg"
    if (Test-Path -LiteralPath $symbolPackage -PathType Leaf) {
        Copy-Item -LiteralPath $symbolPackage -Destination (Join-Path $DestinationDirectory (Split-Path $symbolPackage -Leaf))
    }
}

$hasPackages = 0 -lt $selected.Count
if (-not [string]::IsNullOrWhiteSpace($GitHubOutputPath)) {
    "has_packages=$($hasPackages.ToString().ToLowerInvariant())" >> $GitHubOutputPath
    "package_count=$($selected.Count)" >> $GitHubOutputPath
}

Write-Host "Selected $($selected.Count) NuGet package(s) for release $ExpectedVersion."
