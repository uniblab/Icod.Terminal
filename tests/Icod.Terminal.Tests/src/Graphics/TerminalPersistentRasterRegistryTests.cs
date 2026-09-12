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

using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

/// <summary>
/// Defines the C113 bounded persistent-raster ownership registry contract.
/// </summary>
public sealed class TerminalPersistentRasterRegistryTests {
	[Fact]
	public void ResourceImageNumbersAreNonzeroAndWrapWithoutCollision() {
		TerminalPersistentRasterRegistry registry = new(
			initialImageNumber: uint.MaxValue
		);

		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? maximum
			)
		);
		Assert.NotNull( maximum );
		Assert.Equal( uint.MaxValue, maximum.ImageNumber );

		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? wrapped
			)
		);
		Assert.NotNull( wrapped );
		Assert.Equal( 1u, wrapped.ImageNumber );
		Assert.NotEqual( maximum.ImageNumber, wrapped.ImageNumber );
	}

	[Fact]
	public void ResourceCapacityIsBoundedAtTwoHundredFiftySix() {
		TerminalPersistentRasterRegistry registry = new();
		HashSet<uint> imageNumbers = [];

		for ( int index = 0; index < TerminalPersistentRasterRegistry.MaximumResources; ++index ) {
			Assert.True(
				registry.TryReserveResource(
					out TerminalPersistentRasterResourceState? resource
				)
			);
			Assert.NotNull( resource );
			Assert.NotEqual( 0u, resource.ImageNumber );
			Assert.True( imageNumbers.Add( resource.ImageNumber ) );
		}

		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			registry.LiveResourceCount
		);
		Assert.False(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? unavailable
			)
		);
		Assert.Null( unavailable );
	}

	[Fact]
	public void PlacementIdsAreNonzeroWrapAndUseOneSessionWideCapacity() {
		TerminalPersistentRasterRegistry registry = new(
			initialPlacementId: uint.MaxValue
		);
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? firstResource
			)
		);
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? secondResource
			)
		);
		Assert.NotNull( firstResource );
		Assert.NotNull( secondResource );

		Assert.True(
			registry.TryReservePlacement(
				firstResource,
				out TerminalPersistentRasterPlacementState? maximum
			)
		);
		Assert.NotNull( maximum );
		Assert.Equal( uint.MaxValue, maximum.PlacementId );

		Assert.True(
			registry.TryReservePlacement(
				secondResource,
				out TerminalPersistentRasterPlacementState? wrapped
			)
		);
		Assert.NotNull( wrapped );
		Assert.Equal( 1u, wrapped.PlacementId );

		for ( int index = 2; index < TerminalPersistentRasterRegistry.MaximumPlacements; ++index ) {
			Assert.True(
				registry.TryReservePlacement(
					firstResource,
					out TerminalPersistentRasterPlacementState? placement
				)
			);
			Assert.NotNull( placement );
			Assert.NotEqual( 0u, placement.PlacementId );
		}

		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumPlacements,
			registry.LivePlacementCount
		);
		Assert.False(
			registry.TryReservePlacement(
				secondResource,
				out TerminalPersistentRasterPlacementState? unavailable
			)
		);
		Assert.Null( unavailable );
	}

	[Fact]
	public void MultiplePlacementsRemainAssociatedWithTheirParent() {
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
				out TerminalPersistentRasterPlacementState? first
			)
		);
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? second
			)
		);
		Assert.NotNull( first );
		Assert.NotNull( second );
		Assert.Same( resource, first.Resource );
		Assert.Same( resource, second.Resource );
		Assert.NotEqual( first.PlacementId, second.PlacementId );
	}

	[Fact]
	public void ResourceReleaseIsIdempotentAndBlocksNewChildren() {
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
				out TerminalPersistentRasterPlacementState? placement
			)
		);
		Assert.NotNull( placement );

		Assert.True( registry.TryReleaseResource( resource ) );
		Assert.True( resource.IsClosed );
		Assert.True( placement.IsClosed );
		Assert.Equal( 0, registry.LiveResourceCount );
		Assert.Equal( 0, registry.LivePlacementCount );
		Assert.False(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? unavailable
			)
		);
		Assert.Null( unavailable );
		Assert.False( registry.TryReleaseResource( resource ) );
	}

	[Fact]
	public void PlacementReleaseIsIdempotentAndMonotonicAllocationAvoidsLiveReuse() {
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
				out TerminalPersistentRasterPlacementState? first
			)
		);
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? second
			)
		);
		Assert.NotNull( first );
		Assert.NotNull( second );

		Assert.True( registry.TryReleasePlacement( first ) );
		Assert.True( first.IsClosed );
		Assert.False( registry.TryReleasePlacement( first ) );
		Assert.True(
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? third
			)
		);
		Assert.NotNull( third );
		Assert.NotEqual( second.PlacementId, third.PlacementId );
		Assert.NotEqual( first.PlacementId, third.PlacementId );
	}

	[Fact]
	public void ResourceAndPlacementStatesCarryTheRegistryGeneration() {
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
			registry.TryReservePlacement(
				resource,
				out TerminalPersistentRasterPlacementState? placement
			)
		);
		Assert.NotNull( placement );

		Assert.Equal( 37L, registry.Generation );
		Assert.Equal( registry.Generation, resource.Generation );
		Assert.Equal( registry.Generation, placement.Generation );
	}

	[Fact]
	public async Task ConcurrentResourceReservationProducesUniqueBoundedOwnership() {
		TerminalPersistentRasterRegistry registry = new();
		ConcurrentBag<uint> identities = [];
		Task[] reservations = Enumerable.Range(
			0,
			TerminalPersistentRasterRegistry.MaximumResources
		).Select(
			_ => Task.Run(
				() => {
					Assert.True(
						registry.TryReserveResource(
							out TerminalPersistentRasterResourceState? resource
						)
					);
					Assert.NotNull( resource );
					identities.Add( resource.ImageNumber );
				}
			)
		).ToArray();

		await Task.WhenAll( reservations );

		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			identities.Count
		);
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			identities.Distinct().Count()
		);
		Assert.DoesNotContain( 0u, identities );
		Assert.Equal(
			TerminalPersistentRasterRegistry.MaximumResources,
			registry.LiveResourceCount
		);
	}

	[Fact]
	public void OwnershipStateDoesNotRetainRasterPayloadTypes() {
		Type[] stateTypes = [
			typeof( TerminalPersistentRasterResourceState ),
			typeof( TerminalPersistentRasterPlacementState )
		];

		foreach ( Type stateType in stateTypes ) {
			FieldInfo[] fields = stateType.GetFields(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
			);
			Assert.DoesNotContain(
				fields,
				static field => typeof( TerminalRasterImage ) == field.FieldType
					|| typeof( byte[] ) == field.FieldType
			);
		}
	}
}
