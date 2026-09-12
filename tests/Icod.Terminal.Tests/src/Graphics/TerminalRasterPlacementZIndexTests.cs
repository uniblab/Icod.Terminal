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
/// Verifies the public signed z-order contract for persistent raster placements.
/// </summary>
public sealed class TerminalRasterPlacementZIndexTests {
	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	[InlineData( 1 )]
	[InlineData( int.MinValue )]
	[InlineData( int.MaxValue )]
	public void ZIndexPreservesSignedIntValues(
		int value
	) {
		TerminalRasterPlacementOptions options = new() {
			ZIndex = value
		};

		Assert.Equal( value, options.ZIndex );
	}

	[Fact]
	public void ZIndexDefaultsToNull() {
		TerminalRasterPlacementOptions options = new();

		Assert.Null( options.ZIndex );
	}
}
