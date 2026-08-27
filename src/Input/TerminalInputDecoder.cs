namespace Icod.Terminal;

using System.Buffers;
using System.Text;
using Icod.TermInfo;
using Icod.Timing;

/// <summary>
/// Incrementally decodes one terminal byte stream into terminal-independent input events.
/// </summary>
internal sealed class TerminalInputDecoder {
	private const byte EscapeByte = 0x1B;
	private const int ReadBufferSize = 256;
	private const int MinimumBufferCapacity = 4;
	private const int MaximumBufferCapacity = 1_048_576;
	private const int MinimumPasteChunkBytes = 1;
	private const int MaximumPasteChunkBytes = 1_048_576;

	private readonly ITerminalInput input;
	private readonly IMonotonicClock monotonicClock;
	private readonly TimeSpan escapeSequenceTimeout;
	private readonly int maximumBufferedBytes;
	private readonly int pasteChunkBytes;
	private readonly List<byte> bufferedBytes = [];
	private readonly byte[] readBuffer = new byte[ ReadBufferSize ];
	private readonly List<KeySequence> keySequences = [];
	private readonly byte[]? pasteEndSequence;

	private Task<int>? pendingRead;
	private int pendingReadCapacity;
	private bool endOfInput;
	private bool pasteActive;

	internal TerminalInputDecoder(
		ITerminalInput input,
		TerminalDescription terminal,
		IMonotonicClock monotonicClock,
		TimeSpan escapeSequenceTimeout,
		int maximumBufferedBytes
	) : this(
		input,
		terminal,
		monotonicClock,
		escapeSequenceTimeout,
		maximumBufferedBytes,
		maximumBufferedBytes
	) {
	}

	internal TerminalInputDecoder(
		ITerminalInput input,
		TerminalDescription terminal,
		IMonotonicClock monotonicClock,
		TimeSpan escapeSequenceTimeout,
		int maximumBufferedBytes,
		int pasteChunkBytes
	) {
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( monotonicClock );
		if ( TimeSpan.Zero > escapeSequenceTimeout ) {
			throw new ArgumentOutOfRangeException( nameof( escapeSequenceTimeout ) );
		}
		if ( maximumBufferedBytes < MinimumBufferCapacity
			|| maximumBufferedBytes > MaximumBufferCapacity ) {
			throw new ArgumentOutOfRangeException(
				nameof( maximumBufferedBytes ),
				maximumBufferedBytes,
				$"The decoder buffer capacity must be between {MinimumBufferCapacity} and "
					+ $"{MaximumBufferCapacity} bytes."
			);
		}
		if ( pasteChunkBytes < MinimumPasteChunkBytes
			|| pasteChunkBytes > MaximumPasteChunkBytes ) {
			throw new ArgumentOutOfRangeException(
				nameof( pasteChunkBytes ),
				pasteChunkBytes,
				$"The paste chunk size must be between {MinimumPasteChunkBytes} and "
					+ $"{MaximumPasteChunkBytes} bytes."
			);
		}

		this.input = input;
		this.monotonicClock = monotonicClock;
		this.escapeSequenceTimeout = escapeSequenceTimeout;
		this.maximumBufferedBytes = maximumBufferedBytes;
		this.pasteChunkBytes = pasteChunkBytes;

		this.AddCapability(
			terminal,
			StringCapability.KeyBackspace,
			TerminalInputEvent.FromKey( TerminalKey.Backspace )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyBackTab,
			TerminalInputEvent.FromKey(
				TerminalKey.Tab,
				TerminalKeyModifiers.Shift
			)
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyCursorUp,
			TerminalInputEvent.FromKey( TerminalKey.Up )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyCursorDown,
			TerminalInputEvent.FromKey( TerminalKey.Down )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyCursorLeft,
			TerminalInputEvent.FromKey( TerminalKey.Left )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyCursorRight,
			TerminalInputEvent.FromKey( TerminalKey.Right )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyHome,
			TerminalInputEvent.FromKey( TerminalKey.Home )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyEnd,
			TerminalInputEvent.FromKey( TerminalKey.End )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyEnter,
			TerminalInputEvent.FromKey( TerminalKey.Enter )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyPreviousPage,
			TerminalInputEvent.FromKey( TerminalKey.PageUp )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyNextPage,
			TerminalInputEvent.FromKey( TerminalKey.PageDown )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyInsertCharacter,
			TerminalInputEvent.FromKey( TerminalKey.Insert )
		);
		this.AddCapability(
			terminal,
			StringCapability.KeyDeleteCharacter,
			TerminalInputEvent.FromKey( TerminalKey.Delete )
		);

		for ( int number = 0; number <= 63; number++ ) {
			if ( !Enum.TryParse(
				$"KeyF{number}",
				out StringCapability capability
			) ) {
				continue;
			}

			this.AddCapability(
				terminal,
				capability,
				TerminalInputEvent.FromKey(
					TerminalKey.Function,
					functionKeyNumber: number
				)
			);
		}

		this.AddFocusCapabilities( terminal );
		this.pasteEndSequence = this.AddBracketedPasteCapabilities( terminal );

		this.keySequences.Sort(
			static ( left, right ) =>
				right.Bytes.Length.CompareTo( left.Bytes.Length )
		);
	}

