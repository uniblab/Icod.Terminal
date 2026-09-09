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
namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the D172 canonical Sixel grammar and command codec.
/// </summary>
public sealed class SixelCodecTests {
	[Fact]
	public void CanonicalDcsParametersAreExplicitAndBackgroundPreserving() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "0;1;0" ),
			SixelCodec.EncodeCanonicalDcsParameters()
		);
	}

	[Theory]
	[InlineData( 1, 1, "\"1;1;1;1" )]
	[InlineData( 80, 24, "\"1;1;80;24" )]
	[InlineData( 1000000, 1000000, "\"1;1;1000000;1000000" )]
	public void RasterAttributesUseSquarePixelsAndExplicitExtent(
		int width,
		int height,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			SixelCodec.EncodeRasterAttributes(
				width,
				height
			)
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1000001 )]
	public void RasterDimensionsOutsideSyntaxCeilingAreRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRasterAttributes(
				value,
				1
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRasterAttributes(
				1,
				value
			)
		);
	}

	[Theory]
	[InlineData( 0, '?' )]
	[InlineData( 1, '@' )]
	[InlineData( 63, '~' )]
	public void SixelValuesMapDirectlyToPrintableDataCharacters(
		int value,
		char expected
	) {
		Assert.Equal(
			(byte)expected,
			SixelCodec.EncodeDataCharacter( value )
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 64 )]
	public void SixelDataValuesOutsideSixBitsAreRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeDataCharacter( value )
		);
	}

	[Theory]
	[InlineData( 1, 0, "!1?" )]
	[InlineData( 12, 63, "!12~" )]
	[InlineData( 1000000, 1, "!1000000@" )]
	public void RepeatCommandsAreByteExact(
		int count,
		int value,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			SixelCodec.EncodeRepeat(
				count,
				value
			)
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1000001 )]
	public void RepeatCountOutsideSyntaxCeilingIsRejected(
		int count
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRepeat(
				count,
				0
			)
		);
	}

	[Theory]
	[InlineData( 0, "#0" )]
	[InlineData( 255, "#255" )]
	public void ColorSelectionIsByteExact(
		int register,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			SixelCodec.EncodeColorSelection( register )
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 256 )]
	public void ColorRegistersOutsideProtocolRangeAreRejected(
		int register
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeColorSelection( register )
		);
	}

	[Theory]
	[InlineData( 0, 0, 0, 0, "#0;2;0;0;0" )]
	[InlineData( 7, 100, 50, 1, "#7;2;100;50;1" )]
	[InlineData( 255, 100, 100, 100, "#255;2;100;100;100" )]
	public void RgbColorDefinitionsUseProtocolPercentageCoordinates(
		int register,
		int red,
		int green,
		int blue,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			SixelCodec.EncodeRgbColorDefinition(
				register,
				red,
				green,
				blue
			)
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 101 )]
	public void RgbComponentsOutsideProtocolRangeAreRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRgbColorDefinition(
				0,
				value,
				0,
				0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRgbColorDefinition(
				0,
				0,
				value,
				0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SixelCodec.EncodeRgbColorDefinition(
				0,
				0,
				0,
				value
			)
		);
	}

	[Fact]
	public void GraphicsMovementBytesRemainCanonical() {
		Assert.Equal( (byte)'$', SixelCodec.GraphicsCarriageReturn );
		Assert.Equal( (byte)'-', SixelCodec.GraphicsNewLine );
	}

	[Fact]
	public void TinyGeneratedSixelVectorComposesThroughCanonicalDcsWriter() {
		byte[] raster = SixelCodec.EncodeRasterAttributes(
			2,
			6
		);
		byte[] color = SixelCodec.EncodeRgbColorDefinition(
			0,
			100,
			0,
			0
		);
		byte[] selection = SixelCodec.EncodeColorSelection( 0 );
		byte[] payload = raster
			.Concat( color )
			.Concat( selection )
			.Concat(
				[
					SixelCodec.EncodeDataCharacter( 63 ),
					SixelCodec.EncodeDataCharacter( 63 )
				]
			)
			.ToArray();

		byte[] frame = DcsWriter.EncodeFrame(
			SixelCodec.EncodeCanonicalDcsParameters(),
			ReadOnlySpan<byte>.Empty,
			(byte)'q',
			payload
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001bP0;1;0q\"1;1;2;6#0;2;100;0;0#0~~\u001b\\"
			),
			frame
		);

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			new TerminalResponseFrame(
				TerminalResponseFrameKind.Dcs,
				frame
			)
		);
		Assert.Equal( TerminalControlFamily.Dcs, structure.Family );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "0;1;0" ),
			structure.ParameterBytes.ToArray()
		);
		Assert.Empty( structure.IntermediateBytes.ToArray() );
		Assert.Equal( (byte)'q', structure.FinalByte );
		Assert.Equal( payload, structure.PayloadBytes.ToArray() );
		Assert.Equal(
			TerminalStringTerminatorKind.SevenBitSt,
			structure.TerminatorKind
		);
	}
}
