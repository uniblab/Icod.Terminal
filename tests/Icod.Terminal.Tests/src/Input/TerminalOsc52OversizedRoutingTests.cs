namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Verifies oversized correlated OSC 52 resynchronization across all accepted terminators.
/// </summary>
public sealed class TerminalOsc52OversizedRoutingTests {
	[Fact]
	public async Task OversizedSevenBitBelResponseFailsAndPreservesTrailingInput() {
		byte[] oversized = CreateOversizedSevenBitPrefix();
		List<byte[]> chunks = Split( oversized, 256 ).ToList();
		chunks.Add(
			[
				0x07,
				(byte)'b'
			]
		);

		await AssertOversizedFailureAndTrailingInputAsync(
			chunks,
			new Rune( 'b' )
		);
	}

	[Fact]
	public async Task OversizedC1ResponseFailsAndPreservesTrailingInput() {
		byte[] oversized = Enumerable.Repeat(
			(byte)'A',
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		).ToArray();
		byte[] prefix = [
			0x9D,
			(byte)'5',
			(byte)'2',
			(byte)';',
			(byte)'c',
			(byte)';'
		];
		prefix.CopyTo( oversized, 0 );

		List<byte[]> chunks = Split( oversized, 256 ).ToList();
		chunks.Add(
			[
				0x9C,
				(byte)'c'
			]
		);

		await AssertOversizedFailureAndTrailingInputAsync(
			chunks,
			new Rune( 'c' )
		);
	}

	[Fact]
	public async Task OversizedSevenBitResponseHandlesFragmentedStringTerminator() {
		byte[] oversized = CreateOversizedSevenBitPrefix();
		List<byte[]> chunks = Split( oversized, 256 ).ToList();
		chunks.Add( [ 0x1B ] );
		chunks.Add(
			[
				(byte)'\\',
				(byte)'s'
			]
		);

		await AssertOversizedFailureAndTrailingInputAsync(
			chunks,
			new Rune( 's' )
		);
	}

	private static async Task AssertOversizedFailureAndTrailingInputAsync(
		IEnumerable<byte[]> chunks,
		Rune expectedTrailingRune
	) {
		ArgumentNullException.ThrowIfNull( chunks );

		TerminalInputDecoder decoder = new(
			new ScriptedTerminalInput( chunks ),
			new TerminalDescriptionBuilder( "osc52-oversized-routing" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromMilliseconds( 50 ),
			TerminalSession.MaximumBufferedInputBytes
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			TerminalOsc52Protocol.CreateResponseMatcher(
				TerminalOsc52Selection.Clipboard
			)
		);

		Task<TerminalInputEvent> readTask = decoder.ReadAsync().AsTask();
		await Assert.ThrowsAsync<FormatException>(
			() => expectation.Response
		);

		TerminalInputEvent trailing = await readTask;
		Assert.Equal( expectedTrailingRune, trailing.Character );
	}

	private static byte[] CreateOversizedSevenBitPrefix() {
		byte[] oversized = Enumerable.Repeat(
			(byte)'A',
			TerminalOsc52PayloadCodec.MaximumFrameBytes
		).ToArray();
		Encoding.ASCII.GetBytes( "\u001b]52;c;" ).CopyTo( oversized, 0 );
		return oversized;
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