	internal async ValueTask<TerminalInputEvent> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		while ( true ) {
			cancellationToken.ThrowIfCancellationRequested();

			if ( this.pasteActive ) {
				return await this.ReadPasteEventAsync(
					cancellationToken
				).ConfigureAwait( false );
			}

			if ( 0 == this.bufferedBytes.Count ) {
				if ( !await this.ReadMoreAsync( cancellationToken ).ConfigureAwait( false ) ) {
					return TerminalInputEvent.EndOfInput();
				}
			}

			this.FindKeySequenceMatch(
				out KeySequence? exact,
				out bool needsMore
			);

			if ( exact is not null && !needsMore ) {
				return this.ConsumeKeySequence( exact );
			}

			if ( needsMore ) {
				bool appended = EscapeByte == this.bufferedBytes[ 0 ]
					? await this.ReadMoreWithinEscapeWindowAsync(
						cancellationToken
					).ConfigureAwait( false )
					: await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false )
				;

				if ( appended ) {
					continue;
				}

				if ( exact is not null ) {
					return this.ConsumeKeySequence( exact );
				}

				if ( EscapeByte == this.bufferedBytes[ 0 ] ) {
					this.Consume( 1 );
					return TerminalInputEvent.FromKey( TerminalKey.Escape );
				}
			}

			return await this.DecodeFallbackAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private async ValueTask<TerminalInputEvent> DecodeFallbackAsync(
		CancellationToken cancellationToken
	) {
		byte first = this.bufferedBytes[ 0 ];

		switch ( first ) {
			case EscapeByte:
				this.Consume( 1 );
				return TerminalInputEvent.FromKey( TerminalKey.Escape );

			case 0x08:
			case 0x7F:
				this.Consume( 1 );
				return TerminalInputEvent.FromKey( TerminalKey.Backspace );

			case 0x09:
				this.Consume( 1 );
				return TerminalInputEvent.FromKey( TerminalKey.Tab );

			case 0x0A:
			case 0x0D:
				this.Consume( 1 );
				return TerminalInputEvent.FromKey( TerminalKey.Enter );

			case 0x20:
				this.Consume( 1 );
				return TerminalInputEvent.FromKey( TerminalKey.Space );
		}

		if ( 0x20 > first ) {
			this.Consume( 1 );
			return CreateControlKey( first );
		}

		while ( true ) {
			byte[] source = this.bufferedBytes.ToArray();
			OperationStatus status = Rune.DecodeFromUtf8(
				source,
				out Rune rune,
				out int bytesConsumed
			);

			if ( OperationStatus.Done == status ) {
				this.Consume( bytesConsumed );
				return TerminalInputEvent.FromText( rune );
			}

			if ( OperationStatus.NeedMoreData == status && !this.endOfInput ) {
				if ( await this.ReadMoreAsync( cancellationToken ).ConfigureAwait( false ) ) {
					continue;
				}
			}

			this.Consume( 1 );
			return TerminalInputEvent.FromText( new Rune( '\uFFFD' ) );
		}
	}

