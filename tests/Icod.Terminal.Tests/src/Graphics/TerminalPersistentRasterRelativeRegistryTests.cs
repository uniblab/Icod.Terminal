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
/// Verifies the bounded immutable-parent ownership graph for relative persistent raster placements.
/// </summary>
public sealed class TerminalPersistentRasterRelativeRegistryTests {
	[Fact]
	public void RelativePlacementMayUseParentFromAnotherResource() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState parentResource = ReserveResource( registry );
		TerminalPersistentRasterResourceState childResource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			parentResource
		);

		Assert.True(
			registry.TryReserveRelativePlacement(
				childResource,
				parent,
				columnOffset: -2,
				rowOffset: 3,
				out TerminalPersistentRasterPlacementState? child
			)
		);
		Assert.NotNull( child );
		Assert.Same( childResource, child.Resource );
		Assert.Same( parent, child.Parent );
		Assert.Equal( 1, child.RelativeDepth );
		Assert.Equal( -2, child.ColumnOffset );
		Assert.Equal( 3, child.RowOffset );
		Assert.True( registry.IsPlacementCurrent( parent ) );
		Assert.True( registry.IsPlacementCurrent( child ) );
	}

	[Fact]
	public void RelativeDepthEightIsAcceptedAndDepthNineIsRejected() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resource
		);

		for ( int depth = 1; depth <= TerminalPersistentRasterRegistry.MaximumRelativeDepth; ++depth ) {
			Assert.True(
				registry.TryReserveRelativePlacement(
					resource,
					parent,
					columnOffset: depth,
					rowOffset: -depth,
					out TerminalPersistentRasterPlacementState? child
				)
			);
			Assert.NotNull( child );
			Assert.Equal( depth, child.RelativeDepth );
			parent = child;
		}

		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumRelativeDepth + 1,
			registry.LivePlacementCount
		);
		Assert.False(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				columnOffset: 0,
				rowOffset: 0,
				out TerminalPersistentRasterPlacementState? tooDeep
			)
		);
		Assert.Null( tooDeep );
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumRelativeDepth + 1,
			registry.LivePlacementCount
		);
	}

	[Fact]
	public void ClosedParentCannotAcquireRelativeChild() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resource
		);

		Assert.True( registry.TryReleasePlacement( parent ) );
		Assert.False(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				columnOffset: 1,
				rowOffset: 1,
				out TerminalPersistentRasterPlacementState? child
			)
		);
		Assert.Null( child );
		Assert.Equal( 0, registry.LivePlacementCount );
	}

	[Fact]
	public void ReleasingParentReleasesCrossResourceDescendantsDeepestFirst() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceC = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent,
			columnOffset: 1,
			rowOffset: 2
		);
		TerminalPersistentRasterPlacementState grandchild = ReserveRelativePlacement(
			registry,
			resourceC,
			child,
			columnOffset: 3,
			rowOffset: 4
		);

		Assert.True(
			registry.TryReleasePlacement(
				parent,
				out TerminalPersistentRasterPlacementState[] released
			)
		);
		Assert.Equal(
			[
				grandchild,
				child,
				parent
			],
			released
		);
		Assert.True( grandchild.IsClosed );
		Assert.True( child.IsClosed );
		Assert.True( parent.IsClosed );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.True( registry.IsResourceCurrent( resourceA ) );
		Assert.True( registry.IsResourceCurrent( resourceB ) );
		Assert.True( registry.IsResourceCurrent( resourceC ) );
		Assert.Equal( 3, registry.LiveResourceCount );
	}

	[Fact]
	public void ReleasingResourceCascadesRelativePlacementsButNotDescendantResources() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceC = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			parent,
			columnOffset: -1,
			rowOffset: 2
		);
		TerminalPersistentRasterPlacementState grandchild = ReserveRelativePlacement(
			registry,
			resourceC,
			child,
			columnOffset: -3,
			rowOffset: 4
		);

		Assert.True(
			registry.TryReleaseResource(
				resourceA,
				out TerminalPersistentRasterPlacementState[] released
			)
		);
		Assert.Equal(
			[
				grandchild,
				child,
				parent
			],
			released
		);
		Assert.True( resourceA.IsClosed );
		Assert.False( resourceB.IsClosed );
		Assert.False( resourceC.IsClosed );
		Assert.False( registry.IsResourceCurrent( resourceA ) );
		Assert.True( registry.IsResourceCurrent( resourceB ) );
		Assert.True( registry.IsResourceCurrent( resourceC ) );
		Assert.Equal( 2, registry.LiveResourceCount );
		Assert.Equal( 0, registry.LivePlacementCount );
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
		TerminalPersistentRasterPlacementState parent,
		int columnOffset,
		int rowOffset
	) {
		ArgumentNullException.ThrowIfNull( registry );
		ArgumentNullException.ThrowIfNull( resource );
		ArgumentNullException.ThrowIfNull( parent );
		Assert.True(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				columnOffset,
				rowOffset,
				out TerminalPersistentRasterPlacementState? placement
			)
		);
		return Assert.IsType<TerminalPersistentRasterPlacementState>( placement );
	}
}
