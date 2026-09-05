namespace Icod.Terminal;

/// <summary>
/// Identifies one OSC 52 selection within the internal protocol layer.
/// </summary>
internal enum TerminalOsc52Selection {
	Clipboard,
	Primary,
	Secondary,
	Select
}

/// <summary>
/// Maps the typed OSC 52 selection subset frozen for the 0.7 release.
/// </summary>
internal static class TerminalOsc52SelectionEncoder {
	internal static byte Encode(
		TerminalOsc52Selection selection
	) {
		return selection switch {
			TerminalOsc52Selection.Clipboard => (byte)'c',
			TerminalOsc52Selection.Primary => (byte)'p',
			TerminalOsc52Selection.Secondary => (byte)'q',
			TerminalOsc52Selection.Select => (byte)'s',
			_ => throw new ArgumentOutOfRangeException(
				nameof( selection ),
				selection,
				"The OSC 52 selection is not recognized."
			)
		};
	}
}

/// <summary>
/// Performs bounded canonical RFC 4648 base64 conversion for OSC 52 payloads.
/// </summary>
internal static class TerminalOsc52PayloadCodec {
	internal const int MaximumDecodedPayloadBytes = 65_536;
	internal const int MaximumEncodedPayloadBytes = 87_384;
	internal const int MaximumFrameBytes = 87_400;

	private const int FrameFixedByteCount = 9;

	internal static int GetEncodedLength(
		int decodedByteCount
	) {
		if ( 0 > decodedByteCount
			|| MaximumDecodedPayloadBytes < decodedByteCount ) {
			throw new ArgumentOutOfRangeException( nameof( decodedByteCount ) );
		}

		return checked(
			( ( decodedByteCount + 2 ) / 3 ) * 4
		);
	}

	internal static int GetWriteFrameLength(
		int decodedByteCount
	) {
		int encodedLength = GetEncodedLength( decodedByteCount );
		int frameLength = checked(
			FrameFixedByteCount + encodedLength
		);
		if ( MaximumFrameBytes < frameLength ) {
			throw new ArgumentOutOfRangeException( nameof( decodedByteCount ) );
		}

		return frameLength;
	}

	internal static string Encode(
		ReadOnlySpan<byte> payload
	) {
		if ( MaximumDecodedPayloadBytes < payload.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( payload ),
				payload.Length,
				$"An OSC 52 decoded payload cannot exceed {MaximumDecodedPayloadBytes} bytes."
			);
		}

		int encodedLength = GetEncodedLength( payload.Length );
		string encoded = Convert.ToBase64String(
			payload,
			Base64FormattingOptions.None
		);
		if ( encodedLength != encoded.Length ) {
			throw new InvalidOperationException(
				"The OSC 52 base64 encoder produced an unexpected payload length."
			);
		}

