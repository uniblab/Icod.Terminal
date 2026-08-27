namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T16 focus and bracketed-paste decoding without touching the process terminal.
/// </summary>
public sealed class TerminalFocusPasteDecoderTests {
	[Fact]
	public async Task FragmentedFocusReportsDecodeAsTypedEvents() {
		byte[] bytes = Encoding.Latin1.GetBytes( "x\u001b[I\u001b[Oy" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				bytes.Select(
					static value => new[] { value }
				)
			),
			CreateRichInputTerminal()
		);

		TerminalInputEvent leadingText = await decoder.ReadAsync();
		TerminalInputEvent focused = await decoder.ReadAsync();
		TerminalInputEvent unfocused = await decoder.ReadAsync();
		TerminalInputEvent trailingText = await decoder.ReadAsync();

		Assert.Equal( new Rune( 'x' ), leadingText.Character );
		Assert.Equal( TerminalInputEventKind.Focus, focused.Kind );
		Assert.Equal(
			TerminalFocusState.Focused,
			Assert.IsType<TerminalFocusEvent>( focused.Focus ).State
		);
		Assert.Equal( TerminalInputEventKind.Focus, unfocused.Kind );
		Assert.Equal(
			TerminalFocusState.Unfocused,
			Assert.IsType<TerminalFocusEvent>( unfocused.Focus ).State
		);
		Assert.Equal( new Rune( 'y' ), trailingText.Character );
	}

	[Fact]
	public async Task FragmentedPastePreservesEscapeLookingContent() {
		const string body = "alpha\u001b[31mβ";
		byte[] bytes = Encoding.UTF8.GetBytes(
			"\u001b[200~"
				+ body
				+ "\u001b[201~"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				bytes.Select(
					static value => new[] { value }
				)
			),
			CreateRichInputTerminal()
		);

		TerminalInputEvent begin = await decoder.ReadAsync();
		Assert.Equal( TerminalInputEventKind.Paste, begin.Kind );
		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>( begin.Paste ).Phase
		);

		StringBuilder observed = new();
		while ( true ) {
			TerminalInputEvent inputEvent = await decoder.ReadAsync();
			Assert.Equal( TerminalInputEventKind.Paste, inputEvent.Kind );
			TerminalPasteEvent paste = Assert.IsType<TerminalPasteEvent>(
				inputEvent.Paste
			);
			if ( TerminalPastePhase.End == paste.Phase ) {
				break;
			}

			Assert.Equal( TerminalPastePhase.Data, paste.Phase );
			observed.Append( paste.Text );
		}

		Assert.Equal( body, observed.ToString() );
	}

	[Fact]
	public async Task PasteUsesConfiguredBoundedChunks() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes(
						"\u001b[200~abcdefgh\u001b[201~"
					)
				]
			),
			CreateRichInputTerminal(),
			pasteChunkBytes: 3
		);

		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Phase
		);

		Assert.Equal(
			"abc",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);
		Assert.Equal(
			"def",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);
		Assert.Equal(
			"gh",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);

		Assert.Equal(
			TerminalPastePhase.End,
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Phase
		);
	}

	[Fact]
	public async Task PasteChunkBoundaryNeverSplitsUtf8Scalar() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.UTF8.GetBytes(
						"\u001b[200~🙂x\u001b[201~"
					)
				]
			),
			CreateRichInputTerminal(),
			pasteChunkBytes: 1
		);

		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Phase
		);
		Assert.Equal(
			"🙂",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);
		Assert.Equal(
			"x",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);
		Assert.Equal(
			TerminalPastePhase.End,
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Phase
		);
	}

	[Fact]
	public async Task TruncatedPasteFlushesDataThenReportsEndOfInput() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[200~abc\u001b[20" )
				]
			),
			CreateRichInputTerminal()
		);

		TerminalInputEvent begin = await decoder.ReadAsync();
		TerminalInputEvent data = await decoder.ReadAsync();
		TerminalInputEvent endOfInput = await decoder.ReadAsync();

		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>( begin.Paste ).Phase
		);
		Assert.Equal(
			"abc\u001b[20",
			Assert.IsType<TerminalPasteEvent>( data.Paste ).Text
		);
		Assert.Equal( TerminalInputEventKind.EndOfInput, endOfInput.Kind );
	}

	[Fact]
	public async Task PartialPasteTerminatorCanBeCancelledWithoutBecomingData() {
		using CancellationTokenSource cancellation = new();
		PrefixThenBlockTerminalInput input = new(
			Encoding.Latin1.GetBytes(
				"\u001b[200~abc\u001b[20"
			)
		);
		TerminalInputDecoder decoder = CreateDecoder(
			input,
			CreateRichInputTerminal()
		);

		Assert.Equal(
			TerminalPastePhase.Begin,
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Phase
		);
		Assert.Equal(
			"abc",
			Assert.IsType<TerminalPasteEvent>(
				( await decoder.ReadAsync() ).Paste
			).Text
		);

		cancellation.CancelAfter( TimeSpan.FromMilliseconds( 100 ) );
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => decoder.ReadAsync( cancellation.Token ).AsTask()
		);
	}

	[Fact]
	public async Task PartialRichSequenceStillUsesEscapeAmbiguityTimeout() {
		using CancellationTokenSource cancellation = new();
		PrefixThenBlockTerminalInput input = new(
			Encoding.Latin1.GetBytes( "\u001b[" )
		);
		TerminalInputDecoder decoder = CreateDecoder(
			input,
			CreateRichInputTerminal(),
			escapeSequenceTimeout: TimeSpan.FromMilliseconds( 10 )
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync(
			cancellation.Token
		);
		cancellation.Cancel();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Escape, inputEvent.Key );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription? terminal = null,
		int? pasteChunkBytes = null,
		TimeSpan? escapeSequenceTimeout = null
	) {
		ArgumentNullException.ThrowIfNull( input );

		return new TerminalInputDecoder(
			input,
			terminal ?? CreateRichInputTerminal(),
			SystemMonotonicClock.Instance,
			escapeSequenceTimeout ?? TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes,
			pasteChunkBytes ?? TerminalSession.MaximumBufferedInputBytes
		);
	}

	private static TerminalDescription CreateRichInputTerminal() {
		return new TerminalDescriptionBuilder( "t16-rich-input" )
			.SetExtendedString(
				"BE",
				"\u001b[?2004h"
			)
			.SetExtendedString(
				"BD",
				"\u001b[?2004l"
			)
			.SetExtendedString(
				"PS",
				"\u001b[200~"
			)
			.SetExtendedString(
				"PE",
				"\u001b[201~"
			)
			.SetExtendedString(
				"fe",
				"\u001b[?1004h"
			)
			.SetExtendedString(
				"fd",
				"\u001b[?1004l"
			)
			.SetExtendedString(
				"kxIN",
				"\u001b[I"
			)
			.SetExtendedString(
				"kxOUT",
				"\u001b[O"
			)
			.Build();
	}

	private sealed class ScriptedTerminalInput : ITerminalInput {
		private readonly Queue<byte[]> chunks;

		internal ScriptedTerminalInput(
			IEnumerable<byte[]> chunks
		) {
			ArgumentNullException.ThrowIfNull( chunks );
			this.chunks = new Queue<byte[]>(
				chunks.Select(
					static value => value.ToArray()
				)
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
					"The scripted input chunk exceeds the decoder read buffer."
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
				if ( this.prefix.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted prefix exceeds the decoder read buffer."
					);
				}

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
