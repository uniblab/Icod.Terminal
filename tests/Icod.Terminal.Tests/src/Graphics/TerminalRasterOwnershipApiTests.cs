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
namespace Icod.Terminal.Tests.Graphics;

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the additive 1.14 persistent-raster ownership-observation surface.
/// </summary>
public sealed class TerminalRasterOwnershipApiTests {
	[Fact]
	public void OwnershipStatePublicSurfaceHasFrozenSemanticShape() {
		TerminalRasterOwnershipState state = new(
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);

		Assert.Equal( TerminalRasterOwnershipStatus.Current, state.Status );
		Assert.Equal( TerminalRasterOwnershipLossReason.None, state.LossReason );
		Assert.Equal(
			[
				nameof( TerminalRasterOwnershipStatus.Current ),
				nameof( TerminalRasterOwnershipStatus.Stale ),
				nameof( TerminalRasterOwnershipStatus.Released ),
				nameof( TerminalRasterOwnershipStatus.Disposed )
			],
			Enum.GetNames<TerminalRasterOwnershipStatus>()
		);
		Assert.Equal(
			[
				nameof( TerminalRasterOwnershipLossReason.None ),
				nameof( TerminalRasterOwnershipLossReason.SessionStateLost ),
				nameof( TerminalRasterOwnershipLossReason.ResourceMissing ),
				nameof( TerminalRasterOwnershipLossReason.ParentPlacementLost ),
				nameof( TerminalRasterOwnershipLossReason.AncestorReleased ),
				nameof( TerminalRasterOwnershipLossReason.ResourceReleased ),
				nameof( TerminalRasterOwnershipLossReason.ExplicitDisposal )
			],
			Enum.GetNames<TerminalRasterOwnershipLossReason>()
		);

		PropertyInfo resourceProperty = Assert.IsType<PropertyInfo>(
			typeof( TerminalRasterResource ).GetProperty( nameof( TerminalRasterResource.OwnershipState ) )
		);
		PropertyInfo placementProperty = Assert.IsType<PropertyInfo>(
			typeof( TerminalRasterPlacement ).GetProperty( nameof( TerminalRasterPlacement.OwnershipState ) )
		);
		Assert.Equal( typeof( TerminalRasterOwnershipState ), resourceProperty.PropertyType );
		Assert.Equal( typeof( TerminalRasterOwnershipState ), placementProperty.PropertyType );
		Assert.True( resourceProperty.CanRead );
		Assert.False( resourceProperty.CanWrite );
		Assert.True( placementProperty.CanRead );
		Assert.False( placementProperty.CanWrite );
	}

	[Fact]
	public void OwnershipObservationSurfaceDoesNotExposeProtocolOrGenerationIdentity() {
		string[] forbiddenFragments = [
			"ImageId",
			"ImageNumber",
			"PlacementId",
			"ParentId",
			"Generation"
		];

		Type[] publicTypes = [
			typeof( TerminalRasterOwnershipState ),
			typeof( TerminalRasterResource ),
			typeof( TerminalRasterPlacement )
		];
		foreach ( Type type in publicTypes ) {
			foreach ( MemberInfo member in type.GetMembers( BindingFlags.Instance | BindingFlags.Public ) ) {
				Assert.DoesNotContain(
					forbiddenFragments,
					fragment => member.Name.Contains(
						fragment,
						StringComparison.OrdinalIgnoreCase
					)
				);
			}
		}
	}
}
