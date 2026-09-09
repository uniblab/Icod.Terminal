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
/// Builds structurally validated canonical seven-bit DCS frames for small,
/// bounded internal terminal operations.
/// </summary>
internal static class DcsWriter {
	internal const int MaximumEncodedFrameBytes = 4096;

	private const byte EscapeByte = 0x1B;
	private const byte DcsFinalByte = 0x50;
	private const byte StringTerminatorFinalByte = 0x5C;
	private const byte EightBitStringTerminatorByte = 0x9C;

	/// <summary>
	/// Encodes one complete canonical seven-bit DCS frame from structural fields.
	/// </summary>
	internal static byte[] EncodeFrame(
		ReadOnlySpan<byte> parameterBytes,
		ReadOnlySpan<byte> intermediateBytes,
		byte finalByte,
		ReadOnlySpan<byte> payloadBytes
	) {
		ValidateParameterBytes( parameterBytes );
		ValidateIntermediateBytes( intermediateBytes );
		ValidateFinalByte( finalByte );
		ValidatePayloadBytes( payloadBytes );

		int frameLength = checked(
			5
				+ parameterBytes.Length
				+ intermediateBytes.Length
				+ payloadBytes.Length
		);
		if ( MaximumEncodedFrameBytes < frameLength ) {
			throw new ArgumentOutOfRangeException(
				nameof( payloadBytes ),
				payloadBytes.Length,
				$"A complete small DCS frame cannot exceed {MaximumEncodedFrameBytes} bytes."
			);
		}

		byte[] frame = new byte[ frameLength ];
		frame[ 0 ] = EscapeByte;
		frame[ 1 ] = DcsFinalByte;

		int offset = 2;
		parameterBytes.CopyTo( frame.AsSpan( offset ) );
		offset += parameterBytes.Length;
		intermediateBytes.CopyTo( frame.AsSpan( offset ) );
		offset += intermediateBytes.Length;
		frame[ offset++ ] = finalByte;
		payloadBytes.CopyTo( frame.AsSpan( offset ) );
		offset += payloadBytes.Length;
		frame[ offset++ ] = EscapeByte;
		frame[ offset ] = StringTerminatorFinalByte;
		return frame;
	}

	private static void ValidateParameterBytes(
		ReadOnlySpan<byte> parameterBytes
	) {
		for ( int index = 0; index < parameterBytes.Length; index++ ) {
			byte value = parameterBytes[ index ];
			if ( value is < 0x30 or > 0x3F ) {
				throw new ArgumentException(
					"DCS parameter bytes must be in the inclusive range 0x30 through 0x3F.",
					nameof( parameterBytes )
				);
			}
		}
	}

	private static void ValidateIntermediateBytes(
		ReadOnlySpan<byte> intermediateBytes
	) {
		for ( int index = 0; index < intermediateBytes.Length; index++ ) {
			byte value = intermediateBytes[ index ];
			if ( value is < 0x20 or > 0x2F ) {
				throw new ArgumentException(
					"DCS intermediate bytes must be in the inclusive range 0x20 through 0x2F.",
					nameof( intermediateBytes )
				);
			}
		}
	}

	private static void ValidateFinalByte(
		byte finalByte
	) {
		if ( finalByte is < 0x40 or > 0x7E ) {
			throw new ArgumentOutOfRangeException(
				nameof( finalByte ),
				finalByte,
				"A DCS final selector must be in the inclusive range 0x40 through 0x7E."
			);
		}
	}

	private static void ValidatePayloadBytes(
		ReadOnlySpan<byte> payloadBytes
	) {
		for ( int index = 0; index < payloadBytes.Length; index++ ) {
			byte value = payloadBytes[ index ];
			if ( value is EscapeByte or 0x18 or 0x1A or EightBitStringTerminatorByte ) {
				throw new ArgumentException(
					"A canonical DCS payload cannot contain ESC, CAN, SUB, or the eight-bit ST byte because those bytes alter DCS framing.",
					nameof( payloadBytes )
				);
			}
		}
	}
}
