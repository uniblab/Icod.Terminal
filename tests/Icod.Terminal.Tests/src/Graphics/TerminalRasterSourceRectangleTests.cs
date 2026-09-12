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
/// Verifies the public pixel-space source rectangle value contract.
/// </summary>
public sealed class TerminalRasterSourceRectangleTests {
	[Fact]
	public void ConstructorPreservesPixelRectangle() {
		TerminalRasterSourceRectangle rectangle = new(
			2,
			3,
			4,
			5
		);

		Assert.Equal( 2, rectangle.X );
		Assert.Equal( 3, rectangle.Y );
		Assert.Equal( 4, rectangle.Width );
		Assert.Equal( 5, rectangle.Height );
	}

	[Fact]
	public void ConstructorAcceptsScalarBoundary() {
		TerminalRasterSourceRectangle rectangle = new(
			TerminalRasterImage.MaximumDimension - 1,
			TerminalRasterImage.MaximumDimension - 1,
			1,
			1
		);

		Assert.Equal(
			TerminalRasterImage.MaximumDimension - 1,
			rectangle.X
		);
		Assert.Equal(
			TerminalRasterImage.MaximumDimension - 1,
			rectangle.Y
		);
	}

	[Theory]
	[InlineData( -1, 0, 1, 1 )]
	[InlineData( 0, -1, 1, 1 )]
	[InlineData( 0, 0, 0, 1 )]
	[InlineData( 0, 0, -1, 1 )]
	[InlineData( 0, 0, 1, 0 )]
	[InlineData( 0, 0, 1, -1 )]
	public void ConstructorRejectsNegativeCoordinatesAndNonPositiveExtents(
		int x,
		int y,
		int width,
		int height
	) {
		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalRasterSourceRectangle(
				x,
				y,
				width,
				height
			)
		);
	}

	[Theory]
	[InlineData( TerminalRasterImage.MaximumDimension, 0, 1, 1 )]
	[InlineData( 0, TerminalRasterImage.MaximumDimension, 1, 1 )]
	[InlineData( 0, 0, TerminalRasterImage.MaximumDimension + 1, 1 )]
	[InlineData( 0, 0, 1, TerminalRasterImage.MaximumDimension + 1 )]
	public void ConstructorRejectsScalarsBeyondMaximumDimension(
		int x,
		int y,
		int width,
		int height
	) {
		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalRasterSourceRectangle(
				x,
				y,
				width,
				height
			)
		);
	}
}
