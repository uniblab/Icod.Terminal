/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Packaging;

using Xunit;

/// <summary>
/// Guards the repository policy that active package verification validates dependency
/// shape without duplicating exact dependency-version requirements.
/// </summary>
public sealed class DependencyCouplingPolicyTests {
	[Fact]
	public void PackageVerifierDoesNotPinDependencyVersions() {
		string source = File.ReadAllText(
			Path.Combine(
				FindRepositoryRoot(),
				"tools",
				"package-verifier",
				"Program.cs"
			)
		);

		Assert.DoesNotContain( "TermInfoDependencyVersion", source );
		Assert.DoesNotContain( "TimingDependencyVersion", source );
		Assert.DoesNotContain( "references an unexpected {packageId} version", source );
	}

	[Fact]
	public void AuxiliaryProjectsDoNotPinTerminalRuntimeDependencies() {
		string root = FindRepositoryRoot();
		string[] projectRoots = [
			Path.Combine( root, "tests" ),
			Path.Combine( root, "samples" ),
			Path.Combine( root, "tools" )
		];

		foreach ( string projectRoot in projectRoots ) {
			foreach ( string projectPath in Directory.EnumerateFiles(
				projectRoot,
				"*.csproj",
				SearchOption.AllDirectories
			) ) {
				string source = File.ReadAllText( projectPath );
				Assert.DoesNotContain(
					"PackageReference Include=\"Icod.TermInfo\"",
					source
				);
				Assert.DoesNotContain(
					"PackageReference Include=\"Icod.Timing\"",
					source
				);
			}
		}
	}

	private static string FindRepositoryRoot() {
		DirectoryInfo? directory = new( AppContext.BaseDirectory );
		while ( directory is not null ) {
			if ( File.Exists(
				Path.Combine(
					directory.FullName,
					"Icod.Terminal.csproj"
				)
			) ) {
				return directory.FullName;
			}
			directory = directory.Parent;
		}

		throw new InvalidOperationException(
			"Could not locate the Icod.Terminal repository root from the test output directory."
		);
	}
}
