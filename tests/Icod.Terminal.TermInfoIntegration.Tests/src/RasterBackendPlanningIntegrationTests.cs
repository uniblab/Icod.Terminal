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
using Xunit;

/// <summary>
/// Qualifies the additive Icod.TermInfo.Inspection 1.14 raster-backend planning
/// layer without moving backend-selection policy into Icod.Terminal production code.
/// </summary>
public sealed class RasterBackendPlanningIntegrationTests {
	[Fact]
	public void Inspection114RequiresExplicitPreferenceWhenBothBackendsAreViable() {
		PersistentRasterLifecycleProfile lifecycleProfile =
			CreateSatisfiedLifecycleProfile();
		PersistentRasterPlacementProfile placementProfile =
			CreateSatisfiedPlacementProfile();
		RasterBackendCandidate[] candidates = [
			new RasterBackendCandidate(
				CreateSupportedBackendProfile( RasterBackendKind.Sixel ),
				lifecycleProfile,
				placementProfile
			),
			new RasterBackendCandidate(
				CreateSupportedBackendProfile( RasterBackendKind.KittyGraphics ),
				lifecycleProfile,
				placementProfile
			)
		];
		RasterBackendSelectionRequest request = CreateSelectionRequest();

		RasterBackendSelectionPlan noPreference = RasterBackendPlanner.Plan(
			candidates,
			request
		);
		Assert.Equal(
			RasterBackendSelectionStatus.RequiresPreference,
			noPreference.Status
		);
		Assert.Null( noPreference.SelectedBackend );

		RasterBackendSelectionPlan preferred = RasterBackendPlanner.Plan(
			candidates,
			request,
			new RasterBackendSelectionOptions(
				[
					RasterBackendKind.KittyGraphics,
					RasterBackendKind.Sixel
				]
			)
		);

		Assert.Equal(
			RasterBackendSelectionStatus.Selected,
			preferred.Status
		);
		Assert.Equal(
			RasterBackendKind.KittyGraphics,
			preferred.SelectedBackend
		);
	}

	[Fact]
	public void Inspection114KeepsBackendAvailabilitySeparateFromSemanticTruth() {
		RasterBackendProfile backendProfile = CreateSupportedBackendProfile(
			RasterBackendKind.KittyGraphics
		);
		PersistentRasterLifecycleProfile unknownLifecycle =
			PersistentRasterLifecycleClassifier.Classify(
				Array.Empty<PersistentRasterLifecycleEvidence>()
			);
		PersistentRasterPlacementProfile unknownPlacement =
			PersistentRasterPlacementClassifier.Classify(
				Array.Empty<PersistentRasterPlacementEvidence>()
			);
		RasterBackendSelectionRequest request = CreateSelectionRequest();

		RasterBackendSelectionPlan lifecycleUnknown = RasterBackendPlanner.Plan(
			[
				new RasterBackendCandidate(
					backendProfile,
					unknownLifecycle,
					unknownPlacement
				)
			],
			request
		);
		Assert.Equal(
			RasterBackendSelectionStatus.RequiresRuntimeVerification,
			lifecycleUnknown.Status
		);
		Assert.Null( lifecycleUnknown.SelectedBackend );

		RasterBackendSelectionPlan placementUnknown = RasterBackendPlanner.Plan(
			[
				new RasterBackendCandidate(
					backendProfile,
					CreateSatisfiedLifecycleProfile(),
					unknownPlacement
				)
			],
			request
		);
		Assert.Equal(
			RasterBackendSelectionStatus.RequiresRuntimeVerification,
			placementUnknown.Status
		);
		Assert.Null( placementUnknown.SelectedBackend );
	}

	private static RasterBackendSelectionRequest CreateSelectionRequest() {
		return new RasterBackendSelectionRequest(
			new PersistentRasterLifecycleRequest(
				uploadResource: true,
				placementCount: 1,
				updatePlacement: true,
				deletePlacement: true,
				deleteResource: true,
				requireAcknowledgedUpload: true
			),
			new PersistentRasterPlacementRequest(
				requireSourceRectangle: true,
				requireSignedZOrder: true
			)
		);
	}

	private static PersistentRasterLifecycleProfile CreateSatisfiedLifecycleProfile() {
		PersistentRasterLifecycleEvidenceSubject[] subjects = [
			PersistentRasterLifecycleEvidenceSubject.PersistentUpload,
			PersistentRasterLifecycleEvidenceSubject.AcknowledgedUpload,
			PersistentRasterLifecycleEvidenceSubject.PlacementCreation,
			PersistentRasterLifecycleEvidenceSubject.MultiplePlacements,
			PersistentRasterLifecycleEvidenceSubject.PlacementUpdate,
			PersistentRasterLifecycleEvidenceSubject.PlacementDeletion,
			PersistentRasterLifecycleEvidenceSubject.ResourceDeletion
		];
		PersistentRasterLifecycleEvidence[] evidence =
			new PersistentRasterLifecycleEvidence[ subjects.Length ];
		for ( int index = 0; index < subjects.Length; ++index ) {
			evidence[ index ] = new PersistentRasterLifecycleEvidence(
				subjects[ index ],
				true,
				PersistentRasterLifecycleEvidenceKind.Verified,
				"Icod.Terminal integration qualification",
				index
			);
		}
		return PersistentRasterLifecycleClassifier.Classify( evidence );
	}

	private static PersistentRasterPlacementProfile CreateSatisfiedPlacementProfile() {
		return PersistentRasterPlacementClassifier.Classify(
			[
				new PersistentRasterPlacementEvidence(
					PersistentRasterPlacementSubject.SourceRectangle,
					true,
					PersistentRasterPlacementEvidenceKind.Verified,
					"Icod.Terminal integration qualification",
					0
				),
				new PersistentRasterPlacementEvidence(
					PersistentRasterPlacementSubject.SignedZOrder,
					true,
					PersistentRasterPlacementEvidenceKind.Verified,
					"Icod.Terminal integration qualification",
					1
				)
			]
		);
	}

	private static RasterBackendProfile CreateSupportedBackendProfile(
		RasterBackendKind backend
	) {
		return RasterBackendClassifier.Classify(
			backend,
			[
				new RasterBackendEvidence(
					backend,
					true,
					RasterBackendEvidenceKind.Verified,
					"Icod.Terminal integration qualification",
					0
				)
			]
		);
	}
}
