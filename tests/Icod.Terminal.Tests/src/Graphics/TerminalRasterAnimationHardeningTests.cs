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
/// Adversarial T167 tests for bounded persistent-raster animation ownership.
/// </summary>
public sealed class TerminalRasterAnimationHardeningTests {
	[Fact]
	public void DirectResourceReleaseReclaimsAnimationCapacityBeforeNextAllocation() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterResourceState firstResource = ReserveResource( resources );
		TerminalPersistentRasterAnimationState firstAnimation = CreateAnimation(
			animations,
			firstResource
		);
		PublishOneAppend(
			animations,
			firstAnimation
		);
		Assert.Equal( 2, animations.KnownFrameCount );

		Assert.True( resources.TryReleaseResource( firstResource ) );
		Assert.Equal(
			TerminalRasterAnimationStatus.Released,
			firstAnimation.ObserveState().Status
		);
		Assert.Equal( 0, animations.KnownFrameCount );

		TerminalPersistentRasterResourceState secondResource = ReserveResource( resources );
		Assert.True( animations.TryGetOrCreate( secondResource, out _ ) );
		Assert.Equal( 1, animations.KnownFrameCount );
	}

	[Fact]
	public void DirectResourceMissingReclaimsAnimationCapacityBeforeNextAllocation() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterResourceState firstResource = ReserveResource( resources );
		TerminalPersistentRasterAnimationState firstAnimation = CreateAnimation(
			animations,
			firstResource
		);
		PublishOneAppend(
			animations,
			firstAnimation
		);
		Assert.Equal( 2, animations.KnownFrameCount );

		Assert.True( resources.InvalidateResource( firstResource ) );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.ResourceMissing
			),
			firstAnimation.ObserveState()
		);
		Assert.Equal( 0, animations.KnownFrameCount );

		TerminalPersistentRasterResourceState secondResource = ReserveResource( resources );
		Assert.True( animations.TryGetOrCreate( secondResource, out _ ) );
		Assert.Equal( 1, animations.KnownFrameCount );
	}

	[Fact]
	public void SequenceUncertainAnimationRetainsKnownFrameCapacity() {
		TerminalPersistentRasterRegistry resources = new();
		TerminalPersistentRasterAnimationRegistry animations = new();
		TerminalPersistentRasterAnimationState animation = CreateAnimation(
			animations,
			ReserveResource( resources )
		);
		PublishOneAppend(
			animations,
			animation
		);

		Assert.True( animation.TryMarkSequenceUncertain() );
		Assert.Equal( 2, animations.KnownFrameCount );
		Assert.True( animations.OwnsFrame( animation, animation.RootFrame ) );
		Assert.False( animations.TryReserveAppend( animation, out _ ) );
	}

	private static TerminalPersistentRasterResourceState ReserveResource(
		TerminalPersistentRasterRegistry registry
	) {
		Assert.True(
			registry.TryReserveResource(
				sourceWidth: 8,
				sourceHeight: 8,
				out TerminalPersistentRasterResourceState? resource
			)
		);
		return Assert.IsType<TerminalPersistentRasterResourceState>( resource );
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

	private static void PublishOneAppend(
		TerminalPersistentRasterAnimationRegistry registry,
		TerminalPersistentRasterAnimationState animation
	) {
		Assert.True(
			registry.TryReserveAppend(
				animation,
				out TerminalPersistentRasterAnimationRegistry.AppendReservation? reservation
			)
		);
		Assert.NotNull( reservation );
		Assert.True( registry.TryPublishAppend( reservation, out _ ) );
	}
}
