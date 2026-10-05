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

using Icod.Terminal.Compatibility.Sample;
using Xunit;

/// <summary>Freezes deterministic compatibility-matrix generation.</summary>
public sealed class TerminalCompatibilityMatrixTests {
	[Fact]
	public void EmptyEvidenceKeepsEveryLaneExplicitlyNotRun() {
		string matrix = CompatibilityMatrixRenderer.Render(
			Array.Empty<CompatibilityEvidence>(),
			"1.26.0"
		);

		Assert.Equal( 11, matrix.Split( "| `NotRun` |", StringSplitOptions.None ).Length - 1 );
		Assert.Contains( "| Windows Terminal | `NotRun` |", matrix, StringComparison.Ordinal );
		Assert.Contains( "| VS Code | `NotRun` |", matrix, StringComparison.Ordinal );
		Assert.Contains( "No reviewed live results are recorded.", matrix, StringComparison.Ordinal );
	}

	[Fact]
	public void RenderingIsDeterministicAndMatchesFixture() {
		CompatibilityEvidence kitty = TerminalCompatibilityEvidenceTests.Valid();
		CompatibilityEvidence apple = kitty with {
			TerminalId = "apple-terminal",
			TerminalVersion = "2.14",
			OperatingSystem = "macOS",
			OperatingSystemVersion = "15.6",
			Transport = null,
			TransportVersion = null,
			ScenarioId = "query.dimensions",
			Outcome = CompatibilityOutcome.NotRun,
			AutomatedObservation = null,
			Note = "No reviewed live result exists."
		};
		string expected = File.ReadAllText(
			Path.Combine(
				TerminalCompatibilityEvidenceTests.FindRepositoryRoot(),
				"samples",
				"Icod.Terminal.Compatibility.Sample",
				"fixtures",
				"expected-matrix.md"
			)
		).Replace( "\r\n", "\n", StringComparison.Ordinal );

		string forward = CompatibilityMatrixRenderer.Render(
			new[] { kitty, apple },
			"1.26.0"
		);
		string reverse = CompatibilityMatrixRenderer.Render(
			new[] { apple, kitty },
			"1.26.0"
		);

		Assert.Equal( expected, forward );
		Assert.Equal( forward, reverse );
		Assert.Contains( "`NotRun`", forward, StringComparison.Ordinal );
		Assert.Contains( "## Outcome legend", forward, StringComparison.Ordinal );
		Assert.Contains( "WSL 2", forward, StringComparison.Ordinal );
	}

	[Fact]
	public void DuplicateEnvironmentAndScenarioAreRejected() {
		CompatibilityEvidence evidence = TerminalCompatibilityEvidenceTests.Valid();

		Assert.Throws<FormatException>(
			() => CompatibilityMatrixRenderer.Render(
				new[] { evidence, evidence },
				"1.26.0"
			)
		);
	}
}
