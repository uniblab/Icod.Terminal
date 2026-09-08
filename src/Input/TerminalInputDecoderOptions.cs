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

/// <summary>
/// Configures bounded terminal-input decoding policy for a
/// <see cref="TerminalSession"/>.
/// </summary>
public sealed class TerminalInputDecoderOptions {
	private const int MinimumBufferCapacity = 4;
	private const int MinimumPasteChunkBytes = 1;
	private const int DefaultPasteChunkBytes = 4096;
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
	/// by one bracketed-paste data chunk.
	/// </summary>
	/// <remarks>
	/// A decoder may retain the small number of extra bytes required to finish a
	/// fragmented UTF-8 scalar or exact paste terminator without treating the
	/// complete paste as one buffer. The default remains 4,096 bytes independently
	/// of the larger undecoded-input ceiling required for bounded OSC 52 responses.
	/// </remarks>
	public int PasteChunkBytes {
		get;
		init;
	} = DefaultPasteChunkBytes;

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
