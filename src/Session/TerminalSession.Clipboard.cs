namespace Icod.Terminal;

using System.Text;

/// <summary>
/// Provides semantic OSC 52 clipboard and selection writes for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	private static readonly UTF8Encoding StrictClipboardUtf8 = new(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	);

	/// <summary>
	/// Writes bounded binary content to one terminal-managed clipboard or selection target.
	/// </summary>
	/// <param name="selection">The terminal-managed selection to replace.</param>
	/// <param name="payload">The exact bytes to encode and emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the OSC 52 emission.</returns>
	/// <remarks>
	/// <para>
	/// Successful completion means the complete OSC 52 frame was emitted to the
	/// interactive terminal output endpoint. It does not prove that the terminal
	/// accepted, retained, or exposed the selection content.
	/// </para>
	/// <para>
	/// The payload is limited to 65,536 decoded bytes. An empty payload explicitly
	/// replaces the selected terminal selection with empty content.
	/// </para>
	/// <para>
	/// This operation never reads a clipboard, never accesses an operating-system
	/// clipboard API, and never mutates terminal selection state automatically during
	/// session open, lifecycle handling, or disposal.
	/// </para>
	/// </remarks>
	public async ValueTask WriteClipboardAsync(
		TerminalClipboardSelection selection,
		ReadOnlyMemory<byte> payload,
		CancellationToken cancellationToken = default
	) {
		TerminalOsc52Selection protocolSelection = ToOsc52Selection( selection );
		if ( TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes < payload.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( payload ),
				payload.Length,
				$"A terminal clipboard payload cannot exceed {TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes} bytes."
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Terminal clipboard writes require an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		await OscWriter.WriteOsc52Async(
			this.Output,
			protocolSelection,
			payload,
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Writes strict UTF-8 text to one terminal-managed clipboard or selection target.
	/// </summary>
	/// <param name="selection">The terminal-managed selection to replace.</param>
	/// <param name="value">The Unicode text to encode as strict UTF-8.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the OSC 52 emission.</returns>
	/// <remarks>
	/// This overload always uses strict UTF-8 without a byte-order mark. It does not
	/// use <see cref="ApplicationEncoding"/> because terminal clipboard payloads have
	/// their own deterministic text representation. An empty string clears the target.
	/// </remarks>
	public ValueTask WriteClipboardAsync(
		TerminalClipboardSelection selection,
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		_ = ToOsc52Selection( selection );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] payload;
		try {
			payload = StrictClipboardUtf8.GetBytes( value );
		} catch ( EncoderFallbackException exception ) {
			throw new ArgumentException(
				"Terminal clipboard text must contain well-formed Unicode text.",
				nameof( value ),
				exception
			);
		}

		if ( TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes < payload.Length ) {
			throw new ArgumentException(
				$"Terminal clipboard text may not exceed {TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes} encoded UTF-8 bytes.",
				nameof( value )
			);
		}

		return this.WriteClipboardAsync(
			selection,
			payload,
			cancellationToken
		);
	}

	private static TerminalOsc52Selection ToOsc52Selection(
		TerminalClipboardSelection selection
	) {
		return selection switch {
			TerminalClipboardSelection.Clipboard => TerminalOsc52Selection.Clipboard,
			TerminalClipboardSelection.Primary => TerminalOsc52Selection.Primary,
			TerminalClipboardSelection.Secondary => TerminalOsc52Selection.Secondary,
			TerminalClipboardSelection.Select => TerminalOsc52Selection.Select,
			_ => throw new ArgumentOutOfRangeException(
				nameof( selection ),
				selection,
				"The terminal clipboard selection is not recognized."
			)
		};
	}
}
