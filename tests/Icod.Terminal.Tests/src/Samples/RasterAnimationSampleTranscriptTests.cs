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

using Icod.Terminal;
using Icod.Terminal.RasterAnimation.Sample;
using Xunit;

/// <summary>
/// Freezes the public, protocol-neutral raster-animation sample transcript.
/// </summary>
public sealed class RasterAnimationSampleTranscriptTests {
	[Theory]
	[InlineData( "Frame append", TerminalControlMutationConfirmation.ProtocolAcknowledged )]
	[InlineData( "Partial frame replacement", TerminalControlMutationConfirmation.ProtocolAcknowledged )]
	[InlineData( "Frame composition", TerminalControlMutationConfirmation.ProtocolAcknowledged )]
	[InlineData( "Loading-mode playback", TerminalControlMutationConfirmation.OutputCommitted )]
	[InlineData( "Silent frame composition", TerminalControlMutationConfirmation.OutputCommitted )]
	public void SuccessfulMutationPrintsConfirmationWithoutRenderingClaim(
		string operation,
		TerminalControlMutationConfirmation confirmation
	) {
		string transcript = RasterAnimationCompositionExample.FormatMutationOutcome(
			operation,
			TerminalControlMutationResult.Success( confirmation )
		);

		Assert.Contains( $"Operation={operation}", transcript, StringComparison.Ordinal );
		Assert.Contains( "Status=Available", transcript, StringComparison.Ordinal );
		Assert.Contains( $"Confirmation={confirmation}", transcript, StringComparison.Ordinal );
		Assert.Contains( "Fallback=None", transcript, StringComparison.Ordinal );
		Assert.Contains( "Rendered=NotClaimed", transcript, StringComparison.Ordinal );
		Assert.DoesNotContain( "ImageId", transcript, StringComparison.Ordinal );
		Assert.DoesNotContain( "FrameNumber", transcript, StringComparison.Ordinal );
	}

	[Fact]
	public void DefiniteFailureOmitsConfirmationAndPrintsFallbackReason() {
		string transcript = RasterAnimationCompositionExample.FormatMutationOutcome(
			"Frame append",
			TerminalControlMutationResult.Failed( "terminal rejected the frame" )
		);

		Assert.Contains( "Status=Failed", transcript, StringComparison.Ordinal );
		Assert.Contains(
			"Fallback=terminal rejected the frame",
			transcript,
			StringComparison.Ordinal
		);
		Assert.DoesNotContain( "Confirmation=", transcript, StringComparison.Ordinal );
		Assert.Contains( "Rendered=NotClaimed", transcript, StringComparison.Ordinal );
	}

	[Fact]
	public void TimeoutPrintsAmbiguityWithoutInventingConfirmation() {
		string transcript = RasterAnimationCompositionExample.FormatExceptionOutcome(
			"Frame append",
			new TimeoutException( "synthetic timeout" )
		);

		Assert.Contains( "Status=Ambiguous", transcript, StringComparison.Ordinal );
		Assert.Contains( "Fallback=Timeout", transcript, StringComparison.Ordinal );
		Assert.Contains( "automatic retry is unsafe", transcript, StringComparison.Ordinal );
		Assert.DoesNotContain( "Confirmation=", transcript, StringComparison.Ordinal );
		Assert.Contains( "Rendered=NotClaimed", transcript, StringComparison.Ordinal );
	}

	[Theory]
	[InlineData( true, null, "Status=Available", "Cleanup=Completed" )]
	[InlineData( false, "synthetic cleanup failure", "Status=Failed", "Cleanup=Failed" )]
	public void CleanupOutcomeIsExplicit(
		bool succeeded,
		string? reason,
		string expectedStatus,
		string expectedCleanup
	) {
		string transcript = RasterAnimationCompositionExample.FormatCleanupOutcome(
			succeeded,
			reason
		);

		Assert.Contains( "Operation=Resource cleanup", transcript, StringComparison.Ordinal );
		Assert.Contains( expectedStatus, transcript, StringComparison.Ordinal );
		Assert.Contains( expectedCleanup, transcript, StringComparison.Ordinal );
		Assert.DoesNotContain( "Confirmation=", transcript, StringComparison.Ordinal );
		Assert.Contains( "Rendered=NotClaimed", transcript, StringComparison.Ordinal );
	}
}
