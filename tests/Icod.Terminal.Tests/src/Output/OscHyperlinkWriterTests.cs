namespace Icod.Terminal.Tests.Output;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the T46 OSC 8 writer integration without touching the host terminal.
/// </summary>
public sealed class OscHyperlinkWriterTests {
	[Theory]
	[InlineData( "https://example.com/", null, "1B5D383B3B68747470733A2F2F6578616D706C652E636F6D2F1B5C" )]
	[InlineData( "https://example.com/path?q=v#part", "link-1", "1B5D383B69643D6C696E6B2D313B68747470733A2F2F6578616D706C652E636F6D2F706174683F713D7623706172741B5C" )]
	[InlineData( "mailto:user@example.com", "mail", "1B5D383B69643D6D61696C3B6D61696C746F3A75736572406578616D706C652E636F6D1B5C" )]
	[InlineData( "file:///tmp/report.txt", "", "1B5D383B3B66696C653A2F2F2F746D702F7265706F72742E7478741B5C" )]
	[InlineData( "custom-scheme:value", null, "1B5D383B3B637573746F6D2D736368656D653A76616C75651B5C" )]
	public void EncodesExpectedHyperlinkBeginFrames(
		string uri,
		string? identifier,
		string expectedHex
	) {
		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			OscWriter.EncodeHyperlinkBeginFrame(
				uri,
				identifier
			)
		);
	}

	[Fact]
	public void NormalizesPercentEscapesBeforeFraming() {
		Assert.Equal(
			Convert.FromHexString(
				"1B5D383B69643D6C696E6B3B68747470733A2F2F6578616D706C652E636F6D2F2532462537451B5C"
			),
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/%2f%7e",
				"link"
			)
		);
	}

	[Fact]
	public void EncodesCanonicalHyperlinkEndFrame() {
		Assert.Equal(
			Convert.FromHexString( "1B5D383B3B1B5C" ),
			OscWriter.EncodeHyperlinkEndFrame()
		);
	}

	[Fact]
	public async Task WritesCompleteBeginFrameInOneCallWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteHyperlinkBeginAsync(
			output,
			"https://example.com/",
			"link"
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString(
				"1B5D383B69643D6C696E6B3B68747470733A2F2F6578616D706C652E636F6D2F1B5C"
			),
			output.Bytes.ToArray()
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task WritesCompleteEndFrameInOneCallWithoutFlush() {
		RecordingTerminalOutput output = new();

		await OscWriter.WriteHyperlinkEndAsync( output );

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			Convert.FromHexString( "1B5D383B3B1B5C" ),
			output.Bytes.ToArray()
		);
		Assert.Equal( 0, output.FlushCount );
	}

	[Theory]
	[InlineData( "relative/path", null )]
	[InlineData( "https://example.com/bad%2", null )]
	[InlineData( "https://example.com/", "bad:id" )]
	[InlineData( "https://example.com/", "bad;id" )]
	public async Task RejectedBeginWritesNothing(
		string uri,
		string? identifier
	) {
		RecordingTerminalOutput output = new();

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteHyperlinkBeginAsync(
				output,
				uri,
				identifier
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OversizedUriWritesNothing() {
		RecordingTerminalOutput output = new();
		string uri = "x:" + new string(
			'a',
			TerminalHyperlinkEncoder.MaximumUriByteCount - 1
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => OscWriter.WriteHyperlinkBeginAsync(
				output,
				uri
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CancellationBeforeBeginTransmissionWritesNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteHyperlinkBeginAsync(
				output,
				"https://example.com/",
				cancellationToken: cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CancellationBeforeEndTransmissionWritesNothing() {
		RecordingTerminalOutput output = new();
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => OscWriter.WriteHyperlinkEndAsync(
				output,
				cancellationSource.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task BeginOutputFailurePropagates() {
		FailingTerminalOutput output = new();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => OscWriter.WriteHyperlinkBeginAsync(
				output,
				"https://example.com/"
			).AsTask()
		);

		Assert.Equal( "simulated output failure", exception.Message );
		Assert.Equal( 1, output.WriteCount );
	}

	[Fact]
	public async Task EndOutputFailurePropagates() {
		FailingTerminalOutput output = new();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => OscWriter.WriteHyperlinkEndAsync( output ).AsTask()
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
