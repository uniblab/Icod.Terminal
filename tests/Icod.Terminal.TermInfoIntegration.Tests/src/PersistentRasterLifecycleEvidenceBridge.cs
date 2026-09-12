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

using Icod.TermInfo.Inspection;

/// <summary>
/// Translates protocol-neutral Terminal live capability results into caller-owned
/// TermInfo lifecycle evidence without exposing backend identity.
/// </summary>
internal static class PersistentRasterLifecycleEvidenceBridge {
	internal const string SourceLabel = "Icod.Terminal.live-capability-verification";

	private static readonly PersistentRasterLifecycleEvidenceSubject[] PersistentSubjects = [
		PersistentRasterLifecycleEvidenceSubject.PersistentUpload,
		PersistentRasterLifecycleEvidenceSubject.AcknowledgedUpload,
		PersistentRasterLifecycleEvidenceSubject.PlacementCreation,
		PersistentRasterLifecycleEvidenceSubject.MultiplePlacements,
		PersistentRasterLifecycleEvidenceSubject.PlacementUpdate,
		PersistentRasterLifecycleEvidenceSubject.PlacementDeletion,
		PersistentRasterLifecycleEvidenceSubject.ResourceDeletion
	];

	internal static IReadOnlyList<PersistentRasterLifecycleEvidence> CreateEvidence(
		TerminalCapabilityStatus status,
		int sourceOrdinal
	) {
		if ( 0 > sourceOrdinal ) {
			throw new ArgumentOutOfRangeException( nameof( sourceOrdinal ) );
		}

		if ( TerminalCapabilityEvidenceKind.LiveObservation != status.EvidenceKind ) {
			return Array.Empty<PersistentRasterLifecycleEvidence>();
		}

		bool? isPositive = status.Support switch {
			TerminalCapabilitySupport.Verified => true,
			TerminalCapabilitySupport.Unsupported => false,
			_ => null
		};
		if ( !isPositive.HasValue ) {
			return Array.Empty<PersistentRasterLifecycleEvidence>();
		}

		if ( TerminalCapability.RasterGraphics == status.Capability ) {
			return [
				CreateEvidence(
					PersistentRasterLifecycleEvidenceSubject.RasterDisplay,
					isPositive.Value,
					sourceOrdinal
				)
			];
		}
		if ( TerminalCapability.PersistentRasterGraphics != status.Capability ) {
			return Array.Empty<PersistentRasterLifecycleEvidence>();
		}
		if ( sourceOrdinal > int.MaxValue - ( PersistentSubjects.Length - 1 ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( sourceOrdinal ),
				sourceOrdinal,
				"The source ordinal does not leave room for all persistent-raster lifecycle assertions."
			);
		}

		PersistentRasterLifecycleEvidence[] result =
			new PersistentRasterLifecycleEvidence[ PersistentSubjects.Length ];
		for ( int index = 0; index < PersistentSubjects.Length; index++ ) {
			result[ index ] = CreateEvidence(
				PersistentSubjects[ index ],
				isPositive.Value,
				sourceOrdinal + index
			);
		}
		return result;
	}

	private static PersistentRasterLifecycleEvidence CreateEvidence(
		PersistentRasterLifecycleEvidenceSubject subject,
		bool isPositive,
		int sourceOrdinal
	) {
		return new PersistentRasterLifecycleEvidence(
			subject,
			isPositive,
			PersistentRasterLifecycleEvidenceKind.Verified,
			SourceLabel,
			sourceOrdinal
		);
	}
}
