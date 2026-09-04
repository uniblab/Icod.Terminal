namespace Icod.Terminal.Tests.Output;

using System.Globalization;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the host-independent T38 native-path to file-URI contract.
/// </summary>
public sealed class TerminalLocationUriEncoderTests {
	[Theory]
	[InlineData( "/", "file:///" )]
	[InlineData( "/usr/local/src", "file:///usr/local/src" )]
	[InlineData( "/home/alice/My Project", "file:///home/alice/My%20Project" )]
	[InlineData( "/literal%20name/#/?", "file:///literal%2520name/%23/%3F" )]
	[InlineData( "/é/猫", "file:///%C3%A9/%E7%8C%AB" )]
	[InlineData( "/a/./b/../c/", "file:///a/./b/../c/" )]
	[InlineData( "/a//b", "file:///a//b" )]
	public void EncodesPosixPaths(
		string path,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalLocationUriEncoder.EncodeFileUri(
				path,
				TerminalLocationPathKind.Posix
			)
		);
	}

	[Theory]
	[InlineData( "C:\\", "file:///C:/" )]
	[InlineData( "c:\\Temp", "file:///C:/Temp" )]
	[InlineData( "C:\\Development\\Icod", "file:///C:/Development/Icod" )]
	[InlineData( "C:/Development/Icod/", "file:///C:/Development/Icod/" )]
	[InlineData( "C:\\My Project\\literal%20", "file:///C:/My%20Project/literal%2520" )]
	public void EncodesWindowsDrivePaths(
		string path,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalLocationUriEncoder.EncodeFileUri(
				path,
				TerminalLocationPathKind.WindowsDrive
			)
		);
	}

	[Theory]
	[InlineData( "\\\\server\\share", "file://server/share" )]
	[InlineData( "\\\\server\\share\\", "file://server/share/" )]
	[InlineData( "\\\\server\\share\\My Project\\猫", "file://server/share/My%20Project/%E7%8C%AB" )]
	public void EncodesUncPaths(
		string path,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalLocationUriEncoder.EncodeFileUri(
				path,
				TerminalLocationPathKind.WindowsUnc
			)
		);
	}

	[Theory]
	[InlineData( "example.com", "file://example.com/usr/src" )]
	[InlineData( "localhost", "file://localhost/usr/src" )]
	[InlineData( "127.0.0.1", "file://127.0.0.1/usr/src" )]
	[InlineData( "[2001:db8::1]", "file://[2001:db8::1]/usr/src" )]
	public void EncodesExplicitAuthorities(
		string authority,
		string expected
	) {
		Assert.Equal(
			expected,
			TerminalLocationUriEncoder.EncodeFileUri(
				"/usr/src",
				TerminalLocationPathKind.Posix,
				authority
			)
		);
	}

	[Theory]
	[InlineData( "relative/path", 0 )]
	[InlineData( "", 0 )]
	[InlineData( "C:relative", 1 )]
	[InlineData( "C|\\legacy", 1 )]
	[InlineData( "\\rooted", 1 )]
	[InlineData( "\\\\?\\C:\\Temp", 1 )]
	[InlineData( "\\\\.\\C:\\Temp", 1 )]
	[InlineData( "server\\share", 2 )]
	[InlineData( "\\\\server", 2 )]
	[InlineData( "\\\\server\\", 2 )]
	public void RejectsInvalidNativePaths(
		string path,
		int pathKindValue
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				path,
				(TerminalLocationPathKind)pathKindValue
			)
		);
	}

	[Theory]
	[InlineData( "/bad/\u0000" )]
	[InlineData( "/bad/\u0007" )]
	[InlineData( "/bad/\u001b" )]
	[InlineData( "/bad/\u007f" )]
	[InlineData( "/bad/\u0085" )]
	[InlineData( "/bad/\u009c" )]
	public void RejectsControlCharactersInPaths(
		string path
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				path,
				TerminalLocationPathKind.Posix
			)
		);
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "user@example.com" )]
	[InlineData( "example.com:22" )]
	[InlineData( "example.com/path" )]
	[InlineData( "example.com?query" )]
	[InlineData( "example.com#fragment" )]
	[InlineData( "example%20.com" )]
	[InlineData( "-example.com" )]
	[InlineData( "example-.com" )]
	[InlineData( "münchen.example" )]
	[InlineData( "2001:db8::1" )]
	[InlineData( "[not-ipv6]" )]
	[InlineData( "[fe80::1%eth0]" )]
	[InlineData( "[fe80::1%25eth0]" )]
	public void RejectsInvalidExplicitAuthorities(
		string authority
	) {
		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				"/usr/src",
				TerminalLocationPathKind.Posix,
				authority
			)
		);
	}

	[Fact]
	public void RejectsExplicitAuthorityForUncPath() {
		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				"\\\\server\\share",
				TerminalLocationPathKind.WindowsUnc,
				"other.example"
			)
		);
	}

	[Fact]
	public void EncodesSupplementaryUnicodeAsStrictUtf8() {
		Assert.Equal(
			"file:///emoji/%F0%9F%98%80",
			TerminalLocationUriEncoder.EncodeFileUri(
				"/emoji/😀",
				TerminalLocationPathKind.Posix
			)
		);
	}

	[Fact]
	public void RejectsMalformedUnicode() {
		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				"/bad/\ud800",
				TerminalLocationPathKind.Posix
			)
		);
	}

	[Fact]
	public void AcceptsExactEncodedUriLimit() {
		const int fixedLength = 8;
		string path = "/" + new string(
			'a',
			TerminalLocationUriEncoder.MaximumEncodedUriByteCount - fixedLength
		);

		string encoded = TerminalLocationUriEncoder.EncodeFileUri(
			path,
			TerminalLocationPathKind.Posix
		);

		Assert.Equal(
			TerminalLocationUriEncoder.MaximumEncodedUriByteCount,
			encoded.Length
		);
	}

	[Fact]
	public void RejectsOneByteOverEncodedUriLimit() {
		const int fixedLength = 8;
		string path = "/" + new string(
			'a',
			TerminalLocationUriEncoder.MaximumEncodedUriByteCount - fixedLength + 1
		);

		Assert.Throws<ArgumentException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				path,
				TerminalLocationPathKind.Posix
			)
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
				"file:///I/%C4%B0/%25",
				TerminalLocationUriEncoder.EncodeFileUri(
					"/I/İ/%",
					TerminalLocationPathKind.Posix
				)
			);
			Assert.Equal(
				"file:///I:/src",
				TerminalLocationUriEncoder.EncodeFileUri(
					"i:\\src",
					TerminalLocationPathKind.WindowsDrive
				)
			);
		} finally {
			CultureInfo.CurrentCulture = originalCulture;
			CultureInfo.CurrentUICulture = originalUiCulture;
		}
	}

	[Fact]
	public void RejectsUnknownPathKind() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalLocationUriEncoder.EncodeFileUri(
				"/usr/src",
				(TerminalLocationPathKind)99
			)
		);
	}
}
