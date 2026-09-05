namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T101 internal OSC 9;4 progress writer contract with byte-exact fixtures.
/// </summary>
public sealed class Osc9ProgressWriterTests {
	[Theory]
	[InlineData( 0, 0, "1B5D393B343B303B3007" )]
	[InlineData( 1, 0, "1B5D393B343B313B3007" )]
	[InlineData( 1, 9, "1B5D393B343B313B3907" )]
	[InlineData( 1, 10, "1B5D393B343B313B313007" )]
	[InlineData( 1, 99, "1B5D393B343B313B393907" )]
	[InlineData( 1, 100, "1B5D393B343B313B31303007" )]
	[InlineData( 2, 42, "1B5D393B343B323B343207" )]
	[InlineData( 3, 0, "1B5D393B343B333B3007" )]
	[InlineData( 4, 75, "1B5D393B343B343B373507" )]
	public void EncodesExpectedFrames(
		int stateValue,
		int progress,
		string expectedHex
	) {
		byte[] actual = OscWriter.EncodeOsc9ProgressFrame(
			(Osc9ProgressState)stateValue,
			progress
		);

		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			actual
		);
	}

	[Fact]
	public void RejectsUnknownState() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc9ProgressFrame(
				(Osc9ProgressState)5,
				0
			)
		);
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 101 )]
	public void RejectsProgressOutsideWireRange(
		int progress
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc9ProgressFrame(
				Osc9ProgressState.Normal,
				progress
			)
		);
	}

	[Theory]
	[InlineData( 0, 1 )]
	[InlineData( 0, 100 )]
	[InlineData( 3, 1 )]
	[InlineData( 3, 100 )]
	public void RejectsNonCanonicalProgressForNonDeterminateStates(
		int stateValue,
		int progress
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc9ProgressFrame(
				(Osc9ProgressState)stateValue,
				progress
			)
		);
	}

	[Fact]
	public async Task WritesCompleteFrameOnceWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc9ProgressAsync(
			output,
			Osc9ProgressState.Error,
			42
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Convert.FromHexString( "1B5D393B343B323B343207" ),
			output.Bytes.ToArray()
		);
		Assert.False( output.LastWriteCancellationToken.CanBeCanceled );
	}

	[Fact]
	public async Task PreCancelledWriteEmitsNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc9ProgressAsync(
				output,
				Osc9ProgressState.Normal,
				50,
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task InvalidArgumentsEmitNothing() {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => OscWriter.WriteOsc9ProgressAsync(
				output,
				Osc9ProgressState.Indeterminate,
				1
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Empty( output.Bytes );
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte> Bytes {
			get;
		} = [];

		internal int WriteCount {
			get;
			private set;
		}

		internal int FlushCount {
			get;
			private set;
		}

		internal CancellationToken LastWriteCancellationToken {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			++this.WriteCount;
			this.LastWriteCancellationToken = cancellationToken;
			cancellationToken.ThrowIfCancellationRequested();
			this.Bytes.AddRange( buffer.ToArray() );
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
