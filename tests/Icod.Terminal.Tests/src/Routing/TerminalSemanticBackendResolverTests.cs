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
/// Verifies N157 routing precedence independently from registry declaration order.
/// </summary>
public sealed class TerminalSemanticBackendResolverTests {
	[Fact]
	public void EndpointUnavailabilityPreventsBackendSelection() {
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DesktopNotification,
			new TerminalCapabilityEvidenceLedger(),
			endpointAvailable: false
		);

		Assert.Null( resolution.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unavailable, resolution.State );
		Assert.Equal( TerminalBackendSelectionReason.None, resolution.SelectionReason );
	}

	[Fact]
	public void VerifiedBackendOutranksRegistryDeclarationOrder() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc9Notification,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc99KittyNotification,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DesktopNotification,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.Osc99KittyNotification,
			TerminalCapabilitySupportState.Verified,
			TerminalBackendSelectionReason.Verified
		);
	}

	[Fact]
	public void AnyVerifiedWireBackendOutranksStaticTermInfoAdvertisement() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordTermInfoOperation(
			evidence,
			TerminalSemanticOperation.ClipboardWrite
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc52Clipboard,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.ClipboardWrite,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.Osc52Clipboard,
			TerminalCapabilitySupportState.Verified,
			TerminalBackendSelectionReason.Verified
		);
	}

	[Fact]
	public void ExactTermInfoAdvertisementOutranksAdvertisedWireBackend() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordTermInfoOperation(
			evidence,
			TerminalSemanticOperation.CursorStyle
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.CsiDecscusrCursorStyle,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.CursorStyle,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.TermInfoCapability,
			TerminalCapabilitySupportState.Advertised,
			TerminalBackendSelectionReason.TermInfo
		);
		Assert.Equal(
			TerminalCapabilityEvidenceSource.TermInfo,
			resolution.EvidenceSource
		);
	}

	[Fact]
	public void AdvertisedBackendUsesExplicitN157Preference() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc9Notification,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc777TitledNotification,
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.BuiltInProfile
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DesktopNotification,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.Osc777TitledNotification,
			TerminalCapabilitySupportState.Advertised,
			TerminalBackendSelectionReason.Advertised
		);
	}

	[Fact]
	public void UnsupportedPreferredBackendFallsThroughToAnotherCandidate() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc99KittyNotification,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.Osc777TitledNotification,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DesktopNotification,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.Osc777TitledNotification,
			TerminalCapabilitySupportState.Verified,
			TerminalBackendSelectionReason.Verified
		);
	}

	[Fact]
	public void UnknownStateUsesOnlyReviewedSafeFallbacks() {
		TerminalCapabilityEvidenceLedger evidence = new();

		TerminalSemanticBackendResolution notification = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.DesktopNotification,
			evidence
		);
		TerminalSemanticBackendResolution graphics = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence
		);

		AssertBackend(
			notification,
			TerminalProtocolBackend.Osc9Notification,
			TerminalCapabilitySupportState.Unknown,
			TerminalBackendSelectionReason.SafeFallback
		);
		Assert.Null( graphics.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, graphics.State );
		Assert.Equal( TerminalBackendSelectionReason.None, graphics.SelectionReason );
	}

	[Fact]
	public void PortableCurrentLocationIsTheUnknownSafeFallback() {
		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.CurrentLocation,
			new TerminalCapabilityEvidenceLedger()
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.Osc7CurrentLocation,
			TerminalCapabilitySupportState.Unknown,
			TerminalBackendSelectionReason.SafeFallback
		);
	}

	[Fact]
	public void GraphicsPrefersVerifiedKittyOverVerifiedSixel() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence
		);

		AssertBackend(
			resolution,
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalBackendSelectionReason.Verified
		);
	}

	[Fact]
	public void ExplicitBackendMaySelectUnknownCandidateButNotUnsupportedCandidate() {
		TerminalCapabilityEvidenceLedger evidence = new();
		TerminalSemanticBackendResolution unknown = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence,
			explicitBackend: TerminalProtocolBackend.DcsSixel
		);
		AssertBackend(
			unknown,
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Unknown,
			TerminalBackendSelectionReason.Explicit
		);

		RecordBackend(
			evidence,
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		TerminalSemanticBackendResolution unsupported = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence,
			explicitBackend: TerminalProtocolBackend.DcsSixel
		);

		Assert.Null( unsupported.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unsupported, unsupported.State );
		Assert.Equal( TerminalBackendSelectionReason.None, unsupported.SelectionReason );
	}

	[Fact]
	public void ExplicitBackendMustBelongToSemanticOperation() {
		Assert.Throws<ArgumentException>(
			() => TerminalSemanticBackendResolver.Resolve(
				TerminalSemanticOperation.RasterGraphics,
				new TerminalCapabilityEvidenceLedger(),
				explicitBackend: TerminalProtocolBackend.Osc99KittyNotification
			)
		);
	}

	[Fact]
	public void AllExplicitlyUnsupportedCandidatesResolveOperationAsUnsupported() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.DcsSixel,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		RecordBackend(
			evidence,
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence
		);

		Assert.Null( resolution.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unsupported, resolution.State );
	}

	[Fact]
	public void OneUnsupportedAndOneUnknownWithoutFallbackRemainUnknown() {
		TerminalCapabilityEvidenceLedger evidence = new();
		RecordBackend(
			evidence,
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.RasterGraphics,
			evidence
		);

		Assert.Null( resolution.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
	}

	[Fact]
	public void SemanticLiveEvidenceDoesNotMasqueradeAsTermInfoRecipeEvidence() {
		TerminalCapabilityEvidenceLedger evidence = new();
		evidence.Record(
			TerminalCapabilitySubject.ForSemanticOperation(
				TerminalSemanticOperation.ClipboardWrite
			),
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.LiveProbe
		);

		TerminalSemanticBackendResolution resolution = TerminalSemanticBackendResolver.Resolve(
			TerminalSemanticOperation.ClipboardWrite,
			evidence
		);

		Assert.Null( resolution.SelectedCandidate );
		Assert.Equal( TerminalCapabilitySupportState.Unknown, resolution.State );
	}

	private static void RecordBackend(
		TerminalCapabilityEvidenceLedger evidence,
		TerminalProtocolBackend backend,
		TerminalCapabilitySupportState state,
		TerminalCapabilityEvidenceSource source
	) {
		ArgumentNullException.ThrowIfNull( evidence );
		evidence.Record(
			TerminalCapabilitySubject.ForProtocolBackend( backend ),
			state,
			source
		);
	}

	private static void RecordTermInfoOperation(
		TerminalCapabilityEvidenceLedger evidence,
		TerminalSemanticOperation operation
	) {
		ArgumentNullException.ThrowIfNull( evidence );
		evidence.Record(
			TerminalCapabilitySubject.ForSemanticOperation( operation ),
			TerminalCapabilitySupportState.Advertised,
			TerminalCapabilityEvidenceSource.TermInfo
		);
	}

	private static void AssertBackend(
		TerminalSemanticBackendResolution resolution,
		TerminalProtocolBackend backend,
		TerminalCapabilitySupportState state,
		TerminalBackendSelectionReason reason
	) {
		Assert.NotNull( resolution.SelectedCandidate );
		Assert.Equal( backend, resolution.SelectedCandidate.Value.Backend );
		Assert.Equal( state, resolution.State );
		Assert.Equal( reason, resolution.SelectionReason );
	}
}
