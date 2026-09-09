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
/// Builds structurally validated canonical seven-bit APC frames for small,
/// bounded internal terminal operations.
/// </summary>
internal static class ApcWriter {
	internal const int MaximumEncodedFrameBytes = 8192;

	private const byte EscapeByte = 0x1B;
	private const byte ApcFinalByte = 0x5F;
	private const byte StringTerminatorFinalByte = 0x5C;
	private const byte EightBitStringTerminatorByte = 0x9C;

	/// <summary>
	/// Encodes one complete canonical seven-bit APC frame.
	/// </summary>
	internal static byte[] EncodeFrame(
		ReadOnlySpan<byte> payloadBytes
	) {
		ValidatePayloadBytes( payloadBytes );

		int frameLength = checked( 4 + payloadBytes.Length );
		if ( MaximumEncodedFrameBytes < frameLength ) {
			throw new ArgumentOutOfRangeException(
				nameof( payloadBytes ),
				payloadBytes.Length,
				$"A complete small APC frame cannot exceed {MaximumEncodedFrameBytes} bytes."
			);
		}

		byte[] frame = new byte[ frameLength ];
		frame[ 0 ] = EscapeByte;
		frame[ 1 ] = ApcFinalByte;
		payloadBytes.CopyTo( frame.AsSpan( 2 ) );
		frame[ ^2 ] = EscapeByte;
		frame[ ^1 ] = StringTerminatorFinalByte;
		return frame;
	}

	private static void ValidatePayloadBytes(
		ReadOnlySpan<byte> payloadBytes
	) {
		for ( int index = 0; index < payloadBytes.Length; index++ ) {
			byte value = payloadBytes[ index ];
			if ( value is EscapeByte or 0x18 or 0x1A or EightBitStringTerminatorByte ) {
				throw new ArgumentException(
					"A canonical APC payload cannot contain ESC, CAN, SUB, or the eight-bit ST byte because those bytes alter APC framing.",
					nameof( payloadBytes )
				);
			}
		}
	}
}
