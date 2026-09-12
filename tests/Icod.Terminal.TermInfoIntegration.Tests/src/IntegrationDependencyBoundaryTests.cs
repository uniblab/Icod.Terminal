/*
	Icod.Terminal.TermInfoIntegration.Tests
	Validation utility for Icod.Terminal release and integration contracts.
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
namespace Icod.Terminal.TermInfoIntegration.Tests;

using System.Xml.Linq;
using Xunit;

/// <summary>
/// Guards the production dependency boundary required by the TermInfo integration contract.
/// </summary>
public sealed class IntegrationDependencyBoundaryTests {
	[Fact]
	public void ProductionProjectKeepsInspectionOutOfRuntimeDependencyGraph() {
		XDocument project = XDocument.Load(
			Path.Combine(
				FindRepositoryRoot(),
				"Icod.Terminal.csproj"
			)
		);

		string[] packageIds = project
			.Descendants( "PackageReference" )
			.Select(
				static element => (string?)element.Attribute( "Include" )
			)
			.Where( static packageId => !string.IsNullOrWhiteSpace( packageId ) )
			.Cast<string>()
			.ToArray();

		Assert.Equal(
			[
				"Icod.TermInfo",
				"Icod.Timing"
			],
			packageIds
		);
		Assert.DoesNotContain( "Icod.TermInfo.Inspection", packageIds );
		Assert.DoesNotContain( "Icod.TermInfo.Source", packageIds );
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
