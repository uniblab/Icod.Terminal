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
/// Encodes the bounded output-only portion of the Kitty OSC 99 desktop-notification protocol.
/// </summary>
internal static class TerminalOsc99NotificationEncoder {
	internal const int MaximumIdentifierLength = 128;
	internal const int MaximumPayloadChunkLength = 4096;
	internal const int MaximumTextBytes = 65_536;
	internal const int MaximumIconBytes = 262_144;
	internal const int MaximumMetadataTextBytes = 4096;
	internal const int MaximumMetadataItems = 16;
	internal const int MaximumFrameBytes = 16_384;

	private static readonly UTF8Encoding StrictUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[][] EncodeNotificationFrames(
		string title,
		string body,
		KittyNotificationOptions? options
	) {
		ArgumentNullException.ThrowIfNull( title );
		ArgumentNullException.ThrowIfNull( body );
		options ??= new KittyNotificationOptions();
		if ( 0 == title.Length && 0 == body.Length ) {
			throw new ArgumentException(
				"A Kitty OSC 99 notification requires a non-empty title or body."
			);
		}

		byte[] titleBytes = EncodeBoundedUtf8(
			title,
			nameof( title ),
			MaximumTextBytes
		);
		byte[] bodyBytes = EncodeBoundedUtf8(
			body,
			nameof( body ),
			MaximumTextBytes
		);
		ValidateOptions( options );

		byte[]? iconData = options.IconData?.ToArray();
		if ( iconData is not null ) {
			ValidateIconData( iconData );
		}

		List<PayloadPart> parts = [];
		if ( iconData is not null ) {
			AppendBase64Parts(
				parts,
				"icon",
				iconData
			);
		}
		if ( 0 != titleBytes.Length ) {
			AppendBase64Parts(
				parts,
				"title",
				titleBytes
			);
		}
		if ( 0 != bodyBytes.Length ) {
			AppendBase64Parts(
				parts,
				"body",
				bodyBytes
			);
		}
		if ( 0 == parts.Count ) {
			throw new InvalidOperationException(
				"The Kitty OSC 99 notification encoder produced no payload parts."
			);
		}

		bool requiresIdentifier = 1 < parts.Count || options.Identifier is not null;
		string? identifier = options.Identifier;
		if ( requiresIdentifier && identifier is null ) {
			identifier = "icod" + Guid.NewGuid().ToString( "N", CultureInfo.InvariantCulture );
		}

		List<string> commonMetadata = BuildCommonMetadata(
			options,
			identifier
		);
		byte[][] frames = new byte[ parts.Count ][];
		for ( int index = 0; index < parts.Count; ++index ) {
			PayloadPart part = parts[ index ];
			List<string> metadata = [];
			if ( 0 == index ) {
				metadata.AddRange( commonMetadata );
			} else if ( identifier is not null ) {
				metadata.Add( "i=" + identifier );
			}
			if ( "icon" == part.PayloadType && options.IconDataIdentifier is not null ) {
				if ( !metadata.Any( value => value.StartsWith( "g=", StringComparison.Ordinal ) ) ) {
					metadata.Add( "g=" + options.IconDataIdentifier );
				}
			}
			metadata.Add( "p=" + part.PayloadType );
			metadata.Add( "e=1" );
			metadata.Add( index + 1 == parts.Count ? "d=1" : "d=0" );
			frames[ index ] = EncodeFrame(
				metadata,
				part.EncodedPayload
			);
		}
		return frames;
	}

	internal static byte[] EncodeCloseFrame(
		string identifier
	) {
		ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		return EncodeFrame(
			[
				"i=" + identifier,
				"p=close"
			],
			string.Empty
		);
	}

	internal static void ValidateIdentifier(
		string identifier,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( identifier );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( "0" == identifier ) {
			throw new ArgumentException(
				"The Kitty OSC 99 identifier '0' is reserved for backwards-compatible anonymous reports.",
				parameterName
			);
		}
		if ( MaximumIdentifierLength < identifier.Length ) {
			throw new ArgumentException(
				$"A Kitty OSC 99 identifier cannot exceed {MaximumIdentifierLength} ASCII characters.",
				parameterName
			);
		}
		foreach ( char character in identifier ) {
			bool allowed = ( 'a' <= character && 'z' >= character )
				|| ( 'A' <= character && 'Z' >= character )
				|| ( '0' <= character && '9' >= character )
				|| '_' == character
				|| '-' == character
				|| '+' == character
				|| '.' == character;
			if ( !allowed ) {
				throw new ArgumentException(
					"A Kitty OSC 99 identifier may contain only ASCII letters, digits, '_', '-', '+', and '.'.",
					parameterName
				);
			}
		}
	}

