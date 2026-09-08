param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-ProjectAssemblyName {
	param( [Parameter( Mandatory = $true )][string] $ProjectPath )

	[xml] $projectXml = Get-Content -LiteralPath $ProjectPath -Raw
	$assemblyNameNode = $projectXml.SelectSingleNode( '/Project/PropertyGroup/AssemblyName' )
	if ( $null -ne $assemblyNameNode -and -not [string]::IsNullOrWhiteSpace( [string] $assemblyNameNode.InnerText ) ) {
		return [string] $assemblyNameNode.InnerText
	}

	return [IO.Path]::GetFileNameWithoutExtension( $ProjectPath )
}

function Get-LicenseKind {
	param( [Parameter( Mandatory = $true )][string] $ProjectPath )

	$relative = [IO.Path]::GetRelativePath( $root, $ProjectPath ).Replace( '\\', '/' )
	if ( $relative -eq 'Icod.Terminal.csproj' ) {
		return 'LGPL'
	}

	return 'GPL'
}

function Find-OwningProject {
	param( [Parameter( Mandatory = $true )][string] $SourcePath )

	$directory = Split-Path -Parent $SourcePath
	while ( $directory.StartsWith( $root, [StringComparison]::OrdinalIgnoreCase ) ) {
		$projects = @( Get-ChildItem -LiteralPath $directory -Filter '*.csproj' -File )
		if ( $projects.Count -eq 1 ) {
			return $projects[ 0 ].FullName
		}
		if ( $projects.Count -gt 1 ) {
			throw "Multiple owning projects found for '$SourcePath' in '$directory'."
		}
		if ( $directory -eq $root ) {
			break
		}
		$directory = Split-Path -Parent $directory
	}

	if ( $SourcePath.StartsWith( (Join-Path $root 'src'), [StringComparison]::OrdinalIgnoreCase ) ) {
		return Join-Path $root 'Icod.Terminal.csproj'
	}

	throw "No owning project found for '$SourcePath'."
}

$copyright = 'Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>'
$failures = [Collections.Generic.List[string]]::new()

$projects = @( Get-ChildItem -LiteralPath $root -Recurse -Filter '*.csproj' -File | Where-Object {
	$_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} )

foreach ( $project in $projects ) {
	$content = [IO.File]::ReadAllText( $project.FullName ).Replace( "`r`n", "`n" )
	$assemblyName = Get-ProjectAssemblyName -ProjectPath $project.FullName
	$licenseKind = Get-LicenseKind -ProjectPath $project.FullName
	$licenseName = if ( $licenseKind -eq 'LGPL' ) { 'GNU Lesser General Public License' } else { 'GNU General Public License' }

	if ( -not $content.StartsWith( "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<!--`n`t$assemblyName`n", [StringComparison]::Ordinal ) ) {
		$failures.Add( "Project header placement/assembly mismatch: $([IO.Path]::GetRelativePath( $root, $project.FullName ))" )
	}
	if ( -not $content.Contains( $copyright ) -or -not $content.Contains( $licenseName ) ) {
		$failures.Add( "Project license text mismatch: $([IO.Path]::GetRelativePath( $root, $project.FullName ))" )
	}
	if ( $licenseKind -eq 'GPL' -and $content.Contains( 'GNU Lesser General Public License' ) ) {
		$failures.Add( "GPL project incorrectly contains LGPL text: $([IO.Path]::GetRelativePath( $root, $project.FullName ))" )
	}
}

$sources = @( Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' -File | Where-Object {
	$_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} )

foreach ( $source in $sources ) {
	$content = [IO.File]::ReadAllText( $source.FullName ).Replace( "`r`n", "`n" )
	$projectPath = Find-OwningProject -SourcePath $source.FullName
	$assemblyName = Get-ProjectAssemblyName -ProjectPath $projectPath
	$licenseKind = Get-LicenseKind -ProjectPath $projectPath
	$licenseName = if ( $licenseKind -eq 'LGPL' ) { 'GNU Lesser General Public License' } else { 'GNU General Public License' }

	if ( -not $content.StartsWith( "/*`n`t$assemblyName`n", [StringComparison]::Ordinal ) ) {
		$failures.Add( "C# header placement/assembly mismatch: $([IO.Path]::GetRelativePath( $root, $source.FullName ))" )
	}
	if ( -not $content.Contains( $copyright ) -or -not $content.Contains( $licenseName ) ) {
		$failures.Add( "C# license text mismatch: $([IO.Path]::GetRelativePath( $root, $source.FullName ))" )
	}
	if ( $licenseKind -eq 'GPL' -and $content.Contains( 'GNU Lesser General Public License' ) ) {
		$failures.Add( "GPL C# file incorrectly contains LGPL text: $([IO.Path]::GetRelativePath( $root, $source.FullName ))" )
	}
}

if ( $failures.Count -gt 0 ) {
	$failures | ForEach-Object { Write-Error $_ }
	throw "License header verification failed for $($failures.Count) file(s)."
}

Write-Host "Verified license headers for $($sources.Count) C# files and $($projects.Count) project files."
