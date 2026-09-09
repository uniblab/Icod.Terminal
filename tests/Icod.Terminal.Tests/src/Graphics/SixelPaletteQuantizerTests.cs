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
/// Verifies D174 deterministic bounded Sixel palette conversion.
/// </summary>
public sealed class SixelPaletteQuantizerTests {
	[Fact]
	public void OpaqueIndexedInputPreservesPaletteAndIndicesExactly() {
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 10, 20, 30 ),
			new TerminalRasterColor( 40, 50, 60 )
		];
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			3,
			1,
			[ 1, 0, 1 ],
			palette
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			2
		);

		Assert.Equal( palette, result.Palette.ToArray() );
		Assert.Equal( new byte[] { 1, 0, 1 }, result.Indices.ToArray() );
		Assert.False( result.HasTransparency );
		Assert.Empty( result.TransparentMask.ToArray() );
	}

	[Fact]
	public void ExactRgbColorsUseFirstAppearanceOrderWithoutLoss() {
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			4,
			1,
			[
				0, 0, 255,
				255, 0, 0,
				0, 0, 255,
				0, 255, 0
			]
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			4
		);

		Assert.Equal(
			new[] {
				new TerminalRasterColor( 0, 0, 255 ),
				new TerminalRasterColor( 255, 0, 0 ),
				new TerminalRasterColor( 0, 255, 0 )
			},
			result.Palette.ToArray()
		);
		Assert.Equal(
			new byte[] { 0, 1, 0, 2 },
			result.Indices.ToArray()
		);
	}

	[Fact]
	public void FullyTransparentPixelsDoNotConsumePaletteEntries() {
		TerminalRasterImage source = TerminalRasterImage.CreateRgba32(
			2,
			1,
			[
				255, 0, 0, 0,
				0, 0, 255, 255
			]
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			1
		);

		Assert.Equal(
			new[] {
				new TerminalRasterColor( 0, 0, 255 )
			},
			result.Palette.ToArray()
		);
		Assert.Equal( new byte[] { 0, 0 }, result.Indices.ToArray() );
		Assert.Equal( new byte[] { 1, 0 }, result.TransparentMask.ToArray() );
		Assert.True( result.HasTransparency );
		Assert.True( result.IsTransparent( 0 ) );
		Assert.False( result.IsTransparent( 1 ) );
	}

	[Fact]
	public void AllTransparentRasterProducesNoPalette() {
		TerminalRasterImage source = TerminalRasterImage.CreateRgba32(
			2,
			1,
			[
				1, 2, 3, 0,
				4, 5, 6, 0
			]
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source
		);

		Assert.Empty( result.Palette.ToArray() );
		Assert.Equal( new byte[] { 0, 0 }, result.Indices.ToArray() );
		Assert.Equal( new byte[] { 1, 1 }, result.TransparentMask.ToArray() );
	}

	[Theory]
	[InlineData( 1 )]
	[InlineData( 127 )]
	[InlineData( 254 )]
	public void FractionalRgbaAlphaIsRejected(
		int alpha
	) {
		TerminalRasterImage source = TerminalRasterImage.CreateRgba32(
			1,
			1,
			[ 10, 20, 30, checked( (byte)alpha ) ]
		);

		Assert.Throws<NotSupportedException>(
			() => SixelPaletteQuantizer.Quantize( source )
		);
	}

	[Fact]
	public void FractionalIndexedAlphaIsRejectedWhenReferenced() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			1,
			1,
			[ 0 ],
			[
				new TerminalRasterColor( 10, 20, 30, 128 )
			]
		);

		Assert.Throws<NotSupportedException>(
			() => SixelPaletteQuantizer.Quantize( source )
		);
	}

	[Fact]
	public void TransparentIndexedEntriesUseMaskInsteadOfColorRegister() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 1 ],
			[
				new TerminalRasterColor( 9, 9, 9, 0 ),
				new TerminalRasterColor( 100, 110, 120 )
			]
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			1
		);

		Assert.Equal(
			new[] {
				new TerminalRasterColor( 100, 110, 120 )
			},
			result.Palette.ToArray()
		);
		Assert.Equal( new byte[] { 1, 0 }, result.TransparentMask.ToArray() );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 257 )]
	public void PaletteCeilingMustFitSixelRegisterRange(
		int maximumColors
	) {
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);

		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelPaletteQuantizer.Quantize(
				source,
				maximumColors
			)
		);
	}

	[Fact]
	public void Full256EntryIndexedPalettePassesThrough() {
		TerminalRasterColor[] palette = Enumerable.Range(
			0,
			256
		).Select(
			value => new TerminalRasterColor(
				checked( (byte)value ),
				checked( (byte)( 255 - value ) ),
				checked( (byte)( value ^ 0x55 ) )
			)
		).ToArray();
		byte[] pixels = Enumerable.Range(
			0,
			256
		).Select(
			value => checked( (byte)value )
		).ToArray();
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			256,
			1,
			pixels,
			palette
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			256
		);

		Assert.Equal( 256, result.Palette.Length );
		Assert.Equal( palette, result.Palette.ToArray() );
		Assert.Equal( pixels, result.Indices.ToArray() );
	}

	[Fact]
	public void HighEntropyInputStaysWithinRequestedPaletteCeiling() {
		byte[] pixels = new byte[ 64 * 64 * 3 ];
		for ( int pixel = 0; pixel < 64 * 64; pixel++ ) {
			int offset = pixel * 3;
			pixels[ offset ] = checked( (byte)( ( pixel * 37 ) % 256 ) );
			pixels[ offset + 1 ] = checked( (byte)( ( pixel * 73 ) % 256 ) );
			pixels[ offset + 2 ] = checked( (byte)( ( pixel * 109 ) % 256 ) );
		}
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			64,
			64,
			pixels
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			16
		);

		Assert.InRange( result.Palette.Length, 1, 16 );
		Assert.Equal( source.PixelCount, result.Indices.Length );
		Assert.All(
			result.Indices.ToArray(),
			value => Assert.True( value < result.Palette.Length )
		);
	}

	[Fact]
	public void GradientQuantizationIsDeterministicAcrossRepeatedRuns() {
		byte[] pixels = new byte[ 256 * 3 ];
		for ( int value = 0; value < 256; value++ ) {
			int offset = value * 3;
			pixels[ offset ] = checked( (byte)value );
			pixels[ offset + 1 ] = checked( (byte)( 255 - value ) );
			pixels[ offset + 2 ] = checked( (byte)( value / 2 ) );
		}
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			256,
			1,
			pixels
		);

		SixelPaletteImage first = SixelPaletteQuantizer.Quantize(
			source,
			8
		);
		SixelPaletteImage second = SixelPaletteQuantizer.Quantize(
			source,
			8
		);

		Assert.Equal( first.Palette.ToArray(), second.Palette.ToArray() );
		Assert.Equal( first.Indices.ToArray(), second.Indices.ToArray() );
		Assert.Equal(
			first.TransparentMask.ToArray(),
			second.TransparentMask.ToArray()
		);
	}

	[Fact]
	public void OneColorCeilingReducesMultiColorInputDeterministically() {
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			4,
			1,
			[
				0, 0, 0,
				255, 0, 0,
				0, 255, 0,
				0, 0, 255
			]
		);

		SixelPaletteImage result = SixelPaletteQuantizer.Quantize(
			source,
			1
		);

		Assert.Single( result.Palette.ToArray() );
		Assert.All(
			result.Indices.ToArray(),
			value => Assert.Equal( (byte)0, value )
		);
	}

	[Fact]
	public void TransparencyLookupRejectsInvalidPixelIndex() {
		TerminalRasterImage source = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
		SixelPaletteImage result = SixelPaletteQuantizer.Quantize( source );

		Assert.Throws<ArgumentOutOfRangeException>(
			() => result.IsTransparent( -1 )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => result.IsTransparent( 1 )
		);
	}
}
