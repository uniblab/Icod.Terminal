namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Identifies the three OSC title selectors supported by the 0.4.0 contract.
/// </summary>
internal enum OscTitleSelector {
	IconAndWindowTitle = 0,
	IconName = 1,
	WindowTitle = 2
}

/// <summary>
/// Builds and emits bounded, injection-safe OSC frames for supported semantic operations.
/// </summary>
internal static class OscWriter {
	internal const int MaximumTitlePayloadByteCount = 4096;

	private const byte Escape = 0x1b;
	private const byte OscFinal = 0x5d;
	private const byte Separator = 0x3b;
	private const byte StFinal = 0x5c;
	private static readonly UTF8Encoding StrictUtf8 = new(
		false,
		true
	);

	/// <summary>
	/// Encodes one complete OSC 0/1/2 frame after validating the complete payload.
	/// </summary>
	internal static byte[] EncodeTitleFrame(
		OscTitleSelector selector,
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		byte selectorByte = GetSelectorByte( selector );
		ValidatePayloadCharacters( value );

		int payloadByteCount;
		try {
			payloadByteCount = StrictUtf8.GetByteCount( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"OSC title payloads must contain well-formed Unicode text.",
				nameof( value ),
				exception
			);
		}

		if ( MaximumTitlePayloadByteCount < payloadByteCount ) {
			throw new ArgumentException(
				string.Concat(
					"OSC title payloads may not exceed ",
					MaximumTitlePayloadByteCount,
					" encoded UTF-8 bytes."
				),
				nameof( value )
			);
		}

		byte[] frame = new byte[ 6 + payloadByteCount ];
		frame[ 0 ] = Escape;
		frame[ 1 ] = OscFinal;
		frame[ 2 ] = selectorByte;
		frame[ 3 ] = Separator;

		if ( 0 < payloadByteCount ) {
			StrictUtf8.GetBytes(
				value.AsSpan(),
				frame.AsSpan( 4, payloadByteCount )
			);
		}

		frame[ ^2 ] = Escape;
		frame[ ^1 ] = StFinal;
		return frame;
	}

	/// <summary>
	/// Encodes one complete OSC 7 current-location frame from structured native-path input.
	/// </summary>
	internal static byte[] EncodeLocationFrame(
		string path,
		TerminalLocationPathKind pathKind,
		string? authority = null
	) {
		ArgumentNullException.ThrowIfNull( path );

		string fileUri = TerminalLocationUriEncoder.EncodeFileUri(
			path,
			pathKind,
			authority
		);
		byte[] payload = Encoding.ASCII.GetBytes( fileUri );
		byte[] frame = new byte[ payload.Length + 6 ];
		frame[ 0 ] = Escape;
		frame[ 1 ] = OscFinal;
		frame[ 2 ] = (byte)'7';
		frame[ 3 ] = Separator;
		payload.CopyTo(
			frame,
			4
		);
		frame[ ^2 ] = Escape;
		frame[ ^1 ] = StFinal;
		return frame;
	}

	/// <summary>
	/// Validates and emits one complete title frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once the full
	/// frame has been validated and transmission begins, the underlying write is
	/// intentionally not cancellation-driven so ordinary cancellation cannot
	/// deliberately abandon the frame halfway through.
	/// </remarks>
	internal static ValueTask WriteTitleAsync(
		ITerminalOutput output,
		OscTitleSelector selector,
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( value );
		_ = GetSelectorByte( selector );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeTitleFrame(
			selector,
			value
		);
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Validates and emits one complete OSC 7 current-location frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once the full
	/// URI and frame are validated and transmission begins, the underlying write
	/// is intentionally not cancellation-driven so ordinary cancellation cannot
	/// deliberately abandon the frame halfway through. This operation does not
	/// flush the output service.
	/// </remarks>
	internal static ValueTask WriteLocationAsync(
		ITerminalOutput output,
		string path,
		TerminalLocationPathKind pathKind,
		string? authority = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( path );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeLocationFrame(
			path,
			pathKind,
			authority
		);
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	private static byte GetSelectorByte(
		OscTitleSelector selector
	) {
		return selector switch {
			OscTitleSelector.IconAndWindowTitle => (byte)'0',
			OscTitleSelector.IconName => (byte)'1',
			OscTitleSelector.WindowTitle => (byte)'2',
			_ => throw new ArgumentOutOfRangeException(
				nameof( selector ),
				selector,
				"Only OSC title selectors 0, 1, and 2 are supported by the 0.4.0 writer."
			)
		};
	}

	private static void ValidatePayloadCharacters(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );

		foreach ( char character in value ) {
			if ( '\u001f' >= character
				|| '\u007f' == character
				|| ( '\u0080' <= character && '\u009f' >= character ) ) {
				throw new ArgumentException(
					"OSC title payloads may not contain C0, DEL, or C1 control characters.",
					nameof( value )
				);
			}
		}
	}
}
