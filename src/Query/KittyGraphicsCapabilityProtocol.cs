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
/// Composes and correlates the protocol-defined Kitty Graphics support probe.
/// </summary>
internal static class KittyGraphicsCapabilityProtocol {
	internal static byte[] CreateProbeRequest(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}

		byte[] graphicsFrame = ApcWriter.EncodeFrame(
			KittyGraphicsCodec.EncodeSupportQueryPayload( imageId )
		);
		ReadOnlyMemory<byte> primaryDa =
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesRequest;
		byte[] request = new byte[ checked( graphicsFrame.Length + primaryDa.Length ) ];
		graphicsFrame.CopyTo(
			request,
			0
		);
		primaryDa.Span.CopyTo(
			request.AsSpan( graphicsFrame.Length )
		);
		return request;
	}

	internal static bool IsCorrelatedResponse(
		TerminalResponseFrame frame,
		uint imageId
	) {
		ArgumentNullException.ThrowIfNull( frame );
		ValidateImageId( imageId );
		if ( TerminalResponseFrameKind.Apc != frame.Kind ) {
			return false;
		}

		TerminalControlFrameStructure structure;
		try {
			structure = TerminalControlFrameStructure.Parse( frame );
		} catch ( FormatException ) {
			return false;
		}
		return TerminalControlFamily.Apc == structure.Family
			&& ContainsImageId(
				structure.PayloadBytes.Span,
				imageId
			);
	}

	internal static bool IsCorrelatedResponsePrefix(
		IReadOnlyList<byte> bytes,
		uint imageId
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ValidateImageId( imageId );
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

			if ( IsImageIdField(
				bytes,
				fieldStart,
				index - fieldStart,
				imageId
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

	private static bool ContainsImageId(
		ReadOnlySpan<byte> payload,
		uint imageId
	) {
		if ( 4 > payload.Length || (byte)'G' != payload[ 0 ] ) {
			return false;
		}

		ReadOnlySpan<byte> remainder = payload[ 1.. ];
		int separator = remainder.IndexOf( (byte)';' );
		ReadOnlySpan<byte> controlData = 0 > separator
			? remainder
			: remainder[..separator]
		;
		int offset = 0;
		while ( offset < controlData.Length ) {
			ReadOnlySpan<byte> remaining = controlData[ offset.. ];
			int comma = remaining.IndexOf( (byte)',' );
			ReadOnlySpan<byte> field = 0 > comma
				? remaining
				: remaining[..comma]
			;
			if ( TryParseImageIdField(
				field,
				out uint parsed
			) && imageId == parsed ) {
				return true;
			}

			if ( 0 > comma ) {
				break;
			}
			offset = checked( offset + comma + 1 );
		}
		return false;
	}

	private static bool IsImageIdField(
		IReadOnlyList<byte> bytes,
		int start,
		int length,
		uint imageId
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 3 > length
			|| 0 > start
			|| bytes.Count < start + length
			|| (byte)'i' != bytes[ start ]
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
		return imageId == parsed;
	}

	private static bool TryParseImageIdField(
		ReadOnlySpan<byte> field,
		out uint imageId
	) {
		imageId = 0;
		if ( 3 > field.Length
			|| (byte)'i' != field[ 0 ]
			|| (byte)'=' != field[ 1 ] ) {
			return false;
		}

		uint parsed = 0;
		ReadOnlySpan<byte> value = field[ 2.. ];
		foreach ( byte item in value ) {
			if ( item is < (byte)'0' or > (byte)'9' ) {
				return false;
			}

			uint digit = (uint)( item - (byte)'0' );
			if ( ( uint.MaxValue - digit ) / 10 < parsed ) {
				return false;
			}
			parsed = ( parsed * 10 ) + digit;
		}

		if ( 0 == parsed ) {
			return false;
		}
		imageId = parsed;
		return true;
	}

	private static void ValidateImageId(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}
	}
}
