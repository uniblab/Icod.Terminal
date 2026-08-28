namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Implements bounded DECRQSS request and DECRPSS response handling.
/// </summary>
internal static class TerminalDecrqssProtocol {
	internal const int MaximumRequestIdentifierBytes = 16;
	internal const int MaximumStatusStringBytes = 1024;

	private const byte EscapeByte = 0x1B;
	private const byte DcsByte = 0x90;
	private const byte StringTerminatorByte = 0x9C;

	internal static ITerminalResponseMatcher ResponseMatcher {
		get;
	} = new TerminalDecrqssResponseMatcher();

	internal static ReadOnlyMemory<byte> CreateRequest(
		TerminalStatusStringKind kind
	) {
		byte[] identifier = GetRequestIdentifier( kind );
		if ( MaximumRequestIdentifierBytes < identifier.Length ) {
			throw new InvalidOperationException(
				$"A DECRQSS request identifier cannot exceed {MaximumRequestIdentifierBytes} bytes."
			);
		}

		byte[] request = new byte[ 6 + identifier.Length ];
		request[ 0 ] = EscapeByte;
		request[ 1 ] = (byte)'P';
		request[ 2 ] = (byte)'$';
		request[ 3 ] = (byte)'q';
		identifier.CopyTo(
			request,
			4
		);
		request[ ^2 ] = EscapeByte;
		request[ ^1 ] = (byte)'\\';
		return request;
	}

	internal static TerminalStatusStringResponse ParseResponse(
		TerminalStatusStringKind kind,
		TerminalResponseFrame frame
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ArgumentNullException.ThrowIfNull( frame );

		if ( !TryGetResponseLayout(
			frame,
			out int parameterStart,
			out int parameterLength,
			out int payloadStart,
			out int payloadLength
		) ) {
			throw new FormatException(
				"The terminal response is not a DECRPSS frame."
			);
		}

		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		if ( 1 != parameterLength ) {
			throw new FormatException(
				"A DECRPSS response must contain exactly one validity parameter."
			);
		}

		byte validity = bytes[ parameterStart ];
		if ( (byte)'0' == validity ) {
			if ( 0 != payloadLength ) {
				throw new FormatException(
					"A negative DECRPSS response cannot contain status-string data."
				);
			}

			return new TerminalStatusStringResponse(
				kind,
				isSupported: false,
				statusString: null
			);
		}
		if ( (byte)'1' != validity ) {
			throw new FormatException(
				"A DECRPSS validity parameter must be 0 or 1."
			);
		}
		if ( 0 == payloadLength ) {
			throw new FormatException(
				"A positive DECRPSS response must contain status-string data."
			);
		}
		if ( MaximumStatusStringBytes < payloadLength ) {
			throw new FormatException(
				$"A DECRPSS status string cannot exceed {MaximumStatusStringBytes} bytes."
			);
		}

		ReadOnlySpan<byte> payload = bytes.Slice(
			payloadStart,
			payloadLength
		);
		for ( int index = 0; index < payload.Length; index++ ) {
			if ( payload[ index ] is < 0x20 or > 0x7E ) {
				throw new FormatException(
					"A DECRPSS status string contains a non-printable control byte."
				);
			}
		}

		byte[] identifier = GetRequestIdentifier( kind );
		if ( payload.Length < identifier.Length
			|| !payload.Slice(
				payload.Length - identifier.Length
			).SequenceEqual( identifier ) ) {
			throw new FormatException(
				"The DECRPSS status string does not match the requested control function."
			);
		}

		return new TerminalStatusStringResponse(
			kind,
			isSupported: true,
			Encoding.ASCII.GetString( payload )
		);
	}

	internal static byte[] GetRequestIdentifier(
		TerminalStatusStringKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}

		switch ( kind ) {
			case TerminalStatusStringKind.SelectGraphicRendition:
				return [ (byte)'m' ];

			case TerminalStatusStringKind.ConformanceLevel:
				return [ (byte)'"', (byte)'p' ];

			case TerminalStatusStringKind.CursorStyle:
				return [ (byte)' ', (byte)'q' ];

			case TerminalStatusStringKind.CharacterProtection:
				return [ (byte)'"', (byte)'q' ];

			case TerminalStatusStringKind.ScrollingRegion:
				return [ (byte)'r' ];

			case TerminalStatusStringKind.LeftRightMargins:
				return [ (byte)'s' ];

			case TerminalStatusStringKind.LinesPerPage:
				return [ (byte)'t' ];

			case TerminalStatusStringKind.ColumnsPerPage:
				return [ (byte)'$', (byte)'|' ];

			case TerminalStatusStringKind.ActiveStatusDisplay:
				return [ (byte)'$', (byte)'}' ];

			case TerminalStatusStringKind.StatusLineType:
				return [ (byte)'$', (byte)'~' ];

			case TerminalStatusStringKind.AttributeChangeExtent:
				return [ (byte)'*', (byte)'x' ];

			case TerminalStatusStringKind.LinesPerScreen:
				return [ (byte)'*', (byte)'|' ];

			default:
				throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
	}

	private static bool TryGetResponseLayout(
		TerminalResponseFrame frame,
		out int parameterStart,
		out int parameterLength,
		out int payloadStart,
		out int payloadLength
	) {
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
		if ( 1 != intermediateLength || (byte)'$' != bytes[ intermediateStart ] ) {
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

	private sealed class TerminalDecrqssResponseMatcher : ITerminalResponseMatcher {
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
