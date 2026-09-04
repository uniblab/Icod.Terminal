namespace Icod.Terminal;

using System.Globalization;
using System.Text;

/// <summary>
/// Validates and canonicalizes caller-supplied OSC 8 hyperlink URI and parameter data
/// without performing terminal I/O or URI activation.
/// </summary>
internal static class TerminalHyperlinkEncoder {
	internal const int MaximumUriByteCount = 2083;
	internal const int MaximumIdentifierByteCount = 128;

	private static readonly UTF8Encoding StrictUtf8 = new(
		false,
		true
	);

	/// <summary>
	/// Validates one non-empty absolute RFC 3986 URI and normalizes percent-escape
	/// hexadecimal digits to uppercase without decoding the URI.
	/// </summary>
	internal static string EncodeUri(
		string uri
	) {
		ArgumentNullException.ThrowIfNull( uri );
		ValidateWellFormedUnicode(
			uri,
			nameof( uri )
		);
		if ( 0 == uri.Length ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink target URI may not be empty.",
				nameof( uri )
			);
		}
		if ( MaximumUriByteCount < uri.Length ) {
			throw new ArgumentException(
				string.Concat(
					"OSC 8 hyperlink URIs may not exceed ",
					MaximumUriByteCount.ToString( CultureInfo.InvariantCulture ),
					" bytes."
				),
				nameof( uri )
			);
		}

		int schemeEnd = FindAndValidateScheme( uri );
		ValidateGenericUriSyntax(
			uri,
			schemeEnd + 1
		);

		StringBuilder normalized = new( uri.Length );
		for ( int index = 0; index < uri.Length; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter(
				character,
				nameof( uri )
			);
			if ( '%' != character ) {
				normalized.Append( character );
				continue;
			}

			if ( index + 2 >= uri.Length
				|| !IsHexDigit( uri[ index + 1 ] )
				|| !IsHexDigit( uri[ index + 2 ] ) ) {
				throw new ArgumentException(
					"OSC 8 hyperlink URIs must contain only complete percent escapes of the form %HH.",
					nameof( uri )
				);
			}

			normalized.Append( '%' );
			normalized.Append( char.ToUpperInvariant( uri[ index + 1 ] ) );
			normalized.Append( char.ToUpperInvariant( uri[ index + 2 ] ) );
			index += 2;
		}

