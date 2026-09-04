namespace Icod.Terminal;

using System.Globalization;
using System.Net;
using System.Net.Sockets;
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
				$"OSC 8 hyperlink URIs may not exceed {MaximumUriByteCount.ToString( CultureInfo.InvariantCulture )} bytes.",
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

			ValidatePercentEscapeAt(
				uri,
				index,
				uri.Length
			);
			normalized.Append( '%' );
			normalized.Append( char.ToUpperInvariant( uri[ index + 1 ] ) );
			normalized.Append( char.ToUpperInvariant( uri[ index + 2 ] ) );
			index += 2;
		}

		return normalized.ToString();
	}

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
				$"OSC 8 hyperlink identifiers may not exceed {MaximumIdentifierByteCount.ToString( CultureInfo.InvariantCulture )} bytes.",
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
		int query = uri.IndexOf(
			'?',
			contentStart,
			querySearchEnd - contentStart
		);
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
		if ( 0 > start || start > end || uri.Length < end ) {
			throw new ArgumentOutOfRangeException( nameof( start ) );
		}

		int at = -1;
		for ( int index = start; index < end; ++index ) {
			if ( '@' == uri[ index ] ) {
				if ( 0 <= at ) {
					throw new ArgumentException(
						"An OSC 8 hyperlink URI authority may contain at most one user-info delimiter.",
						nameof( uri )
					);
				}
				at = index;
			}
		}

		int hostPortStart = start;
		if ( 0 <= at ) {
			ValidateUserInfo(
				uri,
				start,
				at
			);
			hostPortStart = at + 1;
		}

		if ( hostPortStart < end && '[' == uri[ hostPortStart ] ) {
			int close = uri.IndexOf(
				']',
				hostPortStart + 1,
				end - hostPortStart - 1
			);
			if ( 0 > close ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains an unterminated IP-literal host.",
					nameof( uri )
				);
			}
			if ( 0 <= uri.IndexOf( '[', hostPortStart + 1, close - hostPortStart - 1 )
				|| 0 <= uri.IndexOf( ']', close + 1, end - close - 1 ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains malformed authority brackets.",
					nameof( uri )
				);
			}

			ValidateIpLiteral( uri[ ( hostPortStart + 1 )..close ] );
			if ( close + 1 < end ) {
				if ( ':' != uri[ close + 1 ] ) {
					throw new ArgumentException(
						"An OSC 8 hyperlink URI contains invalid text after an IP-literal host.",
						nameof( uri )
					);
				}
				ValidatePort(
					uri,
					close + 2,
					end
				);
			}
			return;
		}

		for ( int index = hostPortStart; index < end; ++index ) {
			if ( '[' == uri[ index ] || ']' == uri[ index ] ) {
				throw new ArgumentException(
					"Bracket characters are permitted only around RFC 3986 IP-literal hosts.",
					nameof( uri )
				);
			}
		}

		int portDelimiter = -1;
		for ( int index = hostPortStart; index < end; ++index ) {
			if ( ':' == uri[ index ] ) {
				if ( 0 <= portDelimiter ) {
					throw new ArgumentException(
						"IPv6 and IPvFuture hosts must use RFC 3986 bracketed IP-literal form.",
						nameof( uri )
					);
				}
				portDelimiter = index;
			}
		}

		int hostEnd = 0 <= portDelimiter
			? portDelimiter
			: end;
		ValidateRegName(
			uri,
			hostPortStart,
			hostEnd
		);
		if ( 0 <= portDelimiter ) {
			ValidatePort(
				uri,
				portDelimiter + 1,
				end
			);
		}
	}

	private static void ValidateUserInfo(
		string uri,
		int start,
		int end
	) {
		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter( character, nameof( uri ) );
			if ( '%' == character ) {
				ValidatePercentEscapeAt( uri, index, end );
				index += 2;
				continue;
			}
			if ( !IsUnreservedAscii( character )
				&& !IsSubDelimiter( character )
				&& ':' != character ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains invalid user-info syntax.",
					nameof( uri )
				);
			}
		}
	}

	private static void ValidateRegName(
		string uri,
		int start,
		int end
	) {
		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter( character, nameof( uri ) );
			if ( '%' == character ) {
				ValidatePercentEscapeAt( uri, index, end );
				index += 2;
				continue;
			}
			if ( !IsUnreservedAscii( character )
				&& !IsSubDelimiter( character ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI contains invalid registered-name host syntax.",
					nameof( uri )
				);
			}
		}
	}

	private static void ValidateIpLiteral(
		string literal
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( literal );

		foreach ( char character in literal ) {
			ValidateAsciiUriCharacter( character, nameof( literal ) );
		}
		if ( 'v' == literal[ 0 ] || 'V' == literal[ 0 ] ) {
			ValidateIpvFuture( literal );
			return;
		}
		if ( literal.Contains( '%', StringComparison.Ordinal ) ) {
			throw new ArgumentException(
				"Scoped IPv6 zone identifiers are outside the RFC 3986 OSC 8 hyperlink contract.",
				nameof( literal )
			);
		}
		if ( !IPAddress.TryParse( literal, out IPAddress? address )
			|| AddressFamily.InterNetworkV6 != address.AddressFamily ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink IP-literal host must contain a valid IPv6 or IPvFuture address.",
				nameof( literal )
			);
		}
	}

	private static void ValidateIpvFuture(
		string literal
	) {
		int dot = literal.IndexOf( '.' );
		if ( 2 > dot || literal.Length - 1 == dot ) {
			throw new ArgumentException(
				"An OSC 8 hyperlink IPvFuture host must use v<hex>.<address> syntax.",
				nameof( literal )
			);
		}
		for ( int index = 1; index < dot; ++index ) {
			if ( !IsHexDigit( literal[ index ] ) ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink IPvFuture version must contain hexadecimal digits.",
					nameof( literal )
				);
			}
		}
		for ( int index = dot + 1; index < literal.Length; ++index ) {
			char character = literal[ index ];
			if ( !IsUnreservedAscii( character )
				&& !IsSubDelimiter( character )
				&& ':' != character ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink IPvFuture address contains invalid syntax.",
					nameof( literal )
				);
			}
		}
	}

	private static void ValidatePort(
		string uri,
		int start,
		int end
	) {
		for ( int index = start; index < end; ++index ) {
			if ( '0' > uri[ index ] || '9' < uri[ index ] ) {
				throw new ArgumentException(
					"An OSC 8 hyperlink URI port must contain only decimal digits.",
					nameof( uri )
				);
			}
		}
	}

	private static void ValidatePath(
		string uri,
		int start,
		int end
	) {
		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter( character, nameof( uri ) );
			if ( '%' == character ) {
				ValidatePercentEscapeAt( uri, index, end );
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
		for ( int index = start; index < end; ++index ) {
			char character = uri[ index ];
			ValidateAsciiUriCharacter( character, nameof( uri ) );
			if ( '%' == character ) {
				ValidatePercentEscapeAt( uri, index, end );
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