	private static void ValidateOptions(
		KittyNotificationOptions options
	) {
		ArgumentNullException.ThrowIfNull( options );
		if ( options.Identifier is not null ) {
			ValidateIdentifier(
				options.Identifier,
				nameof( options.Identifier )
			);
		}
		if ( options.IconDataIdentifier is not null ) {
			ValidateIdentifier(
				options.IconDataIdentifier,
				nameof( options.IconDataIdentifier )
			);
		}
		if ( !Enum.IsDefined( options.Occasion ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( options ),
				options.Occasion,
				"The Kitty notification occasion is not recognized."
			);
		}
		if ( options.Urgency is KittyNotificationUrgency urgency
			&& !Enum.IsDefined( urgency ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( options ),
				urgency,
				"The Kitty notification urgency is not recognized."
			);
		}
		if ( options.Expiration is TimeSpan expiration ) {
			if ( TimeSpan.Zero > expiration
				|| int.MaxValue < expiration.TotalMilliseconds ) {
				throw new ArgumentOutOfRangeException(
					nameof( options ),
					expiration,
					"Kitty notification expiration must be zero or a positive interval no greater than Int32.MaxValue milliseconds."
				);
			}
		}

		ValidateOptionalMetadataText(
			options.ApplicationName,
			nameof( options.ApplicationName )
		);
		ValidateOptionalMetadataText(
			options.SoundName,
			nameof( options.SoundName )
		);
		ValidateMetadataCollection(
			options.NotificationTypes,
			nameof( options.NotificationTypes )
		);
		ValidateMetadataCollection(
			options.IconNames,
			nameof( options.IconNames )
		);
	}

	private static List<string> BuildCommonMetadata(
		KittyNotificationOptions options,
		string? identifier
	) {
		ArgumentNullException.ThrowIfNull( options );
		List<string> metadata = [];
		if ( identifier is not null ) {
			metadata.Add( "i=" + identifier );
		}
		if ( options.ApplicationName is not null ) {
			metadata.Add( "f=" + EncodeMetadataText( options.ApplicationName ) );
		}
		foreach ( string notificationType in options.NotificationTypes ) {
			metadata.Add( "t=" + EncodeMetadataText( notificationType ) );
		}
		if ( !options.FocusOnActivation ) {
			metadata.Add( "a=-focus" );
		}
		if ( KittyNotificationOccasion.Always != options.Occasion ) {
			metadata.Add(
				"o=" + GetOccasionWireName( options.Occasion )
			);
		}
		if ( options.Urgency is KittyNotificationUrgency urgency ) {
			metadata.Add(
				"u=" + ( (int)urgency ).ToString( CultureInfo.InvariantCulture )
			);
		}
		if ( options.Expiration is TimeSpan expiration ) {
			long milliseconds = TimeSpan.Zero == expiration
				? 0
				: checked( (long)Math.Ceiling( expiration.TotalMilliseconds ) )
			;
			metadata.Add(
				"w=" + milliseconds.ToString( CultureInfo.InvariantCulture )
			);
		}
		if ( options.SoundName is not null ) {
			metadata.Add( "s=" + EncodeMetadataText( options.SoundName ) );
		}
		foreach ( string iconName in options.IconNames ) {
			metadata.Add( "n=" + EncodeMetadataText( iconName ) );
		}
		if ( options.IconDataIdentifier is not null ) {
			metadata.Add( "g=" + options.IconDataIdentifier );
		}
		return metadata;
	}

	private static string GetOccasionWireName(
		KittyNotificationOccasion occasion
	) {
		return occasion switch {
			KittyNotificationOccasion.Always => "always",
			KittyNotificationOccasion.Unfocused => "unfocused",
			KittyNotificationOccasion.Invisible => "invisible",
			_ => throw new ArgumentOutOfRangeException( nameof( occasion ) )
		};
	}

	private static void AppendBase64Parts(
		List<PayloadPart> parts,
		string payloadType,
		byte[] payload
	) {
		ArgumentNullException.ThrowIfNull( parts );
		ArgumentException.ThrowIfNullOrEmpty( payloadType );
		ArgumentNullException.ThrowIfNull( payload );
		string encoded = Convert.ToBase64String( payload );
		if ( 0 == encoded.Length ) {
			parts.Add(
				new PayloadPart(
					payloadType,
					string.Empty
				)
			);
			return;
		}
		for ( int offset = 0; offset < encoded.Length; offset += MaximumPayloadChunkLength ) {
			int count = Math.Min(
				MaximumPayloadChunkLength,
				encoded.Length - offset
			);
			parts.Add(
				new PayloadPart(
					payloadType,
					encoded.Substring(
						offset,
						count
					)
				)
			);
		}
	}

