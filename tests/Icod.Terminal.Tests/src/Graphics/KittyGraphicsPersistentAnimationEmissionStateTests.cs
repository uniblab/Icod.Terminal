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

public sealed class KittyGraphicsPersistentAnimationEmissionStateTests {
	[Fact]
	public void NewStateHasNotStartedOrCommittedOutput() {
		KittyGraphicsPersistentAnimationEmissionState state = new();

		Assert.False( state.HasStarted );
		Assert.False( state.OutputCommitted );
	}

	[Fact]
	public void MarkStartedChangesOnlyStartedState() {
		KittyGraphicsPersistentAnimationEmissionState state = new();

		state.MarkStarted();

		Assert.True( state.HasStarted );
		Assert.False( state.OutputCommitted );
	}

	[Fact]
	public void MarkOutputCommittedAlsoMarksStarted() {
		KittyGraphicsPersistentAnimationEmissionState state = new();

		state.MarkOutputCommitted();

		Assert.True( state.HasStarted );
		Assert.True( state.OutputCommitted );
	}

	[Fact]
	public void RepeatedConcurrentMarksRemainMonotonic() {
		KittyGraphicsPersistentAnimationEmissionState state = new();

		Parallel.For(
			0,
			1024,
			index => {
				if ( 0 == ( index & 1 ) ) {
					state.MarkStarted();
				} else {
					state.MarkOutputCommitted();
				}
			}
		);

		Assert.True( state.HasStarted );
		Assert.True( state.OutputCommitted );
		state.MarkStarted();
		Assert.True( state.OutputCommitted );
	}
}
