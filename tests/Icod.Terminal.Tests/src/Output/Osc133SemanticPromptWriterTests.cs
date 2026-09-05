namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies byte-exact T121 OSC 133 semantic-prompt framing and commit behavior.
/// </summary>
public sealed class Osc133SemanticPromptWriterTests {
	[Fact]
	public void PromptStartUsesCanonicalStFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;A\u001b\\" ),
			OscWriter.EncodeOsc133PromptStartFrame()
		);
	}

	[Fact]
	public void CommandInputStartUsesCanonicalStFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;B\u001b\\" ),
			OscWriter.EncodeOsc133CommandInputStartFrame()
		);
	}

	[Fact]
	public void CommandOutputStartUsesCanonicalStFrame() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;C\u001b\\" ),
			OscWriter.EncodeOsc133CommandOutputStartFrame()
		);
	}

	[Fact]
	public void CommandAbortUsesBareDAndIsDistinctFromSuccess() {
		byte[] aborted = OscWriter.EncodeOsc133CommandAbortedFrame();
		byte[] success = OscWriter.EncodeOsc133CommandFinishedFrame( 0 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D\u001b\\" ),
			aborted
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;0\u001b\\" ),
			success
		);
		Assert.NotEqual( aborted, success );
	}

	[Theory]
	[InlineData( 0, "\u001b]133;D;0\u001b\\" )]
	[InlineData( 1, "\u001b]133;D;1\u001b\\" )]
	[InlineData( 9, "\u001b]133;D;9\u001b\\" )]
	[InlineData( 10, "\u001b]133;D;10\u001b\\" )]
	[InlineData( 99, "\u001b]133;D;99\u001b\\" )]
	[InlineData( 100, "\u001b]133;D;100\u001b\\" )]
	[InlineData( 255, "\u001b]133;D;255\u001b\\" )]
	public void CompletionEncodesDecimalByteExactly(
		int exitStatus,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			OscWriter.EncodeOsc133CommandFinishedFrame( (byte)exitStatus )
		);
	}

	[Fact]
	public async Task WriterCommitsExactlyOneNoncancellableWriteWithoutFlush() {
		RecordingOutput output = new();

		await OscWriter.WriteOsc133CommandFinishedAsync(
			output,
			127,
			CancellationToken.None
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;127\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledWriterEmitsNothing() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc133PromptStartAsync(
				output,
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task NullOutputIsRejectedBeforeEmission() {
		await Assert.ThrowsAsync<ArgumentNullException>(
			() => OscWriter.WriteOsc133CommandAbortedAsync(
				null!
			).AsTask()
		);
	}

	private sealed class RecordingOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.WriteCancellationTokens.Add( cancellationToken );
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
