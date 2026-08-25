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

	private readonly ITerminalInput input;
	private readonly IMonotonicClock monotonicClock;
	private readonly TimeSpan escapeSequenceTimeout;
	private readonly int maximumBufferedBytes;
	private readonly List<byte> bufferedBytes = [];
	private readonly byte[] readBuffer = new byte[ ReadBufferSize ];
	private readonly List<KeySequence> keySequences = [];

	private Task<int>? pendingRead;
	private int pendingReadCapacity;
	private bool endOfInput;

	internal TerminalInputDecoder(
		ITerminalInput input,
		TerminalDescription terminal,
		IMonotonicClock monotonicClock,
		TimeSpan escapeSequenceTimeout,
		int maximumBufferedBytes
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

		this.input = input;
		this.monotonicClock = monotonicClock;
		this.escapeSequenceTimeout = escapeSequenceTimeout;
		this.maximumBufferedBytes = maximumBufferedBytes;

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
		return sequence.InputEvent;
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

		byte[] bytes = EncodeCapability( value, capability );
		if ( 0 == bytes.Length ) {
			return;
		}
		if ( bytes.Length > this.maximumBufferedBytes ) {
			throw new InvalidOperationException(
				$"Terminal key capability '{capability}' requires {bytes.Length} bytes, exceeding "
					+ $"the decoder limit of {this.maximumBufferedBytes} bytes."
			);
		}

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
