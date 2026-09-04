namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T53 typed-selection and bounded base64 primitives without terminal I/O.
/// </summary>
public sealed class TerminalOsc52PayloadCodecTests {
	[Theory]
	[InlineData( TerminalOsc52Selection.Clipboard, (byte)'c' )]
	[InlineData( TerminalOsc52Selection.Primary, (byte)'p' )]
	[InlineData( TerminalOsc52Selection.Secondary, (byte)'q' )]
	[InlineData( TerminalOsc52Selection.Select, (byte)'s' )]
	public void SelectionMappingIsExact(
		TerminalOsc52Selection selection,
		byte expected
	) {
		Assert.Equal(
			expected,
			TerminalOsc52SelectionEncoder.Encode( selection )
		);
	}

	[Fact]
	public void UnknownSelectionIsRejected() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalOsc52SelectionEncoder.Encode(
				(TerminalOsc52Selection)int.MaxValue
			)
		);
	}

	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 4 )]
	[InlineData( 2, 4 )]
	[InlineData( 3, 4 )]
	[InlineData( 4, 8 )]
	[InlineData( 65_535, 87_380 )]
	[InlineData( 65_536, 87_384 )]
	public void EncodedLengthCalculationIsExact(
		int decodedLength,
		int expectedEncodedLength
	) {
		Assert.Equal(
			expectedEncodedLength,
			TerminalOsc52PayloadCodec.GetEncodedLength( decodedLength )
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 65_537 )]
	public void EncodedLengthRejectsOutOfRangeDecodedSizes(
		int decodedLength
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalOsc52PayloadCodec.GetEncodedLength( decodedLength )
		);
	}

	[Fact]
	public void MaximumWriteFrameLengthMatchesFrozenLimit() {
		Assert.Equal(
			87_393,
			TerminalOsc52PayloadCodec.GetWriteFrameLength(
				TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes
			)
		);
		Assert.True(
			TerminalOsc52PayloadCodec.MaximumFrameBytes
				>= TerminalOsc52PayloadCodec.GetWriteFrameLength(
					TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes
				)
		);
	}

	[Theory]
	[InlineData( "", "" )]
	[InlineData( "f", "Zg==" )]
	[InlineData( "fo", "Zm8=" )]
	[InlineData( "foo", "Zm9v" )]
	[InlineData( "foobar", "Zm9vYmFy" )]
	public void EncoderUsesCanonicalRfc4648Base64(
		string value,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalOsc52PayloadCodec.Encode(
				Encoding.ASCII.GetBytes( value )
			)
		);
	}

	[Fact]
	public void EncoderAcceptsExactMaximumPayload() {
		byte[] payload = new byte[ TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes ];
		for ( int index = 0; index < payload.Length; index++ ) {
			payload[ index ] = (byte)( index & 0xff );
		}

		string encoded = TerminalOsc52PayloadCodec.Encode( payload );

		Assert.Equal(
			TerminalOsc52PayloadCodec.MaximumEncodedPayloadBytes,
			encoded.Length
		);
		Assert.Equal(
			payload,
			TerminalOsc52PayloadCodec.Decode(
				Encoding.ASCII.GetBytes( encoded )
			)
		);
	}

	[Fact]
	public void EncoderRejectsOneByteOverMaximumPayload() {
		byte[] payload = new byte[
			TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes + 1
		];

		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalOsc52PayloadCodec.Encode( payload )
		);
	}

	[Theory]
	[InlineData( "", 0 )]
	[InlineData( "Zg==", 1 )]
	[InlineData( "Zm8=", 2 )]
	[InlineData( "Zm9v", 3 )]
	[InlineData( "Zm9vYmFy", 6 )]
	public void DecoderLengthCalculationMatchesCanonicalPayload(
		string encoded,
		int expectedDecodedLength
	) {
		Assert.Equal(
			expectedDecodedLength,
			TerminalOsc52PayloadCodec.GetDecodedLength(
				Encoding.ASCII.GetBytes( encoded )
			)
		);
	}

	[Theory]
	[InlineData( "Z" )]
	[InlineData( "Zg=" )]
	[InlineData( "Zg===" )]
	[InlineData( "Z g=" )]
	[InlineData( "Zg=\n" )]
	[InlineData( "Zg-_" )]
	[InlineData( "=m9v" )]
	[InlineData( "Zm=v" )]
	[InlineData( "Z===" )]
	[InlineData( "Zh==" )]
	[InlineData( "Zm9=" )]
	public void DecoderRejectsMalformedOrNonCanonicalBase64(
		string encoded
	) {
		Assert.ThrowsAny<Exception>(
			() => TerminalOsc52PayloadCodec.Decode(
				Encoding.ASCII.GetBytes( encoded )
			)
		);
	}

	[Fact]
	public void DecoderRejectsEncodedPayloadOverMaximumBeforeAllocation() {
		byte[] encoded = Enumerable.Repeat(
			(byte)'A',
			TerminalOsc52PayloadCodec.MaximumEncodedPayloadBytes + 4
		).ToArray();

		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalOsc52PayloadCodec.Decode( encoded )
		);
	}

	[Fact]
	public void EmptyPayloadRoundTripsWithoutAllocationVisibleState() {
		byte[] decoded = TerminalOsc52PayloadCodec.Decode( [] );

		Assert.Empty( decoded );
		Assert.Equal( string.Empty, TerminalOsc52PayloadCodec.Encode( [] ) );
	}
}
