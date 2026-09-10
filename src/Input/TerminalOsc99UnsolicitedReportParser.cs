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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

/// <summary>
/// Recognizes the reviewed Kitty OSC 99 unsolicited notification-report subset.
/// </summary>
internal static class TerminalOsc99UnsolicitedReportParser {
	internal static bool TryParse(
		TerminalResponseFrame frame,
		out TerminalSemanticEvent? semanticEvent
	) {
		ArgumentNullException.ThrowIfNull( frame );
		semanticEvent = null;
		if ( TerminalResponseFrameKind.Osc != frame.Kind ) {
			return false;
		}
		if ( !TryParseFrame(
			frame,
			out Osc99Frame? parsed
		) ) {
			return false;
		}

		Dictionary<string, string> metadata = ParseMetadata( parsed.Metadata );
		if ( !metadata.TryGetValue(
			"i",
			out string? identifier
		) ) {
			return false;
		}

		metadata.TryGetValue(
			"p",
			out string? payloadType
		);
		if ( payloadType is not null
			&& !string.Equals(
				payloadType,
				"close",
				StringComparison.Ordinal
			) ) {
			return false;
		}
		if ( !IsValidReturnedIdentifier( identifier ) ) {
			throw new FormatException(
				"The Kitty OSC 99 unsolicited report contains an invalid notification identifier."
			);
		}

		TerminalNotificationEvent notification;
		if ( "close" == payloadType ) {
			if ( 0 == parsed.Payload.Length ) {
				notification = TerminalNotificationEvent.Closed( identifier );
			} else {
				string payload = GetAsciiString(
					parsed.Payload,
					"The Kitty OSC 99 close report payload is not valid ASCII."
				);
				if ( !string.Equals(
					payload,
					"untracked",
					StringComparison.Ordinal
				) ) {
					throw new FormatException(
						"The Kitty OSC 99 close report payload is not recognized."
					);
				}
				notification = TerminalNotificationEvent.CloseTrackingUnavailable(
					identifier
				);
			}
		} else if ( 0 == parsed.Payload.Length ) {
			notification = TerminalNotificationEvent.Activated( identifier );
		} else {
			string payload = GetAsciiString(
				parsed.Payload,
				"The Kitty OSC 99 button report payload is not valid ASCII."
			);
			if ( !int.TryParse(
				payload,
				NumberStyles.None,
				CultureInfo.InvariantCulture,
				out int buttonNumber
			) || 1 > buttonNumber ) {
				throw new FormatException(
					"The Kitty OSC 99 button report requires a positive one-based decimal button number."
				);
			}
			notification = TerminalNotificationEvent.ButtonActivated(
				identifier,
				buttonNumber
			);
		}

		semanticEvent = TerminalSemanticEvent.FromNotification( notification );
		return true;
	}

	private static bool TryParseFrame(
		TerminalResponseFrame frame,
		[NotNullWhen( true )] out Osc99Frame? parsed
	) {
		ArgumentNullException.ThrowIfNull( frame );
		parsed = null;
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
				return false;
			}
		} else if ( 6 <= bytes.Length
			&& 0x9d == bytes[ 0 ]
			&& 0x9c == bytes[ ^1 ] ) {
			start = 1;
			end = bytes.Length - 1;
		} else {
			return false;
		}

		ReadOnlySpan<byte> content = bytes.Slice(
			start,
			end - start
		);
		if ( 3 > content.Length
			|| (byte)'9' != content[ 0 ]
			|| (byte)'9' != content[ 1 ]
			|| (byte)';' != content[ 2 ] ) {
			return false;
		}

		ReadOnlySpan<byte> remainder = content[ 3.. ];
		int separator = remainder.IndexOf( (byte)';' );
		if ( 0 > separator ) {
			throw new FormatException(
				"The Kitty OSC 99 unsolicited report is missing its metadata/payload separator."
			);
		}
		parsed = new Osc99Frame(
			remainder[..separator].ToArray(),
			remainder[( separator + 1 )..].ToArray()
		);
		return true;
	}

	private static Dictionary<string, string> ParseMetadata(
		byte[] metadataBytes
	) {
		ArgumentNullException.ThrowIfNull( metadataBytes );
		string metadataText = GetAsciiString(
			metadataBytes,
			"The Kitty OSC 99 unsolicited-report metadata is not valid ASCII."
		);
		Dictionary<string, string> metadata = new( StringComparer.Ordinal );
		if ( 0 == metadataText.Length ) {
			return metadata;
		}

		foreach ( string item in metadataText.Split( ':', StringSplitOptions.None ) ) {
			int separator = item.IndexOf( '=', StringComparison.Ordinal );
			if ( 1 != separator || separator + 1 >= item.Length ) {
				throw new FormatException(
					"The Kitty OSC 99 unsolicited-report metadata contains a malformed key/value pair."
				);
			}
			string key = item[..separator];
			string value = item[( separator + 1 )..];
			if ( metadata.ContainsKey( key ) ) {
				throw new FormatException(
					"The Kitty OSC 99 unsolicited-report metadata contains a duplicate key."
				);
			}
			metadata.Add(
				key,
				value
			);
		}
		return metadata;
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
		ArgumentNullException.ThrowIfNull( identifier );
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

	private sealed record Osc99Frame(
		byte[] Metadata,
		byte[] Payload
	);
}