		return normalized.ToString();
	}

	/// <summary>
	/// Encodes the optional OSC 8 <c>id</c> semantic parameter.
	/// </summary>
	internal static string EncodeParameters(
		string? identifier
	) {
		if ( string.IsNullOrEmpty( identifier ) ) {
			return string.Empty;
		}

		ValidateWellFormedUnicode(
			identifier,
			nameof( identifier )
		);
		if ( MaximumIdentifierByteCount < identifier.Length ) {
			throw new ArgumentException(
				string.Concat(
					"OSC 8 hyperlink identifiers may not exceed ",
					MaximumIdentifierByteCount.ToString( CultureInfo.InvariantCulture ),
					" bytes."
				),
				nameof( identifier )
			);
		}

		foreach ( char character in identifier ) {
			if ( !IsUnreservedAscii( character ) ) {
				throw new ArgumentException(
					"OSC 8 hyperlink identifiers may contain only RFC 3986 unreserved ASCII characters.",
					nameof( identifier )
				);
			}
		}

		return string.Concat(
			"id=",
			identifier
		);
	}

	private static int FindAndValidateScheme(
		string uri
	) {
		ArgumentNullException.ThrowIfNull( uri );

		int colon = uri.IndexOf( ':' );
		if ( 1 > colon ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink target must be an absolute URI with a valid scheme.",
				nameof( uri )
			);
		}
		if ( !IsAsciiLetter( uri[ 0 ] ) ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink URI scheme must begin with an ASCII letter.",
				nameof( uri )
			);
		}

		for ( int index = 1; index < colon; ++index ) {
			char character = uri[ index ];
			if ( !IsAsciiLetterOrDigit( character )
				&& '+' != character
				&& '-' != character
				&& '.' != character ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains an invalid scheme character.",
					nameof( uri )
				);
			}
		}

		return colon;
	}

	private static void ValidateGenericUriSyntax(
		string uri,
		int contentStart
	) {
		ArgumentNullException.ThrowIfNull( uri );
		if ( 0 > contentStart || uri.Length < contentStart ) {
			throw new ArgumentOutOfRangeException( nameof( contentStart ) );
		}

		int fragment = uri.IndexOf( '#', contentStart );
		if ( 0 <= fragment
			&& 0 <= uri.IndexOf( '#', fragment + 1 ) ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink URI may contain at most one fragment delimiter.",
				nameof( uri )
			);
		}

		int querySearchEnd = 0 <= fragment
			? fragment
			: uri.Length;
		int query = uri.IndexOf( '?', contentStart, querySearchEnd - contentStart );

		int hierarchyEnd = 0 <= query
			? query
			: querySearchEnd;
		if ( contentStart + 2 <= hierarchyEnd
			&& '/' == uri[ contentStart ]
			&& '/' == uri[ contentStart + 1 ] ) {
			int authorityStart = contentStart + 2;
			int authorityEnd = FindFirstDelimiter(
				uri,
				authorityStart,
				hierarchyEnd,
				'/'
			);
			ValidateAuthority(
				uri,
				authorityStart,
				authorityEnd
			);
			ValidatePath(
				uri,
				authorityEnd,
				hierarchyEnd
			);
		} else {
			ValidatePath(
				uri,
				contentStart,
				hierarchyEnd
			);
		}

		if ( 0 <= query ) {
			ValidateQueryOrFragment(
				uri,
				query + 1,
				querySearchEnd
			);
		}
		if ( 0 <= fragment ) {
			ValidateQueryOrFragment(
				uri,
				fragment + 1,
				uri.Length
			);
		}
	}

	private static int FindFirstDelimiter(
		string value,
		int start,
		int end,
		char delimiter
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( 0 > start || start > end || value.Length < end ) {
			throw new ArgumentOutOfRangeException( nameof( start ) );
		}

		int found = value.IndexOf(
			delimiter,
			start,
			end - start
		);
		return 0 <= found
			? found
			: end
		;
	}

	private static void ValidateAuthority(
		string uri,
		int start,
		int end
	) {
		ArgumentNullException.ThrowIfNull( uri );

		bool inIpv6Literal = false;
		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter(
				character,
				nameof( uri )
			);
			if ( '[' == character ) {
				if ( inIpv6Literal ) {
					throw new ArgumentException(
						"An OSC 8 hyperlink URI contains malformed authority brackets.",
						nameof( uri )
					);
				}
				inIpv6Literal = true;
				continue;
			}
			if ( ']' == character ) {
				if ( !inIpv6Literal ) {
					throw new ArgumentException(
						"An OSC 8 hyperlink URI contains malformed authority brackets.",
						nameof( uri )
					);
				}
				inIpv6Literal = false;
				continue;
			}
			if ( '%' == character ) {
				ValidatePercentEscapeAt(
					uri,
					index,
					end
				);
				index += 2;
				continue;
			}
			if ( !IsAuthorityCharacter( character ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains a character not permitted in its authority component.",
					nameof( uri )
				);
			}
		}

		if ( inIpv6Literal ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink URI contains an unterminated authority literal.",
				nameof( uri )
			);
		}
	}

	private static void ValidatePath(
		string uri,
		int start,
		int end
	) {
		ArgumentNullException.ThrowIfNull( uri );

		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter(
				character,
				nameof( uri )
			);
			if ( '%' == character ) {
				ValidatePercentEscapeAt(
					uri,
					index,
					end
				);
				index += 2;
				continue;
			}
			if ( '/' != character && !IsPChar( character ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains a character not permitted in its path component.",
					nameof( uri )
				);
			}
		}
	}

	private static void ValidateQueryOrFragment(
		string uri,
		int start,
		int end
	) {
		ArgumentNullException.ThrowIfNull( uri );

		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter(
				character,
				nameof( uri )
			);
			if ( '%' == character ) {
				ValidatePercentEscapeAt(
					uri,
					index,
					end
				);
				index += 2;
				continue;
			}
			if ( '/' != character
				&& '?' != character
				&& !IsPChar( character ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains a character not permitted in its query or fragment component.",
					nameof( uri )
				);
			}
		}
	}

	private static void ValidatePercentEscapeAt(
		string uri,
		int index,
		int end
	) {
		ArgumentNullException.ThrowIfNull( uri );
		if ( index + 2 >= end
			|| !IsHexDigit( uri[ index + 1 ] )
			|| !IsHexDigit( uri[ index + 2 ] ) ) {
			throw new ArgumentException(
				"OSC 8 hyperlink URIs must contain only complete percent escapes of the form %HH.",
				nameof( uri )
			);
		}
	}

	private static void ValidateAsciiUriCharacter(
		char character,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( parameterName );
		if ( 0x20 >= character
			|| 0x7f <= character ) {
			throw new ArgumentException(
				"OSC 8 hyperlink URIs must contain only visible ASCII URI text and may not contain whitespace or controls.",
				parameterName
			);
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
				"OSC 8 hyperlink text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}
	}

	private static bool IsAuthorityCharacter(
		char character
	) {
		return IsUnreservedAscii( character )
			|| IsSubDelimiter( character )
			|| ':' == character
			|| '@' == character;
	}

	private static bool IsPChar(
		char character
	) {
		return IsUnreservedAscii( character )
			|| IsSubDelimiter( character )
			|| ':' == character
			|| '@' == character;
	}

	private static bool IsSubDelimiter(
		char character
	) {
		return '!' == character
			|| '$' == character
			|| '&' == character
			|| '\'' == character
			|| '(' == character
			|| ')' == character
			|| '*' == character
			|| '+' == character
			|| ',' == character
			|| ';' == character
			|| '=' == character;
	}

	private static bool IsUnreservedAscii(
		char character
	) {
		return IsAsciiLetterOrDigit( character )
			|| '-' == character
			|| '.' == character
			|| '_' == character
			|| '~' == character;
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

	private static bool IsHexDigit(
		char character
	) {
		return ( '0' <= character && '9' >= character )
			|| ( 'A' <= character && 'F' >= character )
			|| ( 'a' <= character && 'f' >= character );
	}
}
