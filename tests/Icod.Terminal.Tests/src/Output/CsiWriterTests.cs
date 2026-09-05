namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies structural CSI framing, DECSCUSR output, and T91 synchronized-output framing.
/// </summary>
public sealed class CsiWriterTests {
	[Theory]
	[InlineData( 1, "1B5B312071" )]
	[InlineData( 2, "1B5B322071" )]
	[InlineData( 3, "1B5B332071" )]
	[InlineData( 4, "1B5B342071" )]
	[InlineData( 5, "1B5B352071" )]
	[InlineData( 6, "1B5B362071" )]
	public void EncodesFrozenCursorStyleParameters(
		int parameter,
		string expectedHex
	) {
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			CsiWriter.EncodeCursorStyleFrame( parameter )
		);
	}

	[Fact]
	public void EncodesStructuralParameterIntermediateAndFinalFields() {
		byte[] frame = CsiWriter.EncodeFrame(
			[ (byte)'?', (byte)'2', (byte)'0', (byte)'2', (byte)'6' ],
			[ (byte)'$' ],
			(byte)'p'
		);

		Assert.Equal(
			Convert.FromHexString( "1B5B3F323032362470" ),
			frame
		);
	}

	[Fact]
	public void EncodesSynchronizedOutputBeginFrame() {
		Assert.Equal(
			Convert.FromHexString( "1B5B3F3230323668" ),
			CsiWriter.EncodeSynchronizedOutputBeginFrame()
		);
	}

	[Fact]
	public void EncodesSynchronizedOutputEndFrame() {
		Assert.Equal(
			Convert.FromHexString( "1B5B3F323032366C" ),
			CsiWriter.EncodeSynchronizedOutputEndFrame()
		);
	}

	[Fact]
	public void RejectsParameterBytesOutsideStructuralRange() {
		Assert.Throws<ArgumentException>(
			() => CsiWriter.EncodeFrame(
				[ (byte)'/' ],
				ReadOnlySpan<byte>.Empty,
				(byte)'A'
			)
		);
	}

	[Fact]
	public void RejectsIntermediateBytesOutsideStructuralRange() {
		Assert.Throws<ArgumentException>(
			() => CsiWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				[ (byte)'0' ],
				(byte)'A'
			)
		);
	}

	[Theory]
	[InlineData( 0x3F )]
	[InlineData( 0x7F )]
	public void RejectsFinalBytesOutsideStructuralRange(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => CsiWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				ReadOnlySpan<byte>.Empty,
				(byte)value
			)
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 7 )]
	[InlineData( -1 )]
	public void RejectsCursorStyleParametersOutsideFrozenRange(
		int parameter
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => CsiWriter.EncodeCursorStyleFrame( parameter )
		);
	}

	[Fact]
	public async Task CursorStyleWriteUsesOneNonCancellableTransportWrite() {
		RecordingOutput output = new();

		await CsiWriter.WriteCursorStyleAsync(
			output,
			6
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5B362071" ),
			output.GetWrite( 0 )
		);
		Assert.False( output.LastCancellationToken.CanBeCanceled );
	}

	[Fact]
	public async Task CursorStyleWriteHonorsCancellationBeforeCommit() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => CsiWriter.WriteCursorStyleAsync(
				output,
				1,
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task SynchronizedOutputBeginUsesOneNonCancellableTransportWrite() {
		RecordingOutput output = new();

		await CsiWriter.WriteSynchronizedOutputBeginAsync( output );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5B3F3230323668" ),
			output.GetWrite( 0 )
		);
		Assert.False( output.LastCancellationToken.CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SynchronizedOutputEndUsesOneNonCancellableTransportWrite() {
		RecordingOutput output = new();

		await CsiWriter.WriteSynchronizedOutputEndAsync( output );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5B3F323032366C" ),
			output.GetWrite( 0 )
		);
		Assert.False( output.LastCancellationToken.CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SynchronizedOutputBeginHonorsCancellationBeforeCommit() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => CsiWriter.WriteSynchronizedOutputBeginAsync(
				output,
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );
	}

	[Fact]
	public async Task SynchronizedOutputEndHonorsCancellationBeforeCommit() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => CsiWriter.WriteSynchronizedOutputEndAsync(
				output,
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, output.WriteCount );
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private readonly List<byte[]> writes = [];

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal int FlushCount {
			get;
			private set;
		}

		internal CancellationToken LastCancellationToken {
			get;
			private set;
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.LastCancellationToken = cancellationToken;
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}
}
