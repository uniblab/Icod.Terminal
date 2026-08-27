namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T17 SGR and legacy mouse decoding without touching the process terminal.
/// </summary>
public sealed class TerminalMouseDecoderTests {
	[Fact]
	public async Task FragmentedSgrPressNormalizesCoordinatesAndModifiers() {
		byte[] bytes = Encoding.ASCII.GetBytes( "\u001b[<28;1;8M" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				bytes.Select(
					static value => new[] { value }
				)
			),
			CreateSgrTerminal()
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		TerminalMouseEvent mouse = Assert.IsType<TerminalMouseEvent>(
			inputEvent.Mouse
		);

		Assert.Equal( TerminalInputEventKind.Mouse, inputEvent.Kind );
		Assert.Equal( TerminalMouseAction.Press, mouse.Action );
		Assert.Equal( TerminalMouseButton.Primary, mouse.Button );
		Assert.Equal( 0, mouse.Column );
		Assert.Equal( 7, mouse.Row );
		Assert.Equal(
			TerminalKeyModifiers.Shift
				| TerminalKeyModifiers.Alt
				| TerminalKeyModifiers.Control,
			mouse.Modifiers
		);
	}

	[Fact]
	public async Task SgrFramesRepresentReleaseMotionWheelAndAdditionalButtons() {
		byte[] bytes = Encoding.ASCII.GetBytes(
			"\u001b[<0;10;20M"
				+ "\u001b[<32;11;20M"
				+ "\u001b[<0;11;20m"
				+ "\u001b[<35;12;21M"
				+ "\u001b[<64;12;21M"
				+ "\u001b[<65;12;21M"
				+ "\u001b[<66;12;21M"
				+ "\u001b[<67;12;21M"
				+ "\u001b[<128;13;22M"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ bytes ] ),
			CreateSgrTerminal()
		);

		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Press,
			TerminalMouseButton.Primary,
			9,
			19
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Move,
			TerminalMouseButton.Primary,
			10,
			19
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Release,
			TerminalMouseButton.Primary,
			10,
			19
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Move,
			TerminalMouseButton.None,
			11,
			20
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.WheelUp,
			TerminalMouseButton.None,
			11,
			20
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.WheelDown,
			TerminalMouseButton.None,
			11,
			20
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.WheelLeft,
			TerminalMouseButton.None,
			11,
			20
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.WheelRight,
			TerminalMouseButton.None,
			11,
			20
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Press,
			TerminalMouseButton.Button4,
			12,
			21
		);
	}

	[Fact]
	public async Task SgrCoordinatesAreNotLimitedToLegacyByteRange() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.ASCII.GetBytes( "\u001b[<2;4000;3000M" )
				]
			),
			CreateSgrTerminal()
		);

		TerminalMouseEvent mouse = Assert.IsType<TerminalMouseEvent>(
			( await decoder.ReadAsync() ).Mouse
		);

		Assert.Equal( TerminalMouseButton.Secondary, mouse.Button );
		Assert.Equal( 3999, mouse.Column );
		Assert.Equal( 2999, mouse.Row );
	}

	[Fact]
	public async Task LegacyFramesSupportBoundaryCoordinatesMotionReleaseAndWheel() {
		byte[] bytes = LegacyFrame( 0, 223, 223 )
			.Concat( LegacyFrame( 32, 222, 221 ) )
			.Concat( LegacyFrame( 3, 222, 221 ) )
			.Concat( LegacyFrame( 64, 220, 219 ) )
			.ToArray();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ bytes ] ),
			CreateLegacyTerminal()
		);

		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Press,
			TerminalMouseButton.Primary,
			222,
			222
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Move,
			TerminalMouseButton.Primary,
			221,
			220
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.Release,
			TerminalMouseButton.Primary,
			221,
			220
		);
		AssertMouse(
			await decoder.ReadAsync(),
			TerminalMouseAction.WheelUp,
			TerminalMouseButton.None,
			219,
			218
		);
	}

	[Fact]
	public async Task MultipleSgrFramesFromOneReadRemainIndividuallyBuffered() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.ASCII.GetBytes(
						"\u001b[<0;2;3M\u001b[<1;4;5M"
					)
				]
			),
			CreateSgrTerminal()
		);

		TerminalMouseEvent first = Assert.IsType<TerminalMouseEvent>(
			( await decoder.ReadAsync() ).Mouse
		);
		TerminalMouseEvent second = Assert.IsType<TerminalMouseEvent>(
			( await decoder.ReadAsync() ).Mouse
		);

		Assert.Equal( TerminalMouseButton.Primary, first.Button );
		Assert.Equal( 1, first.Column );
		Assert.Equal( 2, first.Row );
		Assert.Equal( TerminalMouseButton.Middle, second.Button );
		Assert.Equal( 3, second.Column );
		Assert.Equal( 4, second.Row );
	}

	[Fact]
	public async Task MalformedSgrFrameFallsBackWithoutConsumingMouseEvent() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.ASCII.GetBytes( "\u001b[<0;0;1M" )
				]
			),
			CreateSgrTerminal()
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Escape, inputEvent.Key );
		Assert.Null( inputEvent.Mouse );
	}

	[Fact]
	public async Task PartialMousePrefixStillUsesEscapeAmbiguityTimeout() {
		using CancellationTokenSource cancellation = new();
		PrefixThenBlockTerminalInput input = new(
			Encoding.ASCII.GetBytes( "\u001b[" )
		);
		TerminalInputDecoder decoder = new(
			input,
			CreateSgrTerminal(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 10 ),
			TerminalSession.MaximumBufferedInputBytes
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync(
			cancellation.Token
		);
		cancellation.Cancel();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Escape, inputEvent.Key );
	}

	[Fact]
	public async Task MouseFocusPasteAndTextRemainInOneOrderedDecoderStream() {
		const string pastedMouseText = "\u001b[<0;9;9M";
		byte[] bytes = Encoding.UTF8.GetBytes(
			"x"
				+ "\u001b[<0;2;3M"
				+ "\u001b[I"
				+ "\u001b[200~"
				+ pastedMouseText
				+ "\u001b[201~"
				+ "y"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ bytes ] ),
			CreateSgrRichInputTerminal()
		);

		Assert.Equal( new Rune( 'x' ), ( await decoder.ReadAsync() ).Character );
		Assert.Equal( TerminalInputEventKind.Mouse, ( await decoder.ReadAsync() ).Kind );
		Assert.Equal( TerminalInputEventKind.Focus, ( await decoder.ReadAsync() ).Kind );

		TerminalPasteEvent begin = Assert.IsType<TerminalPasteEvent>(
			( await decoder.ReadAsync() ).Paste
		);
		TerminalPasteEvent data = Assert.IsType<TerminalPasteEvent>(
			( await decoder.ReadAsync() ).Paste
		);
		TerminalPasteEvent end = Assert.IsType<TerminalPasteEvent>(
			( await decoder.ReadAsync() ).Paste
		);

		Assert.Equal( TerminalPastePhase.Begin, begin.Phase );
		Assert.Equal( TerminalPastePhase.Data, data.Phase );
		Assert.Equal( pastedMouseText, data.Text );
		Assert.Equal( TerminalPastePhase.End, end.Phase );
		Assert.Equal( new Rune( 'y' ), ( await decoder.ReadAsync() ).Character );
	}

	private static void AssertMouse(
		TerminalInputEvent inputEvent,
		TerminalMouseAction action,
		TerminalMouseButton button,
		int column,
		int row
	) {
		ArgumentNullException.ThrowIfNull( inputEvent );

		TerminalMouseEvent mouse = Assert.IsType<TerminalMouseEvent>(
			inputEvent.Mouse
		);
		Assert.Equal( TerminalInputEventKind.Mouse, inputEvent.Kind );
		Assert.Equal( action, mouse.Action );
		Assert.Equal( button, mouse.Button );
		Assert.Equal( column, mouse.Column );
		Assert.Equal( row, mouse.Row );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription terminal
	) {
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( terminal );

		return new TerminalInputDecoder(
			input,
			terminal,
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes,
			TerminalSession.MaximumBufferedInputBytes
		);
	}

	private static TerminalDescription CreateSgrTerminal() {
		return new TerminalDescriptionBuilder( "t17-sgr" )
			.SetString(
				StringCapability.KeyMouse,
				"\u001b[<"
			)
			.Build();
	}

	private static TerminalDescription CreateLegacyTerminal() {
		return new TerminalDescriptionBuilder( "t17-legacy" )
			.SetString(
				StringCapability.KeyMouse,
				"\u001b[M"
			)
			.Build();
	}

	private static TerminalDescription CreateSgrRichInputTerminal() {
		return new TerminalDescriptionBuilder( "t17-rich-input" )
			.SetString(
				StringCapability.KeyMouse,
				"\u001b[<"
			)
			.SetExtendedString(
				"kxIN",
				"\u001b[I"
			)
			.SetExtendedString(
				"kxOUT",
				"\u001b[O"
			)
			.SetExtendedString(
				"PS",
				"\u001b[200~"
			)
			.SetExtendedString(
				"PE",
				"\u001b[201~"
			)
			.Build();
	}

	private static byte[] LegacyFrame(
		int code,
		int column,
		int row
	) {
		if ( 0 > code || 223 < code ) {
			throw new ArgumentOutOfRangeException( nameof( code ) );
		}
		if ( column is < 1 or > 223 ) {
			throw new ArgumentOutOfRangeException( nameof( column ) );
		}
		if ( row is < 1 or > 223 ) {
			throw new ArgumentOutOfRangeException( nameof( row ) );
		}

		return [
			0x1B,
			0x5B,
			0x4D,
			(byte)( code + 32 ),
			(byte)( column + 32 ),
			(byte)( row + 32 )
		];
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
					"The scripted mouse input chunk exceeds the decoder read buffer."
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
						"The scripted mouse prefix exceeds the decoder read buffer."
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
