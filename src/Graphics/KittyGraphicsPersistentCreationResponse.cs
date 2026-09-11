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
/// Parses one bounded persistent-resource creation acknowledgement correlated by image number.
/// </summary>
internal sealed class KittyGraphicsPersistentCreationResponse {
	private KittyGraphicsPersistentCreationResponse(
		uint imageNumber,
		uint? imageId,
		bool isSuccess,
		string message
	) {
		this.ImageNumber = imageNumber;
		this.ImageId = imageId;
		this.IsSuccess = isSuccess;
		this.Message = message;
	}

	internal uint ImageNumber {
		get;
	}

	internal uint? ImageId {
		get;
	}

	internal bool IsSuccess {
		get;
	}

	internal bool IsUnavailable {
		get {
			return !this.IsSuccess
				&& this.Message.StartsWith(
					"ENOENT",
					StringComparison.Ordinal
				);
		}
	}

	internal string Message {
		get;
	}

	internal static KittyGraphicsPersistentCreationResponse Parse(
		TerminalResponseFrame frame,
		uint expectedImageNumber
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( 0u == expectedImageNumber ) {
			throw new ArgumentOutOfRangeException( nameof( expectedImageNumber ) );
		}
		if ( TerminalResponseFrameKind.Apc != frame.Kind ) {
			throw new FormatException(
				"The persistent Kitty Graphics response is not an APC frame."
			);
		}

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse( frame );
		if ( TerminalControlFamily.Apc != structure.Family ) {
			throw new FormatException(
				"The persistent Kitty Graphics response is not an APC control string."
			);
		}

		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		if ( 0 == payload.Length || (byte)'G' != payload[ 0 ] ) {
			throw new FormatException(
				"The APC response is not a Kitty Graphics response."
			);
		}

		int separator = payload.IndexOf( (byte)';' );
		if ( 1 >= separator ) {
			throw new FormatException(
				"The persistent Kitty Graphics response is missing control data or its response separator."
			);
		}

		ReadOnlySpan<byte> controlData = payload[1..separator];
		ReadOnlySpan<byte> messageBytes = payload[( separator + 1 )..];
		if ( KittyGraphicsCodec.MaximumControlDataBytes < controlData.Length ) {
			throw new FormatException(
				$"The Kitty Graphics response control data exceeds {KittyGraphicsCodec.MaximumControlDataBytes} bytes."
			);
		}
		if ( 0 == messageBytes.Length ) {
			throw new FormatException(
				"The Kitty Graphics response message cannot be empty."
			);
		}
		if ( KittyGraphicsCodec.MaximumResponseMessageBytes < messageBytes.Length ) {
			throw new FormatException(
				$"The Kitty Graphics response message exceeds {KittyGraphicsCodec.MaximumResponseMessageBytes} bytes."
			);
		}

		ParseControlData(
			controlData,
			out uint? imageId,
			out uint? imageNumber
		);
		if ( !imageNumber.HasValue ) {
			throw new FormatException(
				"A persistent Kitty Graphics creation response must contain the correlated image number."
			);
		}
		if ( expectedImageNumber != imageNumber.Value ) {
			throw new FormatException(
				"The persistent Kitty Graphics creation response contains the wrong image number."
			);
		}

		string message = ParseMessage( messageBytes );
		bool success = string.Equals(
			"OK",
			message,
			StringComparison.Ordinal
		);
		if ( success && !imageId.HasValue ) {
			throw new FormatException(
				"A successful persistent Kitty Graphics creation response must contain a terminal-assigned image id."
			);
		}

		return new KittyGraphicsPersistentCreationResponse(
			imageNumber.Value,
			imageId,
			success,
			message
		);
	}

	private static void ParseControlData(
		ReadOnlySpan<byte> controlData,
		out uint? imageId,
		out uint? imageNumber
	) {
		imageId = null;
		imageNumber = null;
		Span<bool> seenKeys = stackalloc bool[ 128 ];

		int offset = 0;
		while ( offset < controlData.Length ) {
			ReadOnlySpan<byte> remaining = controlData[offset..];
			int comma = remaining.IndexOf( (byte)',' );
			ReadOnlySpan<byte> field = 0 > comma
				? remaining
				: remaining[..comma]
			;
			if ( 3 > field.Length
				|| !IsAsciiLetter( field[ 0 ] )
				|| (byte)'=' != field[ 1 ] ) {
				throw new FormatException(
					"The persistent Kitty Graphics response control data contains a malformed key/value field."
				);
			}

			byte key = field[ 0 ];
			if ( seenKeys[ key ] ) {
				throw new FormatException(
					"The persistent Kitty Graphics response control data contains a duplicate key."
				);
			}
			seenKeys[ key ] = true;

			ReadOnlySpan<byte> value = field[2..];
			ValidateControlValue( value );
			switch ( key ) {
				case (byte)'i':
					uint parsedImageId = ParseUInt32(
						value,
						"i"
					);
					if ( 0u == parsedImageId ) {
						throw new FormatException(
							"A terminal-assigned persistent Kitty Graphics image id must be non-zero."
						);
					}
					imageId = parsedImageId;
					break;

				case (byte)'I':
					uint parsedImageNumber = ParseUInt32(
						value,
						"I"
					);
					if ( 0u == parsedImageNumber ) {
						throw new FormatException(
							"A persistent Kitty Graphics image number must be non-zero."
						);
					}
					imageNumber = parsedImageNumber;
					break;
			}

			if ( 0 > comma ) {
				break;
			}
			offset = checked( offset + comma + 1 );
			if ( controlData.Length == offset ) {
				throw new FormatException(
					"The persistent Kitty Graphics response control data contains an empty field."
				);
			}
		}
	}

	private static void ValidateControlValue(
		ReadOnlySpan<byte> value
	) {
		if ( value.IsEmpty ) {
			throw new FormatException(
				"A persistent Kitty Graphics response control value cannot be empty."
			);
		}
		foreach ( byte item in value ) {
			if ( item is < 0x20 or > 0x7E
				|| (byte)',' == item ) {
				throw new FormatException(
					"A persistent Kitty Graphics response control value must contain printable ASCII without field separators."
				);
			}
		}
	}

	private static uint ParseUInt32(
		ReadOnlySpan<byte> value,
		string key
	) {
		ArgumentException.ThrowIfNullOrEmpty( key );

		uint parsed = 0;
		foreach ( byte item in value ) {
			if ( item is < (byte)'0' or > (byte)'9' ) {
				throw new FormatException(
					$"The persistent Kitty Graphics response key '{key}' must contain an unsigned decimal integer."
				);
			}
			uint digit = (uint)( item - (byte)'0' );
			if ( ( uint.MaxValue - digit ) / 10 < parsed ) {
				throw new FormatException(
					$"The persistent Kitty Graphics response key '{key}' exceeds UInt32.MaxValue."
				);
			}
			parsed = ( parsed * 10 ) + digit;
		}
		return parsed;
	}

	private static string ParseMessage(
		ReadOnlySpan<byte> messageBytes
	) {
		foreach ( byte item in messageBytes ) {
			if ( item is < 0x20 or > 0x7E ) {
				throw new FormatException(
					"The persistent Kitty Graphics response message must contain printable ASCII."
				);
			}
		}
		return Encoding.ASCII.GetString( messageBytes );
	}

	private static bool IsAsciiLetter(
		byte value
	) {
		return value is >= (byte)'A' and <= (byte)'Z'
			or >= (byte)'a' and <= (byte)'z';
	}
}
