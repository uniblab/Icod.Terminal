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

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies A187 raster semantic parity boundaries across Kitty Graphics and Sixel.
/// </summary>
public sealed class TerminalRasterSemanticParityTests {
	[Fact]
	public void KittyUsesIntrinsicDimensionsWithoutPlacementScalingOrCursorOverride() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			3,
			new byte[ 18 ]
		);
		KittyRasterData raster = KittyRasterAdapter.Adapt( image );

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
		);
		string wire = Encoding.ASCII.GetString( payload.Span );
		int separator = wire.IndexOf( ';' );
		Assert.True( 0 < separator );
		string controlData = wire[ ..separator ];

		Assert.Contains( "s=2", controlData, StringComparison.Ordinal );
		Assert.Contains( "v=3", controlData, StringComparison.Ordinal );
		Assert.DoesNotContain( ",c=", controlData, StringComparison.Ordinal );
		Assert.DoesNotContain( ",r=", controlData, StringComparison.Ordinal );
		Assert.DoesNotContain( ",C=", controlData, StringComparison.Ordinal );
	}

	[Fact]
	public void SixelUsesIntrinsicDimensionsWithoutPlacementPolicy() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			3,
			new byte[ 18 ]
		);
		SixelPaletteImage paletteImage = SixelPaletteQuantizer.Quantize( image );

		byte[] payload = SixelEncoder.EncodePayloadSegments( paletteImage )
			.SelectMany( static segment => segment.ToArray() )
			.ToArray();
		string wire = Encoding.ASCII.GetString( payload );

		Assert.StartsWith( "\"1;1;2;3", wire, StringComparison.Ordinal );
	}

	[Fact]
	public void FullyTransparentKittyRgbaPreservesSourceBytes() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			1,
			1,
			[ 10, 20, 30, 0 ]
		);
		KittyRasterData raster = KittyRasterAdapter.Adapt( image );

		ReadOnlyMemory<byte> payload = Assert.Single(
			KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster )
		);

		Assert.Equal(
			"Ga=T,f=32,s=1,v=1,t=d,m=0,q=2;ChQeAA==",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void FullyTransparentSixelPreservesExtentWithoutPainting() {
		byte[] pixels = new byte[ 7 * 4 ];
		for ( int offset = 0; offset < pixels.Length; offset += 4 ) {
			pixels[ offset ] = 10;
			pixels[ offset + 1 ] = 20;
			pixels[ offset + 2 ] = 30;
			pixels[ offset + 3 ] = 0;
		}
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			1,
			7,
			pixels
		);
		SixelPaletteImage paletteImage = SixelPaletteQuantizer.Quantize( image );

		byte[] payload = SixelEncoder.EncodePayloadSegments( paletteImage )
			.SelectMany( static segment => segment.ToArray() )
			.ToArray();

		Assert.Equal(
			"\"1;1;1;7-",
			Encoding.ASCII.GetString( payload )
		);
	}

	[Fact]
	public void FractionalAlphaRemainsRepresentableOnlyByKittyRawPath() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			1,
			1,
			[ 1, 2, 3, 128 ]
		);

		KittyRasterData kitty = KittyRasterAdapter.Adapt( image );
		Assert.Equal( KittyGraphicsPixelFormat.Rgba32, kitty.PixelFormat );
		Assert.Equal( [ 1, 2, 3, 128 ], kitty.Bytes.ToArray() );
		Assert.Throws<NotSupportedException>(
			() => SixelPaletteQuantizer.Quantize( image )
		);
	}
}
