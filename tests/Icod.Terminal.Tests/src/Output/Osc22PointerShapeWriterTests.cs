namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T111 byte-exact OSC 22 pointer-shape writer contract.
/// </summary>
public sealed class Osc22PointerShapeWriterTests {
	[Theory]
	[InlineData( "pointer" )]
	[InlineData( "text" )]
	[InlineData( "wait" )]
	[InlineData( "default" )]
	[InlineData( "ew-resize" )]
	[InlineData( "zoom-out" )]
	public void EncodesCanonicalShapeWithStringTerminator(
		string shapeName
	) {
		byte[] expected = Encoding.ASCII.GetBytes(
			$"\u001b]22;{shapeName}\u001b\\"
		);

		Assert.Equal(
			expected,
			OscWriter.EncodeOsc22PointerShapeFrame( shapeName )
		);
	}

	[Fact]
	public void EncodesTerminalPolicyResetAsEmptyPayload() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]22;\u001b\\" ),
			OscWriter.EncodeOsc22PointerShapeFrame( null )
		);
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "xterm" )]
	[InlineData( "hand2" )]
	[InlineData( "Pointer" )]
	[InlineData( "no-such-shape" )]
	[InlineData( "pointer,wait" )]
	[InlineData( ">wait" )]
	[InlineData( "?__current__" )]
	public void RejectsNonCanonicalBaseSetterPayloads(
		string shapeName
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc22PointerShapeFrame( shapeName )
		);
	}

	[Fact]
	public async Task WriterUsesExactlyOneNonCallerCancellableWriteAndNoFlush() {
		RecordingOutput output = new();

		await OscWriter.WriteOsc22PointerShapeAsync(
			output,
			"pointer"
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]22;pointer\u001b\\" ),
			output.LastWrite
		);
		Assert.False( output.LastWriteCancellationCanBeCanceled );
	}

	[Fact]
	public async Task PreCancelledWriterEmitsNothing() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc22PointerShapeAsync(
				output,
				"pointer",
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task InvalidShapeEmitsNothing() {
		RecordingOutput output = new();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => OscWriter.WriteOsc22PointerShapeAsync(
				output,
				"hand2"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private int flushCount;
		private int writeCount;

		internal int WriteCount {
			get {
				return Volatile.Read( ref this.writeCount );
			}
		}

		internal int FlushCount {
			get {
				return Volatile.Read( ref this.flushCount );
			}
		}

		internal byte[] LastWrite {
			get;
			private set;
		} = [];

		internal bool LastWriteCancellationCanBeCanceled {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.LastWrite = buffer.ToArray();
			this.LastWriteCancellationCanBeCanceled = cancellationToken.CanBeCanceled;
			Interlocked.Increment( ref this.writeCount );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment( ref this.flushCount );
			return ValueTask.CompletedTask;
		}
	}
}
