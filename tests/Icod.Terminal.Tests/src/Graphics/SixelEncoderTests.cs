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
/// Verifies the D175 deterministic bounded Sixel payload encoder.
/// </summary>
public sealed class SixelEncoderTests {
	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 0 )]
	[InlineData( 127, 50 )]
	[InlineData( 128, 50 )]
	[InlineData( 254, 100 )]
	[InlineData( 255, 100 )]
	public void Rgb8ToProtocolPercentageUsesNearestIntegerRounding(
		int channel,
		int expected
	) {
		Assert.Equal(
			expected,
			SixelEncoder.ConvertRgb8ToPercentage(
				checked( (byte)channel )
			)
		);
	}

	[Fact]
	public void OnlyGloballyUsedPaletteRegistersAreDefined() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			1,
			1,
			[ 1 ],
			[
				new TerminalRasterColor( 0, 0, 0 ),
				new TerminalRasterColor( 255, 128, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize(
			source,
			2
		);

		Assert.Equal(
			new[] {
				"\"1;1;1;1",
				"#1;2;100;50;0",
				"#1@"
			},
			EncodeSegmentsAsText( image )
		);
	}

	[Fact]
	public void MultiColorBandUsesAscendingPassesAndGraphicsCarriageReturn() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 1 ],
			[
				new TerminalRasterColor( 0, 0, 0 ),
				new TerminalRasterColor( 255, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize(
			source,
			2
		);

		Assert.Equal(
			new[] {
				"\"1;1;2;1",
				"#0;2;0;0;0",
				"#1;2;100;0;0",
				"#0@",
				"$",
				"#1?@"
			},
			EncodeSegmentsAsText( image )
		);
	}

	[Fact]
	public void ColorPassRetainsLeadingAndInteriorZerosButOmitsTrailingZeros() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			5,
			1,
			[ 1, 0, 1, 0, 1 ],
			[
				new TerminalRasterColor( 0, 0, 0 ),
				new TerminalRasterColor( 255, 255, 255 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize(
			source,
			2
		);

		string[] segments = EncodeSegmentsAsText( image );
		Assert.Equal( "#0?@?@", segments[ 3 ] );
		Assert.Equal( "$", segments[ 4 ] );
		Assert.Equal( "#1@?@?@", segments[ 5 ] );
	}

	[Fact]
	public void SixRowsProduceOneFullSixelMask() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			1,
			6,
			Enumerable.Repeat(
				(byte)0,
				6
			).ToArray(),
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );

		Assert.Equal(
			"#0~",
			EncodeSegmentsAsText( image )[ 2 ]
		);
	}

	[Fact]
	public void PartialFinalBandRestartsAtLowBitAfterOneGraphicsNewLine() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			1,
			7,
			Enumerable.Repeat(
				(byte)0,
				7
			).ToArray(),
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );

		Assert.Equal(
			new[] {
				"\"1;1;1;7",
				"#0;2;0;0;0",
				"#0~",
				"-",
				"#0@"
			},
			EncodeSegmentsAsText( image )
		);
	}

	[Fact]
	public void EmptyMiddleBandStillAdvancesExactlyOnce() {
		byte[] pixels = new byte[ 13 * 4 ];
		for ( int row = 0; row < 13; row++ ) {
			int offset = row * 4;
			pixels[ offset ] = 255;
			pixels[ offset + 1 ] = 0;
			pixels[ offset + 2 ] = 0;
			pixels[ offset + 3 ] = row is 0 or 12
				? byte.MaxValue
				: byte.MinValue
			;
		}
		TerminalRasterImage source = TerminalRasterImage.CreateRgba32(
			1,
			13,
			pixels
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );

		Assert.Equal(
			new[] {
				"\"1;1;1;13",
				"#0;2;100;0;0",
				"#0@",
				"-",
				"-",
				"#0@"
			},
			EncodeSegmentsAsText( image )
		);
	}

	[Fact]
	public void AllTransparentImageEmitsOnlyExtentAndInterBandMovement() {
		byte[] pixels = new byte[ 13 * 4 ];
		TerminalRasterImage source = TerminalRasterImage.CreateRgba32(
			1,
			13,
			pixels
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );

		Assert.Equal(
			new[] {
				"\"1;1;1;13",
				"-",
				"-"
			},
			EncodeSegmentsAsText( image )
		);
	}

	[Theory]
	[InlineData( 1, "#0@" )]
	[InlineData( 3, "#0@@@" )]
	[InlineData( 4, "#0!4@" )]
	public void RepeatSyntaxIsUsedOnlyWhenStrictlyShorter(
		int width,
		string expectedPass
	) {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			width,
			1,
			Enumerable.Repeat(
				(byte)0,
				width
			).ToArray(),
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );
		string[] segments = EncodeSegmentsAsText( image );

		Assert.Equal( expectedPass, segments[ ^1 ] );
	}

	[Fact]
	public void MaximumWidthColorPassRemainsABoundedSegment() {
		int width = TerminalRasterImage.MaximumDimension;
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			width,
			1,
			new byte[ width ],
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );
		ReadOnlyMemory<byte>[] segments = SixelEncoder.EncodePayloadSegments( image ).ToArray();

		Assert.Equal(
			"#0!16384@",
			Encoding.ASCII.GetString( segments[ ^1 ].Span )
		);
		Assert.All(
			segments,
			segment => Assert.InRange(
				segment.Length,
				1,
				width + 4
			)
		);
	}

	[Fact]
	public void PayloadSegmentationIsDeterministicAcrossRepeatedEnumeration() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			4,
			7,
			[
				0, 1, 0, 1,
				1, 0, 1, 0,
				0, 1, 0, 1,
				1, 0, 1, 0,
				0, 1, 0, 1,
				1, 0, 1, 0,
				0, 1, 0, 1
			],
			[
				new TerminalRasterColor( 10, 20, 30 ),
				new TerminalRasterColor( 200, 210, 220 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );

		byte[][] first = EncodeSegments( image );
		byte[][] second = EncodeSegments( image );

		Assert.Equal( first.Length, second.Length );
		for ( int index = 0; index < first.Length; index++ ) {
			Assert.Equal( first[ index ], second[ index ] );
		}
	}

	[Fact]
	public void TinyPayloadComposesIntoCanonicalSevenBitDcsFrame() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 0 ],
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		SixelPaletteImage image = SixelPaletteQuantizer.Quantize( source );
		byte[] payload = SixelEncoder.EncodePayloadSegments( image )
			.SelectMany( segment => segment.ToArray() )
			.ToArray();
		byte[] frame = DcsWriter.EncodeFrame(
			SixelCodec.EncodeCanonicalDcsParameters(),
			ReadOnlySpan<byte>.Empty,
			(byte)'q',
			payload
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001bP0;1;0q\"1;1;2;1#0;2;0;0;0#0@@\u001b\\"
			),
			frame
		);
	}

	private static string[] EncodeSegmentsAsText(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );
		return SixelEncoder.EncodePayloadSegments( image )
			.Select( segment => Encoding.ASCII.GetString( segment.Span ) )
			.ToArray();
	}

	private static byte[][] EncodeSegments(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );
		return SixelEncoder.EncodePayloadSegments( image )
			.Select( segment => segment.ToArray() )
			.ToArray();
	}
}
