/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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

	internal static ITerminalResponseMatcher ResponseMatcher {
		get;
	} = new TerminalXtGetTcapResponseMatcher(
		requestedNameBytes: null
	);

	internal static ITerminalResponseMatcher CreateResponseMatcher(
		string name
	) {
		ArgumentNullException.ThrowIfNull( name );
		return new TerminalXtGetTcapResponseMatcher(
			GetCapabilityNameBytes( name )
		);
	}

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

		if ( !TryGetResponseStructure(
			frame,
			out TerminalControlFrameStructure structure
		) ) {
			throw new FormatException(
				"The terminal response is not an XTGETTCAP frame."
			);
		}

		ReadOnlySpan<byte> parameters = structure.ParameterBytes.Span;
		if ( 1 != parameters.Length ) {
			throw new FormatException(
				"An XTGETTCAP response must contain exactly one validity parameter."
			);
		}

		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		byte validity = parameters[ 0 ];
		if ( (byte)'0' == validity ) {
			if ( !payload.IsEmpty ) {
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
		if ( payload.IsEmpty ) {
			throw new FormatException(
				"A positive XTGETTCAP response must contain a capability name and '=' separator."
			);
		}

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

	private static bool TryGetResponseStructure(
		TerminalResponseFrame frame,
		out TerminalControlFrameStructure structure
	) {
		ArgumentNullException.ThrowIfNull( frame );

		structure = default;
		if ( TerminalResponseFrameKind.Dcs != frame.Kind
			|| !TerminalControlFrameStructure.TryParse(
				frame,
				out TerminalControlFrameStructure parsed
			) ) {
			return false;
		}
		if ( TerminalControlFamily.Dcs != parsed.Family
			|| !parsed.FinalByte.HasValue
			|| (byte)'r' != parsed.FinalByte.Value ) {
			return false;
		}

		ReadOnlySpan<byte> intermediates = parsed.IntermediateBytes.Span;
		if ( 1 != intermediates.Length || (byte)'+' != intermediates[ 0 ] ) {
			return false;
		}

		structure = parsed;
		return true;
	}

	private static bool IsResponseForRequestedName(
		TerminalControlFrameStructure structure,
		ReadOnlySpan<byte> requestedNameBytes
	) {
		ReadOnlySpan<byte> parameters = structure.ParameterBytes.Span;
		if ( 1 != parameters.Length || (byte)'1' != parameters[ 0 ] ) {
			return true;
		}

		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		if ( payload.IsEmpty ) {
			return true;
		}

		int separator = payload.IndexOf( (byte)'=' );
		if ( 0 >= separator ) {
			return true;
		}

		try {
			byte[] returnedNameBytes = DecodeHex(
				payload.Slice(
					0,
					separator
				),
				MaximumCapabilityNameBytes,
				"capability name"
			);
			return returnedNameBytes.AsSpan().SequenceEqual( requestedNameBytes );
		} catch ( FormatException ) {
			return true;
		}
	}

	private sealed class TerminalXtGetTcapResponseMatcher : ITerminalResponseMatcher {
		private readonly byte[]? requestedNameBytes;

		internal TerminalXtGetTcapResponseMatcher(
			byte[]? requestedNameBytes
		) {
			this.requestedNameBytes = requestedNameBytes?.ToArray();
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Dcs;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			if ( !TryGetResponseStructure(
				frame,
				out TerminalControlFrameStructure structure
			) ) {
				return false;
			}
		if ( this.requestedNameBytes is null ) {
			return true;
		}

			return IsResponseForRequestedName(
				structure,
				this.requestedNameBytes
			);
		}
	}
}
