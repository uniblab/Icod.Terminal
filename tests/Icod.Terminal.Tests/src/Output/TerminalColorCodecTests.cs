namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the frozen T130 terminal-color encoding and parsing grammar.
/// </summary>
public sealed class TerminalColorCodecTests {
	[Theory]
	[InlineData( 0x0000, 0x0000, 0x0000, "rgb:0000/0000/0000" )]
	[InlineData( 0xffff, 0xffff, 0xffff, "rgb:ffff/ffff/ffff" )]
	[InlineData( 0xffff, 0x0000, 0x0000, "rgb:ffff/0000/0000" )]
	[InlineData( 0x1234, 0x5678, 0x9abc, "rgb:1234/5678/9abc" )]
	[InlineData( 0xabcd, 0x00ef, 0x0102, "rgb:abcd/00ef/0102" )]
	public void EncoderUsesCanonicalFourDigitLowercaseRgb(
		int red,
		int green,
		int blue,
		string expected
	) {
		TerminalColor color = new(
			(ushort)red,
			(ushort)green,
			(ushort)blue
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			TerminalColorCodec.Encode( color )
		);
		Assert.Equal(
			expected,
			TerminalColorCodec.EncodeString( color )
		);
	}

	[Theory]
	[InlineData( "rgb:0/0/0", 0x0000, 0x0000, 0x0000 )]
	[InlineData( "rgb:f/0/8", 0xffff, 0x0000, 0x8888 )]
	[InlineData( "rgb:1/2/3", 0x1111, 0x2222, 0x3333 )]
	[InlineData( "rgb:00/80/ff", 0x0000, 0x8080, 0xffff )]
	[InlineData( "rgb:12/34/ab", 0x1212, 0x3434, 0xabab )]
	[InlineData( "rgb:000/800/fff", 0x0000, 0x8008, 0xffff )]
	[InlineData( "rgb:123/abc/def", 0x1231, 0xabca, 0xdefd )]
	[InlineData( "rgb:0000/8000/ffff", 0x0000, 0x8000, 0xffff )]
	[InlineData( "RGB:ABCD/00EF/0102", 0xabcd, 0x00ef, 0x0102 )]
	public void RgbParserNormalizesOneThroughFourDigitComponents(
		string specification,
		int red,
		int green,
		int blue
	) {
		TerminalColor expected = new(
			(ushort)red,
			(ushort)green,
			(ushort)blue
		);

		Assert.Equal(
			expected,
			TerminalColorCodec.Parse( specification )
		);
		Assert.True(
			TerminalColorCodec.TryParse(
				Encoding.ASCII.GetBytes( specification ),
				out TerminalColor parsed
			)
		);
		Assert.Equal( expected, parsed );
	}

	[Theory]
	[InlineData( "#000", 0x0000, 0x0000, 0x0000 )]
	[InlineData( "#3a7", 0x3000, 0xa000, 0x7000 )]
	[InlineData( "#F08", 0xf000, 0x0000, 0x8000 )]
	[InlineData( "#123456", 0x1200, 0x3400, 0x5600 )]
	[InlineData( "#abcdef", 0xab00, 0xcd00, 0xef00 )]
	[InlineData( "#123456789", 0x1230, 0x4560, 0x7890 )]
	[InlineData( "#ABCDEF012", 0xabc0, 0xdef0, 0x0120 )]
	[InlineData( "#123456789abc", 0x1234, 0x5678, 0x9abc )]
	public void HashParserTreatsComponentsAsMostSignificantBits(
		string specification,
		int red,
		int green,
		int blue
	) {
		Assert.Equal(
			new TerminalColor(
				(ushort)red,
				(ushort)green,
				(ushort)blue
			),
			TerminalColorCodec.Parse( specification )
		);
	}

	[Fact]
	public void RgbAndHashShorthandHaveDeliberatelyDifferentSemantics() {
		TerminalColor rgb = TerminalColorCodec.Parse( "rgb:3/a/7" );
		TerminalColor hash = TerminalColorCodec.Parse( "#3a7" );

		Assert.Equal( new TerminalColor( 0x3333, 0xaaaa, 0x7777 ), rgb );
		Assert.Equal( new TerminalColor( 0x3000, 0xa000, 0x7000 ), hash );
		Assert.NotEqual( rgb, hash );
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "red" )]
	[InlineData( "rgbi:1/0/0" )]
	[InlineData( "rgb(255,0,0)" )]
	[InlineData( "0xff0000" )]
	[InlineData( "rgb:/0/0" )]
	[InlineData( "rgb:f//0" )]
	[InlineData( "rgb:f/0/" )]
	[InlineData( "rgb:f/00/000" )]
	[InlineData( "rgb:fffff/00000/00000" )]
	[InlineData( "rgb:gg/00/00" )]
	[InlineData( "rgb:ff/00/00/11" )]
	[InlineData( "rgb:ff/00/00 " )]
	[InlineData( " rgb:ff/00/00" )]
	[InlineData( "rgb:ff/00/00x" )]
	[InlineData( "#" )]
	[InlineData( "#12" )]
	[InlineData( "#1234" )]
	[InlineData( "#12345" )]
	[InlineData( "#1234567" )]
	[InlineData( "#12345678" )]
	[InlineData( "#1234567890" )]
	[InlineData( "#123456789ab" )]
	[InlineData( "#123456789abcd" )]
	[InlineData( "#ggg" )]
	public void MalformedOrUnsupportedSpecificationsAreRejected(
		string specification
	) {
		Assert.False(
			TerminalColorCodec.TryParse(
				Encoding.ASCII.GetBytes( specification ),
				out TerminalColor color
			)
		);
		Assert.Equal( default, color );
		Assert.Throws<FormatException>(
			() => TerminalColorCodec.Parse( specification )
		);
	}

	[Fact]
	public void NonAsciiStringIsRejected() {
		Assert.Throws<FormatException>(
			() => TerminalColorCodec.Parse( "rgb:é/0/0" )
		);
	}

	[Fact]
	public void NullStringIsRejected() {
		Assert.Throws<ArgumentNullException>(
			() => TerminalColorCodec.Parse( (string)null! )
		);
	}

	[Fact]
	public void OverlongByteSpecificationIsRejectedBeforeParsing() {
		byte[] specification = Enumerable.Repeat(
			(byte)'0',
			TerminalColorCodec.MaximumSpecificationLength + 1
		).ToArray();

		Assert.False(
			TerminalColorCodec.TryParse(
				specification,
				out TerminalColor color
			)
		);
		Assert.Equal( default, color );
		Assert.Throws<FormatException>(
			() => TerminalColorCodec.Parse( specification )
		);
	}

	[Fact]
	public void CanonicalEncodingRoundTripsRepresentativeSixteenBitValues() {
		TerminalColor[] colors = [
			new TerminalColor( 0x0000, 0x0000, 0x0000 ),
			new TerminalColor( 0xffff, 0xffff, 0xffff ),
			new TerminalColor( 0x0001, 0x0100, 0x1000 ),
			new TerminalColor( 0x1234, 0x5678, 0x9abc ),
			new TerminalColor( 0xfffe, 0x8001, 0x7fff )
		];

		foreach ( TerminalColor color in colors ) {
			Assert.Equal(
				color,
				TerminalColorCodec.Parse( TerminalColorCodec.Encode( color ) )
			);
		}
	}
}
