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
/// Proves the current loose-coupling boundary can consume the additive 1.12
/// advanced persistent-raster placement planning surface.
/// </summary>
public sealed class PersistentRasterPlacementIntegrationTests {
	[Fact]
	public void Inspection112PlansSourceRectangleAndSignedZOrderSeparatelyFromExecutionValues() {
		PersistentRasterLifecycleProfile lifecycleProfile =
			PersistentRasterLifecycleClassifier.Classify(
				[
					new PersistentRasterLifecycleEvidence(
						PersistentRasterLifecycleEvidenceSubject.PlacementCreation,
						true,
						PersistentRasterLifecycleEvidenceKind.Verified,
						"integration lifecycle",
						0
					)
				]
			);
		PersistentRasterLifecyclePlan lifecyclePlan =
			PersistentRasterLifecyclePlanner.Plan(
				lifecycleProfile,
				new PersistentRasterLifecycleRequest( placementCount: 1 )
			);
		Assert.Equal(
			PersistentRasterLifecyclePlanStatus.Success,
			lifecyclePlan.Status
		);

		PersistentRasterPlacementRequest placementRequest =
			new(
				requireSourceRectangle: true,
				requireSignedZOrder: true
			);
		PersistentRasterPlacementProfile unknownProfile =
			PersistentRasterPlacementClassifier.Classify(
				Array.Empty<PersistentRasterPlacementEvidence>()
			);
		PersistentRasterPlacementPlan unknownPlan =
			PersistentRasterPlacementPlanner.Plan(
				lifecyclePlan,
				unknownProfile,
				placementRequest
			);
		Assert.Equal(
			PersistentRasterPlacementPlanStatus.RequiresRuntimeVerification,
			unknownPlan.Status
		);

		PersistentRasterPlacementProfile verifiedProfile =
			PersistentRasterPlacementClassifier.Classify(
				[
					new PersistentRasterPlacementEvidence(
						PersistentRasterPlacementSubject.SourceRectangle,
						true,
						PersistentRasterPlacementEvidenceKind.Verified,
						"integration runtime verification",
						0
					),
					new PersistentRasterPlacementEvidence(
						PersistentRasterPlacementSubject.SignedZOrder,
						true,
						PersistentRasterPlacementEvidenceKind.Verified,
						"integration runtime verification",
						1
					)
				]
			);
		PersistentRasterPlacementPlan verifiedPlan =
			PersistentRasterPlacementPlanner.Plan(
				lifecyclePlan,
				verifiedProfile,
				placementRequest
			);

		Assert.Equal(
			PersistentRasterPlacementPlanStatus.Satisfied,
			verifiedPlan.Status
		);
		Assert.Equal(
			PersistentRasterLifecycleSupportStatus.Supported,
			verifiedProfile.SourceRectangle
		);
		Assert.Equal(
			PersistentRasterLifecycleSupportStatus.Supported,
			verifiedProfile.SignedZOrder
		);
	}
}
