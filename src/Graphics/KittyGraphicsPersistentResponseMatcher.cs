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
/// Matches one persistent Kitty Graphics acknowledgement by the private image
/// number supplied during resource creation.
/// </summary>
internal sealed class KittyGraphicsPersistentResponseMatcher :
	ITerminalResponseMatcher,
	ICorrelatedTerminalResponseMatcher {
	private readonly bool validateMatchedResponse;

	internal KittyGraphicsPersistentResponseMatcher(
		uint imageNumber
	) : this(
		imageNumber,
		validateMatchedResponse: true
	) {
	}

	internal KittyGraphicsPersistentResponseMatcher(
		uint imageNumber,
		bool validateMatchedResponse
	) {
		if ( 0 == imageNumber ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageNumber ),
				imageNumber,
				"A persistent Kitty Graphics image number must be non-zero."
			);
		}
		this.ImageNumber = imageNumber;
		this.validateMatchedResponse = validateMatchedResponse;
	}

	internal uint ImageNumber {
		get;
	}

	public TerminalResponseFrameKind FrameKind {
		get {
			return TerminalResponseFrameKind.Apc;
		}
	}

	public bool IsMatch(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Apc != frame.Kind ) {
			return false;
		}

		TerminalControlFrameStructure structure;
		try {
			structure = TerminalControlFrameStructure.Parse( frame );
		} catch ( FormatException ) {
			return false;
		}
		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		if ( TerminalControlFamily.Apc != structure.Family
			|| payload.IsEmpty
			|| (byte)'G' != payload[ 0 ] ) {
			return false;
		}
		if ( !ContainsExpectedImageNumberField(
			payload[1..],
			this.ImageNumber
		) ) {
			return false;
		}

		if ( this.validateMatchedResponse ) {
			_ = KittyGraphicsPersistentCreationResponse.Parse(
				frame,
				this.ImageNumber
			);
		}
		return true;
	}

	public bool IsCorrelatedPrefix(
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 0 == bytes.Count ) {
			return false;
		}

		int payloadStart;
		if ( 0x9F == bytes[ 0 ] ) {
			payloadStart = 1;
		} else if ( 2 <= bytes.Count
			&& 0x1B == bytes[ 0 ]
			&& (byte)'_' == bytes[ 1 ] ) {
			payloadStart = 2;
		} else {
			return false;
		}
		if ( bytes.Count <= payloadStart
			|| (byte)'G' != bytes[ payloadStart ] ) {
			return false;
		}

		int fieldStart = payloadStart + 1;
		for ( int index = fieldStart; index < bytes.Count; index++ ) {
			byte value = bytes[ index ];
			if ( value is not (byte)',' and not (byte)';' ) {
				continue;
			}

			if ( IsExpectedImageNumberField(
				bytes,
				fieldStart,
				index - fieldStart,
				this.ImageNumber
			) ) {
				return true;
			}
			if ( (byte)';' == value ) {
				return false;
			}
			fieldStart = index + 1;
		}
		return false;
	}

	private static bool ContainsExpectedImageNumberField(
		ReadOnlySpan<byte> controlAndMessage,
		uint imageNumber
	) {
		int fieldStart = 0;
		for ( int index = 0; index < controlAndMessage.Length; index++ ) {
			byte value = controlAndMessage[ index ];
			if ( value is not (byte)',' and not (byte)';' ) {
				continue;
			}

			ReadOnlySpan<byte> field = controlAndMessage.Slice(
				fieldStart,
				index - fieldStart
			);
			if ( IsExpectedImageNumberField(
				field,
				imageNumber
			) ) {
				return true;
			}
			if ( (byte)';' == value ) {
				return false;
			}
			fieldStart = index + 1;
		}
		return false;
	}

	private static bool IsExpectedImageNumberField(
		ReadOnlySpan<byte> field,
		uint imageNumber
	) {
		if ( 3 > field.Length
			|| (byte)'I' != field[ 0 ]
			|| (byte)'=' != field[ 1 ] ) {
			return false;
		}

		uint parsed = 0;
		for ( int index = 2; index < field.Length; index++ ) {
			byte item = field[ index ];
			if ( item is < (byte)'0' or > (byte)'9' ) {
				return false;
			}

			uint digit = (uint)( item - (byte)'0' );
			if ( ( uint.MaxValue - digit ) / 10 < parsed ) {
				return false;
			}
			parsed = ( parsed * 10 ) + digit;
		}
		return imageNumber == parsed;
	}

	private static bool IsExpectedImageNumberField(
		IReadOnlyList<byte> bytes,
		int start,
		int length,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 3 > length
			|| 0 > start
			|| bytes.Count < start + length
			|| (byte)'I' != bytes[ start ]
			|| (byte)'=' != bytes[ start + 1 ] ) {
			return false;
		}

		uint parsed = 0;
		for ( int index = start + 2; index < start + length; index++ ) {
			byte item = bytes[ index ];
			if ( item is < (byte)'0' or > (byte)'9' ) {
				return false;
			}

			uint digit = (uint)( item - (byte)'0' );
			if ( ( uint.MaxValue - digit ) / 10 < parsed ) {
				return false;
			}
			parsed = ( parsed * 10 ) + digit;
		}
		return imageNumber == parsed;
	}
}
