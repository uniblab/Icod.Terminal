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
/// Encodes the bounded iTerm2 OSC 1337 shell-integration metadata core.
/// </summary>
internal static class TerminalOsc1337ShellIntegrationEncoder {
	internal const int MaximumPayloadLength = 65_536;
	internal const int MaximumUserVariableNameLength = 256;
	internal const int MaximumShellNameLength = 64;
	internal const int MaximumRemoteHostComponentLength = 1_024;

	private static readonly UTF8Encoding StrictUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[] EncodeSetMarkFrame() => EncodeBody(
		"1337;SetMark",
		"OSC 1337 SetMark"
	);

	internal static byte[] EncodeCurrentDirectoryFrame(
		string currentDirectory
	) {
		ArgumentException.ThrowIfNullOrEmpty( currentDirectory );
		ValidateTextControls(
			currentDirectory,
			nameof( currentDirectory )
		);
		return EncodeBody(
			"1337;CurrentDir=" + currentDirectory,
			nameof( currentDirectory )
		);
	}

	internal static byte[] EncodeRemoteHostFrame(
		string userName,
		string hostName
	) {
		ArgumentException.ThrowIfNullOrEmpty( userName );
		ArgumentException.ThrowIfNullOrEmpty( hostName );
		ValidateDelimitedText(
			userName,
			nameof( userName ),
			MaximumRemoteHostComponentLength,
			'@',
			';',
			'='
		);
		ValidateDelimitedText(
			hostName,
			nameof( hostName ),
			MaximumRemoteHostComponentLength,
			'@',
			';',
			'='
		);
		return EncodeBody(
			"1337;RemoteHost=" + userName + "@" + hostName,
			nameof( hostName )
		);
	}

	internal static byte[] EncodeSetUserVariableFrame(
		string name,
		string value
	) {
		ArgumentException.ThrowIfNullOrEmpty( name );
		ArgumentNullException.ThrowIfNull( value );
		ValidateDelimitedText(
			name,
			nameof( name ),
			MaximumUserVariableNameLength,
			';',
			'='
		);
		byte[] valueBytes = EncodeStrictUtf8(
			value,
			nameof( value )
		);
		string encodedValue = Convert.ToBase64String( valueBytes );
		return EncodeBody(
			"1337;SetUserVar=" + name + "=" + encodedValue,
			nameof( value )
		);
	}

	internal static byte[] EncodeShellIntegrationVersionFrame(
		int version,
		string shellName
	) {
		if ( 0 > version ) {
			throw new ArgumentOutOfRangeException(
				nameof( version ),
				version,
				"The iTerm2 shell-integration version cannot be negative."
			);
		}
		ArgumentException.ThrowIfNullOrEmpty( shellName );
		ValidateShellName( shellName );
		return EncodeBody(
			"1337;ShellIntegrationVersion="
				+ version.ToString( CultureInfo.InvariantCulture )
				+ ";shell="
				+ shellName,
			nameof( shellName )
		);
	}

	internal static byte[] EncodeClearCapturedOutputFrame() => EncodeBody(
		"1337;ClearCapturedOutput",
		"OSC 1337 ClearCapturedOutput"
	);

	private static byte[] EncodeBody(
		string body,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( body );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		byte[] payload = EncodeStrictUtf8(
			body,
			parameterName
		);
		if ( MaximumPayloadLength < payload.Length ) {
			throw new ArgumentException(
				$"OSC 1337 payload cannot exceed {MaximumPayloadLength} encoded bytes.",
				parameterName
			);
		}

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

	private static byte[] EncodeStrictUtf8(
		string value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		try {
			return StrictUtf8.GetBytes( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 1337 text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}
	}

	private static void ValidateDelimitedText(
		string value,
		string parameterName,
		int maximumEncodedLength,
		params char[] forbiddenCharacters
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		ArgumentNullException.ThrowIfNull( forbiddenCharacters );
		ValidateTextControls(
			value,
			parameterName
		);
		foreach ( char character in forbiddenCharacters ) {
			if ( value.Contains( character, StringComparison.Ordinal ) ) {
				throw new ArgumentException(
					$"OSC 1337 field '{parameterName}' cannot contain '{character}'.",
					parameterName
				);
			}
		}
		byte[] encoded = EncodeStrictUtf8(
			value,
			parameterName
		);
		if ( maximumEncodedLength < encoded.Length ) {
			throw new ArgumentException(
				$"OSC 1337 field '{parameterName}' cannot exceed {maximumEncodedLength} encoded bytes.",
				parameterName
			);
		}
	}

	private static void ValidateShellName(
		string shellName
	) {
		ArgumentException.ThrowIfNullOrEmpty( shellName );
		if ( MaximumShellNameLength < shellName.Length ) {
			throw new ArgumentException(
				$"The iTerm2 shell name cannot exceed {MaximumShellNameLength} ASCII characters.",
				nameof( shellName )
			);
		}
		foreach ( char character in shellName ) {
			bool isAllowed = ( 'a' <= character && 'z' >= character )
				|| ( 'A' <= character && 'Z' >= character )
				|| ( '0' <= character && '9' >= character )
				|| '_' == character
				|| '-' == character
				|| '.' == character
				|| '+' == character;
			if ( !isAllowed ) {
				throw new ArgumentException(
					"The iTerm2 shell name must contain only ASCII letters, digits, '.', '_', '-', or '+'.",
					nameof( shellName )
				);
			}
		}
	}

	private static void ValidateTextControls(
		string value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		foreach ( char character in value ) {
			if ( 0x001f >= character
				|| 0x007f == character
				|| ( 0x0080 <= character && 0x009f >= character ) ) {
				throw new ArgumentException(
					"OSC 1337 text must not contain C0, DEL, or C1 control characters.",
					parameterName
				);
			}
		}
	}
}
