namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T54 outbound OSC 52 writer without touching the host terminal.
/// </summary>
public sealed class Osc52WriterTests {
	[Theory]
	[InlineData( 0, "48656C6C6F", "1B5D35323B633B534756736247383D1B5C" )]
	[InlineData( 1, "00FF10", "1B5D35323B703B415038511B5C" )]
	[InlineData( 2, "666F6F", "1B5D35323B713B5A6D39761B5C" )]
	[InlineData( 3, "", "1B5D35323B733B1B5C" )]
	public void EncodesCanonicalWriteFrames(
		int selectionValue,
		string payloadHex,
		string expectedFrameHex
	) {
		Assert.Equal(
			Convert.FromHexString( expectedFrameHex ),
			OscWriter.EncodeOsc52WriteFrame(
				(TerminalOsc52Selection)selectionValue,
				Convert.FromHexString( payloadHex )
			)
		);
	}

	[Theory]
	[InlineData( 0, "1B5D35323B633B3F1B5C" )]
	[InlineData( 1, "1B5D35323B703B3F1B5C" )]
	[InlineData( 2, "1B5D35323B713B3F1B5C" )]
	[InlineData( 3, "1B5D35323B733B3F1B5C" )]
	public void EncodesCanonicalQueryFrames(
		int selectionValue,
		string expectedFrameHex
	) {
		Assert.Equal(
			Convert.FromHexString( expectedFrameHex ),
			OscWriter.EncodeOsc52QueryFrame(
				(TerminalOsc52Selection)selectionValue
			)
		);
	}

	[Fact]
	public void ExactMaximumPayloadProducesBoundedCompleteFrame() {
		byte[] payload = new byte[ TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes ];
		for ( int index = 0; index < payload.Length; index++ ) {
			payload[ index ] = (byte)( index & 0xff );
		}

		byte[] frame = OscWriter.EncodeOsc52WriteFrame(
			TerminalOsc52Selection.Clipboard,
			payload
		);

		Assert.Equal(
			TerminalOsc52PayloadCodec.GetWriteFrameLength( payload.Length ),
			frame.Length
		);
		Assert.True( frame.Length <= TerminalOsc52PayloadCodec.MaximumFrameBytes );
		Assert.Equal( 0x1b, frame[ 0 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
	}

	[Fact]
	public void OneByteOverMaximumPayloadIsRejectedBeforeFrameAllocation() {
		byte[] payload = new byte[
			TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes + 1
		];

		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc52WriteFrame(
				TerminalOsc52Selection.Clipboard,
				payload
			)
		);
	}

	[Fact]
	public void InvalidSelectionIsRejectedBeforeFrameCreation() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => OscWriter.EncodeOsc52QueryFrame(
				(TerminalOsc52Selection)int.MaxValue
			)
		);
	}

	[Fact]
	public async Task WriteUsesOneCompleteOutputCallWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc52Async(
			output,
			TerminalOsc52Selection.Clipboard,
			new byte[] { (byte)'H', (byte)'i' }
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5D35323B633B53476B3D1B5C" ),
			output.Bytes.ToArray()
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task QueryUsesOneCompleteOutputCallWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteOsc52QueryAsync(
			output,
			TerminalOsc52Selection.Primary
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5D35323B703B3F1B5C" ),
			output.Bytes.ToArray()
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OversizedWriteProducesZeroProtocolOutput() {
		RecordingTerminalOutput output = new();
		byte[] payload = new byte[
			TerminalOsc52PayloadCodec.MaximumDecodedPayloadBytes + 1
		];

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => OscWriter.WriteOsc52Async(
				output,
				TerminalOsc52Selection.Clipboard,
				payload
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task InvalidSelectionProducesZeroProtocolOutput() {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => OscWriter.WriteOsc52QueryAsync(
				output,
				(TerminalOsc52Selection)int.MaxValue
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CancellationBeforeWriteTransmissionProducesZeroOutput() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc52Async(
				output,
				TerminalOsc52Selection.Clipboard,
				new byte[] { 0x01 },
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CancellationBeforeQueryTransmissionProducesZeroOutput() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteOsc52QueryAsync(
				output,
				TerminalOsc52Selection.Clipboard,
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task OutputFailurePropagatesWithoutRetry() {
		FailingTerminalOutput output = new();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => OscWriter.WriteOsc52Async(
				output,
				TerminalOsc52Selection.Clipboard,
				new byte[] { 0x01 }
			).AsTask()
		);

		Assert.Equal( "simulated output failure", exception.Message );
		Assert.Equal( 1, output.WriteCount );
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

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
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

	private sealed class FailingTerminalOutput : ITerminalOutput {
		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			return ValueTask.FromException(
				new IOException( "simulated output failure" )
			);
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}
}
