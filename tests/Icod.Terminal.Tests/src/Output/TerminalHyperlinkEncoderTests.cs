namespace Icod.Terminal.Tests.Output;

using System.Globalization;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the host-independent T45 OSC 8 URI and id parameter encoding contract.
/// </summary>
public sealed class TerminalHyperlinkEncoderTests {
	[Theory]
	[InlineData( "https://example.com/", "https://example.com/" )]
	[InlineData( "http://example.com/path?q=v#part", "http://example.com/path?q=v#part" )]
	[InlineData( "ftp://user@example.com/file", "ftp://user@example.com/file" )]
	[InlineData( "mailto:user@example.com", "mailto:user@example.com" )]
	[InlineData( "file:///tmp/report.txt", "file:///tmp/report.txt" )]
	[InlineData( "custom-scheme:value", "custom-scheme:value" )]
	[InlineData( "https://example.com/a%2fb%3f?q=%7e#x%2a", "https://example.com/a%2Fb%3F?q=%7E#x%2A" )]
	[InlineData( "https://example.com/a;b=c:@/x?y/z?u=v#p/q?x", "https://example.com/a;b=c:@/x?y/z?u=v#p/q?x" )]
	[InlineData( "scheme:/path", "scheme:/path" )]
	[InlineData( "scheme:", "scheme:" )]
	public void EncodesValidAbsoluteUris(
		string uri,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalHyperlinkEncoder.EncodeUri( uri )
		);
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "relative/path" )]
	[InlineData( "/absolute/path" )]
	[InlineData( "1http://example.com" )]
	[InlineData( "ht_tp://example.com" )]
	[InlineData( "https://example.com/a b" )]
	[InlineData( "https://example.com/é" )]
	[InlineData( "https://example.com/%" )]
	[InlineData( "https://example.com/%2" )]
	[InlineData( "https://example.com/%GG" )]
	[InlineData( "https://example.com/\u001b\\" )]
	[InlineData( "https://example.com/\u0007" )]
	[InlineData( "https://example.com/#one#two" )]
	[InlineData( "https://exa[mple.com/" )]
	[InlineData( "https://exa]mple.com/" )]
	public void RejectsInvalidUris(
		string uri
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeUri( uri )
		);
	}

	[Fact]
	public void RejectsMalformedUnicodeUri() {
		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeUri(
				"https://example.com/\ud800"
			)
		);
	}

	[Fact]
	public void AcceptsExactUriLimit() {
		const string prefix = "x:";
		string uri = prefix + new string(
			'a',
			TerminalHyperlinkEncoder.MaximumUriByteCount - prefix.Length
		);

		string encoded = TerminalHyperlinkEncoder.EncodeUri( uri );

		Assert.Equal(
			TerminalHyperlinkEncoder.MaximumUriByteCount,
			encoded.Length
		);
	}

	[Fact]
	public void RejectsOneByteOverUriLimit() {
		const string prefix = "x:";
		string uri = prefix + new string(
			'a',
			TerminalHyperlinkEncoder.MaximumUriByteCount - prefix.Length + 1
		);

		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeUri( uri )
		);
	}

	[Theory]
	[InlineData( null, "" )]
	[InlineData( "", "" )]
	[InlineData( "link", "id=link" )]
	[InlineData( "A-Z_a.b~9", "id=A-Z_a.b~9" )]
	public void EncodesIdentifierParameters(
		string? identifier,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalHyperlinkEncoder.EncodeParameters( identifier )
		);
	}

	[Theory]
	[InlineData( "a:b" )]
	[InlineData( "a=b" )]
	[InlineData( "a;b" )]
	[InlineData( "a%b" )]
	[InlineData( "a b" )]
	[InlineData( "é" )]
	[InlineData( "\u001b" )]
	[InlineData( "\u007f" )]
	public void RejectsInvalidIdentifiers(
		string identifier
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeParameters( identifier )
		);
	}

	[Fact]
	public void RejectsMalformedUnicodeIdentifier() {
		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeParameters( "\ud800" )
		);
	}

	[Fact]
	public void AcceptsExactIdentifierLimit() {
		string identifier = new(
			'a',
			TerminalHyperlinkEncoder.MaximumIdentifierByteCount
		);

		Assert.Equal(
			"id=" + identifier,
			TerminalHyperlinkEncoder.EncodeParameters( identifier )
		);
	}

	[Fact]
	public void RejectsOneByteOverIdentifierLimit() {
		string identifier = new(
			'a',
			TerminalHyperlinkEncoder.MaximumIdentifierByteCount + 1
		);

		Assert.Throws<ArgumentException>(
			() => TerminalHyperlinkEncoder.EncodeParameters( identifier )
		);
	}

	[Fact]
	public void OutputDoesNotDependOnCurrentCulture() {
		CultureInfo originalCulture = CultureInfo.CurrentCulture;
		CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
		try {
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo( "tr-TR" );
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo( "tr-TR" );

			Assert.Equal(
				"HTTP://EXAMPLE.COM/%2F%3A",
				TerminalHyperlinkEncoder.EncodeUri(
					"HTTP://EXAMPLE.COM/%2f%3a"
				)
			);
			Assert.Equal(
				"id=I",
				TerminalHyperlinkEncoder.EncodeParameters( "I" )
			);
		} finally {
			CultureInfo.CurrentCulture = originalCulture;
			CultureInfo.CurrentUICulture = originalUiCulture;
		}
	}

	[Fact]
	public void AcceptedAsciiCannotCreateIdentifierDelimiters() {
		for ( int value = 0; value <= 0x7f; ++value ) {
			char character = (char)value;
			string identifier = character.ToString();
			bool expected = char.IsAsciiLetterOrDigit( character )
				|| '-' == character
				|| '.' == character
				|| '_' == character
				|| '~' == character;

			if ( expected ) {
				string encoded = TerminalHyperlinkEncoder.EncodeParameters( identifier );
				Assert.Equal( "id=" + identifier, encoded );
				Assert.DoesNotContain( ':', encoded[ 3.. ] );
				Assert.DoesNotContain( ';', encoded[ 3.. ] );
				Assert.DoesNotContain( '=', encoded[ 3.. ] );
			} else {
				Assert.Throws<ArgumentException>(
					() => TerminalHyperlinkEncoder.EncodeParameters( identifier )
				);
			}
		}
	}
}
