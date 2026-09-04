namespace Icod.Terminal;

using System.Globalization;
using System.Net;
using System.Text;

/// <summary>
/// Identifies the native path grammar used by the deterministic OSC 7 location encoder.
/// </summary>
internal enum TerminalLocationPathKind {
	Posix = 0,
	WindowsDrive = 1,
	WindowsUnc = 2
}

/// <summary>
/// Converts structured native filesystem locations into deterministic RFC 8089
/// <c>file:</c> URI payloads without consulting the host operating system.
/// </summary>
internal static class TerminalLocationUriEncoder {
	internal const int MaximumEncodedUriByteCount = 16384;

	private const string FileSchemePrefix = "file://";
	private static readonly UTF8Encoding StrictUtf8 = new(
		false,
		true
	);

	/// <summary>
	/// Encodes a native absolute filesystem location as one canonical <c>file:</c> URI.
	/// </summary>
	internal static string EncodeFileUri(
		string path,
		TerminalLocationPathKind pathKind,
		string? authority = null
	) {
		ArgumentNullException.ThrowIfNull( path );
		ValidateWellFormedUnicode(
			path,
			nameof( path )
		);

		string encoded = pathKind switch {
			TerminalLocationPathKind.Posix => EncodePosixPath(
				path,
				authority
			),
			TerminalLocationPathKind.WindowsDrive => EncodeWindowsDrivePath(
				path,
				authority
			),
			TerminalLocationPathKind.WindowsUnc => EncodeWindowsUncPath(
				path,
				authority
			),
			_ => throw new ArgumentOutOfRangeException(
				nameof( pathKind ),
				pathKind,
				"Unknown terminal location path grammar."
			)
		};

		if ( MaximumEncodedUriByteCount < encoded.Length ) {
			throw new ArgumentException(
				string.Concat(
					"Encoded file URI payloads may not exceed ",
					MaximumEncodedUriByteCount.ToString( CultureInfo.InvariantCulture ),
					" bytes."
				),
				nameof( path )
			);
		}

		return encoded;
	}

	private static string EncodePosixPath(
		string path,
		string? authority
	) {
		if ( 0 == path.Length || '/' != path[ 0 ] ) {
			throw new ArgumentException(
				"A POSIX terminal location must be an absolute path beginning with '/'.",
				nameof( path )
			);
		}

		string encodedAuthority = ValidateAndReturnAuthority( authority );
		return string.Concat(
			FileSchemePrefix,
			encodedAuthority,
			EncodePath(
				path,
				static character => '/' == character
			)
		);
	}

	private static string EncodeWindowsDrivePath(
		string path,
		string? authority
	) {
		if ( path.StartsWith( "\\\\?\\", StringComparison.Ordinal )
			|| path.StartsWith( "\\\\.\\", StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"Windows device and extended namespace paths are not supported for OSC 7 publication.",
				nameof( path )
			);
		}
		if ( 3 > path.Length
			|| !IsAsciiLetter( path[ 0 ] )
			|| ':' != path[ 1 ]
			|| !IsWindowsSeparator( path[ 2 ] ) ) {
			throw new ArgumentException(
				"A Windows drive terminal location must be fully qualified, for example 'C:\\path'.",
				nameof( path )
			);
		}

		string encodedAuthority = ValidateAndReturnAuthority( authority );
		char drive = char.ToUpperInvariant( path[ 0 ] );
		string remainder = EncodePath(
			path[ 2.. ],
			IsWindowsSeparator
		);
		return string.Concat(
			FileSchemePrefix,
			encodedAuthority,
			"/",
			drive,
			":",
			remainder
		);
	}

	private static string EncodeWindowsUncPath(
		string path,
		string? authority
	) {
		if ( authority is not null ) {
			throw new ArgumentException(
				"UNC paths derive their URI authority from the UNC server name and may not specify a second authority.",
				nameof( authority )
			);
		}
		if ( path.StartsWith( "\\\\?\\", StringComparison.Ordinal )
			|| path.StartsWith( "\\\\.\\", StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"Windows device and extended namespace paths are not supported for OSC 7 publication.",
				nameof( path )
			);
		}
		if ( !path.StartsWith( "\\\\", StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"A UNC terminal location must begin with two backslashes.",
				nameof( path )
			);
		}

		int serverEnd = path.IndexOf( '\\', 2 );
		if ( 2 >= serverEnd ) {
			throw new ArgumentException(
				"A UNC terminal location must contain both a server and a share name.",
				nameof( path )
			);
		}

		int shareStart = serverEnd + 1;
		int shareEnd = path.IndexOf( '\\', shareStart );
		string server = path[ 2..serverEnd ];
		string share = 0 > shareEnd
			? path[ shareStart.. ]
			: path[ shareStart..shareEnd ];
		if ( 0 == share.Length ) {
			throw new ArgumentException(
				"A UNC terminal location must contain both a server and a share name.",
				nameof( path )
			);
		}

		string encodedAuthority = ValidateAndReturnAuthority( server );
		StringBuilder builder = new();
		builder.Append( FileSchemePrefix );
		builder.Append( encodedAuthority );
		builder.Append( '/' );
		AppendEncodedSegment(
			builder,
			share
		);

		if ( 0 <= shareEnd ) {
			builder.Append(
				EncodePath(
					path[ shareEnd.. ],
					IsWindowsSeparator
				)
			);
		}

		return builder.ToString();
	}

