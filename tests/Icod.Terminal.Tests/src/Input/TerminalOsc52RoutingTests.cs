namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies T55 bounded OSC framing and selection-aware OSC 52 routing.
/// </summary>
public sealed class TerminalOsc52RoutingTests {
	[Theory]
	[InlineData( "1B5D35323B633B534756736247383D1B5C" )]
	[InlineData( "1B5D35323B633B534756736247383D07" )]
	[InlineData( "9D35323B633B534756736247383D9C" )]
	public void RecognizesFrozenOscTerminationForms(
		string frameHex
	) {
		byte[] frame = Convert.FromHexString( frameHex );

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			frame,
			TerminalResponseFrameKind.Osc,
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Complete, result.Status );
		Assert.Equal( frame.Length, result.Length );
	}

	[Fact]
	public void RejectsBelTerminationForC1OscForm() {
		byte[] frame = [
			0x9D,
			(byte)'5',
			(byte)'2',
			(byte)';',
			(byte)'c',
			(byte)';',
			(byte)'Z',
			(byte)'g',
			(byte)'=',
			(byte)'=',
			0x07
		];

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			frame,
			TerminalResponseFrameKind.Osc,
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, result.Status );
	}

	[Fact]
	public void RejectsSevenBitOscWithBareC1Terminator() {
		byte[] frame = [
			0x1B,
			(byte)']',
			(byte)'5',
			(byte)'2',
			(byte)';',
			(byte)'c',
			(byte)';',
			(byte)'Z',
			(byte)'g',
			(byte)'=',
			(byte)'=',
			0x9C
		];

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			frame,
			TerminalResponseFrameKind.Osc,
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, result.Status );
	}

	[Fact]
	public void CorrelatedMatcherAcceptsOnlyRequestedSelection() {
		ITerminalResponseMatcher clipboardMatcher = TerminalOsc52Protocol.CreateResponseMatcher(
			TerminalOsc52Selection.Clipboard
		);
		TerminalResponseFrame clipboard = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( "\u001b]52;c;SGVsbG8=\u001b\\" )
		);
		TerminalResponseFrame primary = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( "\u001b]52;p;SGVsbG8=\u001b\\" )
		);

		Assert.True( clipboardMatcher.IsMatch( clipboard ) );
		Assert.False( clipboardMatcher.IsMatch( primary ) );
	}

	[Theory]
	[InlineData( "\u001b]51;c;SGVsbG8=\u001b\\" )]
	[InlineData( "\u001b]52;cp;SGVsbG8=\u001b\\" )]
	public void MatcherRejectsUnrelatedOrWronglyScopedOsc(
		string text
	) {
		ITerminalResponseMatcher matcher = TerminalOsc52Protocol.CreateResponseMatcher(
			TerminalOsc52Selection.Clipboard
		);
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( text )
		);

		Assert.False( matcher.IsMatch( frame ) );
	}

	[Theory]
	[InlineData( "\u001b]52;c;SGVsbG8_\u001b\\" )]
	[InlineData( "\u001b]52;c;Zh==\u001b\\" )]
	public void MatcherOwnsCorrelatedMalformedPayloadForDeterministicFailure(
		string text
	) {
		ITerminalResponseMatcher matcher = TerminalOsc52Protocol.CreateResponseMatcher(
			TerminalOsc52Selection.Clipboard
		);
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( text )
		);

		Assert.True( matcher.IsMatch( frame ) );
		Assert.Throws<FormatException>(
			() => TerminalOsc52Protocol.ParsePayload(
				frame,
				TerminalOsc52Selection.Clipboard
			)
		);
	}

	[Fact]
	public void ParsesCorrelatedPayloadBytes() {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Osc,
			Encoding.Latin1.GetBytes( "\u001b]52;c;AAECAwQF/w==\u001b\\" )
		);

		Assert.Equal(
			new byte[] { 0, 1, 2, 3, 4, 5, 255 },
			TerminalOsc52Protocol.ParsePayload(
				frame,
				TerminalOsc52Selection.Clipboard
			)
		);
	}

	[Fact]
	public async Task FragmentedSevenBitOsc52ResponseUsesSharedDecoderPath() {
		byte[] response = Encoding.Latin1.GetBytes( "\u001b]52;c;SGVsbG8=\u001b\\" );
		List<byte[]> chunks = response
			.Select( static value => new[] { value } )
			.ToList();
		chunks.Add( Encoding.UTF8.GetBytes( "x" ) );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( chunks )
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			TerminalOsc52Protocol.CreateResponseMatcher(
				TerminalOsc52Selection.Clipboard
			)
		);

		TerminalInputEvent trailing = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'x' ), trailing.Character );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "Hello" ),
			TerminalOsc52Protocol.ParsePayload(
				frame,
				TerminalOsc52Selection.Clipboard
			)
		);
	}

	[Fact]
	public async Task C1Osc52ResponseUsesSharedDecoderPath() {
		byte[] response = [
			0x9D,
			(byte)'5',
			(byte)'2',
			(byte)';',
			(byte)'s',
			(byte)';',
			(byte)'Z',
			(byte)'g',
			(byte)'=',
			(byte)'=',
			0x9C
		];
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				[
					response,
					Encoding.UTF8.GetBytes( "y" )
				]
			)
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			TerminalOsc52Protocol.CreateResponseMatcher(
				TerminalOsc52Selection.Select
			)
		);

		TerminalInputEvent trailing = await decoder.ReadAsync();
		TerminalResponseFrame frame = await expectation.Response;

		Assert.True( frame.UsesEightBitIntroducer );
		Assert.Equal( response, frame.Bytes.ToArray() );
		Assert.Equal( new Rune( 'y' ), trailing.Character );
		Assert.Equal(
			new byte[] { (byte)'f' },
			TerminalOsc52Protocol.ParsePayload(
				frame,
				TerminalOsc52Selection.Select
			)
		);
	}

	[Fact]
	public async Task ExactMaximumPayloadRoutesWithinFrozenBound() {
		byte[] payload = new byte[ TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes ];
		for ( int index = 0; index < payload.Length; index++ ) {
			payload[ index ] = (byte)( index & 0xff );
		}
		string encoded = TerminalOsc52PayloadCodec.Encode( payload );
		byte[] response = Encoding.ASCII.GetBytes(
			$"\u001b]52;c;{encoded}\u001b\\"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( Split( response, 256 ) )
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			TerminalOsc52Protocol.CreateResponseMatcher(
				TerminalOsc52Selection.Clipboard
			)
		);

		Task<TerminalResponseFrame> responseTask = expectation.Response;
		Task<TerminalInputEvent> readTask = decoder.ReadAsync().AsTask();
		TerminalResponseFrame frame = await responseTask;

		Assert.Equal( response.Length, frame.Bytes.Length );
		Assert.Equal(
			payload,
			TerminalOsc52Protocol.ParsePayload(
				frame,
				TerminalOsc52Selection.Clipboard
			)
		);

		TerminalInputEvent end = await readTask;
		Assert.Equal( TerminalInputEventKind.EndOfInput, end.Kind );
	}

	[Fact]
	public void OscFrameCannotExceedFrozenMaximum() {
		byte[] bytes = Enumerable.Repeat(
			(byte)'A',
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		).ToArray();
		bytes[ 0 ] = 0x1B;
		bytes[ 1 ] = (byte)']';

		TerminalResponseFrameParseResult result = TerminalResponseFramer.Parse(
			bytes,
			TerminalResponseFrameKind.Osc,
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Invalid, result.Status );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input
	) {
		ArgumentNullException.ThrowIfNull( input );

		return new TerminalInputDecoder(
			input,
			new TerminalDescriptionBuilder( "osc52-routing" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes
		);
	}

	private static IEnumerable<byte[]> Split(
		byte[] bytes,
		int size
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 0 >= size ) {
			throw new ArgumentOutOfRangeException( nameof( size ) );
		}

		for ( int offset = 0; offset < bytes.Length; offset += size ) {
			int length = Math.Min( size, bytes.Length - offset );
			yield return bytes.AsSpan( offset, length ).ToArray();
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