	private async ValueTask<TerminalInputEvent> ReadPasteEventAsync(
		CancellationToken cancellationToken
	) {
		byte[] terminator = this.pasteEndSequence
			?? throw new InvalidOperationException(
				"The bracketed-paste decoder does not have an end marker."
			);

		while ( true ) {
			cancellationToken.ThrowIfCancellationRequested();

			if ( 0 == this.bufferedBytes.Count ) {
				if ( !await this.ReadMoreAsync( cancellationToken ).ConfigureAwait( false ) ) {
					this.pasteActive = false;
					return TerminalInputEvent.EndOfInput();
				}
			}

			int terminatorIndex = this.FindSequenceIndex( terminator );
			if ( 0 == terminatorIndex ) {
				this.Consume( terminator.Length );
				this.pasteActive = false;
				return TerminalInputEvent.FromPaste(
					new TerminalPasteEvent( TerminalPastePhase.End )
				);
			}

			int safeCount;
			if ( 0 < terminatorIndex ) {
				safeCount = terminatorIndex;
			} else if ( this.endOfInput ) {
				safeCount = this.bufferedBytes.Count;
			} else {
				safeCount = this.bufferedBytes.Count
					- this.GetTrailingSequencePrefixLength( terminator );
			}

			if ( 0 < safeCount ) {
				TerminalInputEvent? dataEvent = this.TryConsumePasteData( safeCount );
				if ( dataEvent is not null ) {
					return dataEvent;
				}
			}

			if ( this.endOfInput ) {
				this.pasteActive = false;
				return TerminalInputEvent.EndOfInput();
			}

			await this.ReadMoreAsync( cancellationToken ).ConfigureAwait( false );
		}
	}

	private TerminalInputEvent? TryConsumePasteData(
		int safeCount
	) {
		if ( 0 >= safeCount || safeCount > this.bufferedBytes.Count ) {
			throw new ArgumentOutOfRangeException( nameof( safeCount ) );
		}

		byte[] source = this.bufferedBytes.GetRange(
			0,
			safeCount
		).ToArray();
		StringBuilder text = new();
		int consumed = 0;

		while ( consumed < source.Length ) {
			OperationStatus status = Rune.DecodeFromUtf8(
				source.AsSpan( consumed ),
				out Rune rune,
				out int bytesConsumed
			);

			if ( OperationStatus.Done == status ) {
				text.Append( rune.ToString() );
				consumed += bytesConsumed;
			} else if ( OperationStatus.NeedMoreData == status
				&& !this.endOfInput
				&& safeCount == this.bufferedBytes.Count ) {
				break;
			} else if ( OperationStatus.InvalidData == status
				|| OperationStatus.NeedMoreData == status ) {
				text.Append( '\uFFFD' );
				++consumed;
			} else {
				throw new InvalidOperationException(
					$"Unexpected UTF-8 decode status '{status}' while reading bracketed paste."
				);
			}

			if ( consumed >= this.pasteChunkBytes ) {
				break;
			}
		}

		if ( 0 == consumed ) {
			return null;
		}

		this.Consume( consumed );
		return TerminalInputEvent.FromPaste(
			new TerminalPasteEvent(
				TerminalPastePhase.Data,
				text.ToString()
			)
		);
	}

