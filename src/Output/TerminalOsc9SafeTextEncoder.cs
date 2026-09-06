namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Encodes the bounded text-bearing OSC 9 forms approved by T160.
/// </summary>
internal static class TerminalOsc9SafeTextEncoder {
	internal const int MaximumNotificationPayloadLength = 4_096;
	internal const int MaximumWindowsCurrentDirectoryPayloadLength = 32_768;

	private static readonly byte[] NotificationPrefix = [
		(byte)'9',
		(byte)';'
	];
	private static readonly byte[] WindowsCurrentDirectoryPrefix = [
		(byte)'9',
		(byte)';',
		(byte)'9',
		(byte)';'
	];
	private static readonly UTF8Encoding StrictUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	internal static byte[] EncodeNotificationFrame(
		string message
	) {
		ArgumentNullException.ThrowIfNull( message );
		return EncodeFrame(
			message,
			NotificationPrefix,
			MaximumNotificationPayloadLength,
			nameof( message )
		);
	}

	internal static byte[] EncodeWindowsCurrentDirectoryFrame(
		string windowsPath
	) {
		ArgumentNullException.ThrowIfNull( windowsPath );
		if ( 0 == windowsPath.Length ) {
			throw new ArgumentException(
				"OSC 9;9 current-directory compatibility requires a non-empty Windows path.",
				nameof( windowsPath )
			);
		}

		return EncodeFrame(
			windowsPath,
			WindowsCurrentDirectoryPrefix,
			MaximumWindowsCurrentDirectoryPayloadLength,
			nameof( windowsPath )
		);
	}

	private static byte[] EncodeFrame(
		string value,
		byte[] prefix,
		int maximumPayloadLength,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( prefix );
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		ValidateTextControls(
			value,
			parameterName
		);

		byte[] encodedValue;
		try {
			encodedValue = StrictUtf8.GetBytes( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC 9 text must contain well-formed Unicode.",
				parameterName,
				exception
			);
		}

		int payloadLength = checked( prefix.Length + encodedValue.Length );
		if ( maximumPayloadLength < payloadLength ) {
			throw new ArgumentException(
				$"OSC 9 payload cannot exceed {maximumPayloadLength} encoded bytes.",
				parameterName
			);
		}

		byte[] frame = new byte[ payloadLength + 4 ];
		frame[ 0 ] = 0x1b;
		frame[ 1 ] = (byte)']';
		prefix.CopyTo(
			frame,
			2
		);
		encodedValue.CopyTo(
			frame,
			2 + prefix.Length
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
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
					"OSC 9 text must not contain C0, DEL, or C1 control characters.",
					parameterName
				);
			}
		}
	}
}
