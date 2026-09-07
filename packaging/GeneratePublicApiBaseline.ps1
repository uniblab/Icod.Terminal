param(
	[string]$Configuration = 'Staging',
	[string]$OutputDirectory = 'artifacts/public-api'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tools/public-api-snapshot/Icod.Terminal.PublicApiSnapshot.csproj'
$outputRoot = [System.IO.Path]::GetFullPath(
	( Join-Path $root $OutputDirectory )
)
$frameworks = @(
	'net8.0',
	'net9.0',
	'net10.0'
)

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

dotnet restore $project
if ( 0 -ne $LASTEXITCODE ) {
	throw 'The public API snapshot tool restore failed.'
}

$generated = @()
foreach ( $framework in $frameworks ) {
	$outputPath = Join-Path $outputRoot "Icod.Terminal-$framework.txt"
	dotnet run `
		--project $project `
		--configuration $Configuration `
		--framework $framework `
		--no-restore `
		-p:ContinuousIntegrationBuild=true `
		-- `
		$outputPath
	if ( 0 -ne $LASTEXITCODE ) {
		throw "The public API snapshot generator failed for $framework."
	}
	$generated += $outputPath
}

$referenceHash = ( Get-FileHash -Algorithm SHA256 $generated[ 0 ] ).Hash
foreach ( $path in $generated | Select-Object -Skip 1 ) {
	$hash = ( Get-FileHash -Algorithm SHA256 $path ).Hash
	if ( $hash -ne $referenceHash ) {
		throw "Public API snapshots differ across target frameworks: '$($generated[ 0 ])' and '$path'."
	}
}

Write-Host "Generated identical Icod.Terminal public API snapshots for net8.0, net9.0, and net10.0."
Write-Host "SHA256: $referenceHash"
