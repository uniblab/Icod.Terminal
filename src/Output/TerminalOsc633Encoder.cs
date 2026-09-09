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
/// Encodes the bounded, documented VS Code OSC 633 shell-integration surface.
/// </summary>
internal static class TerminalOsc633Encoder {
	internal const int MaximumPayloadLength = 65_536;
	internal const int MaximumNonceLength = 512;

	private static readonly UTF8Encoding StrictUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[] EncodeMarker(
		char marker
	) {
		if ( 'A' != marker && 'B' != marker && 'C' != marker && 'D' != marker ) {
			throw new ArgumentOutOfRangeException(
				nameof( marker ),
				marker,
				"OSC 633 marker must be A, B, C, or D."
			);
		}

		return EncodePayload(
			string.Concat(
				"633;",
				marker
			)
		);
	}

	internal static byte[] EncodeCommandFinished(
		int exitCode
	) {
		return EncodePayload(
			string.Concat(
				"633;D;",
				exitCode.ToString( CultureInfo.InvariantCulture )
			)
		);
	}

	internal static byte[] EncodeCommandLine(
		string commandLine,
		string? nonce
	) {
		ArgumentNullException.ThrowIfNull( commandLine );
		ValidateNonce( nonce );

		string serializedCommandLine = SerializeMessage( commandLine );
		string payload = ( nonce is null )
			? string.Concat(
				"633;E;",
				serializedCommandLine
			)
			: string.Concat(
				"633;E;",
				serializedCommandLine,
				";",
				nonce
			)
		;
		return EncodePayload( payload );
	}

	internal static byte[] EncodeCurrentDirectory(
		string currentDirectory,
		string? nonce
	) {
		ArgumentNullException.ThrowIfNull( currentDirectory );
		if ( 0 == currentDirectory.Length ) {
			throw new ArgumentException(
				"OSC 633 current directory cannot be empty.",
				nameof( currentDirectory )
			);
		}
		ValidateNonce( nonce );

		string serializedCurrentDirectory = SerializeMessage( currentDirectory );
		string payload = ( nonce is null )
			? string.Concat(
				"633;P;Cwd=",
				serializedCurrentDirectory
			)
			: string.Concat(
				"633;P;Cwd=",
				serializedCurrentDirectory,
				";",
				nonce
			)
		;
		return EncodePayload( payload );
	}

	internal static byte[] EncodeIsWindows(
		bool isWindows
	) {
		return EncodePayload(
			( isWindows )
				? "633;P;IsWindows=True"
				: "633;P;IsWindows=False"
		);
	}

	internal static byte[] EncodeContinuationPrompt(
		string continuationPrompt
	) {
		ArgumentNullException.ThrowIfNull( continuationPrompt );
		return EncodePayload(
			string.Concat(
				"633;P;ContinuationPrompt=",
				SerializeMessage( continuationPrompt )
			)
		);
	}

	internal static byte[] EncodeRichCommandDetection(
		bool hasRichCommandDetection
	) {
		return EncodePayload(
			( hasRichCommandDetection )
				? "633;P;HasRichCommandDetection=True"
				: "633;P;HasRichCommandDetection=False"
		);
	}

	internal static string SerializeMessage(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		ValidateUnicode( value );

		StringBuilder builder = new( value.Length );
		foreach ( char character in value ) {
			if ( '\\' == character ) {
				builder.Append( "\\\\" );
				continue;
			}
			if ( ';' == character || character <= ' ' ) {
				builder.Append( "\\x" );
				builder.Append(
					( (int)character ).ToString(
						"x2",
						CultureInfo.InvariantCulture
					)
				);
				continue;
			}

			builder.Append( character );
		}

		return builder.ToString();
	}

	private static byte[] EncodePayload(
		string payload
	) {
		ArgumentNullException.ThrowIfNull( payload );
		ValidateUnicode( payload );

		byte[] payloadBytes = StrictUtf8.GetBytes( payload );
		if ( MaximumPayloadLength < payloadBytes.Length ) {
			throw new ArgumentException(
				$"OSC 633 payload exceeds the {MaximumPayloadLength}-byte safety bound.",
				nameof( payload )
			);
		}

		byte[] frame = new byte[ payloadBytes.Length + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		payloadBytes.CopyTo( frame, 2 );
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	private static void ValidateNonce(
		string? nonce
	) {
		if ( nonce is null ) {
			return;
		}
		if ( 0 == nonce.Length || MaximumNonceLength < nonce.Length ) {
			throw new ArgumentException(
				$"OSC 633 nonce must contain 1 through {MaximumNonceLength} ASCII characters.",
				nameof( nonce )
			);
		}

		foreach ( char character in nonce ) {
			if ( character < '!' || '~' < character || ';' == character ) {
				throw new ArgumentException(
					"OSC 633 nonce must contain printable ASCII characters other than semicolon.",
					nameof( nonce )
				);
			}
		}
	}

	private static void ValidateUnicode(
		string value
	) {
		try {
			_ = StrictUtf8.GetByteCount( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 633 text must contain well-formed Unicode.",
				nameof( value ),
				exception
			);
		}
	}
}
