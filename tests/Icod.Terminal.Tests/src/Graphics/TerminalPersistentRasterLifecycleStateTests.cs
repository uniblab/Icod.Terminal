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
/// Defines the packed monotonic ownership-state transition contract used by 1.14.
/// </summary>
public sealed class TerminalPersistentRasterLifecycleStateTests {
	[Fact]
	public void NewStateIsCurrentWithNoLossReason() {
		TerminalPersistentRasterLifecycleState state = new();

		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			state.Observe()
		);
	}

	[Theory]
	[InlineData( TerminalRasterOwnershipLossReason.SessionStateLost )]
	[InlineData( TerminalRasterOwnershipLossReason.ResourceMissing )]
	[InlineData( TerminalRasterOwnershipLossReason.ParentPlacementLost )]
	public void CurrentCanBecomeStaleExactlyOnce(
		TerminalRasterOwnershipLossReason reason
	) {
		TerminalPersistentRasterLifecycleState state = new();

		Assert.True( state.TryMarkStale( reason ) );
		Assert.False( state.TryMarkStale( TerminalRasterOwnershipLossReason.SessionStateLost ) );
		Assert.False( state.TryMarkReleased( TerminalRasterOwnershipLossReason.AncestorReleased ) );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				reason
			),
			state.Observe()
		);
	}

	[Theory]
	[InlineData( TerminalRasterOwnershipLossReason.AncestorReleased )]
	[InlineData( TerminalRasterOwnershipLossReason.ResourceReleased )]
	public void CurrentCanBecomeReleasedExactlyOnce(
		TerminalRasterOwnershipLossReason reason
	) {
		TerminalPersistentRasterLifecycleState state = new();

		Assert.True( state.TryMarkReleased( reason ) );
		Assert.False( state.TryMarkReleased( TerminalRasterOwnershipLossReason.ResourceReleased ) );
		Assert.False( state.TryMarkStale( TerminalRasterOwnershipLossReason.SessionStateLost ) );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Released,
				reason
			),
			state.Observe()
		);
	}

	[Theory]
	[InlineData( TerminalRasterOwnershipLossReason.None )]
	[InlineData( TerminalRasterOwnershipLossReason.AncestorReleased )]
	[InlineData( TerminalRasterOwnershipLossReason.ResourceReleased )]
	[InlineData( TerminalRasterOwnershipLossReason.ExplicitDisposal )]
	public void StaleTransitionRejectsNonStaleReason(
		TerminalRasterOwnershipLossReason reason
	) {
		TerminalPersistentRasterLifecycleState state = new();

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => state.TryMarkStale( reason )
		);
	}

	[Theory]
	[InlineData( TerminalRasterOwnershipLossReason.None )]
	[InlineData( TerminalRasterOwnershipLossReason.SessionStateLost )]
	[InlineData( TerminalRasterOwnershipLossReason.ResourceMissing )]
	[InlineData( TerminalRasterOwnershipLossReason.ParentPlacementLost )]
	[InlineData( TerminalRasterOwnershipLossReason.ExplicitDisposal )]
	public void ReleasedTransitionRejectsNonReleaseReason(
		TerminalRasterOwnershipLossReason reason
	) {
		TerminalPersistentRasterLifecycleState state = new();

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => state.TryMarkReleased( reason )
		);
	}
}
