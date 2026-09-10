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
namespace Icod.Terminal.Tests.Session;

using Xunit;

/// <summary>
/// Defines the C102 public capability-inspection value contract before implementation.
/// </summary>
public sealed class TerminalCapabilityStatusTests {
	[Fact]
	public void CapabilityVocabularyIsCurated() {
		Assert.Equal(
			[
				TerminalCapability.ClipboardRead,
				TerminalCapability.ClipboardWrite,
				TerminalCapability.CursorStyle,
				TerminalCapability.SynchronizedOutput,
				TerminalCapability.KeyboardReporting,
				TerminalCapability.MouseReporting,
				TerminalCapability.FocusReporting,
				TerminalCapability.BracketedPaste,
				TerminalCapability.RasterGraphics
			],
			Enum.GetValues<TerminalCapability>()
		);
	}

	[Fact]
	public void StatusSeparatesSupportEndpointAndEvidence() {
		TerminalCapabilityStatus status = new(
			TerminalCapability.RasterGraphics,
			TerminalCapabilitySupport.Advertised,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.StaticDescription,
			isUsable: true
		);

		Assert.Equal( TerminalCapability.RasterGraphics, status.Capability );
		Assert.Equal( TerminalCapabilitySupport.Advertised, status.Support );
		Assert.Equal(
			TerminalCapabilityEndpointAvailability.Available,
			status.EndpointAvailability
		);
		Assert.Equal(
			TerminalCapabilityEvidenceKind.StaticDescription,
			status.EvidenceKind
		);
		Assert.True( status.IsUsable );
	}

	[Fact]
	public void EndpointUnavailableCannotBeUsable() {
		Assert.Throws<ArgumentException>(
			() => new TerminalCapabilityStatus(
				TerminalCapability.KeyboardReporting,
				TerminalCapabilitySupport.Verified,
				TerminalCapabilityEndpointAvailability.Unavailable,
				TerminalCapabilityEvidenceKind.LiveObservation,
				isUsable: true
			)
		);
	}

	[Fact]
	public void UnsupportedCapabilityCannotBeUsable() {
		Assert.Throws<ArgumentException>(
			() => new TerminalCapabilityStatus(
				TerminalCapability.RasterGraphics,
				TerminalCapabilitySupport.Unsupported,
				TerminalCapabilityEndpointAvailability.Available,
				TerminalCapabilityEvidenceKind.LiveObservation,
				isUsable: true
			)
		);
	}

	[Theory]
	[InlineData(
		TerminalCapabilitySupport.Advertised,
		TerminalCapabilityEvidenceKind.None
	)]
	[InlineData(
		TerminalCapabilitySupport.Advertised,
		TerminalCapabilityEvidenceKind.LiveObservation
	)]
	[InlineData(
		TerminalCapabilitySupport.Verified,
		TerminalCapabilityEvidenceKind.StaticDescription
	)]
	[InlineData(
		TerminalCapabilitySupport.Unsupported,
		TerminalCapabilityEvidenceKind.StaticDescription
	)]
	public void DecisiveEvidenceKindsRemainConsistent(
		TerminalCapabilitySupport support,
		TerminalCapabilityEvidenceKind evidenceKind
	) {
		Assert.Throws<ArgumentException>(
			() => new TerminalCapabilityStatus(
				TerminalCapability.RasterGraphics,
				support,
				TerminalCapabilityEndpointAvailability.Available,
				evidenceKind,
				isUsable: false
			)
		);
	}

	[Fact]
	public void UnknownMayReflectLiveInconclusiveObservation() {
		TerminalCapabilityStatus status = new(
			TerminalCapability.RasterGraphics,
			TerminalCapabilitySupport.Unknown,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.LiveObservation,
			isUsable: false
		);

		Assert.Equal(
			TerminalCapabilityEvidenceKind.LiveObservation,
			status.EvidenceKind
		);
	}
}
