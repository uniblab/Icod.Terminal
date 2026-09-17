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
/// Freezes the T162 bounded private animation-frame ownership model.
/// </summary>
public sealed class TerminalPersistentRasterAnimationRegistryTests {
	[Fact]
	public void OneAnimationStateAndRootFrameExistPerCurrentResource() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterResourceState resource = CreateResource( 1 );

		Assert.True( registry.TryGetOrCreate( resource, out TerminalPersistentRasterAnimationState? first ) );
		Assert.NotNull( first );
		Assert.True( registry.TryGetOrCreate( resource, out TerminalPersistentRasterAnimationState? second ) );
		Assert.Same( first, second );
		Assert.Same( resource, first.Resource );
		Assert.Equal( 1u, first.RootFrame.FrameNumber );
		Assert.Same( first, first.RootFrame.Animation );
		Assert.Equal( 1, first.KnownFrameCount );
		Assert.Equal( 1, registry.KnownFrameCount );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			first.ObserveState()
		);
	}

	[Fact]
	public void AppendNumberAdvancesOnlyAfterPublication() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, 1 );

		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? firstReservation
			)
		);
		Assert.NotNull( firstReservation );
		Assert.Equal( 2u, firstReservation.FrameNumber );
		Assert.Equal( 1, animation.KnownFrameCount );
		Assert.False( registry.TryReserveAppend( animation, out _ ) );

		Assert.True( registry.TryRollbackAppend( firstReservation ) );
		Assert.Equal( 1, animation.KnownFrameCount );

		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? retriedReservation
			)
		);
		Assert.NotNull( retriedReservation );
		Assert.Equal( 2u, retriedReservation.FrameNumber );
		Assert.True(
			registry.TryPublishAppend(
				retriedReservation,
				out TerminalPersistentRasterAnimationFrameState? frame2
			)
		);
		Assert.NotNull( frame2 );
		Assert.Equal( 2u, frame2.FrameNumber );
		Assert.Equal( 2, animation.KnownFrameCount );

		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? thirdReservation
			)
		);
		Assert.NotNull( thirdReservation );
		Assert.Equal( 3u, thirdReservation.FrameNumber );
	}

	[Fact]
	public void SessionWideFrameCeilingIncludesRoots() {
		Assert.Equal( 4096, TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames );

		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, 1 );

		for ( int index = 1; index < TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames; ++index ) {
			Assert.True(
				registry.TryReserveAppend(
					animation,
					out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
				)
			);
			Assert.NotNull( reservation );
			Assert.True( registry.TryPublishAppend( reservation, out _ ) );
		}

		Assert.Equal(
			TerminalPersistentRasterAnimationRegistry.MaximumKnownFrames,
			registry.KnownFrameCount
		);
		Assert.False( registry.TryReserveAppend( animation, out _ ) );
		Assert.False(
			registry.TryGetOrCreate(
				CreateResource( 2 ),
				out _
			)
		);
	}

	[Fact]
	public void ForeignAnimationAndFrameAreRejected() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState first = CreateAnimation( registry, 1 );
		TerminalPersistentRasterAnimationState second = CreateAnimation( registry, 2 );

		Assert.True( registry.OwnsFrame( first, first.RootFrame ) );
		Assert.True( registry.OwnsFrame( second, second.RootFrame ) );
		Assert.False( registry.OwnsFrame( first, second.RootFrame ) );
		Assert.False( registry.OwnsFrame( second, first.RootFrame ) );

		TerminalPersistentRasterAnimationRegistry otherRegistry = new();
		TerminalPersistentRasterAnimationState foreign = CreateAnimation( otherRegistry, 3 );
		Assert.False( registry.TryReserveAppend( foreign, out _ ) );
		Assert.False( registry.OwnsFrame( first, foreign.RootFrame ) );
	}

	[Fact]
	public void SequenceUncertaintyIsMonotonic() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, 1 );

		Assert.True( animation.TryMarkSequenceUncertain() );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.SequenceUncertain,
				TerminalRasterAnimationLossReason.FrameSequenceAmbiguous
			),
			animation.ObserveState()
		);
		Assert.False( animation.TryMarkSequenceUncertain() );
		Assert.Equal(
			TerminalRasterAnimationStatus.SequenceUncertain,
			animation.ObserveState().Status
		);
	}

	[Fact]
	public void SessionInvalidationStalesAnimationAndReleasesCapacity() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, 1 );

		registry.Invalidate();

		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.SessionStateLost
			),
			animation.ObserveState()
		);
		Assert.Equal( 0, registry.KnownFrameCount );
	}

	[Fact]
	public void MissingResourceStalesAnimation() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterResourceState resource = CreateResource( 1 );
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, resource );

		Assert.True( registry.InvalidateResource( resource ) );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.ResourceMissing
			),
			animation.ObserveState()
		);
		Assert.Equal( 0, registry.KnownFrameCount );
	}

	[Fact]
	public void IntentionalResourceReleaseMarksAnimationReleased() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterResourceState resource = CreateResource( 1 );
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, resource );

		Assert.True( registry.ReleaseResource( resource ) );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Released,
				TerminalRasterAnimationLossReason.ResourceReleased
			),
			animation.ObserveState()
		);
		Assert.Equal( 0, registry.KnownFrameCount );
	}

	[Fact]
	public void ExplicitResourceWrapperDisposalMarksOwnerDisposed() {
		TerminalPersistentRasterAnimationRegistry registry = new();
		TerminalPersistentRasterResourceState resource = CreateResource( 1 );
		TerminalPersistentRasterAnimationState animation = CreateAnimation( registry, resource );

		Assert.True( registry.DisposeResourceOwner( resource ) );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.OwnerDisposed,
				TerminalRasterAnimationLossReason.ExplicitResourceDisposal
			),
			animation.ObserveState()
		);
		Assert.Equal( 0, registry.KnownFrameCount );
	}

	private static TerminalPersistentRasterAnimationState CreateAnimation(
		TerminalPersistentRasterAnimationRegistry registry,
		uint imageNumber
	) {
		return CreateAnimation(
			registry,
			CreateResource( imageNumber )
		);
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

	private static TerminalPersistentRasterResourceState CreateResource(
		uint imageNumber
	) {
		return new TerminalPersistentRasterResourceState(
			imageNumber,
			generation: 1,
			sourceWidth: 8,
			sourceHeight: 8
		);
	}
}
