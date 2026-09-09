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
/// Verifies D179 resource bounds for deliberately low-compressibility Sixel output.
/// </summary>
public sealed class SixelEncoderResourceBoundTests {
	[Fact]
	public void MaximumWidthAlternatingRowKeepsEverySegmentBounded() {
		int width = TerminalRasterImage.MaximumDimension;
		byte[] pixels = new byte[ width ];
		for ( int x = 0; x < width; x++ ) {
			pixels[ x ] = checked( (byte)( x & 1 ) );
		}

		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			width,
			1,
			pixels,
			[
				new TerminalRasterColor( 0, 0, 0 ),
				new TerminalRasterColor( 255, 255, 255 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );
		ReadOnlyMemory<byte>[] segments = SixelEncoder.EncodePayloadSegments( image ).ToArray();

		int largestSegment = segments.Max( static segment => segment.Length );
		Assert.InRange(
			largestSegment,
			width,
			width + 4
		);
		Assert.All(
			segments,
			segment => Assert.InRange(
				segment.Length,
				1,
				width + 4
			)
		);
		Assert.Contains(
			segments,
			segment => segment.Length >= width
		);
	}
}
