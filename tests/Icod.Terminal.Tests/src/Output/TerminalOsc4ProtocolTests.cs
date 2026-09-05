namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies byte-exact OSC 4 indexed-palette framing and response parsing.
/// </summary>
public sealed class TerminalOsc4ProtocolTests {
	[Theory]
	[InlineData( 0, "\u001b]4;0;rgb:1234/5678/9abc\u001b\\" )]
	[InlineData( 9, "\u001b]4;9;rgb:1234/5678/9abc\u001b\\" )]
	[InlineData( 10, "\u001b]4;10;rgb:1234/5678/9abc\u001b\\" )]
	[InlineData( 99, "\u001b]4;99;rgb:1234/5678/9abc\u001b\\" )]
	[InlineData( 100, "\u001b]4;100;rgb:1234/5678/9abc\u001b\\" )]
	[InlineData( 255, "\u001b]4;255;rgb:1234/5678/9abc\u001b\\" )]
	public void SingleMutationUsesCanonicalDecimalIndexAndColor(
		int index,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			TerminalOsc4Protocol.CreateSetRequest(
				(byte)index,
				new TerminalColor( 0x1234, 0x5678, 0x9abc )
			)
		);
	}

	[Fact]
	public void BulkMutationUsesOneFrameAndPreservesEntryOrder() {
		TerminalPaletteColor[] entries = [
			new( 1, new TerminalColor( 0x1111, 0x2222, 0x3333 ) ),
			new( 255, new TerminalColor( 0xabcd, 0xef01, 0x2345 ) )
		];

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]4;1;rgb:1111/2222/3333;255;rgb:abcd/ef01/2345\u001b\\"
			),
			TerminalOsc4Protocol.CreateSetRequest( entries )
		);
	}

	[Fact]
	public void BulkMutationRejectsEmptyDuplicateAndOversizedCollections() {
		Assert.Throws<ArgumentException>(
			() => TerminalOsc4Protocol.CreateSetRequest(
				Array.Empty<TerminalPaletteColor>()
			)
		);
		Assert.Throws<ArgumentException>(
			() => TerminalOsc4Protocol.CreateSetRequest(
				[
					new TerminalPaletteColor( 7, default ),
					new TerminalPaletteColor( 7, TerminalColor.FromRgb8( 1, 2, 3 ) )
				]
			)
		);

		TerminalPaletteColor[] oversized = new TerminalPaletteColor[ 257 ];
		Assert.Throws<ArgumentException>(
			() => TerminalOsc4Protocol.CreateSetRequest( oversized )
		);
	}

	[Theory]
	[InlineData( 0, "\u001b]4;0;?\u001b\\" )]
	[InlineData( 255, "\u001b]4;255;?\u001b\\" )]
	public void QueryUsesCanonicalStFrame(
		int index,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			TerminalOsc4Protocol.CreateQueryRequest( (byte)index )
		);
	}

	[Theory]
	[InlineData( "\u001b]4;17;rgb:ff/00/80\u001b\\" )]
	[InlineData( "\u001b]4;17;rgb:ff/00/80\u0007" )]
	public void ObservationParsesSevenBitOscTerminators(
		string response
	) {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( response )
		);

		Assert.Equal(
			new TerminalColor( 0xffff, 0x0000, 0x8080 ),
			TerminalOsc4Protocol.ParseObservation( frame, 17 )
		);
	}

	[Fact]
	public void ObservationParsesC1OscResponse() {
		byte[] response = [
			0x9d,
			(byte)'4',
			(byte)';',
			(byte)'2',
			(byte)'5',
			(byte)'5',
			(byte)';',
			(byte)'#',
			(byte)'3',
			(byte)'a',
			(byte)'7',
			0x9c
		];

		Assert.Equal(
			new TerminalColor( 0x3000, 0xa000, 0x7000 ),
			TerminalOsc4Protocol.ParseObservation(
				new TerminalResponseFrame(
					TerminalResponseFrameKind.Osc,
					response
				),
				255
			)
		);
	}

	[Fact]
	public void WrongIndexAndMalformedColorAreRejected() {
		TerminalResponseFrame wrongIndex = new(
			TerminalResponseFrameKind.Osc,
			Encoding.ASCII.GetBytes( "\u001b]4;8;rgb:ffff/0000/0000\u001b\\" )
		);
		TerminalResponseFrame malformedColor = new(
			TerminalResponseFrameKind.Osc,
			Encoding.ASCII.GetBytes( "\u001b]4;7;red\u001b\\" )
		);

		Assert.Throws<FormatException>(
			() => TerminalOsc4Protocol.ParseObservation( wrongIndex, 7 )
		);
		Assert.Throws<FormatException>(
			() => TerminalOsc4Protocol.ParseObservation( malformedColor, 7 )
		);
	}
}
