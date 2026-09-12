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

using System.Reflection;
using Icod.TermInfo.Inspection;
using Xunit;

/// <summary>
/// Defines the T1111-B consumer-owned live-evidence mapping contract.
/// </summary>
public sealed class PersistentRasterLifecycleEvidenceBridgeTests {
	private static readonly PersistentRasterLifecycleEvidenceSubject[] PersistentSubjects = [
		PersistentRasterLifecycleEvidenceSubject.PersistentUpload,
		PersistentRasterLifecycleEvidenceSubject.AcknowledgedUpload,
		PersistentRasterLifecycleEvidenceSubject.PlacementCreation,
		PersistentRasterLifecycleEvidenceSubject.MultiplePlacements,
		PersistentRasterLifecycleEvidenceSubject.PlacementUpdate,
		PersistentRasterLifecycleEvidenceSubject.PlacementDeletion,
		PersistentRasterLifecycleEvidenceSubject.ResourceDeletion
	];

	[Fact]
	public void VerifiedPersistentRasterMapsToSevenPositiveVerifiedAssertions() {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.PersistentRasterGraphics,
			TerminalCapabilitySupport.Verified,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.LiveObservation,
			isUsable: true
		);

		IReadOnlyList<PersistentRasterLifecycleEvidence> evidence =
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				20
			);

		Assert.Equal( PersistentSubjects, evidence.Select( item => item.Subject ) );
		Assert.All( evidence, item => Assert.True( item.IsPositive ) );
		Assert.All(
			evidence,
			item => Assert.Equal(
				PersistentRasterLifecycleEvidenceKind.Verified,
				item.Kind
			)
		);
		Assert.All(
			evidence,
			item => Assert.Equal(
				PersistentRasterLifecycleEvidenceBridge.SourceLabel,
				item.SourceLabel
			)
		);
		Assert.Equal(
			Enumerable.Range( 20, PersistentSubjects.Length ),
			evidence.Select( item => item.SourceOrdinal )
		);
	}

	[Fact]
	public void UnsupportedPersistentRasterMapsToSevenNegativeVerifiedAssertions() {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.PersistentRasterGraphics,
			TerminalCapabilitySupport.Unsupported,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.LiveObservation,
			isUsable: false
		);

		IReadOnlyList<PersistentRasterLifecycleEvidence> evidence =
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				40
			);

		Assert.Equal( PersistentSubjects, evidence.Select( item => item.Subject ) );
		Assert.All( evidence, item => Assert.False( item.IsPositive ) );
		Assert.All(
			evidence,
			item => Assert.Equal(
				PersistentRasterLifecycleEvidenceKind.Verified,
				item.Kind
			)
		);
	}

	[Fact]
	public void VerifiedOrdinaryRasterMapsOnlyRasterDisplay() {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.RasterGraphics,
			TerminalCapabilitySupport.Verified,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.LiveObservation,
			isUsable: true
		);

		PersistentRasterLifecycleEvidence item = Assert.Single(
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				7
			)
		);

		Assert.Equal(
			PersistentRasterLifecycleEvidenceSubject.RasterDisplay,
			item.Subject
		);
		Assert.True( item.IsPositive );
		Assert.Equal( PersistentRasterLifecycleEvidenceKind.Verified, item.Kind );
		Assert.Equal( 7, item.SourceOrdinal );
	}

	[Theory]
	[InlineData( TerminalCapabilitySupport.Unknown, TerminalCapabilityEvidenceKind.None )]
	[InlineData(
		TerminalCapabilitySupport.Advertised,
		TerminalCapabilityEvidenceKind.StaticDescription
	)]
	public void NonLiveSupportKnowledgeDoesNotBecomeVerifiedEvidence(
		TerminalCapabilitySupport support,
		TerminalCapabilityEvidenceKind evidenceKind
	) {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.PersistentRasterGraphics,
			support,
			TerminalCapabilityEndpointAvailability.Available,
			evidenceKind,
			isUsable: false
		);

		Assert.Empty(
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				0
			)
		);
	}

	[Fact]
	public void EndpointUnavailabilityDoesNotBecomeVerifiedNonSupport() {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.PersistentRasterGraphics,
			TerminalCapabilitySupport.Unknown,
			TerminalCapabilityEndpointAvailability.Unavailable,
			TerminalCapabilityEvidenceKind.None,
			isUsable: false
		);

		Assert.Empty(
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				0
			)
		);
	}

	[Fact]
	public void UnrelatedVerifiedCapabilityDoesNotProduceRasterEvidence() {
		TerminalCapabilityStatus status = CreateStatus(
			TerminalCapability.ClipboardWrite,
			TerminalCapabilitySupport.Verified,
			TerminalCapabilityEndpointAvailability.Available,
			TerminalCapabilityEvidenceKind.LiveObservation,
			isUsable: true
		);

		Assert.Empty(
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				0
			)
		);
	}

	private static TerminalCapabilityStatus CreateStatus(
		TerminalCapability capability,
		TerminalCapabilitySupport support,
		TerminalCapabilityEndpointAvailability endpointAvailability,
		TerminalCapabilityEvidenceKind evidenceKind,
		bool isUsable
	) {
		ConstructorInfo constructor = typeof( TerminalCapabilityStatus ).GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			[
				typeof( TerminalCapability ),
				typeof( TerminalCapabilitySupport ),
				typeof( TerminalCapabilityEndpointAvailability ),
				typeof( TerminalCapabilityEvidenceKind ),
				typeof( bool )
			],
			modifiers: null
		) ?? throw new InvalidOperationException(
			"TerminalCapabilityStatus internal constructor was not found."
		);

		return (TerminalCapabilityStatus)constructor.Invoke(
			[
				capability,
				support,
				endpointAvailability,
				evidenceKind,
				isUsable
			]
		);
	}
}
