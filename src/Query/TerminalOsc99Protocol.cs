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
/// Implements correlated Kitty OSC 99 capability and alive-notification queries.
/// </summary>
internal static class TerminalOsc99Protocol {
	internal const int MaximumSupportPayloadBytes = 4096;
	internal const int MaximumAliveIdentifiers = 256;

	internal static string CreateQueryIdentifier() {
		return "icodq" + Guid.NewGuid().ToString( "N" );
	}

	internal static byte[] CreateSupportQueryRequest(
		string identifier
	) {
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		return CreateQueryRequest(
			identifier,
			"?"
		);
	}

	internal static byte[] CreateAliveQueryRequest(
		string identifier
	) {
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		return CreateQueryRequest(
			identifier,
			"alive"
		);
	}

	internal static ITerminalResponseMatcher CreateSupportResponseMatcher(
		string identifier
	) {
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		return new TerminalOsc99ResponseMatcher(
			identifier,
			"?"
		);
	}

	internal static ITerminalResponseMatcher CreateAliveResponseMatcher(
		string identifier
	) {
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		return new TerminalOsc99ResponseMatcher(
			identifier,
			"alive"
		);
	}

	internal static KittyNotificationSupport ParseSupportResponse(
		TerminalResponseFrame frame,
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( frame );
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		Osc99Frame parsed = ParseFrame( frame );
		RequireQueryIdentity(
			parsed,
			identifier,
			"?"
		);
		if ( MaximumSupportPayloadBytes < parsed.Payload.Length ) {
			throw new FormatException(
				$"The Kitty OSC 99 support response exceeds {MaximumSupportPayloadBytes} bytes."
			);
		}

		bool supportsFocus = false;
		bool supportsReport = false;
		bool supportsCloseEvents = false;
		bool supportsTitle = false;
		bool supportsBody = false;
		bool supportsClose = false;
		bool supportsIconData = false;
		bool supportsAlive = false;
		bool supportsButtons = false;
		bool supportsAutoExpiration = false;
		List<KittyNotificationOccasion> occasions = [];
		List<KittyNotificationUrgency> urgencies = [];
		List<string> sounds = [];

		foreach ( KeyValuePair<string, string> item in ParseMetadataText( parsed.Payload ) ) {
			switch ( item.Key ) {
				case "a":
					foreach ( string value in SplitCommaList( item.Value ) ) {
						if ( "focus" == value ) {
							supportsFocus = true;
						} else if ( "report" == value ) {
							supportsReport = true;
						}
					}
					break;
				case "c":
					supportsCloseEvents = ParseSupportBoolean(
						item.Value,
						"c"
					);
					break;
				case "o":
					foreach ( string value in SplitCommaList( item.Value ) ) {
						if ( TryParseOccasion(
							value,
							out KittyNotificationOccasion occasion
						) ) {
							occasions.Add( occasion );
						}
					}
					break;
				case "p":
					foreach ( string value in SplitCommaList( item.Value ) ) {
						switch ( value ) {
							case "title":
								supportsTitle = true;
								break;
							case "body":
								supportsBody = true;
								break;
							case "close":
								supportsClose = true;
								break;
							case "icon":
								supportsIconData = true;
								break;
							case "alive":
								supportsAlive = true;
								break;
							case "buttons":
								supportsButtons = true;
								break;
						}
					}
					break;
				case "s":
					foreach ( string value in SplitCommaList( item.Value ) ) {
						if ( 0 != value.Length ) {
							sounds.Add( value );
						}
					}
					break;
				case "u":
					foreach ( string value in SplitCommaList( item.Value ) ) {
						if ( TryParseUrgency(
							value,
							out KittyNotificationUrgency urgency
						) ) {
							urgencies.Add( urgency );
						}
					}
					break;
				case "w":
					supportsAutoExpiration = ParseSupportBoolean(
						item.Value,
						"w"
					);
					break;
			}
		}

		return new KittyNotificationSupport(
			supportsFocus,
			supportsReport,
			supportsCloseEvents,
			supportsTitle,
			supportsBody,
			supportsClose,
			supportsIconData,
			supportsAlive,
			supportsButtons,
			supportsAutoExpiration,
			occasions,
			urgencies,
			sounds
		);
	}

