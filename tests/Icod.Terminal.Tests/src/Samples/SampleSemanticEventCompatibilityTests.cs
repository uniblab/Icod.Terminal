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
namespace Icod.Terminal.Tests.Samples;

using Xunit;

/// <summary>
/// Verifies that event-oriented samples model the additive 1.x event contract safely.
/// </summary>
public sealed class SampleSemanticEventCompatibilityTests {
	[Theory]
	[InlineData( "samples/Icod.Terminal.RichInput.Sample/Program.cs" )]
	[InlineData( "samples/Icod.Terminal.Query.Sample/Program.cs" )]
	public void EventInspectorSamplesHandleSemanticAndFutureKinds(
		string relativePath
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( relativePath );
		string source = File.ReadAllText(
			Path.Combine(
				FindRepositoryRoot(),
				relativePath.Replace( '/', Path.DirectorySeparatorChar )
			)
		);

		Assert.Contains( "case TerminalEventKind.Semantic:", source );
		Assert.DoesNotContain( "Unexpected terminal event kind", source );
	}

	[Fact]
	public void ColorSampleDescribesUnifiedTerminalEvents() {
		string source = File.ReadAllText(
			Path.Combine(
				FindRepositoryRoot(),
				"samples",
				"Icod.Terminal.Color.Sample",
				"Program.cs"
			)
		);

		Assert.DoesNotContain( "Generate one terminal input event", source );
		Assert.DoesNotContain( "No input event arrived", source );
		Assert.Contains( "Generate one terminal event", source );
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
