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
/// Qualifies fixed-count T167 concurrency and race behavior for persistent-raster animation state.
/// </summary>
public sealed class TerminalRasterAnimationConcurrencyTests {
	private const int ReaderCount = 8;
	private const int ObservationsPerReader = 25_000;
	private const int ReservationWorkerCount = 16;
	private const int PublishInvalidationRaceCount = 128;

	[Fact]
	public async Task ConcurrentStateReadersNeverObserveTornStatusReasonPairs() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterResourceState resource = CreateResource();
		TerminalPersistentRasterAnimationState animation = CreateAnimation(
			registry,
			resource
		);
		TerminalRasterAnimationState current = new(
			TerminalRasterAnimationStatus.Current,
			TerminalRasterAnimationLossReason.None
		);
		TerminalRasterAnimationState uncertain = new(
			TerminalRasterAnimationStatus.SequenceUncertain,
			TerminalRasterAnimationLossReason.FrameSequenceAmbiguous
		);
		TerminalRasterAnimationState stale = new(
			TerminalRasterAnimationStatus.Stale,
			TerminalRasterAnimationLossReason.SessionStateLost
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
						TerminalRasterAnimationState observation = animation.ObserveState();
						Assert.True(
							observation == current
								|| observation == uncertain
								|| observation == stale
						);
					}
				}
			)
		).ToArray();
		Task<bool> ambiguity = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				return animation.TryMarkSequenceUncertain();
			}
		);
		Task<bool> invalidation = Task.Run(
			async () => {
				await start.Task.ConfigureAwait( false );
				return resource.TryMarkStale(
					TerminalRasterOwnershipLossReason.SessionStateLost
				);
			}
		);

		start.SetResult();
		bool[] transitions = await Task.WhenAll(
			ambiguity,
			invalidation
		);
		await Task.WhenAll( readers );

		Assert.True( transitions[ 1 ] );
		Assert.Equal( stale, animation.ObserveState() );
		Assert.Equal( 0, registry.KnownFrameCount );
	}

	[Fact]
	public async Task ConcurrentAppendReservationsPublishAtMostOnePendingIdentity() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation(
			registry,
			CreateResource()
		);
		TaskCompletionSource start = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		Task<TerminalPersistentRasterAnimationRegistry.AppendReservation?>[] workers =
			Enumerable.Range(
				0,
				ReservationWorkerCount
			).Select(
				_ => Task.Run(
					async () => {
						await start.Task.ConfigureAwait( false );
						return registry.TryReserveAppend(
							animation,
							out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
						)
							? reservation
							: null
						;
					}
				)
			).ToArray();

		start.SetResult();
		TerminalPersistentRasterAnimationRegistry.AppendReservation?[] results =
			await Task.WhenAll( workers );
		TerminalPersistentRasterAnimationRegistry.AppendReservation winner =
			Assert.Single( results.OfType<TerminalPersistentRasterAnimationRegistry.AppendReservation>() );
		Assert.Equal( 2u, winner.FrameNumber );
		Assert.Equal( 1, registry.KnownFrameCount );
		Assert.True( registry.TryRollbackAppend( winner ) );
		Assert.True( registry.TryReserveAppend( animation, out _ ) );
	}

	[Fact]
	public async Task AppendPublicationRacingInvalidationCannotResurrectOwnedFrames() {
		for ( int iteration = 0; iteration < PublishInvalidationRaceCount; ++iteration ) {
			TerminalPersistentRasterAnimationRegistry registry = new();
			TerminalPersistentRasterResourceState resource = CreateResource();
			TerminalPersistentRasterAnimationState animation = CreateAnimation(
				registry,
				resource
			);
			Assert.True(
				registry.TryReserveAppend(
					animation,
					out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
				)
			);
			Assert.NotNull( reservation );
			TaskCompletionSource start = new(
				TaskCreationOptions.RunContinuationsAsynchronously
			);
			Task<bool> publication = Task.Run(
				async () => {
					await start.Task.ConfigureAwait( false );
					return registry.TryPublishAppend(
						reservation,
						out _
					);
				}
			);
			Task<bool> invalidation = Task.Run(
				async () => {
					await start.Task.ConfigureAwait( false );
					return resource.TryMarkStale(
						TerminalRasterOwnershipLossReason.SessionStateLost
					);
				}
			);

			start.SetResult();
			_ = await publication;
			Assert.True( await invalidation );

			Assert.Equal(
				new TerminalRasterAnimationState(
					TerminalRasterAnimationStatus.Stale,
					TerminalRasterAnimationLossReason.SessionStateLost
				),
				animation.ObserveState()
			);
			Assert.Equal( 0, registry.KnownFrameCount );
			Assert.False( registry.OwnsFrame( animation, animation.RootFrame ) );
			Assert.False( registry.TryReserveAppend( animation, out _ ) );
		}
	}

	[Fact]
	public async Task SequenceUncertainRetainsKnownTokensUnderConcurrentObservation() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation(
			registry,
			CreateResource()
		);
		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
			)
		);
		Assert.NotNull( reservation );
		Assert.True(
			registry.TryPublishAppend(
				reservation,
				out TerminalPersistentRasterAnimationFrameState? frame
			)
		);
		Assert.NotNull( frame );
		Assert.True( animation.TryMarkSequenceUncertain() );
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
						Assert.Equal(
							TerminalRasterAnimationStatus.SequenceUncertain,
							animation.ObserveState().Status
						);
						Assert.Equal( 2, registry.KnownFrameCount );
						Assert.True( registry.OwnsFrame( animation, animation.RootFrame ) );
						Assert.True( registry.OwnsFrame( animation, frame ) );
					}
				}
			)
		).ToArray();

		start.SetResult();
		await Task.WhenAll( readers );

		Assert.False( registry.TryReserveAppend( animation, out _ ) );
		Assert.Equal( 2, registry.KnownFrameCount );
	}

	private static TerminalPersistentRasterAnimationState CreateAnimation(
		TerminalPersistentRasterAnimationRegistry registry,
		TerminalPersistentRasterResourceState resource
	) {
		Assert.True(
			registry.TryGetOrCreate(
				resource,
				out TerminalPersistentRasterAnimationState? animation
			)
		);
		return Assert.IsType<TerminalPersistentRasterAnimationState>( animation );
	}

	private static TerminalPersistentRasterResourceState CreateResource() {
		return new TerminalPersistentRasterResourceState(
			imageNumber: 1,
			generation: 1,
			sourceWidth: 8,
			sourceHeight: 8
		);
	}
}
