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
	MERCHANTIBILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Samples;

using Xunit;

/// <summary>
/// Keeps the stable 1.28 consumer guidance aligned with accepted downstream evidence.
/// </summary>
public sealed class RasterAnimationDocumentationTests {
	private const string DownstreamHead =
		"d9ae518f0ba075f5e264acaeb14de2c9e8bc2c3d";

	[Fact]
	public void StableGuidanceExplainsConfirmationAndTheThreeSampleModes() {
		string root = TerminalCompatibilityEvidenceTests.FindRepositoryRoot();
		string readme = File.ReadAllText( Path.Combine( root, "README.md" ) );
		string sampleIndex = File.ReadAllText(
			Path.Combine( root, "samples", "README.md" )
		);

		Assert.Contains(
			"**Animation mutation confirmation (1.28)**",
			readme,
			StringComparison.Ordinal
		);
		Assert.Contains(
			"switch ( result.Confirmation )",
			readme,
			StringComparison.Ordinal
		);
		Assert.Contains( "Default animation witness", sampleIndex, StringComparison.Ordinal );
		Assert.Contains( "--tile-atlas", sampleIndex, StringComparison.Ordinal );
		Assert.Contains( "--headless-transcript", sampleIndex, StringComparison.Ordinal );
		Assert.Contains( DownstreamHead, sampleIndex, StringComparison.Ordinal );
	}

	[Fact]
	public void AcceptedDownstreamEvidenceIsScopedConsistently() {
		string root = TerminalCompatibilityEvidenceTests.FindRepositoryRoot();
		string sampleWalkthrough = File.ReadAllText(
			Path.Combine(
				root,
				"samples",
				"Icod.Terminal.RasterAnimation.Sample",
				"README.md"
			)
		);
		string compatibility = File.ReadAllText(
			Path.Combine(
				root,
				"docs",
				"Kitty-Graphics-Transaction-Compatibility-1.28.md"
			)
		);
		string hold = File.ReadAllText(
			Path.Combine( root, "docs", "Graphics-Development-Hold.md" )
		);

		foreach ( string document in new[] {
			sampleWalkthrough,
			compatibility,
			hold
		} ) {
			Assert.Contains( DownstreamHead, document, StringComparison.Ordinal );
			Assert.Contains(
				"help, resize, and independent exit observations remain `NotRun`",
				document,
				StringComparison.Ordinal
			);
			Assert.DoesNotContain(
				"full DCurses ATLAS workload remain",
				document,
				StringComparison.OrdinalIgnoreCase
			);
		}
		Assert.DoesNotContain(
			"Full downstream ATLAS and compose-publish acceptance remain open",
			compatibility,
			StringComparison.Ordinal
		);
	}
}