		return encoded;
	}

	internal static int GetDecodedLength(
		ReadOnlySpan<byte> encodedPayload
	) {
		ValidateEncodedPayloadLength( encodedPayload.Length );
		if ( encodedPayload.IsEmpty ) {
			return 0;
		}

		ValidateCanonicalBase64( encodedPayload );

		int padding = '=' == encodedPayload[ ^1 ]
			? '=' == encodedPayload[ ^2 ]
				? 2
				: 1
			: 0;
		int decodedLength = checked(
			( encodedPayload.Length / 4 ) * 3 - padding
		);
		if ( MaximumDecodedPayloadBytes < decodedLength ) {
			throw new ArgumentOutOfRangeException(
				nameof( encodedPayload ),
				encodedPayload.Length,
				$"An OSC 52 decoded payload cannot exceed {MaximumDecodedPayloadBytes} bytes."
			);
		}

		return decodedLength;
	}

	internal static byte[] Decode(
		ReadOnlySpan<byte> encodedPayload
	) {
		int decodedLength = GetDecodedLength( encodedPayload );
		if ( 0 == decodedLength ) {
			return [];
		}

		byte[] decoded = new byte[ decodedLength ];
		int outputIndex = 0;

		for ( int inputIndex = 0; inputIndex < encodedPayload.Length; inputIndex += 4 ) {
			int first = DecodeSextet( encodedPayload[ inputIndex ] );
			int second = DecodeSextet( encodedPayload[ inputIndex + 1 ] );
			byte thirdByte = encodedPayload[ inputIndex + 2 ];
			byte fourthByte = encodedPayload[ inputIndex + 3 ];

			decoded[ outputIndex++ ] = (byte)( ( first << 2 ) | ( second >> 4 ) );

			if ( '=' == thirdByte ) {
				continue;
			}

			int third = DecodeSextet( thirdByte );
			decoded[ outputIndex++ ] = (byte)(
				( ( second & 0x0f ) << 4 ) | ( third >> 2 )
			);

			if ( '=' == fourthByte ) {
				continue;
			}

			int fourth = DecodeSextet( fourthByte );
			decoded[ outputIndex++ ] = (byte)(
				( ( third & 0x03 ) << 6 ) | fourth
			);
		}

		if ( decodedLength != outputIndex ) {
			throw new InvalidOperationException(
				"The OSC 52 base64 decoder produced an unexpected payload length."
			);
		}

		return decoded;
	}

	private static void ValidateEncodedPayloadLength(
		int encodedByteCount
	) {
		if ( 0 > encodedByteCount
			|| MaximumEncodedPayloadBytes < encodedByteCount ) {
			throw new ArgumentOutOfRangeException( nameof( encodedByteCount ) );
		}
		if ( 0 != encodedByteCount % 4 ) {
			throw new FormatException(
				"An OSC 52 base64 payload length must be a multiple of four."
			);
		}
	}

	private static void ValidateCanonicalBase64(
		ReadOnlySpan<byte> encodedPayload
	) {
		for ( int index = 0; index < encodedPayload.Length; index += 4 ) {
			bool finalBlock = index + 4 == encodedPayload.Length;
			int first = DecodeSextetOrThrow( encodedPayload[ index ] );
			int second = DecodeSextetOrThrow( encodedPayload[ index + 1 ] );
			byte thirdByte = encodedPayload[ index + 2 ];
			byte fourthByte = encodedPayload[ index + 3 ];

			_ = first;

			if ( '=' == thirdByte ) {
				if ( !finalBlock || '=' != fourthByte ) {
					throw new FormatException(
						"OSC 52 base64 padding is only valid at the end of the payload."
					);
				}
				if ( 0 != ( second & 0x0f ) ) {
					throw new FormatException(
						"OSC 52 base64 contains non-zero unused bits."
					);
				}
				continue;
			}

			int third = DecodeSextetOrThrow( thirdByte );
			if ( '=' == fourthByte ) {
				if ( !finalBlock ) {
					throw new FormatException(
						"OSC 52 base64 padding is only valid at the end of the payload."
					);
				}
				if ( 0 != ( third & 0x03 ) ) {
					throw new FormatException(
						"OSC 52 base64 contains non-zero unused bits."
					);
				}
				continue;
			}

			_ = DecodeSextetOrThrow( fourthByte );
		}
	}

	private static int DecodeSextetOrThrow(
		byte value
	) {
		int decoded = DecodeSextet( value );
		if ( 0 > decoded ) {
			throw new FormatException(
				$"OSC 52 payload contains invalid base64 byte 0x{value:X2}."
			);
		}

		return decoded;
	}

	private static int DecodeSextet(
		byte value
	) {
		return value switch {
			>= (byte)'A' and <= (byte)'Z' => value - (byte)'A',
			>= (byte)'a' and <= (byte)'z' => value - (byte)'a' + 26,
			>= (byte)'0' and <= (byte)'9' => value - (byte)'0' + 52,
			(byte)'+' => 62,
			(byte)'/' => 63,
			_ => -1
		};
	}
}
