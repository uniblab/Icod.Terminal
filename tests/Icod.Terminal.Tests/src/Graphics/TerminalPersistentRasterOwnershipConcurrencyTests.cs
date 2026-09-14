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
/// Verifies atomic and monotonic persistent-raster ownership observation under concurrent access.
/// </summary>
public sealed class TerminalPersistentRasterOwnershipConcurrencyTests {
	private const int ReaderCount = 8;
	private const int ObservationsPerReader = 50_000;

	[Fact]
	public async Task ConcurrentReadersNeverObserveTornStatusReasonPairs() {
		TerminalPersistentRasterLifecycleState state = new();
		TerminalRasterOwnershipState current = new(
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		TerminalRasterOwnershipState staleState = new(
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		TerminalRasterOwnershipState releasedState = new(
			TerminalRasterOwnershipStatus.Released,
			TerminalRasterOwnershipLossReason.AncestorReleased
		);
		TaskCompletionSource start = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		Task[] readers = Enumerable.Range(
			0,
			ReaderCount
		).Select(
			_ => Task.Run(
				async () => {
					await start.Task.ConfigureAwait( false );
					for ( int index = 0; index < ObservationsPerReader; ++index ) {
						TerminalRasterOwnershipState observation = state.Observe();
						Assert.True(
							observation == current
								|| observation == staleState
								|| observation == releasedState
						);
					}
				}
			)
		).ToArray();
		Task<bool> stale = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				return state.TryMarkStale(
					TerminalRasterOwnershipLossReason.SessionStateLost
				);
			}
		);
		Task<bool> released = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				return state.TryMarkReleased(
					TerminalRasterOwnershipLossReason.AncestorReleased
				);
			}
		);

		start.SetResult();
		bool[] transitionResults = await Task.WhenAll(
			stale,
			released
		);
		await Task.WhenAll( readers );

		Assert.NotEqual( transitionResults[ 0 ], transitionResults[ 1 ] );
		TerminalRasterOwnershipState final = state.Observe();
		Assert.True(
			final == staleState
				|| final == releasedState
		);
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

		TerminalRasterOwnershipState current = new(
			TerminalRasterOwnershipStatus.Current,
			TerminalRasterOwnershipLossReason.None
		);
		TerminalRasterOwnershipState staleState = new(
			TerminalRasterOwnershipStatus.Stale,
			TerminalRasterOwnershipLossReason.SessionStateLost
		);
		TaskCompletionSource start = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		Task[] readers = Enumerable.Range(
			0,
			ReaderCount
		).Select(
			_ => Task.Run(
				async () => {
					await start.Task.ConfigureAwait( false );
					for ( int index = 0; index < ObservationsPerReader; ++index ) {
						TerminalRasterOwnershipState resourceObservation =
							resource.ObserveOwnershipState();
						TerminalRasterOwnershipState placementObservation =
							placement.ObserveOwnershipState();
						Assert.True(
							resourceObservation == current
								|| resourceObservation == staleState
						);
						Assert.True(
							placementObservation == current
								|| placementObservation == staleState
						);
					}
				}
			)
		).ToArray();
		Task invalidation = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				registry.Invalidate();
			}
		);

		start.SetResult();
		await Task.WhenAll( readers.Append( invalidation ) );

		Assert.Equal( staleState, resource.ObserveOwnershipState() );
		Assert.Equal( staleState, placement.ObserveOwnershipState() );
	}
}
