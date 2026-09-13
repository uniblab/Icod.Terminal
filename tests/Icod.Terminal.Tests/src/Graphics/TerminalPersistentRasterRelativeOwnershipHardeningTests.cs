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
/// Verifies T137 graph stress, generation, capacity, identity, and concurrency boundaries.
/// </summary>
public sealed class TerminalPersistentRasterRelativeOwnershipHardeningTests {
	[Fact]
	public void DepthZeroThroughEightRetainsSignedOffsetExtremaAndRejectsDepthNine() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState parent = ReservePlacement(
			registry,
			resource
		);

		Assert.Equal( 0, parent.RelativeDepth );
		Assert.Null( parent.Parent );
		for ( int depth = 1; depth <= TerminalPersistentRasterRegistry.MaximumRelativeDepth; ++depth ) {
			int columnOffset = 0 == depth % 2
				? int.MinValue
				: int.MaxValue
			;
			int rowOffset = 0 == depth % 2
				? int.MaxValue
				: int.MinValue
			;
			TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
				registry,
				resource,
				parent,
				columnOffset,
				rowOffset
			);

			Assert.Equal( depth, child.RelativeDepth );
			Assert.Same( parent, child.Parent );
			Assert.Equal( columnOffset, child.ColumnOffset );
			Assert.Equal( rowOffset, child.RowOffset );
			Assert.True( registry.IsPlacementCurrent( child ) );
			parent = child;
		}

		Assert.False(
			registry.TryReserveRelativePlacement(
				resource,
				parent,
				0,
				0,
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
	public void ReleasingMiddleResourceRemovesOnlyItsRelativeSubtree() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceC = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceD = ReserveResource( registry );
		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState middle = ReserveRelativePlacement(
			registry,
			resourceB,
			root,
			1,
			2
		);
		TerminalPersistentRasterPlacementState descendant = ReserveRelativePlacement(
			registry,
			resourceC,
			middle,
			3,
			4
		);
		TerminalPersistentRasterPlacementState sibling = ReserveRelativePlacement(
			registry,
			resourceD,
			root,
			-5,
			-6
		);

		Assert.True(
			registry.TryReleaseResource(
				resourceB,
				out TerminalPersistentRasterPlacementState[] released
			)
		);
		Assert.Equal(
			[
				descendant,
				middle
			],
			released
		);
		Assert.True( middle.IsClosed );
		Assert.True( descendant.IsClosed );
		Assert.False( registry.TryReleasePlacement( middle ) );
		Assert.True( registry.IsPlacementCurrent( root ) );
		Assert.True( registry.IsPlacementCurrent( sibling ) );
		Assert.True( registry.IsResourceCurrent( resourceA ) );
		Assert.False( registry.IsResourceCurrent( resourceB ) );
		Assert.True( registry.IsResourceCurrent( resourceC ) );
		Assert.True( registry.IsResourceCurrent( resourceD ) );
		Assert.Equal( 3, registry.LiveResourceCount );
		Assert.Equal( 2, registry.LivePlacementCount );
	}

	[Fact]
	public void GenerationInvalidationMakesCompleteGraphStaleWithoutReusingOldState() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resourceA = ReserveResource( registry );
		TerminalPersistentRasterResourceState resourceB = ReserveResource( registry );
		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resourceA
		);
		TerminalPersistentRasterPlacementState child = ReserveRelativePlacement(
			registry,
			resourceB,
			root,
			int.MinValue,
			int.MaxValue
		);
		long oldGeneration = registry.Generation;

		registry.Invalidate();

		Assert.NotEqual( oldGeneration, registry.Generation );
		Assert.Equal( 0, registry.LiveResourceCount );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.False( registry.IsResourceCurrent( resourceA ) );
		Assert.False( registry.IsResourceCurrent( resourceB ) );
		Assert.False( registry.IsPlacementCurrent( root ) );
		Assert.False( registry.IsPlacementCurrent( child ) );
		Assert.False( registry.TryReleasePlacement( child ) );
		Assert.False(
			registry.TryReserveRelativePlacement(
				resourceB,
				root,
				0,
				0,
				out TerminalPersistentRasterPlacementState? staleReservation
			)
		);
		Assert.Null( staleReservation );

		TerminalPersistentRasterResourceState replacementResource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState replacementPlacement = ReservePlacement(
			registry,
			replacementResource
		);
		Assert.Equal( registry.Generation, replacementResource.Generation );
		Assert.Equal( registry.Generation, replacementPlacement.Generation );
		Assert.True( registry.IsResourceCurrent( replacementResource ) );
		Assert.True( registry.IsPlacementCurrent( replacementPlacement ) );
	}

	[Fact]
	public void ResourceCapacityRemainsExactlyTwoHundredFiftySixWithRelativeGraph() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState[] resources = new TerminalPersistentRasterResourceState[
			TerminalPersistentRasterRegistry.MaximumResources
		];
		for ( int index = 0; index < resources.Length; ++index ) {
			resources[ index ] = ReserveResource( registry );
		}
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			registry.LiveResourceCount
		);
		Assert.False(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? overflowResource
			)
		);
		Assert.Null( overflowResource );

		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resources[ 0 ]
		);
		for ( int index = 1; index < resources.Length; ++index ) {
			_ = ReserveRelativePlacement(
				registry,
				resources[ index ],
				root,
				index,
				-index
			);
		}
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			registry.LivePlacementCount
		);

		Assert.True( registry.TryReleaseResource( resources[ 1 ] ) );
		TerminalPersistentRasterResourceState replacement = ReserveResource( registry );
		Assert.True( registry.IsResourceCurrent( replacement ) );
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			registry.LiveResourceCount
		);
	}

	[Fact]
	public void PlacementCapacityRemainsExactlyFourThousandNinetySixForRelativeGraph() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resource
		);

		for ( int index = 1; index < TerminalPersistentRasterRegistry.MaximumPlacements; ++index ) {
			_ = ReserveRelativePlacement(
				registry,
				resource,
				root,
				index,
				-index
			);
		}
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumPlacements,
			registry.LivePlacementCount
		);
		Assert.False(
			registry.TryReserveRelativePlacement(
				resource,
				root,
				0,
				0,
				out TerminalPersistentRasterPlacementState? overflowPlacement
			)
		);
		Assert.Null( overflowPlacement );

		Assert.True(
			registry.TryReleasePlacement(
				root,
				out TerminalPersistentRasterPlacementState[] released
			)
		);
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumPlacements,
			released.Length
		);
		Assert.Equal( 0, registry.LivePlacementCount );
	}

	[Fact]
	public void PlacementIdentityWrapRemainsNonzeroAndCollisionFreeForRelativeChildren() {
		TerminalPersistentRasterRegistry registry = new(
			initialPlacementId: uint.MaxValue
		);
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resource
		);
		TerminalPersistentRasterPlacementState firstChild = ReserveRelativePlacement(
			registry,
			resource,
			root,
			1,
			1
		);
		TerminalPersistentRasterPlacementState secondChild = ReserveRelativePlacement(
			registry,
			resource,
			root,
			2,
			2
		);

		Assert.Equal( uint.MaxValue, root.PlacementId );
		Assert.Equal( 1u, firstChild.PlacementId );
		Assert.Equal( 2u, secondChild.PlacementId );
		Assert.NotEqual( 0u, root.PlacementId );
		Assert.NotEqual( 0u, firstChild.PlacementId );
		Assert.NotEqual( 0u, secondChild.PlacementId );
		Assert.Equal(
			3,
			new HashSet<uint> {
				root.PlacementId,
				firstChild.PlacementId,
				secondChild.PlacementId
			}.Count
		);
	}

	[Fact]
	public async Task ConcurrentRelativeReserveReleaseDoesNotLeakOrInvalidateParent() {
		TerminalPersistentRasterRegistry registry = new();
		TerminalPersistentRasterResourceState resource = ReserveResource( registry );
		TerminalPersistentRasterPlacementState root = ReservePlacement(
			registry,
			resource
		);
		const int workerCount = 128;

		Task<bool>[] workers = Enumerable.Range( 0, workerCount ).Select(
			index => Task.Run(
				() => {
					if ( !registry.TryReserveRelativePlacement(
						resource,
						root,
						index,
						-index,
						out TerminalPersistentRasterPlacementState? child
					) || child is null ) {
						return false;
					}
					return registry.TryReleasePlacement( child );
				}
			)
		).ToArray();
		bool[] results = await Task.WhenAll( workers );

		Assert.All(
			results,
			static result => Assert.True( result )
		);
		Assert.Equal( 1, registry.LivePlacementCount );
		Assert.True( registry.IsPlacementCurrent( root ) );
		Assert.True( registry.IsResourceCurrent( resource ) );
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
