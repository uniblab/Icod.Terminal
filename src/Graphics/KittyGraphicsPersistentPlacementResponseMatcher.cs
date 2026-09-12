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
/// Matches one persistent Kitty Graphics placement acknowledgement by the private
/// terminal image and placement identities owned by the session.
/// </summary>
internal sealed class KittyGraphicsPersistentPlacementResponseMatcher :
	ITerminalResponseMatcher,
	ICorrelatedTerminalResponseMatcher {
	internal KittyGraphicsPersistentPlacementResponseMatcher(
		uint imageId,
		uint placementId
	) {
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( 0u == placementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}

		this.ImageId = imageId;
		this.PlacementId = placementId;
	}

	internal uint ImageId {
		get;
	}

	internal uint PlacementId {
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

		ReadOnlySpan<byte> controlAndMessage = payload[1..];
		return ContainsExpectedField(
			controlAndMessage,
			(byte)'i',
			this.ImageId
		) && ContainsExpectedField(
			controlAndMessage,
			(byte)'p',
			this.PlacementId
		);
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

		bool imageMatched = false;
		bool placementMatched = false;
		int fieldStart = payloadStart + 1;
		for ( int index = fieldStart; index < bytes.Count; index++ ) {
			byte value = bytes[ index ];
			if ( value is not (byte)',' and not (byte)';' ) {
				continue;
			}

			int fieldLength = index - fieldStart;
			imageMatched |= IsExpectedField(
				bytes,
				fieldStart,
				fieldLength,
				(byte)'i',
				this.ImageId
			);
			placementMatched |= IsExpectedField(
				bytes,
				fieldStart,
				fieldLength,
				(byte)'p',
				this.PlacementId
			);
			if ( imageMatched && placementMatched ) {
				return true;
			}
			if ( (byte)';' == value ) {
				return false;
			}
			fieldStart = index + 1;
		}
		return false;
	}

	private static bool ContainsExpectedField(
		ReadOnlySpan<byte> controlAndMessage,
		byte key,
		uint expected
	) {
		int fieldStart = 0;
		for ( int index = 0; index < controlAndMessage.Length; index++ ) {
			byte value = controlAndMessage[ index ];
			if ( value is not (byte)',' and not (byte)';' ) {
				continue;
			}

			if ( IsExpectedField(
				controlAndMessage.Slice(
					fieldStart,
					index - fieldStart
				),
				key,
				expected
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

	private static bool IsExpectedField(
		ReadOnlySpan<byte> field,
		byte key,
		uint expected
	) {
		if ( 3 > field.Length
			|| key != field[ 0 ]
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
		return expected == parsed;
	}

	private static bool IsExpectedField(
		IReadOnlyList<byte> bytes,
		int start,
		int length,
		byte key,
		uint expected
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 3 > length
			|| 0 > start
			|| bytes.Count < start + length
			|| key != bytes[ start ]
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
		return expected == parsed;
	}
}