	private static string EncodePath(
		string path,
		Func<char, bool> isSeparator
	) {
		ArgumentNullException.ThrowIfNull( path );
		ArgumentNullException.ThrowIfNull( isSeparator );

		StringBuilder builder = new();
		int segmentStart = 0;
		for ( int index = 0; index < path.Length; ++index ) {
			if ( !isSeparator( path[ index ] ) ) {
				continue;
			}

			AppendEncodedSegment(
				builder,
				path[ segmentStart..index ]
			);
			builder.Append( '/' );
			segmentStart = index + 1;
		}

		AppendEncodedSegment(
			builder,
			path[ segmentStart.. ]
		);
		return builder.ToString();
	}

	private static void AppendEncodedSegment(
		StringBuilder builder,
		string segment
	) {
		ArgumentNullException.ThrowIfNull( builder );
		ArgumentNullException.ThrowIfNull( segment );

		byte[] bytes;
		try {
			bytes = StrictUtf8.GetBytes( segment );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"Terminal locations must contain well-formed Unicode text.",
				nameof( segment ),
				exception
			);
		}

		foreach ( byte value in bytes ) {
			if ( IsUnreservedAscii( value ) ) {
				builder.Append( (char)value );
				continue;
			}

			builder.Append( '%' );
			builder.Append( GetUpperHexDigit( value >> 4 ) );
			builder.Append( GetUpperHexDigit( value & 0x0f ) );
		}
	}

	private static string ValidateAndReturnAuthority(
		string? authority
	) {
		if ( authority is null ) {
			return string.Empty;
		}
		if ( 0 == authority.Length ) {
			throw new ArgumentException(
				"An explicit terminal location authority may not be empty.",
				nameof( authority )
			);
		}
		ValidateWellFormedUnicode(
			authority,
			nameof( authority )
		);
		if ( authority.Any( static character => 0x7f < character ) ) {
			throw new ArgumentException(
				"Internationalized terminal location authorities are not supported in 0.5.0.",
				nameof( authority )
			);
		}
		if ( authority.Contains( '@', StringComparison.Ordinal )
			|| authority.Contains( '/', StringComparison.Ordinal )
			|| authority.Contains( '\\', StringComparison.Ordinal )
			|| authority.Contains( '?', StringComparison.Ordinal )
			|| authority.Contains( '#', StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"A terminal location authority must contain only host information.",
				nameof( authority )
			);
		}

		if ( authority.StartsWith( '[', StringComparison.Ordinal )
			&& authority.EndsWith( ']', StringComparison.Ordinal ) ) {
			string literal = authority[ 1..^1 ];
			if ( IPAddress.TryParse( literal, out IPAddress? address )
				&& System.Net.Sockets.AddressFamily.InterNetworkV6 == address.AddressFamily ) {
				return authority;
			}

			throw new ArgumentException(
				"A bracketed terminal location authority must be a valid IPv6 literal.",
				nameof( authority )
			);
		}

		if ( authority.Contains( ':', StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"Ports and unbracketed IPv6 literals are not supported in terminal location authorities.",
				nameof( authority )
			);
		}
		if ( IPAddress.TryParse( authority, out IPAddress? parsedAddress ) ) {
			if ( System.Net.Sockets.AddressFamily.InterNetwork == parsedAddress.AddressFamily ) {
				return authority;
			}

			throw new ArgumentException(
				"IPv6 terminal location authorities must use bracketed URI-literal form.",
				nameof( authority )
			);
		}

		ValidateDnsName( authority );
		return authority;
	}

	private static void ValidateDnsName(
		string authority
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( authority );
		if ( 253 < authority.Length ) {
			throw new ArgumentException(
				"A terminal location DNS authority may not exceed 253 characters.",
				nameof( authority )
			);
		}

		string[] labels = authority.Split( '.' );
		foreach ( string label in labels ) {
			if ( 0 == label.Length || 63 < label.Length ) {
				throw new ArgumentException(
					"A terminal location DNS authority contains an invalid label length.",
					nameof( authority )
				);
			}
			if ( '-' == label[ 0 ] || '-' == label[ ^1 ] ) {
				throw new ArgumentException(
					"A terminal location DNS label may not begin or end with '-'.",
					nameof( authority )
				);
			}
			if ( label.Any(
				static character => !IsAsciiLetterOrDigit( character ) && '-' != character
			) ) {
				throw new ArgumentException(
					"A terminal location DNS authority contains unsupported characters.",
					nameof( authority )
				);
			}
		}
	}

	private static void ValidateWellFormedUnicode(
		string value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrWhiteSpace( parameterName );

		try {
			_ = StrictUtf8.GetByteCount( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"Terminal location text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}
	}

	private static bool IsWindowsSeparator(
		char character
	) {
		return '\\' == character || '/' == character;
	}

	private static bool IsAsciiLetter(
		char character
	) {
		return ( 'A' <= character && 'Z' >= character )
			|| ( 'a' <= character && 'z' >= character );
	}

	private static bool IsAsciiLetterOrDigit(
		char character
	) {
		return IsAsciiLetter( character )
			|| ( '0' <= character && '9' >= character );
	}

	private static bool IsUnreservedAscii(
		byte value
	) {
		return ( (byte)'A' <= value && (byte)'Z' >= value )
			|| ( (byte)'a' <= value && (byte)'z' >= value )
			|| ( (byte)'0' <= value && (byte)'9' >= value )
			|| (byte)'-' == value
			|| (byte)'.' == value
			|| (byte)'_' == value
			|| (byte)'~' == value;
	}

	private static char GetUpperHexDigit(
		int value
	) {
		return 10 > value
			? (char)( '0' + value )
			: (char)( 'A' + value - 10 )
		;
	}
}
