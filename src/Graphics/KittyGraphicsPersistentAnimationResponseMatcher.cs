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

internal sealed class KittyGraphicsPersistentAnimationResponseMatcher :
	ITerminalResponseMatcher,
	ICorrelatedTerminalResponseMatcher {
	internal KittyGraphicsPersistentAnimationResponseMatcher(
		uint imageId
	) {
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		this.ImageId = imageId;
	}

	internal uint ImageId {
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

		try {
			KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse( frame );
			return this.ImageId == response.ImageId
				&& !response.ImageNumber.HasValue
				&& !response.PlacementId.HasValue;
		} catch ( FormatException ) {
			return false;
		}
	}

	public bool IsCorrelatedPrefix(
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		int payloadStart;
		if ( 1 <= bytes.Count && 0x9F == bytes[ 0 ] ) {
			payloadStart = 1;
		} else if ( 2 <= bytes.Count
			&& 0x1B == bytes[ 0 ]
			&& (byte)'_' == bytes[ 1 ] ) {
			payloadStart = 2;
		} else {
			return false;
		}
		if ( bytes.Count <= payloadStart || (byte)'G' != bytes[ payloadStart ] ) {
			return false;
		}

		int fieldStart = payloadStart + 1;
		for ( int index = fieldStart; index < bytes.Count; ++index ) {
			byte value = bytes[ index ];
			if ( value is not (byte)',' and not (byte)';' ) {
				continue;
			}
			if ( IsExpectedImageField(
				bytes,
				fieldStart,
				index - fieldStart,
				this.ImageId
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

	private static bool IsExpectedImageField(
		IReadOnlyList<byte> bytes,
		int start,
		int length,
		uint expected
	) {
		if ( 3 > length
			|| 0 > start
			|| bytes.Count < start + length
			|| (byte)'i' != bytes[ start ]
			|| (byte)'=' != bytes[ start + 1 ] ) {
			return false;
		}

		uint parsed = 0;
		for ( int index = start + 2; index < start + length; ++index ) {
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