	private static byte[] EncodeFrame(
		IReadOnlyList<string> metadata,
		string payload
	) {
		ArgumentNullException.ThrowIfNull( metadata );
		ArgumentNullException.ThrowIfNull( payload );
		string body = "99;" + string.Join( ':', metadata ) + ";" + payload;
		byte[] encoded = Encoding.ASCII.GetBytes( body );
		if ( MaximumFrameBytes < encoded.Length ) {
			throw new ArgumentException(
				$"A Kitty OSC 99 frame cannot exceed {MaximumFrameBytes} bytes."
			);
		}
		byte[] frame = new byte[ encoded.Length + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		encoded.CopyTo(
			frame,
			2
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static string EncodeMetadataText(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		return Convert.ToBase64String(
			EncodeBoundedUtf8(
				value,
				nameof( value ),
				MaximumMetadataTextBytes
			)
		);
	}

	private static void ValidateOptionalMetadataText(
		string? value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( value is null ) {
			return;
		}
		EncodeBoundedUtf8(
			value,
			parameterName,
			MaximumMetadataTextBytes
		);
	}

	private static void ValidateMetadataCollection(
		IReadOnlyList<string> values,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( values );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( MaximumMetadataItems < values.Count ) {
			throw new ArgumentException(
				$"Kitty OSC 99 metadata collections cannot contain more than {MaximumMetadataItems} entries.",
				parameterName
			);
		}
		for ( int index = 0; index < values.Count; ++index ) {
			string? value = values[ index ];
			if ( value is null ) {
				throw new ArgumentException(
					"Kitty OSC 99 metadata collections cannot contain null values.",
					parameterName
				);
			}
			EncodeBoundedUtf8(
				value,
				parameterName,
				MaximumMetadataTextBytes
			);
		}
	}

	private static byte[] EncodeBoundedUtf8(
		string value,
		string parameterName,
		int maximumBytes
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( 0 > maximumBytes ) {
			throw new ArgumentOutOfRangeException( nameof( maximumBytes ) );
		}
		byte[] bytes;
		try {
			bytes = StrictUtf8.GetBytes( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"Kitty OSC 99 text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}
		if ( maximumBytes < bytes.Length ) {
			throw new ArgumentException(
				$"Kitty OSC 99 text cannot exceed {maximumBytes} UTF-8 bytes.",
				parameterName
			);
		}
		return bytes;
	}

	private static void ValidateIconData(
		byte[] iconData
	) {
		ArgumentNullException.ThrowIfNull( iconData );
		if ( 0 == iconData.Length || MaximumIconBytes < iconData.Length ) {
			throw new ArgumentException(
				$"Kitty OSC 99 icon data must contain between 1 and {MaximumIconBytes} bytes.",
				nameof( KittyNotificationOptions.IconData )
			);
		}

		bool png = 8 <= iconData.Length
			&& 0x89 == iconData[ 0 ]
			&& (byte)'P' == iconData[ 1 ]
			&& (byte)'N' == iconData[ 2 ]
			&& (byte)'G' == iconData[ 3 ]
			&& 0x0d == iconData[ 4 ]
			&& 0x0a == iconData[ 5 ]
			&& 0x1a == iconData[ 6 ]
			&& 0x0a == iconData[ 7 ];
		bool jpeg = 3 <= iconData.Length
			&& 0xff == iconData[ 0 ]
			&& 0xd8 == iconData[ 1 ]
			&& 0xff == iconData[ 2 ];
		bool gif = 6 <= iconData.Length
			&& (byte)'G' == iconData[ 0 ]
			&& (byte)'I' == iconData[ 1 ]
			&& (byte)'F' == iconData[ 2 ]
			&& (byte)'8' == iconData[ 3 ]
			&& ( (byte)'7' == iconData[ 4 ] || (byte)'9' == iconData[ 4 ] )
			&& (byte)'a' == iconData[ 5 ];
		if ( !png && !jpeg && !gif ) {
			throw new ArgumentException(
				"Kitty OSC 99 transmitted icons must be PNG, JPEG, or GIF data.",
				nameof( KittyNotificationOptions.IconData )
			);
		}
	}

	private sealed record PayloadPart(
		string PayloadType,
		string EncodedPayload
	);
}
