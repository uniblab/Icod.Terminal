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
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}
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
}
