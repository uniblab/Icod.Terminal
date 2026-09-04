namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T39 OSC 7 writer integration without touching the host terminal.
/// </summary>
public sealed class OscLocationWriterTests {
	[Theory]
	[InlineData( "/", 0, null, "1B5D373B66696C653A2F2F2F1B5C" )]
	[InlineData( "/usr/local/src", 0, null, "1B5D373B66696C653A2F2F2F7573722F6C6F63616C2F7372631B5C" )]
	[InlineData( "C:\\Development\\Icod", 1, null, "1B5D373B66696C653A2F2F2F433A2F446576656C6F706D656E742F49636F641B5C" )]
	[InlineData( "\\\\server\\share\\dir", 2, null, "1B5D373B66696C653A2F2F7365727665722F73686172652F6469721B5C" )]
	[InlineData( "/usr/src", 0, "example.com", "1B5D373B66696C653A2F2F6578616D706C652E636F6D2F7573722F7372631B5C" )]
	public void EncodesExpectedLocationFrames(
		string path,
		int pathKindValue,
		string? authority,
		string expectedHex
	) {
		byte[] actual = OscWriter.EncodeLocationFrame(
			path,
			(TerminalLocationPathKind)pathKindValue,
			authority
		);

		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			actual
		);
	}

	[Fact]
	public void EncodesUriEscapingBeforeFraming() {
		Assert.Equal(
			Convert.FromHexString(
				"1B5D373B66696C653A2F2F2F4D7925323050726F6A6563742F25323532302F2545372538432541421B5C"
			),
			OscWriter.EncodeLocationFrame(
				"/My Project/%20/猫",
				TerminalLocationPathKind.Posix
			)
		);
	}

	[Fact]
	public async Task WritesCompleteLocationFrameInOneCall() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteLocationAsync(
			output,
			"/usr/local/src",
			TerminalLocationPathKind.Posix
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString(
				"1B5D373B66696C653A2F2F2F7573722F6C6F63616C2F7372631B5C"
			),
			output.Bytes.ToArray()
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task RejectedLocationWritesNothing() {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteLocationAsync(
				output,
				"relative/path",
				TerminalLocationPathKind.Posix
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OversizedLocationWritesNothing() {
		RecordingTerminalOutput output = new();
		const int fixedLength = 8;
		string path = "/" + new string(
			'a',
			TerminalLocationUriEncoder.MaximumEncodedUriByteCount - fixedLength + 1
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteLocationAsync(
				output,
				path,
				TerminalLocationPathKind.Posix
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CancellationBeforeLocationTransmissionWritesNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteLocationAsync(
				output,
				"/usr/src",
				TerminalLocationPathKind.Posix,
				cancellationToken: cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task OutputFailurePropagates() {
		FailingTerminalOutput output = new();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => OscWriter.WriteLocationAsync(
				output,
				"/usr/src",
				TerminalLocationPathKind.Posix
			).AsTask()
		);

		Assert.Equal(
			"simulated output failure",
			exception.Message
		);
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
