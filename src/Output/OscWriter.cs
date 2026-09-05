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
internal static partial class OscWriter {
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

		byte[] frame = new byte[ payloadByteCount + 6 ];
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
		return EncodeSingleSeparatorFrame(
			(byte)'7',
			payload
		);
	}

	/// <summary>
	/// Encodes one complete OSC 8 hyperlink begin frame.
	/// </summary>
	internal static byte[] EncodeHyperlinkBeginFrame(
		string uri,
		string? identifier = null
	) {
		ArgumentNullException.ThrowIfNull( uri );

		string encodedUri = TerminalHyperlinkEncoder.EncodeUri( uri );
		string encodedParameters = TerminalHyperlinkEncoder.EncodeParameters( identifier );
		byte[] parameters = Encoding.ASCII.GetBytes( encodedParameters );
		byte[] target = Encoding.ASCII.GetBytes( encodedUri );
		return EncodeDoubleSeparatorFrame(
			(byte)'8',
			parameters,
			target
		);
	}

	/// <summary>
	/// Encodes the canonical OSC 8 hyperlink close frame.
	/// </summary>
	internal static byte[] EncodeHyperlinkEndFrame() {
		return EncodeDoubleSeparatorFrame(
			(byte)'8',
			ReadOnlySpan<byte>.Empty,
			ReadOnlySpan<byte>.Empty
		);
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

	/// <summary>
	/// Validates and emits one complete OSC 8 hyperlink begin frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once the complete
	/// begin frame has been validated and transmission begins, the underlying write
	/// is intentionally not cancellation-driven. This operation does not flush.
	/// </remarks>
	internal static ValueTask WriteHyperlinkBeginAsync(
		ITerminalOutput output,
		string uri,
		string? identifier = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeHyperlinkBeginFrame(
			uri,
			identifier
		);
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	/// <summary>
	/// Emits the canonical OSC 8 hyperlink close frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once transmission
	/// begins, the complete close frame is written without caller-driven cancellation.
	/// This operation does not flush.
	/// </remarks>
	internal static ValueTask WriteHyperlinkEndAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeHyperlinkEndFrame();
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	private static byte[] EncodeSingleSeparatorFrame(
		byte selectorByte,
		ReadOnlySpan<byte> payload
	) {
		byte[] frame = new byte[ payload.Length + 6 ];
		frame[ 0 ] = Escape;
		frame[ 1 ] = OscFinal;
		frame[ 2 ] = selectorByte;
		frame[ 3 ] = Separator;
		payload.CopyTo( frame.AsSpan( 4 ) );
		frame[ ^2 ] = Escape;
		frame[ ^1 ] = StFinal;
		return frame;
	}

	private static byte[] EncodeDoubleSeparatorFrame(
		byte selectorByte,
		ReadOnlySpan<byte> firstPayload,
		ReadOnlySpan<byte> secondPayload
	) {
		byte[] frame = new byte[ firstPayload.Length + secondPayload.Length + 7 ];
		frame[ 0 ] = Escape;
		frame[ 1 ] = OscFinal;
		frame[ 2 ] = selectorByte;
		frame[ 3 ] = Separator;
		firstPayload.CopyTo( frame.AsSpan( 4 ) );
		int secondSeparatorIndex = 4 + firstPayload.Length;
		frame[ secondSeparatorIndex ] = Separator;
		secondPayload.CopyTo( frame.AsSpan( secondSeparatorIndex + 1 ) );
		frame[ ^2 ] = Escape;
		frame[ ^1 ] = StFinal;
		return frame;
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
