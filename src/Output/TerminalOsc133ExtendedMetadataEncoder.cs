namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Encodes the bounded OSC 133 extended metadata frozen by T150.
/// </summary>
internal static class TerminalOsc133ExtendedMetadataEncoder {
	internal const int MaximumPayloadLength = 65_536;

	private const string PromptStartPrefix = "133;A";
	private const string CommandOutputPrefix = "133;C;cmdline_url=";
	private const string RedrawDisabledParameter = ";redraw=0";
	private const string SpecialCursorKeyParameter = ";special_key=1";
	private const string SecondaryPromptParameter = ";k=s";
	private const string AbsoluteClickEventsParameter = ";click_events=1";
	private const string RelativeClickEventsParameter = ";click_events=2";

	private static readonly UTF8Encoding StrictUtf8 = new(
		emitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[] EncodePromptStart(
		bool shellDoesNotRedrawPrompt,
		bool useSpecialCursorKey,
		bool secondaryPrompt,
		byte clickEvents
	) {
		if ( 2 < clickEvents ) {
			throw new ArgumentOutOfRangeException(
				nameof( clickEvents ),
				clickEvents,
				"OSC 133 prompt click events must be 0, 1, or 2."
			);
		}

		int payloadLength = PromptStartPrefix.Length;
		if ( shellDoesNotRedrawPrompt ) {
			payloadLength += RedrawDisabledParameter.Length;
		}
		if ( useSpecialCursorKey ) {
			payloadLength += SpecialCursorKeyParameter.Length;
		}
		if ( secondaryPrompt ) {
			payloadLength += SecondaryPromptParameter.Length;
		}
		payloadLength += clickEvents switch {
			0 => 0,
			1 => AbsoluteClickEventsParameter.Length,
			2 => RelativeClickEventsParameter.Length,
			_ => throw new InvalidOperationException(
				"The validated OSC 133 click-event value is not supported."
			)
		};

		byte[] frame = CreateFrame( payloadLength );
		Span<byte> payload = frame.AsSpan( 2, payloadLength );
		int offset = 0;
		WriteAscii(
			PromptStartPrefix,
			payload,
			ref offset
		);
		if ( shellDoesNotRedrawPrompt ) {
			WriteAscii(
				RedrawDisabledParameter,
				payload,
				ref offset
			);
		}
		if ( useSpecialCursorKey ) {
			WriteAscii(
				SpecialCursorKeyParameter,
				payload,
				ref offset
			);
		}
		if ( secondaryPrompt ) {
			WriteAscii(
				SecondaryPromptParameter,
				payload,
				ref offset
			);
		}
		if ( 1 == clickEvents ) {
			WriteAscii(
				AbsoluteClickEventsParameter,
				payload,
				ref offset
			);
		} else if ( 2 == clickEvents ) {
			WriteAscii(
				RelativeClickEventsParameter,
				payload,
				ref offset
			);
		}

		return frame;
	}

	internal static byte[] EncodeCommandOutput(
		string commandLine
	) {
		ArgumentNullException.ThrowIfNull( commandLine );

		int maximumEncodedCommandLength = MaximumPayloadLength - CommandOutputPrefix.Length;
		int utf8ByteCount;
		try {
			utf8ByteCount = StrictUtf8.GetByteCount( commandLine );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 133 command-line metadata must contain well-formed Unicode.",
				nameof( commandLine ),
				exception
			);
		}

		if ( maximumEncodedCommandLength < utf8ByteCount ) {
			throw CreatePayloadTooLargeException( nameof( commandLine ) );
		}

		byte[] utf8 = new byte[ utf8ByteCount ];
		try {
			StrictUtf8.GetBytes(
				commandLine,
				0,
				commandLine.Length,
				utf8,
				0
			);
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 133 command-line metadata must contain well-formed Unicode.",
				nameof( commandLine ),
				exception
			);
		}

		int encodedLength = 0;
		foreach ( byte value in utf8 ) {
			encodedLength += IsUnreserved( value )
				? 1
				: 3
			;
			if ( maximumEncodedCommandLength < encodedLength ) {
				throw CreatePayloadTooLargeException( nameof( commandLine ) );
			}
		}

		int payloadLength = CommandOutputPrefix.Length + encodedLength;
		byte[] frame = CreateFrame( payloadLength );
		Span<byte> payload = frame.AsSpan( 2, payloadLength );
		int offset = 0;
		WriteAscii(
			CommandOutputPrefix,
			payload,
			ref offset
		);
		foreach ( byte value in utf8 ) {
			if ( IsUnreserved( value ) ) {
				payload[ offset++ ] = value;
			} else {
				payload[ offset++ ] = (byte)'%';
				payload[ offset++ ] = ToUpperHex( value >> 4 );
				payload[ offset++ ] = ToUpperHex( value & 0x0f );
			}
		}

		return frame;
	}

	private static byte[] CreateFrame(
		int payloadLength
	) {
		if ( 0 >= payloadLength || MaximumPayloadLength < payloadLength ) {
			throw new ArgumentOutOfRangeException( nameof( payloadLength ) );
		}

		byte[] frame = new byte[ payloadLength + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static void WriteAscii(
		string value,
		Span<byte> destination,
		ref int offset
	) {
		ArgumentNullException.ThrowIfNull( value );
		foreach ( char character in value ) {
			if ( 0x7f < character ) {
				throw new InvalidOperationException(
					"An internal OSC 133 protocol literal is not ASCII."
				);
			}
			destination[ offset++ ] = (byte)character;
		}
	}

	private static bool IsUnreserved(
		byte value
	) {
		return ( (byte)'A' <= value && (byte)'Z' >= value )
			|| ( (byte)'a' <= value && (byte)'z' >= value )
			|| ( (byte)'0' <= value && (byte)'9' >= value )
			|| (byte)'-' == value
			|| (byte)'.' == value
			|| (byte)'_' == value
			|| (byte)'~' == value
		;
	}

	private static byte ToUpperHex(
		int value
	) {
		if ( 0 > value || 15 < value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		return (byte)( 10 > value
			? '0' + value
			: 'A' + value - 10
		);
	}

	private static ArgumentException CreatePayloadTooLargeException(
		string parameterName
	) {
		return new ArgumentException(
			$"OSC 133 metadata cannot exceed {MaximumPayloadLength} encoded payload bytes.",
			parameterName
		);
	}
}
