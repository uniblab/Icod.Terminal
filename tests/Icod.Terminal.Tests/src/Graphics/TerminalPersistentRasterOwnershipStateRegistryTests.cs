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

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies lifecycle-state publication at the persistent-raster registry ownership boundary.
/// </summary>
public sealed class TerminalPersistentRasterOwnershipStateRegistryTests {
	[Fact]
	public void RegistryInvalidationPublishesSessionStateLost() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState placement = ReservePlacement(
			registry,
			resource
		);

		registry.Invalidate();

		AssertState(
			resource.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		AssertState(
			placement.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
	}

	[Fact]
	public void MissingResourceInvalidatesItsCompleteDependentPlacementSubtree() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent
		);

		Assert.True( registry.InvalidateResource( resourceA ) );

		AssertState(
			resourceA.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.ResourceMissing
		);
		AssertState(
			parent.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.ResourceMissing
		);
		AssertState(
			child.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.ResourceMissing
		);
		AssertState(
			resourceB.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
	}

	[Fact]
	public void MissingParentInvalidatesPlacementSubtreeWithoutStalingResources() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent
		);

		Assert.True( registry.InvalidatePlacementSubtree( parent ) );

		AssertState(
			parent.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.ParentPlacementLost
		);
		AssertState(
			child.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.ParentPlacementLost
		);
		AssertState(
			resourceA.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		AssertState(
			resourceB.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
	}

	[Fact]
	public void ParentReleaseMarksCompletePlacementSubtreeAsAncestorReleased() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent
		);

		Assert.True( registry.TryReleasePlacement( parent ) );

		AssertState(
			parent.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.AncestorReleased
		);
		AssertState(
			child.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.AncestorReleased
		);
		AssertState(
			resourceA.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		AssertState(
			resourceB.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
	}

	[Fact]
	public void ResourceReleaseDistinguishesOwnPlacementsFromCrossResourceDescendants() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent
		);

		Assert.True( registry.TryReleaseResource( resourceA ) );

		AssertState(
			parent.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.ResourceReleased
		);
		AssertState(
			child.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.AncestorReleased
		);
		AssertState(
			resourceB.ObserveOwnershipState(),
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
	}

	private static void AssertState(
		TerminalRasterOwnershipState state,
		TerminalRasterOwnershipStatus status,
		TerminalRasterOwnershipLossReason reason
	) {
		Assert.Equal( status, state.Status );
		Assert.Equal( reason, state.LossReason );
	}

	private static TerminalPersistentRasterResourceState ReserveResource(
		TerminalPersistentRasterRegistry registry
	) {
		ArgumentNullException.ThrowIfNull( registry );
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? resource
			)
		);
		return Assert.IsType<TerminalPersistentRasterResourceState>( resource );
	}

	private static TerminalPersistentRasterPlacementState ReservePlacement(
		TerminalPersistentRasterRegistry registry,
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( registry );
		ArgumentNullException.ThrowIfNull( resource );
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? placement
			)
		);
		return Assert.IsType<TerminalPersistentRasterPlacementState>( placement );
	}

	private static TerminalPersistentRasterPlacementState ReserveRelativePlacement(
		TerminalPersistentRasterRegistry registry,
		TerminalPersistentRasterResourceState resource,
		TerminalPersistentRasterPlacementState parent
	) {
		ArgumentNullException.ThrowIfNull( registry );
		ArgumentNullException.ThrowIfNull( resource );
		ArgumentNullException.ThrowIfNull( parent );
		Assert.True(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				columnOffset: 1,
				rowOffset: -1,
				out TerminalPersistentRasterPlacementState? placement
			)
		);
		return Assert.IsType<TerminalPersistentRasterPlacementState>( placement );
	}
}