	private int FindSequenceIndex(
		IReadOnlyList<byte> sequence
	) {
		ArgumentNullException.ThrowIfNull( sequence );
		if ( 0 == sequence.Count ) {
			throw new ArgumentException(
				"A terminal input sequence cannot be empty.",
				nameof( sequence )
			);
		}
		if ( this.bufferedBytes.Count < sequence.Count ) {
			return -1;
		}

		int lastStart = this.bufferedBytes.Count - sequence.Count;
		for ( int start = 0; start <= lastStart; start++ ) {
			bool match = true;
			for ( int index = 0; index < sequence.Count; index++ ) {
				if ( this.bufferedBytes[ start + index ] != sequence[ index ] ) {
					match = false;
					break;
				}
			}

			if ( match ) {
				return start;
			}
		}

		return -1;
	}

	private int GetTrailingSequencePrefixLength(
		IReadOnlyList<byte> sequence
	) {
		ArgumentNullException.ThrowIfNull( sequence );
		if ( 0 == sequence.Count ) {
			throw new ArgumentException(
				"A terminal input sequence cannot be empty.",
				nameof( sequence )
			);
		}

		int maximumLength = Math.Min(
			this.bufferedBytes.Count,
			sequence.Count - 1
		);
		for ( int length = maximumLength; 0 < length; length-- ) {
			int bufferStart = this.bufferedBytes.Count - length;
			bool match = true;
			for ( int index = 0; index < length; index++ ) {
				if ( this.bufferedBytes[ bufferStart + index ] != sequence[ index ] ) {
					match = false;
					break;
				}
			}

			if ( match ) {
				return length;
			}
		}

		return 0;
	}

	private static TerminalInputEvent CreateControlKey(
		byte value
	) {
		char character = value switch {
			0 => '@',
			>= 1 and <= 26 => (char)( 'A' + value - 1 ),
			28 => '\\',
			29 => ']',
			30 => '^',
			31 => '_',
			_ => (char)( '@' + value )
		};

		return TerminalInputEvent.FromKey(
			TerminalKey.Character,
			TerminalKeyModifiers.Control,
			new Rune( character )
		);
	}

	private void FindKeySequenceMatch(
		out KeySequence? exact,
		out bool needsMore
	) {
		exact = null;
		needsMore = false;

		foreach ( KeySequence sequence in this.keySequences ) {
			if ( this.bufferedBytes.Count >= sequence.Bytes.Length ) {
				if ( this.BufferStartsWith( sequence.Bytes ) ) {
					exact ??= sequence;
				}
				continue;
			}

			if ( this.SequenceStartsWithBuffer( sequence.Bytes ) ) {
				needsMore = true;
			}
		}
	}

	private TerminalInputEvent ConsumeKeySequence(
		KeySequence sequence
	) {
		ArgumentNullException.ThrowIfNull( sequence );

		this.Consume( sequence.Bytes.Length );
		TerminalInputEvent inputEvent = sequence.InputEvent;
		if ( inputEvent.Paste is { Phase: TerminalPastePhase.Begin } ) {
			this.pasteActive = true;
		}
		return inputEvent;
	}

	private bool BufferStartsWith(
		IReadOnlyList<byte> bytes
	) {
		if ( this.bufferedBytes.Count < bytes.Count ) {
			return false;
		}

		for ( int index = 0; index < bytes.Count; index++ ) {
			if ( this.bufferedBytes[ index ] != bytes[ index ] ) {
				return false;
			}
		}

		return true;
	}

	private bool SequenceStartsWithBuffer(
		IReadOnlyList<byte> bytes
	) {
		if ( this.bufferedBytes.Count >= bytes.Count ) {
			return false;
		}

		for ( int index = 0; index < this.bufferedBytes.Count; index++ ) {
			if ( this.bufferedBytes[ index ] != bytes[ index ] ) {
				return false;
			}
		}

		return true;
	}

	private async ValueTask<bool> ReadMoreAsync(
		CancellationToken cancellationToken
	) {
		if ( this.endOfInput ) {
			return false;
		}

		Task<int> readTask = this.EnsurePendingRead( cancellationToken );
		int count = await this.CompletePendingReadAsync( readTask ).ConfigureAwait( false );
		return 0 < count;
	}

