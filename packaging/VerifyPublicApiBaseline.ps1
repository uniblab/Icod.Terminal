param(
	[string]$Configuration = 'Staging',
	[string]$OutputDirectory = 'artifacts/public-api',
	[string]$BaselinePath = 'docs/Public-API-Baseline-1.0-rc1.sha256'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$baselineFile = [System.IO.Path]::GetFullPath(
	( Join-Path $root $BaselinePath )
)
$outputRoot = [System.IO.Path]::GetFullPath(
	( Join-Path $root $OutputDirectory )
)

if ( ![System.IO.File]::Exists( $baselineFile ) ) {
	throw "The public API baseline fingerprint does not exist: '$baselineFile'."
}

$expectedHash = [System.IO.File]::ReadAllText( $baselineFile ).Trim()
if ( $expectedHash -notmatch '^[0-9A-Fa-f]{64}$' ) {
	throw "The public API baseline fingerprint is not a single SHA-256 value: '$baselineFile'."
}

& ( Join-Path $PSScriptRoot 'GeneratePublicApiBaseline.ps1' ) `
	-Configuration $Configuration `
	-OutputDirectory $OutputDirectory

$generated = @(
	( Join-Path $outputRoot 'Icod.Terminal-net8.0.txt' )
	( Join-Path $outputRoot 'Icod.Terminal-net9.0.txt' )
	( Join-Path $outputRoot 'Icod.Terminal-net10.0.txt' )
)

function Get-NormalizedSha256(
	[string]$Path
) {
	$text = [System.IO.File]::ReadAllText( $Path )
	$normalized = $text.Replace( "`r`n", "`n" ).Replace( "`r", "`n" )
	$encoding = [System.Text.UTF8Encoding]::new( $false )
	$bytes = $encoding.GetBytes( $normalized )
	$sha256 = [System.Security.Cryptography.SHA256]::Create()
	try {
		return [System.Convert]::ToHexString(
			$sha256.ComputeHash( $bytes )
		).ToLowerInvariant()
	} finally {
		$sha256.Dispose()
	}
}

$actualHashes = foreach ( $path in $generated ) {
	if ( ![System.IO.File]::Exists( $path ) ) {
		throw "The public API snapshot was not generated: '$path'."
	}
	Get-NormalizedSha256 -Path $path
}

$referenceHash = $actualHashes[ 0 ]
foreach ( $hash in $actualHashes | Select-Object -Skip 1 ) {
	if ( $hash -ne $referenceHash ) {
		throw 'The normalized public API snapshots differ across target frameworks.'
	}
}

if ( $referenceHash -ne $expectedHash.ToLowerInvariant() ) {
	$message = "The Icod.Terminal public API does not match the frozen 1.0.0-rc1 baseline. Expected $expectedHash but generated $referenceHash. Inspect artifacts/public-api before changing the baseline intentionally."
	throw $message
}

Write-Host 'Verified the frozen Icod.Terminal 1.0.0-rc1 public API baseline.'
Write-Host "SHA256: $referenceHash"