	internal static IReadOnlyList<string> ParseAliveResponse(
		TerminalResponseFrame frame,
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( frame );
		TerminalOsc99NotificationEncoder.ValidateIdentifier(
			identifier,
			nameof( identifier )
		);
		Osc99Frame parsed = ParseFrame( frame );
		RequireQueryIdentity(
			parsed,
			identifier,
			"alive"
		);
		if ( MaximumSupportPayloadBytes < parsed.Payload.Length ) {
			throw new FormatException(
				$"The Kitty OSC 99 alive response exceeds {MaximumSupportPayloadBytes} bytes."
			);
		}
		if ( 0 == parsed.Payload.Length ) {
			return Array.Empty<string>();
		}

		string text = GetAsciiString(
			parsed.Payload,
			"The Kitty OSC 99 alive response is not valid ASCII."
		);
		string[] values = text.Split(
			',',
			StringSplitOptions.None
		);
		if ( MaximumAliveIdentifiers < values.Length ) {
			throw new FormatException(
				$"The Kitty OSC 99 alive response cannot contain more than {MaximumAliveIdentifiers} identifiers."
			);
		}
		List<string> identifiers = [];
		foreach ( string value in values ) {
			if ( !IsValidReturnedIdentifier( value ) ) {
				throw new FormatException(
					"The Kitty OSC 99 alive response contains an invalid notification identifier."
				);
			}
			identifiers.Add( value );
		}
		return identifiers.ToArray();
	}

