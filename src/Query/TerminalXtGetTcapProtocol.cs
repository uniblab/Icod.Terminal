namespace Icod.Terminal;

/// <summary>
/// Implements bounded XTGETTCAP request and response handling.
/// </summary>
internal static class TerminalXtGetTcapProtocol {
	internal const int MaximumCapabilityNameBytes = 64;
	internal const int MaximumEncodedCapabilityNameBytes =
		MaximumCapabilityNameBytes * 2;
	internal const int MaximumCapabilityValueBytes = 1024;

	private const byte EscapeByte = 0x1B;
	private const byte DcsByte = 0x90;
	private const byte StringTerminatorByte = 0x9C;

	internal static ITerminalResponseMatcher ResponseMatcher {
		get;
	} = new TerminalXtGetTcapResponseMatcher();

	internal static void ValidateCapabilityName(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );
		_ = GetCapabilityNameBytes( name );
	}

	internal static ReadOnlyMemory<byte> CreateRequest(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );
		byte[] nameBytes = GetCapabilityNameBytes( name );
		byte[] encodedName = EncodeHex( nameBytes );
		if ( MaximumEncodedCapabilityNameBytes < encodedName.Length ) {
			throw new InvalidOperationException(
				$"An encoded XTGETTCAP capability name cannot exceed "
					+ $"{MaximumEncodedCapabilityNameBytes} bytes."
			);
		}

		byte[] request = new byte[ 6 + encodedName.Length ];
		request[ 0 ] = EscapeByte;
		request[ 1 ] = (byte)'P';
		request[ 2 ] = (byte)'+';
		request[ 3 ] = (byte)'q';
		encodedName.CopyTo(
			request,
			4
		);
		request[ ^2 ] = EscapeByte;
		request[ ^1 ] = (byte)'\\';
		return request;
	}

	internal static TerminalCapabilityObservation ParseResponse(
		string requestedName,
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( requestedName );
		ArgumentNullException.ThrowIfNull( frame );
		byte[] requestedNameBytes = GetCapabilityNameBytes( requestedName );

		if ( !TryGetResponseLayout(
			frame,
			out int parameterStart,
			out int parameterLength,
			out int payloadStart,
			out int payloadLength
		) ) {
			throw new FormatException(
				"The terminal response is not an XTGETTCAP frame."
			);
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		if ( 1 != parameterLength ) {
			throw new FormatException(
				"An XTGETTCAP response must contain exactly one validity parameter."
			);
		}

		byte validity = bytes[ parameterStart ];
		if ( (byte)'0' == validity ) {
			if ( 0 != payloadLength ) {
				throw new FormatException(
					"A negative XTGETTCAP response cannot contain capability data."
				);
			}

			return new TerminalCapabilityObservation(
				requestedName,
				isSupported: false,
				valueBytes: null
			);
		}
		if ( (byte)'1' != validity ) {
			throw new FormatException(
				"An XTGETTCAP validity parameter must be 0 or 1."
			);
		}
		if ( 0 == payloadLength ) {
			throw new FormatException(
				"A positive XTGETTCAP response must contain a capability name and '=' separator."
			);
		}

		ReadOnlySpan<byte> payload = bytes.Slice(
			payloadStart,
			payloadLength
		);
		int separator = payload.IndexOf( (byte)'=' );
		if ( 0 >= separator ) {
			throw new FormatException(
				"A positive XTGETTCAP response must contain an encoded capability name followed by '='."
			);
		}
		if ( payload.Slice( separator + 1 ).IndexOf( (byte)'=' ) >= 0 ) {
			throw new FormatException(
				"An XTGETTCAP response cannot contain more than one name/value separator."
			);
		}

		ReadOnlySpan<byte> encodedName = payload.Slice(
			0,
			separator
		);
		ReadOnlySpan<byte> encodedValue = payload.Slice(
			separator + 1
		);

		byte[] returnedNameBytes = DecodeHex(
			encodedName,
			MaximumCapabilityNameBytes,
			"capability name"
		);
		if ( !returnedNameBytes.AsSpan().SequenceEqual( requestedNameBytes ) ) {
			throw new FormatException(
				"The XTGETTCAP response does not match the requested capability name."
			);
		}

		byte[] valueBytes = DecodeHex(
			encodedValue,
			MaximumCapabilityValueBytes,
			"capability value"
		);

		return new TerminalCapabilityObservation(
			requestedName,
			isSupported: true,
			valueBytes
		);
	}

	internal static byte[] GetCapabilityNameBytes(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );
		if ( 0 == name.Length ) {
			throw new ArgumentException(
				"An XTGETTCAP capability name cannot be empty.",
				nameof( name )
			);
		}
		if ( MaximumCapabilityNameBytes < name.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( name ),
				name.Length,
				$"An XTGETTCAP capability name cannot exceed "
					+ $"{MaximumCapabilityNameBytes} ASCII bytes."
			);
		}

		byte[] result = new byte[ name.Length ];
		for ( int index = 0; index < name.Length; index++ ) {
			char current = name[ index ];
			if ( current is < '!' or > '~' ) {
				throw new ArgumentException(
					"An XTGETTCAP capability name must contain only printable non-space ASCII characters.",
					nameof( name )
				);
			}

			result[ index ] = checked( (byte)current );
		}

		return result;
	}

	private static byte[] EncodeHex(
		ReadOnlySpan<byte> bytes
	) {
		byte[] encoded = new byte[ checked( bytes.Length * 2 ) ];
		for ( int index = 0; index < bytes.Length; index++ ) {
			byte value = bytes[ index ];
			encoded[ index * 2 ] = EncodeHexNibble(
				(byte)( value >> 4 )
			);
			encoded[ index * 2 + 1 ] = EncodeHexNibble(
				(byte)( value & 0x0F )
			);
		}

		return encoded;
	}

	private static byte[] DecodeHex(
		ReadOnlySpan<byte> encoded,
		int maximumDecodedBytes,
		string fieldName
	) {
		if ( 0 > maximumDecodedBytes ) {
			throw new ArgumentOutOfRangeException( nameof( maximumDecodedBytes ) );
		}
		ArgumentNullException.ThrowIfNull( fieldName );

		if ( 0 != encoded.Length % 2 ) {
			throw new FormatException(
				$"The XTGETTCAP encoded {fieldName} contains an odd number of hexadecimal digits."
			);
		}

		int decodedLength = encoded.Length / 2;
		if ( maximumDecodedBytes < decodedLength ) {
			throw new FormatException(
				$"The XTGETTCAP decoded {fieldName} cannot exceed "
					+ $"{maximumDecodedBytes} bytes."
			);
		}

		byte[] decoded = new byte[ decodedLength ];
		for ( int index = 0; index < decodedLength; index++ ) {
			int high = DecodeHexNibble( encoded[ index * 2 ] );
			int low = DecodeHexNibble( encoded[ index * 2 + 1 ] );
			if ( 0 > high || 0 > low ) {
				throw new FormatException(
					$"The XTGETTCAP encoded {fieldName} contains a non-hexadecimal character."
				);
			}

			decoded[ index ] = checked( (byte)( ( high << 4 ) | low ) );
		}

		return decoded;
	}

	private static byte EncodeHexNibble(
		byte value
	) {
		if ( 9 >= value ) {
			return checked( (byte)( (byte)'0' + value ) );
		}

		return checked( (byte)( (byte)'A' + value - 10 ) );
	}

	private static int DecodeHexNibble(
		byte value
	) {
		if ( value is >= (byte)'0' and <= (byte)'9' ) {
			return value - (byte)'0';
		}
		if ( value is >= (byte)'A' and <= (byte)'F' ) {
			return value - (byte)'A' + 10;
		}
		if ( value is >= (byte)'a' and <= (byte)'f' ) {
			return value - (byte)'a' + 10;
		}

		return -1;
	}

	private static bool TryGetResponseLayout(
		TerminalResponseFrame frame,
		out int parameterStart,
		out int parameterLength,
		out int payloadStart,
		out int payloadLength
	) {
		ArgumentNullException.ThrowIfNull( frame );

		parameterStart = 0;
		parameterLength = 0;
		payloadStart = 0;
		payloadLength = 0;

		if ( TerminalResponseFrameKind.Dcs != frame.Kind ) {
			return false;
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		if ( !TryGetDcsContentBounds(
			bytes,
			out int contentStart,
			out int contentEnd
		) ) {
			return false;
		}

		int index = contentStart;
		parameterStart = index;
		while ( index < contentEnd && IsParameterByte( bytes[ index ] ) ) {
			++index;
		}
		parameterLength = index - parameterStart;

		int intermediateStart = index;
		while ( index < contentEnd && IsIntermediateByte( bytes[ index ] ) ) {
			++index;
		}
		int intermediateLength = index - intermediateStart;
		if ( 1 != intermediateLength || (byte)'+' != bytes[ intermediateStart ] ) {
			return false;
		}
		if ( index >= contentEnd || (byte)'r' != bytes[ index ] ) {
			return false;
		}

		payloadStart = index + 1;
		payloadLength = contentEnd - payloadStart;
		return true;
	}

	private static bool TryGetDcsContentBounds(
		ReadOnlySpan<byte> bytes,
		out int contentStart,
		out int contentEnd
	) {
		contentStart = 0;
		contentEnd = 0;

		if ( 4 > bytes.Length ) {
			return false;
		}

		if ( DcsByte == bytes[ 0 ] ) {
			contentStart = 1;
		} else if ( 2 <= bytes.Length
			&& EscapeByte == bytes[ 0 ]
			&& (byte)'P' == bytes[ 1 ] ) {
			contentStart = 2;
		} else {
			return false;
		}

		if ( StringTerminatorByte == bytes[ ^1 ] ) {
			contentEnd = bytes.Length - 1;
		} else if ( 2 <= bytes.Length
			&& EscapeByte == bytes[ ^2 ]
			&& (byte)'\\' == bytes[ ^1 ] ) {
			contentEnd = bytes.Length - 2;
		} else {
			return false;
		}

		return contentStart < contentEnd;
	}

	private static bool IsParameterByte(
		byte value
	) {
		return value is >= 0x30 and <= 0x3F;
	}

	private static bool IsIntermediateByte(
		byte value
	) {
		return value is >= 0x20 and <= 0x2F;
	}

	private sealed class TerminalXtGetTcapResponseMatcher : ITerminalResponseMatcher {
		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Dcs;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TryGetResponseLayout(
				frame,
				out _,
				out _,
				out _,
				out _
			);
		}
	}
}
