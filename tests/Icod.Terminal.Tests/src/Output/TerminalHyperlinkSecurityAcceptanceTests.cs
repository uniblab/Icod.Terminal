namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Exercises the T49 OSC 8 injection and resource-boundary acceptance matrix.
/// </summary>
public sealed class TerminalHyperlinkSecurityAcceptanceTests {
	[Fact]
	public void UriRejectsEveryC0DelAndC1ControlCharacter() {
		List<char> characters = [];
		for ( int value = 0x00; value <= 0x1f; ++value ) {
			characters.Add( (char)value );
		}
		characters.Add( '\u007f' );
		for ( int value = 0x80; value <= 0x9f; ++value ) {
			characters.Add( (char)value );
		}

		foreach ( char character in characters ) {
			string uri = string.Concat(
				"https://example.com/a",
				character,
				"b"
			);

			Assert.Throws<ArgumentException>(
				() => TerminalHyperlinkEncoder.EncodeUri( uri )
			);
		}
	}

	[Fact]
	public void PercentEncodedEscapeLookingBytesRemainData() {
		const string uri = "https://example.com/%1b%5c%07";

		string encoded = TerminalHyperlinkEncoder.EncodeUri( uri );
		byte[] frame = OscWriter.EncodeHyperlinkBeginFrame(
			uri,
			"safe"
		);

		Assert.Equal(
			"https://example.com/%1B%5C%07",
			encoded
		);
		Assert.Equal( 2, frame.Count( static value => 0x1b == value ) );
		Assert.Equal( 0, frame.Count( static value => 0x07 == value ) );
		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( 0x5c, frame[ ^1 ] );
	}

	[Theory]
	[InlineData( "a:b" )]
	[InlineData( "a;b" )]
	[InlineData( "a=b" )]
	[InlineData( "a%b" )]
	[InlineData( "a@b" )]
	[InlineData( "a/b" )]
	[InlineData( "a?b" )]
	[InlineData( "a#b" )]
	[InlineData( "a b" )]
	[InlineData( "\u001b" )]
	[InlineData( "\u0007" )]
	[InlineData( "é" )]
	public void IdentifierCannotInjectParameterOrOscSyntax(
		string identifier
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeParameters( identifier )
		);
	}

	[Fact]
	public void UriReservedCharactersRemainInsideUriField() {
		const string uri = "https://example.com/a;b=c:@/x?y=1;z=2#frag";

		byte[] frame = OscWriter.EncodeHyperlinkBeginFrame(
			uri,
			"link"
		);
		string ascii = System.Text.Encoding.ASCII.GetString( frame );

		Assert.Contains( ";id=link;https://", ascii, StringComparison.Ordinal );
		Assert.Contains( "/a;b=c:@/x?y=1;z=2#frag", ascii, StringComparison.Ordinal );
		Assert.EndsWith( "\u001b\\", ascii, StringComparison.Ordinal );
	}

	[Fact]
	public void ExactUriAndIdentifierLimitsProduceOneCompleteSafeFrame() {
		const string prefix = "x:";
		string uri = prefix + new string(
			'a',
			TerminalHyperlinkEncoder.MaximumUriByteCount - prefix.Length
		);
		string identifier = new(
			'b',
			TerminalHyperlinkEncoder.MaximumIdentifierByteCount
		);

		byte[] frame = OscWriter.EncodeHyperlinkBeginFrame(
			uri,
			identifier
		);

		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( (byte)']', frame[ 1 ] );
		Assert.Equal( (byte)'8', frame[ 2 ] );
		Assert.Equal( 0x1b, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
		Assert.Equal( 2, frame.Count( static value => 0x1b == value ) );
	}

	[Fact]
	public void OneOverEitherResourceLimitIsRejectedBeforeFraming() {
		const string prefix = "x:";
		string oversizedUri = prefix + new string(
			'a',
			TerminalHyperlinkEncoder.MaximumUriByteCount - prefix.Length + 1
		);
		string oversizedIdentifier = new(
			'b',
			TerminalHyperlinkEncoder.MaximumIdentifierByteCount + 1
		);

		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeHyperlinkBeginFrame( oversizedUri )
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/",
				oversizedIdentifier
			)
		);
	}

	[Theory]
	[InlineData( "https://one@two@three/" )]
	[InlineData( "https://example.com:abc/" )]
	[InlineData( "https://example.com:80:90/" )]
	[InlineData( "https://2001:db8::1/" )]
	[InlineData( "https://[not-an-ipv6-address]/" )]
	[InlineData( "https://[fe80::1%25eth0]/" )]
	[InlineData( "https://[v.test]/" )]
	public void MalformedAuthorityCannotCrossTheSecurityBoundary(
		string uri
	) {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeHyperlinkBeginFrame( uri )
		);
	}
}