	private static byte[] CreateQueryRequest(
		string identifier,
		string payloadType
	) {
		ArgumentException.ThrowIfNullOrEmpty( identifier );
		ArgumentException.ThrowIfNullOrEmpty( payloadType );
		string body = "99;i=" + identifier + ":p=" + payloadType + ";";
		byte[] payload = Encoding.ASCII.GetBytes( body );
		byte[] frame = new byte[ payload.Length + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		payload.CopyTo(
			frame,
			2
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static Osc99Frame ParseFrame(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			throw new FormatException( "The terminal response is not an OSC frame." );
		}
		ReadOnlySpan<byte> bytes = frame.Bytes.Span;
		int start;
		int end;
		if ( 7 <= bytes.Length
			&& 0x1b == bytes[ 0 ]
			&& (byte)']' == bytes[ 1 ] ) {
			start = 2;
			if ( 0x07 == bytes[ ^1 ] ) {
				end = bytes.Length - 1;
			} else if ( 0x1b == bytes[ ^2 ]
				&& (byte)'\\' == bytes[ ^1 ] ) {
				end = bytes.Length - 2;
			} else {
				throw new FormatException( "The OSC 99 response is not terminated." );
			}
		} else if ( 6 <= bytes.Length
			&& 0x9d == bytes[ 0 ]
			&& 0x9c == bytes[ ^1 ] ) {
			start = 1;
			end = bytes.Length - 1;
		} else {
			throw new FormatException( "The terminal response is not a recognized OSC 99 frame." );
		}

		ReadOnlySpan<byte> content = bytes.Slice(
			start,
			end - start
		);
		if ( 3 > content.Length
			|| (byte)'9' != content[ 0 ]
			|| (byte)'9' != content[ 1 ]
			|| (byte)';' != content[ 2 ] ) {
			throw new FormatException( "The terminal response is not an OSC 99 response." );
		}
		ReadOnlySpan<byte> remainder = content[ 3.. ];
		int separator = remainder.IndexOf( (byte)';' );
		if ( 0 > separator ) {
			throw new FormatException(
				"The OSC 99 response is missing its metadata/payload separator."
			);
		}
		return new Osc99Frame(
			remainder[..separator].ToArray(),
			remainder[( separator + 1 )..].ToArray()
		);
	}

	private static void RequireQueryIdentity(
		Osc99Frame frame,
		string identifier,
		string payloadType
	) {
		ArgumentNullException.ThrowIfNull( frame );
		ArgumentException.ThrowIfNullOrEmpty( identifier );
		ArgumentException.ThrowIfNullOrEmpty( payloadType );
		Dictionary<string, string> metadata = ParseMetadata(
			frame.Metadata
		);
		if ( !metadata.TryGetValue( "i", out string? receivedIdentifier )
			|| !string.Equals(
				receivedIdentifier,
				identifier,
				StringComparison.Ordinal
			)
			|| !metadata.TryGetValue( "p", out string? receivedPayloadType )
			|| !string.Equals(
				receivedPayloadType,
				payloadType,
				StringComparison.Ordinal
			) ) {
			throw new FormatException(
				"The OSC 99 response does not match the active query identity."
			);
		}
	}

	private static Dictionary<string, string> ParseMetadata(
		byte[] metadataBytes
	) {
		ArgumentNullException.ThrowIfNull( metadataBytes );
		string metadataText = GetAsciiString(
			metadataBytes,
			"The Kitty OSC 99 response metadata is not valid ASCII."
		);
		Dictionary<string, string> metadata = new( StringComparer.Ordinal );
		if ( 0 == metadataText.Length ) {
			return metadata;
		}
		foreach ( string item in metadataText.Split( ':', StringSplitOptions.None ) ) {
			int separator = item.IndexOf( '=', StringComparison.Ordinal );
			if ( 1 != separator || separator + 1 >= item.Length ) {
				throw new FormatException(
					"The Kitty OSC 99 response metadata contains a malformed key/value pair."
				);
			}
			string key = item[..separator];
			string value = item[( separator + 1 )..];
			if ( metadata.ContainsKey( key ) ) {
				throw new FormatException(
					"The Kitty OSC 99 response metadata contains a duplicate key."
				);
			}
			metadata.Add(
				key,
				value
			);
		}
		return metadata;
	}

	private static IEnumerable<KeyValuePair<string, string>> ParseMetadataText(
		byte[] payload
	) {
		ArgumentNullException.ThrowIfNull( payload );
		string text = GetAsciiString(
			payload,
			"The Kitty OSC 99 support payload is not valid ASCII."
		);
		if ( 0 == text.Length ) {
			yield break;
		}
		foreach ( string item in text.Split( ':', StringSplitOptions.None ) ) {
			int separator = item.IndexOf( '=', StringComparison.Ordinal );
			if ( 1 != separator || separator + 1 >= item.Length ) {
				throw new FormatException(
					"The Kitty OSC 99 support payload contains a malformed key/value pair."
				);
			}
			yield return new KeyValuePair<string, string>(
				item[..separator],
				item[( separator + 1 )..]
			);
		}
	}

	private static IEnumerable<string> SplitCommaList(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		return value.Split(
			',',
			StringSplitOptions.RemoveEmptyEntries
		);
	}

	private static bool ParseSupportBoolean(
		string value,
		string key
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( key );
		return value switch {
			"0" => false,
			"1" => true,
			_ => throw new FormatException(
				$"The Kitty OSC 99 support key '{key}' must contain 0 or 1."
			)
		};
	}

	private static bool TryParseOccasion(
		string value,
		out KittyNotificationOccasion occasion
	) {
		ArgumentNullException.ThrowIfNull( value );
		switch ( value ) {
			case "always":
				occasion = KittyNotificationOccasion.Always;
				return true;
			case "unfocused":
				occasion = KittyNotificationOccasion.Unfocused;
				return true;
			case "invisible":
				occasion = KittyNotificationOccasion.Invisible;
				return true;
			default:
				occasion = default;
				return false;
		}
	}

	private static bool TryParseUrgency(
		string value,
		out KittyNotificationUrgency urgency
	) {
		ArgumentNullException.ThrowIfNull( value );
		switch ( value ) {
			case "0":
				urgency = KittyNotificationUrgency.Low;
				return true;
			case "1":
				urgency = KittyNotificationUrgency.Normal;
				return true;
			case "2":
				urgency = KittyNotificationUrgency.Critical;
				return true;
			default:
				urgency = default;
				return false;
		}
	}

	private static string GetAsciiString(
		byte[] bytes,
		string errorMessage
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ArgumentException.ThrowIfNullOrEmpty( errorMessage );
		foreach ( byte value in bytes ) {
			if ( 0x7f < value ) {
				throw new FormatException( errorMessage );
			}
		}
		return Encoding.ASCII.GetString( bytes );
	}

	private static bool IsValidReturnedIdentifier(
		string identifier
	) {
		if ( 0 == identifier.Length
			|| TerminalOsc99NotificationEncoder.MaximumIdentifierLength < identifier.Length ) {
			return false;
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
				return false;
			}
		}
		return true;
	}

	private sealed class TerminalOsc99ResponseMatcher : ITerminalResponseMatcher {
		private readonly string identifier;
		private readonly string payloadType;

		internal TerminalOsc99ResponseMatcher(
			string identifier,
			string payloadType
		) {
			ArgumentException.ThrowIfNullOrEmpty( identifier );
			ArgumentException.ThrowIfNullOrEmpty( payloadType );
			this.identifier = identifier;
			this.payloadType = payloadType;
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Osc;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
				return false;
			}
			try {
				Osc99Frame parsed = ParseFrame( frame );
				Dictionary<string, string> metadata = ParseMetadata( parsed.Metadata );
				return metadata.TryGetValue( "i", out string? receivedIdentifier )
					&& string.Equals(
						receivedIdentifier,
						this.identifier,
						StringComparison.Ordinal
					)
					&& metadata.TryGetValue( "p", out string? receivedPayloadType )
					&& string.Equals(
						receivedPayloadType,
						this.payloadType,
						StringComparison.Ordinal
					);
			} catch ( FormatException ) {
				return false;
			}
		}
	}

	private sealed record Osc99Frame(
		byte[] Metadata,
		byte[] Payload
	);
}
