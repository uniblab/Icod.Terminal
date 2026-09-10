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

using System.Globalization;
using System.Text;

/// <summary>
/// Encodes the reviewed Kitty Graphics query grammar and parses bounded
/// correlated Kitty Graphics APC responses.
/// </summary>
internal static class KittyGraphicsCodec {
	internal const int MaximumControlDataBytes = 1024;
	internal const int MaximumResponseMessageBytes = 1024;

	private const byte GraphicsMarker = (byte)'G';
	private const byte ControlPayloadSeparator = (byte)';';

	/// <summary>
	/// Encodes the application payload for the protocol-defined one-pixel
	/// direct RGB24 support query.
	/// </summary>
	internal static byte[] EncodeSupportQueryPayload(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}

		string text = "Gi="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",s=1,v=1,a=q,t=d,f=24;AAAA";
		return Encoding.ASCII.GetBytes( text );
	}

	/// <summary>
	/// Parses one structurally complete Kitty Graphics response APC.
	/// </summary>
	internal static KittyGraphicsResponse ParseResponse(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Apc != frame.Kind ) {
			throw new FormatException(
				"The terminal response is not an APC frame."
			);
		}

		TerminalControlFrameStructure structure =
			TerminalControlFrameStructure.Parse( frame );
		if ( TerminalControlFamily.Apc != structure.Family ) {
			throw new FormatException(
				"The terminal response is not an APC control string."
			);
		}

		ReadOnlySpan<byte> payload = structure.PayloadBytes.Span;
		if ( 0 == payload.Length || GraphicsMarker != payload[ 0 ] ) {
			throw new FormatException(
				"The APC response is not a Kitty Graphics response."
			);
		}

		ReadOnlySpan<byte> remainder = payload[ 1.. ];
		int separator = remainder.IndexOf( ControlPayloadSeparator );
		if ( 0 >= separator ) {
			throw new FormatException(
				"The Kitty Graphics response is missing non-empty control data or its response separator."
			);
		}

		ReadOnlySpan<byte> controlData = remainder[..separator];
		ReadOnlySpan<byte> messageBytes = remainder[( separator + 1 )..];
		if ( MaximumControlDataBytes < controlData.Length ) {
			throw new FormatException(
				$"The Kitty Graphics response control data exceeds {MaximumControlDataBytes} bytes."
			);
		}
		if ( 0 == messageBytes.Length ) {
			throw new FormatException(
				"The Kitty Graphics response message cannot be empty."
			);
		}
		if ( MaximumResponseMessageBytes < messageBytes.Length ) {
			throw new FormatException(
				$"The Kitty Graphics response message exceeds {MaximumResponseMessageBytes} bytes."
			);
		}

		ParseResponseControlData(
			controlData,
			out uint imageId,
			out uint? imageNumber,
			out uint? placementId
		);
		string message = ParseResponseMessage( messageBytes );
		return new KittyGraphicsResponse(
			imageId,
			imageNumber,
			placementId,
			string.Equals(
				"OK",
				message,
				StringComparison.Ordinal
			),
			message
		);
	}

	private static void ParseResponseControlData(
		ReadOnlySpan<byte> controlData,
		out uint imageId,
		out uint? imageNumber,
		out uint? placementId
	) {
		imageId = 0;
		imageNumber = null;
		placementId = null;
		Span<bool> seenKeys = stackalloc bool[ 128 ];

		int offset = 0;
		while ( offset < controlData.Length ) {
			ReadOnlySpan<byte> remaining = controlData[ offset.. ];
			int comma = remaining.IndexOf( (byte)',' );
			ReadOnlySpan<byte> field = 0 > comma
				? remaining
				: remaining[..comma]
			;
			if ( 3 > field.Length
				|| !IsAsciiLetter( field[ 0 ] )
				|| (byte)'=' != field[ 1 ] ) {
				throw new FormatException(
					"The Kitty Graphics response control data contains a malformed key/value field."
				);
			}

			byte key = field[ 0 ];
			if ( seenKeys[ key ] ) {
				throw new FormatException(
					"The Kitty Graphics response control data contains a duplicate key."
				);
			}
			seenKeys[ key ] = true;

			ReadOnlySpan<byte> value = field[ 2.. ];
			ValidateControlValue( value );
			switch ( key ) {
				case (byte)'i':
					imageId = ParseUInt32(
						value,
						"i"
					);
					if ( 0 == imageId ) {
						throw new FormatException(
							"A correlated Kitty Graphics response image id must be non-zero."
						);
					}
					break;
				case (byte)'I':
					imageNumber = ParseUInt32(
						value,
						"I"
					);
					break;
				case (byte)'p':
					placementId = ParseUInt32(
						value,
						"p"
					);
					break;
			}

			if ( 0 > comma ) {
				break;
			}
			offset = checked( offset + comma + 1 );
			if ( controlData.Length == offset ) {
				throw new FormatException(
					"The Kitty Graphics response control data contains an empty field."
				);
			}
		}

		if ( !seenKeys[ (byte)'i' ] ) {
			throw new FormatException(
				"A correlated Kitty Graphics response must contain an image id."
			);
		}
	}

	private static void ValidateControlValue(
		ReadOnlySpan<byte> value
	) {
		if ( value.IsEmpty ) {
			throw new FormatException(
				"A Kitty Graphics response control value cannot be empty."
			);
		}
		foreach ( byte item in value ) {
			if ( item is < 0x20 or > 0x7E
				|| (byte)',' == item ) {
				throw new FormatException(
					"A Kitty Graphics response control value must contain printable ASCII without field separators."
				);
			}
		}
	}

	private static uint ParseUInt32(
		ReadOnlySpan<byte> value,
		string key
	) {
		ArgumentException.ThrowIfNullOrEmpty( key );
		if ( value.IsEmpty ) {
			throw new FormatException(
				$"The Kitty Graphics response key '{key}' has no value."
			);
		}

		uint parsed = 0;
		foreach ( byte item in value ) {
			if ( item is < (byte)'0' or > (byte)'9' ) {
				throw new FormatException(
					$"The Kitty Graphics response key '{key}' must contain an unsigned decimal integer."
				);
			}
			uint digit = (uint)( item - (byte)'0' );
			if ( ( uint.MaxValue - digit ) / 10 < parsed ) {
				throw new FormatException(
					$"The Kitty Graphics response key '{key}' exceeds UInt32.MaxValue."
				);
			}
			parsed = checked( ( parsed * 10 ) + digit );
		}
		return parsed;
	}

	private static string ParseResponseMessage(
		ReadOnlySpan<byte> messageBytes
	) {
		foreach ( byte item in messageBytes ) {
			if ( item is < 0x20 or > 0x7E ) {
				throw new FormatException(
					"The Kitty Graphics response message must contain printable ASCII."
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

/// <summary>
/// Represents one bounded correlated Kitty Graphics protocol response.
/// </summary>
internal sealed class KittyGraphicsResponse {
	internal KittyGraphicsResponse(
		uint imageId,
		uint? imageNumber,
		uint? placementId,
		bool isSuccess,
		string message
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A correlated Kitty Graphics response image id must be non-zero."
			);
		}
		ArgumentException.ThrowIfNullOrEmpty( message );

		this.ImageId = imageId;
		this.ImageNumber = imageNumber;
		this.PlacementId = placementId;
		this.IsSuccess = isSuccess;
		this.Message = message;
	}

	internal uint ImageId {
		get;
	}

	internal uint? ImageNumber {
		get;
	}

	internal uint? PlacementId {
		get;
	}

	internal bool IsSuccess {
		get;
	}

	internal string Message {
		get;
	}
}
