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
/// Verifies the D173 common tightly packed owned raw raster model.
/// </summary>
public sealed class TerminalRasterImageTests {
	[Fact]
	public void Rgb24IsTightlyPackedAndImplicitlyOpaque() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			2,
			[
				1, 2, 3,
				4, 5, 6,
				7, 8, 9,
				10, 11, 12
			]
		);

		Assert.Equal( 2, image.Width );
		Assert.Equal( 2, image.Height );
		Assert.Equal( TerminalRasterPixelFormat.Rgb24, image.PixelFormat );
		Assert.Equal( 3, image.BytesPerPixel );
		Assert.Equal( 6, image.RowByteLength );
		Assert.Equal( 4, image.PixelCount );
		Assert.Empty( image.Palette.ToArray() );
		Assert.Equal(
			new byte[] { 7, 8, 9, 10, 11, 12 },
			image.GetRowBytes( 1 ).ToArray()
		);
		Assert.Equal(
			new TerminalRasterColor(
				4,
				5,
				6,
				255
			),
			image.GetPixelColor(
				1,
				0
			)
		);
	}

	[Fact]
	public void Rgba32PreservesStraightAlphaExactly() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			2,
			1,
			[
				10, 20, 30, 0,
				40, 50, 60, 127
			]
		);

		Assert.Equal( TerminalRasterPixelFormat.Rgba32, image.PixelFormat );
		Assert.Equal( 4, image.BytesPerPixel );
		Assert.Equal( 8, image.RowByteLength );
		Assert.Equal(
			new TerminalRasterColor(
				10,
				20,
				30,
				0
			),
			image.GetPixelColor(
				0,
				0
			)
		);
		Assert.Equal(
			new TerminalRasterColor(
				40,
				50,
				60,
				127
			),
			image.GetPixelColor(
				1,
				0
			)
		);
	}

	[Fact]
	public void Indexed8ResolvesCopiedPaletteEntriesIncludingAlpha() {
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 1, 2, 3, 255 ),
			new TerminalRasterColor( 4, 5, 6, 64 )
		];
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			2,
			2,
			[ 0, 1, 1, 0 ],
			palette
		);

		Assert.Equal( TerminalRasterPixelFormat.Indexed8, image.PixelFormat );
		Assert.Equal( 1, image.BytesPerPixel );
		Assert.Equal( 2, image.RowByteLength );
		Assert.Equal( 2, image.Palette.Length );
		Assert.Equal(
			new TerminalRasterColor(
				4,
				5,
				6,
				64
			),
			image.GetPixelColor(
				1,
				0
			)
		);
	}

	[Fact]
	public void ConstructionCopiesCallerPixelAndPaletteStorage() {
		byte[] rgba = [ 10, 20, 30, 40 ];
		TerminalRasterImage direct = TerminalRasterImage.CreateRgba32(
			1,
			1,
			rgba
		);
		rgba[ 0 ] = 200;
		rgba[ 3 ] = 201;

		Assert.Equal(
			new TerminalRasterColor(
				10,
				20,
				30,
				40
			),
			direct.GetPixelColor(
				0,
				0
			)
		);

		byte[] indices = [ 0 ];
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 1, 2, 3, 4 )
		];
		TerminalRasterImage indexed = TerminalRasterImage.CreateIndexed8(
			1,
			1,
			indices,
			palette
		);
		indices[ 0 ] = 99;
		palette[ 0 ] = new TerminalRasterColor( 9, 9, 9, 9 );

		Assert.Equal(
			new TerminalRasterColor(
				1,
				2,
				3,
				4
			),
			indexed.GetPixelColor(
				0,
				0
			)
		);
	}

	[Theory]
	[InlineData( 0, 1 )]
	[InlineData( 1, 0 )]
	[InlineData( 16385, 1 )]
	[InlineData( 1, 16385 )]
	[InlineData( 4097, 4097 )]
	public void InvalidOrExcessiveDimensionsAreRejected(
		int width,
		int height
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalRasterImage.CreateRgb24(
				width,
				height,
				ReadOnlySpan<byte>.Empty
			)
		);
	}

	[Theory]
	[InlineData( 5 )]
	[InlineData( 7 )]
	public void DirectRasterRequiresExactTightlyPackedLength(
		int byteCount
	) {
		byte[] pixels = new byte[ byteCount ];
		Assert.Throws<ArgumentException>(
			() => TerminalRasterImage.CreateRgb24(
				2,
				1,
				pixels
			)
		);
	}

	[Theory]
	[InlineData( 1 )]
	[InlineData( 3 )]
	public void IndexedRasterRequiresExactTightlyPackedLength(
		int byteCount
	) {
		byte[] pixels = new byte[ byteCount ];
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 0, 0, 0 )
		];

		Assert.Throws<ArgumentException>(
			() => TerminalRasterImage.CreateIndexed8(
				2,
				1,
				pixels,
				palette
			)
		);
	}

	[Fact]
	public void IndexedRasterRequiresBoundedNonEmptyPalette() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalRasterImage.CreateIndexed8(
				1,
				1,
				[ 0 ],
				ReadOnlySpan<TerminalRasterColor>.Empty
			)
		);

		TerminalRasterColor[] tooMany = Enumerable.Repeat(
			new TerminalRasterColor( 0, 0, 0 ),
			TerminalRasterImage.MaximumPaletteEntries + 1
		).ToArray();
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalRasterImage.CreateIndexed8(
				1,
				1,
				[ 0 ],
				tooMany
			)
		);
	}

	[Fact]
	public void IndexedRasterRejectsPaletteReferencesThatDoNotExist() {
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 0, 0, 0 ),
			new TerminalRasterColor( 255, 255, 255 )
		];

		Assert.Throws<ArgumentException>(
			() => TerminalRasterImage.CreateIndexed8(
				2,
				1,
				[ 0, 2 ],
				palette
			)
		);
	}

	[Fact]
	public void RowAndPixelCoordinatesAreBounded() {
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetRowBytes( -1 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetRowBytes( 1 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetPixelColor(
				-1,
				0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetPixelColor(
				1,
				0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetPixelColor(
				0,
				-1
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => image.GetPixelColor(
				0,
				1
			)
		);
	}
}