	private async ValueTask<bool> ReadMoreWithinEscapeWindowAsync(
		CancellationToken cancellationToken
	) {
		if ( this.endOfInput ) {
			return false;
		}

		Task<int> readTask = this.EnsurePendingRead( cancellationToken );
		if ( readTask.IsCompleted ) {
			return 0 < await this.CompletePendingReadAsync( readTask ).ConfigureAwait( false );
		}

		if ( TimeSpan.Zero == this.escapeSequenceTimeout ) {
			return false;
		}

		Task delayTask = this.monotonicClock.DelayAsync(
			this.escapeSequenceTimeout,
			cancellationToken
		).AsTask();
		Task completed = await Task.WhenAny(
			readTask,
			delayTask
		).ConfigureAwait( false );

		if ( ReferenceEquals( completed, delayTask ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			return false;
		}

		return 0 < await this.CompletePendingReadAsync( readTask ).ConfigureAwait( false );
	}

	private Task<int> EnsurePendingRead(
		CancellationToken cancellationToken
	) {
		if ( this.pendingRead is not null ) {
			return this.pendingRead;
		}

		int remainingCapacity = this.maximumBufferedBytes - this.bufferedBytes.Count;
		if ( 0 >= remainingCapacity ) {
			throw new InvalidOperationException(
				$"The terminal input decoder reached its {this.maximumBufferedBytes}-byte buffer limit."
			);
		}

		this.pendingReadCapacity = Math.Min( ReadBufferSize, remainingCapacity );
		this.pendingRead = this.input.ReadAsync(
			this.readBuffer.AsMemory( 0, this.pendingReadCapacity ),
			cancellationToken
		).AsTask();

		return this.pendingRead;
	}

	private async ValueTask<int> CompletePendingReadAsync(
		Task<int> readTask
	) {
		ArgumentNullException.ThrowIfNull( readTask );

		int capacity = this.pendingReadCapacity;
		int count;

		try {
			count = await readTask.ConfigureAwait( false );
		} finally {
			if ( ReferenceEquals( this.pendingRead, readTask ) && readTask.IsCompleted ) {
				this.pendingRead = null;
				this.pendingReadCapacity = 0;
			}
		}

		if ( count < 0 || count > capacity ) {
			throw new InvalidOperationException(
				"The terminal input source returned an invalid byte count."
			);
		}
		if ( 0 == count ) {
			this.endOfInput = true;
			return 0;
		}
		if ( this.maximumBufferedBytes - this.bufferedBytes.Count < count ) {
			throw new InvalidOperationException(
				$"The terminal input decoder exceeded its {this.maximumBufferedBytes}-byte buffer limit."
			);
		}

		for ( int index = 0; index < count; index++ ) {
			this.bufferedBytes.Add( this.readBuffer[ index ] );
		}

		return count;
	}

	private void AddFocusCapabilities(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		if (
			!terminal.TryGetExtendedString(
				"kxIN",
				out string? focusIn
			)
			|| string.IsNullOrEmpty( focusIn )
		) {
			return;
		}
		if (
			!terminal.TryGetExtendedString(
				"kxOUT",
				out string? focusOut
			)
			|| string.IsNullOrEmpty( focusOut )
		) {
			return;
		}

		this.AddExtendedCapability(
			"kxIN",
			focusIn,
			TerminalInputEvent.FromFocus(
				new TerminalFocusEvent( TerminalFocusState.Focused )
			)
		);
		this.AddExtendedCapability(
			"kxOUT",
			focusOut,
			TerminalInputEvent.FromFocus(
				new TerminalFocusEvent( TerminalFocusState.Unfocused )
			)
		);
	}

	private byte[]? AddBracketedPasteCapabilities(
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( terminal );

		if (
			!terminal.TryGetExtendedString(
				"PS",
				out string? pasteStart
			)
			|| string.IsNullOrEmpty( pasteStart )
		) {
			return null;
		}
		if (
			!terminal.TryGetExtendedString(
				"PE",
				out string? pasteEnd
			)
			|| string.IsNullOrEmpty( pasteEnd )
		) {
			return null;
		}

		byte[] startBytes = EncodeExtendedCapability(
			pasteStart,
			"PS"
		);
		byte[] endBytes = EncodeExtendedCapability(
			pasteEnd,
			"PE"
		);

		this.AddSequence(
			startBytes,
			TerminalInputEvent.FromPaste(
				new TerminalPasteEvent( TerminalPastePhase.Begin )
			),
			"Terminal extended input capability 'PS'"
		);
		this.ValidateSequenceLength(
			endBytes,
			"Terminal extended input capability 'PE'"
		);

		return endBytes;
	}

	private void AddExtendedCapability(
		string name,
		string value,
		TerminalInputEvent inputEvent
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( name );
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( inputEvent );

		this.AddSequence(
			EncodeExtendedCapability(
				value,
				name
			),
			inputEvent,
			$"Terminal extended input capability '{name}'"
		);
	}

	private void AddCapability(
		TerminalDescription terminal,
		StringCapability capability,
		TerminalInputEvent inputEvent
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( inputEvent );

		string? value = terminal.GetString( capability );
		if ( string.IsNullOrEmpty( value ) ) {
			return;
		}

		this.AddSequence(
			EncodeCapability(
				value,
				capability
			),
			inputEvent,
			$"Terminal key capability '{capability}'"
		);
	}

	private void AddSequence(
		byte[] bytes,
		TerminalInputEvent inputEvent,
		string displayName
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ArgumentNullException.ThrowIfNull( inputEvent );
		ArgumentException.ThrowIfNullOrWhiteSpace( displayName );

		if ( 0 == bytes.Length ) {
			return;
		}
		this.ValidateSequenceLength(
			bytes,
			displayName
		);

		foreach ( KeySequence existing in this.keySequences ) {
			if ( existing.Bytes.AsSpan().SequenceEqual( bytes ) ) {
				return;
			}
		}

		this.keySequences.Add(
			new KeySequence(
				bytes,
				inputEvent
			)
		);
	}

	private void ValidateSequenceLength(
		IReadOnlyCollection<byte> bytes,
		string displayName
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		ArgumentException.ThrowIfNullOrWhiteSpace( displayName );

		if ( bytes.Count > this.maximumBufferedBytes ) {
			throw new InvalidOperationException(
				$"{displayName} requires {bytes.Count} bytes, exceeding "
					+ $"the decoder limit of {this.maximumBufferedBytes} bytes."
			);
		}
	}

	private static byte[] EncodeExtendedCapability(
		string value,
		string name
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentException.ThrowIfNullOrWhiteSpace( name );

		foreach ( char character in value ) {
			if ( byte.MaxValue < character ) {
				throw new InvalidOperationException(
					$"Terminal extended input capability '{name}' contains data outside the "
						+ "reversible 8-bit terminfo range."
				);
			}
		}

		return Encoding.Latin1.GetBytes( value );
	}

	private static byte[] EncodeCapability(
		string value,
		StringCapability capability
	) {
		ArgumentNullException.ThrowIfNull( value );

		foreach ( char character in value ) {
			if ( byte.MaxValue < character ) {
				throw new InvalidOperationException(
					$"Terminal key capability '{capability}' contains data outside the reversible "
						+ "8-bit terminfo range."
				);
			}
		}

		return Encoding.Latin1.GetBytes( value );
	}

	private void Consume(
		int count
	) {
		if ( 0 >= count || count > this.bufferedBytes.Count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}

		this.bufferedBytes.RemoveRange( 0, count );
	}

	private sealed class KeySequence {
		internal KeySequence(
			byte[] bytes,
			TerminalInputEvent inputEvent
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			ArgumentNullException.ThrowIfNull( inputEvent );

			this.Bytes = bytes;
			this.InputEvent = inputEvent;
		}

		internal byte[] Bytes {
			get;
		}

		internal TerminalInputEvent InputEvent {
			get;
		}
	}
}
