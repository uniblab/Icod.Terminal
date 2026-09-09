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
namespace Icod.Terminal.Tests.Routing;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies N155 capability support/evidence composition and lifecycle invalidation.
/// </summary>
public sealed class TerminalCapabilityEvidenceLedgerTests {
	[Fact]
	public void EndpointUnavailabilityOverridesAllCapabilityEvidence() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityResolution resolution = ledger.Resolve(
			subject,
			endpointAvailable: false
		);

		Assert.Equal( TerminalCapabilitySupportState.Unavailable, resolution.State );
		Assert.Null( resolution.EvidenceSource );
	}

	[Fact]
	public void TermInfoAdvertisementPrecedesBuiltInProfileAdvertisement() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = OperationSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Advertised, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.TermInfo,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void VerifiedLiveEvidenceOverridesStaticAdvertisement() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Verified, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.LiveProbe,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void ExplicitNegativeLiveEvidenceOverridesAdvertisement() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Unsupported, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void LaterDecisiveLiveEvidenceSupersedesEarlierLiveEvidence() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.LiveProbe
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Verified, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void UnknownLiveObservationDoesNotEraseDecisiveLiveEvidence() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Unknown,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Verified, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.LiveProbe,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void UnknownLiveObservationDoesNotOverrideStaticAdvertisement() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = OperationSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Unknown,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Advertised, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.TermInfo,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void UnknownLiveObservationIsRetainedWhenNoStrongerEvidenceExists() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Unknown,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.ProtocolResponse,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void AdvancingLiveGenerationInvalidatesLiveEvidenceButPreservesStaticEvidence() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);
		ledger.Record(
			subject,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		long before = ledger.LiveGeneration;

		ledger.AdvanceLiveGeneration();
		TerminalCapabilityResolution resolution = ledger.Resolve( subject );

		Assert.Equal( checked( before + 1 ), ledger.LiveGeneration );
		Assert.Equal( TerminalCapabilitySupportState.Advertised, resolution.State );
		Assert.Equal(
			TerminalCapabilityEvidenceSource.BuiltInProfile,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void EvidenceRemainsIsolatedPerSubject() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject notification = TerminalCapabilitySubject.ForSemanticOperation(
			TerminalSemanticOperation.DesktopNotification
		);
		TerminalCapabilitySubject graphics = TerminalCapabilitySubject.ForSemanticOperation(
			TerminalSemanticOperation.RasterGraphics
		);
		ledger.Record(
			notification,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		Assert.Equal(
			TerminalCapabilitySupportState.Verified,
			ledger.Resolve( notification ).State
		);
		Assert.Equal(
			TerminalCapabilitySupportState.Unknown,
			ledger.Resolve( graphics ).State
		);
	}

	[Fact]
	public void InvalidEvidenceSourceStateCombinationsAreRejected() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = BackendSubject();

		Assert.Throws<ArgumentException>(
			() => ledger.Record(
				subject,
				TerminalCapabilitySupportState.Unavailable,
				TerminalCapabilityEvidenceSource.ProtocolResponse
			)
		);
		Assert.Throws<ArgumentException>(
			() => ledger.Record(
				subject,
				TerminalCapabilitySupportState.Verified,
				TerminalCapabilityEvidenceSource.TermInfo
			)
		);
		Assert.Throws<ArgumentException>(
			() => ledger.Record(
				subject,
				TerminalCapabilitySupportState.Advertised,
				TerminalCapabilityEvidenceSource.LiveProbe
			)
		);
	}

	[Fact]
	public void DefaultCapabilitySubjectCannotEnterLedger() {
		TerminalCapabilityEvidenceLedger ledger = new();
		TerminalCapabilitySubject subject = default;

		Assert.Throws<ArgumentException>(
			() => ledger.Record(
				subject,
				TerminalCapabilitySupportState.Unknown,
				TerminalCapabilityEvidenceSource.LiveProbe
			)
		);
	}

	private static TerminalCapabilitySubject BackendSubject() {
		return TerminalCapabilitySubject.ForProtocolBackend(
			TerminalProtocolBackend.ApcKittyGraphics
		);
	}

	private static TerminalCapabilitySubject OperationSubject() {
		return TerminalCapabilitySubject.ForSemanticOperation(
			TerminalSemanticOperation.RasterGraphics
		);
	}
}
