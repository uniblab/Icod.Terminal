param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-RepositoryRelativePath {
	param( [Parameter( Mandatory = $true )][string] $Path )

	$rootPath = [IO.Path]::GetFullPath( $root )
	$fullPath = [IO.Path]::GetFullPath( $Path )

	if ( $fullPath.Equals( $rootPath, [StringComparison]::OrdinalIgnoreCase ) ) {
		return '.'
	}

	$separator = [IO.Path]::DirectorySeparatorChar.ToString()
	$rootPrefix = $rootPath
	if ( -not $rootPrefix.EndsWith( $separator, [StringComparison]::Ordinal ) ) {
		$rootPrefix += $separator
	}

	if ( -not $fullPath.StartsWith( $rootPrefix, [StringComparison]::OrdinalIgnoreCase ) ) {
		throw "Path '$fullPath' is not within repository root '$rootPath'."
	}

	return $fullPath.Substring( $rootPrefix.Length ).Replace( '\', '/' )
}

function Get-ProjectAssemblyName {
	param( [Parameter( Mandatory = $true )][string] $ProjectPath )

	[xml] $projectXml = Get-Content -LiteralPath $ProjectPath -Raw
	$assemblyNameNode = $projectXml.SelectSingleNode( '/Project/PropertyGroup/AssemblyName' )
	if ( $null -ne $assemblyNameNode -and -not [string]::IsNullOrWhiteSpace( [string] $assemblyNameNode.InnerText ) ) {
		return [string] $assemblyNameNode.InnerText
	}

	return [IO.Path]::GetFileNameWithoutExtension( $ProjectPath )
}

function Get-ProjectDescription {
	param( [Parameter( Mandatory = $true )][string] $AssemblyName )

	if ( $AssemblyName -eq 'Icod.Terminal' ) {
		return 'Managed, cross-platform live-terminal session and terminal-control library for .NET.'
	}
	if ( $AssemblyName -eq 'Icod.Terminal.Tests' ) {
		return 'Automated test suite for the Icod.Terminal library.'
	}
	if ( $AssemblyName -eq 'Icod.Terminal.Sample' ) {
		return 'Basic sample application demonstrating Icod.Terminal session usage.'
	}
	if ( $AssemblyName -like '*.Sample' ) {
		$topic = $AssemblyName.Replace( 'Icod.Terminal.', '' ).Replace( '.Sample', '' )
		return "Sample application demonstrating Icod.Terminal $topic features."
	}
	if ( $AssemblyName -like '*.DCurses*Acceptance' -or $AssemblyName -like '*.DCurses*Soak' ) {
		return 'Downstream Icod.DCurses acceptance utility for Icod.Terminal integration contracts.'
	}
	if ( $AssemblyName -like '*.Package*Smoke' ) {
		return 'Package smoke-test utility for Icod.Terminal release and compatibility contracts.'
	}
	if ( $AssemblyName -eq 'Icod.Terminal.PackageVerifier' ) {
		return 'Package verification utility for Icod.Terminal release artifacts.'
	}
	if ( $AssemblyName -eq 'Icod.Terminal.PublicApiSnapshot' ) {
		return 'Public API snapshot generator for the Icod.Terminal library.'
	}

	return 'Validation utility for Icod.Terminal release and integration contracts.'
}

function Get-LicenseKind {
	param( [Parameter( Mandatory = $true )][string] $ProjectPath )

	$relative = Get-RepositoryRelativePath -Path $ProjectPath
	if ( $relative -eq 'Icod.Terminal.csproj' ) {
		return 'LGPL'
	}

	return 'GPL'
}

function Get-CSharpHeader {
	param(
		[Parameter( Mandatory = $true )][string] $AssemblyName,
		[Parameter( Mandatory = $true )][string] $Description,
		[Parameter( Mandatory = $true )][ValidateSet( 'LGPL', 'GPL' )][string] $LicenseKind
	)

	$licenseName = if ( $LicenseKind -eq 'LGPL' ) { 'GNU Lesser General Public License' } else { 'GNU General Public License' }

	return @"
/*
`t$AssemblyName
`t$Description
`tCopyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
`tThis program is free software: you can redistribute it and/or modify
`tit under the terms of the $licenseName as published by
`tthe Free Software Foundation, either version 3 of the License, or
`t(at your option) any later version.

`tThis program is distributed in the hope that it will be useful,
`tbut WITHOUT ANY WARRANTY; without even the implied warranty of
`tMERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
`t$licenseName for more details.

`tYou should have received a copy of the $licenseName
`talong with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
"@
}

function Get-ProjectHeader {
	param(
		[Parameter( Mandatory = $true )][string] $AssemblyName,
		[Parameter( Mandatory = $true )][string] $Description,
		[Parameter( Mandatory = $true )][ValidateSet( 'LGPL', 'GPL' )][string] $LicenseKind
	)

	$licenseName = if ( $LicenseKind -eq 'LGPL' ) { 'GNU Lesser General Public License' } else { 'GNU General Public License' }

	return @"
<?xml version="1.0" encoding="utf-8"?>
<!--
`t$AssemblyName
`t$Description
`tCopyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
-->

<!--
`tThis program is free software: you can redistribute it and/or modify
`tit under the terms of the $licenseName as published by
`tthe Free Software Foundation, either version 3 of the License, or
`t(at your option) any later version.

`tThis program is distributed in the hope that it will be useful,
`tbut WITHOUT ANY WARRANTY; without even the implied warranty of
`tMERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
`t$licenseName for more details.

`tYou should have received a copy of the $licenseName
`talong with this program.  If not, see <https://www.gnu.org/licenses/>.
-->
"@
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

$failures = [Collections.Generic.List[string]]::new()

$projects = @( Get-ChildItem -LiteralPath $root -Recurse -Filter '*.csproj' -File | Where-Object {
	$_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} )

foreach ( $project in $projects ) {
	$content = [IO.File]::ReadAllText( $project.FullName ).Replace( "`r`n", "`n" )
	$assemblyName = Get-ProjectAssemblyName -ProjectPath $project.FullName
	$description = Get-ProjectDescription -AssemblyName $assemblyName
	$licenseKind = Get-LicenseKind -ProjectPath $project.FullName
	$expectedHeader = Get-ProjectHeader -AssemblyName $assemblyName -Description $description -LicenseKind $licenseKind

	if ( -not $content.StartsWith( $expectedHeader, [StringComparison]::Ordinal ) ) {
		$relativePath = Get-RepositoryRelativePath -Path $project.FullName
		$failures.Add( "Project header mismatch: $relativePath" )
	}
}

$sources = @( Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' -File | Where-Object {
	$_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} )

foreach ( $source in $sources ) {
	$content = [IO.File]::ReadAllText( $source.FullName ).Replace( "`r`n", "`n" )
	$projectPath = Find-OwningProject -SourcePath $source.FullName
	$assemblyName = Get-ProjectAssemblyName -ProjectPath $projectPath
	$description = Get-ProjectDescription -AssemblyName $assemblyName
	$licenseKind = Get-LicenseKind -ProjectPath $projectPath
	$expectedHeader = Get-CSharpHeader -AssemblyName $assemblyName -Description $description -LicenseKind $licenseKind

	if ( -not $content.StartsWith( $expectedHeader, [StringComparison]::Ordinal ) ) {
		$relativePath = Get-RepositoryRelativePath -Path $source.FullName
		$failures.Add( "C# header mismatch: $relativePath" )
	}
}

if ( $failures.Count -gt 0 ) {
	$failures | ForEach-Object { Write-Error $_ }
	throw "License header verification failed for $($failures.Count) file(s)."
}

Write-Host "Verified exact license headers for $($sources.Count) C# files and $($projects.Count) project files."
