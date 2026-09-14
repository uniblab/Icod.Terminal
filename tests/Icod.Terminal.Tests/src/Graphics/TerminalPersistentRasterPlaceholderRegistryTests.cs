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

using Xunit;

/// <summary>
/// Defines the T152 bounded virtual-placement ownership and lifecycle contract.
/// </summary>
public sealed class TerminalPersistentRasterPlaceholderRegistryTests {
	[Fact]
	public void PlaceholderPlacementIdsAreBoundedWrapAndAvoidPhysicalCollision() {
		TerminalPersistentRasterRegistry registry = new(
			initialPlacementId: 1u,
			initialPlaceholderPlacementId: TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId
		);
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? resource
			)
		);
		Assert.NotNull( resource );

		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? physical
			)
		);
		Assert.NotNull( physical );
		Assert.Equal( 1u, physical.PlacementId );

		Assert.True(
			registry.TryReservePlaceholder(
				resource,
				columns: 4,
				rows: 3,
				out TerminalPersistentRasterPlaceholderState? maximum
			)
		);
		Assert.NotNull( maximum );
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId,
			maximum.PlacementId
		);

		Assert.True(
			registry.TryReservePlaceholder(
				resource,
				columns: 4,
				rows: 3,
				out TerminalPersistentRasterPlaceholderState? wrapped
			)
		);
		Assert.NotNull( wrapped );
		Assert.Equal( 2u, wrapped.PlacementId );
		Assert.InRange(
			wrapped.PlacementId,
			1u,
			TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId
		);
	}

	[Fact]
	public void PhysicalAndVirtualPlacementsShareOneCapacityCeiling() {
		TerminalPersistentRasterRegistry registry = new();
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? resource
			)
		);
		Assert.NotNull( resource );
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? physical
			)
		);
		Assert.NotNull( physical );

		for ( int index = 1; index < TerminalPersistentRasterRegistry.MaximumPlacements; ++index ) {
			Assert.True(
				registry.TryReservePlaceholder(
					resource,
					columns: 1,
					rows: 1,
					out TerminalPersistentRasterPlaceholderState? placeholder
				)
			);
			Assert.NotNull( placeholder );
		}

		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumPlacements,
			registry.LivePlacementCount
		);
		Assert.False(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? unavailablePhysical
			)
		);
		Assert.Null( unavailablePhysical );
		Assert.False(
			registry.TryReservePlaceholder(
				resource,
				columns: 1,
				rows: 1,
				out TerminalPersistentRasterPlaceholderState? unavailablePlaceholder
			)
		);
		Assert.Null( unavailablePlaceholder );
	}

	[Fact]
	public void PlaceholderStateCarriesResourceGeometryGenerationAndCurrentLifecycle() {
		TerminalPersistentRasterRegistry registry = new(
			initialGeneration: 37
		);
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? resource
			)
		);
		Assert.NotNull( resource );
		Assert.True(
			registry.TryReservePlaceholder(
				resource,
				columns: 7,
				rows: 5,
				out TerminalPersistentRasterPlaceholderState? placeholder
			)
		);
		Assert.NotNull( placeholder );

		Assert.Same( resource, placeholder.Resource );
		Assert.Equal( 7, placeholder.Columns );
		Assert.Equal( 5, placeholder.Rows );
		Assert.Equal( 37L, placeholder.Generation );
		Assert.InRange(
			placeholder.PlacementId,
			1u,
			TerminalPersistentRasterRegistry.MaximumPlaceholderPlacementId
		);
		Assert.False( placeholder.IsClosed );
		Assert.True( registry.IsPlaceholderCurrent( placeholder ) );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			placeholder.ObserveOwnershipState()
		);
	}

	[Fact]
	public void ResourceInvalidationStalesPlaceholderAsResourceMissing() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlaceholderState placeholder = ReservePlaceholder(
			registry,
			resource
		);

		Assert.True( registry.InvalidateResource( resource ) );
		Assert.False( registry.IsPlaceholderCurrent( placeholder ) );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.ResourceMissing
			),
			placeholder.ObserveOwnershipState()
		);
	}

	[Fact]
	public void SessionInvalidationStalesPlaceholderAsSessionStateLost() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlaceholderState placeholder = ReservePlaceholder(
			registry,
			resource
		);

		registry.Invalidate();

		Assert.False( registry.IsPlaceholderCurrent( placeholder ) );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.SessionStateLost
			),
			placeholder.ObserveOwnershipState()
		);
	}

	[Fact]
	public void ResourceReleaseReleasesPlaceholderAsResourceReleased() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlaceholderState placeholder = ReservePlaceholder(
			registry,
			resource
		);

		Assert.True( registry.TryReleaseResource( resource ) );

		Assert.True( placeholder.IsClosed );
		Assert.False( registry.IsPlaceholderCurrent( placeholder ) );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Released,
				TerminalRasterOwnershipLossReason.ResourceReleased
			),
			placeholder.ObserveOwnershipState()
		);
	}

	[Fact]
	public void PlaceholderReleaseIsIdempotentAndFreesCombinedCapacity() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlaceholderState placeholder = ReservePlaceholder(
			registry,
			resource
		);

		Assert.Equal( 1, registry.LivePlacementCount );
		Assert.True( registry.TryReleasePlaceholder( placeholder ) );
		Assert.True( placeholder.IsClosed );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.False( registry.TryReleasePlaceholder( placeholder ) );
		Assert.False( registry.IsPlaceholderCurrent( placeholder ) );
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

	private static TerminalPersistentRasterPlaceholderState ReservePlaceholder(
		TerminalPersistentRasterRegistry registry,
		TerminalPersistentRasterResourceState resource
	) {
		ArgumentNullException.ThrowIfNull( registry );
		ArgumentNullException.ThrowIfNull( resource );
		Assert.True(
			registry.TryReservePlaceholder(
				resource,
				columns: 3,
				rows: 2,
				out TerminalPersistentRasterPlaceholderState? placeholder
			)
		);
		return Assert.IsType<TerminalPersistentRasterPlaceholderState>( placeholder );
	}
}
