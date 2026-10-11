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

using System.Xml.Linq;
using Xunit;

/// <summary>
/// Keeps the distribution workflow aligned with the reviewed development identity.
/// </summary>
public sealed class StableReleaseIdentityTests {
	[Fact]
	public void MainDistributionPinsOneTwentyNineStableIdentity() {
		string root = FindRepositoryRoot();
		XDocument properties = XDocument.Load(
			Path.Combine( root, "Directory.Build.props" )
		);
		string versionPrefix = properties
			.Descendants()
			.Single( element => "VersionPrefix" == element.Name.LocalName )
			.Value
			.Trim();
		XElement? versionSuffix = properties
			.Descendants()
			.SingleOrDefault( element => "VersionSuffix" == element.Name.LocalName );
		string workflow = File.ReadAllText(
			Path.Combine( root, ".github", "workflows", "main.yaml" )
		);
		string readme = File.ReadAllText(
			Path.Combine( root, "README.md" )
		);
		string changelog = File.ReadAllText(
			Path.Combine( root, "CHANGELOG.md" )
		);
		string releaseNotes = File.ReadAllText(
			Path.Combine( root, "docs", "releases", "1.29.0.md" )
		);

		Assert.Equal( "1.29.0", versionPrefix );
		Assert.True(
			null == versionSuffix
			|| String.IsNullOrWhiteSpace( versionSuffix.Value )
		);
		Assert.Contains(
			"EXPECTED_VERSION: 1.29.0",
			workflow,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"Stable release: **1.29.0**.",
			readme,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"dotnet add package Icod.Terminal --version 1.29.0",
			readme,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"https://github.com/uniblab/Icod.Terminal/blob/main/docs/releases/1.29.0.md",
			readme,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"## 1.29.0",
			changelog,
			StringComparison.Ordinal
		);
		Assert.True(
			File.Exists(
				Path.Combine( root, "docs", "releases", "1.29.0.md" )
			)
		);
		foreach ( string requiredReleaseNoteText in new[] {
			"net8.0",
			"net9.0",
			"net10.0",
			"Compatibility-and-Versioning.md"
		} ) {
			Assert.Contains(
				requiredReleaseNoteText,
				releaseNotes,
				StringComparison.Ordinal
			);
		}
		Assert.Equal(
			2,
			CountOccurrences(
				workflow,
				"-ExpectedVersion '${{ env.EXPECTED_VERSION }}'"
			)
		);
	}

	private static int CountOccurrences(
		string source,
		string value
	) {
		ArgumentNullException.ThrowIfNull( source );
		ArgumentException.ThrowIfNullOrWhiteSpace( value );

		int count = 0;
		int offset = 0;
		while ( true ) {
			int index = source.IndexOf(
				value,
				offset,
				StringComparison.Ordinal
			);
			if ( 0 > index ) {
				return count;
			}
			++count;
			offset = index + value.Length;
		}
	}

	private static string FindRepositoryRoot() {
		DirectoryInfo? current = new( AppContext.BaseDirectory );
		while ( current is not null ) {
			if (
				File.Exists(
					Path.Combine(
						current.FullName,
						"Icod.Terminal.csproj"
					)
				)
			) {
				return current.FullName;
			}
			current = current.Parent;
		}
		throw new InvalidOperationException(
			"Unable to locate the Icod.Terminal repository root."
		);
	}
}
