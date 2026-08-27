namespace Icod.Terminal;

/// <summary>
/// Configures bounded terminal-input decoding policy for a
/// <see cref="TerminalSession"/>.
/// </summary>
public sealed class TerminalInputDecoderOptions {
	private const int MinimumBufferCapacity = 4;
	private const int MinimumPasteChunkBytes = 1;
	private const int MaximumPasteChunkBytes = 1_048_576;

	/// <summary>
	/// Gets or initializes the bounded ambiguity interval used to distinguish an
	/// isolated Escape key from an incomplete escape-prefixed terminal sequence.
	/// </summary>
	public TimeSpan EscapeSequenceTimeout {
		get;
		init;
	} = TerminalSession.DefaultEscapeSequenceTimeout;

	/// <summary>
	/// Gets or initializes the maximum number of undecoded bytes retained by the
	/// incremental decoder.
	/// </summary>
	public int MaximumBufferedBytes {
		get;
		init;
	} = TerminalSession.MaximumBufferedInputBytes;

	/// <summary>
	/// Gets or initializes the target maximum number of raw paste bytes represented
	/// by one future bracketed-paste data chunk.
	/// </summary>
	/// <remarks>
	/// T14 reserves this policy for the T16 paste implementation. A decoder may retain
	/// the small number of extra bytes required to finish a fragmented UTF-8 scalar or
	/// exact paste terminator without treating the complete paste as one buffer.
	/// </remarks>
	public int PasteChunkBytes {
		get;
		init;
	} = TerminalSession.MaximumBufferedInputBytes;

	internal void Validate() {
		if ( TimeSpan.Zero > this.EscapeSequenceTimeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.EscapeSequenceTimeout ),
				this.EscapeSequenceTimeout,
				"The Escape-sequence timeout cannot be negative."
			);
		}
		if ( this.MaximumBufferedBytes < MinimumBufferCapacity
			|| this.MaximumBufferedBytes > TerminalSession.MaximumBufferedInputBytes ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.MaximumBufferedBytes ),
				this.MaximumBufferedBytes,
				$"The decoder buffer capacity must be between {MinimumBufferCapacity} and "
					+ $"{TerminalSession.MaximumBufferedInputBytes} bytes."
			);
		}
		if ( this.PasteChunkBytes < MinimumPasteChunkBytes
			|| this.PasteChunkBytes > MaximumPasteChunkBytes ) {
			throw new ArgumentOutOfRangeException(
				nameof( this.PasteChunkBytes ),
				this.PasteChunkBytes,
				$"The paste chunk size must be between {MinimumPasteChunkBytes} and "
					+ $"{MaximumPasteChunkBytes} bytes."
			);
		}
	}
}
