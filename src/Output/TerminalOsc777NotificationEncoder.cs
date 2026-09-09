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

using System.Text;

/// <summary>
/// Encodes the bounded urxvt-style OSC 777 desktop-notification form.
/// </summary>
internal static class TerminalOsc777NotificationEncoder {
	internal const int MaximumPayloadLength = 4_096;

	private static readonly byte[] Prefix = [
		(byte)'7',
		(byte)'7',
		(byte)'7',
		(byte)';',
		(byte)'n',
		(byte)'o',
		(byte)'t',
		(byte)'i',
		(byte)'f',
		(byte)'y',
		(byte)';'
	];
	private static readonly UTF8Encoding StrictUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[] EncodeFrame(
		string title,
		string message
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( message );
		ValidateField(
			title,
			nameof( title )
		);
		ValidateField(
			message,
			nameof( message )
		);

		byte[] encodedTitle = EncodeField(
			title,
			nameof( title )
		);
		byte[] encodedMessage = EncodeField(
			message,
			nameof( message )
		);
		int payloadLength = checked(
			Prefix.Length
				+ encodedTitle.Length
				+ 1
				+ encodedMessage.Length
		);
		if ( MaximumPayloadLength < payloadLength ) {
			throw new ArgumentException(
				$"OSC 777 notification payload cannot exceed {MaximumPayloadLength} encoded bytes.",
				nameof( message )
			);
		}

		byte[] frame = new byte[ payloadLength + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		Prefix.CopyTo(
			frame,
			2
		);
		int offset = 2 + Prefix.Length;
		encodedTitle.CopyTo(
			frame,
			offset
		);
		offset += encodedTitle.Length;
		frame[ offset++ ] = (byte)';';
		encodedMessage.CopyTo(
			frame,
			offset
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static byte[] EncodeField(
		string value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );

		try {
			return StrictUtf8.GetBytes( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 777 notification text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}
	}

	private static void ValidateField(
		string value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );

		foreach ( char character in value ) {
			if ( ';' == character ) {
				throw new ArgumentException(
					"OSC 777 notification fields must not contain semicolons because the protocol defines no field-escaping mechanism.",
					parameterName
				);
			}
			if ( 0x001f >= character
				|| 0x007f == character
				|| ( 0x0080 <= character && 0x009f >= character ) ) {
				throw new ArgumentException(
					"OSC 777 notification text must not contain C0, DEL, or C1 control characters.",
					parameterName
				);
			}
		}
	}
}
