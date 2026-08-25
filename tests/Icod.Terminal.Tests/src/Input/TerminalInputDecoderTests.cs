namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies incremental T08 byte decoding without touching the process terminal.
/// </summary>
public sealed class TerminalInputDecoderTests {
	[Fact]
	public async Task FragmentedTerminfoSequenceDecodesAsOneKey() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "fragmented" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[A"
			)
			.Build();
		ScriptedTerminalInput input = new(
			[
				[ 0x1B ],
				Encoding.ASCII.GetBytes( "[" ),
				Encoding.ASCII.GetBytes( "A" )
			]
		);
		TerminalInputDecoder decoder = CreateDecoder( input, terminal );

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Up, inputEvent.Key );
	}

	[Fact]
	public async Task MultipleEventsFromOneReadRemainBuffered() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.UTF8.GetBytes( "ab" )
				]
			)
		);

		TerminalInputEvent first = await decoder.ReadAsync();
		TerminalInputEvent second = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Text, first.Kind );
		Assert.Equal( new Rune( 'a' ), first.Character );
		Assert.Equal( TerminalInputEventKind.Text, second.Kind );
		Assert.Equal( new Rune( 'b' ), second.Character );
	}

	[Fact]
	public async Task FragmentedUtf8DecodesOneUnicodeScalar() {
		byte[] smile = Encoding.UTF8.GetBytes( "🙂" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					smile[ 0..1 ],
					smile[ 1..3 ],
					smile[ 3..4 ]
				]
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Text, inputEvent.Kind );
		Assert.Equal( new Rune( 0x1F642 ), inputEvent.Character );
	}

	[Fact]
	public async Task LongerOverlappingSequenceWinsWhenContinuationArrives() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "overlap" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[A"
			)
			.SetString(
				StringCapability.KeyF1,
				"\u001b[AB"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[A" ),
					Encoding.Latin1.GetBytes( "B" )
				]
			),
			terminal
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalKey.Function, inputEvent.Key );
		Assert.Equal( 1, inputEvent.FunctionKeyNumber );
	}

	[Fact]
	public async Task ExactOverlappingSequenceWinsAfterEscapeTimeout() {
		using CancellationTokenSource cancellation = new();
		TerminalDescription terminal = new TerminalDescriptionBuilder( "overlap-timeout" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[A"
			)
			.SetString(
				StringCapability.KeyF1,
				"\u001b[AB"
			)
			.Build();
		PrefixThenBlockTerminalInput input = new(
			Encoding.Latin1.GetBytes( "\u001b[A" )
		);
		TerminalInputDecoder decoder = new(
			input,
			terminal,
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 10 ),
			TerminalSession.MaximumBufferedInputBytes
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync( cancellation.Token );
		cancellation.Cancel();

		Assert.Equal( TerminalKey.Up, inputEvent.Key );
	}

	[Fact]
	public async Task IsolatedEscapeUsesBoundedAmbiguityDelay() {
		using CancellationTokenSource cancellation = new();
		PrefixThenBlockTerminalInput input = new( [ 0x1B ] );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "escape" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[A"
			)
			.Build();
		TerminalInputDecoder decoder = new(
			input,
			terminal,
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 10 ),
			TerminalSession.MaximumBufferedInputBytes
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync( cancellation.Token );
		cancellation.Cancel();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Escape, inputEvent.Key );
	}

	[Fact]
	public async Task NavigationFunctionShiftTabControlAndEndOfInputAreRepresented() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "keys" )
			.SetString( StringCapability.KeyBackTab, "\u001b[Z" )
			.SetString( StringCapability.KeyCursorLeft, "\u001b[D" )
			.SetString( StringCapability.KeyHome, "\u001b[H" )
			.SetString( StringCapability.KeyEnd, "\u001b[F" )
			.SetString( StringCapability.KeyPreviousPage, "\u001b[5~" )
			.SetString( StringCapability.KeyNextPage, "\u001b[6~" )
			.SetString( StringCapability.KeyInsertCharacter, "\u001b[2~" )
			.SetString( StringCapability.KeyDeleteCharacter, "\u001b[3~" )
			.SetString( StringCapability.KeyF37, "\u001b[37~" )
			.Build();
		byte[] bytes = Encoding.Latin1.GetBytes(
			"\u001b[Z\u001b[D\u001b[H\u001b[F\u001b[5~\u001b[6~\u001b[2~\u001b[3~\u001b[37~"
		).Concat(
			new byte[] { 0x03, 0x0D, 0x20, 0x09, 0x08, 0x7F }
		).ToArray();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ bytes ] ),
			terminal
		);

		Assert.Equal( TerminalKey.Tab, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Left, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Home, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.End, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.PageUp, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.PageDown, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Insert, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Delete, ( await decoder.ReadAsync() ).Key );

		TerminalInputEvent function = await decoder.ReadAsync();
		Assert.Equal( TerminalKey.Function, function.Key );
		Assert.Equal( 37, function.FunctionKeyNumber );

		TerminalInputEvent control = await decoder.ReadAsync();
		Assert.Equal( TerminalKey.Character, control.Key );
		Assert.Equal( TerminalKeyModifiers.Control, control.Modifiers );
		Assert.Equal( new Rune( 'C' ), control.Character );

		Assert.Equal( TerminalKey.Enter, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Space, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Tab, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Backspace, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalKey.Backspace, ( await decoder.ReadAsync() ).Key );
		Assert.Equal( TerminalInputEventKind.EndOfInput, ( await decoder.ReadAsync() ).Kind );
	}

	[Fact]
	public void RejectsTerminfoKeySequenceLargerThanBufferBound() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "oversized" )
			.SetString(
				StringCapability.KeyCursorUp,
				"\u001b[ABC"
			)
			.Build();

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => new TerminalInputDecoder(
				new ScriptedTerminalInput( [] ),
				terminal,
				SystemMonotonicClock.Instance,
				TimeSpan.Zero,
				4
			)
		);

		Assert.Contains( "decoder limit", exception.Message );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( input );

		return new TerminalInputDecoder(
			input,
			terminal ?? new TerminalDescriptionBuilder( "text" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes
		);
	}

	private sealed class ScriptedTerminalInput : ITerminalInput {
		private readonly Queue<byte[]> chunks;

		internal ScriptedTerminalInput(
			IEnumerable<byte[]> chunks
		) {
			ArgumentNullException.ThrowIfNull( chunks );
			this.chunks = new Queue<byte[]>(
				chunks.Select( static value => value.ToArray() )
			);
		}

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();

			if ( 0 == this.chunks.Count ) {
				return ValueTask.FromResult( 0 );
			}

			byte[] chunk = this.chunks.Dequeue();
			if ( chunk.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted chunk exceeds the decoder read buffer."
				);
			}

			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}

	private sealed class PrefixThenBlockTerminalInput : ITerminalInput {
		private readonly byte[] prefix;
		private int readCount;

		internal PrefixThenBlockTerminalInput(
			byte[] prefix
		) {
			ArgumentNullException.ThrowIfNull( prefix );
			this.prefix = prefix.ToArray();
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( 1 == Interlocked.Increment( ref this.readCount ) ) {
				this.prefix.AsSpan().CopyTo( buffer.Span );
				return this.prefix.Length;
			}

			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}
}
