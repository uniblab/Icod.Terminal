namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T22 bounded response framing and expectation-driven demultiplexing.
/// </summary>
public sealed class TerminalResponseRoutingTests {
	[Fact]
	public async Task ExpectedCsiResponseWinsOverAmbiguousModifiedFunctionKey() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001b[1;2R" );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "ambiguous-cpr" )
			.SetString(
				StringCapability.KeyF3,
				"\u001b[1;2R"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					response.Concat( Encoding.UTF8.GetBytes( "x" ) ).ToArray()
				]
			),
			terminal
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Csi,
				response
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( TerminalInputEventKind.Text, inputEvent.Kind );
		Assert.Equal( new Rune( 'x' ), inputEvent.Character );
		Assert.Equal( response, frame.Bytes.ToArray() );
	}

	[Fact]
	public async Task CprShapedInputRemainsModifiedFunctionKeyWithoutExpectation() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "ambiguous-key" )
			.SetString(
				StringCapability.KeyF3,
				"\u001b[1;2R"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					Encoding.Latin1.GetBytes( "\u001b[1;2R" )
				]
			),
			terminal
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Function, inputEvent.Key );
		Assert.Equal( 3, inputEvent.FunctionKeyNumber );
		Assert.Equal( TerminalKeyModifiers.Shift, inputEvent.Modifiers );
	}

	[Fact]
	public async Task RejectedCsiCandidateRemainsOnApplicationInputPath() {
		byte[] observed = Encoding.Latin1.GetBytes( "\u001b[1;2R" );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "rejected-cpr" )
			.SetString(
				StringCapability.KeyF3,
				"\u001b[1;2R"
			)
			.Build();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ observed ] ),
			terminal
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Csi,
				Encoding.Latin1.GetBytes( "\u001b[3;4R" )
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.False( expectation.Response.IsCompleted );
		Assert.Equal( TerminalKey.Function, inputEvent.Key );
		Assert.Equal( 3, inputEvent.FunctionKeyNumber );
		Assert.Equal( TerminalKeyModifiers.Shift, inputEvent.Modifiers );
	}

	[Fact]
	public async Task FragmentedCsiResponseIsRoutedAcrossArbitraryReadBoundaries() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001b[24;80R" );
		List<byte[]> chunks = response
			.Select( static value => new[] { value } )
			.ToList();
		chunks.Add( Encoding.UTF8.GetBytes( "z" ) );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( chunks )
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Csi,
				response
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'z' ), inputEvent.Character );
	}

	[Fact]
	public async Task SevenBitDcsResponseIsRoutedWithoutConsumingAdjacentInput() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001bP1$r0m\u001b\\" );
		byte[] input = Encoding.UTF8.GetBytes( "a" )
			.Concat( response )
			.Concat( Encoding.UTF8.GetBytes( "b" ) )
			.ToArray();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ input ] )
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Dcs,
				response
			)
		);

		TerminalInputEvent before = await decoder.ReadAsync();
		Assert.False( expectation.Response.IsCompleted );

		TerminalInputEvent after = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( new Rune( 'a' ), before.Character );
		Assert.Equal( new Rune( 'b' ), after.Character );
		Assert.Equal( response, frame.Bytes.ToArray() );
	}

	[Fact]
	public async Task EightBitCsiResponseIsRecognizedOnlyWhenExpected() {
		byte[] response = [ 0x9B, (byte)'2', (byte)';', (byte)'5', (byte)'R' ];
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					response.Concat( Encoding.UTF8.GetBytes( "q" ) ).ToArray()
				]
			)
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Csi,
				response
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.True( frame.UsesEightBitIntroducer );
		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'q' ), inputEvent.Character );
	}

	[Fact]
	public async Task EightBitDcsResponseIsRecognizedWhenExpected() {
		byte[] response = [
			0x90,
			(byte)'1',
			(byte)'$',
			(byte)'r',
			(byte)'0',
			(byte)'m',
			0x9C
		];
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					response.Concat( Encoding.UTF8.GetBytes( "v" ) ).ToArray()
				]
			)
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Dcs,
				response
			)
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.True( frame.UsesEightBitIntroducer );
		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'v' ), inputEvent.Character );
	}

	[Fact]
	public async Task FocusInputRemainsOrderedAheadOfExpectedDcsResponse() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001bP1$r0m\u001b\\" );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "focus-response" )
			.SetExtendedString(
				"kxIN",
				"\u001b[I"
			)
			.SetExtendedString(
				"kxOUT",
				"\u001b[O"
			)
			.Build();
		byte[] input = Encoding.Latin1.GetBytes( "\u001b[I" )
			.Concat( response )
			.Concat( Encoding.UTF8.GetBytes( "f" ) )
			.ToArray();
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ input ] ),
			terminal
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Dcs,
				response
			)
		);

		TerminalInputEvent focus = await decoder.ReadAsync();
		Assert.False( expectation.Response.IsCompleted );

		TerminalInputEvent trailing = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( TerminalInputEventKind.Focus, focus.Kind );
		Assert.Equal(
			TerminalFocusState.Focused,
			Assert.IsType<TerminalFocusEvent>( focus.Focus ).State
		);
		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'f' ), trailing.Character );
	}

	[Fact]
	public async Task ResponseShapedBytesInsideBracketedPasteRemainPasteData() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001b[1;2R" );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "paste-response" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.Build();
		byte[] input = Encoding.Latin1.GetBytes(
			"\u001b[200~\u001b[1;2R\u001b[201~\u001b[1;2Rz"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [ input ] ),
			terminal
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new ExactResponseMatcher(
				TerminalResponseFrameKind.Csi,
				response
			)
		);

		TerminalInputEvent begin = await decoder.ReadAsync();
		TerminalInputEvent data = await decoder.ReadAsync();
		TerminalInputEvent end = await decoder.ReadAsync();
		Assert.False( expectation.Response.IsCompleted );

		TerminalInputEvent trailing = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( TerminalPastePhase.Begin, begin.Paste?.Phase );
		Assert.Equal( TerminalPastePhase.Data, data.Paste?.Phase );
		Assert.Equal( "\u001b[1;2R", data.Paste?.Text );
		Assert.Equal( TerminalPastePhase.End, end.Paste?.Phase );
		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'z' ), trailing.Character );
	}

	[Fact]
	public async Task OversizedIncompleteDcsCandidateFallsBackWithoutExceedingDecoderBound() {
		byte[] input = Encoding.Latin1.GetBytes( "\u001bP1$rabc" );
		TerminalInputDecoder decoder = new(
			new ScriptedTerminalInput( [ input ] ),
			new TerminalDescriptionBuilder( "bounded-dcs" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.Zero,
			8
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			new MatchAnyResponseMatcher( TerminalResponseFrameKind.Dcs )
		);

		TerminalInputEvent inputEvent = await decoder.ReadAsync();

		Assert.False( expectation.Response.IsCompleted );
		Assert.Equal( TerminalInputEventKind.Key, inputEvent.Kind );
		Assert.Equal( TerminalKey.Escape, inputEvent.Key );
	}

	[Fact]
	public void RejectsSecondActiveResponseExpectation() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( [] )
		);
		decoder.RegisterResponseExpectation(
			new MatchAnyResponseMatcher( TerminalResponseFrameKind.Csi )
		);

		Assert.Throws<InvalidOperationException>(
			() => decoder.RegisterResponseExpectation(
				new MatchAnyResponseMatcher( TerminalResponseFrameKind.Dcs )
			)
		);
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( input );

		return new TerminalInputDecoder(
			input,
			terminal ?? new TerminalDescriptionBuilder( "response-routing" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes
		);
	}

	private sealed class ExactResponseMatcher : ITerminalResponseMatcher {
		private readonly byte[] expected;

		internal ExactResponseMatcher(
			TerminalResponseFrameKind frameKind,
			byte[] expected
		) {
			ArgumentNullException.ThrowIfNull( expected );
			this.FrameKind = frameKind;
			this.expected = expected.ToArray();
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		}

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return this.FrameKind == frame.Kind
				&& frame.Bytes.Span.SequenceEqual( this.expected )
			;
		}
	}

	private sealed class MatchAnyResponseMatcher : ITerminalResponseMatcher {
		internal MatchAnyResponseMatcher(
			TerminalResponseFrameKind frameKind
		) {
			this.FrameKind = frameKind;
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		}

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return this.FrameKind == frame.Kind;
		}
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
}
