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
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies atomic and monotonic persistent-raster ownership observation under concurrent access.
/// </summary>
public sealed class TerminalPersistentRasterOwnershipConcurrencyTests {
	[Theory]
	[InlineData( true )]
	[InlineData( false )]
	public async Task ConcurrentReadersNeverObserveTornStatusReasonPairs(
		bool staleWins
	) {
		TerminalPersistentRasterLifecycleState state = new();
		ConcurrentBag<TerminalRasterOwnershipState> observations = [];
		using CancellationTokenSource stop = new();
		Task[] readers = Enumerable.Range(
			0,
			8
		).Select(
			_ => Task.Run(
				() => {
					while ( !stop.IsCancellationRequested ) {
						observations.Add( state.Observe() );
					}
				}
			)
		).ToArray();

		Task<bool> stale = Task.Run(
			() => state.TryMarkStale(
				TerminalRasterOwnershipLossReason.SessionStateLost
			)
		);
		Task<bool> released = Task.Run(
			() => state.TryMarkReleased(
				TerminalRasterOwnershipLossReason.AncestorReleased
			)
		);
		await Task.WhenAll(
			stale,
			released
		);
		await Task.Delay( 25 );
		stop.Cancel();
		await Task.WhenAll( readers );

		Assert.NotEqual( stale.Result, released.Result );
		TerminalRasterOwnershipState final = state.Observe();
		Assert.True(
			final == new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.SessionStateLost
			) || final == new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Released,
				TerminalRasterOwnershipLossReason.AncestorReleased
			)
		);
		foreach ( TerminalRasterOwnershipState observation in observations ) {
			Assert.True(
				observation == new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Current,
					TerminalRasterOwnershipLossReason.None
				) || observation == final
			);
		}

		_ = staleWins;
	}

	[Fact]
	public async Task ConcurrentRegistryInvalidationAndObservationNeverResurrectsCurrentState() {
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

		ConcurrentBag<TerminalRasterOwnershipState> resourceObservations = [];
		ConcurrentBag<TerminalRasterOwnershipState> placementObservations = [];
		using CancellationTokenSource stop = new();
		Task reader = Task.Run(
			() => {
				while ( !stop.IsCancellationRequested ) {
					resourceObservations.Add( resource.ObserveOwnershipState() );
					placementObservations.Add( placement.ObserveOwnershipState() );
				}
			}
		);

		await Task.Run( registry.Invalidate );
		await Task.Delay( 25 );
		stop.Cancel();
		await reader;

		TerminalRasterOwnershipState expected = new(
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		Assert.Equal( expected, resource.ObserveOwnershipState() );
		Assert.Equal( expected, placement.ObserveOwnershipState() );
		Assert.All(
			resourceObservations,
			observation => Assert.True(
				observation == new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Current,
					TerminalRasterOwnershipLossReason.None
				) || observation == expected
			)
		);
		Assert.All(
			placementObservations,
			observation => Assert.True(
				observation == new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Current,
					TerminalRasterOwnershipLossReason.None
				) || observation == expected
			)
		);
	}
}
